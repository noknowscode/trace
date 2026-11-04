using Trace.Infrastructure.Platform;
using Trace.Infrastructure.Repositories;
using Trace.Presentation;

namespace Trace.Tests;

/// <summary>Scan's two ways in, a tag and a typed code, over a real store.</summary>
public class ScanModelTests
{
    private TestHarness _harness = null!;

    [SetUp]
    public void SetUp() => _harness = TestHarness.Create();

    [TearDown]
    public void TearDown() => _harness.Dispose();

    private static ScanModel ScanOver(ITraceStore store) =>
        new(new SimulatedAssetScanService(store), new AssetRepository(store), new NullNavigator());

    [Test]
    public async Task A_code_no_asset_matches_says_so()
    {
        var model = ScanOver(_harness.Store);
        await model.Code.UpdateAsync(_ => "X-1", default);

        await model.Lookup(default);

        (await model.NotResolved).Should().BeTrue();
        (await model.ResultMessage).Should().Contain("No asset on this device matches X-1");
    }

    [Test]
    public async Task An_empty_code_asks_for_one()
    {
        var model = ScanOver(_harness.Store);

        await model.Lookup(default);

        (await model.NotResolved).Should().BeTrue();
        (await model.ResultMessage).Should().Be("Enter an asset code or scan a tag.");
    }

    [Test]
    public async Task Records_that_cannot_be_read_are_not_reported_as_an_unknown_asset()
    {
        var files = _harness.BreakableFiles();
        files.ReadsBroken = true;
        var model = ScanOver(_harness.StoreOver(files));
        await model.Code.UpdateAsync(_ => "P-204", default);

        // The store's IOException used to escape both commands, and Scan said nothing.
        await model.Lookup(default);

        (await model.ReadError).Should().Contain("could not be read");
        (await model.NotResolved).Should().BeFalse("the code may well be right");

        await model.ScanTag(default);
        (await model.ReadError).Should().Contain("could not be read");

        // Readable again: the next lookup answers, and the message goes. (A code that resolves
        // navigates, which the Switch hook proves at runtime; this one does not.)
        files.ReadsBroken = false;
        await model.Code.UpdateAsync(_ => "X-1", default);
        await model.Lookup(default);

        (await model.ReadError).Should().BeNullOrEmpty();
        (await model.NotResolved).Should().BeTrue();
    }
}
