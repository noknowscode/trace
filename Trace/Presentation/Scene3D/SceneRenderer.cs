using SkiaSharp;

namespace Trace.Presentation.Scene3D;

/// <summary>The colours the scene is lit and grounded with, taken from the app theme.</summary>
public readonly record struct ScenePalette(SKColor Background, SKColor Ground, SKColor KeyLight, SKColor FillLight);

/// <summary>
/// Rasterises a <see cref="SceneModel"/>.
/// </summary>
/// <remarks>
/// Triangles are transformed and lit on the CPU, sorted back to front, and handed to Skia
/// as a single vertex buffer — one <c>DrawVertices</c> call for the whole machine. Skia has
/// no depth buffer, so the painter's order is the depth test; equipment is a set of convex
/// solids, which is exactly the case where sorting by centroid is sound.
///
/// Anti-aliasing is left off for the mesh: with it on, Skia feathers every triangle
/// individually and the shared edges inside a smooth surface show as hairline seams. The
/// silhouette is cleaned up by rendering the whole scene at twice the size and scaling it
/// down, which costs one extra surface and no per-frame branching.
/// </remarks>
public sealed class SceneRenderer
{
    private const float Supersample = 2f;

    private SKPoint[] _positions = new SKPoint[3072];
    private SKColor[] _colors = new SKColor[3072];
    private Face[] _faces = new Face[1024];

    /// <summary>Where each inspection point landed, in 0..1 of the destination rectangle.</summary>
    public Dictionary<string, AnchorProjection> Anchors { get; } = [];

