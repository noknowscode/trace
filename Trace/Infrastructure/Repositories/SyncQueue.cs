using Trace.Domain.Enums;
using Trace.Domain.Models;
using Trace.Infrastructure.Persistence;
using Trace.Services;

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

    public async ValueTask<SyncResult> SyncAsync(CancellationToken ct)
    {
        if (!_network.IsOnline)
        {
            return new SyncResult(0, 0, WasOffline: true);
        }

        var succeeded = 0;
        var failed = 0;

        await _store.MutateAsync(
            snapshot =>
            {
                var settledEventIds = new List<string>();
                var queue = snapshot.Queue.ToList();

                for (var i = 0; i < queue.Count; i++)
                {
                    var operation = queue[i];
                    if (operation.Status is not (SyncState.Pending or SyncState.Failed))
                    {
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
                        continue;
                    }

                    succeeded++;
                    queue[i] = operation with
                    {
                        Status = SyncState.Synced,
                        AttemptCount = attempt,
                        Error = null,
                    };

                    if (operation.EntityType == nameof(InspectionEvent))
                    {
                        settledEventIds.Add(operation.EntityId);
                    }
                }

                if (succeeded == 0 && failed == 0)
                {
                    return snapshot;
                }

                var trimmed = Trim(queue.ToImmutableList());
                var events = settledEventIds.Count == 0
                    ? snapshot.Events
                    : snapshot.Events
                        .Select(e => settledEventIds.Contains(e.Id)
                            ? e with { SyncState = SyncState.Synced }
                            : e)
                        .ToImmutableList();

                return snapshot with { Queue = trimmed, Events = events };
            },
            ct);

        _log.LogInformation("Sync pass finished: {Succeeded} succeeded, {Failed} failed.", succeeded, failed);
        return new SyncResult(succeeded, failed, WasOffline: false);
    }

    public async ValueTask RetryAsync(string operationId, CancellationToken ct) =>
        await _store.MutateAsync(
            snapshot => snapshot with
            {
                Queue = snapshot.Queue
                    .Select(o => o.Id == operationId && o.Status == SyncState.Failed
                        ? o with { Status = SyncState.Pending, Error = null }
                        : o)
                    .ToImmutableList(),
            },
            ct);

    public async ValueTask RetryAllFailedAsync(CancellationToken ct) =>
        await _store.MutateAsync(
            snapshot => snapshot.Queue.Any(o => o.Status == SyncState.Failed)
                ? snapshot with
                {
                    Queue = snapshot.Queue
                        .Select(o => o.Status == SyncState.Failed
                            ? o with { Status = SyncState.Pending, Error = null }
                            : o)
                        .ToImmutableList(),
                }
                : snapshot,
            ct);

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
