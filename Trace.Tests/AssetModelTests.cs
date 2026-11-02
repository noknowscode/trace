using Trace.Presentation;
using Uno.Extensions.Reactive;

namespace Trace.Tests;

/// <summary>
/// The asset workspace's commands over a real store: what the technician does at a point and
/// what ends up recorded. MVUX folds the editors into the draft in the background, so a test
/// waits for the draft to show an edit before it acts on it.
/// </summary>
public class AssetModelTests
{
    private TestHarness _harness = null!;

    [SetUp]
    public void SetUp() => _harness = TestHarness.Create();

    [TearDown]
    public void TearDown() => _harness.Dispose();

    private async Task<AssetModel> OpenAtAsync(string pointId)
    {
        var model = await _harness.OpenAssetAsync();
        await model.SelectPoint(await _harness.PointAsync(pointId), default);
        return model;
    }

    /// <summary>Types a reading and a note and makes a call, as the inspector's fields do.</summary>
    private static async Task EnterAsync(AssetModel model, string reading, Disposition disposition, string notes = "")
    {
        await model.ReadingText.UpdateAsync(_ => reading, default);
        await model.NoteText.UpdateAsync(_ => notes, default);
        await model.ChosenDisposition.UpdateAsync(_ => disposition, default);

        await TestHarness.UntilAsync(async () => await model.Draft is { } draft
            && draft.ReadingText == reading
            && draft.Notes == notes
            && draft.Disposition == disposition);
    }

    private async Task<IImmutableList<CorrectiveAction>> ActionsAsync() =>
        (await _harness.Store.GetAsync(default)).Actions;

    [Test]
    public async Task Completing_a_point_editing_it_and_completing_it_again_records_two_inspections()
    {
        var before = (await _harness.Store.GetAsync(default)).Events.Count(e => e.PointId == "P-204-seal");
        var model = await OpenAtAsync("P-204-seal");

        await EnterAsync(model, "1.6", Disposition.Pass);
        await model.CompletePoint(default);

        // The editor kept the completed draft, saved it back on the next edit, and every
        // later Complete at this point was reported as "Already recorded" and wrote nothing.
        await EnterAsync(model, "2.1", Disposition.Watch);
        await model.CompletePoint(default);

        var events = (await _harness.Store.GetAsync(default)).Events.Where(e => e.PointId == "P-204-seal").ToList();
        events.Should().HaveCount(before + 2);
        events.Should().ContainSingle(e => e.Reading == 2.1 && e.Disposition == Disposition.Watch);
        (await model.Confirmation).Should().StartWith("Seal housing recorded as Watch");
    }

    [Test]
    public async Task Corrective_work_is_offered_only_from_an_inspection_recorded_as_fail()
    {
        var model = await OpenAtAsync("P-204-seal");
        await EnterAsync(model, "1.6", Disposition.Pass);
        await model.CompletePoint(default);

        // Tapping Fail after a Pass was recorded used to offer corrective work raised from the
        // passed event.
        await model.ChosenDisposition.UpdateAsync(_ => Disposition.Fail, default);
        await TestHarness.UntilAsync(async () => (await model.Draft)?.Disposition == Disposition.Fail);

        (await model.CanRaiseAction).Should().BeFalse();

        var before = await ActionsAsync();
        await model.ActionSummary.UpdateAsync(_ => "Replace seal", default);
        await model.CreateAction(default);
        (await ActionsAsync()).Should().HaveCount(before.Count);
    }

    [Test]
    public async Task A_failed_point_raises_work_against_its_own_event()
    {
        var model = await OpenAtAsync("P-204-seal");
        await EnterAsync(model, "7.4", Disposition.Fail, "Seal weeping at the gland.");
        await model.CompletePoint(default);

        (await model.CanRaiseAction).Should().BeTrue();
        var recorded = (await model.RecordedEvent)!;

        await model.CreateAction(default);

        var action = (await ActionsAsync()).Should().ContainSingle(a => a.SourceInspectionEventId == recorded.Id).Subject;
        action.PointId.Should().Be("P-204-seal");
        action.Summary.Should().Be("Inspect seal housing on P-204", "the summary is suggested from the failed point");
    }

