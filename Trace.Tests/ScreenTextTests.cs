using Trace.Presentation;

namespace Trace.Tests;

/// <summary>The words the screens put on domain values, kept out of the domain.</summary>
public class ScreenTextTests
{
    private static CorrectiveAction Action(ActionStatus status, string assignee = "Mechanical Team") => new(
        "ca-1", "ev-1", "P-204", "Centrifugal Pump", "P-204-seal", "Seal housing",
        CorrectivePriority.High, "Replace the seal", assignee, status, DateTimeOffset.UnixEpoch);

    [TestCase(ActionStatus.Open, "Dispatch")]
    [TestCase(ActionStatus.Dispatched, "Close")]
    [TestCase(ActionStatus.Closed, "")]
    public void A_finding_s_button_names_its_next_step(ActionStatus status, string step)
    {
        ScreenText.NextStep(Action(status)).Should().Be(step);
    }

    [Test]
    public void A_screen_reader_hears_the_step_and_the_work()
    {
        ScreenText.NextStepName(Action(ActionStatus.Open)).Should().Be("Dispatch Replace the seal");
    }

    [TestCase(nameof(InspectionEvent), "create", "Inspection")]
    [TestCase(nameof(CorrectiveAction), "create", "Corrective action")]
    [TestCase(nameof(CorrectiveAction), "update", "Action status")]
    public void A_sync_row_names_the_change(string entity, string operation, string kind)
    {
        var queued = new SyncOperation("op-1", entity, "x", operation, "d", DateTimeOffset.UnixEpoch, SyncState.Pending, 0, false, null);

        ScreenText.SyncKind(queued).Should().Be(kind);
    }

    [Test]
    public void The_confirmation_ends_once_whatever_the_assignee()
    {
        ScreenText.ActionRaised(Action(ActionStatus.Open)).Should().Be("Corrective action raised for Seal housing and assigned to Mechanical Team.");
        ScreenText.ActionRaised(Action(ActionStatus.Open, "Priya S.")).Should().Be("Corrective action raised for Seal housing and assigned to Priya S.");
    }

    private static AssetSummary Card(AssetCondition condition, int openFindings, DateTimeOffset? dueAt) => new(
        "a-1", "P-204", "Centrifugal Pump", "Centrifugal Pump", "Water Treatment", "Area B",
        "Morning", "Vibration trend", dueAt, 4, 3, condition, openFindings);

    [TestCase(AssetCondition.Failed, "Fail")]
    [TestCase(AssetCondition.Watch, "Watch")]
    [TestCase(AssetCondition.Healthy, "Pass")]
    [TestCase(AssetCondition.Unknown, "Not inspected")]
    public void An_asset_card_names_its_condition(AssetCondition condition, string label)
    {
        ScreenText.Condition(Card(condition, 0, null)).Should().Be(label);
    }

    [Test]
    public void An_asset_card_counts_its_points_and_its_open_findings()
    {
        ScreenText.Progress(Card(AssetCondition.Watch, 0, null)).Should().Be("3 of 4 points inspected");
        ScreenText.Findings(Card(AssetCondition.Watch, 1, null)).Should().Be("1 open finding");
        ScreenText.Findings(Card(AssetCondition.Watch, 2, null)).Should().Be("2 open findings");
    }

    [Test]
    public void Only_a_scheduled_asset_says_when_it_is_due()
    {
        var due = new DateTimeOffset(2026, 11, 4, 9, 30, 0, TimeSpan.Zero);

        ScreenText.Due(Card(AssetCondition.Healthy, 0, due)).Should().Be($"Due {due:t}");
        ScreenText.Due(Card(AssetCondition.Healthy, 0, null)).Should().BeEmpty();
    }
}
