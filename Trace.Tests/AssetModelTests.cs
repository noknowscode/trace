using Trace.Presentation;

namespace Trace.Tests;

/// <summary>
/// The asset workspace itself: its record of the machine, its tabs, the point it hands the
/// inspector, and where Back goes.
/// </summary>
public class AssetModelTests
{
    private TestHarness _harness = null!;

    [SetUp]
    public void SetUp() => _harness = TestHarness.Create();

    [TearDown]
    public void TearDown() => _harness.Dispose();

    [Test]
    public async Task The_workspace_opens_on_the_machine_with_its_points_in_order()
    {
        var model = await _harness.OpenAssetAsync();

        (await model.TabIndex).Should().Be((int)AssetTab.Inspect, "the machine is the task, not a record about it");
        (await model.ShowsMachine).Should().BeTrue();
        (await model.SchematicKind).Should().Be(AssetKind.CentrifugalPump);

        var markers = await model.Markers;
        markers.Select(m => m.Point.Sequence).Should().BeInAscendingOrder();
        markers.Should().HaveCount((await _harness.PumpAsync()).Points.Count);
        markers.Should().NotContain(m => m.IsSelected, "no point is open yet");
    }

    [Test]
    public async Task Choosing_a_point_opens_it_on_the_inspect_tab_and_marks_it()
    {
        var model = await _harness.OpenAssetAsync();
        await model.TabIndex.UpdateAsync(_ => (int)AssetTab.History, default);

        await model.SelectPoint(await _harness.PointAsync("P-204-seal"), default);

        (await model.TabIndex).Should().Be((int)AssetTab.Inspect);
        (await model.IsInspect).Should().BeTrue();
        (await model.InspectorModel.IsOpen).Should().BeTrue();
        await TestHarness.UntilAsync(async () => (await model.Markers).Any(m => m.IsSelected));
        (await model.Markers).Should().ContainSingle(m => m.IsSelected).Which.Id.Should().Be("P-204-seal");
    }

    [Test]
    public async Task A_marker_opens_the_point_it_carries()
    {
        var model = await _harness.OpenAssetAsync();
        var coupling = (await model.Markers).Single(m => m.Id == "P-204-coupling");

        await model.SelectMarker(coupling, default);

        (await model.InspectorModel.SelectedPointId).Should().Be("P-204-coupling");
    }

    [Test]
    public async Task Each_tab_shows_one_face_of_the_asset()
    {
        var model = await _harness.OpenAssetAsync();

        foreach (var tab in Enum.GetValues<AssetTab>())
        {
            await model.TabIndex.UpdateAsync(_ => (int)tab, default);

            (await model.IsOverview).Should().Be(tab == AssetTab.Overview);
            (await model.IsInspect).Should().Be(tab == AssetTab.Inspect);
            (await model.IsHistory).Should().Be(tab == AssetTab.History);
            (await model.IsDocuments).Should().Be(tab == AssetTab.Documents);
            (await model.ShowsMachine).Should().Be(tab == AssetTab.Inspect, "the machine is drawn on Inspect only");
        }
    }

    [Test]
    public async Task History_lists_the_asset_s_inspections_newest_first()
    {
        var model = await _harness.OpenAssetAsync();

        var history = await model.History;

        history.Should().NotBeEmpty();
        history.Should().OnlyContain(e => e.AssetId == "P-204");
        history.Select(e => e.Timestamp).Should().BeInDescendingOrder();
    }

    [Test]
    public async Task Back_returns_to_the_destination_the_asset_was_opened_from()
    {
        var origin = new NavigationOrigin();
        origin.Shown("Findings");

        var model = await _harness.OpenAssetAsync(origin: origin);

        model.BackRoute.Should().Be("../Findings");
    }

    [Test]
    public async Task The_identity_comes_from_the_row_that_was_tapped()
    {
        var model = await _harness.OpenAssetAsync("C-07");

        model.AssetId.Should().Be("C-07");
        model.Designation.Should().StartWith("C-07");
        (await model.SchematicKind).Should().Be(AssetKind.Compressor);
    }
}
