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
}
