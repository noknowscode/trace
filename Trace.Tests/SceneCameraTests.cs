using Trace.Presentation.Scene3D;

namespace Trace.Tests;

/// <summary>
/// The camera frames every asset without per-asset tuning, so the framing maths is the one
/// part of the 3D pipeline worth pinning down in tests.
/// </summary>
[TestFixture]
public class SceneCameraTests
{
    private const float Width = 1000f;
    private const float Height = 700f;

    private static float ProjectedWidth(SceneCamera camera, SceneModel model)
    {
        var view = camera.BuildTransform(model, Width, Height);
        float min = float.MaxValue, max = float.MinValue;

        foreach (var point in model.FramingPoints)
        {
            var (x, _, _) = view.ProjectWorld(point);
            min = MathF.Min(min, x);
            max = MathF.Max(max, x);
        }

        return max - min;
    }

    [Test]
    public void Zooming_in_makes_the_machine_bigger()
    {
        var model = EquipmentModels.For(AssetKind.CentrifugalPump);
        var camera = new SceneCamera();

        var atRest = ProjectedWidth(camera, model);

        camera.Zoom = 0.5f;
        var zoomedIn = ProjectedWidth(camera, model);

        camera.Zoom = 2f;
        var zoomedOut = ProjectedWidth(camera, model);

        zoomedIn.Should().BeGreaterThan(atRest);
        zoomedOut.Should().BeLessThan(atRest);
    }

    [TestCase(AssetKind.CentrifugalPump)]
    [TestCase(AssetKind.AirHandler)]
    [TestCase(AssetKind.Compressor)]
    public void Every_asset_arrives_framed_inside_the_viewport(AssetKind kind)
    {
        var model = EquipmentModels.For(kind);
        var view = new SceneCamera().BuildTransform(model, Width, Height);

        foreach (var point in model.FramingPoints)
        {
            var (x, y, _) = view.ProjectWorld(point);
            x.Should().BeInRange(0f, Width);
            y.Should().BeInRange(0f, Height);
        }
    }

    [Test]
    public void Framing_fills_the_viewport_rather_than_leaving_it_mostly_empty()
    {
        var model = EquipmentModels.For(AssetKind.CentrifugalPump);

        // The fit targets a 0.9 margin; anything below half the viewport means the camera
        // has gone back to framing a bounding sphere and wasting the panel.
        ProjectedWidth(new SceneCamera(), model).Should().BeGreaterThan(Width * 0.5f);
    }
}
