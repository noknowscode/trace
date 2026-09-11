namespace Trace.Services;

public interface IInspectionRepository
{
    /// <summary>Every event for an asset, newest first.</summary>
    IAsyncEnumerable<IImmutableList<InspectionEvent>> WatchHistory(string assetId, CancellationToken ct);

    /// <summary>Every event for a single point, newest first — the trend behind one reading.</summary>
    IAsyncEnumerable<IImmutableList<InspectionEvent>> WatchPointHistory(string pointId, CancellationToken ct);

    ValueTask<IImmutableList<InspectionEvent>> GetHistoryAsync(string assetId, CancellationToken ct);
}
