using Trace.Infrastructure.Persistence;

namespace Trace.Infrastructure.Repositories;

public sealed class AssetRepository : IAssetRepository
{
    private readonly ITraceStore _store;

    public AssetRepository(ITraceStore store) => _store = store;

    public async ValueTask<Asset?> GetAsync(string assetId, CancellationToken ct) =>
        (await _store.GetAsync(ct)).FindAsset(assetId);

    public IAsyncEnumerable<Asset> Watch(string assetId, CancellationToken ct) =>
        StoreProjections.Project(_store.Watch(ct), s => s.FindAsset(assetId)!, ct);

    public IAsyncEnumerable<IImmutableList<AssetSummary>> WatchSummaries(CancellationToken ct) =>
        StoreProjections.ProjectList(_store.Watch(ct), SummaryProjector.AllAssets, ct);
}
