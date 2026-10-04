namespace Axrone.Numeric;

using System.Buffers;
using Axrone.Simd;

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
    IAffineTransformable4x4<Mat4>,
    IMatrixStorage4x4<Mat4>,
    IFormattable,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IParsable<Mat4>,
    ISpanParsable<Mat4>,
    IUtf8SpanParsable<Mat4>
{
    private static readonly SearchValues<char> Separators = SearchValues.Create(" ,;\t\r\n{}[]()");
    private static readonly SearchValues<byte> SeparatorsUtf8 = SearchValues.Create(" ,;\t\r\n{}[]()"u8);
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
    public static Mat4 operator *(Mat4 a, Mat4 b) => Multiply(in a, in b);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 Multiply(in Mat4 left, in Mat4 right)
    {
        Multiply(in left, in right, out Mat4 result);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Multiply(in Mat4 left, in Mat4 right, out Mat4 result)
    {
        ref float bRef = ref Unsafe.AsRef(in right.M11);
        Vector128<float> b0 = Vector128.LoadUnsafe(ref bRef, 0);
        Vector128<float> b1 = Vector128.LoadUnsafe(ref bRef, 4);
        Vector128<float> b2 = Vector128.LoadUnsafe(ref bRef, 8);
        Vector128<float> b3 = Vector128.LoadUnsafe(ref bRef, 12);

        Unsafe.SkipInit(out result);
        ref float resRef = ref Unsafe.AsRef(in result.M11);

        if (SimdRow32.IsFusedMultiplyAddSupported)
        {
            SimdRow32.MultiplyAddRowFused(left.M11, left.M12, left.M13, left.M14, b0, b1, b2, b3).StoreUnsafe(ref resRef, 0);
            SimdRow32.MultiplyAddRowFused(left.M21, left.M22, left.M23, left.M24, b0, b1, b2, b3).StoreUnsafe(ref resRef, 4);
            SimdRow32.MultiplyAddRowFused(left.M31, left.M32, left.M33, left.M34, b0, b1, b2, b3).StoreUnsafe(ref resRef, 8);
            SimdRow32.MultiplyAddRowFused(left.M41, left.M42, left.M43, left.M44, b0, b1, b2, b3).StoreUnsafe(ref resRef, 12);
            return;
        }

        SimdRow32.MultiplyAddRowPlain(left.M11, left.M12, left.M13, left.M14, b0, b1, b2, b3).StoreUnsafe(ref resRef, 0);
        SimdRow32.MultiplyAddRowPlain(left.M21, left.M22, left.M23, left.M24, b0, b1, b2, b3).StoreUnsafe(ref resRef, 4);
        SimdRow32.MultiplyAddRowPlain(left.M31, left.M32, left.M33, left.M34, b0, b1, b2, b3).StoreUnsafe(ref resRef, 8);
        SimdRow32.MultiplyAddRowPlain(left.M41, left.M42, left.M43, left.M44, b0, b1, b2, b3).StoreUnsafe(ref resRef, 12);
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
        ref float mRef = ref Unsafe.AsRef(in matrix.M11);
        Vector128<float> row0 = Vector128.LoadUnsafe(ref mRef, 0);
        Vector128<float> row1 = Vector128.LoadUnsafe(ref mRef, 4);
        Vector128<float> row2 = Vector128.LoadUnsafe(ref mRef, 8);
        Vector128<float> row3 = Vector128.LoadUnsafe(ref mRef, 12);

        Vector128<float> res = SimdRow32.IsFusedMultiplyAddSupported
            ? SimdRow32.MultiplyAddRowFused(vector.X, vector.Y, vector.Z, vector.W, row0, row1, row2, row3)
            : SimdRow32.MultiplyAddRowPlain(vector.X, vector.Y, vector.Z, vector.W, row0, row1, row2, row3);

        Unsafe.SkipInit(out Vec4 result);
        res.StoreUnsafe(ref result.X);
        return result;
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
    public static Mat4 Add(in Mat4 left, in Mat4 right) => left + right;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 Subtract(in Mat4 left, in Mat4 right) => left - right;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 Multiply(in Mat4 left, float scalar) => left * scalar;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreateTranslation(Vec3 position) =>
        new(
            1.0f, 0.0f, 0.0f, 0.0f,
            0.0f, 1.0f, 0.0f, 0.0f,
            0.0f, 0.0f, 1.0f, 0.0f,
            position.X, position.Y, position.Z, 1.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreateTranslation(float x, float y, float z) =>
        new(
            1.0f, 0.0f, 0.0f, 0.0f,
            0.0f, 1.0f, 0.0f, 0.0f,
            0.0f, 0.0f, 1.0f, 0.0f,
            x, y, z, 1.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreateScale(Vec3 scales) =>
        new(
            scales.X, 0.0f, 0.0f, 0.0f,
            0.0f, scales.Y, 0.0f, 0.0f,
            0.0f, 0.0f, scales.Z, 0.0f,
            0.0f, 0.0f, 0.0f, 1.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreateScale(float scale) => CreateScale(new Vec3(scale));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreateScale(float xScale, float yScale, float zScale) =>
        CreateScale(new Vec3(xScale, yScale, zScale));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreateRotationX(AngleRadians angle)
    {
        (float sin, float cos) = MathF.SinCos(angle.Value);
        return new Mat4(
            1.0f, 0.0f, 0.0f, 0.0f,
            0.0f, cos, sin, 0.0f,
            0.0f, -sin, cos, 0.0f,
            0.0f, 0.0f, 0.0f, 1.0f
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreateRotationX(float angleRadians) => CreateRotationX(new AngleRadians(angleRadians));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreateRotationY(AngleRadians angle)
    {
        (float sin, float cos) = MathF.SinCos(angle.Value);
        return new Mat4(
            cos, 0.0f, -sin, 0.0f,
            0.0f, 1.0f, 0.0f, 0.0f,
            sin, 0.0f, cos, 0.0f,
            0.0f, 0.0f, 0.0f, 1.0f
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreateRotationY(float angleRadians) => CreateRotationY(new AngleRadians(angleRadians));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreateRotationZ(AngleRadians angle)
    {
        (float sin, float cos) = MathF.SinCos(angle.Value);
        return new Mat4(
            cos, sin, 0.0f, 0.0f,
            -sin, cos, 0.0f, 0.0f,
            0.0f, 0.0f, 1.0f, 0.0f,
            0.0f, 0.0f, 0.0f, 1.0f
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreateRotationZ(float angleRadians) => CreateRotationZ(new AngleRadians(angleRadians));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreateFromAxisAngle(UnitAxis3 axis, AngleRadians angle)
    {
        float x = axis.X, y = axis.Y, z = axis.Z;
        (float sa, float ca) = MathF.SinCos(angle.Value);
        float xx = x * x, yy = y * y, zz = z * z;
        float xy = x * y, xz = x * z, yz = y * z;

        return new Mat4(
            xx + (ca * (1.0f - xx)),
            (xy - (ca * xy)) + (sa * z),
            (xz - (ca * xz)) - (sa * y),
            0.0f,
            (xy - (ca * xy)) - (sa * z),
            yy + (ca * (1.0f - yy)),
            (yz - (ca * yz)) + (sa * x),
            0.0f,
            (xz - (ca * xz)) + (sa * y),
            (yz - (ca * yz)) - (sa * x),
            zz + (ca * (1.0f - zz)),
            0.0f,
            0.0f, 0.0f, 0.0f, 1.0f
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreateFromAxisAngle(Vec3 axis, float angleRadians) =>
        CreateFromAxisAngle(UnitAxis3.Create(axis), new AngleRadians(angleRadians));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreateFromQuaternion(Quat rotation)
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

        return new Mat4(
            1.0f - (2.0f * (yy + zz)), 2.0f * (xy + wz), 2.0f * (xz - wy), 0.0f,
            2.0f * (xy - wz), 1.0f - (2.0f * (zz + xx)), 2.0f * (yz + wx), 0.0f,
            2.0f * (xz + wy), 2.0f * (yz - wx), 1.0f - (2.0f * (yy + xx)), 0.0f,
            0.0f, 0.0f, 0.0f, 1.0f
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreateFromYawPitchRoll(AngleRadians yaw, AngleRadians pitch, AngleRadians roll) =>
        CreateFromQuaternion(Quat.CreateFromYawPitchRoll(yaw, pitch, roll));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreateFromYawPitchRoll(float yaw, float pitch, float roll) =>
        CreateFromYawPitchRoll(new AngleRadians(yaw), new AngleRadians(pitch), new AngleRadians(roll));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreatePerspectiveFieldOfView<TPolicy>(AngleRadians fieldOfView, float aspectRatio, float nearPlaneDistance, float farPlaneDistance)
        where TPolicy : struct, IProjectionPolicy =>
        TPolicy.CreatePerspective(fieldOfView, aspectRatio, nearPlaneDistance, farPlaneDistance);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreatePerspectiveFieldOfView(float fieldOfView, float aspectRatio, float nearPlaneDistance, float farPlaneDistance) =>
        CreatePerspectiveFieldOfView<RightHandedZeroToOne>(new AngleRadians(fieldOfView), aspectRatio, nearPlaneDistance, farPlaneDistance);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreateOrthographic<TPolicy>(float width, float height, float zNearPlane, float zFarPlane)
        where TPolicy : struct, IProjectionPolicy =>
        TPolicy.CreateOrthographic(width, height, zNearPlane, zFarPlane);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreateOrthographic(float width, float height, float zNearPlane, float zFarPlane) =>
        CreateOrthographic<RightHandedZeroToOne>(width, height, zNearPlane, zFarPlane);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreateLookAt(Vec3 cameraPosition, Vec3 cameraTarget, Vec3 cameraUpVector)
    {
        Vec3 zaxis = Vec3.Normalize(cameraPosition - cameraTarget);
        Vec3 xaxis = Vec3.Normalize(Vec3.Cross(cameraUpVector, zaxis));
        Vec3 yaxis = Vec3.Cross(zaxis, xaxis);

        return new Mat4(
            xaxis.X, yaxis.X, zaxis.X, 0.0f,
            xaxis.Y, yaxis.Y, zaxis.Y, 0.0f,
            xaxis.Z, yaxis.Z, zaxis.Z, 0.0f,
            -Vec3.Dot(xaxis, cameraPosition), -Vec3.Dot(yaxis, cameraPosition), -Vec3.Dot(zaxis, cameraPosition), 1.0f
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreateWorld(Vec3 position, Vec3 forward, Vec3 up)
    {
        Vec3 zaxis = Vec3.Normalize(-forward);
        Vec3 xaxis = Vec3.Normalize(Vec3.Cross(up, zaxis));
        Vec3 yaxis = Vec3.Cross(zaxis, xaxis);

        return new Mat4(
            xaxis.X, xaxis.Y, xaxis.Z, 0.0f,
            yaxis.X, yaxis.Y, yaxis.Z, 0.0f,
            zaxis.X, zaxis.Y, zaxis.Z, 0.0f,
            position.X, position.Y, position.Z, 1.0f
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly MatrixDecompositionResult Decompose()
    {
        Vec3 translation = new(M41, M42, M43);

        float sx = new Vec3(M11, M12, M13).Length();
        float sy = new Vec3(M21, M22, M23).Length();
        float sz = new Vec3(M31, M32, M33).Length();

        if (Determinant() < 0.0f)
        {
            sx = -sx;
        }

        Vec3 scale = new(sx, sy, sz);

        if (MathF.Abs(sx) <= 1e-30f || MathF.Abs(sy) <= 1e-30f || MathF.Abs(sz) <= 1e-30f)
        {
            return new MatrixDecompositionResult(scale, Quat.Identity, translation, DecompositionStatus.DegenerateScale);
        }

        Mat4 rotMat = new(
            M11 / sx, M12 / sx, M13 / sx, 0.0f,
            M21 / sy, M22 / sy, M23 / sy, 0.0f,
            M31 / sz, M32 / sz, M33 / sz, 0.0f,
            0.0f, 0.0f, 0.0f, 1.0f
        );

        Quat rotation = Quat.CreateFromRotationMatrix((Matrix4x4)rotMat);
        return new MatrixDecompositionResult(scale, rotation, translation, DecompositionStatus.Success);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool Decompose(out Vec3 scale, out Quat rotation, out Vec3 translation)
    {
        MatrixDecompositionResult result = Decompose();
        scale = result.Scale;
        rotation = result.Rotation;
        translation = result.Translation;
        return result.IsSuccess;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 Lerp(Mat4 a, Mat4 b, float amount)
    {
        Unsafe.SkipInit(out Mat4 result);
        ref float aRef = ref Unsafe.AsRef(in a.M11);
        ref float bRef = ref Unsafe.AsRef(in b.M11);
        ref float dst = ref result.M11;

        if (Vector256.IsHardwareAccelerated && SimdRow32.IsFusedMultiplyAddSupported)
        {
            Vector256<float> amt256 = Vector256.Create(amount);
            Vector256<float> a0 = Vector256.LoadUnsafe(ref aRef, 0);
            Vector256<float> a1 = Vector256.LoadUnsafe(ref aRef, 8);
            Vector256<float> b0 = Vector256.LoadUnsafe(ref bRef, 0);
            Vector256<float> b1 = Vector256.LoadUnsafe(ref bRef, 8);

            Vector256.FusedMultiplyAdd(b0 - a0, amt256, a0).StoreUnsafe(ref dst, 0);
            Vector256.FusedMultiplyAdd(b1 - a1, amt256, a1).StoreUnsafe(ref dst, 8);
            return result;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> amt128 = Vector128.Create(amount);
            if (SimdRow32.IsFusedMultiplyAddSupported)
            {
                for (nuint i = 0; i < 16; i += 4)
                {
                    SimdRow32.LerpRowFused(
                        Vector128.LoadUnsafe(ref aRef, i),
                        Vector128.LoadUnsafe(ref bRef, i),
                        amt128).StoreUnsafe(ref dst, i);
                }
            }
            else
            {
                for (nuint i = 0; i < 16; i += 4)
                {
                    SimdRow32.LerpRowPlain(
                        Vector128.LoadUnsafe(ref aRef, i),
                        Vector128.LoadUnsafe(ref bRef, i),
                        amt128).StoreUnsafe(ref dst, i);
                }
            }
            return result;
        }

        for (nuint i = 0; i < 16; i++)
        {
            Unsafe.Add(ref dst, i) = MathF.FusedMultiplyAdd(Unsafe.Add(ref bRef, i) - Unsafe.Add(ref aRef, i), amount, Unsafe.Add(ref aRef, i));
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly void Apply<TAction, TState>(ref TState state)
        where TAction : struct, IMatrixTransformAction<TState>
        where TState : allows ref struct
    {
        TAction.Execute(in this, ref state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public MatrixElementEnumerator GetEnumerator() => new(in this);

    public MatrixRowEnumerable Rows
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => new(in this);
    }

    public MatrixColumnEnumerable Columns
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => new(in this);
    }

    public override readonly string ToString() => ToString(null, CultureInfo.InvariantCulture);

    public readonly string ToString(string? format) => ToString(format, CultureInfo.InvariantCulture);

    public readonly string ToString(string? format, IFormatProvider? formatProvider)
    {
        Span<char> buffer = stackalloc char[512];
        if (TryFormat(buffer, out int charsWritten, format, formatProvider))
        {
            return new string(buffer.Slice(0, charsWritten));
        }
        return "{ ... }";
    }

    public readonly bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null)
    {
        charsWritten = 0;
        Span<char> temp = stackalloc char[512];
        int written = 0;

        temp[written++] = '{';
        temp[written++] = ' ';

        NumberFormatInfo nfi = NumberFormatInfo.GetInstance(provider);
        ReadOnlySpan<char> separator = nfi.NumberDecimalSeparator == "," ? "; " : ", ";

        ref float baseRef = ref Unsafe.AsRef(in M11);
        for (nuint i = 0; i < 16; i++)
        {
            if (i > 0)
            {
                if (temp.Length - written < separator.Length)
                {
                    return false;
                }
                separator.CopyTo(temp.Slice(written));
                written += separator.Length;
            }

            if (!Unsafe.Add(ref baseRef, i).TryFormat(temp.Slice(written), out int w, format, provider))
            {
                return false;
            }
            written += w;
        }

        if (temp.Length - written < 2)
        {
            return false;
        }
        temp[written++] = ' ';
        temp[written++] = '}';

        if (destination.Length < written)
        {
            return false;
        }

        temp.Slice(0, written).CopyTo(destination);
        charsWritten = written;
        return true;
    }

    public readonly bool TryFormat(
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null)
    {
        bytesWritten = 0;
        Span<byte> temp = stackalloc byte[512];
        int written = 0;

        temp[written++] = (byte)'{';
        temp[written++] = (byte)' ';

        NumberFormatInfo nfi = NumberFormatInfo.GetInstance(provider);
        ReadOnlySpan<byte> separator = nfi.NumberDecimalSeparator == "," ? "; "u8 : ", "u8;

        ref float baseRef = ref Unsafe.AsRef(in M11);
        for (nuint i = 0; i < 16; i++)
        {
            if (i > 0)
            {
                if (temp.Length - written < separator.Length)
                {
                    return false;
                }
                separator.CopyTo(temp.Slice(written));
                written += separator.Length;
            }

            if (!Unsafe.Add(ref baseRef, i).TryFormat(temp.Slice(written), out int w, format, provider))
            {
                return false;
            }
            written += w;
        }

        if (temp.Length - written < 2)
        {
            return false;
        }
        temp[written++] = (byte)' ';
        temp[written++] = (byte)'}';

        if (utf8Destination.Length < written)
        {
            return false;
        }

        temp.Slice(0, written).CopyTo(utf8Destination);
        bytesWritten = written;
        return true;
    }

    public static Mat4 Parse(string s, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan(), provider);
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Mat4 result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }
        return TryParse(s.AsSpan(), provider, out result);
    }

    public static Mat4 Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out Mat4 result))
        {
            NumericThrowHelper.ThrowFormatException("Invalid Mat4 format string.");
        }
        return result;
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Mat4 result)
    {
        result = default;
        ReadOnlySpan<char> remaining = s;
        Unsafe.SkipInit(out Mat4 m);
        ref float baseRef = ref m.M11;
        int count = 0;

        while (!remaining.IsEmpty && count < 16)
        {
            int tokenStart = remaining.IndexOfAnyExcept(Separators);
            if (tokenStart < 0)
            {
                break;
            }
            remaining = remaining.Slice(tokenStart);

            int tokenEnd = remaining.IndexOfAny(Separators);
            ReadOnlySpan<char> token = tokenEnd < 0 ? remaining : remaining.Slice(0, tokenEnd);
            remaining = tokenEnd < 0 ? default : remaining.Slice(tokenEnd);

            if (!float.TryParse(token, NumberStyles.Float, provider, out Unsafe.Add(ref baseRef, (nuint)count)))
            {
                return false;
            }
            count++;
        }

        if (count == 16)
        {
            result = m;
            return true;
        }

        return false;
    }

    public static Mat4 Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider)
    {
        if (!TryParse(utf8Text, provider, out Mat4 result))
        {
            NumericThrowHelper.ThrowFormatException("Invalid Mat4 UTF-8 format stream.");
        }
        return result;
    }

    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Mat4 result)
    {
        result = default;
        ReadOnlySpan<byte> remaining = utf8Text;
        Unsafe.SkipInit(out Mat4 m);
        ref float baseRef = ref m.M11;
        int count = 0;

        while (!remaining.IsEmpty && count < 16)
        {
            int tokenStart = remaining.IndexOfAnyExcept(SeparatorsUtf8);
            if (tokenStart < 0)
            {
                break;
            }
            remaining = remaining.Slice(tokenStart);

            int tokenEnd = remaining.IndexOfAny(SeparatorsUtf8);
            ReadOnlySpan<byte> token = tokenEnd < 0 ? remaining : remaining.Slice(0, tokenEnd);
            remaining = tokenEnd < 0 ? default : remaining.Slice(tokenEnd);

            if (!float.TryParse(token, NumberStyles.Float, provider, out Unsafe.Add(ref baseRef, (nuint)count)))
            {
                return false;
            }
            count++;
        }

        if (count == 16)
        {
            result = m;
            return true;
        }

        return false;
    }
}

public interface IMatrixTransformAction<TState> where TState : allows ref struct
{
    static abstract void Execute(ref readonly Mat4 matrix, ref TState state);
}

public enum DecompositionStatus : byte
{
    Success = 0,
    DegenerateScale = 1,
    Singular = 2
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct MatrixDecompositionResult
{
    public readonly Vec3 Scale;
    public readonly Quat Rotation;
    public readonly Vec3 Translation;
    public readonly DecompositionStatus Status;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal MatrixDecompositionResult(Vec3 scale, Quat rotation, Vec3 translation, DecompositionStatus status)
    {
        Scale = scale;
        Rotation = rotation;
        Translation = translation;
        Status = status;
    }

    public bool IsSuccess => Status == DecompositionStatus.Success;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TResult Match<TResult>(
        Func<Vec3, Quat, Vec3, TResult> onSuccess,
        Func<DecompositionStatus, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return Status switch
        {
            DecompositionStatus.Success => onSuccess(Scale, Rotation, Translation),
            DecompositionStatus.DegenerateScale => onFailure(DecompositionStatus.DegenerateScale),
            DecompositionStatus.Singular => onFailure(DecompositionStatus.Singular),
            _ => onFailure(Status)
        };
    }
}

public readonly struct EmptyTransform;
public readonly struct ScaledTransform;
public readonly struct RotatedTransform;
public readonly struct TranslatedTransform;

public readonly struct TransformPipeline<TPhase>
{
    internal readonly Mat4 Matrix;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal TransformPipeline(Mat4 matrix) => Matrix = matrix;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static TransformPipeline<EmptyTransform> Begin() => new(Mat4.Identity);
}

public static class TransformPipelineExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static TransformPipeline<ScaledTransform> Scale(this TransformPipeline<EmptyTransform> pipe, Vec3 scale) =>
        new(Mat4.CreateScale(scale));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static TransformPipeline<ScaledTransform> Scale(this TransformPipeline<EmptyTransform> pipe, float uniformScale) =>
        new(Mat4.CreateScale(uniformScale));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static TransformPipeline<RotatedTransform> Rotate(this TransformPipeline<ScaledTransform> pipe, Quat rotation) =>
        new(pipe.Matrix * Mat4.CreateFromQuaternion(rotation));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static TransformPipeline<RotatedTransform> Rotate(this TransformPipeline<EmptyTransform> pipe, Quat rotation) =>
        new(Mat4.CreateFromQuaternion(rotation));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static TransformPipeline<TranslatedTransform> Translate(this TransformPipeline<RotatedTransform> pipe, Vec3 translation) =>
        new(pipe.Matrix * Mat4.CreateTranslation(translation));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static TransformPipeline<TranslatedTransform> Translate(this TransformPipeline<ScaledTransform> pipe, Vec3 translation) =>
        new(pipe.Matrix * Mat4.CreateTranslation(translation));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static TransformPipeline<TranslatedTransform> Translate(this TransformPipeline<EmptyTransform> pipe, Vec3 translation) =>
        new(Mat4.CreateTranslation(translation));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 Build(this TransformPipeline<TranslatedTransform> pipe) => pipe.Matrix;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 Build(this TransformPipeline<RotatedTransform> pipe) => pipe.Matrix;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 Build(this TransformPipeline<ScaledTransform> pipe) => pipe.Matrix;
}

public ref struct MatrixElementEnumerator
{
    private readonly Mat4 _matrix;
    private int _index;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal MatrixElementEnumerator(scoped ref readonly Mat4 matrix)
    {
        _matrix = matrix;
        _index = -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool MoveNext() => ++_index < 16;

    public readonly float Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            if ((uint)_index >= 16U)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(_index));
            }

            return Unsafe.Add(ref Unsafe.AsRef(in _matrix.M11), (nuint)(uint)_index);
        }
    }
}

public ref struct MatrixRowEnumerable
{
    private readonly Mat4 _matrix;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal MatrixRowEnumerable(scoped ref readonly Mat4 matrix) => _matrix = matrix;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public MatrixRowEnumerator GetEnumerator() => new(in _matrix);
}

public ref struct MatrixRowEnumerator
{
    private readonly Mat4 _matrix;
    private int _index;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal MatrixRowEnumerator(scoped ref readonly Mat4 matrix)
    {
        _matrix = matrix;
        _index = -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool MoveNext() => ++_index < 4;

    public readonly Vec4 Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => _index switch
        {
            0 => _matrix.Row0,
            1 => _matrix.Row1,
            2 => _matrix.Row2,
            _ => _matrix.Row3
        };
    }
}

public ref struct MatrixColumnEnumerable
{
    private readonly Mat4 _matrix;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal MatrixColumnEnumerable(scoped ref readonly Mat4 matrix) => _matrix = matrix;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public MatrixColumnEnumerator GetEnumerator() => new(in _matrix);
}

public ref struct MatrixColumnEnumerator
{
    private readonly Mat4 _matrix;
    private int _index;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal MatrixColumnEnumerator(scoped ref readonly Mat4 matrix)
    {
        _matrix = matrix;
        _index = -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool MoveNext() => ++_index < 4;

    public readonly Vec4 Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => _index switch
        {
            0 => _matrix.Column0,
            1 => _matrix.Column1,
            2 => _matrix.Column2,
            _ => _matrix.Column3
        };
    }
}

public interface IProjectionPolicy
{
    static abstract Mat4 CreatePerspective(AngleRadians fieldOfViewY, float aspectRatio, float nearPlane, float farPlane);
    static abstract Mat4 CreateOrthographic(float width, float height, float nearPlane, float farPlane);
}

public readonly struct RightHandedZeroToOne : IProjectionPolicy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreatePerspective(AngleRadians fieldOfViewY, float aspectRatio, float nearPlane, float farPlane)
    {
        if (fieldOfViewY.Value <= 0.0f || fieldOfViewY.Value >= MathF.PI)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(fieldOfViewY), "Field of view must be in range (0, PI).");
        }
        if (nearPlane <= 0.0f)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(nearPlane), "Near plane must be positive.");
        }
        if (farPlane <= 0.0f || nearPlane >= farPlane)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(farPlane), "Far plane must be greater than near plane.");
        }

        float yScale = 1.0f / MathF.Tan(fieldOfViewY.Value * 0.5f);
        float xScale = yScale / aspectRatio;
        float negFarRange = float.IsPositiveInfinity(farPlane) ? -1.0f : farPlane / (nearPlane - farPlane);

        return new Mat4(
            xScale, 0.0f, 0.0f, 0.0f,
            0.0f, yScale, 0.0f, 0.0f,
            0.0f, 0.0f, negFarRange, -1.0f,
            0.0f, 0.0f, nearPlane * negFarRange, 0.0f
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreateOrthographic(float width, float height, float nearPlane, float farPlane)
    {
        float range = nearPlane - farPlane;
        return new Mat4(
            2.0f / width, 0.0f, 0.0f, 0.0f,
            0.0f, 2.0f / height, 0.0f, 0.0f,
            0.0f, 0.0f, 1.0f / range, 0.0f,
            0.0f, 0.0f, nearPlane / range, 1.0f
        );
    }
}

public readonly struct RightHandedNegOneToOne : IProjectionPolicy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreatePerspective(AngleRadians fieldOfViewY, float aspectRatio, float nearPlane, float farPlane)
    {
        if (fieldOfViewY.Value <= 0.0f || fieldOfViewY.Value >= MathF.PI)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(fieldOfViewY), "Field of view must be in range (0, PI).");
        }
        if (nearPlane <= 0.0f)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(nearPlane), "Near plane must be positive.");
        }
        if (farPlane <= 0.0f || nearPlane >= farPlane)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(farPlane), "Far plane must be greater than near plane.");
        }

        float yScale = 1.0f / MathF.Tan(fieldOfViewY.Value * 0.5f);
        float xScale = yScale / aspectRatio;
        float range = nearPlane - farPlane;

        return new Mat4(
            xScale, 0.0f, 0.0f, 0.0f,
            0.0f, yScale, 0.0f, 0.0f,
            0.0f, 0.0f, (farPlane + nearPlane) / range, -1.0f,
            0.0f, 0.0f, (2.0f * farPlane * nearPlane) / range, 0.0f
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreateOrthographic(float width, float height, float nearPlane, float farPlane)
    {
        float range = nearPlane - farPlane;
        return new Mat4(
            2.0f / width, 0.0f, 0.0f, 0.0f,
            0.0f, 2.0f / height, 0.0f, 0.0f,
            0.0f, 0.0f, 2.0f / range, 0.0f,
            0.0f, 0.0f, (farPlane + nearPlane) / range, 1.0f
        );
    }
}

public readonly struct LeftHandedZeroToOne : IProjectionPolicy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreatePerspective(AngleRadians fieldOfViewY, float aspectRatio, float nearPlane, float farPlane)
    {
        if (fieldOfViewY.Value <= 0.0f || fieldOfViewY.Value >= MathF.PI)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(fieldOfViewY), "Field of view must be in range (0, PI).");
        }
        if (nearPlane <= 0.0f)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(nearPlane), "Near plane must be positive.");
        }
        if (farPlane <= 0.0f || nearPlane >= farPlane)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(farPlane), "Far plane must be greater than near plane.");
        }

        float yScale = 1.0f / MathF.Tan(fieldOfViewY.Value * 0.5f);
        float xScale = yScale / aspectRatio;
        float range = farPlane - nearPlane;

        return new Mat4(
            xScale, 0.0f, 0.0f, 0.0f,
            0.0f, yScale, 0.0f, 0.0f,
            0.0f, 0.0f, farPlane / range, 1.0f,
            0.0f, 0.0f, -nearPlane * farPlane / range, 0.0f
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat4 CreateOrthographic(float width, float height, float nearPlane, float farPlane)
    {
        float range = farPlane - nearPlane;
        return new Mat4(
            2.0f / width, 0.0f, 0.0f, 0.0f,
            0.0f, 2.0f / height, 0.0f, 0.0f,
            0.0f, 0.0f, 1.0f / range, 0.0f,
            0.0f, 0.0f, -nearPlane / range, 1.0f
        );
    }
}
