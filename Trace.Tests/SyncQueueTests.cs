
namespace Trace.Tests;

/// <summary>
/// The outbox: what clears, what fails, and what a retry does about it.
/// </summary>
public class SyncQueueTests
{
    private TestHarness _harness = null!;

    [SetUp]
    public void SetUp() => _harness = TestHarness.Create();

    [TearDown]
    public void TearDown() => _harness.Dispose();

    private async Task<InspectionEvent> CompleteSealAsync(bool withPhoto, Disposition disposition = Disposition.Pass)
    {
        var point = await _harness.PointAsync("P-204-seal");
        var draft = await _harness.Workflow.StartOrResumeDraftAsync(point, default);

        var evidence = withPhoto
            ? ImmutableList.Create(await _harness.Evidence.SavePhotoAsync(
                point.Id, [1, 2, 3], ".png", "Seal face", default))
            : ImmutableList<Evidence>.Empty;

        var outcome = await _harness.Workflow.CompleteAsync(
            draft with
            {
                ReadingText = "1.6",
                Disposition = disposition,
                Notes = disposition == Disposition.Fail ? "Weeping at the gland." : string.Empty,
                Evidence = evidence,
            },
            default);

        outcome.Succeeded.Should().BeTrue();
        return outcome.Event!;
    }

    [Test]
    public async Task Syncing_online_clears_a_plain_inspection_and_marks_the_event_synced()
    {
        var completed = await CompleteSealAsync(withPhoto: false);

        var result = await _harness.Queue.SyncAsync(default);

        result.Succeeded.Should().Be(1);
        result.Failed.Should().Be(0);

        var snapshot = await _harness.Store.GetAsync(default);
        snapshot.PendingSyncCount.Should().Be(0);
        snapshot.Queue.Single(o => o.EntityId == completed.Id).Status.Should().Be(SyncState.Synced);
        snapshot.Events.Single(e => e.Id == completed.Id).SyncState.Should().Be(SyncState.Synced);
    }

    [Test]
    public async Task An_upload_carrying_a_photo_fails_once_and_succeeds_on_retry()
    {
        var completed = await CompleteSealAsync(withPhoto: true);

        var first = await _harness.Queue.SyncAsync(default);
        first.Failed.Should().Be(1);

        var failed = (await _harness.Store.GetAsync(default)).Queue.Single(o => o.EntityId == completed.Id);
        failed.Status.Should().Be(SyncState.Failed);
        failed.Error.Should().NotBeNullOrWhiteSpace();

        // A failed operation still counts as outstanding work.
        (await _harness.Store.GetAsync(default)).PendingSyncCount.Should().Be(1);

        await _harness.Queue.RetryAllFailedAsync(default);
        var second = await _harness.Queue.SyncAsync(default);

        second.Succeeded.Should().Be(1);
        (await _harness.Store.GetAsync(default)).PendingSyncCount.Should().Be(0);
    }

    [Test]
    public async Task Retrying_all_failed_operations_moves_them_back_to_pending()
    {
        await CompleteSealAsync(withPhoto: true);
        await _harness.Queue.SyncAsync(default);

        await _harness.Queue.RetryAllFailedAsync(default);

        var snapshot = await _harness.Store.GetAsync(default);
        snapshot.Queue.Should().NotContain(o => o.Status == SyncState.Failed);
        snapshot.Queue.Should().Contain(o => o.Status == SyncState.Pending);
    }

    [Test]
    public async Task Syncing_offline_attempts_nothing()
    {
        await CompleteSealAsync(withPhoto: false);
        _harness.Network.SetOffline(true);

        var result = await _harness.Queue.SyncAsync(default);

        result.WasOffline.Should().BeTrue();
        (result.Succeeded + result.Failed).Should().Be(0);
        (await _harness.Store.GetAsync(default)).PendingSyncCount.Should().Be(1);
    }

    [Test]
    public async Task The_queue_survives_a_relaunch()
    {
        var completed = await CompleteSealAsync(withPhoto: false);

        var relaunched = await _harness.Reopen().GetAsync(default);

        relaunched.PendingSyncCount.Should().Be(1);
        relaunched.Queue.Should().Contain(o => o.EntityId == completed.Id);
    }

    [Test]
    public async Task A_failed_upload_marks_its_inspection_failed_until_it_goes()
    {
        var completed = await CompleteSealAsync(withPhoto: true);

        await _harness.Queue.SyncAsync(default);
        StateOf(await _harness.Store.GetAsync(default), completed.Id).Should().Be(
            SyncState.Failed, "History shows the upload failed; it used to stay Pending");

        await _harness.Queue.RetryAllFailedAsync(default);
        StateOf(await _harness.Store.GetAsync(default), completed.Id).Should().Be(SyncState.Pending, "it is queued again");

        await _harness.Queue.SyncAsync(default);
        StateOf(await _harness.Store.GetAsync(default), completed.Id).Should().Be(SyncState.Synced);
    }

    [Test]
    public async Task Corrective_work_waits_for_the_inspection_it_came_from()
    {
        var failedInspection = await CompleteSealAsync(withPhoto: true, Disposition.Fail);
        var action = await RaiseFromAsync(failedInspection);
        await _harness.Dispatch.AdvanceAsync(action.Id, ActionStatus.Open, default);

        // The inspection's upload fails, so neither the action nor its status update may go.
        var first = await _harness.Queue.SyncAsync(default);

        first.Should().Be(new SyncResult(Succeeded: 0, Failed: 1, WasOffline: false, Held: 2));
        var afterFirst = await _harness.Store.GetAsync(default);
        afterFirst.Queue.Where(o => o.EntityId == action.Id).Should().OnlyContain(
            o => o.Status == SyncState.Pending && o.AttemptCount == 0, "work that waits is not attempted");

        // The next pass sends the inspection first, then the work raised from it, in order.
        var second = await _harness.Queue.SyncAsync(default);

        second.Should().Be(new SyncResult(Succeeded: 3, Failed: 0, WasOffline: false, Held: 0));
        var afterSecond = await _harness.Store.GetAsync(default);
        afterSecond.PendingSyncCount.Should().Be(0);
        StateOf(afterSecond, failedInspection.Id).Should().Be(SyncState.Synced);
    }

    [Test]
    public async Task Corrective_work_from_an_inspection_that_synced_goes_at_once()
    {
        var failedInspection = await CompleteSealAsync(withPhoto: false, Disposition.Fail);
        await RaiseFromAsync(failedInspection);

        var result = await _harness.Queue.SyncAsync(default);

        result.Should().Be(new SyncResult(Succeeded: 2, Failed: 0, WasOffline: false, Held: 0));
    }

    private async Task<CorrectiveAction> RaiseFromAsync(InspectionEvent failed)
    {
        var outcome = await _harness.Workflow.RaiseCorrectiveActionAsync(
            new CorrectiveActionRequest(failed.AssetId, failed.PointId, failed.Id, "Replace the seal", CorrectivePriority.High, "Mechanical Team"),
            default);
        outcome.Succeeded.Should().BeTrue();
        return outcome.Action!;
    }

    private static SyncState StateOf(Infrastructure.Persistence.TraceSnapshot snapshot, string eventId) =>
        snapshot.Events.Single(e => e.Id == eventId).SyncState;
}
