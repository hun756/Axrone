namespace Axrone.Numeric;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Vec2 :
    IEquatable<Vec2>,
    IEqualityOperators<Vec2, Vec2, bool>,
    IAdditionOperators<Vec2, Vec2, Vec2>,
    ISubtractionOperators<Vec2, Vec2, Vec2>,
    IMultiplyOperators<Vec2, Vec2, Vec2>,
    IMultiplyOperators<Vec2, float, Vec2>,
    IDivisionOperators<Vec2, Vec2, Vec2>,
    IDivisionOperators<Vec2, float, Vec2>,
    IBitwiseOperators<Vec2, Vec2, Vec2>,
    IUnaryNegationOperators<Vec2, Vec2>,
    IUnaryPlusOperators<Vec2, Vec2>
{
    public float X;
    public float Y;

    public const float MachineEpsilon = 1.192092896e-07f;
    public const float DefaultTolerance = 1e-6f;

    public static Vec2 Zero => default;
    public static Vec2 One => new(1.0f, 1.0f);
    public static Vec2 UnitX => new(1.0f, 0.0f);
    public static Vec2 UnitY => new(0.0f, 1.0f);
    public static Vec2 NegativeUnitX => new(-1.0f, 0.0f);
    public static Vec2 NegativeUnitY => new(0.0f, -1.0f);
    public static Vec2 PositiveInfinity => new(float.PositiveInfinity, float.PositiveInfinity);
    public static Vec2 NegativeInfinity => new(float.NegativeInfinity, float.NegativeInfinity);
    public static Vec2 NaN => new(float.NaN, float.NaN);
    public static Vec2 Epsilon => new(MachineEpsilon, MachineEpsilon);

    public static Vec2 Pi => new(MathF.PI, MathF.PI);
    public static Vec2 Tau => new(MathF.Tau, MathF.Tau);
    public static Vec2 E => new(MathF.E, MathF.E);
    public static Vec2 NegativeZero => new(-0.0f, -0.0f);
    public static Vec2 AllBitsSet => new(BitConverter.UInt32BitsToSingle(0xFFFF_FFFF), BitConverter.UInt32BitsToSingle(0xFFFF_FFFF));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec2(float value) : this(value, value) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec2(float x, float y)
    {
        X = x;
        Y = y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec2(ReadOnlySpan<float> values)
    {
        if (values.Length < 2)
        {
            NumericThrowHelper.ThrowArgumentException(nameof(values), "Source span must contain at least 2 elements.");
        }
        X = values[0];
        Y = values[1];
    }

    public float this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get
        {
            if ((uint)index >= 2)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
            }
            return Unsafe.Add(ref Unsafe.AsRef(in X), (nuint)(uint)index);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if ((uint)index >= 2)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
            }
            Unsafe.Add(ref X, (nuint)(uint)index) = value;
        }
    }

    public readonly bool IsAllZero => X == 0.0f && Y == 0.0f;
    public readonly bool IsAllFinite => float.IsFinite(X) && float.IsFinite(Y);
    public readonly bool IsAnyNaN => float.IsNaN(X) || float.IsNaN(Y);
    public readonly bool IsAnyInfinity => float.IsInfinity(X) || float.IsInfinity(Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Deconstruct(out float x, out float y)
    {
        x = X;
        y = Y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ReadOnlySpan<float> AsSpan() => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in X), 2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(Span<float> destination)
    {
        if (destination.Length < 2)
        {
            NumericThrowHelper.ThrowArgumentException(nameof(destination), "Destination span is shorter than 2 elements.");
        }
        destination[0] = X;
        destination[1] = Y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(float[] destination) => CopyTo(destination, 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(float[] destination, int index)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (index < 0 || index > destination.Length)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
        }
        if (destination.Length - index < 2)
        {
            NumericThrowHelper.ThrowArgumentException(nameof(destination), "Destination array does not have enough capacity.");
        }
        destination[index] = X;
        destination[index + 1] = Y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool TryCopyTo(Span<float> destination)
    {
        if (destination.Length >= 2)
        {
            destination[0] = X;
            destination[1] = Y;
            return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector64<float> AsVector64() => Vector64.Create(X, Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Vec2(Vector64<float> vector) =>
        new(vector.GetElement(0), vector.GetElement(1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Vector64<float>(Vec2 vector) => vector.AsVector64();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator (float X, float Y)(Vec2 value) => (value.X, value.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Vec2((float X, float Y) value) => new(value.X, value.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator System.Numerics.Vector2(Vec2 v) =>
        Unsafe.BitCast<Vec2, System.Numerics.Vector2>(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Vec2(System.Numerics.Vector2 v) =>
        Unsafe.BitCast<System.Numerics.Vector2, Vec2>(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly System.Numerics.Vector2 ToSystemNumerics() => (System.Numerics.Vector2)this;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 FromSystemNumerics(System.Numerics.Vector2 value) => (Vec2)value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Create(float value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Create(float x, float y) => new(x, y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Create(ReadOnlySpan<float> values) => new(values);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 CreateScalar(float value) => new(value, 0.0f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 CreateScalarUnsafe(float value) => new(value, 0.0f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vec2 Load(float* source) => new(source[0], source[1]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vec2 LoadAligned(float* source) => new(source[0], source[1]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vec2 LoadAlignedNonTemporal(float* source) => new(source[0], source[1]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 LoadUnsafe(ref readonly float source) =>
        new(source, Unsafe.Add(ref Unsafe.AsRef(in source), 1));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 LoadUnsafe(ref readonly float source, nuint elementOffset) =>
        LoadUnsafe(ref Unsafe.Add(ref Unsafe.AsRef(in source), elementOffset));

    public static Vec2 SumAll(params ReadOnlySpan<Vec2> vectors)
    {
        Vec2 accumulator = Zero;
        for (int i = 0; i < vectors.Length; i++)
        {
            accumulator += vectors[i];
        }
        return accumulator;
    }

    public static Vec2 Average(params ReadOnlySpan<Vec2> vectors)
    {
        if (vectors.IsEmpty)
        {
            NumericThrowHelper.ThrowArgumentException(nameof(vectors), "Collection cannot be empty.");
        }
        return SumAll(vectors) / (float)vectors.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sum(Vec2 vector) => vector.X + vector.Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool All(Vec2 vector) => vector.X != 0.0f && vector.Y != 0.0f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AllWhereAllBitsSet(Vec2 vector) =>
        BitConverter.SingleToUInt32Bits(vector.X) == 0xFFFF_FFFF &&
        BitConverter.SingleToUInt32Bits(vector.Y) == 0xFFFF_FFFF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Any(Vec2 vector) => vector.X != 0.0f || vector.Y != 0.0f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AnyWhereAllBitsSet(Vec2 vector) =>
        BitConverter.SingleToUInt32Bits(vector.X) == 0xFFFF_FFFF ||
        BitConverter.SingleToUInt32Bits(vector.Y) == 0xFFFF_FFFF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool None(Vec2 vector) => !Any(vector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NoneWhereAllBitsSet(Vec2 vector) => !AnyWhereAllBitsSet(vector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Count(Vec2 vector)
    {
        int count = 0;
        if (vector.X != 0.0f) count++;
        if (vector.Y != 0.0f) count++;
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountWhereAllBitsSet(Vec2 vector)
    {
        int count = 0;
        if (BitConverter.SingleToUInt32Bits(vector.X) == 0xFFFF_FFFF) count++;
        if (BitConverter.SingleToUInt32Bits(vector.Y) == 0xFFFF_FFFF) count++;
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsAll(Vec2 left, Vec2 right) => left == right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsAny(Vec2 left, Vec2 right) =>
        left.X == right.X || left.Y == right.Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 operator +(Vec2 left, Vec2 right) =>
        new(left.X + right.X, left.Y + right.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 operator -(Vec2 left, Vec2 right) =>
        new(left.X - right.X, left.Y - right.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 operator *(Vec2 left, Vec2 right) =>
        new(left.X * right.X, left.Y * right.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 operator *(Vec2 left, float right) =>
        new(left.X * right, left.Y * right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 operator *(float left, Vec2 right) => right * left;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 operator /(Vec2 left, Vec2 right) =>
        new(left.X / right.X, left.Y / right.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 operator /(Vec2 left, float right) =>
        new(left.X / right, left.Y / right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 operator -(Vec2 value) => new(-value.X, -value.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 operator +(Vec2 value) => value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 operator &(Vec2 left, Vec2 right) =>
        new(
            BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(left.X) & BitConverter.SingleToUInt32Bits(right.X)),
            BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(left.Y) & BitConverter.SingleToUInt32Bits(right.Y))
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 operator |(Vec2 left, Vec2 right) =>
        new(
            BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(left.X) | BitConverter.SingleToUInt32Bits(right.X)),
            BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(left.Y) | BitConverter.SingleToUInt32Bits(right.Y))
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 operator ^(Vec2 left, Vec2 right) =>
        new(
            BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(left.X) ^ BitConverter.SingleToUInt32Bits(right.X)),
            BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(left.Y) ^ BitConverter.SingleToUInt32Bits(right.Y))
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 operator ~(Vec2 value) =>
        new(
            BitConverter.UInt32BitsToSingle(~BitConverter.SingleToUInt32Bits(value.X)),
            BitConverter.UInt32BitsToSingle(~BitConverter.SingleToUInt32Bits(value.Y))
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 BitwiseAnd(Vec2 left, Vec2 right) => left & right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 BitwiseOr(Vec2 left, Vec2 right) => left | right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Xor(Vec2 left, Vec2 right) => left ^ right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 OnesComplement(Vec2 value) => ~value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 AndNot(Vec2 left, Vec2 right) => left & ~right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vec2 left, Vec2 right) =>
        left.X == right.X && left.Y == right.Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vec2 left, Vec2 right) => !(left == right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Add(Vec2 left, Vec2 right) => left + right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Subtract(Vec2 left, Vec2 right) => left - right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Multiply(Vec2 left, Vec2 right) => left * right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Multiply(Vec2 left, float right) => left * right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Multiply(float left, Vec2 right) => left * right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Divide(Vec2 left, Vec2 right) => left / right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Divide(Vec2 left, float right) => left / right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Negate(Vec2 value) => -value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 IsNaN(Vec2 vector) =>
        new(
            float.IsNaN(vector.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsNaN(vector.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 IsFinite(Vec2 vector) =>
        new(
            float.IsFinite(vector.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsFinite(vector.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 IsInfinity(Vec2 vector) =>
        new(
            float.IsInfinity(vector.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsInfinity(vector.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 IsNormal(Vec2 vector) =>
        new(
            float.IsNormal(vector.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsNormal(vector.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 IsSubnormal(Vec2 vector) =>
        new(
            float.IsSubnormal(vector.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsSubnormal(vector.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 IsZero(Vec2 vector) =>
        new(
            vector.X == 0.0f ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            vector.Y == 0.0f ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 IsNegative(Vec2 vector) =>
        new(
            float.IsNegative(vector.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsNegative(vector.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 IsPositive(Vec2 vector) =>
        new(
            float.IsPositive(vector.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsPositive(vector.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 IsNegativeInfinity(Vec2 vector) =>
        new(
            float.IsNegativeInfinity(vector.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsNegativeInfinity(vector.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 IsPositiveInfinity(Vec2 vector) =>
        new(
            float.IsPositiveInfinity(vector.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsPositiveInfinity(vector.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 IsInteger(Vec2 vector) =>
        new(
            float.IsInteger(vector.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsInteger(vector.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 IsEvenInteger(Vec2 vector) =>
        new(
            float.IsEvenInteger(vector.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsEvenInteger(vector.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 IsOddInteger(Vec2 vector) =>
        new(
            float.IsOddInteger(vector.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsOddInteger(vector.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(Vec2 other) => this == other;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(Vec2 other, float tolerance) =>
        MathF.Abs(X - other.X) <= tolerance &&
        MathF.Abs(Y - other.Y) <= tolerance;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Equals(Vec2 left, Vec2 right, float tolerance = DefaultTolerance) =>
        left.Equals(right, tolerance);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool BitEquals(Vec2 other) =>
        BitConverter.SingleToUInt32Bits(X) == BitConverter.SingleToUInt32Bits(other.X) &&
        BitConverter.SingleToUInt32Bits(Y) == BitConverter.SingleToUInt32Bits(other.Y);

    public override readonly bool Equals([NotNullWhen(true)] object? obj) =>
        obj is Vec2 other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(X, Y);
}
