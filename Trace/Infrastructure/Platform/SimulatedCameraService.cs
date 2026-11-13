using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Trace.Infrastructure.Platform;

/// <summary>
/// Stands in for the device camera.
/// </summary>
/// <remarks>
/// Capture returns one of the sample frames bundled with the app rather than opening a
/// camera — this is an explicit simulation boundary, not hidden behaviour. The permission
/// model is faithful to the platforms: a first ask can be granted, but once denied the OS
/// will not re-prompt, so the app has to route the technician to settings. That is the
/// recovery path the app exercises.
/// </remarks>
public sealed class SimulatedCameraService : ICameraService
{
    private static readonly string[] SampleCaptures =
    [
        "ms-appx:///Assets/Evidence/capture-seal-weep.png",
        "ms-appx:///Assets/Evidence/capture-coupling-guard.png",
        "ms-appx:///Assets/Evidence/capture-bearing-thermal.png",
    ];

    private readonly ILogger<SimulatedCameraService> _log;
    private readonly List<Channel<CameraPermission>> _watchers = [];
    private int _captureIndex;

    public SimulatedCameraService(ILogger<SimulatedCameraService> log) => _log = log;

    public CameraPermission Permission { get; private set; } = CameraPermission.Unknown;

    public async IAsyncEnumerable<CameraPermission> WatchPermission([EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateBounded<CameraPermission>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

        lock (_watchers)
        {
            _watchers.Add(channel);
        }

        try
        {
            yield return Permission;

            await foreach (var permission in channel.Reader.ReadAllAsync(ct))
            {
                yield return permission;
            }
        }
        finally
        {
            lock (_watchers)
            {
                _watchers.Remove(channel);
            }
        }
    }

    public ValueTask<CameraPermission> RequestPermissionAsync(CancellationToken ct)
    {
        // A first ask can be granted. Once denied, the platform stops prompting and the
        // only way back is the system settings screen.
        if (Permission == CameraPermission.Unknown)
        {
            SetPermission(CameraPermission.Granted);
        }

        return ValueTask.FromResult(Permission);
    }

    public async ValueTask<PhotoCapture?> CapturePhotoAsync(CancellationToken ct)
    {
        if (Permission != CameraPermission.Granted)
        {
            return null;
        }

        var source = SampleCaptures[_captureIndex % SampleCaptures.Length];
        _captureIndex++;

        try
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(source));
            using var stream = await file.OpenStreamForReadAsync();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            return new PhotoCapture(buffer.ToArray(), ".png");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Could not load the sample capture {Source}.", source);
            return null;
        }
    }

    public void SetPermission(CameraPermission permission)
    {
        if (Permission == permission)
        {
            return;
        }

        Permission = permission;

        Channel<CameraPermission>[] watchers;
        lock (_watchers)
        {
            watchers = [.. _watchers];
        }

        foreach (var watcher in watchers)
        {
            watcher.Writer.TryWrite(permission);
        }
    }
}
