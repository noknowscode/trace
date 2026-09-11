namespace Trace.Services;

public enum CameraPermission
{
    Unknown = 0,
    Granted = 1,
    Denied = 2,
}

public sealed record PhotoCapture(byte[] Content, string Extension);

/// <summary>
/// Photo capture behind an interface. The desktop and demo implementation produces a
/// deterministic frame instead of opening a camera, so the whole evidence workflow — including
/// the permission-denied recovery path — can be exercised without hardware.
/// </summary>
public interface ICameraService
{
    CameraPermission Permission { get; }

    IAsyncEnumerable<CameraPermission> WatchPermission(CancellationToken ct);

    ValueTask<CameraPermission> RequestPermissionAsync(CancellationToken ct);

    /// <summary>Returns null when permission is not granted.</summary>
    ValueTask<PhotoCapture?> CapturePhotoAsync(CancellationToken ct);

    /// <summary>Demo affordance: flips the simulated permission so the denied state is reachable.</summary>
    void SetPermission(CameraPermission permission);
}
