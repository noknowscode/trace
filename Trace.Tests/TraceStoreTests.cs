using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Trace.Infrastructure.Repositories;
using Trace.Presentation;
using Uno.Extensions.Reactive;

namespace Trace.Tests;

/// <summary>
/// Store behaviour the whole app leans on: a seeded first run, durable writes, and list
/// projections that stay correct as state moves.
/// </summary>
public class TraceStoreTests
{
    private TestHarness _harness = null!;

    [SetUp]
    public void SetUp() => _harness = TestHarness.Create();

    [TearDown]
    public void TearDown() => _harness.Dispose();

    [Test]
    public async Task A_first_run_seeds_the_round_described_in_the_build_spec()
    {
        var snapshot = await _harness.Store.GetAsync(default);

        snapshot.Assignments.Should().HaveCount(3);
        snapshot.Assets.Select(a => a.Id).Should().Contain(["P-204", "AHU-12", "C-07"]);

        var pump = snapshot.FindAsset("P-204")!;
        pump.Area.Should().Be("Area B");
        pump.Site.Should().Be("Water Treatment");
        pump.Points.Select(p => p.Label).Should()
            .ContainInOrder("Motor body", "Seal housing", "Coupling", "Drive-end bearing");

        var seal = pump.FindPoint("P-204-seal")!;
        seal.LatestReading.Should().Be(7.2);
        seal.Unit.Should().Be("mm/s");
        seal.NormalMin.Should().Be(0);
        seal.NormalMax.Should().Be(2.8);
        seal.LatestRangeStatus.Should().Be(RangeStatus.Above);

        snapshot.Events.Count(e => e.AssetId == "P-204").Should().BeGreaterThanOrEqualTo(3);
        snapshot.Actions.Should().ContainSingle();
    }

    [Test]
    public async Task Seeded_state_is_written_to_disk_and_read_back_on_relaunch()
    {
        await _harness.Store.GetAsync(default);

        var relaunched = await _harness.Reopen().GetAsync(default);

        relaunched.Assets.Should().HaveCount(3);
        relaunched.FindAsset("P-204")!.FindPoint("P-204-seal")!.LatestReading.Should().Be(7.2);
    }

    [Test]
    public async Task An_asset_condition_rolls_up_the_worst_disposition_across_its_points()
    {
        var snapshot = await _harness.Store.GetAsync(default);

        // The seal housing is failed, so the pump is failed regardless of the other points.
        snapshot.FindAsset("P-204")!.Condition.Should().Be(AssetCondition.Failed);

        // AHU-12 has a watch on the drive belt and nothing worse.
        snapshot.FindAsset("AHU-12")!.Condition.Should().Be(AssetCondition.Watch);
    }

    [Test]
    public async Task A_point_that_has_never_been_read_does_not_count_as_inspected()
    {
        var compressor = (await _harness.Store.GetAsync(default)).FindAsset("C-07")!;

        compressor.PointCount.Should().Be(4);
        compressor.InspectedPointCount.Should().Be(3);
        compressor.FindPoint("C-07-drain")!.HasReading.Should().BeFalse();
    }

    [Test]
    public async Task Today_rows_carry_live_progress_and_open_finding_counts()
    {
        var repository = new AssignmentRepository(_harness.Store);
        var today = await TestHarness.FirstAsync(repository.WatchToday(default));

        today.Should().HaveCount(3);
        today.Select(r => r.AssetId).Should().ContainInOrder("P-204", "AHU-12", "C-07");

        var pump = today.First();
        pump.PointsTotal.Should().Be(4);
        pump.PointsInspected.Should().Be(4);
        pump.Condition.Should().Be(AssetCondition.Failed);
        pump.OpenFindings.Should().Be(1);
        pump.IsScheduled.Should().BeTrue();
    }

    [Test]
    public async Task A_mutation_that_changes_nothing_does_not_produce_a_new_snapshot()
    {
        var before = await _harness.Store.GetAsync(default);

        var after = await _harness.Store.MutateAsync(s => s, default);

        after.Should().BeSameAs(before);
    }

    [Test]
    public async Task Watchers_receive_the_current_state_then_every_change()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var received = new List<int>();

        var reader = Task.Run(
            async () =>
            {
                await foreach (var count in _harness.Queue.WatchPendingCount(cts.Token))
                {
                    received.Add(count);
                    if (received.Count == 2)
                    {
                        break;
                    }
                }
            },
            cts.Token);

        // Give the watcher a moment to attach and emit the current value.
        while (received.Count == 0 && !cts.IsCancellationRequested)
        {
            await Task.Delay(20, cts.Token);
        }

