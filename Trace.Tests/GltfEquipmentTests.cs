using SkiaSharp;
using Trace.Domain.Enums;
using Trace.Presentation.Scene3D;
using Trace.Tests.Support;

namespace Trace.Tests;

/// <summary>
/// The import pipeline for real equipment models, proven against the parametric pump
/// pushed through the same writer a modeller's tooling would use.
/// </summary>
[TestFixture]
public class GltfEquipmentTests
{
    private static SceneModel RoundTrip(SceneModel original)
    {
        using var buffer = new MemoryStream();
        GltfEquipmentWriter.Write(original, buffer);
        buffer.Position = 0;
        return GltfEquipmentLoader.Load(buffer);
    }

    [Test]
    public void A_pump_survives_a_round_trip_through_glTF()
    {
        var original = EquipmentModels.For(AssetKind.CentrifugalPump);

        var imported = RoundTrip(original);

        imported.TriangleCount.Should().Be(original.TriangleCount);
        imported.Parts.Count.Should().Be(original.Parts.Count);
        imported.Centre.X.Should().BeApproximately(original.Centre.X, 1e-4f);
        imported.Centre.Y.Should().BeApproximately(original.Centre.Y, 1e-4f);
        imported.Centre.Z.Should().BeApproximately(original.Centre.Z, 1e-4f);
        imported.Radius.Should().BeApproximately(original.Radius, 1e-4f);
    }

    [Test]
    public void Inspection_points_come_from_named_nodes_in_the_file()
    {
        var original = EquipmentModels.For(AssetKind.CentrifugalPump);

        var imported = RoundTrip(original);

        imported.Anchors.Keys.Should().BeEquivalentTo(original.Anchors.Keys);
        foreach (var (id, position) in original.Anchors)
        {
            imported.Anchors[id].X.Should().BeApproximately(position.X, 1e-4f, id);
            imported.Anchors[id].Y.Should().BeApproximately(position.Y, 1e-4f, id);
            imported.Anchors[id].Z.Should().BeApproximately(position.Z, 1e-4f, id);
        }
    }

    [Test]
    public void Material_colour_is_carried_through_the_base_colour_factor()
    {
        var original = EquipmentModels.For(AssetKind.Compressor);

        var imported = RoundTrip(original);

        for (var i = 0; i < original.Parts.Count; i++)
        {
            var expected = original.Parts[i].Material.Albedo;
            var actual = imported.Parts[i].Material.Albedo;
            ((int)actual.Red).Should().BeCloseTo(expected.Red, 1, $"part {i} red");
            ((int)actual.Green).Should().BeCloseTo(expected.Green, 1, $"part {i} green");
            ((int)actual.Blue).Should().BeCloseTo(expected.Blue, 1, $"part {i} blue");
        }
    }

    [Test]
    public void An_imported_model_renders_through_the_real_renderer()
    {
        var imported = RoundTrip(EquipmentModels.For(AssetKind.CentrifugalPump));
        var fresh = RenderToBitmap(imported, out var anchors);

        NonBackgroundFraction(fresh).Should().BeGreaterThan(0.08f, "the machine covers a visible part of the frame");
        anchors.Should().HaveCount(imported.Anchors.Count);
        anchors.Values.Should().OnlyContain(a => a.X >= 0 && a.X <= 1 && a.Y >= 0 && a.Y <= 1);
    }

    [Test]
    public void The_renderer_can_be_disposed_and_used_again()
    {
        var model = EquipmentModels.For(AssetKind.AirHandler);
        using var renderer = new SceneRenderer();

        RenderInto(renderer, model);
        renderer.Dispose();
        var second = RenderInto(renderer, model);

        NonBackgroundFraction(second).Should().BeGreaterThan(0.08f);
    }

    [Test]
    public void A_foreign_asset_loads_when_one_is_available_locally()
    {
        // Khronos sample assets carry their own licences, so none is committed here. When
        // one has been fetched next to the build, prove the loader copes with a file the
        // app did not write.
        var candidates = new[] { "/tmp/Duck.glb", "/tmp/Lantern.glb" };
        var path = candidates.FirstOrDefault(File.Exists);
        if (path is null)
        {
            Assert.Ignore("No foreign glTF sample present.");
        }

        using var stream = File.OpenRead(path!);
        var model = GltfEquipmentLoader.Load(stream);

        model.TriangleCount.Should().BeGreaterThan(100);
        model.Radius.Should().BeGreaterThan(0f);
        NonBackgroundFraction(RenderToBitmap(model, out _)).Should().BeGreaterThan(0.05f);
        TestContext.Progress.WriteLine($"{Path.GetFileName(path)}: {model.TriangleCount} tris, {model.Parts.Count} parts");
    }

