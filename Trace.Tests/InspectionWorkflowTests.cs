using Trace.Infrastructure.Repositories;


namespace Trace.Tests;

/// <summary>
/// The completion transaction: what lands on the device, what reaches the outbox, and what
/// happens when the same submission arrives twice.
/// </summary>
public class InspectionWorkflowTests
{
    private TestHarness _harness = null!;

    [SetUp]
    public void SetUp() => _harness = TestHarness.Create();

    [TearDown]
    public void TearDown() => _harness.Dispose();

    private async Task<InspectionDraft> DraftForSealAsync(
        string reading = "1.6",
        Disposition disposition = Disposition.Pass,
        string notes = "")
    {
        var point = await _harness.PointAsync("P-204-seal");
        var draft = await _harness.Workflow.StartOrResumeDraftAsync(point, default);
        return draft with { ReadingText = reading, Disposition = disposition, Notes = notes };
    }

    [Test]
    public async Task Completing_a_point_writes_an_event_moves_the_point_and_queues_a_sync()
    {
        var draft = await DraftForSealAsync("1.6", Disposition.Pass);

        var outcome = await _harness.Workflow.CompleteAsync(draft, default);

        outcome.Succeeded.Should().BeTrue();
        outcome.Event.Should().NotBeNull();
        outcome.Event!.Disposition.Should().Be(Disposition.Pass);
        outcome.Event.Technician.Should().Be("Test Tech");

        var snapshot = await _harness.Store.GetAsync(default);

        snapshot.Events.Should().ContainSingle(e => e.Id == outcome.Event.Id);

        var point = snapshot.FindAsset("P-204")!.FindPoint("P-204-seal")!;
        point.LatestReading.Should().Be(1.6);
        point.Disposition.Should().Be(Disposition.Pass);
        point.LastInspectedAt.Should().Be(_harness.Time.GetLocalNow());

        snapshot.Queue.Should().ContainSingle(o => o.EntityId == outcome.Event.Id)
            .Which.Status.Should().Be(SyncState.Pending);
    }

    [Test]
    public async Task An_invalid_draft_writes_nothing()
    {
        var before = await _harness.Store.GetAsync(default);
        var draft = await DraftForSealAsync("7.2", Disposition.Fail);

        var outcome = await _harness.Workflow.CompleteAsync(draft, default);

        outcome.Succeeded.Should().BeFalse();
        outcome.Validation.Blocker.Should().Be(InspectionBlocker.FailNeedsEvidence);

        var after = await _harness.Store.GetAsync(default);
        after.Events.Count.Should().Be(before.Events.Count);
        after.Queue.Should().BeEmpty();
    }

    [Test]
    public async Task Submitting_the_same_draft_twice_is_idempotent()
    {
        var draft = await DraftForSealAsync("1.6", Disposition.Pass);

        var first = await _harness.Workflow.CompleteAsync(draft, default);
        var second = await _harness.Workflow.CompleteAsync(draft, default);

        second.Succeeded.Should().BeTrue();
        second.WasDuplicate.Should().BeTrue();
        second.Event!.Id.Should().Be(first.Event!.Id);

        var snapshot = await _harness.Store.GetAsync(default);
        snapshot.Events.Count(e => e.ClientToken == draft.ClientToken).Should().Be(1);
        snapshot.Queue.Count(o => o.EntityId == first.Event.Id).Should().Be(1);
    }

    [Test]
    public async Task Completing_while_offline_commits_locally_and_leaves_the_work_queued()
    {
        _harness.Network.SetOffline(true);

        var draft = await DraftForSealAsync("2.1", Disposition.Watch);
        var outcome = await _harness.Workflow.CompleteAsync(draft, default);

        outcome.Succeeded.Should().BeTrue("offline must never block completing an inspection");

        var snapshot = await _harness.Store.GetAsync(default);
        snapshot.Events.Should().Contain(e => e.Id == outcome.Event!.Id);
        snapshot.PendingSyncCount.Should().Be(1);

        // And nothing leaves the device while it is offline.
        var result = await _harness.Queue.SyncAsync(default);
        result.WasOffline.Should().BeTrue();
        (await _harness.Store.GetAsync(default)).PendingSyncCount.Should().Be(1);
    }