    [Test]
    public async Task Choosing_another_point_forgets_the_failure_recorded_at_the_last_one()
    {
        var model = await OpenAtAsync("P-204-seal");
        await EnterAsync(model, "7.4", Disposition.Fail, "Seal weeping at the gland.");
        await model.CompletePoint(default);
        (await model.CanRaiseAction).Should().BeTrue();

        // Opening the coupling used to keep the seal's event, so an action raised there
        // carried the coupling's name and the seal's inspection.
        await model.SelectPoint(await _harness.PointAsync("P-204-coupling"), default);

        (await model.CanRaiseAction).Should().BeFalse();
        (await model.RecordedEvent).Should().BeNull();

        var before = await ActionsAsync();
        await model.ActionSummary.UpdateAsync(_ => "Replace coupling", default);
        await model.CreateAction(default);
        (await ActionsAsync()).Should().HaveCount(before.Count);
    }

    [Test]
    public async Task A_completion_the_device_cannot_write_is_reported_in_the_inspector()
    {
        await _harness.Store.GetAsync(default);
        var model = await _harness.OpenAssetAsync(store: _harness.StoreThatDiesWriting("commit.json"));
        await model.SelectPoint(await _harness.PointAsync("P-204-seal"), default);
        await EnterAsync(model, "1.6", Disposition.Pass);

        // The store's IOException used to escape the command, and the inspector showed nothing.
        await model.CompletePoint(default);

        (await model.CompletionError).Should().Contain("could not be saved");
        (await model.Confirmation).Should().BeNullOrEmpty();
        (await model.Draft)!.ReadingText.Should().Be("1.6", "the reading stays for another try");
    }

    [Test]
    public async Task A_completion_for_an_asset_that_has_gone_says_why_nothing_was_recorded()
    {
        var model = await OpenAtAsync("P-204-seal");
        await EnterAsync(model, "1.6", Disposition.Pass);

        await _harness.Store.MutateAsync(s => s with { Assets = s.Assets.RemoveAll(a => a.Id == "P-204") }, default);
        await model.CompletePoint(default);

        (await model.CompletionError).Should().Contain("no longer available");
        (await model.Confirmation).Should().BeNullOrEmpty();
    }

    [Test]
    public async Task A_point_with_nothing_entered_offers_no_discard()
    {
        var model = await OpenAtAsync("P-204-seal");

        (await model.CanDiscard).Should().BeFalse("a pristine draft has nothing to lose");
    }

    [Test]
    public async Task Discarding_asks_first_then_starts_the_point_afresh()
    {
        var model = await OpenAtAsync("P-204-seal");
        await EnterAsync(model, "1.6", Disposition.Fail, "Weeping at the gland.");
        var entered = (await model.Draft)!;
        await TestHarness.UntilAsync(async () =>
            (await _harness.Store.GetAsync(default)).FindDraft("P-204-seal")?.Notes == "Weeping at the gland.");

        (await model.CanDiscard).Should().BeTrue();

        await model.AskDiscard(default);
        (await model.IsDiscardPending).Should().BeTrue();
        (await _harness.Store.GetAsync(default)).FindDraft("P-204-seal")!.Notes
            .Should().Be("Weeping at the gland.", "nothing is thrown away until the technician confirms");

        await model.DiscardDraft(default);

        (await model.IsDiscardPending).Should().BeFalse();
        var stored = (await _harness.Store.GetAsync(default)).FindDraft("P-204-seal")!;
        stored.ClientToken.Should().NotBe(entered.ClientToken);
        stored.Notes.Should().BeEmpty();
        stored.Disposition.Should().Be(Disposition.None);
        (await model.NoteText).Should().BeEmpty();
        (await model.ReadingText).Should().Be("7.2", "the point starts again from its last reading");
    }
}
