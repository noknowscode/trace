using Trace.Infrastructure.Repositories;

namespace Trace.Tests;

/// <summary>
/// Corrective work moving on from Findings: Open, Dispatched, Closed, each step written with
/// its sync operation, one step per request.
/// </summary>
public class ActionDispatchTests
{
    private TestHarness _harness = null!;

    [SetUp]
    public void SetUp() => _harness = TestHarness.Create();

    [TearDown]
    public void TearDown() => _harness.Dispose();

    /// <summary>Fails the seal and raises an open action from it.</summary>
    private async Task<CorrectiveAction> RaiseOpenActionAsync()
    {
        var point = await _harness.PointAsync("P-204-seal");
        var draft = await _harness.Workflow.StartOrResumeDraftAsync(point, default);
        var completed = await _harness.Workflow.CompleteAsync(
            draft with { ReadingText = "7.4", Disposition = Disposition.Fail, Notes = "Seal weeping." }, default);
        var raised = await _harness.Workflow.RaiseCorrectiveActionAsync(
            new CorrectiveActionRequest(
                "P-204", "P-204-seal", completed.Event!.Id, "Replace pump seal",
                CorrectivePriority.High, "Mechanical Team"),
            default);
        return raised.Action!;
    }

    [TestCase(ActionStatus.Open, ActionStatus.Dispatched)]
    [TestCase(ActionStatus.Dispatched, ActionStatus.Closed)]
    public void Each_status_has_one_next_step(ActionStatus from, ActionStatus to) =>
        InspectionRules.NextStatus(from).Should().Be(to);

    [Test]
    public void A_closed_action_has_no_next_step() =>
        InspectionRules.NextStatus(ActionStatus.Closed).Should().BeNull();

    [Test]
    public async Task Dispatching_writes_the_status_and_queues_its_update()
    {
        var action = await RaiseOpenActionAsync();

        var outcome = await _harness.Dispatch.AdvanceAsync(action.Id, ActionStatus.Open, default);

        outcome.Succeeded.Should().BeTrue();
        outcome.WasDuplicate.Should().BeFalse();
        outcome.Action!.Status.Should().Be(ActionStatus.Dispatched);

        var stored = await _harness.Reopen().GetAsync(default);
        stored.Actions.Single(a => a.Id == action.Id).Status.Should().Be(ActionStatus.Dispatched, "the step survives a relaunch");
        stored.Queue.Should().ContainSingle(o => o.EntityId == action.Id && o.Operation == "update" && o.Status == SyncState.Pending);
    }

    [Test]
    public async Task A_dispatched_action_closes_and_stops_counting_as_open()
    {
        var action = await RaiseOpenActionAsync();
        var openBefore = (await _harness.Store.GetAsync(default)).OpenFindingsFor("P-204");
        await _harness.Dispatch.AdvanceAsync(action.Id, ActionStatus.Open, default);

        var outcome = await _harness.Dispatch.AdvanceAsync(action.Id, ActionStatus.Dispatched, default);

        outcome.Action!.Status.Should().Be(ActionStatus.Closed);
        outcome.Action.CanAdvance.Should().BeFalse();
        (await _harness.Store.GetAsync(default)).OpenFindingsFor("P-204").Should().Be(openBefore - 1);
    }

    [Test]
    public async Task A_second_tap_takes_one_step_not_two()
    {
        var action = await RaiseOpenActionAsync();

        await _harness.Dispatch.AdvanceAsync(action.Id, ActionStatus.Open, default);
        var again = await _harness.Dispatch.AdvanceAsync(action.Id, ActionStatus.Open, default);

        again.Succeeded.Should().BeTrue();
        again.WasDuplicate.Should().BeTrue();
        var snapshot = await _harness.Store.GetAsync(default);
        snapshot.Actions.Single(a => a.Id == action.Id).Status.Should().Be(ActionStatus.Dispatched, "not Closed");
        snapshot.Queue.Count(o => o.EntityId == action.Id && o.Operation == "update").Should().Be(1);
    }

    [Test]
    public async Task A_closed_action_and_an_unknown_one_are_refused()
    {
        var action = await RaiseOpenActionAsync();

        (await _harness.Dispatch.AdvanceAsync(action.Id, ActionStatus.Closed, default)).Succeeded.Should().BeFalse();
        (await _harness.Dispatch.AdvanceAsync("ca-missing", ActionStatus.Open, default)).Error.Should().Contain("no longer on the device");
    }

    [Test]
    public async Task A_status_update_syncs_like_any_other_write()
    {
        var action = await RaiseOpenActionAsync();
        await _harness.Dispatch.AdvanceAsync(action.Id, ActionStatus.Open, default);

        await _harness.Queue.SyncAsync(default);

        var operations = (await _harness.Store.GetAsync(default)).Queue.Where(o => o.EntityId == action.Id).ToList();
        operations.Should().OnlyContain(o => o.Status == SyncState.Synced);
        operations.Select(o => o.KindLabel).Should().BeEquivalentTo(["Corrective action", "Action status"]);
    }

    [TestCase("Assigned", ActionStatus.Dispatched)]
    [TestCase("InProgress", ActionStatus.Dispatched)]
    [TestCase("Complete", ActionStatus.Closed)]
    [TestCase("Dispatched", ActionStatus.Dispatched)]
    public async Task Statuses_written_by_earlier_builds_still_load(string written, ActionStatus read)
    {
        await _harness.Store.GetAsync(default);
        var json = (await _harness.Files.ReadTextAsync("actions.json", default))!;
        json.Should().Contain("\"status\": \"Dispatched\"", "the seeded action is dispatched");
        await _harness.Files.WriteTextAsync("actions.json", json.Replace("\"status\": \"Dispatched\"", $"\"status\": \"{written}\""), default);

        var reopened = await _harness.Reopen().GetAsync(default);

        reopened.SetAside.Should().BeEmpty("an earlier build's actions file is not unreadable");
        reopened.Actions.Single(a => a.Id == "ca-1001").Status.Should().Be(read);
    }
}
