using System.Numerics;
using SharpGLTF.Schema2;
using SkiaSharp;

namespace Trace.Presentation.Scene3D;

/// <summary>
/// Reads a glTF 2.0 binary asset into a <see cref="SceneModel"/>.
/// </summary>
/// <remarks>
/// Every triangle primitive becomes one <see cref="MeshPart"/>, baked into world space
/// through its node's transform. glTF is right-handed, Y-up and counter-clockwise, which
/// are the renderer's own conventions, so no axis is flipped.
///
/// Inspection points come from the file too: a node with a name and no mesh is an anchor,
/// and its name is the point id. That lets whoever prepares the model place the points on
/// it, instead of the app carrying a table of coordinates per machine.
/// </remarks>
public static class GltfEquipmentLoader
{
    private const string BaseColorChannel = "BaseColor";

    public static SceneModel Load(Stream glb)
    {
        var root = ModelRoot.ReadGLB(glb);
        var parts = new List<MeshPart>();
        var anchors = new Dictionary<string, Vec3>();

        foreach (var node in root.DefaultScene.VisualChildren)
        {
            Visit(node, parts, anchors);
        }

        if (parts.Count == 0)
        {
            throw new InvalidDataException("The model contains no triangle geometry.");
        }

        return new SceneModel(parts, anchors);
    }

    private static void Visit(Node node, List<MeshPart> parts, Dictionary<string, Vec3> anchors)
    {
        var world = node.WorldMatrix;

        if (node.Mesh is { } mesh)
        {
            foreach (var primitive in mesh.Primitives)
            {
                if (ToPart(primitive, world) is { } part)
                {
                    parts.Add(part);
                }
            }
        }
        else if (!string.IsNullOrEmpty(node.Name))
        {
            var t = world.Translation;
            anchors[node.Name] = new Vec3(t.X, t.Y, t.Z);
        }

        foreach (var child in node.VisualChildren)
        {
            Visit(child, parts, anchors);
        }
    }

    private static MeshPart? ToPart(MeshPrimitive primitive, Matrix4x4 world)
    {
        var positions = primitive.GetVertexAccessor("POSITION")?.AsVector3Array();
        if (positions is null || positions.Count == 0)
        {
            return null;
        }

        var vertices = new Vec3[positions.Count];
        for (var i = 0; i < vertices.Length; i++)
        {
            var p = Vector3.Transform(positions[i], world);
            vertices[i] = new Vec3(p.X, p.Y, p.Z);
        }

        var indices = new List<int>();
        foreach (var (a, b, c) in primitive.GetTriangleIndices())
        {
            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
        }

        if (indices.Count == 0)
        {
            return null;
        }

        return new MeshPart(vertices, [.. indices], Material.Machined(Albedo(primitive.Material)));
    }

    private static SKColor Albedo(SharpGLTF.Schema2.Material? material)
    {
        // Textures are not sampled: the renderer flat-shades each face from its material
        // colour, so the PBR base colour factor is the whole of what it can use.
        var colour = material?.FindChannel(BaseColorChannel)?.Color ?? new Vector4(0.6f, 0.62f, 0.64f, 1f);

        return new SKColor(
            (byte)Math.Clamp(colour.X * 255f, 0f, 255f),
            (byte)Math.Clamp(colour.Y * 255f, 0f, 255f),
            (byte)Math.Clamp(colour.Z * 255f, 0f, 255f));
    }
}
