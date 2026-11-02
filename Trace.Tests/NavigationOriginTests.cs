using Trace.Presentation;

namespace Trace.Tests;

/// <summary>
/// Back from an asset returns to the destination that opened it (UNO-BUILD-SPEC: back
/// restores the exact context), not always to Today.
/// </summary>
public class NavigationOriginTests
{
    private TestHarness _harness = null!;

    [SetUp]
    public void SetUp() => _harness = TestHarness.Create();

    [TearDown]
    public void TearDown() => _harness.Dispose();

    [TestCase("Today")]
    [TestCase("Assets")]
    [TestCase("Scan")]
    [TestCase("Findings")]
    public async Task Back_returns_to_the_destination_the_asset_was_opened_from(string destination)
    {
        var origin = new NavigationOrigin();
        origin.Shown(destination);
        origin.Shown("Asset");

        var model = await _harness.OpenAssetAsync(origin: origin);

        // Back used to name Today whatever had opened the asset. (Navigating needs the app's
        // route resolver, so the test reads the route; the desktop run checks the navigation.)
        model.BackRoute.Should().Be($"../{destination}");
    }

    [Test]
    public void Only_a_destination_counts_as_an_origin()
    {
        var origin = new NavigationOrigin();
        origin.Route.Should().Be("Today", "the round is where the app starts");

        origin.Shown("Findings");
        origin.Shown("Asset");
        origin.Shown("Main");
        origin.Shown(null);

        origin.Route.Should().Be("Findings");
    }
}
