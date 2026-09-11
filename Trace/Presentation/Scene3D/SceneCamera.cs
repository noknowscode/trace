namespace Trace.Presentation.Scene3D;

/// <summary>
/// An orbit camera framed on the machine. Yaw and pitch are what a technician changes by
/// dragging; distance is derived from the model so every asset arrives well framed without
/// per-asset tuning.
/// </summary>
public sealed class SceneCamera
{
    private const float MinPitch = -1.25f;
    private const float MaxPitch = 1.25f;

    public float Yaw { get; set; } = 0.62f;

    public float Pitch { get; set; } = 0.36f;

    /// <summary>Multiplier on the auto-framed distance. Larger pulls back.</summary>
    public float Zoom { get; set; } = 1f;

    public void Orbit(float deltaYaw, float deltaPitch)
    {
        Yaw += deltaYaw;
        Pitch = Math.Clamp(Pitch + deltaPitch, MinPitch, MaxPitch);
    }

    /// <summary>
    /// Builds the world-to-view transform and the projection scale for a given viewport.
    /// The machine is centred and framed so it fills the view with a small margin.
    /// </summary>
    public ViewTransform BuildTransform(SceneModel model, float width, float height)
    {
        var cosPitch = MathF.Cos(Pitch);
        var forward = new Vec3(
            MathF.Sin(Yaw) * cosPitch,
            MathF.Sin(Pitch),
            MathF.Cos(Yaw) * cosPitch).Normalized;

        var right = Vec3.Cross(Vec3.Up, forward).Normalized;
        var up = Vec3.Cross(forward, right).Normalized;

        // Frame the bounding sphere: a 34 degree half-angle keeps perspective honest
        // without the wide-lens distortion that makes equipment look like a toy.
        const float HalfFieldOfView = 0.34f;
        var distance = model.Radius / MathF.Tan(HalfFieldOfView) * 1.12f * Zoom;
        var eye = model.Centre + (forward * distance);

        // Solve the focal length that just fits the machine's projected outline into this
        // viewport, so a tall phone panel and a wide desktop one are both filled. The
        // margin leaves room for the markers, which sit on top of the silhouette.
        const float Margin = 0.9f;
        var halfWidth = width * 0.5f * Margin;
        var halfHeight = height * 0.5f * Margin;

        var focal = float.MaxValue;
        foreach (var point in model.FramingPoints)
        {
            var relative = point - eye;
            var depth = MathF.Max(-Vec3.Dot(relative, forward), 0.01f);
            var x = MathF.Abs(Vec3.Dot(relative, right));
            var y = MathF.Abs(Vec3.Dot(relative, up));

            if (x > 1e-4f)
            {
                focal = MathF.Min(focal, halfWidth * depth / x);
            }

            if (y > 1e-4f)
            {
                focal = MathF.Min(focal, halfHeight * depth / y);
            }
        }

        if (float.IsInfinity(focal) || focal <= 0f)
        {
            focal = MathF.Min(width, height) * 0.5f / MathF.Tan(HalfFieldOfView);
        }

        return new ViewTransform(eye, right, up, forward, focal, width * 0.5f, height * 0.5f);
    }
}

/// <summary>
/// World space to screen space. Held as a readonly struct so the renderer can project tens
/// of thousands of vertices without touching the heap.
/// </summary>
public readonly struct ViewTransform(
    Vec3 eye,
    Vec3 right,
    Vec3 up,
    Vec3 forward,
    float focal,
    float centreX,
    float centreY)
{
    public Vec3 Eye { get; } = eye;

    public Vec3 Right { get; } = right;

    public Vec3 Up { get; } = up;

    public Vec3 Forward { get; } = forward;

    /// <summary>View-space position: X right, Y up, Z towards the camera (positive = in front).</summary>
    public Vec3 ToView(Vec3 world)
    {
        var relative = world - Eye;
        return new Vec3(
            Vec3.Dot(relative, Right),
            Vec3.Dot(relative, Up),
            -Vec3.Dot(relative, Forward));
    }

    /// <summary>Projects a view-space point to screen pixels. Depth is the view-space Z.</summary>
    public (float X, float Y, float Depth) Project(Vec3 view)
    {
        var depth = MathF.Max(view.Z, 0.01f);
        var scale = focal / depth;
        return (centreX + (view.X * scale), centreY - (view.Y * scale), depth);
    }

    public (float X, float Y, float Depth) ProjectWorld(Vec3 world) => Project(ToView(world));
}
