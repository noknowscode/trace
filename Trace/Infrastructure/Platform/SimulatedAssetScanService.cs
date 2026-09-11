using Trace.Services;

namespace Trace.Infrastructure.Platform;

/// <summary>
/// Resolves an asset from a tag payload.
/// </summary>
/// <remarks>
/// Real QR and NFC hardware would replace <see cref="ScanAsync"/> only. The payload format
/// and the resolve path below are what a device scan would feed into, so swapping the
/// hardware in later does not change how an asset gets opened.
/// </remarks>
public sealed class SimulatedAssetScanService : IAssetScanService
{
    /// <summary>The tag payload format stuck on the equipment, e.g. <c>trace://asset/P-204</c>.</summary>
    private const string TagPrefix = "trace://asset/";

    private readonly ITraceStore _store;
    private int _scanIndex;

    public SimulatedAssetScanService(ITraceStore store) => _store = store;

    public async ValueTask<ScanResult> ScanAsync(CancellationToken ct)
    {
        var snapshot = await _store.GetAsync(ct);
        if (snapshot.Assets.Count == 0)
        {
            return new ScanResult(false, null, string.Empty, "No assets are available on this device.");
        }

        // Walks the tagged assets in turn, then presents an unknown tag, so both the found
        // and the not-found paths are reachable from the scan screen.
        var index = _scanIndex++ % (snapshot.Assets.Count + 1);

        if (index == snapshot.Assets.Count)
        {
            return await ResolveAsync($"{TagPrefix}X-999", ct);
        }

        return await ResolveAsync($"{TagPrefix}{snapshot.Assets[index].Id}", ct);
    }

    public async ValueTask<ScanResult> ResolveAsync(string rawValue, CancellationToken ct)
    {
        var value = (rawValue ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return new ScanResult(false, null, value, "Enter an asset code or scan a tag.");
        }

        var code = value.StartsWith(TagPrefix, StringComparison.OrdinalIgnoreCase)
            ? value[TagPrefix.Length..]
            : value;

        var snapshot = await _store.GetAsync(ct);
        var asset = snapshot.Assets.FirstOrDefault(a =>
            string.Equals(a.Id, code, StringComparison.OrdinalIgnoreCase));

        return asset is null
            ? new ScanResult(false, null, value, $"No asset on this device matches {code}.")
            : new ScanResult(true, asset.Id, value, $"{asset.Id} · {asset.Name}");
    }
}
