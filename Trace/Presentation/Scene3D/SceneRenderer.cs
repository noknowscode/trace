using SkiaSharp;

namespace Trace.Presentation.Scene3D;

/// <summary>The colours the scene is lit and grounded with, taken from the app theme.</summary>
public readonly record struct ScenePalette(SKColor Background, SKColor Ground, SKColor KeyLight, SKColor FillLight)
{
    /// <summary>
    /// A theme colour as the renderer needs it, with the brush's own opacity folded into the
    /// alpha, so a resource can say "black at 35 %" once.
    /// </summary>
    public static SKColor FromColor(byte alpha, byte red, byte green, byte blue, double opacity) =>
        new(red, green, blue, (byte)Math.Round(alpha * Math.Clamp(opacity, 0d, 1d)));
}

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
public sealed class SceneRenderer : IDisposable
{
    private const float Supersample = 2f;

    private SKPoint[] _positions = new SKPoint[3072];
    private SKColor[] _colors = new SKColor[3072];
    private Face[] _faces = new Face[1024];

    // Native Skia objects live across frames. The offscreen surface alone is the whole
    // viewport at twice the size — recreating it per frame is the single largest cost of
    // an orbit, and a mask filter left to the finaliser is native memory the GC cannot see.
    private SKSurface? _surface;
    private int _surfaceWidth;
    private int _surfaceHeight;
    private SKPaint? _meshPaint;
    private SKPaint? _blitPaint;
    private SKPaint? _shadowPaint;
    private SKPathBuilder? _shadowBuilder;
    private SKMaskFilter? _shadowBlur;
    private float _shadowSigma = -1f;

    /// <summary>Where each inspection point landed, in 0..1 of the destination rectangle.</summary>
    public Dictionary<string, AnchorProjection> Anchors { get; } = [];