    [Test]
    public async Task A_draft_survives_leaving_the_point_and_relaunching_the_app()
    {
        var point = await _harness.PointAsync("P-204-coupling");
        var draft = await _harness.Workflow.StartOrResumeDraftAsync(point, default);

        await _harness.Workflow.SaveDraftAsync(
            draft with { ReadingText = "1.2", Notes = "Guard refitted." },
            default);

        var relaunched = await _harness.Reopen().GetAsync(default);
        var resumed = relaunched.FindDraft("P-204-coupling");

        resumed.Should().NotBeNull();
        resumed!.ReadingText.Should().Be("1.2");
        resumed.Notes.Should().Be("Guard refitted.");
        resumed.ClientToken.Should().Be(draft.ClientToken, "resuming must not mint a new identity");
    }

    [Test]
    public async Task Completing_clears_the_draft()
    {
        var draft = await DraftForSealAsync("1.6", Disposition.Pass);

        await _harness.Workflow.CompleteAsync(draft, default);

        (await _harness.Store.GetAsync(default)).FindDraft("P-204-seal").Should().BeNull();
    }

    [Test]
    public async Task A_completed_draft_saved_again_does_not_come_back()
    {
        var draft = await DraftForSealAsync("1.6", Disposition.Pass);
        await _harness.Workflow.CompleteAsync(draft, default);

        // An editor still holding the completed draft saves it on the next keystroke.
        await _harness.Workflow.SaveDraftAsync(draft with { ReadingText = "1.9" }, default);

        (await _harness.Store.GetAsync(default)).FindDraft("P-204-seal").Should().BeNull();

        var next = await _harness.Workflow.StartOrResumeDraftAsync(await _harness.PointAsync("P-204-seal"), default);
        next.ClientToken.Should().NotBe(draft.ClientToken);
        next.ReadingText.Should().Be("1.6", "a new draft starts from the reading just recorded");
    }

    [Test]
    public async Task A_duplicate_completion_clears_a_draft_left_holding_its_token()
    {
        var draft = await DraftForSealAsync("1.6", Disposition.Pass);
        await _harness.Workflow.CompleteAsync(draft, default);

        // A store written before drafts with a completed token were refused can still hold one.
        await _harness.Store.MutateAsync(s => s with { Drafts = s.Drafts.Add(draft) }, default);

        var again = await _harness.Workflow.CompleteAsync(draft, default);

        again.WasDuplicate.Should().BeTrue();
        (await _harness.Store.GetAsync(default)).FindDraft("P-204-seal").Should().BeNull(
            "the point must take a fresh draft, or every later completion there is this duplicate");
    }

    [Test]
    public async Task A_corrective_action_inherits_its_asset_point_and_source_event()
    {
        var draft = await DraftForSealAsync("7.4", Disposition.Fail, "Seal weeping under load.");
        var completed = await _harness.Workflow.CompleteAsync(draft, default);

        var outcome = await _harness.Workflow.RaiseCorrectiveActionAsync(
            new CorrectiveActionRequest(
                AssetId: "P-204",
                PointId: "P-204-seal",
                SourceInspectionEventId: completed.Event!.Id,
                Summary: "Replace pump seal",
                Priority: CorrectivePriority.High,
                Assignee: "Mechanical Team"),
            default);

        outcome.Succeeded.Should().BeTrue();

        var action = outcome.Action!;
        action.AssetId.Should().Be("P-204");
        action.AssetName.Should().Be("Centrifugal Pump");
        action.PointId.Should().Be("P-204-seal");
        action.PointLabel.Should().Be("Seal housing");
        action.SourceInspectionEventId.Should().Be(completed.Event.Id);
        action.Status.Should().Be(ActionStatus.Open);
        action.CreatedAt.Should().Be(_harness.Time.GetLocalNow());

        var snapshot = await _harness.Store.GetAsync(default);
        snapshot.Actions.Should().Contain(a => a.Id == action.Id);
        snapshot.Queue.Should().Contain(o => o.EntityId == action.Id);
    }

