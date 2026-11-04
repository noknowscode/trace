using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Trace.Infrastructure.Persistence;
using Trace.Infrastructure.Platform;
using Trace.Infrastructure.Repositories;
using Trace.Presentation;

namespace Trace.Tests;

/// <summary>
/// A real store over a throwaway directory. The tests exercise the same persistence path
/// the app uses, so "it persisted" means bytes actually reached a file.
/// </summary>
internal sealed class TestHarness : IDisposable
{
    private readonly string _root;

    private TestHarness(string root, TestTimeProvider time)
    {
        _root = root;
        Time = time;
        Files = new LocalFileStore(root);
        Store = new TraceStore(Files, Time, NullLogger<TraceStore>.Instance);
        Network = new SimulatedNetworkStatusService();
        Queue = new SyncQueue(Store, Network, NullLogger<SyncQueue>.Instance);
        Workflow = new InspectionWorkflow(
            Store,
            Time,
            Options.Create(new AppConfig { Technician = "Test Tech" }),
            NullLogger<InspectionWorkflow>.Instance);
        Evidence = new LocalEvidenceStore(Files, Time);
    }

    public TestTimeProvider Time { get; }

    public LocalFileStore Files { get; }

    public TraceStore Store { get; }

    public SimulatedNetworkStatusService Network { get; }

    public SyncQueue Queue { get; }

    public InspectionWorkflow Workflow { get; }

    public LocalEvidenceStore Evidence { get; }

    public static TestHarness Create()
    {
        var root = Path.Combine(Path.GetTempPath(), "trace-tests", Guid.NewGuid().ToString("N"));
        return new TestHarness(root, new TestTimeProvider(new DateTimeOffset(2026, 3, 4, 9, 30, 0, TimeSpan.Zero)));
    }

    /// <summary>Opens a second store over the same directory, as a relaunch would.</summary>
    public TraceStore Reopen() => new(new LocalFileStore(_root), Time, NullLogger<TraceStore>.Instance);

    /// <summary>
    /// A store over the same directory whose writes stop dead at <paramref name="key"/>, the
    /// way a process killed partway through a commit leaves the files.
    /// </summary>
    public TraceStore StoreThatDiesWriting(string key) =>
        new(new DyingFileStore(new LocalFileStore(_root), key), Time, NullLogger<TraceStore>.Instance);

    /// <summary>The same directory through a file store whose writes can be made to fail.</summary>
    public BreakableFileStore BreakableFiles() => new(new LocalFileStore(_root));

    /// <summary>A store over <paramref name="files"/>, sharing this harness's clock.</summary>
    public TraceStore StoreOver(ILocalFileStore files) => new(files, Time, NullLogger<TraceStore>.Instance);

    public InspectionWorkflow WorkflowOver(ITraceStore store) => new(
        store,
        Time,
        Options.Create(new AppConfig { Technician = "Test Tech" }),
        NullLogger<InspectionWorkflow>.Instance);

    public async Task<Asset> PumpAsync() =>
        (await Store.GetAsync(default)).FindAsset("P-204")
        ?? throw new InvalidOperationException("Seed asset P-204 is missing.");

    /// <summary>Takes the value a watch stream emits for the current state.</summary>
    public static async Task<T> FirstAsync<T>(IAsyncEnumerable<T> source)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await foreach (var value in source.WithCancellation(cts.Token))
        {
            return value;
        }

        throw new InvalidOperationException("The stream completed without emitting a value.");
    }

    /// <summary>
    /// Opens the asset workspace for an asset the way navigation does: with the row that was
    /// tapped, over this harness's store.
    /// </summary>
    public async Task<AssetModel> OpenAssetAsync(
        string assetId = "P-204",
        ITraceStore? store = null,
        NavigationOrigin? origin = null,
        ICameraService? camera = null,
        ILocalEvidenceStore? evidence = null)
    {
        store ??= Store;
        var assets = new AssetRepository(store);
        var summary = (await FirstAsync(assets.WatchSummaries(default))).Single(s => s.AssetId == assetId);

        return new AssetModel(
            summary,
            assets,
            new InspectionRepository(store),
            new CorrectiveActionRepository(store),
            ReferenceEquals(store, Store) ? Workflow : WorkflowOver(store),
            camera ?? new SimulatedCameraService(NullLogger<SimulatedCameraService>.Instance),
            evidence ?? Evidence,
            Network,
            new NullNavigator(),
            origin ?? new NavigationOrigin(),
            new FeedRecovery(),
            Time);
    }

    /// <summary>Polls <paramref name="condition"/> until it holds, for up to 5 s.</summary>
    public static async Task UntilAsync(Func<Task<bool>> condition)
    {
        for (var i = 0; i < 100 && !await condition(); i++)
        {
            await Task.Delay(50);
        }
    }

    public async Task<InspectionPoint> PointAsync(string pointId) =>
        (await PumpAsync()).FindPoint(pointId)
        ?? throw new InvalidOperationException($"Seed point {pointId} is missing.");

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a test over.
        }
    }
}

