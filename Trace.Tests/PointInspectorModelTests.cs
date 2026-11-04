using Trace.Presentation;

namespace Trace.Tests;

/// <summary>
/// The point inspector's commands over a real store: what the technician does at a point and
/// what ends up recorded. MVUX folds the editors into the draft in the background, so a test
/// waits for the draft to show an edit before it acts on it.
/// </summary>
public class PointInspectorModelTests
{
    private TestHarness _harness = null!;

    [SetUp]
    public void SetUp() => _harness = TestHarness.Create();

    [TearDown]
    public void TearDown() => _harness.Dispose();

    private async Task<PointInspectorModel> OpenAtAsync(string pointId, ITraceStore? store = null)
    {
        var model = await _harness.OpenAssetAsync(store: store);
        await model.SelectPoint(await _harness.PointAsync(pointId), default);
        return model.InspectorModel;
    }

    [Test]
    public async Task Opening_a_point_shows_it_with_a_draft_from_its_last_reading()
    {
        var inspector = await OpenAtAsync("P-204-seal");

        (await inspector.IsOpen).Should().BeTrue();
        (await inspector.SelectedPoint)!.Label.Should().Be("Seal housing");
        (await inspector.ReadingText).Should().Be("7.2", "the draft starts from the point's last reading");
        (await inspector.ChosenDisposition).Should().Be(Disposition.None);
        (await inspector.CanComplete).Should().BeFalse("no call has been made yet");
    }

    [Test]
    public async Task An_out_of_range_reading_asks_for_a_call_and_never_makes_one()
    {
        var inspector = await OpenAtAsync("P-204-seal");
        await Editing.EnterAsync(inspector, "7.4", Disposition.None);

        (await inspector.IsOutOfRange).Should().BeTrue();
        (await inspector.ChosenDisposition).Should().Be(Disposition.None, "out of range prompts a call; it does not force Fail");
        (await inspector.CanComplete).Should().BeFalse();
    }

    [Test]
    public async Task A_fail_without_a_note_or_photo_cannot_be_completed()
    {
        var inspector = await OpenAtAsync("P-204-seal");
        await Editing.EnterAsync(inspector, "7.4", Disposition.Fail);

        (await inspector.HasBlocker).Should().BeTrue();
        (await inspector.CanComplete).Should().BeFalse();

        await Editing.EnterAsync(inspector, "7.4", Disposition.Fail, "Seal weeping at the gland.");
        (await inspector.CanComplete).Should().BeTrue("a note satisfies a failed point");
    }

    [Test]
    public async Task Completing_a_point_editing_it_and_completing_it_again_records_two_inspections()
    {
        var before = (await _harness.Store.GetAsync(default)).Events.Count(e => e.PointId == "P-204-seal");
        var inspector = await OpenAtAsync("P-204-seal");

        await Editing.EnterAsync(inspector, "1.6", Disposition.Pass);
        await inspector.CompletePoint(default);

        // The editor kept the completed draft, saved it back on the next edit, and every
        // later Complete at this point was reported as "Already recorded" and wrote nothing.
        await Editing.EnterAsync(inspector, "2.1", Disposition.Watch);
        await inspector.CompletePoint(default);

        var events = (await _harness.Store.GetAsync(default)).Events.Where(e => e.PointId == "P-204-seal").ToList();
        events.Should().HaveCount(before + 2);
        events.Should().ContainSingle(e => e.Reading == 2.1 && e.Disposition == Disposition.Watch);
        (await inspector.Confirmation).Should().StartWith("Seal housing recorded as Watch");
    }

    [Test]
    public async Task A_completion_the_device_cannot_write_is_reported_in_the_inspector()
    {
        await _harness.Store.GetAsync(default);
        var inspector = await OpenAtAsync("P-204-seal", _harness.StoreThatDiesWriting("commit.json"));
        await Editing.EnterAsync(inspector, "1.6", Disposition.Pass);

        // The store's IOException used to escape the command, and the inspector showed nothing.
        await inspector.CompletePoint(default);

        (await inspector.CompletionError).Should().Contain("could not be saved");
        (await inspector.Confirmation).Should().BeNullOrEmpty();
        (await inspector.Draft)!.ReadingText.Should().Be("1.6", "the reading stays for another try");
    }

