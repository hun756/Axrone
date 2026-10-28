namespace Axrone.Simd;

/// <summary>Structure-of-arrays buffer for four-float quaternions.</summary>
public sealed class SoaQuatBuffer : SoaBuffer
{
    /// <summary>Epsilon used by normalization and inversion.</summary>
    public const float Epsilon = 1e-6f;

    /// <summary>Dot threshold above which slerp uses a linear blend.</summary>
    public const float SlerpThreshold = 0.9995f;

    /// <summary>Creates a buffer.</summary>
    /// <param name="capacity">Initial element capacity.</param>
    public SoaQuatBuffer(int capacity)
        : base(capacity, 4)
    {
    }

    /// <summary>Writes one quaternion.</summary>
    public void Set(int index, float x, float y, float z, float w)
    {
        ValidateIndex(index);
        int offset = index * 4;
        Span<float> data = FullSpan;
        data[offset] = x;
        data[offset + 1] = y;
        data[offset + 2] = z;
        data[offset + 3] = w;
    }

    /// <summary>Reads one quaternion.</summary>
    public (float X, float Y, float Z, float W) Get(int index)
    {
        ValidateIndex(index);
        int offset = index * 4;
        ReadOnlySpan<float> data = FullSpan;
        return (data[offset], data[offset + 1], data[offset + 2], data[offset + 3]);
    }

    /// <summary>Gets one quaternion as a writable slice.</summary>
    public Span<float> Slice(int index)
    {
        ValidateIndex(index);
        return FullSpan.Slice(index * 4, 4);
    }

    /// <summary>Writes the identity quaternion.</summary>
    public void SetIdentity(int index) => Set(index, 0f, 0f, 0f, 1f);

    /// <summary>Normalizes one quaternion; degenerate input becomes identity.</summary>
    public void Normalize(int destination, SoaQuatBuffer source, int sourceIndex)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateIndex(destination);
        source.ValidateIndex(sourceIndex);

        ReadOnlySpan<float> s = source.FullSpan.Slice(sourceIndex * 4, 4);
        Span<float> d = FullSpan.Slice(destination * 4, 4);
        float lengthSquared = (s[0] * s[0]) + (s[1] * s[1]) + (s[2] * s[2]) + (s[3] * s[3]);
        if (lengthSquared <= Epsilon)
        {
            d[0] = 0f;
            d[1] = 0f;
            d[2] = 0f;
            d[3] = 1f;
            return;
        }

