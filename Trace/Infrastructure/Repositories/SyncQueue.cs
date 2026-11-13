using Trace.Infrastructure.Persistence;

namespace Trace.Infrastructure.Repositories;

/// <summary>
/// The durable outbox and its simulated transport.
/// </summary>
/// <remarks>
/// There is no server behind TRACE, so a sync pass resolves each operation locally. The
/// rule is deterministic rather than random: an operation carrying photo evidence fails on
/// its first attempt — a large upload dropping on a weak field link is the failure that
/// actually happens — and succeeds on retry. Everything else succeeds immediately. That
/// makes the failed and retry states reachable in a demo without pretending a network
/// round-trip took place.
///
/// Order holds as it would against a server: inspections are sent first, corrective work only
/// once the inspection it came from has gone, and a record's later change only after its earlier
/// one. Work that has to wait stays Pending, unattempted.
/// </remarks>
public sealed class SyncQueue : ISyncQueue
{
    /// <summary>How many settled operations stay visible before the oldest are dropped.</summary>
    private const int SyncedHistoryLimit = 20;

    private readonly ITraceStore _store;
    private readonly INetworkStatusService _network;
    private readonly ILogger<SyncQueue> _log;

    public SyncQueue(ITraceStore store, INetworkStatusService network, ILogger<SyncQueue> log)
    {
        _store = store;
        _network = network;
        _log = log;
    }

    public IAsyncEnumerable<IImmutableList<SyncOperation>> Watch(CancellationToken ct) =>
        StoreProjections.ProjectList(
            _store.Watch(ct),
            s => s.Queue
                .OrderBy(o => o.Status == SyncState.Failed ? 0 : o.Status == SyncState.Pending ? 1 : 2)
                .ThenByDescending(o => o.CreatedAt)
                .ToImmutableList(),
            ct);

    public IAsyncEnumerable<int> WatchPendingCount(CancellationToken ct) =>
        StoreProjections.Project(_store.Watch(ct), s => s.PendingSyncCount, ct);

    public IAsyncEnumerable<IImmutableList<string>> WatchSetAside(CancellationToken ct) =>
        StoreProjections.ProjectList(_store.Watch(ct), s => s.SetAside, ct);

    public async ValueTask<SyncResult> SyncAsync(CancellationToken ct)
    {
        if (!_network.IsOnline)
        {
            return new SyncResult(0, 0, WasOffline: true);
        }

        var succeeded = 0;
        var failed = 0;
        var held = 0;

        await _store.MutateAsync(
            snapshot =>
            {
                // What each inspection's own record says after this pass: Synced once its upload
                // went, Failed while it did not, so History never shows a failed upload as Pending.
                var eventStates = new Dictionary<string, SyncState>();
                var queue = snapshot.Queue.ToList();

                // Inspections go first: corrective work refers to the inspection it came from, so
                // it may leave the device only after that inspection has (OrderBy is stable).
                var order = Enumerable.Range(0, queue.Count)
                    .OrderBy(i => queue[i].EntityType == nameof(InspectionEvent) ? 0 : 1)
                    .ToList();

                foreach (var i in order)
                {
                    var operation = queue[i];
                    if (operation.Status is not (SyncState.Pending or SyncState.Failed))
                    {
                        continue;
                    }

                    if (WaitsForEarlierWork(i, queue, snapshot.Actions))
                    {
                        // Not attempted: it stays Pending and goes on a later pass.
                        held++;
                        continue;
                    }

                    var attempt = operation.AttemptCount + 1;

                    if (operation.HasAttachments && operation.AttemptCount == 0)
                    {
                        failed++;
                        queue[i] = operation with
                        {
                            Status = SyncState.Failed,
                            AttemptCount = attempt,
                            Error = "Evidence upload interrupted. Retry when the signal improves.",
                        };
                        Settle(operation, SyncState.Failed);
                        continue;
                    }

                    succeeded++;
                    queue[i] = operation with
                    {
                        Status = SyncState.Synced,
                        AttemptCount = attempt,
                        Error = null,
                    };
                    Settle(operation, SyncState.Synced);
                }

                if (succeeded == 0 && failed == 0)
                {
                    return snapshot;
                }

                return snapshot with
                {
                    Queue = Trim(queue.ToImmutableList()),
                    Events = WithSyncStates(snapshot.Events, eventStates),
                };

                void Settle(SyncOperation operation, SyncState state)
                {
                    if (operation.EntityType == nameof(InspectionEvent))
                    {
                        eventStates[operation.EntityId] = state;
                    }
                }
            },
            ct);

        _log.LogInformation(
            "Sync pass finished: {Succeeded} succeeded, {Failed} failed, {Held} held for their inspection.",
            succeeded, failed, held);
        return new SyncResult(succeeded, failed, WasOffline: false, Held: held);
    }