    [Test]
    public async Task A_completion_for_an_asset_that_has_gone_says_why_nothing_was_recorded()
    {
        var inspector = await OpenAtAsync("P-204-seal");
        await Editing.EnterAsync(inspector, "1.6", Disposition.Pass);

        await _harness.Store.MutateAsync(s => s with { Assets = s.Assets.RemoveAll(a => a.Id == "P-204") }, default);
        await inspector.CompletePoint(default);

        (await inspector.CompletionError).Should().Contain("no longer available");
        (await inspector.Confirmation).Should().BeNullOrEmpty();
    }

    [Test]
    public async Task A_point_with_nothing_entered_offers_no_discard()
    {
        var inspector = await OpenAtAsync("P-204-seal");

        (await inspector.CanDiscard).Should().BeFalse("a pristine draft has nothing to lose");
    }

    [Test]
    public async Task Discarding_asks_first_then_starts_the_point_afresh()
    {
        var inspector = await OpenAtAsync("P-204-seal");
        await Editing.EnterAsync(inspector, "1.6", Disposition.Fail, "Weeping at the gland.");
        var entered = (await inspector.Draft)!;
        await TestHarness.UntilAsync(async () =>
            (await _harness.Store.GetAsync(default)).FindDraft("P-204-seal")?.Notes == "Weeping at the gland.");

        (await inspector.CanDiscard).Should().BeTrue();

        await inspector.AskDiscard(default);
        (await inspector.IsDiscardPending).Should().BeTrue();
        (await _harness.Store.GetAsync(default)).FindDraft("P-204-seal")!.Notes
            .Should().Be("Weeping at the gland.", "nothing is thrown away until the technician confirms");

        await inspector.DiscardDraft(default);

        (await inspector.IsDiscardPending).Should().BeFalse();
        var stored = (await _harness.Store.GetAsync(default)).FindDraft("P-204-seal")!;
        stored.ClientToken.Should().NotBe(entered.ClientToken);
        stored.Notes.Should().BeEmpty();
        stored.Disposition.Should().Be(Disposition.None);
        (await inspector.NoteText).Should().BeEmpty();
        (await inspector.ReadingText).Should().Be("7.2", "the point starts again from its last reading");
    }

    [Test]
    public async Task Keeping_the_draft_cancels_the_discard_and_loses_nothing()
    {
        var inspector = await OpenAtAsync("P-204-seal");
        await Editing.EnterAsync(inspector, "1.6", Disposition.Watch, "Slight weep.");

        await inspector.AskDiscard(default);
        await inspector.KeepDraft(default);

        (await inspector.IsDiscardPending).Should().BeFalse();
        (await inspector.Draft)!.Notes.Should().Be("Slight weep.");
    }

    [Test]
    public async Task A_photo_the_device_cannot_keep_is_reported_and_attaches_nothing()
    {
        var files = _harness.BreakableFiles();
        files.Broken = true;
        var model = await _harness.OpenAssetAsync(
            camera: new FrameCamera(),
            evidence: new Trace.Infrastructure.Platform.LocalEvidenceStore(files, _harness.Time));
        await model.SelectPoint(await _harness.PointAsync("P-204-seal"), default);
        var inspector = model.InspectorModel;

        // The evidence store's IOException used to escape the command, and Add photo did nothing visible.
        await inspector.AttachPhoto(default);

        (await inspector.EvidenceError).Should().Contain("could not be saved");
        (await inspector.Draft)!.Evidence.Should().BeEmpty();

        files.Broken = false;
        await inspector.AttachPhoto(default);

        (await inspector.EvidenceError).Should().BeNullOrEmpty("a photo that was kept clears the message");
        (await inspector.Draft)!.Evidence.Should().ContainSingle();
        (await inspector.HasEvidence).Should().BeTrue();
    }

    [Test]
    public async Task Closing_the_inspector_keeps_the_draft_and_clears_the_selection()
    {
        var inspector = await OpenAtAsync("P-204-seal");
        await Editing.EnterAsync(inspector, "3.0", Disposition.Watch, "Rising.");
        await TestHarness.UntilAsync(async () =>
            (await _harness.Store.GetAsync(default)).FindDraft("P-204-seal")?.Notes == "Rising.");

        await inspector.Close(default);

        (await inspector.IsOpen).Should().BeFalse();
        (await inspector.SelectedPointId).Should().BeEmpty();
        (await _harness.Store.GetAsync(default)).FindDraft("P-204-seal")!.ReadingText
            .Should().Be("3.0", "leaving a point keeps what was entered");
    }
}

