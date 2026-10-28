namespace Axrone.Simd;

/// <summary>Structure-of-arrays buffer for three-float vectors.</summary>
public sealed class SoaVec3Buffer : SoaBuffer
{
    /// <summary>Epsilon used when normalizing degenerate vectors.</summary>
    public const float Epsilon = 1e-6f;

    /// <summary>Creates a buffer.</summary>
    /// <param name="capacity">Initial element capacity.</param>
    public SoaVec3Buffer(int capacity)
        : base(capacity, 3)
    {
    }

    /// <summary>Writes one vector.</summary>
    public void Set(int index, float x, float y, float z)
    {
        ValidateIndex(index);
        int offset = index * 3;
        Span<float> data = FullSpan;
        data[offset] = x;
        data[offset + 1] = y;
        data[offset + 2] = z;
    }

    /// <summary>Reads one vector.</summary>
    public (float X, float Y, float Z) Get(int index)
    {
        ValidateIndex(index);
        int offset = index * 3;
        ReadOnlySpan<float> data = FullSpan;
        return (data[offset], data[offset + 1], data[offset + 2]);
    }

    /// <summary>Gets one vector as a writable slice.</summary>
    public Span<float> Slice(int index)
    {
        ValidateIndex(index);
        return FullSpan.Slice(index * 3, 3);
    }

