namespace Trace.Presentation.Scene3D;

/// <summary>
/// Builds equipment geometry from solids of revolution and boxes.
/// </summary>
/// <remarks>
/// The machines are authored in code rather than imported so the geometry and the
/// inspection points are defined in the same coordinate space — an anchor is placed on the
/// seal housing because the seal housing is a known solid, not because someone eyeballed a
/// coordinate over a picture. It also keeps the app free of third-party model assets.
/// </remarks>
public static class MeshBuilder
{
    /// <summary>An axis-aligned box, given its centre and full extents.</summary>
    public static MeshPart Box(Vec3 centre, Vec3 size, Material material)
    {
        var h = size * 0.5f;

        var vertices = new[]
        {
            centre + new Vec3(-h.X, -h.Y, -h.Z),
            centre + new Vec3(h.X, -h.Y, -h.Z),
            centre + new Vec3(h.X, h.Y, -h.Z),
            centre + new Vec3(-h.X, h.Y, -h.Z),
            centre + new Vec3(-h.X, -h.Y, h.Z),
            centre + new Vec3(h.X, -h.Y, h.Z),
            centre + new Vec3(h.X, h.Y, h.Z),
            centre + new Vec3(-h.X, h.Y, h.Z),
        };

        var indices = new[]
        {
            0, 2, 1, 0, 3, 2, // back
            4, 5, 6, 4, 6, 7, // front
            0, 1, 5, 0, 5, 4, // bottom
            3, 7, 6, 3, 6, 2, // top
            0, 4, 7, 0, 7, 3, // left
            1, 2, 6, 1, 6, 5, // right
        };

        return new MeshPart(vertices, indices, material);
    }

    /// <summary>
    /// A capped cone between two points — the workhorse. Equal radii give a cylinder, a zero
    /// end radius gives a cone, and a short large-radius run gives a disc or a flange.
    /// </summary>
    public static MeshPart Tube(
        Vec3 from,
        Vec3 to,
        float radiusFrom,
        float radiusTo,
        Material material,
        int segments = 28,
        bool capStart = true,
        bool capEnd = true)
    {
        var axis = (to - from).Normalized;
        var u = axis.AnyPerpendicular();
        var v = Vec3.Cross(axis, u).Normalized;

        var vertices = new List<Vec3>((segments * 2) + 2);
        var indices = new List<int>(segments * 12);

        for (var i = 0; i < segments; i++)
        {
            var angle = MathF.Tau * i / segments;
            var radial = (u * MathF.Cos(angle)) + (v * MathF.Sin(angle));
            vertices.Add(from + (radial * radiusFrom));
            vertices.Add(to + (radial * radiusTo));
        }

        for (var i = 0; i < segments; i++)
        {
            var a = i * 2;
            var b = a + 1;
            var c = ((i + 1) % segments) * 2;
            var d = c + 1;

            indices.Add(a);
            indices.Add(c);
            indices.Add(b);
            indices.Add(b);
            indices.Add(c);
            indices.Add(d);
        }

        if (capStart && radiusFrom > 1e-4f)
        {
            var centre = vertices.Count;
            vertices.Add(from);
            for (var i = 0; i < segments; i++)
            {
                indices.Add(centre);
                indices.Add(i * 2);
                indices.Add(((i + 1) % segments) * 2);
            }
        }

        if (capEnd && radiusTo > 1e-4f)
        {
            var centre = vertices.Count;
            vertices.Add(to);
            for (var i = 0; i < segments; i++)
            {
                indices.Add(centre);
                indices.Add((((i + 1) % segments) * 2) + 1);
                indices.Add((i * 2) + 1);
            }
        }

        return new MeshPart([.. vertices], [.. indices], material);
    }

    public static MeshPart Cylinder(
        Vec3 from,
        Vec3 to,
        float radius,
        Material material,
        int segments = 28,
        bool capped = true) =>
        Tube(from, to, radius, radius, material, segments, capped, capped);

    /// <summary>A ring of a given thickness — bolt collars, bearing shoulders, motor fins.</summary>
    public static MeshPart Ring(
        Vec3 centre,
        Vec3 axis,
        float innerRadius,
        float outerRadius,
        float thickness,
        Material material,
        int segments = 28)
    {
        var direction = axis.Normalized;
        var half = direction * (thickness * 0.5f);
        var u = direction.AnyPerpendicular();
        var v = Vec3.Cross(direction, u).Normalized;

        var vertices = new List<Vec3>(segments * 4);
        var indices = new List<int>(segments * 24);

        for (var i = 0; i < segments; i++)
        {
            var angle = MathF.Tau * i / segments;
            var radial = (u * MathF.Cos(angle)) + (v * MathF.Sin(angle));

            vertices.Add(centre - half + (radial * innerRadius));
            vertices.Add(centre - half + (radial * outerRadius));
            vertices.Add(centre + half + (radial * innerRadius));
            vertices.Add(centre + half + (radial * outerRadius));
        }

        for (var i = 0; i < segments; i++)
        {
            var a = i * 4;
            var b = ((i + 1) % segments) * 4;

            // outer wall
            Quad(indices, a + 1, b + 1, b + 3, a + 3);
            // inner wall
            Quad(indices, b, a, a + 2, b + 2);
            // back face
            Quad(indices, a, b, b + 1, a + 1);
            // front face
            Quad(indices, a + 3, b + 3, b + 2, a + 2);
        }

        return new MeshPart([.. vertices], [.. indices], material);
    }

    /// <summary>
    /// A solid of revolution from a profile of (distance along axis, radius) pairs. This is
    /// what gives a pump volute or a receiver its shouldered, cast look.
    /// </summary>
    /// <remarks>
    /// The profile is an array so the collection expressions at the call sites build arrays:
    /// the WinAppSDK build rejects one that targets <c>IReadOnlyList&lt;T&gt;</c> (CsWinRT1032).
    /// </remarks>
    public static MeshPart Revolve(
        Vec3 origin,
        Vec3 axis,
        (float Along, float Radius)[] profile,
        Material material,
        int segments = 28)
    {
        var direction = axis.Normalized;
        var parts = new List<MeshPart>();

        for (var i = 0; i < profile.Length - 1; i++)
        {
            var (a, ra) = profile[i];
            var (b, rb) = profile[i + 1];

            parts.Add(Tube(
                origin + (direction * a),
                origin + (direction * b),
                ra,
                rb,
                material,
                segments,
                capStart: i == 0,
                capEnd: i == profile.Length - 2));
        }

        return Combine(parts, material);
    }

    /// <summary>Merges parts that share a material into one buffer, to keep the draw cheap.</summary>
    public static MeshPart Combine(IReadOnlyList<MeshPart> parts, Material material)
    {
        var vertices = new List<Vec3>();
        var indices = new List<int>();

        foreach (var part in parts)
        {
            var offset = vertices.Count;
            vertices.AddRange(part.Vertices);
            foreach (var index in part.Indices)
            {
                indices.Add(index + offset);
            }
        }

        return new MeshPart([.. vertices], [.. indices], material);
    }

    private static void Quad(List<int> indices, int a, int b, int c, int d)
    {
        indices.Add(a);
        indices.Add(b);
        indices.Add(c);
        indices.Add(a);
        indices.Add(c);
        indices.Add(d);
    }
}
