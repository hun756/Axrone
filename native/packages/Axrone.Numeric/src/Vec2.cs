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

    public static float Dot(Vec2 left, Vec2 right) =>
        MathF.FusedMultiplyAdd(left.X, right.X, left.Y * right.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DotStrict(Vec2 left, Vec2 right) =>
        (left.X * right.X) + (left.Y * right.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Cross(Vec2 left, Vec2 right) =>
        (left.X * right.Y) - (left.Y * right.X);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float LengthSquared() => Dot(this, this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float Length() => MathF.Sqrt(LengthSquared());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceSquared(Vec2 value1, Vec2 value2)
    {
        float dx = value1.X - value2.X;
        float dy = value1.Y - value2.Y;
        return (dx * dx) + (dy * dy);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Distance(Vec2 value1, Vec2 value2) =>
        MathF.Sqrt(DistanceSquared(value1, value2));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Normalize(Vec2 value) => Normalize<StrictIeeeStrategy>(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Normalize<TStrategy>(Vec2 value)
        where TStrategy : struct, INormalizationStrategy
    {
        float lengthSquared = value.LengthSquared();
        if (lengthSquared > 0.0f)
        {
            float invLength = TStrategy.ReciprocalSqrt(lengthSquared);
            if (float.IsFinite(invLength) && invLength > 0.0f)
            {
                return new Vec2(value.X * invLength, value.Y * invLength);
            }
            float len = MathF.Sqrt(lengthSquared);
            if (len > 0.0f)
            {
                return new Vec2(value.X / len, value.Y / len);
            }
        }
        return Zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryNormalize(Vec2 value, out Vec2 result) =>
        TryNormalize(value, out result, 0.0f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryNormalize(Vec2 value, out Vec2 result, float tolerance)
    {
        float lengthSquared = value.LengthSquared();
        float tolSquared = tolerance > 0.0f ? tolerance * tolerance : 0.0f;
        if (lengthSquared > tolSquared && lengthSquared > 0.0f)
        {
            float invLength = 1.0f / MathF.Sqrt(lengthSquared);
            if (float.IsFinite(invLength))
            {
                result = new Vec2(value.X * invLength, value.Y * invLength);
                return true;
            }
            float len = MathF.Sqrt(lengthSquared);
            if (len > 0.0f)
            {
                result = new Vec2(value.X / len, value.Y / len);
                return true;
            }
        }
        result = Zero;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Reflect(Vec2 vector, Vec2 normal)
    {
        float dot2 = Dot(vector, normal) * 2.0f;
        return new Vec2(
            vector.X - (normal.X * dot2),
            vector.Y - (normal.Y * dot2)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Project(Vec2 vector, Vec2 onNormal)
    {
        float sqrMag = Dot(onNormal, onNormal);
        if (sqrMag <= 0.0f)
        {
            return Zero;
        }
        float scale = Dot(vector, onNormal) / sqrMag;
        return onNormal * scale;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Slide(Vec2 vector, Vec2 normal)
    {
        float normalSq = Dot(normal, normal);
        if (normalSq <= 0.0f)
        {
            return vector;
        }
        return vector - (normal * (Dot(vector, normal) / normalSq));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Perpendicular(Vec2 vector) => new(-vector.Y, vector.X);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec2 Perpendicular() => Perpendicular(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 PerpendicularClockwise(Vec2 vector) => new(vector.Y, -vector.X);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec2 PerpendicularClockwise() => PerpendicularClockwise(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Rotate(Vec2 vector, float angleRadians)
    {
        var (sin, cos) = MathF.SinCos(angleRadians);
        return new Vec2(
            MathF.FusedMultiplyAdd(vector.X, cos, -(vector.Y * sin)),
            MathF.FusedMultiplyAdd(vector.X, sin, vector.Y * cos)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec2 Rotate(float angleRadians) => Rotate(this, angleRadians);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 RotateAround(Vec2 vector, Vec2 pivot, float angleRadians) =>
        Rotate(vector - pivot, angleRadians) + pivot;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec2 RotateAround(Vec2 pivot, float angleRadians) => RotateAround(this, pivot, angleRadians);

    public static float Angle(Vec2 from, Vec2 to)
    {
        float maxA = MathF.Max(MathF.Abs(from.X), MathF.Abs(from.Y));
        float maxB = MathF.Max(MathF.Abs(to.X), MathF.Abs(to.Y));
        if (maxA <= 0.0f || maxB <= 0.0f)
        {
            return 0.0f;
        }

        Vec2 a = from * (1.0f / maxA);
        Vec2 b = to * (1.0f / maxB);

        float lenA = a.Length();
        float lenB = b.Length();
        if (lenA <= 0.0f || lenB <= 0.0f)
        {
            return 0.0f;
        }

        float cos = Dot(a, b) / (lenA * lenB);
        return MathF.Acos(Math.Clamp(cos, -1.0f, 1.0f));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SignedAngle(Vec2 from, Vec2 to) =>
        MathF.Atan2(Cross(from, to), Dot(from, to));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Clamp(Vec2 value, Vec2 min, Vec2 max)
    {
        float x = value.X;
        x = (x < min.X) ? min.X : x;
        x = (x > max.X) ? max.X : x;

        float y = value.Y;
        y = (y < min.Y) ? min.Y : y;
        y = (y > max.Y) ? max.Y : y;

        return new Vec2(x, y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 ClampNative(Vec2 value, Vec2 min, Vec2 max) => Clamp(value, min, max);

    public static Vec2 ClampLength(Vec2 value, float minLength, float maxLength)
    {
        float sqrMagnitude = value.LengthSquared();
        if (sqrMagnitude <= 0.0f)
        {
            return minLength > 0.0f ? new Vec2(minLength, 0.0f) : Zero;
        }

        float magnitude = MathF.Sqrt(sqrMagnitude);
        if (magnitude < minLength)
        {
            return value * (minLength / magnitude);
        }
        if (magnitude > maxLength)
        {
            return value * (maxLength / magnitude);
        }
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Min(Vec2 left, Vec2 right) =>
        new(MathF.Min(left.X, right.X), MathF.Min(left.Y, right.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Max(Vec2 left, Vec2 right) =>
        new(MathF.Max(left.X, right.X), MathF.Max(left.Y, right.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 MinNative(Vec2 left, Vec2 right) => Min(left, right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 MaxNative(Vec2 left, Vec2 right) => Max(left, right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 MinNumber(Vec2 left, Vec2 right) =>
        new(float.MinNumber(left.X, right.X), float.MinNumber(left.Y, right.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 MaxNumber(Vec2 left, Vec2 right) =>
        new(float.MaxNumber(left.X, right.X), float.MaxNumber(left.Y, right.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 MinMagnitude(Vec2 left, Vec2 right) =>
        new(float.MinMagnitude(left.X, right.X), float.MinMagnitude(left.Y, right.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 MaxMagnitude(Vec2 left, Vec2 right) =>
        new(float.MaxMagnitude(left.X, right.X), float.MaxMagnitude(left.Y, right.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 MinMagnitudeNumber(Vec2 left, Vec2 right) =>
        new(float.MinMagnitudeNumber(left.X, right.X), float.MinMagnitudeNumber(left.Y, right.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 MaxMagnitudeNumber(Vec2 left, Vec2 right) =>
        new(float.MaxMagnitudeNumber(left.X, right.X), float.MaxMagnitudeNumber(left.Y, right.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 CopySign(Vec2 value, Vec2 sign) =>
        new(MathF.CopySign(value.X, sign.X), MathF.CopySign(value.Y, sign.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 FusedMultiplyAdd(Vec2 left, Vec2 right, Vec2 addend) =>
        new(
            MathF.FusedMultiplyAdd(left.X, right.X, addend.X),
            MathF.FusedMultiplyAdd(left.Y, right.Y, addend.Y)
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 MultiplyAddEstimate(Vec2 left, Vec2 right, Vec2 addend) =>
        FusedMultiplyAdd(left, right, addend);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Abs(Vec2 value) =>
        new(MathF.Abs(value.X), MathF.Abs(value.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Sqrt(Vec2 value) =>
        new(MathF.Sqrt(value.X), MathF.Sqrt(value.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 SquareRoot(Vec2 value) => Sqrt(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Sin(Vec2 vector) =>
        new(MathF.Sin(vector.X), MathF.Sin(vector.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Cos(Vec2 vector) =>
        new(MathF.Cos(vector.X), MathF.Cos(vector.Y));

    public static (Vec2 Sin, Vec2 Cos) SinCos(Vec2 vector)
    {
        var (sX, cX) = MathF.SinCos(vector.X);
        var (sY, cY) = MathF.SinCos(vector.Y);
        return (new Vec2(sX, sY), new Vec2(cX, cY));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Exp(Vec2 vector) =>
        new(MathF.Exp(vector.X), MathF.Exp(vector.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Log(Vec2 vector) =>
        new(MathF.Log(vector.X), MathF.Log(vector.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Log2(Vec2 vector) =>
        new(MathF.Log2(vector.X), MathF.Log2(vector.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Hypot(Vec2 x, Vec2 y) => Sqrt((x * x) + (y * y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 DegreesToRadians(Vec2 degrees) => degrees * (MathF.PI / 180.0f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 RadiansToDegrees(Vec2 radians) => radians * (180.0f / MathF.PI);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Floor(Vec2 value) =>
        new(MathF.Floor(value.X), MathF.Floor(value.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Ceiling(Vec2 value) =>
        new(MathF.Ceiling(value.X), MathF.Ceiling(value.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Round(Vec2 value) =>
        new(MathF.Round(value.X), MathF.Round(value.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Truncate(Vec2 value) =>
        new(MathF.Truncate(value.X), MathF.Truncate(value.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Lerp(Vec2 a, Vec2 b, float t) =>
        new(
            MathF.FusedMultiplyAdd(b.X - a.X, t, a.X),
            MathF.FusedMultiplyAdd(b.Y - a.Y, t, a.Y)
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 LerpClamped(Vec2 a, Vec2 b, float t) =>
        Lerp(a, b, Math.Clamp(t, 0.0f, 1.0f));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 SmoothStep(Vec2 from, Vec2 to, float amount)
    {
        amount = Math.Clamp(amount, 0.0f, 1.0f);
        float factor = amount * amount * (3.0f - (2.0f * amount));
        return Lerp(from, to, factor);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 SmootherStep(Vec2 from, Vec2 to, float amount)
    {
        amount = Math.Clamp(amount, 0.0f, 1.0f);
        float inner = MathF.FusedMultiplyAdd(amount, 6.0f, -15.0f);
        float poly = MathF.FusedMultiplyAdd(amount, inner, 10.0f);
        float factor = amount * amount * amount * poly;
        return Lerp(from, to, factor);
    }

    public static Vec2 MoveTowards(Vec2 current, Vec2 target, float maxDistanceDelta)
    {
        float dx = target.X - current.X;
        float dy = target.Y - current.Y;
        float distSq = (dx * dx) + (dy * dy);

        if (distSq == 0.0f || (maxDistanceDelta >= 0.0f && distSq <= maxDistanceDelta * maxDistanceDelta))
        {
            return target;
        }

        float dist = MathF.Sqrt(distSq);
        float ratio = maxDistanceDelta / dist;
        return new Vec2(
            MathF.FusedMultiplyAdd(dx, ratio, current.X),
            MathF.FusedMultiplyAdd(dy, ratio, current.Y)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Inverse(Vec2 vector) =>
        new(1.0f / vector.X, 1.0f / vector.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec2 Inverse() => Inverse(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 InverseSafe(Vec2 vector, float fallback = 0.0f)
    {
        float invX = 1.0f / vector.X;
        float invY = 1.0f / vector.Y;
        return new Vec2(
            float.IsFinite(invX) ? invX : fallback,
            float.IsFinite(invY) ? invY : fallback
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec2 InverseSafe(float fallback = 0.0f) => InverseSafe(this, fallback);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 DivideSafe(Vec2 left, Vec2 right, float fallback = 0.0f)
    {
        float x = left.X / right.X;
        float y = left.Y / right.Y;
        return new Vec2(
            float.IsFinite(x) ? x : fallback,
            float.IsFinite(y) ? y : fallback
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 DivideSafe(Vec2 left, float right, float fallback = 0.0f)
    {
        float inv = 1.0f / right;
        return float.IsFinite(inv) ? left * inv : new Vec2(fallback);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ManhattanDistance(Vec2 a, Vec2 b) =>
        MathF.Abs(a.X - b.X) + MathF.Abs(a.Y - b.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float ManhattanDistance(Vec2 destination) => ManhattanDistance(this, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ChebyshevDistance(Vec2 a, Vec2 b) =>
        MathF.Max(MathF.Abs(a.X - b.X), MathF.Abs(a.Y - b.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float ChebyshevDistance(Vec2 destination) => ChebyshevDistance(this, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 AddScalar(Vec2 vector, float scalar) =>
        new(vector.X + scalar, vector.Y + scalar);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec2 AddScalar(float scalar) => AddScalar(this, scalar);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 SubtractScalar(Vec2 vector, float scalar) =>
        new(vector.X - scalar, vector.Y - scalar);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec2 SubtractScalar(float scalar) => SubtractScalar(this, scalar);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Transform(Vec2 position, System.Numerics.Matrix3x2 matrix) =>
        new(
            MathF.FusedMultiplyAdd(position.X, matrix.M11, MathF.FusedMultiplyAdd(position.Y, matrix.M21, matrix.M31)),
            MathF.FusedMultiplyAdd(position.X, matrix.M12, MathF.FusedMultiplyAdd(position.Y, matrix.M22, matrix.M32))
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 TransformNormal(Vec2 normal, System.Numerics.Matrix3x2 matrix) =>
        new(
            MathF.FusedMultiplyAdd(normal.X, matrix.M11, normal.Y * matrix.M21),
            MathF.FusedMultiplyAdd(normal.X, matrix.M12, normal.Y * matrix.M22)
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Transform(Vec2 position, System.Numerics.Matrix4x4 matrix) =>
        new(
            MathF.FusedMultiplyAdd(position.X, matrix.M11, MathF.FusedMultiplyAdd(position.Y, matrix.M21, matrix.M41)),
            MathF.FusedMultiplyAdd(position.X, matrix.M12, MathF.FusedMultiplyAdd(position.Y, matrix.M22, matrix.M42))
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2 Transform(Vec2 value, System.Numerics.Quaternion rotation)
    {
        float x2 = rotation.X + rotation.X;
        float y2 = rotation.Y + rotation.Y;
        float z2 = rotation.Z + rotation.Z;

        float wz2 = rotation.W * z2;
        float xx2 = rotation.X * x2;
        float xy2 = rotation.X * y2;
        float yy2 = rotation.Y * y2;
        float zz2 = rotation.Z * z2;

        return new Vec2(
            (value.X * (1.0f - yy2 - zz2)) + (value.Y * (xy2 - wz2)),
            (value.X * (xy2 + wz2)) + (value.Y * (1.0f - xx2 - zz2))
        );
    }
}
