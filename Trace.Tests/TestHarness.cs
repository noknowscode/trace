using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Trace.Domain.Models;
using Trace.Infrastructure.Persistence;
using Trace.Infrastructure.Platform;
using Trace.Infrastructure.Repositories;
using Trace.Models;
using Trace.Services;

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
        Evidence = new LocalEvidenceStore(Files);
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
