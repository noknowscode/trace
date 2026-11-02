using SkiaSharp;
using Trace.Domain.Enums;

namespace Trace.Presentation.Scene3D;

/// <summary>
/// The machines, built as solids.
/// </summary>
/// <remarks>
/// Each asset is assembled from revolved and boxed primitives at plausible proportions, and
/// every inspection point is anchored to the solid it actually refers to: the seal anchor
/// sits on the seal collar between the bearing housing and the volute, not at a coordinate
/// someone picked off a drawing. Because the anchors live in the same space as the
/// geometry, they stay on their part as the machine is turned.
///
/// Dimensions are in metres.
/// </remarks>
public static class EquipmentModels
{
    // The equipment's own paint and metal, as a glTF file would carry them: data about the
    // machine, the same in both themes. The light the machine is seen under comes from the
    // theme (AssetSceneViewStyle).
    private static readonly Material CastIron = Material.Painted(new SKColor(0x5E, 0x6C, 0x68));
    private static readonly Material Steel = Material.Machined(new SKColor(0x8A, 0x98, 0x95));
    private static readonly Material DarkSteel = Material.Machined(new SKColor(0x47, 0x53, 0x50));
    private static readonly Material Skid = Material.Matte(new SKColor(0x3A, 0x45, 0x42));
    private static readonly Material Guard = Material.Painted(new SKColor(0xB7, 0x8A, 0x2E));
    private static readonly Material Panel = Material.Painted(new SKColor(0x6F, 0x7C, 0x79));
    private static readonly Material Copper = Material.Machined(new SKColor(0x9A, 0x6B, 0x45));

    private static readonly Dictionary<AssetKind, SceneModel> Cache = [];
    private static readonly Lock Gate = new();

