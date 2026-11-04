using Trace.Presentation;

namespace Trace.Tests;

/// <summary>
/// Corrective work raised from the point inspector, through the sheet, over a real store.
/// </summary>
public class CorrectiveActionSheetModelTests
{
    private TestHarness _harness = null!;

    [SetUp]
    public void SetUp() => _harness = TestHarness.Create();

    [TearDown]
    public void TearDown() => _harness.Dispose();

    private async Task<IImmutableList<CorrectiveAction>> ActionsAsync() =>
        (await _harness.Store.GetAsync(default)).Actions;

    /// <summary>Opens the seal housing, fails it with a note and completes it.</summary>
    private async Task<AssetModel> FailTheSealAsync(ITraceStore? store = null)
    {
        var model = await _harness.OpenAssetAsync(store: store);
        await model.SelectPoint(await _harness.PointAsync("P-204-seal"), default);
        await Editing.EnterAsync(model.InspectorModel, "7.4", Disposition.Fail, "Seal weeping at the gland.");
        await model.InspectorModel.CompletePoint(default);
        return model;
    }

    [Test]
    public async Task Corrective_work_is_offered_only_from_an_inspection_recorded_as_fail()
    {
        var model = await _harness.OpenAssetAsync();
        await model.SelectPoint(await _harness.PointAsync("P-204-seal"), default);
        var inspector = model.InspectorModel;
        await Editing.EnterAsync(inspector, "1.6", Disposition.Pass);
        await inspector.CompletePoint(default);

        // Tapping Fail after a Pass was recorded used to offer corrective work raised from the
        // passed event.
        await inspector.ChosenDisposition.UpdateAsync(_ => Disposition.Fail, default);
        await TestHarness.UntilAsync(async () => (await inspector.Draft)?.Disposition == Disposition.Fail);

        (await inspector.CanRaiseAction).Should().BeFalse();

        await inspector.OpenActionSheet(default);
        (await model.ActionSheetModel.IsOpen).Should().BeFalse("nothing failed, so there is nothing to raise");

        var before = await ActionsAsync();
        await model.ActionSheetModel.Summary.UpdateAsync(_ => "Replace seal", default);
        await model.ActionSheetModel.Create(default);
        (await ActionsAsync()).Should().HaveCount(before.Count);
    }

    [Test]
    public async Task A_failed_point_raises_work_against_its_own_event()
    {
        var model = await FailTheSealAsync();
        var inspector = model.InspectorModel;
        var sheet = model.ActionSheetModel;

        (await inspector.CanRaiseAction).Should().BeTrue();
        var recorded = (await inspector.RecordedEvent)!;

        await inspector.OpenActionSheet(default);
        (await sheet.IsOpen).Should().BeTrue();
        (await sheet.Summary).Should().Be("Inspect seal housing on P-204", "the summary is suggested from the failed point");

        await sheet.Create(default);

        var action = (await ActionsAsync()).Should().ContainSingle(a => a.SourceInspectionEventId == recorded.Id).Subject;
        action.PointId.Should().Be("P-204-seal");
        action.AssetId.Should().Be("P-204");
        action.Priority.Should().Be(CorrectivePriority.High);
        action.Assignee.Should().Be("Mechanical Team");
        (await sheet.IsOpen).Should().BeFalse("the sheet closes once the work is raised");
    }

    [Test]
    public async Task The_raised_action_is_confirmed_in_the_inspector()
    {
        var model = await FailTheSealAsync();
        await model.InspectorModel.OpenActionSheet(default);
        await model.ActionSheetModel.Assignee.UpdateAsync(_ => "Priya S.", default);

        await model.ActionSheetModel.Create(default);

        await TestHarness.UntilAsync(async () => (await model.InspectorModel.Confirmation)?.StartsWith("Corrective action") == true);
        (await model.InspectorModel.Confirmation).Should().Be(
            "Corrective action raised for Seal housing and assigned to Priya S.",
            "the assignee's own full stop ends the sentence; it used to read \"Priya S..\"");
    }

