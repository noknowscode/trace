using System.Numerics;
using SharpGLTF.Schema2;
using SharpGLTF.Transforms;
using Trace.Presentation.Scene3D;

namespace Trace.Tests.Support;

/// <summary>
/// Writes a <see cref="SceneModel"/> as a glTF binary: one mesh per part, and one empty,
/// named node per inspection point. This is the same shape a modeller would hand over, so
/// the loader is exercised against exactly what it will meet in the field.
/// </summary>
internal static class GltfEquipmentWriter
{
    public static void Write(SceneModel model, Stream destination)
    {
        var root = ModelRoot.CreateModel();
        var scene = root.UseScene("Equipment");

        for (var index = 0; index < model.Parts.Count; index++)
        {
            var part = model.Parts[index];
            var albedo = part.Material.Albedo;

            var material = root.CreateMaterial($"part-{index}").WithPBRMetallicRoughness();
            var baseColor = material.FindChannel("BaseColor")!.Value;
            baseColor.Color = new Vector4(albedo.Red / 255f, albedo.Green / 255f, albedo.Blue / 255f, 1f);

            var positions = new Vector3[part.Vertices.Length];
            for (var i = 0; i < positions.Length; i++)
            {
                var v = part.Vertices[i];
                positions[i] = new Vector3(v.X, v.Y, v.Z);
            }

            var mesh = root.CreateMesh($"part-{index}");
            mesh.CreatePrimitive()
                .WithVertexAccessor("POSITION", positions)
                .WithIndicesAccessor(PrimitiveType.TRIANGLES, part.Indices)
                .WithMaterial(material);

            scene.CreateNode($"part-{index}").WithMesh(mesh);
        }

        foreach (var (id, position) in model.Anchors)
        {
            var anchor = scene.CreateNode(id);
            anchor.LocalTransform = new AffineTransform(null, null, new Vector3(position.X, position.Y, position.Z));
        }

        root.WriteGLB(destination);
    }
}
