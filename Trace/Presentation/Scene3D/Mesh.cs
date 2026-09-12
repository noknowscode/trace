using SkiaSharp;

namespace Trace.Presentation.Scene3D;

/// <summary>How a surface takes light. Gloss drives the size and strength of the highlight.</summary>
public readonly record struct Material(SKColor Albedo, float Gloss)
{
    public static Material Painted(SKColor albedo) => new(albedo, 0.18f);

    public static Material Machined(SKColor albedo) => new(albedo, 0.55f);

    public static Material Matte(SKColor albedo) => new(albedo, 0.05f);
}

/// <summary>
/// One rigid piece of equipment geometry: a triangle soup with a material. Parts are kept
/// separate so a pump reads as a motor bolted to a casing rather than as one blob.
/// </summary>
public sealed class MeshPart
{
    public MeshPart(Vec3[] vertices, int[] indices, Material material)
    {
        Vertices = vertices;
        Indices = indices;
        Material = material;

        // Geometry never moves, so a face normal is a property of the part, not of the
        // frame. Computing it here takes one cross product and one square root per face
        // out of every frame of an orbit.
        Normals = new Vec3[indices.Length / 3];
        for (var i = 0; i < Normals.Length; i++)
        {
            var a = vertices[indices[i * 3]];
            var b = vertices[indices[(i * 3) + 1]];
            var c = vertices[indices[(i * 3) + 2]];
            Normals[i] = Vec3.Cross(b - a, c - a).Normalized;
        }
    }

    public Vec3[] Vertices { get; }

    /// <summary>Triangle list, three indices per face, wound counter-clockwise when seen from outside.</summary>
    public int[] Indices { get; }

    /// <summary>Outward unit normal per face, in model space.</summary>
    public Vec3[] Normals { get; }

    public Material Material { get; }

    public int TriangleCount => Indices.Length / 3;
}

/// <summary>
/// A complete machine: its geometry, the points a technician inspects on it, and the bounds
/// the camera frames.
/// </summary>
public sealed class SceneModel
{
    public SceneModel(IReadOnlyList<MeshPart> parts, IReadOnlyDictionary<string, Vec3> anchors)
    {
        Parts = parts;
        Anchors = anchors;

        var min = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vec3(float.MinValue, float.MinValue, float.MinValue);

        foreach (var part in parts)
        {
            foreach (var vertex in part.Vertices)
            {
                min = new Vec3(MathF.Min(min.X, vertex.X), MathF.Min(min.Y, vertex.Y), MathF.Min(min.Z, vertex.Z));
                max = new Vec3(MathF.Max(max.X, vertex.X), MathF.Max(max.Y, vertex.Y), MathF.Max(max.Z, vertex.Z));
            }
        }

        Centre = (min + max) * 0.5f;
        Radius = MathF.Max(((max - min) * 0.5f).Length, 0.001f);
        TriangleCount = parts.Sum(part => part.TriangleCount);

        // The camera frames these rather than the bounding sphere or box. A pump is far
        // longer than it is tall, so a sphere large enough to hold it leaves a viewport
        // mostly empty; a box is tighter but its diagonal corners still project well
        // outside the silhouette, which costs almost as much. Every eighth vertex tracks
        // the real outline closely enough to frame against and keeps the per-frame fit
        // cheap.
        FramingPoints = [.. parts.SelectMany(part => part.Vertices).Where((_, i) => i % 8 == 0)];
    }

    /// <summary>A decimated vertex set the camera fits the viewport around.</summary>
    public IReadOnlyList<Vec3> FramingPoints { get; }

    public IReadOnlyList<MeshPart> Parts { get; }

    /// <summary>Inspection point positions in model space, keyed by point id.</summary>
    public IReadOnlyDictionary<string, Vec3> Anchors { get; }

    public Vec3 Centre { get; }

    public float Radius { get; }

    public int TriangleCount { get; }
}
