using Trace.Infrastructure.Persistence;

namespace Trace.Services;

/// <summary>
/// The single source of truth for TRACE's local state. Reads are snapshots, writes are pure
/// transforms, and <see cref="Watch"/> pushes every committed change — which is what keeps
/// the Today list, the sync badge and the asset view agreeing without manual refreshes.
/// </summary>
public interface ITraceStore
{
    ValueTask<TraceSnapshot> GetAsync(CancellationToken ct);

    /// <summary>Yields the current snapshot immediately, then every snapshot committed after it.</summary>
    IAsyncEnumerable<TraceSnapshot> Watch(CancellationToken ct);

    /// <summary>
    /// Applies a transform under a lock and persists whatever it changed. The transform must
    /// be pure — it may run against a snapshot newer than the one the caller last read.
    /// </summary>
    ValueTask<TraceSnapshot> MutateAsync(Func<TraceSnapshot, TraceSnapshot> mutate, CancellationToken ct);
}