    public async ValueTask RetryAllFailedAsync(CancellationToken ct) =>
        await _store.MutateAsync(
            snapshot =>
            {
                var retried = snapshot.Queue.Where(o => o.Status == SyncState.Failed).ToList();
                if (retried.Count == 0)
                {
                    return snapshot;
                }

                // A failed inspection is queued again, and its record says so.
                var events = retried
                    .Where(o => o.EntityType == nameof(InspectionEvent))
                    .ToDictionary(o => o.EntityId, _ => SyncState.Pending);

                return snapshot with
                {
                    Queue = snapshot.Queue
                        .Select(o => o.Status == SyncState.Failed
                            ? o with { Status = SyncState.Pending, Error = null }
                            : o)
                        .ToImmutableList(),
                    Events = WithSyncStates(snapshot.Events, events),
                };
            },
            ct);

    /// <summary>
    /// True when the operation at <paramref name="index"/> must wait: an earlier change to the same
    /// record has not synced yet (an action's status update waits for the action), or it is
    /// corrective work whose source inspection is still pending or failed.
    /// </summary>
    private static bool WaitsForEarlierWork(int index, List<SyncOperation> queue, IImmutableList<CorrectiveAction> actions)
    {
        var operation = queue[index];

        if (queue.Take(index).Any(o => IsOutstanding(o) && o.EntityType == operation.EntityType && o.EntityId == operation.EntityId))
        {
            return true;
        }

        if (operation.EntityType != nameof(CorrectiveAction)
            || actions.FirstOrDefault(a => a.Id == operation.EntityId) is not { } action)
        {
            return false;
        }

        return queue.Any(o => IsOutstanding(o)
            && o.EntityType == nameof(InspectionEvent)
            && o.EntityId == action.SourceInspectionEventId);
    }

    private static bool IsOutstanding(SyncOperation operation) =>
        operation.Status is SyncState.Pending or SyncState.Failed;

    private static IImmutableList<InspectionEvent> WithSyncStates(
        IImmutableList<InspectionEvent> events,
        IReadOnlyDictionary<string, SyncState> states) =>
        states.Count == 0
            ? events
            : events
                .Select(e => states.TryGetValue(e.Id, out var state) && e.SyncState != state ? e with { SyncState = state } : e)
                .ToImmutableList();

    /// <summary>
    /// Enqueues a write. Called from the inspection workflow inside the same store mutation
    /// as the record itself, so a record can never exist locally without its outbox entry.
    /// </summary>
    internal static IImmutableList<SyncOperation> Enqueue(
        IImmutableList<SyncOperation> queue,
        string entityType,
        string entityId,
        string operation,
        string description,
        bool hasAttachments,
        DateTimeOffset at) =>
        Trim(queue.Add(new SyncOperation(
            Id: $"op-{Guid.NewGuid():N}",
            EntityType: entityType,
            EntityId: entityId,
            Operation: operation,
            Description: description,
            CreatedAt: at,
            Status: SyncState.Pending,
            AttemptCount: 0,
            HasAttachments: hasAttachments,
            Error: null)));

    private static IImmutableList<SyncOperation> Trim(IImmutableList<SyncOperation> queue)
    {
        var synced = queue.Where(o => o.Status == SyncState.Synced).ToList();
        if (synced.Count <= SyncedHistoryLimit)
        {
            return queue;
        }

        var drop = synced
            .OrderByDescending(o => o.CreatedAt)
            .Skip(SyncedHistoryLimit)
            .Select(o => o.Id)
            .ToHashSet();

        return queue.Where(o => !drop.Contains(o.Id)).ToImmutableList();
    }
}
