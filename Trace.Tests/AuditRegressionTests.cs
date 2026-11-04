namespace Trace.Tests;

/// <summary>
/// Regressions found by the codebase audit. Each one shipped, so each one gets a test.
/// </summary>
public class AuditRegressionTests
{
    private TestHarness _harness = null!;

    [SetUp]
    public void SetUp() => _harness = TestHarness.Create();

    [TearDown]
    public void TearDown() => _harness.Dispose();

    [TestCase("NaN")]
    [TestCase("Infinity")]
    [TestCase("-Infinity")]
    [TestCase("∞")]
    public void A_named_float_literal_is_not_a_reading(string text)
    {
        // These parse under NumberStyles.Float, compare false against every range so they
        // report as in-range, and then cannot be serialized — which failed the completion
        // and left the poisoned value in the snapshot for the rest of the session.
        InspectionRules.TryParseReading(text, out _).Should().BeFalse();
    }

    [Test]
    public async Task A_reading_that_cannot_be_stored_is_rejected_before_it_reaches_the_store()
    {
        var point = await _harness.PointAsync("P-204-seal");
        var draft = await _harness.Workflow.StartOrResumeDraftAsync(point, default);

        var outcome = await _harness.Workflow.CompleteAsync(
            draft with { ReadingText = "NaN", Disposition = Disposition.Pass }, default);

        outcome.Succeeded.Should().BeFalse();
        outcome.Validation.Blocker.Should().Be(InspectionBlocker.ReadingNotNumeric);

        // The store must still accept ordinary work afterwards.
        var next = await _harness.Workflow.CompleteAsync(
            draft with { ReadingText = "1.6", Disposition = Disposition.Pass }, default);

        next.Succeeded.Should().BeTrue();
    }

    [Test]
    public async Task An_unreadable_asset_catalogue_does_not_discard_the_outbox()
    {
        var point = await _harness.PointAsync("P-204-seal");
        var draft = await _harness.Workflow.StartOrResumeDraftAsync(point, default);
        await _harness.Workflow.CompleteAsync(
            draft with { ReadingText = "1.6", Disposition = Disposition.Pass }, default);

        var before = await _harness.Store.GetAsync(default);
        before.Queue.Should().NotBeEmpty();
        before.Events.Should().NotBeEmpty();

        // A truncated catalogue used to look exactly like a first run, so the store reseeded
        // and every queued operation the technician had not yet synced went with it.
        await _harness.Files.WriteTextAsync("assets.json", "[{\"id\":\"P-2", default);

        var reopened = _harness.Reopen();
        var after = await reopened.GetAsync(default);

        after.Assets.Should().NotBeEmpty("the catalogue is reseeded so the app stays usable");
        after.Queue.Select(o => o.Id).Should().BeEquivalentTo(before.Queue.Select(o => o.Id));
        after.Events.Select(e => e.Id).Should().BeEquivalentTo(before.Events.Select(e => e.Id));
    }

    [Test]
    public async Task Opening_one_point_many_times_at_once_yields_a_single_draft()
    {
        var point = await _harness.PointAsync("P-204-motor");

        var drafts = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => _harness.Workflow.StartOrResumeDraftAsync(point, default).AsTask()));

        // Several drafts for one point means several client tokens, and the idempotency
        // guard on completion then matches none of them.
        var snapshot = await _harness.Store.GetAsync(default);
        snapshot.Drafts.Count(d => d.PointId == point.Id).Should().Be(1);
        drafts.Select(d => d.ClientToken).Distinct().Should().HaveCount(1);
    }
}
