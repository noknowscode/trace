using Trace.Presentation;

namespace Trace.Tests;

/// <summary>The supervisor's Findings: what is open, what is at risk, and dispatching from a row.</summary>
public class SupervisorFindingsModelTests
{
    private TestHarness _harness = null!;

    [SetUp]
    public void SetUp() => _harness = TestHarness.Create();

    [TearDown]
    public void TearDown() => _harness.Dispose();

    private SupervisorFindingsModel FindingsOver(ITraceStore store, IActionDispatch? dispatch = null) => new(
        new Trace.Infrastructure.Repositories.CorrectiveActionRepository(store),
        new Trace.Infrastructure.Repositories.AssetRepository(store),
        dispatch ?? _harness.Dispatch,
        new FeedRecovery());

    private async Task<CorrectiveAction> AddOpenActionAsync(ITraceStore store)
    {
        var seeded = (await store.GetAsync(default)).Actions.First();
        var open = seeded with { Id = "ca-2001", Status = ActionStatus.Open, Summary = "Realign coupling", PointId = "P-204-coupling", PointLabel = "Coupling" };
        await store.MutateAsync(s => s with { Actions = s.Actions.Add(open) }, default);
        return open;
    }

    [Test]
    public async Task The_headline_counts_open_work_and_what_waits_for_dispatch()
    {
        await AddOpenActionAsync(_harness.Store);
        var model = FindingsOver(_harness.Store);

        (await model.Headline).Should().Be("2 corrective actions are open. 1 is waiting to be dispatched.");
    }

    [Test]
    public async Task Dispatching_a_row_moves_it_on_and_the_list_follows()
    {
        var open = await AddOpenActionAsync(_harness.Store);
        var model = FindingsOver(_harness.Store);

        await model.Advance(open, default);

        await TestHarness.UntilAsync(async () => (await model.Actions).Single(a => a.Id == open.Id).Status == ActionStatus.Dispatched);
        (await model.Actions).Single(a => a.Id == open.Id).NextStep.Should().Be("Close");
        (await model.DispatchError).Should().BeNullOrEmpty();
        (await model.Headline).Should().Be("2 corrective actions are open.");
    }

    [Test]
    public async Task Closed_work_drops_to_the_end_of_the_list()
    {
        var open = await AddOpenActionAsync(_harness.Store);
        var model = FindingsOver(_harness.Store);
        var seeded = (await model.Actions).Single(a => a.Id == "ca-1001");

        await model.Advance(seeded, default);

        await TestHarness.UntilAsync(async () => (await model.Actions).Last().Id == "ca-1001");
        var actions = await model.Actions;
        actions.Last().Status.Should().Be(ActionStatus.Closed);
        actions.First().Id.Should().Be(open.Id);
        (await model.Headline).Should().StartWith("1 corrective action is open.");
    }

    [Test]
    public async Task A_step_the_device_cannot_write_is_reported()
    {
        var files = _harness.BreakableFiles();
        var store = _harness.StoreOver(files);
        var open = await AddOpenActionAsync(store);
        var model = FindingsOver(store, new Trace.Infrastructure.Repositories.ActionDispatch(
            store, _harness.Time, Microsoft.Extensions.Logging.Abstractions.NullLogger<Trace.Infrastructure.Repositories.ActionDispatch>.Instance));

        files.Broken = true;
        await model.Advance(open, default);

        (await model.DispatchError).Should().Contain("could not be updated");
        (await _harness.Reopen().GetAsync(default)).Actions.Single(a => a.Id == open.Id).Status.Should().Be(ActionStatus.Open);
    }

    [Test]
    public async Task Assets_needing_attention_are_listed_worst_first()
    {
        var model = FindingsOver(_harness.Store);

        var atRisk = await model.AtRisk;

        atRisk.Should().NotBeEmpty();
        atRisk.Select(a => a.Condition).Should().BeInDescendingOrder("a failed asset comes before one on watch");
    }
}
