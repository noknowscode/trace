namespace Trace.Services;

public sealed record ScanResult(bool Found, string? AssetId, string RawValue, string Message);

/// <summary>
/// QR and NFC asset identification. The simulated implementation reads the same tag payload
/// format the real scanner would emit, so the resolve-and-open path is identical.
/// </summary>
public interface IAssetScanService
{
    /// <summary>Simulates presenting a tag to the device.</summary>
    ValueTask<ScanResult> ScanAsync(CancellationToken ct);

    /// <summary>Resolves a tag payload or a typed asset code to an asset.</summary>
    ValueTask<ScanResult> ResolveAsync(string rawValue, CancellationToken ct);
}