internal sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public TestTimeProvider(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}

/// <summary>A navigator that goes nowhere: the model tests drive commands that do not navigate.</summary>
internal sealed class NullNavigator : INavigator
{
    public Route? Route => null;

    public Task<bool> CanNavigate(Route route) => Task.FromResult(false);

    public Task<NavigationResponse?> NavigateAsync(NavigationRequest request) => Task.FromResult<NavigationResponse?>(null);
}

/// <summary>Writes nothing from <paramref name="fatalKey"/> on, as if the process died there.</summary>
internal sealed class DyingFileStore(ILocalFileStore inner, string fatalKey) : ILocalFileStore
{
    private bool _dead;

    public ValueTask<string?> ReadTextAsync(string name, CancellationToken ct) => inner.ReadTextAsync(name, ct);

    public ValueTask WriteTextAsync(string name, string content, CancellationToken ct)
    {
        _dead |= name == fatalKey;
        return _dead
            ? throw new IOException($"The process died before {name} was written.")
            : inner.WriteTextAsync(name, content, ct);
    }

    public ValueTask<string> WriteBytesAsync(string name, byte[] content, CancellationToken ct) =>
        inner.WriteBytesAsync(name, content, ct);

    public ValueTask DeleteAsync(string name, CancellationToken ct) =>
        _dead ? throw new IOException($"The process died before {name} was deleted.") : inner.DeleteAsync(name, ct);

    public ValueTask MoveAsync(string name, string newName, CancellationToken ct) => inner.MoveAsync(name, newName, ct);
}

/// <summary>Fails every write while <see cref="Broken"/> is set, as a full or read-only disk does.</summary>
internal sealed class BreakableFileStore(ILocalFileStore inner) : ILocalFileStore
{
    public bool Broken { get; set; }

    public ValueTask<string?> ReadTextAsync(string name, CancellationToken ct) => inner.ReadTextAsync(name, ct);

    public ValueTask WriteTextAsync(string name, string content, CancellationToken ct) =>
        Broken ? throw Full(name) : inner.WriteTextAsync(name, content, ct);

    public ValueTask<string> WriteBytesAsync(string name, byte[] content, CancellationToken ct) =>
        Broken ? throw Full(name) : inner.WriteBytesAsync(name, content, ct);

    public ValueTask DeleteAsync(string name, CancellationToken ct) =>
        Broken ? throw Full(name) : inner.DeleteAsync(name, ct);

    public ValueTask MoveAsync(string name, string newName, CancellationToken ct) =>
        Broken ? throw Full(name) : inner.MoveAsync(name, newName, ct);

    private static IOException Full(string name) => new($"No space left on the device for {name}.");
}

/// <summary>A camera that is allowed and always returns the same small frame.</summary>
internal sealed class FrameCamera : ICameraService
{
    public CameraPermission Permission => CameraPermission.Granted;

    public async IAsyncEnumerable<CameraPermission> WatchPermission(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await Task.CompletedTask;
        yield return Permission;
    }

    public ValueTask<CameraPermission> RequestPermissionAsync(CancellationToken ct) => ValueTask.FromResult(Permission);

    public ValueTask<PhotoCapture?> CapturePhotoAsync(CancellationToken ct) =>
        ValueTask.FromResult<PhotoCapture?>(new PhotoCapture([1, 2, 3], ".png"));

    public void SetPermission(CameraPermission permission)
    {
    }
}
