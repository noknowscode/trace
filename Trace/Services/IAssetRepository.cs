namespace Trace.Services;

public interface IAssetRepository
{
    /// <summary>
    /// Yields the asset now and again on every change. Yields <c>null</c> when the id does
    /// not resolve, which surfaces as the asset-unavailable state rather than an error.
    /// </summary>
    IAsyncEnumerable<Asset?> Watch(string assetId, CancellationToken ct);

    IAsyncEnumerable<IImmutableList<AssetSummary>> WatchSummaries(CancellationToken ct);
}