    [Test]
    public async Task Once_work_is_raised_from_an_inspection_it_is_not_offered_again()
    {
        var model = await FailTheSealAsync();
        var inspector = model.InspectorModel;
        var sheet = model.ActionSheetModel;
        var recorded = (await inspector.RecordedEvent)!;
        await inspector.OpenActionSheet(default);
        await sheet.Create(default);

        // Create corrective action stayed on offer after success, and a second Create wrote a
        // second action and a second sync operation for the same failure.
        await TestHarness.UntilAsync(async () => !await inspector.CanRaiseAction);
        (await inspector.CanRaiseAction).Should().BeFalse();

        await inspector.OpenActionSheet(default);
        (await sheet.IsOpen).Should().BeFalse("there is nothing left to raise from that inspection");

        await sheet.Create(default);
        var snapshot = await _harness.Store.GetAsync(default);
        snapshot.Actions.Should().ContainSingle(a => a.SourceInspectionEventId == recorded.Id);
        snapshot.Queue.Count(o => o.EntityType == nameof(CorrectiveAction)).Should().Be(1);
    }

    [Test]
    public async Task A_new_failure_at_the_same_point_can_raise_its_own_action()
    {
        var model = await FailTheSealAsync();
        await model.InspectorModel.OpenActionSheet(default);
        await model.ActionSheetModel.Create(default);
        await TestHarness.UntilAsync(async () => !await model.InspectorModel.CanRaiseAction);

        await Editing.EnterAsync(model.InspectorModel, "8.1", Disposition.Fail, "Worse than this morning.");
        await model.InspectorModel.CompletePoint(default);

        (await model.InspectorModel.CanRaiseAction).Should().BeTrue("a new inspection failed");
        await model.InspectorModel.OpenActionSheet(default);
        await model.ActionSheetModel.Create(default);
        (await ActionsAsync()).Count(a => a.PointId == "P-204-seal" && a.Status == ActionStatus.Open).Should().Be(2);
    }

    [Test]
    public async Task A_summary_typed_for_the_same_inspection_survives_cancel()
    {
        var model = await FailTheSealAsync();
        var sheet = model.ActionSheetModel;
        await model.InspectorModel.OpenActionSheet(default);
        await sheet.Summary.UpdateAsync(_ => "Replace the mechanical seal", default);

        await sheet.Cancel(default);
        (await sheet.IsOpen).Should().BeFalse();
        await model.InspectorModel.OpenActionSheet(default);

        (await sheet.Summary).Should().Be("Replace the mechanical seal");
    }

    [Test]
    public async Task A_blank_summary_cannot_be_created()
    {
        var model = await FailTheSealAsync();
        var sheet = model.ActionSheetModel;
        await model.InspectorModel.OpenActionSheet(default);

        await sheet.Summary.UpdateAsync(_ => "   ", default);

        (await sheet.CanCreate).Should().BeFalse();
        await sheet.Create(default);
        (await sheet.Error).Should().Contain("summary is required");
        (await ActionsAsync()).Should().NotContain(a => a.Summary.Length == 0);
    }

    [Test]
    public async Task Choosing_another_point_forgets_the_failure_recorded_at_the_last_one()
    {
        var model = await FailTheSealAsync();
        (await model.InspectorModel.CanRaiseAction).Should().BeTrue();
        await model.InspectorModel.OpenActionSheet(default);

        // Opening the coupling used to keep the seal's event, so an action raised there
        // carried the coupling's name and the seal's inspection.
        await model.SelectPoint(await _harness.PointAsync("P-204-coupling"), default);

        (await model.InspectorModel.CanRaiseAction).Should().BeFalse();
        (await model.InspectorModel.RecordedEvent).Should().BeNull();
        (await model.ActionSheetModel.IsOpen).Should().BeFalse();
        (await model.ActionSheetModel.Source).Should().BeNull();

        var before = await ActionsAsync();
        await model.ActionSheetModel.Summary.UpdateAsync(_ => "Replace coupling", default);
        await model.ActionSheetModel.Create(default);
        (await ActionsAsync()).Should().HaveCount(before.Count);
    }

    [Test]
    public async Task A_corrective_action_the_device_cannot_write_is_reported_on_the_sheet()
    {
        var files = _harness.BreakableFiles();
        var model = await FailTheSealAsync(_harness.StoreOver(files));
        (await model.InspectorModel.CanRaiseAction).Should().BeTrue();
        await model.InspectorModel.OpenActionSheet(default);

        files.Broken = true;

        // The store's IOException used to escape the command, and the sheet stayed open with no word.
        await model.ActionSheetModel.Create(default);

        (await model.ActionSheetModel.Error).Should().Contain("could not be saved");
        (await model.ActionSheetModel.IsOpen).Should().BeTrue("the form stays for another try");
        var recorded = (await model.InspectorModel.RecordedEvent)!;
        (await _harness.Reopen().GetAsync(default)).Actions
            .Should().NotContain(a => a.SourceInspectionEventId == recorded.Id);
    }
}