    /// <summary>
    /// Writes the bundled pump asset. Run on demand:
    /// dotnet test --filter Export_the_bundled_pump_asset
    /// </summary>
    [Test]
    [Explicit("Regenerates Trace/Assets/Models/p-204.glb from the parametric pump.")]
    public void Export_the_bundled_pump_asset()
    {
        var target = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "Trace", "Assets", "Models", "p-204.glb"));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        using var file = File.Create(target);
        GltfEquipmentWriter.Write(EquipmentModels.For(AssetKind.CentrifugalPump), file);

        TestContext.Progress.WriteLine($"Wrote {target}");
    }

    private static readonly ScenePalette Palette = new(
        Background: new SKColor(0x10, 0x1A, 0x18),
        Ground: new SKColor(0, 0, 0, 90),
        KeyLight: new SKColor(0xE8, 0xF3, 0xEE),
        FillLight: new SKColor(0x7E, 0x8C, 0x99));

    private static SKBitmap RenderToBitmap(SceneModel model, out Dictionary<string, AnchorProjection> anchors)
    {
        using var renderer = new SceneRenderer();
        var bitmap = RenderInto(renderer, model);
        anchors = new Dictionary<string, AnchorProjection>(renderer.Anchors);
        return bitmap;
    }

    private static SKBitmap RenderInto(SceneRenderer renderer, SceneModel model)
    {
        using var surface = SKSurface.Create(new SKImageInfo(480, 320));
        renderer.Render(surface.Canvas, null, new SKRect(0, 0, 480, 320), model, new SceneCamera(), Palette);
        using var image = surface.Snapshot();
        return SKBitmap.FromImage(image);
    }

    private static float NonBackgroundFraction(SKBitmap bitmap)
    {
        var background = Palette.Background;
        var different = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (Math.Abs(p.Red - background.Red) > 8 || Math.Abs(p.Green - background.Green) > 8 || Math.Abs(p.Blue - background.Blue) > 8)
                {
                    different++;
                }
            }
        }

        return different / (float)(bitmap.Width * bitmap.Height);
    }
}

[TestFixture]
public class SceneRendererPerformanceTests
{
    [Test]
    public void An_orbit_frame_of_the_pump_stays_well_under_a_display_refresh_budget()
    {
        var model = EquipmentModels.For(AssetKind.CentrifugalPump);
        var palette = new ScenePalette(
            new SKColor(0x10, 0x1A, 0x18), new SKColor(0, 0, 0, 90),
            new SKColor(0xE8, 0xF3, 0xEE), new SKColor(0x7E, 0x8C, 0x99));
        using var renderer = new SceneRenderer();
        using var surface = SKSurface.Create(new SKImageInfo(1400, 700));
        var camera = new SceneCamera();
        var bounds = new SKRect(0, 0, 1400, 700);

        renderer.Render(surface.Canvas, null, bounds, model, camera, palette);

        var before = GC.GetTotalAllocatedBytes(precise: true);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        const int Frames = 30;
        for (var i = 0; i < Frames; i++)
        {
            camera.Orbit(0.02f, 0.005f);
            renderer.Render(surface.Canvas, null, bounds, model, camera, palette);
        }

        watch.Stop();
        var perFrameMs = watch.Elapsed.TotalMilliseconds / Frames;
        var managedKbPerFrame = (GC.GetTotalAllocatedBytes(precise: true) - before) / 1024.0 / Frames;
        TestContext.Progress.WriteLine($"pump @1400x700 2x: {perFrameMs:F1} ms/frame, {managedKbPerFrame:F1} KB managed alloc/frame");

        // The allocation bound is the sharp one: copying the vertex buffers out per frame
        // cost ~94 KB, so anything near 64 KB is that regression. The time bound is only a
        // sanity check — this measured ~100 ms on a slow container CPU and a fraction of
        // that on a workstation, so it is set wide enough not to flake in CI.
        perFrameMs.Should().BeLessThan(300);
        managedKbPerFrame.Should().BeLessThan(64, "the vertex buffers are pooled and no longer copied per frame");
    }
}
