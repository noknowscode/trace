
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

    private async Task<InspectionEvent> CompleteSealAsync(bool withPhoto)
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
                Disposition = Disposition.Pass,
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
        failed.HasError.Should().BeTrue();
        failed.IsRetryable.Should().BeTrue();

        // A failed operation still counts as outstanding work.
        (await _harness.Store.GetAsync(default)).PendingSyncCount.Should().Be(1);

        await _harness.Queue.RetryAsync(failed.Id, default);
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
        result.AnyAttempted.Should().BeFalse();
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
}
