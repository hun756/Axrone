namespace Axrone.Numeric;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct RowIndex : IEquatable<RowIndex>, IComparable<RowIndex>
{
    public readonly byte Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private RowIndex(byte value) => Value = value;

    public static RowIndex R0 => new(0);

    public static RowIndex R1 => new(1);

    public static RowIndex R2 => new(2);

    public static RowIndex R3 => new(3);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static RowIndex From(int index)
    {
        if ((uint)index >= 4U)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
        }

        return new RowIndex((byte)index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator RowIndex(int index) => From(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator int(RowIndex index) => index.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator nuint(RowIndex index) => index.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(RowIndex other) => Value.CompareTo(other.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <(RowIndex left, RowIndex right) => left.Value < right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >(RowIndex left, RowIndex right) => left.Value > right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <=(RowIndex left, RowIndex right) => left.Value <= right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >=(RowIndex left, RowIndex right) => left.Value >= right.Value;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct ColumnIndex : IEquatable<ColumnIndex>, IComparable<ColumnIndex>
{
    public readonly byte Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ColumnIndex(byte value) => Value = value;

    public static ColumnIndex C0 => new(0);

    public static ColumnIndex C1 => new(1);

    public static ColumnIndex C2 => new(2);

    public static ColumnIndex C3 => new(3);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ColumnIndex From(int index)
    {
        if ((uint)index >= 4U)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
        }

        return new ColumnIndex((byte)index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator ColumnIndex(int index) => From(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator int(ColumnIndex index) => index.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator nuint(ColumnIndex index) => index.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(ColumnIndex other) => Value.CompareTo(other.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <(ColumnIndex left, ColumnIndex right) => left.Value < right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >(ColumnIndex left, ColumnIndex right) => left.Value > right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <=(ColumnIndex left, ColumnIndex right) => left.Value <= right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >=(ColumnIndex left, ColumnIndex right) => left.Value >= right.Value;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct MatrixIndex : IEquatable<MatrixIndex>, IComparable<MatrixIndex>
{
    public readonly byte Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private MatrixIndex(byte value) => Value = value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static MatrixIndex From(int index)
    {
        if ((uint)index >= 16U)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
        }

        return new MatrixIndex((byte)index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static MatrixIndex From(RowIndex row, ColumnIndex column) =>
        new((byte)((row.Value << 2) | column.Value));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator MatrixIndex(int index) => From(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator int(MatrixIndex index) => index.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator nuint(MatrixIndex index) => index.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Deconstruct(out RowIndex row, out ColumnIndex column)
    {
        row = RowIndex.From(Value >> 2);
        column = ColumnIndex.From(Value & 3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(MatrixIndex other) => Value.CompareTo(other.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <(MatrixIndex left, MatrixIndex right) => left.Value < right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >(MatrixIndex left, MatrixIndex right) => left.Value > right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <=(MatrixIndex left, MatrixIndex right) => left.Value <= right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >=(MatrixIndex left, MatrixIndex right) => left.Value >= right.Value;
}

public interface IReadOnlyMatrix4x4<TSelf>
    where TSelf : struct, IReadOnlyMatrix4x4<TSelf>
{
    #pragma warning disable CA1043 // Nominal indices are the whole point: a 0..3 / 0..15 proof checked at construction.
    float this[RowIndex row, ColumnIndex column] { get; }
    float this[MatrixIndex index] { get; }
    #pragma warning restore CA1043
    Vec4 Row0 { get; }
    Vec4 Row1 { get; }
    Vec4 Row2 { get; }
    Vec4 Row3 { get; }
    Vec4 Column0 { get; }
    Vec4 Column1 { get; }
    Vec4 Column2 { get; }
    Vec4 Column3 { get; }
    float Determinant();
    TSelf Transpose();
    bool Invert(out TSelf result);
    bool IsIdentity { get; }
    bool IsAllZero { get; }
    bool IsAllFinite { get; }
}

public interface IAffineTransformable4x4<TSelf>
    where TSelf : struct, IAffineTransformable4x4<TSelf>
{
    static abstract TSelf CreateTranslation(Vec3 position);
    static abstract TSelf CreateScale(Vec3 scales);
    static abstract TSelf CreateFromQuaternion(Quat rotation);
    static abstract TSelf CreateFromAxisAngle(UnitAxis3 axis, AngleRadians angle);
}

public interface IMatrixStorage4x4<TSelf>
    where TSelf : struct, IMatrixStorage4x4<TSelf>
{
    ReadOnlySpan<float> AsSpan();
    void CopyTo(Span<float> destination);
    bool TryCopyTo(Span<float> destination);
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Mat4 :
    IMatrixStorage4x4<Mat4>
{
    public const float MachineEpsilon = 1.1920929E-07F;

    public const float DefaultTolerance = MachineEpsilon * 8F;

    public float M11;
    public float M12;
    public float M13;
    public float M14;
    public float M21;
    public float M22;
    public float M23;
    public float M24;
    public float M31;
    public float M32;
    public float M33;
    public float M34;
    public float M41;
    public float M42;
    public float M43;
    public float M44;

    public static Mat4 Identity => new(
        1.0f, 0.0f, 0.0f, 0.0f,
        0.0f, 1.0f, 0.0f, 0.0f,
        0.0f, 0.0f, 1.0f, 0.0f,
        0.0f, 0.0f, 0.0f, 1.0f
    );

    public static Mat4 Zero => default;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Mat4(
        float m11, float m12, float m13, float m14,
        float m21, float m22, float m23, float m24,
        float m31, float m32, float m33, float m34,
        float m41, float m42, float m43, float m44)
    {
        M11 = m11; M12 = m12; M13 = m13; M14 = m14;
        M21 = m21; M22 = m22; M23 = m23; M24 = m24;
        M31 = m31; M32 = m32; M33 = m33; M34 = m34;
        M41 = m41; M42 = m42; M43 = m43; M44 = m44;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Mat4(Vec4 row0, Vec4 row1, Vec4 row2, Vec4 row3)
    {
        M11 = row0.X; M12 = row0.Y; M13 = row0.Z; M14 = row0.W;
        M21 = row1.X; M22 = row1.Y; M23 = row1.Z; M24 = row1.W;
        M31 = row2.X; M32 = row2.Y; M33 = row2.Z; M34 = row2.W;
        M41 = row3.X; M42 = row3.Y; M43 = row3.Z; M44 = row3.W;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Mat4(ReadOnlySpan<float> values)
    {
        if (values.Length < 16)
        {
            NumericThrowHelper.ThrowArgumentException(nameof(values), "Source span must contain at least 16 elements.");
        }
        ref float src = ref MemoryMarshal.GetReference(values);
        this = Unsafe.As<float, Mat4>(ref src);
    }

    #pragma warning disable CA1043 // Nominal indices are the whole point: a 0..3 / 0..15 proof checked at construction.
    public float this[RowIndex row, ColumnIndex column]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get => Unsafe.Add(ref Unsafe.AsRef(in M11), (nuint)((row.Value << 2) | column.Value));
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set => Unsafe.Add(ref M11, (nuint)((row.Value << 2) | column.Value)) = value;
    }

    public float this[MatrixIndex index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get => Unsafe.Add(ref Unsafe.AsRef(in M11), (nuint)index.Value);
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set => Unsafe.Add(ref M11, (nuint)index.Value) = value;
    }
    #pragma warning restore CA1043

    public float this[int row, int column]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get
        {
            if ((uint)row >= 4 || (uint)column >= 4)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(row));
            }
            return Unsafe.Add(ref Unsafe.AsRef(in M11), (nuint)((uint)row * 4 + (uint)column));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set
        {
            if ((uint)row >= 4 || (uint)column >= 4)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(row));
            }
            Unsafe.Add(ref M11, (nuint)((uint)row * 4 + (uint)column)) = value;
        }
    }

    public float this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get
        {
            if ((uint)index >= 16U)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
            }
            return Unsafe.Add(ref Unsafe.AsRef(in M11), (nuint)(uint)index);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set
        {
            if ((uint)index >= 16U)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
            }
            Unsafe.Add(ref M11, (nuint)(uint)index) = value;
        }
    }

    public Vec4 Row0
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get => new(M11, M12, M13, M14);
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set { M11 = value.X; M12 = value.Y; M13 = value.Z; M14 = value.W; }
    }

    public Vec4 Row1
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get => new(M21, M22, M23, M24);
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set { M21 = value.X; M22 = value.Y; M23 = value.Z; M24 = value.W; }
    }

    public Vec4 Row2
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get => new(M31, M32, M33, M34);
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set { M31 = value.X; M32 = value.Y; M33 = value.Z; M34 = value.W; }
    }

    public Vec4 Row3
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get => new(M41, M42, M43, M44);
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set { M41 = value.X; M42 = value.Y; M43 = value.Z; M44 = value.W; }
    }

    public Vec4 Column0
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get => new(M11, M21, M31, M41);
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set { M11 = value.X; M21 = value.Y; M31 = value.Z; M41 = value.W; }
    }

    public Vec4 Column1
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get => new(M12, M22, M32, M42);
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set { M12 = value.X; M22 = value.Y; M32 = value.Z; M42 = value.W; }
    }

    public Vec4 Column2
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get => new(M13, M23, M33, M43);
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set { M13 = value.X; M23 = value.Y; M33 = value.Z; M43 = value.W; }
    }

    public Vec4 Column3
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get => new(M14, M24, M34, M44);
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set { M14 = value.X; M24 = value.Y; M34 = value.Z; M44 = value.W; }
    }

    public Vec3 Translation
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get => new(M41, M42, M43);
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set { M41 = value.X; M42 = value.Y; M43 = value.Z; }
    }

    public readonly bool IsIdentity => M11 == 1.0f && M22 == 1.0f && M33 == 1.0f && M44 == 1.0f &&
        M12 == 0.0f && M13 == 0.0f && M14 == 0.0f &&
        M21 == 0.0f && M23 == 0.0f && M24 == 0.0f &&
        M31 == 0.0f && M32 == 0.0f && M34 == 0.0f &&
        M41 == 0.0f && M42 == 0.0f && M43 == 0.0f;

    public readonly bool IsAllZero =>
        M11 == 0.0f && M12 == 0.0f && M13 == 0.0f && M14 == 0.0f &&
        M21 == 0.0f && M22 == 0.0f && M23 == 0.0f && M24 == 0.0f &&
        M31 == 0.0f && M32 == 0.0f && M33 == 0.0f && M34 == 0.0f &&
        M41 == 0.0f && M42 == 0.0f && M43 == 0.0f && M44 == 0.0f;

    public readonly bool IsAllFinite =>
        float.IsFinite(M11) && float.IsFinite(M12) && float.IsFinite(M13) && float.IsFinite(M14) &&
        float.IsFinite(M21) && float.IsFinite(M22) && float.IsFinite(M23) && float.IsFinite(M24) &&
        float.IsFinite(M31) && float.IsFinite(M32) && float.IsFinite(M33) && float.IsFinite(M34) &&
        float.IsFinite(M41) && float.IsFinite(M42) && float.IsFinite(M43) && float.IsFinite(M44);

    public readonly bool IsAnyNaN =>
        float.IsNaN(M11) || float.IsNaN(M12) || float.IsNaN(M13) || float.IsNaN(M14) ||
        float.IsNaN(M21) || float.IsNaN(M22) || float.IsNaN(M23) || float.IsNaN(M24) ||
        float.IsNaN(M31) || float.IsNaN(M32) || float.IsNaN(M33) || float.IsNaN(M34) ||
        float.IsNaN(M41) || float.IsNaN(M42) || float.IsNaN(M43) || float.IsNaN(M44);

    public readonly bool IsAnyInfinity =>
        float.IsInfinity(M11) || float.IsInfinity(M12) || float.IsInfinity(M13) || float.IsInfinity(M14) ||
        float.IsInfinity(M21) || float.IsInfinity(M22) || float.IsInfinity(M23) || float.IsInfinity(M24) ||
        float.IsInfinity(M31) || float.IsInfinity(M32) || float.IsInfinity(M33) || float.IsInfinity(M34) ||
        float.IsInfinity(M41) || float.IsInfinity(M42) || float.IsInfinity(M43) || float.IsInfinity(M44);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly ReadOnlySpan<float> AsSpan() =>
        MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in M11), 16);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Span<float> AsWritableSpan() =>
        MemoryMarshal.CreateSpan(ref M11, 16);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly void CopyTo(Span<float> destination)
    {
        if (destination.Length < 16)
        {
            NumericThrowHelper.ThrowArgumentException(nameof(destination), "Destination span must contain at least 16 elements.");
        }
        ref float dst = ref MemoryMarshal.GetReference(destination);
        Unsafe.As<float, Mat4>(ref dst) = this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool TryCopyTo(Span<float> destination)
    {
        if (destination.Length >= 16)
        {
            ref float dst = ref MemoryMarshal.GetReference(destination);
            Unsafe.As<float, Mat4>(ref dst) = this;
            return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static explicit operator Matrix4x4(Mat4 m) =>
        Unsafe.BitCast<Mat4, Matrix4x4>(m);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static explicit operator Mat4(Matrix4x4 m) =>
        Unsafe.BitCast<Matrix4x4, Mat4>(m);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly Matrix4x4 ToSystemNumerics() => (Matrix4x4)this;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 FromSystemNumerics(Matrix4x4 value) => (Mat4)value;

    // __NEXT__
}
