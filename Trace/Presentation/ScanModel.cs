namespace Trace.Presentation;

/// <summary>
/// Identify the machine you are standing in front of. Presenting a tag and typing the code
/// off the nameplate resolve through exactly the same path, because gloves, grease and a
/// scratched QR label are all normal.
/// </summary>
public partial record ScanModel
{
    private readonly IAssetScanService _scanner;
    private readonly IAssetRepository _assets;
    private readonly INavigator _navigator;

    public ScanModel(IAssetScanService scanner, IAssetRepository assets, INavigator navigator)
    {
        _scanner = scanner;
        _assets = assets;
        _navigator = navigator;
    }

    public IState<string> Code => State.Value(this, () => string.Empty);

    public IState<ScanResult> Result => State<ScanResult>.Empty(this);

    public IFeed<bool> NotResolved => Result.Select(result => result is not null && !result.Found);

    public IFeed<string> ResultMessage => Result.Select(result => result?.Message ?? string.Empty);

    public async ValueTask ScanTag(CancellationToken ct)
    {
        var result = await _scanner.ScanAsync(ct);
        await Result.UpdateAsync(_ => result, ct);

        if (result.Found)
        {
            await Code.UpdateAsync(_ => result.AssetId ?? string.Empty, ct);
            await OpenAsync(result.AssetId!, ct);
        }
    }

    public async ValueTask Lookup(CancellationToken ct)
    {
        var code = await Code;
        var result = await _scanner.ResolveAsync(code ?? string.Empty, ct);
        await Result.UpdateAsync(_ => result, ct);

        if (result.Found)
        {
            await OpenAsync(result.AssetId!, ct);
        }
    }

    private async ValueTask OpenAsync(string assetId, CancellationToken ct)
    {
        // Navigation carries the same row shape the lists carry, so an asset opened from a
        // tag lands on exactly the page an asset opened from the round does.
        var summaries = await TraceAsync(_assets.WatchSummaries, ct);
        var summary = summaries.FirstOrDefault(s => s.AssetId == assetId);

        if (summary is not null)
        {
            await _navigator.NavigateRouteAsync(this, "../Asset", data: summary, cancellation: ct);
        }
    }

    private static async ValueTask<IImmutableList<AssetSummary>> TraceAsync(
        Func<CancellationToken, IAsyncEnumerable<IImmutableList<AssetSummary>>> source,
        CancellationToken ct)
    {
        await foreach (var value in source(ct).WithCancellation(ct))
        {
            return value;
        }

        return ImmutableList<AssetSummary>.Empty;
    }
}
