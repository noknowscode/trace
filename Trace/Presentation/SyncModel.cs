using Trace.Services;

namespace Trace.Presentation;

/// <summary>
/// What has not left the device. TRACE never infers sync from connectivity, so this screen
/// is the honest answer to "is my work safe yet".
/// </summary>
public partial record SyncModel
{
    private readonly ISyncQueue _queue;
    private readonly INetworkStatusService _network;

    public SyncModel(ISyncQueue queue, INetworkStatusService network)
    {
        _queue = queue;
        _network = network;
    }

    public IListFeed<SyncOperation> Operations => ListFeed.AsyncEnumerable(_queue.Watch);

    public IFeed<int> PendingCount => Feed.AsyncEnumerable(_queue.WatchPendingCount);

    public IFeed<bool> HasPending => PendingCount.Select(count => count > 0);

    public IFeed<bool> IsSettled => PendingCount.Select(count => count == 0);

    public IFeed<string> PendingLabel => PendingCount.Select(count => count switch
    {
        0 => "Everything on this device has reached the server.",
        1 => "1 change is waiting to reach the server.",
        _ => $"{count} changes are waiting to reach the server.",
    });

    public IFeed<bool> IsOnline => Feed.AsyncEnumerable(_network.Watch);

    public IFeed<bool> IsOffline => IsOnline.Select(online => !online);

    public IState<string> LastResult => State<string>.Empty(this);

    public IState<bool> HasFailures => State.Value(this, () => false);

    public async ValueTask SyncNow(CancellationToken ct)
    {
        var result = await _queue.SyncAsync(ct);

        var message = result switch
        {
            { WasOffline: true } => "Still offline. Your work stays on the device until there is a connection.",
            { Succeeded: 0, Failed: 0 } => "Nothing was waiting to sync.",
            { Failed: 0 } => $"{result.Succeeded} change{(result.Succeeded == 1 ? "" : "s")} synchronized.",
            { Succeeded: 0 } => $"{result.Failed} upload{(result.Failed == 1 ? "" : "s")} failed. They can be retried.",
            _ => $"{result.Succeeded} synchronized, {result.Failed} failed and can be retried.",
        };

        await LastResult.UpdateAsync(_ => message, ct);
        await HasFailures.UpdateAsync(_ => result.Failed > 0, ct);
    }

    public async ValueTask RetryFailed(CancellationToken ct)
    {
        await _queue.RetryAllFailedAsync(ct);
        await LastResult.UpdateAsync(_ => "Failed uploads are queued again. Sync when you have signal.", ct);
        await HasFailures.UpdateAsync(_ => false, ct);
    }

    public void ToggleConnection() => _network.SetOffline(_network.IsOnline);
}