/// <summary>Types into the inspector's fields the way the view does, and waits for the draft to take it.</summary>
internal static class Editing
{
    public static async Task EnterAsync(PointInspectorModel inspector, string reading, Disposition disposition, string notes = "")
    {
        await inspector.ReadingText.UpdateAsync(_ => reading, default);
        await inspector.NoteText.UpdateAsync(_ => notes, default);
        await inspector.ChosenDisposition.UpdateAsync(_ => disposition, default);

        await TestHarness.UntilAsync(async () => await inspector.Draft is { } draft
            && draft.ReadingText == reading
            && draft.Notes == notes
            && draft.Disposition == disposition);
    }
}

/// <summary>The inspector when the device cannot keep the draft: it says so, and the entry stays on screen.</summary>
public class PointInspectorStorageTests
{
    private TestHarness _harness = null!;

    [SetUp]
    public void SetUp() => _harness = TestHarness.Create();

    [TearDown]
    public void TearDown() => _harness.Dispose();

    [Test]
    public async Task A_draft_the_device_cannot_save_is_reported_and_the_entry_stays()
    {
        var files = _harness.BreakableFiles();
        var model = await _harness.OpenAssetAsync(store: _harness.StoreOver(files));
        await model.SelectPoint(await _harness.PointAsync("P-204-seal"), default);
        var inspector = model.InspectorModel;

        files.Broken = true;

        // The autosave's IOException used to vanish in the background, so a technician typed
        // on into a draft that would not survive the app closing.
        await Editing.EnterAsync(inspector, "3.1", Disposition.Watch, "Rising since last round.");
        await TestHarness.UntilAsync(async () => !string.IsNullOrEmpty(await inspector.DraftError));

        (await inspector.DraftError).Should().Contain("could not be saved");
        (await inspector.ReadingText).Should().Be("3.1", "the entry stays in the editor");

        files.Broken = false;
        await Editing.EnterAsync(inspector, "3.2", Disposition.Watch, "Rising since last round.");
        await TestHarness.UntilAsync(async () => string.IsNullOrEmpty(await inspector.DraftError));

        (await inspector.DraftError).Should().BeNullOrEmpty("a draft that was saved clears the message");
        (await _harness.Reopen().GetAsync(default)).FindDraft("P-204-seal")!.ReadingText.Should().Be("3.2");
    }

    [Test]
    public async Task A_point_whose_draft_cannot_be_kept_still_opens()
    {
        var files = _harness.BreakableFiles();
        var model = await _harness.OpenAssetAsync(store: _harness.StoreOver(files));
        files.Broken = true;

        // Starting the draft writes it; the IOException used to escape SelectPoint, and the
        // point never opened.
        await model.SelectPoint(await _harness.PointAsync("P-204-seal"), default);

        var inspector = model.InspectorModel;
        (await inspector.IsOpen).Should().BeTrue();
        (await inspector.DraftError).Should().Contain("could not be saved");
        (await inspector.ReadingText).Should().Be("7.2", "the point opens on a fresh draft held in memory");
    }

    [Test]
    public async Task A_discard_the_device_cannot_write_keeps_what_was_entered()
    {
        var files = _harness.BreakableFiles();
        var store = _harness.StoreOver(files);
        var model = await _harness.OpenAssetAsync(store: store);
        await model.SelectPoint(await _harness.PointAsync("P-204-seal"), default);
        var inspector = model.InspectorModel;
        await Editing.EnterAsync(inspector, "1.6", Disposition.Fail, "Weeping at the gland.");
        await TestHarness.UntilAsync(async () =>
            (await store.GetAsync(default)).FindDraft("P-204-seal")?.Notes == "Weeping at the gland.");

        files.Broken = true;
        await inspector.AskDiscard(default);
        await inspector.DiscardDraft(default);

        (await inspector.DraftError).Should().Contain("could not be discarded");
        (await inspector.NoteText).Should().Be("Weeping at the gland.", "nothing was thrown away");
        (await _harness.Reopen().GetAsync(default)).FindDraft("P-204-seal")!.Notes.Should().Be("Weeping at the gland.");
    }
}