        float inverse = 1f / MathF.Sqrt(lengthSquared);
        d[0] = s[0] * inverse;
        d[1] = s[1] * inverse;
        d[2] = s[2] * inverse;
        d[3] = s[3] * inverse;
    }

    /// <summary>Computes a quaternion dot product.</summary>
    public static float Dot(SoaQuatBuffer left, int leftIndex, SoaQuatBuffer right, int rightIndex)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        left.ValidateIndex(leftIndex);
        right.ValidateIndex(rightIndex);

        ReadOnlySpan<float> l = left.FullSpan.Slice(leftIndex * 4, 4);
        ReadOnlySpan<float> r = right.FullSpan.Slice(rightIndex * 4, 4);
        return (l[0] * r[0]) + (l[1] * r[1]) + (l[2] * r[2]) + (l[3] * r[3]);
    }

    /// <summary>Multiplies two quaternions.</summary>
    public void Multiply(int destination, SoaQuatBuffer left, int leftIndex, SoaQuatBuffer right, int rightIndex)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        ValidateIndex(destination);
        left.ValidateIndex(leftIndex);
        right.ValidateIndex(rightIndex);

        ReadOnlySpan<float> l = left.FullSpan.Slice(leftIndex * 4, 4);
        ReadOnlySpan<float> r = right.FullSpan.Slice(rightIndex * 4, 4);
        Span<float> d = FullSpan.Slice(destination * 4, 4);
        float ax = l[0], ay = l[1], az = l[2], aw = l[3];
        float bx = r[0], by = r[1], bz = r[2], bw = r[3];
        d[0] = (ax * bw) + (aw * bx) + (ay * bz) - (az * by);
        d[1] = (ay * bw) + (aw * by) + (az * bx) - (ax * bz);
        d[2] = (az * bw) + (aw * bz) + (ax * by) - (ay * bx);
        d[3] = (aw * bw) - (ax * bx) - (ay * by) - (az * bz);
    }

    /// <summary>Inverts one unit quaternion.</summary>
    public void Invert(int destination, SoaQuatBuffer source, int sourceIndex)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateIndex(destination);
        source.ValidateIndex(sourceIndex);

        ReadOnlySpan<float> s = source.FullSpan.Slice(sourceIndex * 4, 4);
        Span<float> d = FullSpan.Slice(destination * 4, 4);
        float lengthSquared = (s[0] * s[0]) + (s[1] * s[1]) + (s[2] * s[2]) + (s[3] * s[3]);
        if (lengthSquared <= Epsilon)
        {
            d[0] = 0f;
            d[1] = 0f;
            d[2] = 0f;
            d[3] = 1f;
            return;
        }

        float inverse = 1f / lengthSquared;
        d[0] = -s[0] * inverse;
        d[1] = -s[1] * inverse;
        d[2] = -s[2] * inverse;
        d[3] = s[3] * inverse;
    }

    /// <summary>Spherical interpolation between two quaternions.</summary>
    public void Slerp(
        int destination,
        SoaQuatBuffer left,
        int leftIndex,
        SoaQuatBuffer right,
        int rightIndex,
        float t)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        ValidateIndex(destination);
        left.ValidateIndex(leftIndex);
        right.ValidateIndex(rightIndex);

        float amount = Math.Clamp(t, 0f, 1f);
        ReadOnlySpan<float> l = left.FullSpan.Slice(leftIndex * 4, 4);
        ReadOnlySpan<float> r = right.FullSpan.Slice(rightIndex * 4, 4);
        Span<float> d = FullSpan.Slice(destination * 4, 4);

        float ax = l[0], ay = l[1], az = l[2], aw = l[3];
        float bx = r[0], by = r[1], bz = r[2], bw = r[3];
        float dot = (ax * bx) + (ay * by) + (az * bz) + (aw * bw);
        if (dot < 0f)
        {
            dot = -dot;
            bx = -bx;
            by = -by;
            bz = -bz;
            bw = -bw;
        }

        float scale0;
        float scale1;
        if (dot > SlerpThreshold)
        {
            scale0 = 1f - amount;
            scale1 = amount;
        }
        else
        {
            float theta = MathF.Acos(dot);
            float sinTheta = MathF.Sin(theta);
            scale0 = MathF.Sin((1f - amount) * theta) / sinTheta;
            scale1 = MathF.Sin(amount * theta) / sinTheta;
        }

        float x = (scale0 * ax) + (scale1 * bx);
        float y = (scale0 * ay) + (scale1 * by);
        float z = (scale0 * az) + (scale1 * bz);
        float w = (scale0 * aw) + (scale1 * bw);
        float lengthSquared = (x * x) + (y * y) + (z * z) + (w * w);
        if (lengthSquared <= Epsilon)
        {
            d[0] = 0f;
            d[1] = 0f;
            d[2] = 0f;
            d[3] = 1f;
            return;
        }

        float inverse = 1f / MathF.Sqrt(lengthSquared);
        d[0] = x * inverse;
        d[1] = y * inverse;
        d[2] = z * inverse;
        d[3] = w * inverse;
    }

    /// <summary>Applies one quaternion to a vector.</summary>
    public void ApplyToVec3(int quaternionIndex, SoaVec3Buffer destination, int destinationIndex, SoaVec3Buffer source, int sourceIndex)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(source);
        ValidateIndex(quaternionIndex);
        destination.ValidateIndex(destinationIndex);
        source.ValidateIndex(sourceIndex);

        ReadOnlySpan<float> q = FullSpan.Slice(quaternionIndex * 4, 4);
        ReadOnlySpan<float> v = source.FullSpan.Slice(sourceIndex * 3, 3);
        Span<float> d = destination.FullSpan.Slice(destinationIndex * 3, 3);

        float qx = q[0], qy = q[1], qz = q[2], qw = q[3];
        float vx = v[0], vy = v[1], vz = v[2];
        float tx = 2f * ((qy * vz) - (qz * vy));
        float ty = 2f * ((qz * vx) - (qx * vz));
        float tz = 2f * ((qx * vy) - (qy * vx));
        d[0] = vx + (qw * tx) + (qy * tz) - (qz * ty);
        d[1] = vy + (qw * ty) + (qz * tx) - (qx * tz);
        d[2] = vz + (qw * tz) + (qx * ty) - (qy * tx);
    }

    /// <summary>Accumulates a weighted quaternion into a blend accumulator.</summary>
    public void AccumulateWeighted(
        int accumulatorIndex,
        SoaQuatBuffer reference,
        int referenceIndex,
        SoaQuatBuffer source,
        int sourceIndex,
        float weight,
        bool first)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(source);
        ValidateIndex(accumulatorIndex);
        reference.ValidateIndex(referenceIndex);
        source.ValidateIndex(sourceIndex);

        Span<float> acc = FullSpan.Slice(accumulatorIndex * 4, 4);
        ReadOnlySpan<float> referenceValue = reference.FullSpan.Slice(referenceIndex * 4, 4);
        ReadOnlySpan<float> value = source.FullSpan.Slice(sourceIndex * 4, 4);
        float sign = !first && SoaQuatBuffer.Dot(reference, referenceIndex, source, sourceIndex) < 0f ? -1f : 1f;

        if (first)
        {
            acc.Clear();
        }

        acc[0] += value[0] * weight * sign;
        acc[1] += value[1] * weight * sign;
        acc[2] += value[2] * weight * sign;
        acc[3] += value[3] * weight * sign;
    }

    /// <summary>Normalizes a weighted accumulator into a final quaternion.</summary>
    public void FinalizeWeighted(int destination, int accumulatorIndex, float totalWeight)
    {
        ValidateIndex(destination);
        ValidateIndex(accumulatorIndex);
        Span<float> accumulator = FullSpan.Slice(accumulatorIndex * 4, 4);
        if (totalWeight <= 0f)
        {
            SetIdentity(destination);
            return;
        }

        float inverse = 1f / totalWeight;
        accumulator[0] *= inverse;
        accumulator[1] *= inverse;
        accumulator[2] *= inverse;
        accumulator[3] *= inverse;
        Normalize(destination, this, accumulatorIndex);
    }

    /// <summary>Copies one quaternion.</summary>
    public void CopyFrom(int index, SoaQuatBuffer source, int sourceIndex)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateIndex(index);
        source.ValidateIndex(sourceIndex);
        source.FullSpan.Slice(sourceIndex * 4, 4).CopyTo(FullSpan.Slice(index * 4, 4));
    }

    /// <summary>Spherically interpolates every element of three buffers.</summary>
    public void SlerpAll(SoaQuatBuffer left, SoaQuatBuffer right, float t)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        int count = Math.Min(Count, Math.Min(left.Count, right.Count));
        for (var index = 0; index < count; index++)
        {
            Slerp(index, left, index, right, index, t);
        }
    }

    /// <summary>Copies every element from another quaternion buffer.</summary>
    public void CopyAllFrom(SoaQuatBuffer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        int count = Math.Min(Count, source.Count);
        source.FullSpan[..(count * 4)].CopyTo(FullSpan);
    }
}
