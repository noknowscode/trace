namespace Trace.Services;

public interface IInspectionRepository
{
    /// <summary>Every event for an asset, newest first.</summary>
    IAsyncEnumerable<IImmutableList<InspectionEvent>> WatchHistory(string assetId, CancellationToken ct);

    ValueTask<IImmutableList<InspectionEvent>> GetHistoryAsync(string assetId, CancellationToken ct);
}
