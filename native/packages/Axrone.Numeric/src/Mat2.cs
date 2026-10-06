namespace Axrone.Numeric;

using Axrone.Simd;

public interface IReadOnlyMatrix2x2<TSelf>
    where TSelf : struct, IReadOnlyMatrix2x2<TSelf>
{
    #pragma warning disable CA1043
    float this[RowIndex row, ColumnIndex column] { get; }
    #pragma warning restore CA1043
    Vec2 Row0 { get; }
    Vec2 Row1 { get; }
    Vec2 Column0 { get; }
    Vec2 Column1 { get; }
    float Determinant();
    TSelf Transpose();
    bool Invert(out TSelf result);
    bool IsIdentity { get; }
    bool IsAllZero { get; }
    bool IsAllFinite { get; }
}

public interface IAffineTransformable2x2<TSelf>
    where TSelf : struct, IAffineTransformable2x2<TSelf>
{
    static abstract TSelf CreateScale(Vec2 scales);
    static abstract TSelf CreateRotation(AngleRadians angle);
}

public interface IMatrixStorage2x2<TSelf>
    where TSelf : struct, IMatrixStorage2x2<TSelf>
{
    ReadOnlySpan<float> AsSpan();
    void CopyTo(Span<float> destination);
    bool TryCopyTo(Span<float> destination);
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Mat2 :
    IEquatable<Mat2>,
    IEqualityOperators<Mat2, Mat2, bool>,
    IAdditionOperators<Mat2, Mat2, Mat2>,
    ISubtractionOperators<Mat2, Mat2, Mat2>,
    IMultiplyOperators<Mat2, Mat2, Mat2>,
    IMultiplyOperators<Mat2, float, Mat2>,
    IUnaryNegationOperators<Mat2, Mat2>,
    IUnaryPlusOperators<Mat2, Mat2>,
    IAdditiveIdentity<Mat2, Mat2>,
    IMultiplicativeIdentity<Mat2, Mat2>,
    IReadOnlyMatrix2x2<Mat2>,
    IAffineTransformable2x2<Mat2>,
    IMatrixStorage2x2<Mat2>
{
    public const float MachineEpsilon = 1.1920929E-07F;

    public const float DefaultTolerance = MachineEpsilon * 8F;

    public float M11;
    public float M12;
    public float M21;
    public float M22;

    public static Mat2 Identity => new(
        1.0f, 0.0f,
        0.0f, 1.0f
    );

    public static Mat2 Zero => default;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Mat2(
        float m11, float m12,
        float m21, float m22)
    {
        M11 = m11; M12 = m12;
        M21 = m21; M22 = m22;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Mat2(Vec2 row0, Vec2 row1)
    {
        M11 = row0.X; M12 = row0.Y;
        M21 = row1.X; M22 = row1.Y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Mat2(ReadOnlySpan<float> values)
    {
        if (values.Length < 4)
        {
            NumericThrowHelper.ThrowArgumentException(nameof(values), "Source span must contain at least 4 elements.");
        }
        ref float src = ref MemoryMarshal.GetReference(values);
        this = Unsafe.As<float, Mat2>(ref src);
    }

    #pragma warning disable CA1043
    public float this[RowIndex row, ColumnIndex column]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get
        {
            if (row.Value >= 2U || column.Value >= 2U)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(row));
            }
            return Unsafe.Add(ref Unsafe.AsRef(in M11), (nuint)(row.Value * 2 + column.Value));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set
        {
            if (row.Value >= 2U || column.Value >= 2U)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(row));
            }
            Unsafe.Add(ref M11, (nuint)(row.Value * 2 + column.Value)) = value;
        }
    }
    #pragma warning restore CA1043

    public float this[int row, int column]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get
        {
            if ((uint)row >= 2 || (uint)column >= 2)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(row));
            }
            return Unsafe.Add(ref Unsafe.AsRef(in M11), (nuint)((uint)row * 2 + (uint)column));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set
        {
            if ((uint)row >= 2 || (uint)column >= 2)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(row));
            }
            Unsafe.Add(ref M11, (nuint)((uint)row * 2 + (uint)column)) = value;
        }
    }

    public float this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get
        {
            if ((uint)index >= 4U)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
            }
            return Unsafe.Add(ref Unsafe.AsRef(in M11), (nuint)(uint)index);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set
        {
            if ((uint)index >= 4U)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
            }
            Unsafe.Add(ref M11, (nuint)(uint)index) = value;
        }
    }

    public Vec2 Row0
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get => new(M11, M12);
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set { M11 = value.X; M12 = value.Y; }
    }

    public Vec2 Row1
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get => new(M21, M22);
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set { M21 = value.X; M22 = value.Y; }
    }

    public Vec2 Column0
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get => new(M11, M21);
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set { M11 = value.X; M21 = value.Y; }
    }

    public Vec2 Column1
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get => new(M12, M22);
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set { M12 = value.X; M22 = value.Y; }
    }

    public readonly bool IsIdentity => M11 == 1.0f && M22 == 1.0f &&
        M12 == 0.0f && M21 == 0.0f;

    public readonly bool IsAllZero =>
        M11 == 0.0f && M12 == 0.0f &&
        M21 == 0.0f && M22 == 0.0f;

    public readonly bool IsAllFinite =>
        float.IsFinite(M11) && float.IsFinite(M12) &&
        float.IsFinite(M21) && float.IsFinite(M22);

    public readonly bool IsAnyNaN =>
        float.IsNaN(M11) || float.IsNaN(M12) ||
        float.IsNaN(M21) || float.IsNaN(M22);

    public readonly bool IsAnyInfinity =>
        float.IsInfinity(M11) || float.IsInfinity(M12) ||
        float.IsInfinity(M21) || float.IsInfinity(M22);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly ReadOnlySpan<float> AsSpan() =>
        MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in M11), 4);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Span<float> AsWritableSpan() =>
        MemoryMarshal.CreateSpan(ref M11, 4);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly void CopyTo(Span<float> destination)
    {
        if (destination.Length < 4)
        {
            NumericThrowHelper.ThrowArgumentException(nameof(destination), "Destination span must contain at least 4 elements.");
        }
        ref float dst = ref MemoryMarshal.GetReference(destination);
        Unsafe.As<float, Mat2>(ref dst) = this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool TryCopyTo(Span<float> destination)
    {
        if (destination.Length >= 4)
        {
            ref float dst = ref MemoryMarshal.GetReference(destination);
            Unsafe.As<float, Mat2>(ref dst) = this;
            return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly float Trace() => M11 + M22;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly Mat2 Transpose() =>
        new(
            M11, M21,
            M12, M22
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat2 Transpose(Mat2 matrix) => matrix.Transpose();

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly float Determinant() => (M11 * M22) - (M12 * M21);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool Invert(out Mat2 result)
    {
        float det = (M11 * M22) - (M12 * M21);

        if (MathF.Abs(det) <= 1e-30f)
        {
            result = default;
            return false;
        }

        float invDet = 1.0f / det;

        result = new Mat2(
            M22 * invDet,
            -M12 * invDet,
            -M21 * invDet,
            M11 * invDet
        );

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool Invert(Mat2 matrix, out Mat2 result) => matrix.Invert(out result);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat2 CreateScale(Vec2 scales) =>
        new(
            scales.X, 0.0f,
            0.0f, scales.Y
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat2 CreateScale(float scale) => CreateScale(new Vec2(scale));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat2 CreateScale(float xScale, float yScale) =>
        CreateScale(new Vec2(xScale, yScale));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat2 CreateRotation(AngleRadians angle)
    {
        (float sin, float cos) = MathF.SinCos(angle.Value);
        return new Mat2(
            cos, sin,
            -sin, cos
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat2 CreateRotation(float angleRadians) => CreateRotation(new AngleRadians(angleRadians));

    public static Mat2 AdditiveIdentity => Zero;
    public static Mat2 MultiplicativeIdentity => Identity;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat2 operator +(Mat2 left, Mat2 right)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            Unsafe.SkipInit(out Mat2 result);
            ref float l = ref Unsafe.AsRef(in left.M11);
            ref float r = ref Unsafe.AsRef(in right.M11);
            ref float dst = ref result.M11;
            (Vector128.LoadUnsafe(ref l) + Vector128.LoadUnsafe(ref r)).StoreUnsafe(ref dst);
            return result;
        }

        return new Mat2(
            left.M11 + right.M11, left.M12 + right.M12,
            left.M21 + right.M21, left.M22 + right.M22
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat2 operator -(Mat2 left, Mat2 right)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            Unsafe.SkipInit(out Mat2 result);
            ref float l = ref Unsafe.AsRef(in left.M11);
            ref float r = ref Unsafe.AsRef(in right.M11);
            ref float dst = ref result.M11;
            (Vector128.LoadUnsafe(ref l) - Vector128.LoadUnsafe(ref r)).StoreUnsafe(ref dst);
            return result;
        }

        return new Mat2(
            left.M11 - right.M11, left.M12 - right.M12,
            left.M21 - right.M21, left.M22 - right.M22
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat2 operator *(Mat2 a, Mat2 b) => Multiply(in a, in b);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat2 Multiply(in Mat2 left, in Mat2 right)
    {
        Multiply(in left, in right, out Mat2 result);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Multiply(in Mat2 left, in Mat2 right, out Mat2 result)
    {
        float l11 = left.M11, l12 = left.M12;
        float l21 = left.M21, l22 = left.M22;

        float r11 = right.M11, r12 = right.M12;
        float r21 = right.M21, r22 = right.M22;

        result = new Mat2(
            ((l11 * r11) + (l12 * r21)),
            ((l11 * r12) + (l12 * r22)),
            ((l21 * r11) + (l22 * r21)),
            ((l21 * r12) + (l22 * r22))
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat2 operator *(Mat2 left, float scalar)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            Unsafe.SkipInit(out Mat2 result);
            ref float l = ref Unsafe.AsRef(in left.M11);
            ref float dst = ref result.M11;
            (Vector128.LoadUnsafe(ref l) * Vector128.Create(scalar)).StoreUnsafe(ref dst);
            return result;
        }

        return new Mat2(
            left.M11 * scalar, left.M12 * scalar,
            left.M21 * scalar, left.M22 * scalar
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat2 operator *(float scalar, Mat2 matrix) => matrix * scalar;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec2 operator *(Mat2 matrix, Vec2 vector) =>
        new(
            MathF.FusedMultiplyAdd(vector.X, matrix.M11, vector.Y * matrix.M21),
            MathF.FusedMultiplyAdd(vector.X, matrix.M12, vector.Y * matrix.M22)
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec2 operator *(Vec2 vector, Mat2 matrix) => matrix * vector;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat2 operator -(Mat2 value) => value * -1.0f;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat2 operator +(Mat2 value) => value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat2 Add(in Mat2 left, in Mat2 right) => left + right;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat2 Subtract(in Mat2 left, in Mat2 right) => left - right;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat2 Multiply(in Mat2 left, float scalar) => left * scalar;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat2 Negate(Mat2 value) => -value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Mat2 Lerp(Mat2 a, Mat2 b, float amount)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            Unsafe.SkipInit(out Mat2 result);
            ref float aRef = ref Unsafe.AsRef(in a.M11);
            ref float bRef = ref Unsafe.AsRef(in b.M11);
            ref float dst = ref result.M11;
            Vector128<float> amt = Vector128.Create(amount);

            (SimdRow32.IsFusedMultiplyAddSupported
                ? SimdRow32.LerpRowFused(Vector128.LoadUnsafe(ref aRef), Vector128.LoadUnsafe(ref bRef), amt)
                : SimdRow32.LerpRowPlain(Vector128.LoadUnsafe(ref aRef), Vector128.LoadUnsafe(ref bRef), amt)
            ).StoreUnsafe(ref dst);
            return result;
        }

        Unsafe.SkipInit(out Mat2 scalar);
        ref float sa = ref Unsafe.AsRef(in a.M11);
        ref float sb = ref Unsafe.AsRef(in b.M11);
        ref float dd = ref scalar.M11;
        for (nuint i = 0; i < 4; i++)
        {
            Unsafe.Add(ref dd, i) = MathF.FusedMultiplyAdd(Unsafe.Add(ref sb, i) - Unsafe.Add(ref sa, i), amount, Unsafe.Add(ref sa, i));
        }
        return scalar;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool Equals(Mat2 other, float tolerance = DefaultTolerance)
    {
        ref float a = ref Unsafe.AsRef(in M11);
        ref float b = ref Unsafe.AsRef(in other.M11);

        for (nuint i = 0; i < 4; i++)
        {
            if (MathF.Abs(Unsafe.Add(ref a, i) - Unsafe.Add(ref b, i)) > tolerance)
            {
                return false;
            }
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator ==(Mat2 left, Mat2 right)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            ref float l = ref Unsafe.AsRef(in left.M11);
            ref float r = ref Unsafe.AsRef(in right.M11);
            return Vector128.EqualsAll(Vector128.LoadUnsafe(ref l), Vector128.LoadUnsafe(ref r));
        }

        return left.M11 == right.M11 && left.M12 == right.M12 &&
            left.M21 == right.M21 && left.M22 == right.M22;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator !=(Mat2 left, Mat2 right) => !(left == right);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool Equals(Mat2 other) => this == other;

    public override readonly bool Equals(object? obj) =>
        obj is Mat2 other && Equals(other);

    public override readonly int GetHashCode()
    {
        HashCode hash = default;
        ref float self = ref Unsafe.AsRef(in M11);
        for (nuint i = 0; i < 4; i++)
        {
            hash.Add(Unsafe.Add(ref self, i));
        }
        return hash.ToHashCode();
    }
}