    /// <summary>
    /// Models are built once and shared. Geometry generation is cheap but not free, and the
    /// same pump is drawn on every frame of an orbit.
    /// </summary>
    public static SceneModel For(AssetKind kind)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue(kind, out var cached))
            {
                return cached;
            }

            var model = kind switch
            {
                AssetKind.AirHandler => BuildAirHandler(),
                AssetKind.Compressor => BuildCompressor(),
                _ => BuildCentrifugalPump(),
            };

            Cache[kind] = model;
            return model;
        }
    }

    /// <summary>
    /// The profile of a totally-enclosed fan-cooled motor frame: a tapered nose, a run of
    /// cooling fins, and a tapered tail. Returned as one profile so the frame revolves into
    /// a single closed solid.
    /// </summary>
    private static (float Along, float Radius)[] FinnedFrame(
        float noseRadius,
        float bodyRadius,
        float finRadius,
        float length,
        int finCount)
    {
        var profile = new List<(float, float)>
        {
            (0f, noseRadius),
            (length * 0.05f, bodyRadius * 0.92f),
            (length * 0.09f, bodyRadius),
        };

        var first = length * 0.13f;
        var last = length * 0.87f;
        var pitch = (last - first) / finCount;
        var ridge = pitch * 0.42f;

        for (var i = 0; i < finCount; i++)
        {
            var at = first + (i * pitch);
            profile.Add((at, bodyRadius));
            profile.Add((at + (pitch * 0.12f), finRadius));
            profile.Add((at + (pitch * 0.12f) + ridge, finRadius));
            profile.Add((at + (pitch * 0.24f) + ridge, bodyRadius));
        }

        profile.Add((last, bodyRadius));
        profile.Add((length * 0.94f, bodyRadius * 0.9f));
        profile.Add((length, noseRadius * 1.25f));

        return profile.ToArray();
    }

    /// <summary>
    /// A back-pull-out centrifugal pump on a fabricated skid: motor, coupling guard,
    /// bearing frame, seal collar, volute with suction and discharge flanges.
    /// </summary>
    private static SceneModel BuildCentrifugalPump()
    {
        var parts = new List<MeshPart>();

        // Skid and feet
        parts.Add(MeshBuilder.Box(new Vec3(0f, -0.42f, 0f), new Vec3(2.5f, 0.09f, 0.78f), Skid));
        parts.Add(MeshBuilder.Box(new Vec3(-1.1f, -0.36f, 0f), new Vec3(0.1f, 0.05f, 0.8f), Skid));
        parts.Add(MeshBuilder.Box(new Vec3(1.1f, -0.36f, 0f), new Vec3(0.1f, 0.05f, 0.8f), Skid));
        parts.Add(MeshBuilder.Box(new Vec3(-0.72f, -0.22f, 0f), new Vec3(0.5f, 0.32f, 0.46f), Skid));
        parts.Add(MeshBuilder.Box(new Vec3(0.62f, -0.24f, 0f), new Vec3(0.46f, 0.28f, 0.5f), Skid));

        // Motor: a ribbed TEFC frame. The cooling fins are part of the frame's own profile
        // rather than rings sitting on it — separate solids that share a surface have no
        // depth order between them, and Skia has no depth buffer to break the tie.
        parts.Add(MeshBuilder.Revolve(
            new Vec3(-1.22f, 0.06f, 0f),
            new Vec3(1, 0, 0),
            FinnedFrame(0.16f, 0.3f, 0.33f, 0.96f, 9),
            CastIron,
            32));

        // Fan cowl behind the motor
        parts.Add(MeshBuilder.Revolve(
            new Vec3(-1.44f, 0.06f, 0f),
            new Vec3(1, 0, 0),
            [(0f, 0.1f), (0.05f, 0.21f), (0.2f, 0.23f), (0.22f, 0.16f)],
            DarkSteel));

        parts.Add(MeshBuilder.Box(new Vec3(-0.78f, 0.4f, 0f), new Vec3(0.34f, 0.2f, 0.3f), DarkSteel));
        parts.Add(MeshBuilder.Cylinder(
            new Vec3(-0.78f, 0.5f, 0f), new Vec3(-0.78f, 0.56f, 0f), 0.05f, Copper, 16));

        // Coupling guard
        parts.Add(MeshBuilder.Revolve(
            new Vec3(-0.24f, 0.06f, 0f),
            new Vec3(1, 0, 0),
            [(0f, 0.2f), (0.02f, 0.23f), (0.24f, 0.23f), (0.26f, 0.2f)],
            Guard));

        // Bearing frame
        parts.Add(MeshBuilder.Revolve(
            new Vec3(0.04f, 0.06f, 0f),
            new Vec3(1, 0, 0),
            [(0f, 0.18f), (0.04f, 0.24f), (0.34f, 0.24f), (0.38f, 0.2f), (0.46f, 0.19f)],
            CastIron));
        parts.Add(MeshBuilder.Ring(new Vec3(0.14f, 0.06f, 0f), new Vec3(1, 0, 0), 0.24f, 0.27f, 0.03f, Steel, 24));
        parts.Add(MeshBuilder.Ring(new Vec3(0.34f, 0.06f, 0f), new Vec3(1, 0, 0), 0.24f, 0.27f, 0.03f, Steel, 24));
        parts.Add(MeshBuilder.Box(new Vec3(0.24f, -0.16f, 0f), new Vec3(0.3f, 0.24f, 0.34f), CastIron));

        // Mechanical seal collar
        parts.Add(MeshBuilder.Revolve(
            new Vec3(0.5f, 0.06f, 0f),
            new Vec3(1, 0, 0),
            [(0f, 0.17f), (0.02f, 0.21f), (0.12f, 0.21f), (0.14f, 0.17f)],
            Steel));
        parts.Add(MeshBuilder.Ring(new Vec3(0.57f, 0.06f, 0f), new Vec3(1, 0, 0), 0.21f, 0.235f, 0.022f, DarkSteel, 24));

        // Volute casing
        parts.Add(MeshBuilder.Revolve(
            new Vec3(0.64f, 0.06f, 0f),
            new Vec3(1, 0, 0),
            [(0f, 0.22f), (0.03f, 0.44f), (0.1f, 0.47f), (0.24f, 0.47f), (0.3f, 0.4f), (0.33f, 0.24f)],
            CastIron));
        parts.Add(MeshBuilder.Ring(new Vec3(0.67f, 0.06f, 0f), new Vec3(1, 0, 0), 0.44f, 0.49f, 0.03f, DarkSteel, 32));

        // Suction flange on the casing nose, discharge standing up off the top
        parts.Add(MeshBuilder.Revolve(
            new Vec3(0.97f, 0.06f, 0f),
            new Vec3(1, 0, 0),
            [(0f, 0.18f), (0.14f, 0.18f), (0.16f, 0.24f), (0.2f, 0.24f)],
            CastIron));
        parts.Add(MeshBuilder.Revolve(
            new Vec3(0.76f, 0.5f, 0f),
            new Vec3(0, 1, 0),
            [(0f, 0.15f), (0.22f, 0.15f), (0.24f, 0.21f), (0.28f, 0.21f)],
            CastIron));

        var anchors = new Dictionary<string, Vec3>
        {
            ["P-204-motor"] = new(-0.64f, 0.32f, 0.12f),
            ["P-204-coupling"] = new(-0.11f, 0.28f, 0.1f),
            ["P-204-bearing"] = new(0.24f, 0.3f, 0.1f),
            ["P-204-seal"] = new(0.57f, 0.27f, 0.08f),
        };

        return new SceneModel(parts, anchors);
    }

    /// <summary>
    /// A packaged air handler: filter section, cooling coil, fan section with a belt drive,
    /// and a discharge duct, on a roof curb.
    /// </summary>
    private static SceneModel BuildAirHandler()
    {
        var parts = new List<MeshPart>();

        parts.Add(MeshBuilder.Box(new Vec3(0f, -0.62f, 0f), new Vec3(2.7f, 0.12f, 1.3f), Skid));

        // Cabinet, built as three sections so the seams read as access panels
        parts.Add(MeshBuilder.Box(new Vec3(-0.86f, 0f, 0f), new Vec3(0.88f, 1.1f, 1.2f), Panel));
        parts.Add(MeshBuilder.Box(new Vec3(-0.02f, 0f, 0f), new Vec3(0.76f, 1.1f, 1.2f), Panel));
        parts.Add(MeshBuilder.Box(new Vec3(0.82f, 0f, 0f), new Vec3(0.88f, 1.1f, 1.2f), Panel));

        // Panel seams and frame rails
        parts.Add(MeshBuilder.Box(new Vec3(-0.42f, 0f, 0.001f), new Vec3(0.03f, 1.12f, 1.22f), DarkSteel));
        parts.Add(MeshBuilder.Box(new Vec3(0.38f, 0f, 0.001f), new Vec3(0.03f, 1.12f, 1.22f), DarkSteel));
        parts.Add(MeshBuilder.Box(new Vec3(0f, 0.56f, 0f), new Vec3(2.64f, 0.05f, 1.24f), DarkSteel));

        // Filter bank, recessed behind a framed opening
        parts.Add(MeshBuilder.Box(new Vec3(-0.86f, 0f, 0.6f), new Vec3(0.66f, 0.84f, 0.06f), DarkSteel));
        for (var i = 0; i < 3; i++)
        {
            parts.Add(MeshBuilder.Box(
                new Vec3(-0.86f, 0.26f - (i * 0.26f), 0.63f),
                new Vec3(0.6f, 0.2f, 0.03f),
                Steel));
        }

        // Cooling coil: a finned block with header pipes
        parts.Add(MeshBuilder.Box(new Vec3(-0.02f, 0f, 0.58f), new Vec3(0.6f, 0.86f, 0.08f), DarkSteel));
        for (var i = 0; i < 11; i++)
        {
            parts.Add(MeshBuilder.Box(
                new Vec3(-0.28f + (i * 0.052f), 0f, 0.62f),
                new Vec3(0.016f, 0.8f, 0.03f),
                Copper));
        }

        parts.Add(MeshBuilder.Cylinder(
            new Vec3(-0.02f, 0.46f, 0.66f), new Vec3(-0.02f, 0.46f, 0.92f), 0.05f, Copper, 18));
        parts.Add(MeshBuilder.Cylinder(
            new Vec3(-0.02f, -0.46f, 0.66f), new Vec3(-0.02f, -0.46f, 0.92f), 0.05f, Copper, 18));

        // Supply fan: a scroll housing with a shaft and a belt drive below it
        parts.Add(MeshBuilder.Revolve(
            new Vec3(0.82f, 0.12f, -0.2f),
            new Vec3(0, 0, 1),
            [(0f, 0.12f), (0.04f, 0.36f), (0.5f, 0.36f), (0.54f, 0.12f)],
            DarkSteel));
        parts.Add(MeshBuilder.Ring(
            new Vec3(0.82f, 0.12f, 0.32f), new Vec3(0, 0, 1), 0.12f, 0.36f, 0.03f, Steel, 30));
        parts.Add(MeshBuilder.Cylinder(
            new Vec3(0.82f, 0.12f, 0.3f), new Vec3(0.82f, 0.12f, 0.52f), 0.045f, Steel, 16));

        parts.Add(MeshBuilder.Revolve(
            new Vec3(0.82f, -0.36f, 0.38f),
            new Vec3(0, 0, 1),
            [(0f, 0.14f), (0.03f, 0.17f), (0.09f, 0.17f), (0.12f, 0.14f)],
            DarkSteel));
        parts.Add(MeshBuilder.Box(new Vec3(0.82f, -0.12f, 0.44f), new Vec3(0.04f, 0.5f, 0.03f), Material.Matte(new SKColor(0x2B, 0x2F, 0x2E))));

        // Discharge duct
        parts.Add(MeshBuilder.Box(new Vec3(1.44f, 0.12f, 0f), new Vec3(0.36f, 0.66f, 0.7f), Panel));

        var anchors = new Dictionary<string, Vec3>
        {
            ["AHU-12-filter"] = new(-0.86f, 0.12f, 0.68f),
            ["AHU-12-coil"] = new(-0.02f, 0.1f, 0.68f),
            ["AHU-12-fan"] = new(0.82f, 0.12f, 0.56f),
            ["AHU-12-belt"] = new(0.82f, -0.36f, 0.5f),
        };

        return new SceneModel(parts, anchors);
    }

    /// <summary>
    /// A packaged rotary screw compressor: drive motor, airend, oil separator vessel with a
    /// sight glass, and a horizontal air receiver with an auto-drain.
    /// </summary>
    private static SceneModel BuildCompressor()
    {
        var parts = new List<MeshPart>();

        parts.Add(MeshBuilder.Box(new Vec3(0f, -0.6f, 0f), new Vec3(2.6f, 0.11f, 1.05f), Skid));

        // Drive motor
        parts.Add(MeshBuilder.Revolve(
            new Vec3(-1.16f, -0.06f, 0f),
            new Vec3(1, 0, 0),
            FinnedFrame(0.15f, 0.28f, 0.305f, 0.82f, 7),
            CastIron,
            30));

        parts.Add(MeshBuilder.Box(new Vec3(-1.02f, 0.34f, 0f), new Vec3(0.44f, 0.26f, 0.34f), DarkSteel));

        // Airend: two intersecting rotor housings
        parts.Add(MeshBuilder.Revolve(
            new Vec3(-0.36f, 0.02f, 0f),
            new Vec3(1, 0, 0),
            [(0f, 0.2f), (0.04f, 0.26f), (0.5f, 0.26f), (0.54f, 0.2f)],
            Steel));
        parts.Add(MeshBuilder.Revolve(
            new Vec3(-0.36f, -0.24f, 0f),
            new Vec3(1, 0, 0),
            [(0f, 0.17f), (0.04f, 0.22f), (0.5f, 0.22f), (0.54f, 0.17f)],
            Steel));
        parts.Add(MeshBuilder.Ring(new Vec3(-0.3f, 0.02f, 0f), new Vec3(1, 0, 0), 0.26f, 0.29f, 0.03f, DarkSteel, 24));

        // Discharge pipe into the separator
        parts.Add(MeshBuilder.Cylinder(
            new Vec3(0.18f, 0.14f, 0f), new Vec3(0.46f, 0.14f, 0f), 0.06f, Copper, 16));

        // Oil separator vessel with a sight glass
        parts.Add(MeshBuilder.Revolve(
            new Vec3(0.5f, -0.45f, 0f),
            new Vec3(0, 1, 0),
            [(0f, 0.1f), (0.06f, 0.22f), (0.14f, 0.25f), (0.86f, 0.25f), (0.94f, 0.21f), (1f, 0.1f)],
            CastIron));
        parts.Add(MeshBuilder.Ring(new Vec3(0.5f, -0.18f, 0f), new Vec3(0, 1, 0), 0.25f, 0.28f, 0.03f, DarkSteel, 26));
        parts.Add(MeshBuilder.Box(new Vec3(0.5f, -0.14f, 0.26f), new Vec3(0.07f, 0.3f, 0.05f), Material.Machined(new SKColor(0xC8, 0xB8, 0x72))));

        // Air receiver, laid on saddles
        parts.Add(MeshBuilder.Revolve(
            new Vec3(0.92f, -0.1f, 0f),
            new Vec3(1, 0, 0),
            [(0f, 0.12f), (0.07f, 0.3f), (0.14f, 0.34f), (0.74f, 0.34f), (0.81f, 0.3f), (0.88f, 0.12f)],
            CastIron));
        parts.Add(MeshBuilder.Box(new Vec3(1.06f, -0.44f, 0f), new Vec3(0.14f, 0.2f, 0.56f), Skid));
        parts.Add(MeshBuilder.Box(new Vec3(1.58f, -0.44f, 0f), new Vec3(0.14f, 0.2f, 0.56f), Skid));

        // Auto-drain under the receiver
        parts.Add(MeshBuilder.Cylinder(
            new Vec3(1.34f, -0.42f, 0f), new Vec3(1.34f, -0.56f, 0f), 0.05f, Copper, 14));
        parts.Add(MeshBuilder.Box(new Vec3(1.34f, -0.6f, 0f), new Vec3(0.14f, 0.1f, 0.12f), DarkSteel));

        var anchors = new Dictionary<string, Vec3>
        {
            ["C-07-motor"] = new(-0.78f, 0.18f, 0.16f),
            ["C-07-discharge"] = new(-0.1f, 0.26f, 0.12f),
            ["C-07-oil"] = new(0.5f, -0.14f, 0.3f),
            ["C-07-drain"] = new(1.34f, -0.58f, 0.1f),
        };

        return new SceneModel(parts, anchors);
    }
}
