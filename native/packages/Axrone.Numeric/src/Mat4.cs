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
    IEquatable<Mat4>,
    IEqualityOperators<Mat4, Mat4, bool>,
    IAdditionOperators<Mat4, Mat4, Mat4>,
    ISubtractionOperators<Mat4, Mat4, Mat4>,
    IMultiplyOperators<Mat4, Mat4, Mat4>,
    IMultiplyOperators<Mat4, float, Mat4>,
    IUnaryNegationOperators<Mat4, Mat4>,
    IUnaryPlusOperators<Mat4, Mat4>,
    IAdditiveIdentity<Mat4, Mat4>,
    IMultiplicativeIdentity<Mat4, Mat4>,
    IReadOnlyMatrix4x4<Mat4>,
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

    public static Mat4 AdditiveIdentity => Zero;
    public static Mat4 MultiplicativeIdentity => Identity;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 operator +(Mat4 left, Mat4 right)
    {
        Unsafe.SkipInit(out Mat4 result);
        ref float l = ref Unsafe.AsRef(in left.M11);
        ref float r = ref Unsafe.AsRef(in right.M11);
        ref float dst = ref result.M11;

        if (Vector256.IsHardwareAccelerated)
        {
            (Vector256.LoadUnsafe(ref l, 0) + Vector256.LoadUnsafe(ref r, 0)).StoreUnsafe(ref dst, 0);
            (Vector256.LoadUnsafe(ref l, 8) + Vector256.LoadUnsafe(ref r, 8)).StoreUnsafe(ref dst, 8);
            return result;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            (Vector128.LoadUnsafe(ref l, 0) + Vector128.LoadUnsafe(ref r, 0)).StoreUnsafe(ref dst, 0);
            (Vector128.LoadUnsafe(ref l, 4) + Vector128.LoadUnsafe(ref r, 4)).StoreUnsafe(ref dst, 4);
            (Vector128.LoadUnsafe(ref l, 8) + Vector128.LoadUnsafe(ref r, 8)).StoreUnsafe(ref dst, 8);
            (Vector128.LoadUnsafe(ref l, 12) + Vector128.LoadUnsafe(ref r, 12)).StoreUnsafe(ref dst, 12);
            return result;
        }

        return new Mat4(
            left.M11 + right.M11, left.M12 + right.M12, left.M13 + right.M13, left.M14 + right.M14,
            left.M21 + right.M21, left.M22 + right.M22, left.M23 + right.M23, left.M24 + right.M24,
            left.M31 + right.M31, left.M32 + right.M32, left.M33 + right.M33, left.M34 + right.M34,
            left.M41 + right.M41, left.M42 + right.M42, left.M43 + right.M43, left.M44 + right.M44
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 operator -(Mat4 left, Mat4 right)
    {
        Unsafe.SkipInit(out Mat4 result);
        ref float l = ref Unsafe.AsRef(in left.M11);
        ref float r = ref Unsafe.AsRef(in right.M11);
        ref float dst = ref result.M11;

        if (Vector256.IsHardwareAccelerated)
        {
            (Vector256.LoadUnsafe(ref l, 0) - Vector256.LoadUnsafe(ref r, 0)).StoreUnsafe(ref dst, 0);
            (Vector256.LoadUnsafe(ref l, 8) - Vector256.LoadUnsafe(ref r, 8)).StoreUnsafe(ref dst, 8);
            return result;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            (Vector128.LoadUnsafe(ref l, 0) - Vector128.LoadUnsafe(ref r, 0)).StoreUnsafe(ref dst, 0);
            (Vector128.LoadUnsafe(ref l, 4) - Vector128.LoadUnsafe(ref r, 4)).StoreUnsafe(ref dst, 4);
            (Vector128.LoadUnsafe(ref l, 8) - Vector128.LoadUnsafe(ref r, 8)).StoreUnsafe(ref dst, 8);
            (Vector128.LoadUnsafe(ref l, 12) - Vector128.LoadUnsafe(ref r, 12)).StoreUnsafe(ref dst, 12);
            return result;
        }

        return new Mat4(
            left.M11 - right.M11, left.M12 - right.M12, left.M13 - right.M13, left.M14 - right.M14,
            left.M21 - right.M21, left.M22 - right.M22, left.M23 - right.M23, left.M24 - right.M24,
            left.M31 - right.M31, left.M32 - right.M32, left.M33 - right.M33, left.M34 - right.M34,
            left.M41 - right.M41, left.M42 - right.M42, left.M43 - right.M43, left.M44 - right.M44
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 operator *(Mat4 a, Mat4 b)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            ref float bRef = ref Unsafe.AsRef(in b.M11);
            Vector128<float> b0 = Vector128.LoadUnsafe(ref bRef, 0);
            Vector128<float> b1 = Vector128.LoadUnsafe(ref bRef, 4);
            Vector128<float> b2 = Vector128.LoadUnsafe(ref bRef, 8);
            Vector128<float> b3 = Vector128.LoadUnsafe(ref bRef, 12);

            Unsafe.SkipInit(out Mat4 res);
            ref float resRef = ref res.M11;

            Vector128.FusedMultiplyAdd(Vector128.Create(a.M11), b0,
                Vector128.FusedMultiplyAdd(Vector128.Create(a.M12), b1,
                Vector128.FusedMultiplyAdd(Vector128.Create(a.M13), b2, Vector128.Create(a.M14) * b3))).StoreUnsafe(ref resRef, 0);

            Vector128.FusedMultiplyAdd(Vector128.Create(a.M21), b0,
                Vector128.FusedMultiplyAdd(Vector128.Create(a.M22), b1,
                Vector128.FusedMultiplyAdd(Vector128.Create(a.M23), b2, Vector128.Create(a.M24) * b3))).StoreUnsafe(ref resRef, 4);

            Vector128.FusedMultiplyAdd(Vector128.Create(a.M31), b0,
                Vector128.FusedMultiplyAdd(Vector128.Create(a.M32), b1,
                Vector128.FusedMultiplyAdd(Vector128.Create(a.M33), b2, Vector128.Create(a.M34) * b3))).StoreUnsafe(ref resRef, 8);

            Vector128.FusedMultiplyAdd(Vector128.Create(a.M41), b0,
                Vector128.FusedMultiplyAdd(Vector128.Create(a.M42), b1,
                Vector128.FusedMultiplyAdd(Vector128.Create(a.M43), b2, Vector128.Create(a.M44) * b3))).StoreUnsafe(ref resRef, 12);

            return res;
        }

        return new Mat4(
            a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31 + a.M14 * b.M41,
            a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32 + a.M14 * b.M42,
            a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33 + a.M14 * b.M43,
            a.M11 * b.M14 + a.M12 * b.M24 + a.M13 * b.M34 + a.M14 * b.M44,

            a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31 + a.M24 * b.M41,
            a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32 + a.M24 * b.M42,
            a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33 + a.M24 * b.M43,
            a.M21 * b.M14 + a.M22 * b.M24 + a.M23 * b.M34 + a.M24 * b.M44,

            a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31 + a.M34 * b.M41,
            a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32 + a.M34 * b.M42,
            a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33 + a.M34 * b.M43,
            a.M31 * b.M14 + a.M32 * b.M24 + a.M33 * b.M34 + a.M34 * b.M44,

            a.M41 * b.M11 + a.M42 * b.M21 + a.M43 * b.M31 + a.M44 * b.M41,
            a.M41 * b.M12 + a.M42 * b.M22 + a.M43 * b.M32 + a.M44 * b.M42,
            a.M41 * b.M13 + a.M42 * b.M23 + a.M43 * b.M33 + a.M44 * b.M43,
            a.M41 * b.M14 + a.M42 * b.M24 + a.M43 * b.M34 + a.M44 * b.M44
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 operator *(Mat4 left, float scalar)
    {
        Unsafe.SkipInit(out Mat4 result);
        ref float l = ref Unsafe.AsRef(in left.M11);
        ref float dst = ref result.M11;

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> s = Vector256.Create(scalar);
            (Vector256.LoadUnsafe(ref l, 0) * s).StoreUnsafe(ref dst, 0);
            (Vector256.LoadUnsafe(ref l, 8) * s).StoreUnsafe(ref dst, 8);
            return result;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> s = Vector128.Create(scalar);
            (Vector128.LoadUnsafe(ref l, 0) * s).StoreUnsafe(ref dst, 0);
            (Vector128.LoadUnsafe(ref l, 4) * s).StoreUnsafe(ref dst, 4);
            (Vector128.LoadUnsafe(ref l, 8) * s).StoreUnsafe(ref dst, 8);
            (Vector128.LoadUnsafe(ref l, 12) * s).StoreUnsafe(ref dst, 12);
            return result;
        }

        return new Mat4(
            left.M11 * scalar, left.M12 * scalar, left.M13 * scalar, left.M14 * scalar,
            left.M21 * scalar, left.M22 * scalar, left.M23 * scalar, left.M24 * scalar,
            left.M31 * scalar, left.M32 * scalar, left.M33 * scalar, left.M34 * scalar,
            left.M41 * scalar, left.M42 * scalar, left.M43 * scalar, left.M44 * scalar
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 operator *(float scalar, Mat4 matrix) => matrix * scalar;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec4 operator *(Mat4 matrix, Vec4 vector)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            ref float mRef = ref Unsafe.AsRef(in matrix.M11);
            Vector128<float> row0 = Vector128.LoadUnsafe(ref mRef, 0);
            Vector128<float> row1 = Vector128.LoadUnsafe(ref mRef, 4);
            Vector128<float> row2 = Vector128.LoadUnsafe(ref mRef, 8);
            Vector128<float> row3 = Vector128.LoadUnsafe(ref mRef, 12);

            Vector128<float> res = Vector128.FusedMultiplyAdd(Vector128.Create(vector.X), row0,
                Vector128.FusedMultiplyAdd(Vector128.Create(vector.Y), row1,
                Vector128.FusedMultiplyAdd(Vector128.Create(vector.Z), row2, Vector128.Create(vector.W) * row3)));

            Unsafe.SkipInit(out Vec4 result);
            res.StoreUnsafe(ref result.X);
            return result;
        }

        return new Vec4(
            MathF.FusedMultiplyAdd(vector.X, matrix.M11, MathF.FusedMultiplyAdd(vector.Y, matrix.M21, MathF.FusedMultiplyAdd(vector.Z, matrix.M31, vector.W * matrix.M41))),
            MathF.FusedMultiplyAdd(vector.X, matrix.M12, MathF.FusedMultiplyAdd(vector.Y, matrix.M22, MathF.FusedMultiplyAdd(vector.Z, matrix.M32, vector.W * matrix.M42))),
            MathF.FusedMultiplyAdd(vector.X, matrix.M13, MathF.FusedMultiplyAdd(vector.Y, matrix.M23, MathF.FusedMultiplyAdd(vector.Z, matrix.M33, vector.W * matrix.M43))),
            MathF.FusedMultiplyAdd(vector.X, matrix.M14, MathF.FusedMultiplyAdd(vector.Y, matrix.M24, MathF.FusedMultiplyAdd(vector.Z, matrix.M34, vector.W * matrix.M44)))
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec4 operator *(Vec4 vector, Mat4 matrix) => matrix * vector;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 operator -(Mat4 value) => value * -1.0f;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 operator +(Mat4 value) => value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator ==(Mat4 left, Mat4 right)
    {
        ref float l = ref Unsafe.AsRef(in left.M11);
        ref float r = ref Unsafe.AsRef(in right.M11);

        if (Vector256.IsHardwareAccelerated)
        {
            return Vector256.EqualsAll(Vector256.LoadUnsafe(ref l, 0), Vector256.LoadUnsafe(ref r, 0)) &&
                   Vector256.EqualsAll(Vector256.LoadUnsafe(ref l, 8), Vector256.LoadUnsafe(ref r, 8));
        }

        if (Vector128.IsHardwareAccelerated)
        {
            return Vector128.EqualsAll(Vector128.LoadUnsafe(ref l, 0), Vector128.LoadUnsafe(ref r, 0)) &&
                   Vector128.EqualsAll(Vector128.LoadUnsafe(ref l, 4), Vector128.LoadUnsafe(ref r, 4)) &&
                   Vector128.EqualsAll(Vector128.LoadUnsafe(ref l, 8), Vector128.LoadUnsafe(ref r, 8)) &&
                   Vector128.EqualsAll(Vector128.LoadUnsafe(ref l, 12), Vector128.LoadUnsafe(ref r, 12));
        }

        return left.M11 == right.M11 && left.M12 == right.M12 && left.M13 == right.M13 && left.M14 == right.M14 &&
               left.M21 == right.M21 && left.M22 == right.M22 && left.M23 == right.M23 && left.M24 == right.M24 &&
               left.M31 == right.M31 && left.M32 == right.M32 && left.M33 == right.M33 && left.M34 == right.M34 &&
               left.M41 == right.M41 && left.M42 == right.M42 && left.M43 == right.M43 && left.M44 == right.M44;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator !=(Mat4 left, Mat4 right) => !(left == right);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 Add(Mat4 left, Mat4 right) => left + right;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 Subtract(Mat4 left, Mat4 right) => left - right;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 Multiply(Mat4 left, Mat4 right) => left * right;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 Multiply(Mat4 left, float scalar) => left * scalar;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 Negate(Mat4 value) => -value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool Equals(Mat4 other) => this == other;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool Equals(Mat4 other, float tolerance = DefaultTolerance)
    {
        ref float a = ref Unsafe.AsRef(in M11);
        ref float b = ref Unsafe.AsRef(in other.M11);

        for (nuint i = 0; i < 16; i++)
        {
            if (MathF.Abs(Unsafe.Add(ref a, i) - Unsafe.Add(ref b, i)) > tolerance)
            {
                return false;
            }
        }
        return true;
    }

    public override readonly bool Equals(object? obj) =>
        obj is Mat4 other && Equals(other);

    public override readonly int GetHashCode()
    {
        HashCode hash = default;
        ref float self = ref Unsafe.AsRef(in M11);
        for (nuint i = 0; i < 16; i++)
        {
            hash.Add(Unsafe.Add(ref self, i));
        }
        return hash.ToHashCode();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly float Trace() => M11 + M22 + M33 + M44;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly Mat4 Transpose()
    {
        if (System.Runtime.Intrinsics.X86.Sse.IsSupported)
        {
            ref float m = ref Unsafe.AsRef(in M11);
            Vector128<float> r0 = Vector128.LoadUnsafe(ref m, 0);
            Vector128<float> r1 = Vector128.LoadUnsafe(ref m, 4);
            Vector128<float> r2 = Vector128.LoadUnsafe(ref m, 8);
            Vector128<float> r3 = Vector128.LoadUnsafe(ref m, 12);

            Vector128<float> l12 = System.Runtime.Intrinsics.X86.Sse.UnpackLow(r0, r1);
            Vector128<float> l34 = System.Runtime.Intrinsics.X86.Sse.UnpackLow(r2, r3);
            Vector128<float> u12 = System.Runtime.Intrinsics.X86.Sse.UnpackHigh(r0, r1);
            Vector128<float> u34 = System.Runtime.Intrinsics.X86.Sse.UnpackHigh(r2, r3);

            Unsafe.SkipInit(out Mat4 res);
            ref float dst = ref res.M11;
            System.Runtime.Intrinsics.X86.Sse.MoveLowToHigh(l12, l34).StoreUnsafe(ref dst, 0);
            System.Runtime.Intrinsics.X86.Sse.MoveHighToLow(l34, l12).StoreUnsafe(ref dst, 4);
            System.Runtime.Intrinsics.X86.Sse.MoveLowToHigh(u12, u34).StoreUnsafe(ref dst, 8);
            System.Runtime.Intrinsics.X86.Sse.MoveHighToLow(u34, u12).StoreUnsafe(ref dst, 12);
            return res;
        }

        return new Mat4(
            M11, M21, M31, M41,
            M12, M22, M32, M42,
            M13, M23, M33, M43,
            M14, M24, M34, M44
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 Transpose(Mat4 matrix) => matrix.Transpose();

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly float Determinant()
    {
        float s0 = (M11 * M22) - (M12 * M21);
        float s1 = (M11 * M23) - (M13 * M21);
        float s2 = (M11 * M24) - (M14 * M21);
        float s3 = (M12 * M23) - (M13 * M22);
        float s4 = (M12 * M24) - (M14 * M22);
        float s5 = (M13 * M24) - (M14 * M23);

        float c5 = (M33 * M44) - (M34 * M43);
        float c4 = (M32 * M44) - (M34 * M42);
        float c3 = (M32 * M43) - (M33 * M42);
        float c2 = (M31 * M44) - (M34 * M41);
        float c1 = (M31 * M43) - (M33 * M41);
        float c0 = (M31 * M42) - (M32 * M41);

        return (s0 * c5) - (s1 * c4) + (s2 * c3) + (s3 * c2) - (s4 * c1) + (s5 * c0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool Invert(out Mat4 result)
    {
        float s0 = (M11 * M22) - (M12 * M21);
        float s1 = (M11 * M23) - (M13 * M21);
        float s2 = (M11 * M24) - (M14 * M21);
        float s3 = (M12 * M23) - (M13 * M22);
        float s4 = (M12 * M24) - (M14 * M22);
        float s5 = (M13 * M24) - (M14 * M23);

        float c5 = (M33 * M44) - (M34 * M43);
        float c4 = (M32 * M44) - (M34 * M42);
        float c3 = (M32 * M43) - (M33 * M42);
        float c2 = (M31 * M44) - (M34 * M41);
        float c1 = (M31 * M43) - (M33 * M41);
        float c0 = (M31 * M42) - (M32 * M41);

        float det = (s0 * c5) - (s1 * c4) + (s2 * c3) + (s3 * c2) - (s4 * c1) + (s5 * c0);

        if (MathF.Abs(det) <= 1e-30f)
        {
            result = default;
            return false;
        }

        float invDet = 1.0f / det;

        result = new Mat4(
            ((M22 * c5) - (M23 * c4) + (M24 * c3)) * invDet,
            ((-M12 * c5) + (M13 * c4) - (M14 * c3)) * invDet,
            ((M42 * s5) - (M43 * s4) + (M44 * s3)) * invDet,
            ((-M32 * s5) + (M33 * s4) - (M34 * s3)) * invDet,

            ((-M21 * c5) + (M23 * c2) - (M24 * c1)) * invDet,
            ((M11 * c5) - (M13 * c2) + (M14 * c1)) * invDet,
            ((-M41 * s5) + (M43 * s2) - (M44 * s1)) * invDet,
            ((M31 * s5) - (M33 * s2) + (M34 * s1)) * invDet,

            ((M21 * c4) - (M22 * c2) + (M24 * c0)) * invDet,
            ((-M11 * c4) + (M12 * c2) - (M14 * c0)) * invDet,
            ((M41 * s4) - (M42 * s2) + (M44 * s0)) * invDet,
            ((-M31 * s4) + (M32 * s2) - (M34 * s0)) * invDet,

            ((-M21 * c3) + (M22 * c1) - (M23 * c0)) * invDet,
            ((M11 * c3) - (M12 * c1) + (M13 * c0)) * invDet,
            ((-M41 * s3) + (M42 * s1) - (M43 * s0)) * invDet,
            ((M31 * s3) - (M32 * s1) + (M33 * s0)) * invDet
        );

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool Invert(Mat4 matrix, out Mat4 result) => matrix.Invert(out result);

    // __NEXT__
}
