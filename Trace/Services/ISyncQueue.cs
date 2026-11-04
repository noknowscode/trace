namespace Trace.Services;

/// <summary>
/// The durable outbox. Everything TRACE writes locally is enqueued here, and the queue is
/// the only thing that can declare a record synced.
/// </summary>
public interface ISyncQueue
{
    IAsyncEnumerable<IImmutableList<SyncOperation>> Watch(CancellationToken ct);

    IAsyncEnumerable<int> WatchPendingCount(CancellationToken ct);

    /// <summary>
    /// The files of this device's records that could not be read at launch and were set aside.
    /// While there are any, an empty queue does not mean everything reached the server.
    /// </summary>
    IAsyncEnumerable<IImmutableList<string>> WatchSetAside(CancellationToken ct);

    /// <summary>Attempts every pending and failed operation. Returns the number that succeeded.</summary>
    ValueTask<SyncResult> SyncAsync(CancellationToken ct);

    ValueTask RetryAllFailedAsync(CancellationToken ct);
}

public sealed record SyncResult(int Succeeded, int Failed, bool WasOffline);
