using Trace.Services;

namespace Trace.Presentation;

/// <summary>
/// Every asset on the device, for the times a technician needs a machine that is not on
/// today's round.
/// </summary>
public partial record AssetsModel
{
    private readonly IAssetRepository _assets;

    public AssetsModel(IAssetRepository assets) => _assets = assets;

    public IState<string> Query => State.Value(this, () => string.Empty);

    public IListFeed<AssetSummary> Assets =>
        Feed.Combine(Feed.AsyncEnumerable(_assets.WatchSummaries), Query)
            .Select(pair => Match(pair.Item1, pair.Item2))
            .AsListFeed();

    private static IImmutableList<AssetSummary> Match(IImmutableList<AssetSummary> assets, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return assets;
        }

        var term = query.Trim();

        return assets
            .Where(a =>
                a.AssetId.Contains(term, StringComparison.OrdinalIgnoreCase)
                || a.AssetName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || a.TypeLabel.Contains(term, StringComparison.OrdinalIgnoreCase)
                || a.Area.Contains(term, StringComparison.OrdinalIgnoreCase)
                || a.Site.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToImmutableList();
    }
}