    public void Render(SKCanvas canvas, SKRect bounds, SceneModel model, SceneCamera camera, ScenePalette palette)
    {
        canvas.Clear(palette.Background);

        if (bounds.Width < 4 || bounds.Height < 4)
        {
            return;
        }

        var scaledWidth = (int)MathF.Ceiling(bounds.Width * Supersample);
        var scaledHeight = (int)MathF.Ceiling(bounds.Height * Supersample);

        using var surface = SKSurface.Create(new SKImageInfo(
            scaledWidth, scaledHeight, SKColorType.Rgba8888, SKAlphaType.Premul));

        if (surface is null)
        {
            // Without an offscreen surface, draw straight to the canvas rather than nothing.
            DrawScene(canvas, bounds.Width, bounds.Height, model, camera, palette, 1f);
            return;
        }

        surface.Canvas.Clear(palette.Background);
        DrawScene(surface.Canvas, scaledWidth, scaledHeight, model, camera, palette, Supersample);

        using var image = surface.Snapshot();
        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawImage(
            image,
            new SKRect(0, 0, bounds.Width, bounds.Height),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear),
            paint);
    }

    private void DrawScene(
        SKCanvas canvas,
        float width,
        float height,
        SceneModel model,
        SceneCamera camera,
        ScenePalette palette,
        float scale)
    {
        var view = camera.BuildTransform(model, width, height);

        // A soft contact shadow under the machine, so it sits on something.
        DrawGroundShadow(canvas, model, view, palette, scale);

        var faceCount = 0;
        EnsureFaceCapacity(model.TriangleCount);

        // Key light over the technician's left shoulder, fill from the opposite side, and a
        // faint up-light so undersides do not go to pure black.
        var keyDirection = new Vec3(-0.45f, 0.78f, 0.44f).Normalized;
        var fillDirection = new Vec3(0.62f, 0.2f, -0.5f).Normalized;

        foreach (var part in model.Parts)
        {
            var vertices = part.Vertices;
            var indices = part.Indices;

            for (var i = 0; i < indices.Length; i += 3)
            {
                var a = vertices[indices[i]];
                var b = vertices[indices[i + 1]];
                var c = vertices[indices[i + 2]];

                var normal = Vec3.Cross(b - a, c - a).Normalized;

                var va = view.ToView(a);
                var vb = view.ToView(b);
                var vc = view.ToView(c);

                if (va.Z <= 0.02f || vb.Z <= 0.02f || vc.Z <= 0.02f)
                {
                    continue;
                }

                var (ax, ay, _) = view.Project(va);
                var (bx, by, _) = view.Project(vb);
                var (cx, cy, _) = view.Project(vc);

                // Screen-space winding: negative area means the face points away.
                var area = ((bx - ax) * (cy - ay)) - ((by - ay) * (cx - ax));
                if (area >= 0)
                {
                    continue;
                }

                var toEye = ((model.Centre - view.Eye).Length > 0
                    ? (view.Eye - ((a + b + c) / 3f)).Normalized
                    : Vec3.Up);

                _faces[faceCount++] = new Face(
                    ax, ay, bx, by, cx, cy,
                    (va.Z + vb.Z + vc.Z) / 3f,
                    Shade(part.Material, normal, toEye, keyDirection, fillDirection, palette));
            }
        }

        if (faceCount == 0)
        {
            return;
        }

        // Painter's order: farthest first.
        Array.Sort(_faces, 0, faceCount, FaceDepthComparer.Instance);

        EnsureVertexCapacity(faceCount * 3);

        for (var i = 0; i < faceCount; i++)
        {
            var face = _faces[i];
            var v = i * 3;

            _positions[v] = new SKPoint(face.AX, face.AY);
            _positions[v + 1] = new SKPoint(face.BX, face.BY);
            _positions[v + 2] = new SKPoint(face.CX, face.CY);
            _colors[v] = face.Colour;
            _colors[v + 1] = face.Colour;
            _colors[v + 2] = face.Colour;
        }

        var positions = _positions.AsSpan(0, faceCount * 3).ToArray();
        var colours = _colors.AsSpan(0, faceCount * 3).ToArray();

        // Skia combines the vertex colours with the paint through the blend mode: modulating
        // against white passes them through untouched. Leaving the paint at its default
        // black draws the whole machine as a silhouette.
        using var mesh = SKVertices.CreateCopy(SKVertexMode.Triangles, positions, colours);
        using var paint = new SKPaint { IsAntialias = false, Color = SKColors.White };
        canvas.DrawVertices(mesh, SKBlendMode.Modulate, paint);

        ProjectAnchors(model, view, width, height, scale);
    }

    /// <summary>
    /// Projects each inspection point and records whether the machine is between it and the
    /// camera, so a marker on the far side can be shown as behind rather than floating.
    /// </summary>
    private void ProjectAnchors(SceneModel model, ViewTransform view, float width, float height, float scale)
    {
        Anchors.Clear();

        foreach (var (id, position) in model.Anchors)
        {
            var viewSpace = view.ToView(position);
            if (viewSpace.Z <= 0.02f)
            {
                continue;
            }

            var (x, y, _) = view.Project(viewSpace);

            // An anchor is facing the camera when its outward direction from the machine's
            // axis has a positive component towards the eye.
            var outward = (position - new Vec3(position.X, model.Centre.Y, model.Centre.Z)).Normalized;
            var towardsEye = (view.Eye - position).Normalized;
            var facing = Vec3.Dot(outward, towardsEye) > -0.15f;

            Anchors[id] = new AnchorProjection(
                x / width,
                y / height,
                facing);
        }
    }

    private static void DrawGroundShadow(
        SKCanvas canvas,
        SceneModel model,
        ViewTransform view,
        ScenePalette palette,
        float scale)
    {
        var floor = model.Centre.Y - model.Radius * 0.62f;
        var radius = model.Radius * 0.95f;

        Span<SKPoint> ellipse = stackalloc SKPoint[24];
        for (var i = 0; i < ellipse.Length; i++)
        {
            var angle = MathF.Tau * i / ellipse.Length;
            var world = new Vec3(
                model.Centre.X + (MathF.Cos(angle) * radius * 1.25f),
                floor,
                model.Centre.Z + (MathF.Sin(angle) * radius * 0.85f));

            var projected = view.ProjectWorld(world);
            ellipse[i] = new SKPoint(projected.X, projected.Y);
        }

        using var path = new SKPath();
        path.MoveTo(ellipse[0]);
        for (var i = 1; i < ellipse.Length; i++)
        {
            path.LineTo(ellipse[i]);
        }

        path.Close();

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = palette.Ground,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 14f * scale),
        };

        canvas.DrawPath(path, paint);
    }

    private static SKColor Shade(
        Material material,
        Vec3 normal,
        Vec3 toEye,
        Vec3 key,
        Vec3 fill,
        ScenePalette palette)
    {
        var keyAmount = MathF.Max(Vec3.Dot(normal, key), 0f);
        var fillAmount = MathF.Max(Vec3.Dot(normal, fill), 0f) * 0.42f;
        var sky = (Vec3.Dot(normal, Vec3.Up) + 1f) * 0.5f * 0.22f;

        // A narrow rim along grazing angles reads as the light catching a machined edge.
        var rim = MathF.Pow(1f - MathF.Max(Vec3.Dot(normal, toEye), 0f), 3.2f) * material.Gloss * 0.9f;

        var albedo = material.Albedo;

        var r = (albedo.Red * (0.2f + (keyAmount * 0.85f) + sky))
            + (palette.KeyLight.Red * keyAmount * material.Gloss * 0.35f)
            + (palette.FillLight.Red * fillAmount)
            + (palette.KeyLight.Red * rim);
        var g = (albedo.Green * (0.2f + (keyAmount * 0.85f) + sky))
            + (palette.KeyLight.Green * keyAmount * material.Gloss * 0.35f)
            + (palette.FillLight.Green * fillAmount)
            + (palette.KeyLight.Green * rim);
        var b = (albedo.Blue * (0.2f + (keyAmount * 0.85f) + sky))
            + (palette.KeyLight.Blue * keyAmount * material.Gloss * 0.35f)
            + (palette.FillLight.Blue * fillAmount)
            + (palette.KeyLight.Blue * rim);

        return new SKColor(Clamp(r), Clamp(g), Clamp(b));
    }

    private static byte Clamp(float value) => (byte)Math.Clamp(value, 0f, 255f);

    private void EnsureFaceCapacity(int required)
    {
        if (_faces.Length < required)
        {
            _faces = new Face[required + 256];
        }
    }

    private void EnsureVertexCapacity(int required)
    {
        if (_positions.Length < required)
        {
            _positions = new SKPoint[required + 768];
            _colors = new SKColor[required + 768];
        }
    }

    private readonly record struct Face(
        float AX, float AY,
        float BX, float BY,
        float CX, float CY,
        float Depth,
        SKColor Colour);

    private sealed class FaceDepthComparer : IComparer<Face>
    {
        public static FaceDepthComparer Instance { get; } = new();

        public int Compare(Face x, Face y) => y.Depth.CompareTo(x.Depth);
    }
}

/// <summary>Where an inspection point landed on screen, in 0..1 of the view.</summary>
public readonly record struct AnchorProjection(float X, float Y, bool IsFacingCamera);