    [Test]
    public async Task Raising_work_twice_from_one_inspection_keeps_one_action()
    {
        var draft = await DraftForSealAsync("7.4", Disposition.Fail, "Seal weeping.");
        var completed = await _harness.Workflow.CompleteAsync(draft, default);
        var request = new CorrectiveActionRequest(
            "P-204", "P-204-seal", completed.Event!.Id, "Replace pump seal",
            CorrectivePriority.High, "Mechanical Team");

        var first = await _harness.Workflow.RaiseCorrectiveActionAsync(request, default);
        var second = await _harness.Workflow.RaiseCorrectiveActionAsync(
            request with { Summary = "Replace pump seal again", Priority = CorrectivePriority.Low }, default);

        first.WasDuplicate.Should().BeFalse();
        second.Succeeded.Should().BeTrue();
        second.WasDuplicate.Should().BeTrue();
        second.Action!.Id.Should().Be(first.Action!.Id, "the second request resolves to the action already raised");
        second.Action.Summary.Should().Be("Replace pump seal");

        var snapshot = await _harness.Store.GetAsync(default);
        snapshot.Actions.Should().ContainSingle(a => a.SourceInspectionEventId == completed.Event.Id);
        snapshot.Queue.Should().ContainSingle(o => o.EntityType == nameof(CorrectiveAction) && o.EntityId == first.Action.Id);
    }

    [Test]
    public async Task A_corrective_action_needs_a_summary()
    {
        var draft = await DraftForSealAsync("7.4", Disposition.Fail, "Seal weeping.");
        var completed = await _harness.Workflow.CompleteAsync(draft, default);

        var outcome = await _harness.Workflow.RaiseCorrectiveActionAsync(
            new CorrectiveActionRequest(
                "P-204", "P-204-seal", completed.Event!.Id, "   ",
                CorrectivePriority.High, "Mechanical Team"),
            default);

        outcome.Succeeded.Should().BeFalse();
        outcome.Error.Should().NotBeNullOrWhiteSpace();
        (await _harness.Store.GetAsync(default)).Actions.Should().NotContain(a => a.Summary.Trim().Length == 0);
    }

    [Test]
    public async Task A_corrective_action_cannot_carry_another_points_inspection()
    {
        var draft = await DraftForSealAsync("7.4", Disposition.Fail, "Seal weeping.");
        var completed = await _harness.Workflow.CompleteAsync(draft, default);

        var outcome = await _harness.Workflow.RaiseCorrectiveActionAsync(
            new CorrectiveActionRequest(
                "P-204", "P-204-coupling", completed.Event!.Id, "Realign coupling",
                CorrectivePriority.High, "Mechanical Team"),
            default);

        outcome.Succeeded.Should().BeFalse();
        outcome.Error.Should().Contain("another point");
        (await _harness.Store.GetAsync(default)).Actions.Should().NotContain(a => a.PointId == "P-204-coupling");
    }

    [Test]
    public async Task A_corrective_action_cannot_be_raised_from_a_passed_inspection()
    {
        var draft = await DraftForSealAsync("1.6", Disposition.Pass);
        var completed = await _harness.Workflow.CompleteAsync(draft, default);

        var outcome = await _harness.Workflow.RaiseCorrectiveActionAsync(
            new CorrectiveActionRequest(
                "P-204", "P-204-seal", completed.Event!.Id, "Replace pump seal",
                CorrectivePriority.High, "Mechanical Team"),
            default);

        outcome.Succeeded.Should().BeFalse();
        outcome.Error.Should().Contain("Fail");
        (await _harness.Store.GetAsync(default)).Actions.Should().NotContain(a => a.SourceInspectionEventId == completed.Event.Id);
    }

    [Test]
    public async Task An_inspection_appends_to_the_asset_history_newest_first()
    {
        var draft = await DraftForSealAsync("1.6", Disposition.Pass);
        _harness.Time.Advance(TimeSpan.FromHours(4));

        var outcome = await _harness.Workflow.CompleteAsync(draft, default);

        var history = await new InspectionRepository(_harness.Store).GetHistoryAsync("P-204", default);

        history.First().Id.Should().Be(outcome.Event!.Id);
        history.Should().BeInDescendingOrder(e => e.Timestamp);
    }
}