    /// <param name="context">The destination's GPU context, or null for a raster canvas.</param>
    /// <param name="bounds">The drawing area in the canvas's own units.</param>
    /// <param name="pixelScale">
    /// Device pixels per canvas unit. A canvas that already carries the display scale draws
    /// in logical units, and the supersampled surface has to be sized for the pixels those
    /// units will actually cover or the result is blurred back up on the way out.
    /// </param>
    public void Render(
        SKCanvas canvas,
        GRRecordingContext? context,
        SKRect bounds,
        SceneModel model,
        SceneCamera camera,
        ScenePalette palette,
        float pixelScale = 1f)
    {
        if (bounds.Width < 4 || bounds.Height < 4)
        {
            canvas.Clear(palette.Background);
            Anchors.Clear();
            return;
        }

        var density = Supersample * MathF.Max(pixelScale, 0.5f);
        var scaledWidth = (int)MathF.Ceiling(bounds.Width * density);
        var scaledHeight = (int)MathF.Ceiling(bounds.Height * density);
        var surface = EnsureSurface(scaledWidth, scaledHeight, context);

        if (surface is null)
        {
            // Without an offscreen surface, draw straight to the canvas rather than nothing.
            canvas.Clear(palette.Background);
            DrawScene(canvas, bounds.Width, bounds.Height, model, camera, palette, 1f);
            return;
        }

        surface.Canvas.Clear(palette.Background);
        DrawScene(surface.Canvas, scaledWidth, scaledHeight, model, camera, palette, density);

        // An exact 2:1 downscale with linear filtering already averages a 2x2 box per pixel;
        // a mip chain on a fresh image every frame would be rebuilt every frame for nothing.
        using var image = surface.Snapshot();
        canvas.DrawImage(
            image,
            new SKRect(0, 0, bounds.Width, bounds.Height),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None),
            BlitPaint);
    }

    /// <summary>
    /// Releases the native Skia state. Safe to call between frames: the next render
    /// recreates what it needs, so the owning view can drop everything on Unloaded and pick
    /// up again if it is reloaded.
    /// </summary>
    public void Dispose()
    {
        _surface?.Dispose();
        _surface = null;
        _surfaceWidth = 0;
        _surfaceHeight = 0;
        if (_shadowPaint is not null)
        {
            _shadowPaint.MaskFilter = null;
        }

        _shadowBlur?.Dispose();
        _shadowBlur = null;
        _shadowSigma = -1f;
        _meshPaint?.Dispose();
        _blitPaint?.Dispose();
        _shadowPaint?.Dispose();
        _shadowBuilder?.Dispose();
        _meshPaint = _blitPaint = _shadowPaint = null;
        _shadowBuilder = null;
    }

    // The small native objects are created on first use and again after a Dispose, so releasing
    // them on Unloaded never leaves a later frame drawing with a freed paint.
    private SKPaint MeshPaint => _meshPaint ??= new SKPaint { IsAntialias = false, Color = SKColors.White };

    private SKPaint BlitPaint => _blitPaint ??= new SKPaint { IsAntialias = true };

    private SKPaint ShadowPaint => _shadowPaint ??= new SKPaint { IsAntialias = true };

    private SKPathBuilder ShadowBuilder => _shadowBuilder ??= new SKPathBuilder();

    private SKSurface? EnsureSurface(int width, int height, GRRecordingContext? context)
    {
        if (_surface is not null && _surfaceWidth == width && _surfaceHeight == height)
        {
            return _surface;
        }

        _surface?.Dispose();
        var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

        // Follow the destination: a GPU-backed canvas gets a GPU-backed scratch surface so
        // the final blit never crosses the bus, and a raster canvas gets raster.
        _surface = context is not null
            ? SKSurface.Create(context, budgeted: true, info) ?? SKSurface.Create(info)
            : SKSurface.Create(info);

        _surfaceWidth = width;
        _surfaceHeight = height;
        return _surface;
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
            var normals = part.Normals;

            for (var i = 0; i < indices.Length; i += 3)
            {
                var a = vertices[indices[i]];
                var b = vertices[indices[i + 1]];
                var c = vertices[indices[i + 2]];

                var normal = normals[i / 3];

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

                // The eye is always outside the model's bounding sphere, so this direction
                // is never degenerate.
                var toEye = (view.Eye - ((a + b + c) / 3f)).Normalized;

                _faces[faceCount++] = new Face(
                    ax, ay, bx, by, cx, cy,
                    (va.Z + vb.Z + vc.Z) / 3f,
                    Shade(part.Material, normal, toEye, keyDirection, fillDirection, palette));
            }
        }

        if (faceCount > 0)
        {
            // Painter's order: farthest first.
            Array.Sort(_faces, 0, faceCount, FaceDepthComparer.Instance);

            var used = faceCount * 3;
            EnsureVertexCapacity(used);

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

            // SKVertices only takes whole arrays, and copying the used prefix out every frame
            // would defeat the pooling. Instead the tail past the last face is collapsed onto one
            // point: a triangle with three identical corners covers no pixels, so the whole
            // pooled buffer can be handed over as-is.
            var last = _positions[used - 1];
            for (var i = used; i < _positions.Length; i++)
            {
                _positions[i] = last;
            }

            // Skia combines the vertex colours with the paint through the blend mode:
            // modulating against white passes them through untouched. Leaving the paint at
            // its default black draws the whole machine as a silhouette.
            using var mesh = SKVertices.CreateCopy(SKVertexMode.Triangles, _positions, _colors);
            canvas.DrawVertices(mesh, SKBlendMode.Modulate, MeshPaint);
        }

        // Anchors are refreshed even when nothing was drawn, or the markers would stay
        // pinned where a previous frame put them.
        ProjectAnchors(model, view, width, height);
    }

    /// <summary>
    /// Projects each inspection point and records whether the machine is between it and the
    /// camera, so a marker on the far side can be shown as behind rather than floating.
    /// </summary>
    private void ProjectAnchors(SceneModel model, ViewTransform view, float width, float height)
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

    private void DrawGroundShadow(
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

        // One builder reused across frames; Detach hands over the path and leaves the
        // builder empty for the next frame.
        var builder = ShadowBuilder;
        var paint = ShadowPaint;

        builder.MoveTo(ellipse[0]);
        for (var i = 1; i < ellipse.Length; i++)
        {
            builder.LineTo(ellipse[i]);
        }

        builder.Close();
        using var path = builder.Detach();

        // The blur is the most expensive operation in the frame and its radius only changes
        // with the supersample factor, so the filter is rebuilt when that changes and not
        // otherwise. Disposing a paint does not dispose its mask filter.
        var sigma = 14f * scale;
        if (_shadowBlur is null || _shadowSigma != sigma)
        {
            paint.MaskFilter = null;
            _shadowBlur?.Dispose();
            _shadowBlur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, sigma);
            _shadowSigma = sigma;
            paint.MaskFilter = _shadowBlur;
        }

        paint.Color = palette.Ground;
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
