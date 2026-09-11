using System.Runtime.CompilerServices;

namespace Trace.Presentation.Scene3D;

/// <summary>
/// A point or direction in the scene. A struct with aggressive inlining because the
/// renderer walks every vertex of the machine on each frame.
/// </summary>
public readonly record struct Vec3(float X, float Y, float Z)
{
    public static readonly Vec3 Zero = new(0, 0, 0);

    public static readonly Vec3 Up = new(0, 1, 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator *(Vec3 a, float k) => new(a.X * k, a.Y * k, a.Z * k);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator /(Vec3 a, float k) => new(a.X / k, a.Y / k, a.Z / k);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(Vec3 a, Vec3 b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Cross(Vec3 a, Vec3 b) => new(
        (a.Y * b.Z) - (a.Z * b.Y),
        (a.Z * b.X) - (a.X * b.Z),
        (a.X * b.Y) - (a.Y * b.X));

    public float Length => MathF.Sqrt(Dot(this, this));

    public Vec3 Normalized
    {
        get
        {
            var length = Length;
            return length < 1e-6f ? Zero : this / length;
        }
    }

    /// <summary>Any unit vector perpendicular to this one, for building a frame around an axis.</summary>
    public Vec3 AnyPerpendicular()
    {
        var reference = MathF.Abs(X) < 0.9f ? new Vec3(1, 0, 0) : new Vec3(0, 1, 0);
        return Cross(this, reference).Normalized;
    }
}
