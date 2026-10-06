namespace Axrone.Numeric;

using Axrone.Simd;
using System.Buffers;

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
    IAdditionOperators<Mat3, Mat3, Mat3>,
    ISubtractionOperators<Mat3, Mat3, Mat3>,
    IMultiplyOperators<Mat3, Mat3, Mat3>,
    IMultiplyOperators<Mat3, float, Mat3>,
    IUnaryNegationOperators<Mat3, Mat3>,
    IUnaryPlusOperators<Mat3, Mat3>,
    IAdditiveIdentity<Mat3, Mat3>,
    IMultiplicativeIdentity<Mat3, Mat3>,
    IReadOnlyMatrix3x3<Mat3>,
    IAffineTransformable3x3<Mat3>,
    IMatrixStorage3x3<Mat3>,
    IFormattable,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IParsable<Mat3>,
    ISpanParsable<Mat3>,
    IUtf8SpanParsable<Mat3>
{
    private static readonly SearchValues<char> Separators = SearchValues.Create(" ,;\t\r\n{}[]()");
    private static readonly SearchValues<byte> SeparatorsUtf8 = SearchValues.Create(" ,;\t\r\n{}[]()"u8);
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

    public static Mat3 AdditiveIdentity => Zero;
    public static Mat3 MultiplicativeIdentity => Identity;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 operator +(Mat3 left, Mat3 right)
    {
        Unsafe.SkipInit(out Mat3 result);
        ref float l = ref Unsafe.AsRef(in left.M11);
        ref float r = ref Unsafe.AsRef(in right.M11);
        ref float dst = ref result.M11;

        if (Vector256.IsHardwareAccelerated)
        {
            (Vector256.LoadUnsafe(ref l, 0) + Vector256.LoadUnsafe(ref r, 0)).StoreUnsafe(ref dst, 0);
            Unsafe.Add(ref dst, 8) = Unsafe.Add(ref l, 8) + Unsafe.Add(ref r, 8);
            return result;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            (Vector128.LoadUnsafe(ref l, 0) + Vector128.LoadUnsafe(ref r, 0)).StoreUnsafe(ref dst, 0);
            (Vector128.LoadUnsafe(ref l, 4) + Vector128.LoadUnsafe(ref r, 4)).StoreUnsafe(ref dst, 4);
            Unsafe.Add(ref dst, 8) = Unsafe.Add(ref l, 8) + Unsafe.Add(ref r, 8);
            return result;
        }

        return new Mat3(
            left.M11 + right.M11, left.M12 + right.M12, left.M13 + right.M13,
            left.M21 + right.M21, left.M22 + right.M22, left.M23 + right.M23,
            left.M31 + right.M31, left.M32 + right.M32, left.M33 + right.M33
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 operator -(Mat3 left, Mat3 right)
    {
        Unsafe.SkipInit(out Mat3 result);
        ref float l = ref Unsafe.AsRef(in left.M11);
        ref float r = ref Unsafe.AsRef(in right.M11);
        ref float dst = ref result.M11;

        if (Vector256.IsHardwareAccelerated)
        {
            (Vector256.LoadUnsafe(ref l, 0) - Vector256.LoadUnsafe(ref r, 0)).StoreUnsafe(ref dst, 0);
            Unsafe.Add(ref dst, 8) = Unsafe.Add(ref l, 8) - Unsafe.Add(ref r, 8);
            return result;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            (Vector128.LoadUnsafe(ref l, 0) - Vector128.LoadUnsafe(ref r, 0)).StoreUnsafe(ref dst, 0);
            (Vector128.LoadUnsafe(ref l, 4) - Vector128.LoadUnsafe(ref r, 4)).StoreUnsafe(ref dst, 4);
            Unsafe.Add(ref dst, 8) = Unsafe.Add(ref l, 8) - Unsafe.Add(ref r, 8);
            return result;
        }

        return new Mat3(
            left.M11 - right.M11, left.M12 - right.M12, left.M13 - right.M13,
            left.M21 - right.M21, left.M22 - right.M22, left.M23 - right.M23,
            left.M31 - right.M31, left.M32 - right.M32, left.M33 - right.M33
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 operator *(Mat3 a, Mat3 b) => Multiply(in a, in b);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 Multiply(in Mat3 left, in Mat3 right)
    {
        Multiply(in left, in right, out Mat3 result);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Multiply(in Mat3 left, in Mat3 right, out Mat3 result)
    {
        float l11 = left.M11, l12 = left.M12, l13 = left.M13;
        float l21 = left.M21, l22 = left.M22, l23 = left.M23;
        float l31 = left.M31, l32 = left.M32, l33 = left.M33;

        float r11 = right.M11, r12 = right.M12, r13 = right.M13;
        float r21 = right.M21, r22 = right.M22, r23 = right.M23;
        float r31 = right.M31, r32 = right.M32, r33 = right.M33;

        result = new Mat3(
            ((l11 * r11) + (l12 * r21)) + (l13 * r31),
            ((l11 * r12) + (l12 * r22)) + (l13 * r32),
            ((l11 * r13) + (l12 * r23)) + (l13 * r33),
            ((l21 * r11) + (l22 * r21)) + (l23 * r31),
            ((l21 * r12) + (l22 * r22)) + (l23 * r32),
            ((l21 * r13) + (l22 * r23)) + (l23 * r33),
            ((l31 * r11) + (l32 * r21)) + (l33 * r31),
            ((l31 * r12) + (l32 * r22)) + (l33 * r32),
            ((l31 * r13) + (l32 * r23)) + (l33 * r33)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 operator *(Mat3 left, float scalar)
    {
        Unsafe.SkipInit(out Mat3 result);
        ref float l = ref Unsafe.AsRef(in left.M11);
        ref float dst = ref result.M11;

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> s = Vector256.Create(scalar);
            (Vector256.LoadUnsafe(ref l, 0) * s).StoreUnsafe(ref dst, 0);
            Unsafe.Add(ref dst, 8) = Unsafe.Add(ref l, 8) * scalar;
            return result;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> s = Vector128.Create(scalar);
            (Vector128.LoadUnsafe(ref l, 0) * s).StoreUnsafe(ref dst, 0);
            (Vector128.LoadUnsafe(ref l, 4) * s).StoreUnsafe(ref dst, 4);
            Unsafe.Add(ref dst, 8) = Unsafe.Add(ref l, 8) * scalar;
            return result;
        }

        return new Mat3(
            left.M11 * scalar, left.M12 * scalar, left.M13 * scalar,
            left.M21 * scalar, left.M22 * scalar, left.M23 * scalar,
            left.M31 * scalar, left.M32 * scalar, left.M33 * scalar
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 operator *(float scalar, Mat3 matrix) => matrix * scalar;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec3 operator *(Mat3 matrix, Vec3 vector) =>
        new(
            MathF.FusedMultiplyAdd(vector.X, matrix.M11, MathF.FusedMultiplyAdd(vector.Y, matrix.M21, vector.Z * matrix.M31)),
            MathF.FusedMultiplyAdd(vector.X, matrix.M12, MathF.FusedMultiplyAdd(vector.Y, matrix.M22, vector.Z * matrix.M32)),
            MathF.FusedMultiplyAdd(vector.X, matrix.M13, MathF.FusedMultiplyAdd(vector.Y, matrix.M23, vector.Z * matrix.M33))
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec3 operator *(Vec3 vector, Mat3 matrix) => matrix * vector;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 operator -(Mat3 value) => value * -1.0f;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 operator +(Mat3 value) => value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 Add(in Mat3 left, in Mat3 right) => left + right;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 Subtract(in Mat3 left, in Mat3 right) => left - right;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 Multiply(in Mat3 left, float scalar) => left * scalar;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 Negate(Mat3 value) => -value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 Lerp(Mat3 a, Mat3 b, float amount)
    {
        Unsafe.SkipInit(out Mat3 result);
        ref float aRef = ref Unsafe.AsRef(in a.M11);
        ref float bRef = ref Unsafe.AsRef(in b.M11);
        ref float dst = ref result.M11;

        if (Vector256.IsHardwareAccelerated && SimdRow32.IsFusedMultiplyAddSupported)
        {
            Vector256<float> amt256 = Vector256.Create(amount);
            Vector256<float> a0 = Vector256.LoadUnsafe(ref aRef, 0);
            Vector256<float> b0 = Vector256.LoadUnsafe(ref bRef, 0);

            Vector256.FusedMultiplyAdd(b0 - a0, amt256, a0).StoreUnsafe(ref dst, 0);
            Unsafe.Add(ref dst, 8) = MathF.FusedMultiplyAdd(Unsafe.Add(ref bRef, 8) - Unsafe.Add(ref aRef, 8), amount, Unsafe.Add(ref aRef, 8));
            return result;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> amt128 = Vector128.Create(amount);
            if (SimdRow32.IsFusedMultiplyAddSupported)
            {
                SimdRow32.LerpRowFused(Vector128.LoadUnsafe(ref aRef, 0), Vector128.LoadUnsafe(ref bRef, 0), amt128).StoreUnsafe(ref dst, 0);
                SimdRow32.LerpRowFused(Vector128.LoadUnsafe(ref aRef, 4), Vector128.LoadUnsafe(ref bRef, 4), amt128).StoreUnsafe(ref dst, 4);
            }
            else
            {
                SimdRow32.LerpRowPlain(Vector128.LoadUnsafe(ref aRef, 0), Vector128.LoadUnsafe(ref bRef, 0), amt128).StoreUnsafe(ref dst, 0);
                SimdRow32.LerpRowPlain(Vector128.LoadUnsafe(ref aRef, 4), Vector128.LoadUnsafe(ref bRef, 4), amt128).StoreUnsafe(ref dst, 4);
            }
            Unsafe.Add(ref dst, 8) = MathF.FusedMultiplyAdd(Unsafe.Add(ref bRef, 8) - Unsafe.Add(ref aRef, 8), amount, Unsafe.Add(ref aRef, 8));
            return result;
        }

        for (nuint i = 0; i < 9; i++)
        {
            Unsafe.Add(ref dst, i) = MathF.FusedMultiplyAdd(Unsafe.Add(ref bRef, i) - Unsafe.Add(ref aRef, i), amount, Unsafe.Add(ref aRef, i));
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool Equals(Mat3 other, float tolerance = DefaultTolerance)
    {
        ref float a = ref Unsafe.AsRef(in M11);
        ref float b = ref Unsafe.AsRef(in other.M11);

        for (nuint i = 0; i < 9; i++)
        {
            if (MathF.Abs(Unsafe.Add(ref a, i) - Unsafe.Add(ref b, i)) > tolerance)
            {
                return false;
            }
        }
        return true;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 CreateTranslation(Vec2 position) =>
        new(
            1.0f, 0.0f, 0.0f,
            0.0f, 1.0f, 0.0f,
            position.X, position.Y, 1.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 CreateTranslation(float x, float y) =>
        new(
            1.0f, 0.0f, 0.0f,
            0.0f, 1.0f, 0.0f,
            x, y, 1.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 CreateRotationX(AngleRadians angle)
    {
        (float sin, float cos) = MathF.SinCos(angle.Value);
        return new Mat3(
            1.0f, 0.0f, 0.0f,
            0.0f, cos, sin,
            0.0f, -sin, cos
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 CreateRotationX(float angleRadians) => CreateRotationX(new AngleRadians(angleRadians));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 CreateRotationY(AngleRadians angle)
    {
        (float sin, float cos) = MathF.SinCos(angle.Value);
        return new Mat3(
            cos, 0.0f, -sin,
            0.0f, 1.0f, 0.0f,
            sin, 0.0f, cos
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 CreateRotationY(float angleRadians) => CreateRotationY(new AngleRadians(angleRadians));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 CreateRotationZ(AngleRadians angle)
    {
        (float sin, float cos) = MathF.SinCos(angle.Value);
        return new Mat3(
            cos, sin, 0.0f,
            -sin, cos, 0.0f,
            0.0f, 0.0f, 1.0f
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 CreateRotationZ(float angleRadians) => CreateRotationZ(new AngleRadians(angleRadians));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 CreateFromYawPitchRoll(AngleRadians yaw, AngleRadians pitch, AngleRadians roll) =>
        CreateFromQuaternion(Quat.CreateFromYawPitchRoll(yaw, pitch, roll));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 CreateFromYawPitchRoll(float yaw, float pitch, float roll) =>
        CreateFromYawPitchRoll(new AngleRadians(yaw), new AngleRadians(pitch), new AngleRadians(roll));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly Mat4 ToMat4() =>
        new(
            M11, M12, M13, 0.0f,
            M21, M22, M23, 0.0f,
            M31, M32, M33, 0.0f,
            0.0f, 0.0f, 0.0f, 1.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat3 FromMat4(in Mat4 matrix) =>
        new(
            matrix.M11, matrix.M12, matrix.M13,
            matrix.M21, matrix.M22, matrix.M23,
            matrix.M31, matrix.M32, matrix.M33
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly void Apply<TAction, TState>(ref TState state)
        where TAction : struct, IMat3TransformAction<TState>
        where TState : allows ref struct
    {
        TAction.Execute(in this, ref state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Mat3ElementEnumerator GetEnumerator() => new(in this);

    public Mat3RowEnumerable Rows
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => new(in this);
    }

    public Mat3ColumnEnumerable Columns
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => new(in this);
    }

    public override readonly string ToString() => ToString(null, CultureInfo.InvariantCulture);

    public readonly string ToString(string? format) => ToString(format, CultureInfo.InvariantCulture);

    public readonly string ToString(string? format, IFormatProvider? formatProvider)
    {
        Span<char> buffer = stackalloc char[320];
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
        Span<char> temp = stackalloc char[320];
        int written = 0;

        temp[written++] = '{';
        temp[written++] = ' ';

        NumberFormatInfo nfi = NumberFormatInfo.GetInstance(provider);
        ReadOnlySpan<char> separator = nfi.NumberDecimalSeparator == "," ? "; " : ", ";

        ref float baseRef = ref Unsafe.AsRef(in M11);
        for (nuint i = 0; i < 9; i++)
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
        Span<byte> temp = stackalloc byte[320];
        int written = 0;

        temp[written++] = (byte)'{';
        temp[written++] = (byte)' ';

        NumberFormatInfo nfi = NumberFormatInfo.GetInstance(provider);
        ReadOnlySpan<byte> separator = nfi.NumberDecimalSeparator == "," ? "; "u8 : ", "u8;

        ref float baseRef = ref Unsafe.AsRef(in M11);
        for (nuint i = 0; i < 9; i++)
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

    public static Mat3 Parse(string s, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan(), provider);
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Mat3 result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }
        return TryParse(s.AsSpan(), provider, out result);
    }

    public static Mat3 Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out Mat3 result))
        {
            NumericThrowHelper.ThrowFormatException("Invalid Mat3 format string.");
        }
        return result;
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Mat3 result)
    {
        result = default;
        ReadOnlySpan<char> remaining = s;
        Unsafe.SkipInit(out Mat3 m);
        ref float baseRef = ref m.M11;
        int count = 0;

        while (!remaining.IsEmpty && count < 9)
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

        if (count == 9)
        {
            result = m;
            return true;
        }

        return false;
    }

    public static Mat3 Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider)
    {
        if (!TryParse(utf8Text, provider, out Mat3 result))
        {
            NumericThrowHelper.ThrowFormatException("Invalid Mat3 UTF-8 format stream.");
        }
        return result;
    }

    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Mat3 result)
    {
        result = default;
        ReadOnlySpan<byte> remaining = utf8Text;
        Unsafe.SkipInit(out Mat3 m);
        ref float baseRef = ref m.M11;
        int count = 0;

        while (!remaining.IsEmpty && count < 9)
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

        if (count == 9)
        {
            result = m;
            return true;
        }

        return false;
    }
}

public interface IMat3TransformAction<TState> where TState : allows ref struct
{
    static abstract void Execute(ref readonly Mat3 matrix, ref TState state);
}

public ref struct Mat3ElementEnumerator
{
    private readonly Mat3 _matrix;
    private int _index;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal Mat3ElementEnumerator(scoped ref readonly Mat3 matrix)
    {
        _matrix = matrix;
        _index = -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool MoveNext() => ++_index < 9;

    public readonly float Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            if ((uint)_index >= 9U)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(_index));
            }

            return Unsafe.Add(ref Unsafe.AsRef(in _matrix.M11), (nuint)(uint)_index);
        }
    }
}

public ref struct Mat3RowEnumerable
{
    private readonly Mat3 _matrix;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal Mat3RowEnumerable(scoped ref readonly Mat3 matrix) => _matrix = matrix;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Mat3RowEnumerator GetEnumerator() => new(in _matrix);
}

public ref struct Mat3RowEnumerator
{
    private readonly Mat3 _matrix;
    private int _index;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal Mat3RowEnumerator(scoped ref readonly Mat3 matrix)
    {
        _matrix = matrix;
        _index = -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool MoveNext() => ++_index < 3;

    public readonly Vec3 Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => _index switch
        {
            0 => _matrix.Row0,
            1 => _matrix.Row1,
            _ => _matrix.Row2
        };
    }
}

public ref struct Mat3ColumnEnumerable
{
    private readonly Mat3 _matrix;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal Mat3ColumnEnumerable(scoped ref readonly Mat3 matrix) => _matrix = matrix;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Mat3ColumnEnumerator GetEnumerator() => new(in _matrix);
}

public ref struct Mat3ColumnEnumerator
{
    private readonly Mat3 _matrix;
    private int _index;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal Mat3ColumnEnumerator(scoped ref readonly Mat3 matrix)
    {
        _matrix = matrix;
        _index = -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool MoveNext() => ++_index < 3;

    public readonly Vec3 Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => _index switch
        {
            0 => _matrix.Column0,
            1 => _matrix.Column1,
            _ => _matrix.Column2
        };
    }
}