        var point = await _harness.PointAsync("P-204-motor");
        var draft = await _harness.Workflow.StartOrResumeDraftAsync(point, default);
        await _harness.Workflow.CompleteAsync(
            draft with { ReadingText = "70", Disposition = Disposition.Pass },
            default);

        await reader;

        received.Should().StartWith([0, 1]);
    }

    [Test]
    public async Task A_completion_cut_off_between_its_files_is_finished_on_the_next_launch()
    {
        await _harness.Store.GetAsync(default);

        // The process dies after events.json is written and before sync-queue.json is.
        var dying = _harness.StoreThatDiesWriting("sync-queue.json");
        var workflow = _harness.WorkflowOver(dying);
        var point = await _harness.PointAsync("P-204-seal");
        var draft = await workflow.StartOrResumeDraftAsync(point, default);

        var complete = async () => await workflow.CompleteAsync(
            draft with { ReadingText = "1.6", Disposition = Disposition.Pass }, default);
        await complete.Should().ThrowAsync<IOException>();

        var eventId = $"ev-{draft.ClientToken}";
        (await _harness.Files.ReadTextAsync("events.json", default)).Should().Contain(eventId);
        (await _harness.Files.ReadTextAsync("sync-queue.json", default) ?? string.Empty).Should().NotContain(eventId,
            "this is the half-written state: an inspection with no outbox entry");

        var relaunched = await _harness.Reopen().GetAsync(default);

        relaunched.Events.Should().ContainSingle(e => e.Id == eventId);
        relaunched.Queue.Should().ContainSingle(o => o.EntityId == eventId);
        relaunched.FindDraft("P-204-seal").Should().BeNull();
        relaunched.FindAsset("P-204")!.FindPoint("P-204-seal")!.LatestReading.Should().Be(1.6);
        (await _harness.Files.ReadTextAsync("commit.json", default)).Should().BeNull("the journal is retired once replayed");
    }

    [Test]
    public async Task A_commit_whose_journal_never_landed_leaves_the_previous_state()
    {
        var before = await _harness.Store.GetAsync(default);

        var dying = _harness.StoreThatDiesWriting("commit.json");
        var workflow = _harness.WorkflowOver(dying);
        var draft = await workflow.StartOrResumeDraftAsync(await _harness.PointAsync("P-204-seal"), default);

        var complete = async () => await workflow.CompleteAsync(
            draft with { ReadingText = "1.6", Disposition = Disposition.Pass }, default);
        await complete.Should().ThrowAsync<IOException>();

        var relaunched = await _harness.Reopen().GetAsync(default);
        relaunched.Events.Select(e => e.Id).Should().BeEquivalentTo(before.Events.Select(e => e.Id));
        relaunched.Queue.Should().BeEmpty();
        relaunched.FindDraft("P-204-seal").Should().NotBeNull("the reading is still a draft, ready to complete again");
    }

    [Test]
    public async Task A_damaged_outbox_is_set_aside_and_reported_rather_than_replaced()
    {
        var point = await _harness.PointAsync("P-204-seal");
        var draft = await _harness.Workflow.StartOrResumeDraftAsync(point, default);
        await _harness.Workflow.CompleteAsync(draft with { ReadingText = "1.6", Disposition = Disposition.Pass }, default);

        const string damaged = "[{\"id\":\"op-";
        await _harness.Files.WriteTextAsync("sync-queue.json", damaged, default);

        var reopened = _harness.Reopen();
        var snapshot = await reopened.GetAsync(default);

        snapshot.Queue.Should().BeEmpty();
        snapshot.Events.Should().Contain(e => e.ClientToken == draft.ClientToken, "the sections that could be read are kept");
        var aside = snapshot.SetAside.Should().ContainSingle().Subject;
        aside.Should().StartWith("sync-queue.json.unreadable-");
        (await _harness.Files.ReadTextAsync(aside, default)).Should().Be(damaged, "the damaged file is kept for recovery");

        // A later write starts a new outbox and leaves the set-aside copy alone.
        var workflow = _harness.WorkflowOver(reopened);
        var motor = await workflow.StartOrResumeDraftAsync(await _harness.PointAsync("P-204-motor"), default);
        await workflow.CompleteAsync(motor with { ReadingText = "70", Disposition = Disposition.Pass }, default);
        (await _harness.Files.ReadTextAsync(aside, default)).Should().Be(damaged);

        // And the Sync screen does not claim everything reached the server.
        var sync = new SyncModel(
            new SyncQueue(reopened, _harness.Network, NullLogger<SyncQueue>.Instance), _harness.Network, new FeedRecovery());
        (await sync.HasSetAside).Should().BeTrue();
        (await sync.IsSettled).Should().BeFalse();
        (await sync.SetAsideWarning).Should().Contain(aside);
    }
}
