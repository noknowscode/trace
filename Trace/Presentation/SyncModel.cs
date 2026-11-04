namespace Trace.Presentation;

/// <summary>
/// What has not left the device. TRACE never infers sync from connectivity, so this screen
/// is the honest answer to "is my work safe yet".
/// </summary>
public partial record SyncModel
{
    private readonly ISyncQueue _queue;
    private readonly INetworkStatusService _network;
    private readonly FeedRecovery _recovery;

    public SyncModel(ISyncQueue queue, INetworkStatusService network, FeedRecovery recovery)
    {
        _queue = queue;
        _network = network;
        _recovery = recovery;
    }

    public IListFeed<SyncOperation> Operations => _recovery.WatchList(_queue.Watch);

    public IFeed<int> PendingCount => _recovery.Watch(_queue.WatchPendingCount);

    public IFeed<bool> HasPending => PendingCount.Select(count => count > 0);

    /// <summary>
    /// Records that could not be read at launch and were set aside. Whatever they held is not
    /// in the queue, so an empty queue is not proof that everything reached the server.
    /// </summary>
    private IFeed<IImmutableList<string>> SetAside => _recovery.Watch(_queue.WatchSetAside);

    public IFeed<bool> HasSetAside => SetAside.Select(files => files.Count > 0);

    public IFeed<string> SetAsideWarning => SetAside.Select(files => files.Count == 0
        ? string.Empty
        : $"TRACE could not read part of this device's records when it started and set them aside ({string.Join(", ", files)}). "
            + "Work they held may not have reached the server. Keep this device as it is and contact support before reinstalling.");

    public IFeed<bool> IsSettled =>
        Feed.Combine(PendingCount, SetAside).Select(pair => pair.Item1 == 0 && pair.Item2.Count == 0);

    public IFeed<string> PendingLabel => Feed.Combine(PendingCount, SetAside).Select(pair => pair.Item1 switch
    {
        0 when pair.Item2.Count > 0 => "Nothing is queued, but part of this device's records could not be read.",
        0 => "Everything on this device has reached the server.",
        1 => "1 change is waiting to reach the server.",
        _ => $"{pair.Item1} changes are waiting to reach the server.",
    });

    public IFeed<bool> IsOnline => Feed.AsyncEnumerable(_network.Watch);

    public IFeed<bool> IsOffline => IsOnline.Select(online => !online);

    public IState<string> LastResult => State<string>.Empty(this);

    /// <summary>
    /// Read from the queue itself, so failed uploads left over from an earlier session can be
    /// retried as soon as the screen opens, not only after a sync pass in this one.
    /// </summary>
    public IFeed<bool> HasFailures => _recovery.Watch(_queue.Watch)
        .Select(operations => operations.Any(operation => operation.Status == SyncState.Failed));

    public IFeed<string> ConnectionAction => IsOnline.Select(online => online ? "Go offline" : "Go online");

    public async ValueTask SyncNow(CancellationToken ct)
    {
        SyncResult result;
        try
        {
            result = await _queue.SyncAsync(ct);
        }
        catch (Exception ex) when (StoreFailures.IsStorageFailure(ex))
        {
            await LastResult.UpdateAsync(_ => QueueWriteFailed, ct);
            return;
        }

        var message = result switch
        {
            { WasOffline: true } => "Still offline. Your work stays on the device until there is a connection.",
            { Succeeded: 0, Failed: 0, Held: 0 } => "Nothing was waiting to sync.",
            { Failed: 0 } => $"{result.Succeeded} change{(result.Succeeded == 1 ? "" : "s")} synchronized.",
            { Succeeded: 0 } => $"{result.Failed} upload{(result.Failed == 1 ? "" : "s")} failed. They can be retried.",
            _ => $"{result.Succeeded} synchronized, {result.Failed} failed and can be retried.",
        };

        // Corrective work never reaches the server ahead of the inspection it came from.
        message += result.Held switch
        {
            0 => string.Empty,
            1 => " 1 corrective action change waits until the inspection it came from has synced.",
            _ => $" {result.Held} corrective action changes wait until the inspections they came from have synced.",
        };

        await LastResult.UpdateAsync(_ => message, ct);
    }

    public async ValueTask RetryFailed(CancellationToken ct)
    {
        try
        {
            await _queue.RetryAllFailedAsync(ct);
        }
        catch (Exception ex) when (StoreFailures.IsStorageFailure(ex))
        {
            await LastResult.UpdateAsync(_ => QueueWriteFailed, ct);
            return;
        }

        await LastResult.UpdateAsync(_ => "Failed uploads are queued again. Sync when you have signal.", ct);
    }

    public void ToggleConnection() => _network.SetOffline(_network.IsOnline);

    /// <summary>Reads the device's records again after a failed read.</summary>
    public void Retry() => _recovery.Retry();

    private const string QueueWriteFailed =
        "The sync queue on this device could not be updated. Your work is still on the device; try again, and restart TRACE if it keeps failing.";
}
