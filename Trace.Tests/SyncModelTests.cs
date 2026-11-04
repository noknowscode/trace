using Microsoft.Extensions.Logging.Abstractions;
using Trace.Infrastructure.Repositories;
using Trace.Presentation;

namespace Trace.Tests;

/// <summary>The Sync screen's commands, when the queue on the device cannot be written.</summary>
public class SyncModelTests
{
    private TestHarness _harness = null!;

    [SetUp]
    public void SetUp() => _harness = TestHarness.Create();

    [TearDown]
    public void TearDown() => _harness.Dispose();

    [Test]
    public async Task Sync_commands_that_cannot_update_the_queue_say_so()
    {
        var files = _harness.BreakableFiles();
        var store = _harness.StoreOver(files);
        var workflow = _harness.WorkflowOver(store);
        var point = await _harness.PointAsync("P-204-seal");
        var draft = await workflow.StartOrResumeDraftAsync(point, default);
        var photo = await _harness.Evidence.SavePhotoAsync(point.Id, [1, 2, 3], ".png", "Seal face", default);
        (await workflow.CompleteAsync(
                draft with { ReadingText = "1.6", Disposition = Disposition.Pass, Evidence = [photo] },
                default))
            .Succeeded.Should().BeTrue();

        var model = new SyncModel(
            new SyncQueue(store, _harness.Network, NullLogger<SyncQueue>.Instance),
            _harness.Network,
            new FeedRecovery());

        // An upload carrying a photo fails its first pass, which leaves something to retry.
        await model.SyncNow(default);
        (await model.LastResult).Should().Contain("failed");

        // The store's IOException used to escape both commands, and the screen said nothing.
        files.Broken = true;
        await model.RetryFailed(default);
        (await model.LastResult).Should().Contain("could not be updated");
        await model.SyncNow(default);
        (await model.LastResult).Should().Contain("could not be updated");

        files.Broken = false;
        await model.RetryFailed(default);
        await model.SyncNow(default);
        (await model.LastResult).Should().Contain("synchronized");
    }

    [Test]
    public async Task Sync_says_when_corrective_work_waits_for_its_inspection()
    {
        var point = await _harness.PointAsync("P-204-seal");
        var draft = await _harness.Workflow.StartOrResumeDraftAsync(point, default);
        var photo = await _harness.Evidence.SavePhotoAsync(point.Id, [1, 2, 3], ".png", "Seal face", default);
        var failed = (await _harness.Workflow.CompleteAsync(
                draft with { ReadingText = "7.6", Disposition = Disposition.Fail, Evidence = [photo] },
                default))
            .Event!;
        (await _harness.Workflow.RaiseCorrectiveActionAsync(
                new CorrectiveActionRequest(failed.AssetId, failed.PointId, failed.Id, "Replace the seal", CorrectivePriority.High, "Mechanical Team"),
                default))
            .Succeeded.Should().BeTrue();
        var model = new SyncModel(_harness.Queue, _harness.Network, new FeedRecovery());

        await model.SyncNow(default);

        (await model.LastResult).Should().Be(
            "1 upload failed. They can be retried. 1 corrective action change waits until the inspection it came from has synced.");

        await model.SyncNow(default);
        (await model.LastResult).Should().Be("2 changes synchronized.");
    }
}
