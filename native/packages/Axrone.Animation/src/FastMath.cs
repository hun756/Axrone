namespace Axrone.Animation;

/// <summary>Branch-lean quaternion and time math for the animation hot path.</summary>
public static class FastMath
{
    /// <summary>Clamps to the unit interval.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Clamp01(float value)
    {
        if (value < 0.0f)
        {
            return 0.0f;
        }

        if (value > 1.0f)
        {
            return 1.0f;
        }

        return value;
    }

    /// <summary>Zero-safe normalization; degenerate quaternions become identity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quaternion Normalize(in Quaternion q)
    {
        float lenSq = (q.X * q.X) + (q.Y * q.Y) + (q.Z * q.Z) + (q.W * q.W);
        if (lenSq < 1e-15f)
        {
            return Quaternion.Identity;
        }

        float invLen = 1.0f / MathF.Sqrt(lenSq);
        return new Quaternion(q.X * invLen, q.Y * invLen, q.Z * invLen, q.W * invLen);
    }

    /// <summary>Spherical interpolation with antipodal fix and linear fallback.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quaternion Slerp(in Quaternion q1, in Quaternion q2, float t)
    {
        t = Math.Clamp(t, 0.0f, 1.0f);
        float cosTheta = (q1.X * q2.X) + (q1.Y * q2.Y) + (q1.Z * q2.Z) + (q1.W * q2.W);
        Quaternion target = q2;

        if (cosTheta < 0.0f)
        {
            cosTheta = -cosTheta;
            target = new Quaternion(-target.X, -target.Y, -target.Z, -target.W);
        }

        if (cosTheta > AnimationConstants.SlerpLinearThreshold)
        {
            return Normalize(new Quaternion(
                q1.X + (t * (target.X - q1.X)),
                q1.Y + (t * (target.Y - q1.Y)),
                q1.Z + (t * (target.Z - q1.Z)),
                q1.W + (t * (target.W - q1.W))));
        }

        float angle = MathF.Acos(cosTheta);
        float sinAngle = MathF.Sin(angle);
        float invSinAngle = 1.0f / sinAngle;

        float w1 = MathF.Sin((1.0f - t) * angle) * invSinAngle;
        float w2 = MathF.Sin(t * angle) * invSinAngle;

        return new Quaternion(
            (w1 * q1.X) + (w2 * target.X),
            (w1 * q1.Y) + (w2 * target.Y),
            (w1 * q1.Z) + (w2 * target.Z),
            (w1 * q1.W) + (w2 * target.W));
    }

    /// <summary>Shortest-arc rotation from one direction to another.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaternion QuaternionFromTo(in Vector3 from, in Vector3 to)
    {
        Vector3 v0 = Vector3.Normalize(from);
        Vector3 v1 = Vector3.Normalize(to);
        float d = Vector3.Dot(v0, v1);

        if (d >= 1.0f - AnimationConstants.SoaEpsilon)
        {
            return Quaternion.Identity;
        }

        if (d <= -1.0f + AnimationConstants.SoaEpsilon)
        {
            Vector3 axis = Vector3.Cross(Vector3.UnitX, v0);
            if (axis.LengthSquared() < AnimationConstants.SoaEpsilon)
            {
                axis = Vector3.Cross(Vector3.UnitY, v0);
            }

            axis = Vector3.Normalize(axis);
            return Quaternion.CreateFromAxisAngle(axis, MathF.PI);
        }

        Vector3 a = Vector3.Cross(v0, v1);
        Quaternion q = new(a.X, a.Y, a.Z, 1.0f + d);
        return Quaternion.Normalize(q);
    }

    /// <summary>Wraps time into [0, duration).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float WrapTime(float time, float duration)
    {
        if (duration <= 0.0f)
        {
            return 0.0f;
        }

        float m = time % duration;
        return m < 0.0f ? m + duration : m;
    }
}
