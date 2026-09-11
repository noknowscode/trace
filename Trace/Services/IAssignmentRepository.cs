namespace Trace.Services;

public interface IAssignmentRepository
{
    /// <summary>Today's round, in walking order, with live progress and finding counts.</summary>
    IAsyncEnumerable<IImmutableList<AssetSummary>> WatchToday(CancellationToken ct);
}
