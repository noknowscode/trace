using Trace.Services;

namespace Trace.Presentation;

/// <summary>
/// Every asset on the device, for the times a technician needs a machine that is not on
/// today's round.
/// </summary>
public partial record AssetsModel
{
    private readonly IAssetRepository _assets;
    private readonly FeedRecovery _recovery;

    public AssetsModel(IAssetRepository assets, FeedRecovery recovery)
    {
        _assets = assets;
        _recovery = recovery;
    }

    public IState<string> Query => State.Value(this, () => string.Empty);

    public IListFeed<AssetSummary> Assets =>
        Feed.Combine(_recovery.Watch(_assets.WatchSummaries), Query)
            .Select(pair => Match(pair.Item1, pair.Item2))
            .AsListFeed();

    /// <summary>Reads the device's records again after a failed read.</summary>
    public void Retry() => _recovery.Retry();

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
