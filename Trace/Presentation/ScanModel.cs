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

    /// <summary>Why the last scan or lookup found nothing to say about the code: the device's records could not be read.</summary>
    public IState<string> ReadError => State<string>.Empty(this);

    public async ValueTask ScanTag(CancellationToken ct)
    {
        if (await ResolveAsync(_scanner.ScanAsync, ct) is { Found: true, AssetId: { } assetId })
        {
            await Code.UpdateAsync(_ => assetId, ct);
            await OpenAsync(assetId, ct);
        }
    }

    public async ValueTask Lookup(CancellationToken ct)
    {
        var code = await Code ?? string.Empty;
        if (await ResolveAsync(token => _scanner.ResolveAsync(code, token), ct) is { Found: true, AssetId: { } assetId })
        {
            await OpenAsync(assetId, ct);
        }
    }

    /// <summary>
    /// Resolves a tag or code against the device's records and shows the answer. A store that
    /// cannot be read is not "no such asset": it says so, and the code stays for another try.
    /// </summary>
    private async ValueTask<ScanResult?> ResolveAsync(
        Func<CancellationToken, ValueTask<ScanResult>> resolve,
        CancellationToken ct)
    {
        ScanResult result;
        try
        {
            result = await resolve(ct);
        }
        catch (Exception ex) when (StoreFailures.IsStorageFailure(ex))
        {
            await Result.UpdateAsync(_ => null, ct);
            await ReadError.UpdateAsync(_ => RecordsUnreadable, ct);
            return null;
        }

        await ReadError.UpdateAsync(_ => string.Empty, ct);
        await Result.UpdateAsync(_ => result, ct);
        return result;
    }

    private async ValueTask OpenAsync(string assetId, CancellationToken ct)
    {
        // Navigation carries the same row shape the lists carry, so an asset opened from a
        // tag lands on exactly the page an asset opened from the round does.
        IImmutableList<AssetSummary> summaries;
        try
        {
            summaries = await TraceAsync(_assets.WatchSummaries, ct);
        }
        catch (Exception ex) when (StoreFailures.IsStorageFailure(ex))
        {
            await ReadError.UpdateAsync(_ => RecordsUnreadable, ct);
            return;
        }

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

    private const string RecordsUnreadable =
        "This device's records could not be read, so nothing was looked up. Try again; if it keeps failing, restart TRACE.";
}
