namespace Axrone.Animation;

using Axrone.Simd;

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
        if (lenSq < AnimationConstants.QuaternionDegenerateLengthSq)
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

    /// <summary>Rotates a vector without building a matrix.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vector3 RotateVector(in Quaternion q, in Vector3 v)
    {
        Vector3 qv = new(q.X, q.Y, q.Z);
        Vector3 t = 2.0f * Vector3.Cross(qv, v);
        return v + (q.W * t) + Vector3.Cross(qv, t);
    }

    /// <summary>Hamilton product.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quaternion Multiply(in Quaternion a, in Quaternion b)
    {
        return new Quaternion(
            (a.W * b.X) + (a.X * b.W) + (a.Y * b.Z) - (a.Z * b.Y),
            (a.W * b.Y) - (a.X * b.Z) + (a.Y * b.W) + (a.Z * b.X),
            (a.W * b.Z) + (a.X * b.Y) - (a.Y * b.X) + (a.Z * b.W),
            (a.W * b.W) - (a.X * b.X) - (a.Y * b.Y) - (a.Z * b.Z));
    }

    /// <summary>Conjugate (unit-quaternion inverse).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quaternion Invert(in Quaternion q)
    {
        return new Quaternion(-q.X, -q.Y, -q.Z, q.W);
    }

    /// <summary>
    /// Concatenates a local transform onto its parent world transform: the single
    /// home for hierarchy composition (forward kinematics, subtree refresh, pose
    /// evaluation all route here, so the math cannot drift between call sites).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ConcatenateLocal(
        in Vector3 parentTranslation,
        in Quaternion parentRotation,
        in Vector3 parentScale,
        in Vector3 localTranslation,
        in Quaternion localRotation,
        in Vector3 localScale,
        out Vector3 outTranslation,
        out Quaternion outRotation,
        out Vector3 outScale)
    {
        outScale = parentScale * localScale;
        outRotation = Normalize(parentRotation * localRotation);
        outTranslation = parentTranslation + Vector3.Transform(localTranslation * parentScale, parentRotation);
    }

    /// <summary>Composes a column-major TRS matrix.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ComposeTransformMatrix(in Vector3 translation, in Quaternion rotation, in Vector3 scale, Span<float> output)
    {
        float xx = rotation.X * rotation.X;
        float yy = rotation.Y * rotation.Y;
        float zz = rotation.Z * rotation.Z;
        float xy = rotation.X * rotation.Y;
        float xz = rotation.X * rotation.Z;
        float yz = rotation.Y * rotation.Z;
        float wx = rotation.W * rotation.X;
        float wy = rotation.W * rotation.Y;
        float wz = rotation.W * rotation.Z;

        output[0] = (1.0f - (2.0f * (yy + zz))) * scale.X;
        output[1] = (2.0f * (xy - wz)) * scale.Y;
        output[2] = (2.0f * (xz + wy)) * scale.Z;
        output[3] = translation.X;

        output[4] = (2.0f * (xy + wz)) * scale.X;
        output[5] = (1.0f - (2.0f * (xx + zz))) * scale.Y;
        output[6] = (2.0f * (yz - wx)) * scale.Z;
        output[7] = translation.Y;

        output[8] = (2.0f * (xz - wy)) * scale.X;
        output[9] = (2.0f * (yz + wx)) * scale.Y;
        output[10] = (1.0f - (2.0f * (xx + yy))) * scale.Z;
        output[11] = translation.Z;

        output[12] = 0.0f;
        output[13] = 0.0f;
        output[14] = 0.0f;
        output[15] = 1.0f;
    }

    /// <summary>
    /// Vector lerp with tiered width selection through the SIMD runtime, so forced-scalar
    /// test overrides apply here too.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorizedLerp(ReadOnlySpan<float> from, ReadOnlySpan<float> to, float alpha, Span<float> destination)
    {
        nuint length = (nuint)destination.Length;
        nuint i = 0;

        if (SimdRuntime.IsSupported(SimdFeature.Vector256HardwareAccelerated) && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> vAlpha = Vector256.Create(alpha);
            nuint simdBias = length - (length % (nuint)Vector256<float>.Count);

            ref float rFrom = ref MemoryMarshal.GetReference(from);
            ref float rTo = ref MemoryMarshal.GetReference(to);
            ref float rDst = ref MemoryMarshal.GetReference(destination);

            for (; i < simdBias; i += (nuint)Vector256<float>.Count)
            {
                Vector256<float> v0 = Vector256.LoadUnsafe(ref rFrom, i);
                Vector256<float> v1 = Vector256.LoadUnsafe(ref rTo, i);
                Vector256<float> res = v0 + (vAlpha * (v1 - v0));
                res.StoreUnsafe(ref rDst, i);
            }
        }
        else if (SimdRuntime.IsSupported(SimdFeature.Vector128HardwareAccelerated) && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> vAlpha = Vector128.Create(alpha);
            nuint simdBias = length - (length % (nuint)Vector128<float>.Count);

            ref float rFrom = ref MemoryMarshal.GetReference(from);
            ref float rTo = ref MemoryMarshal.GetReference(to);
            ref float rDst = ref MemoryMarshal.GetReference(destination);

            for (; i < simdBias; i += (nuint)Vector128<float>.Count)
            {
                Vector128<float> v0 = Vector128.LoadUnsafe(ref rFrom, i);
                Vector128<float> v1 = Vector128.LoadUnsafe(ref rTo, i);
                Vector128<float> res = v0 + (vAlpha * (v1 - v0));
                res.StoreUnsafe(ref rDst, i);
            }
        }

        for (; i < length; i++)
        {
            destination[(int)i] = from[(int)i] + (alpha * (to[(int)i] - from[(int)i]));
        }
    }

    /// <summary>Row-major 4x4 product.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MultiplyMatrix4x4(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> dest)
    {
        for (int r = 0; r < 4; r++)
        {
            int r4 = r * 4;
            float a0 = a[r4], a1 = a[r4 + 1], a2 = a[r4 + 2], a3 = a[r4 + 3];
            dest[r4] = (a0 * b[0]) + (a1 * b[4]) + (a2 * b[8]) + (a3 * b[12]);
            dest[r4 + 1] = (a0 * b[1]) + (a1 * b[5]) + (a2 * b[9]) + (a3 * b[13]);
            dest[r4 + 2] = (a0 * b[2]) + (a1 * b[6]) + (a2 * b[10]) + (a3 * b[14]);
            dest[r4 + 3] = (a0 * b[3]) + (a1 * b[7]) + (a2 * b[11]) + (a3 * b[15]);
        }
    }
}
