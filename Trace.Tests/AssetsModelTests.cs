using Trace.Infrastructure.Repositories;
using Trace.Presentation;

namespace Trace.Tests;

/// <summary>Every asset on the device, and the search that narrows it.</summary>
public class AssetsModelTests
{
    private TestHarness _harness = null!;

    [SetUp]
    public void SetUp() => _harness = TestHarness.Create();

    [TearDown]
    public void TearDown() => _harness.Dispose();

    private AssetsModel Assets() => new(new AssetRepository(_harness.Store), new FeedRecovery());

    private static async Task<IEnumerable<string>> ShownAsync(AssetsModel model) =>
        (await model.Assets ?? ImmutableList<AssetSummary>.Empty).Select(a => a.AssetId);

    [Test]
    public async Task With_no_search_every_asset_is_listed()
    {
        var model = Assets();

        (await ShownAsync(model)).Should().BeEquivalentTo("P-204", "AHU-12", "C-07");
    }

    [TestCase("c-07", "C-07", TestName = "By code, in any case")]
    [TestCase("air handler", "AHU-12", TestName = "By name")]
    [TestCase("compressor", "C-07", TestName = "By type")]
    [TestCase("line 3", "C-07", TestName = "By area")]
    [TestCase("water treatment", "P-204", TestName = "By site")]
    [TestCase("  roof  ", "AHU-12", TestName = "With the spaces around the term ignored")]
    public async Task The_search_matches_code_name_type_area_and_site(string query, string expected)
    {
        var model = Assets();

        await model.Query.UpdateAsync(_ => query, default);

        (await ShownAsync(model)).Should().Equal(expected);
    }

    [Test]
    public async Task A_search_nothing_matches_lists_nothing_and_clearing_it_lists_everything_again()
    {
        var model = Assets();

        await model.Query.UpdateAsync(_ => "zzq", default);
        (await ShownAsync(model)).Should().BeEmpty();

        await model.Query.UpdateAsync(_ => "   ", default);
        (await ShownAsync(model)).Should().HaveCount(3, "a blank search is no search");
    }
}
