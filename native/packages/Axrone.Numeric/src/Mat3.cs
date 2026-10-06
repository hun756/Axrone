namespace Axrone.Numeric;

public interface IReadOnlyMatrix3x3<TSelf>
    where TSelf : struct, IReadOnlyMatrix3x3<TSelf>
{
    #pragma warning disable CA1043
    float this[RowIndex row, ColumnIndex column] { get; }
    #pragma warning restore CA1043
    Vec3 Row0 { get; }
    Vec3 Row1 { get; }
    Vec3 Row2 { get; }
    Vec3 Column0 { get; }
    Vec3 Column1 { get; }
    Vec3 Column2 { get; }
    float Determinant();
    TSelf Transpose();
    bool Invert(out TSelf result);
    bool IsIdentity { get; }
    bool IsAllZero { get; }
    bool IsAllFinite { get; }
}

public interface IAffineTransformable3x3<TSelf>
    where TSelf : struct, IAffineTransformable3x3<TSelf>
{
    static abstract TSelf CreateScale(Vec3 scales);
    static abstract TSelf CreateFromQuaternion(Quat rotation);
    static abstract TSelf CreateFromAxisAngle(UnitAxis3 axis, AngleRadians angle);
}

public interface IMatrixStorage3x3<TSelf>
    where TSelf : struct, IMatrixStorage3x3<TSelf>
{
    ReadOnlySpan<float> AsSpan();
    void CopyTo(Span<float> destination);
    bool TryCopyTo(Span<float> destination);
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Mat3 :
    IEquatable<Mat3>,
    IEqualityOperators<Mat3, Mat3, bool>,
    IReadOnlyMatrix3x3<Mat3>,
    IAffineTransformable3x3<Mat3>,
    IMatrixStorage3x3<Mat3>
{
    public const float MachineEpsilon = 1.1920929E-07F;

    public const float DefaultTolerance = MachineEpsilon * 8F;

    public float M11;
    public float M12;
    public float M13;
    public float M21;
    public float M22;
    public float M23;
    public float M31;
    public float M32;
    public float M33;

    public static Mat3 Identity => new(
        1.0f, 0.0f, 0.0f,
        0.0f, 1.0f, 0.0f,
        0.0f, 0.0f, 1.0f
    );

    public static Mat3 Zero => default;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Mat3(
        float m11, float m12, float m13,
        float m21, float m22, float m23,
        float m31, float m32, float m33)
    {
        M11 = m11; M12 = m12; M13 = m13;
        M21 = m21; M22 = m22; M23 = m23;
        M31 = m31; M32 = m32; M33 = m33;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Mat3(Vec3 row0, Vec3 row1, Vec3 row2)
    {
        M11 = row0.X; M12 = row0.Y; M13 = row0.Z;
        M21 = row1.X; M22 = row1.Y; M23 = row1.Z;
        M31 = row2.X; M32 = row2.Y; M33 = row2.Z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Mat3(ReadOnlySpan<float> values)
    {
        if (values.Length < 9)
        {
            NumericThrowHelper.ThrowArgumentException(nameof(values), "Source span must contain at least 9 elements.");
        }
        ref float src = ref MemoryMarshal.GetReference(values);
        this = Unsafe.As<float, Mat3>(ref src);
    }

    #pragma warning disable CA1043
    public float this[RowIndex row, ColumnIndex column]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get
        {
            if (row.Value >= 3U || column.Value >= 3U)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(row));
            }
            return Unsafe.Add(ref Unsafe.AsRef(in M11), (nuint)(row.Value * 3 + column.Value));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set
        {
            if (row.Value >= 3U || column.Value >= 3U)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(row));
            }
            Unsafe.Add(ref M11, (nuint)(row.Value * 3 + column.Value)) = value;
        }
    }
    #pragma warning restore CA1043

    public float this[int row, int column]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get
        {
            if ((uint)row >= 3 || (uint)column >= 3)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(row));
            }
            return Unsafe.Add(ref Unsafe.AsRef(in M11), (nuint)((uint)row * 3 + (uint)column));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set
        {
            if ((uint)row >= 3 || (uint)column >= 3)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(row));
            }
            Unsafe.Add(ref M11, (nuint)((uint)row * 3 + (uint)column)) = value;
        }
    }

    public float this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get
        {
            if ((uint)index >= 9U)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
            }
            return Unsafe.Add(ref Unsafe.AsRef(in M11), (nuint)(uint)index);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set
        {
            if ((uint)index >= 9U)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
            }
            Unsafe.Add(ref M11, (nuint)(uint)index) = value;
        }
    }

    public Vec3 Row0
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get => new(M11, M12, M13);
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set { M11 = value.X; M12 = value.Y; M13 = value.Z; }
    }

    public Vec3 Row1
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get => new(M21, M22, M23);
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set { M21 = value.X; M22 = value.Y; M23 = value.Z; }
    }

    public Vec3 Row2
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get => new(M31, M32, M33);
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set { M31 = value.X; M32 = value.Y; M33 = value.Z; }
    }

    public Vec3 Column0
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get => new(M11, M21, M31);
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set { M11 = value.X; M21 = value.Y; M31 = value.Z; }
    }

    public Vec3 Column1
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get => new(M12, M22, M32);
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set { M12 = value.X; M22 = value.Y; M32 = value.Z; }
    }

    public Vec3 Column2
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get => new(M13, M23, M33);
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set { M13 = value.X; M23 = value.Y; M33 = value.Z; }
    }

    public readonly bool IsIdentity => M11 == 1.0f && M22 == 1.0f && M33 == 1.0f &&
        M12 == 0.0f && M13 == 0.0f &&
        M21 == 0.0f && M23 == 0.0f &&
        M31 == 0.0f && M32 == 0.0f;

    public readonly bool IsAllZero =>
        M11 == 0.0f && M12 == 0.0f && M13 == 0.0f &&
        M21 == 0.0f && M22 == 0.0f && M23 == 0.0f &&
        M31 == 0.0f && M32 == 0.0f && M33 == 0.0f;

    public readonly bool IsAllFinite =>
        float.IsFinite(M11) && float.IsFinite(M12) && float.IsFinite(M13) &&
        float.IsFinite(M21) && float.IsFinite(M22) && float.IsFinite(M23) &&
        float.IsFinite(M31) && float.IsFinite(M32) && float.IsFinite(M33);

    public readonly bool IsAnyNaN =>
        float.IsNaN(M11) || float.IsNaN(M12) || float.IsNaN(M13) ||
        float.IsNaN(M21) || float.IsNaN(M22) || float.IsNaN(M23) ||
        float.IsNaN(M31) || float.IsNaN(M32) || float.IsNaN(M33);

    public readonly bool IsAnyInfinity =>
        float.IsInfinity(M11) || float.IsInfinity(M12) || float.IsInfinity(M13) ||
        float.IsInfinity(M21) || float.IsInfinity(M22) || float.IsInfinity(M23) ||
        float.IsInfinity(M31) || float.IsInfinity(M32) || float.IsInfinity(M33);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly ReadOnlySpan<float> AsSpan() =>
        MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in M11), 9);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Span<float> AsWritableSpan() =>
        MemoryMarshal.CreateSpan(ref M11, 9);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly void CopyTo(Span<float> destination)
    {
        if (destination.Length < 9)
        {
            NumericThrowHelper.ThrowArgumentException(nameof(destination), "Destination span must contain at least 9 elements.");
        }
        ref float dst = ref MemoryMarshal.GetReference(destination);
        Unsafe.As<float, Mat3>(ref dst) = this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool TryCopyTo(Span<float> destination)
    {
        if (destination.Length >= 9)
        {
            ref float dst = ref MemoryMarshal.GetReference(destination);
            Unsafe.As<float, Mat3>(ref dst) = this;
            return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly float Trace() => M11 + M22 + M33;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly Mat3 Transpose() =>
        new(
            M11, M21, M31,
            M12, M22, M32,
            M13, M23, M33
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 Transpose(Mat3 matrix) => matrix.Transpose();

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly float Determinant() =>
        (M11 * ((M22 * M33) - (M23 * M32))) -
        (M12 * ((M21 * M33) - (M23 * M31))) +
        (M13 * ((M21 * M32) - (M22 * M31)));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool Invert(out Mat3 result)
    {
        float a11 = (M22 * M33) - (M23 * M32);
        float a12 = (M23 * M31) - (M21 * M33);
        float a13 = (M21 * M32) - (M22 * M31);

        float det = (M11 * a11) + (M12 * a12) + (M13 * a13);

        if (MathF.Abs(det) <= 1e-30f)
        {
            result = default;
            return false;
        }

        float invDet = 1.0f / det;

        result = new Mat3(
            a11 * invDet,
            ((M13 * M32) - (M12 * M33)) * invDet,
            ((M12 * M23) - (M13 * M22)) * invDet,
            a12 * invDet,
            ((M11 * M33) - (M13 * M31)) * invDet,
            ((M13 * M21) - (M11 * M23)) * invDet,
            a13 * invDet,
            ((M12 * M31) - (M11 * M32)) * invDet,
            ((M11 * M22) - (M12 * M21)) * invDet
        );

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool Invert(Mat3 matrix, out Mat3 result) => matrix.Invert(out result);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 CreateScale(Vec3 scales) =>
        new(
            scales.X, 0.0f, 0.0f,
            0.0f, scales.Y, 0.0f,
            0.0f, 0.0f, scales.Z
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 CreateScale(float scale) => CreateScale(new Vec3(scale));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 CreateScale(float xScale, float yScale, float zScale) =>
        CreateScale(new Vec3(xScale, yScale, zScale));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 CreateFromAxisAngle(UnitAxis3 axis, AngleRadians angle)
    {
        float x = axis.X, y = axis.Y, z = axis.Z;
        (float sa, float ca) = MathF.SinCos(angle.Value);
        float xx = x * x, yy = y * y, zz = z * z;
        float xy = x * y, xz = x * z, yz = y * z;

        return new Mat3(
            xx + (ca * (1.0f - xx)),
            (xy - (ca * xy)) + (sa * z),
            (xz - (ca * xz)) - (sa * y),
            (xy - (ca * xy)) - (sa * z),
            yy + (ca * (1.0f - yy)),
            (yz - (ca * yz)) + (sa * x),
            (xz - (ca * xz)) + (sa * y),
            (yz - (ca * yz)) - (sa * x),
            zz + (ca * (1.0f - zz))
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 CreateFromAxisAngle(Vec3 axis, float angleRadians) =>
        CreateFromAxisAngle(UnitAxis3.Create(axis), new AngleRadians(angleRadians));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 CreateFromQuaternion(Quat rotation)
    {
        float xx = rotation.X * rotation.X;
        float yy = rotation.Y * rotation.Y;
        float zz = rotation.Z * rotation.Z;
        float xy = rotation.X * rotation.Y;
        float wz = rotation.Z * rotation.W;
        float xz = rotation.Z * rotation.X;
        float wy = rotation.Y * rotation.W;
        float yz = rotation.Y * rotation.Z;
        float wx = rotation.X * rotation.W;

        return new Mat3(
            1.0f - (2.0f * (yy + zz)), 2.0f * (xy + wz), 2.0f * (xz - wy),
            2.0f * (xy - wz), 1.0f - (2.0f * (zz + xx)), 2.0f * (yz + wx),
            2.0f * (xz + wy), 2.0f * (yz - wx), 1.0f - (2.0f * (yy + xx))
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator ==(Mat3 left, Mat3 right) =>
        left.M11 == right.M11 && left.M12 == right.M12 && left.M13 == right.M13 &&
        left.M21 == right.M21 && left.M22 == right.M22 && left.M23 == right.M23 &&
        left.M31 == right.M31 && left.M32 == right.M32 && left.M33 == right.M33;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator !=(Mat3 left, Mat3 right) => !(left == right);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool Equals(Mat3 other) => this == other;

    public override readonly bool Equals(object? obj) =>
        obj is Mat3 other && Equals(other);

    public override readonly int GetHashCode()
    {
        HashCode hash = default;
        ref float self = ref Unsafe.AsRef(in M11);
        for (nuint i = 0; i < 9; i++)
        {
            hash.Add(Unsafe.Add(ref self, i));
        }
        return hash.ToHashCode();
    }
}
