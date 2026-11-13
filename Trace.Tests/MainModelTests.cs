using Trace.Presentation;

namespace Trace.Tests;

/// <summary>The shell: how much work has not left the device, and whether it can be sent.</summary>
public class MainModelTests
{
    private TestHarness _harness = null!;

    [SetUp]
    public void SetUp() => _harness = TestHarness.Create();

    [TearDown]
    public void TearDown() => _harness.Dispose();

    private MainModel Shell() => new(_harness.Queue, _harness.Network, new FeedRecovery());

    [Test]
    public async Task The_sync_badge_counts_what_the_device_still_holds()
    {
        var model = Shell();
        (await model.PendingCount).Should().Be(0);
        (await model.HasPending).Should().BeFalse();

        var point = await _harness.PointAsync("P-204-seal");
        var draft = await _harness.Workflow.StartOrResumeDraftAsync(point, default);
        await _harness.Workflow.CompleteAsync(draft with { ReadingText = "1.6", Disposition = Disposition.Pass }, default);

        (await model.PendingCount).Should().Be(1);
        (await model.HasPending).Should().BeTrue();

        await _harness.Queue.SyncAsync(default);
        (await model.PendingCount).Should().Be(0, "a synced change has left the device");
    }

    [Test]
    public async Task The_shell_follows_the_connection()
    {
        var model = Shell();
        (await model.IsOnline).Should().BeTrue();

        _harness.Network.SetOffline(true);

        (await model.IsOnline).Should().BeFalse();
    }
}
