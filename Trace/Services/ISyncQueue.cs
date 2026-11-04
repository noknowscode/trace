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

    /// <summary>
    /// Attempts every pending and failed operation whose earlier work has synced, and says how
    /// many went, failed, and waited for their inspection.
    /// </summary>
    ValueTask<SyncResult> SyncAsync(CancellationToken ct);

    /// <summary>Moves every failed operation back to pending, with its inspection, for the next pass.</summary>
    ValueTask RetryAllFailedAsync(CancellationToken ct);
}

/// <param name="Held">
/// Corrective work left pending because the inspection it came from has not synced yet.
/// </param>
public sealed record SyncResult(int Succeeded, int Failed, bool WasOffline, int Held = 0);