    /// <summary>Copies one vector.</summary>
    public void CopyFrom(int index, SoaVec3Buffer source, int sourceIndex)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateIndex(index);
        source.ValidateIndex(sourceIndex);
        source.FullSpan.Slice(sourceIndex * 3, 3).CopyTo(FullSpan.Slice(index * 3, 3));
    }

    /// <summary>Adds two vectors.</summary>
    public void Add(int destination, SoaVec3Buffer left, int leftIndex, SoaVec3Buffer right, int rightIndex)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        ValidateIndex(destination);
        left.ValidateIndex(leftIndex);
        right.ValidateIndex(rightIndex);

        ReadOnlySpan<float> l = left.FullSpan.Slice(leftIndex * 3, 3);
        ReadOnlySpan<float> r = right.FullSpan.Slice(rightIndex * 3, 3);
        Span<float> d = FullSpan.Slice(destination * 3, 3);
        d[0] = l[0] + r[0];
        d[1] = l[1] + r[1];
        d[2] = l[2] + r[2];
    }

    /// <summary>Subtracts two vectors.</summary>
    public void Subtract(int destination, SoaVec3Buffer left, int leftIndex, SoaVec3Buffer right, int rightIndex)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        ValidateIndex(destination);
        left.ValidateIndex(leftIndex);
        right.ValidateIndex(rightIndex);

        ReadOnlySpan<float> l = left.FullSpan.Slice(leftIndex * 3, 3);
        ReadOnlySpan<float> r = right.FullSpan.Slice(rightIndex * 3, 3);
        Span<float> d = FullSpan.Slice(destination * 3, 3);
        d[0] = l[0] - r[0];
        d[1] = l[1] - r[1];
        d[2] = l[2] - r[2];
    }

    /// <summary>Multiplies two vectors component-wise.</summary>
    public void Multiply(int destination, SoaVec3Buffer left, int leftIndex, SoaVec3Buffer right, int rightIndex)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        ValidateIndex(destination);
        left.ValidateIndex(leftIndex);
        right.ValidateIndex(rightIndex);

        ReadOnlySpan<float> l = left.FullSpan.Slice(leftIndex * 3, 3);
        ReadOnlySpan<float> r = right.FullSpan.Slice(rightIndex * 3, 3);
        Span<float> d = FullSpan.Slice(destination * 3, 3);
        d[0] = l[0] * r[0];
        d[1] = l[1] * r[1];
        d[2] = l[2] * r[2];
    }

    /// <summary>Scales one vector.</summary>
    public void Scale(int destination, SoaVec3Buffer source, int sourceIndex, float scalar)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateIndex(destination);
        source.ValidateIndex(sourceIndex);

        ReadOnlySpan<float> s = source.FullSpan.Slice(sourceIndex * 3, 3);
        Span<float> d = FullSpan.Slice(destination * 3, 3);
        d[0] = s[0] * scalar;
        d[1] = s[1] * scalar;
        d[2] = s[2] * scalar;
    }

    /// <summary>Linearly interpolates two vectors.</summary>
    public void Lerp(int destination, SoaVec3Buffer left, int leftIndex, SoaVec3Buffer right, int rightIndex, float t)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        ValidateIndex(destination);
        left.ValidateIndex(leftIndex);
        right.ValidateIndex(rightIndex);

        float inverse = 1f - t;
        ReadOnlySpan<float> l = left.FullSpan.Slice(leftIndex * 3, 3);
        ReadOnlySpan<float> r = right.FullSpan.Slice(rightIndex * 3, 3);
        Span<float> d = FullSpan.Slice(destination * 3, 3);
        d[0] = (l[0] * inverse) + (r[0] * t);
        d[1] = (l[1] * inverse) + (r[1] * t);
        d[2] = (l[2] * inverse) + (r[2] * t);
    }

    /// <summary>Computes a dot product.</summary>
    public static float Dot(SoaVec3Buffer left, int leftIndex, SoaVec3Buffer right, int rightIndex)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        left.ValidateIndex(leftIndex);
        right.ValidateIndex(rightIndex);

        ReadOnlySpan<float> l = left.FullSpan.Slice(leftIndex * 3, 3);
        ReadOnlySpan<float> r = right.FullSpan.Slice(rightIndex * 3, 3);
        return (l[0] * r[0]) + (l[1] * r[1]) + (l[2] * r[2]);
    }

    /// <summary>Computes a cross product.</summary>
    public void Cross(int destination, SoaVec3Buffer left, int leftIndex, SoaVec3Buffer right, int rightIndex)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        ValidateIndex(destination);
        left.ValidateIndex(leftIndex);
        right.ValidateIndex(rightIndex);

        ReadOnlySpan<float> l = left.FullSpan.Slice(leftIndex * 3, 3);
        ReadOnlySpan<float> r = right.FullSpan.Slice(rightIndex * 3, 3);
        Span<float> d = FullSpan.Slice(destination * 3, 3);
        float lx = l[0], ly = l[1], lz = l[2];
        float rx = r[0], ry = r[1], rz = r[2];
        d[0] = (ly * rz) - (lz * ry);
        d[1] = (lz * rx) - (lx * rz);
        d[2] = (lx * ry) - (ly * rx);
    }

    /// <summary>Normalizes one vector; degenerate input becomes zero.</summary>
    public void Normalize(int destination, SoaVec3Buffer source, int sourceIndex)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateIndex(destination);
        source.ValidateIndex(sourceIndex);

        ReadOnlySpan<float> s = source.FullSpan.Slice(sourceIndex * 3, 3);
        Span<float> d = FullSpan.Slice(destination * 3, 3);
        float lengthSquared = (s[0] * s[0]) + (s[1] * s[1]) + (s[2] * s[2]);
        if (lengthSquared <= Epsilon)
        {
            d.Clear();
            return;
        }

        float inverse = 1f / MathF.Sqrt(lengthSquared);
        d[0] = s[0] * inverse;
        d[1] = s[1] * inverse;
        d[2] = s[2] * inverse;
    }

    /// <summary>Computes a vector length.</summary>
    public float Length(int index) => MathF.Sqrt(LengthSquared(index));

    /// <summary>Computes a squared vector length.</summary>
    public float LengthSquared(int index)
    {
        ValidateIndex(index);
        ReadOnlySpan<float> s = FullSpan.Slice(index * 3, 3);
        return (s[0] * s[0]) + (s[1] * s[1]) + (s[2] * s[2]);
    }

    /// <summary>Interpolates every element of three buffers.</summary>
    public void LerpAll(SoaVec3Buffer left, SoaVec3Buffer right, float t)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        int count = Math.Min(Count, Math.Min(left.Count, right.Count));
        ReadOnlySpan<float> l = left.FullSpan[..(count * 3)];
        ReadOnlySpan<float> r = right.FullSpan[..(count * 3)];
        Span<float> d = FullSpan[..(count * 3)];
        float inverse = 1f - t;

        for (var index = 0; index < d.Length; index++)
        {
            d[index] = (l[index] * inverse) + (r[index] * t);
        }
    }

    /// <summary>Copies every element from another vector buffer.</summary>
    public void CopyAllFrom(SoaVec3Buffer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        int count = Math.Min(Count, source.Count);
        source.FullSpan[..(count * 3)].CopyTo(FullSpan);
    }
}
