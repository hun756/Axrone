namespace Axrone.Numeric;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Vec4 :
    IEquatable<Vec4>,
    IEqualityOperators<Vec4, Vec4, bool>,
    IAdditionOperators<Vec4, Vec4, Vec4>,
    ISubtractionOperators<Vec4, Vec4, Vec4>,
    IMultiplyOperators<Vec4, Vec4, Vec4>,
    IMultiplyOperators<Vec4, float, Vec4>,
    IDivisionOperators<Vec4, Vec4, Vec4>,
    IDivisionOperators<Vec4, float, Vec4>,
    IBitwiseOperators<Vec4, Vec4, Vec4>,
    IUnaryNegationOperators<Vec4, Vec4>,
    IUnaryPlusOperators<Vec4, Vec4>
{
    public float X;
    public float Y;
    public float Z;
    public float W;

    public const float MachineEpsilon = 1.192092896e-07f;
    public const float DefaultTolerance = 1e-6f;

    public static Vec4 Zero => default;
    public static Vec4 One => new(1.0f, 1.0f, 1.0f, 1.0f);
    public static Vec4 UnitX => new(1.0f, 0.0f, 0.0f, 0.0f);
    public static Vec4 UnitY => new(0.0f, 1.0f, 0.0f, 0.0f);
    public static Vec4 UnitZ => new(0.0f, 0.0f, 1.0f, 0.0f);
    public static Vec4 UnitW => new(0.0f, 0.0f, 0.0f, 1.0f);
    public static Vec4 NegativeUnitX => new(-1.0f, 0.0f, 0.0f, 0.0f);
    public static Vec4 NegativeUnitY => new(0.0f, -1.0f, 0.0f, 0.0f);
    public static Vec4 NegativeUnitZ => new(0.0f, 0.0f, -1.0f, 0.0f);
    public static Vec4 NegativeUnitW => new(0.0f, 0.0f, 0.0f, -1.0f);
    public static Vec4 PositiveInfinity => new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
    public static Vec4 NegativeInfinity => new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
    public static Vec4 NaN => new(float.NaN, float.NaN, float.NaN, float.NaN);
    public static Vec4 Epsilon => new(MachineEpsilon, MachineEpsilon, MachineEpsilon, MachineEpsilon);

    public static Vec4 Pi => new(MathF.PI, MathF.PI, MathF.PI, MathF.PI);
    public static Vec4 Tau => new(MathF.Tau, MathF.Tau, MathF.Tau, MathF.Tau);
    public static Vec4 E => new(MathF.E, MathF.E, MathF.E, MathF.E);
    public static Vec4 NegativeZero => new(-0.0f, -0.0f, -0.0f, -0.0f);
    public static Vec4 AllBitsSet => new(
        BitConverter.UInt32BitsToSingle(0xFFFF_FFFF),
        BitConverter.UInt32BitsToSingle(0xFFFF_FFFF),
        BitConverter.UInt32BitsToSingle(0xFFFF_FFFF),
        BitConverter.UInt32BitsToSingle(0xFFFF_FFFF)
    );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec4(float value) : this(value, value, value, value) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec4(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec4(Vec2 value, float z, float w) : this(value.X, value.Y, z, w) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec4(Vec3 value, float w) : this(value.X, value.Y, value.Z, w) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec4(ReadOnlySpan<float> values)
    {
        if (values.Length < 4)
        {
            NumericThrowHelper.ThrowArgumentException(nameof(values), "Source span must contain at least 4 elements.");
        }
        X = values[0];
        Y = values[1];
        Z = values[2];
        W = values[3];
    }

    public float this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get
        {
            if ((uint)index >= 4)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
            }
            return Unsafe.Add(ref Unsafe.AsRef(in X), (nuint)(uint)index);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if ((uint)index >= 4)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
            }
            Unsafe.Add(ref X, (nuint)(uint)index) = value;
        }
    }

    public readonly bool IsAllZero => X == 0.0f && Y == 0.0f && Z == 0.0f && W == 0.0f;
    public readonly bool IsAllFinite => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z) && float.IsFinite(W);
    public readonly bool IsAnyNaN => float.IsNaN(X) || float.IsNaN(Y) || float.IsNaN(Z) || float.IsNaN(W);
    public readonly bool IsAnyInfinity => float.IsInfinity(X) || float.IsInfinity(Y) || float.IsInfinity(Z) || float.IsInfinity(W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Deconstruct(out float x, out float y, out float z, out float w)
    {
        x = X;
        y = Y;
        z = Z;
        w = W;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ReadOnlySpan<float> AsSpan() => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in X), 4);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(Span<float> destination)
    {
        if (destination.Length < 4)
        {
            NumericThrowHelper.ThrowArgumentException(nameof(destination), "Destination span is shorter than 4 elements.");
        }
        destination[0] = X;
        destination[1] = Y;
        destination[2] = Z;
        destination[3] = W;
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
        if (destination.Length - index < 4)
        {
            NumericThrowHelper.ThrowArgumentException(nameof(destination), "Destination array does not have enough capacity.");
        }
        destination[index] = X;
        destination[index + 1] = Y;
        destination[index + 2] = Z;
        destination[index + 3] = W;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool TryCopyTo(Span<float> destination)
    {
        if (destination.Length >= 4)
        {
            destination[0] = X;
            destination[1] = Y;
            destination[2] = Z;
            destination[3] = W;
            return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector128<float> AsVector128() => Vector128.Create(X, Y, Z, W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Vec4(Vector128<float> vector) =>
        new(vector.GetElement(0), vector.GetElement(1), vector.GetElement(2), vector.GetElement(3));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Vector128<float>(Vec4 vector) => vector.AsVector128();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator (float X, float Y, float Z, float W)(Vec4 value) => (value.X, value.Y, value.Z, value.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Vec4((float X, float Y, float Z, float W) value) => new(value.X, value.Y, value.Z, value.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator System.Numerics.Vector4(Vec4 v) =>
        Unsafe.BitCast<Vec4, System.Numerics.Vector4>(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Vec4(System.Numerics.Vector4 v) =>
        Unsafe.BitCast<System.Numerics.Vector4, Vec4>(v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly System.Numerics.Vector4 ToSystemNumerics() => (System.Numerics.Vector4)this;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 FromSystemNumerics(System.Numerics.Vector4 value) => (Vec4)value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Create(float value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Create(float x, float y, float z, float w) => new(x, y, z, w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Create(Vec2 value, float z, float w) => new(value, z, w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Create(Vec3 value, float w) => new(value, w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Create(ReadOnlySpan<float> values) => new(values);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 CreateScalar(float value) => new(value, 0.0f, 0.0f, 0.0f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 CreateScalarUnsafe(float value) => new(value, 0.0f, 0.0f, 0.0f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vec4 Load(float* source) => new(source[0], source[1], source[2], source[3]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vec4 LoadAligned(float* source) => new(source[0], source[1], source[2], source[3]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vec4 LoadAlignedNonTemporal(float* source) => new(source[0], source[1], source[2], source[3]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 LoadUnsafe(ref readonly float source) =>
        new(source, Unsafe.Add(ref Unsafe.AsRef(in source), 1), Unsafe.Add(ref Unsafe.AsRef(in source), 2), Unsafe.Add(ref Unsafe.AsRef(in source), 3));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 LoadUnsafe(ref readonly float source, nuint elementOffset) =>
        LoadUnsafe(ref Unsafe.Add(ref Unsafe.AsRef(in source), elementOffset));

    public static Vec4 SumAll(params ReadOnlySpan<Vec4> vectors)
    {
        Vec4 accumulator = Zero;
        for (int i = 0; i < vectors.Length; i++)
        {
            accumulator += vectors[i];
        }
        return accumulator;
    }

    public static Vec4 Average(params ReadOnlySpan<Vec4> vectors)
    {
        if (vectors.IsEmpty)
        {
            NumericThrowHelper.ThrowArgumentException(nameof(vectors), "Collection cannot be empty.");
        }
        return SumAll(vectors) / (float)vectors.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sum(Vec4 vector) => vector.X + vector.Y + vector.Z + vector.W;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool All(Vec4 vector) => vector.X != 0.0f && vector.Y != 0.0f && vector.Z != 0.0f && vector.W != 0.0f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AllWhereAllBitsSet(Vec4 vector) =>
        BitConverter.SingleToUInt32Bits(vector.X) == 0xFFFF_FFFF &&
        BitConverter.SingleToUInt32Bits(vector.Y) == 0xFFFF_FFFF &&
        BitConverter.SingleToUInt32Bits(vector.Z) == 0xFFFF_FFFF &&
        BitConverter.SingleToUInt32Bits(vector.W) == 0xFFFF_FFFF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Any(Vec4 vector) => vector.X != 0.0f || vector.Y != 0.0f || vector.Z != 0.0f || vector.W != 0.0f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AnyWhereAllBitsSet(Vec4 vector) =>
        BitConverter.SingleToUInt32Bits(vector.X) == 0xFFFF_FFFF ||
        BitConverter.SingleToUInt32Bits(vector.Y) == 0xFFFF_FFFF ||
        BitConverter.SingleToUInt32Bits(vector.Z) == 0xFFFF_FFFF ||
        BitConverter.SingleToUInt32Bits(vector.W) == 0xFFFF_FFFF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool None(Vec4 vector) => !Any(vector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NoneWhereAllBitsSet(Vec4 vector) => !AnyWhereAllBitsSet(vector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Count(Vec4 vector)
    {
        int count = 0;
        if (vector.X != 0.0f) count++;
        if (vector.Y != 0.0f) count++;
        if (vector.Z != 0.0f) count++;
        if (vector.W != 0.0f) count++;
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountWhereAllBitsSet(Vec4 vector)
    {
        int count = 0;
        if (BitConverter.SingleToUInt32Bits(vector.X) == 0xFFFF_FFFF) count++;
        if (BitConverter.SingleToUInt32Bits(vector.Y) == 0xFFFF_FFFF) count++;
        if (BitConverter.SingleToUInt32Bits(vector.Z) == 0xFFFF_FFFF) count++;
        if (BitConverter.SingleToUInt32Bits(vector.W) == 0xFFFF_FFFF) count++;
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsAll(Vec4 left, Vec4 right) => left == right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsAny(Vec4 left, Vec4 right) =>
        left.X == right.X || left.Y == right.Y || left.Z == right.Z || left.W == right.W;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 operator +(Vec4 left, Vec4 right) =>
        new(left.X + right.X, left.Y + right.Y, left.Z + right.Z, left.W + right.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 operator -(Vec4 left, Vec4 right) =>
        new(left.X - right.X, left.Y - right.Y, left.Z - right.Z, left.W - right.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 operator *(Vec4 left, Vec4 right) =>
        new(left.X * right.X, left.Y * right.Y, left.Z * right.Z, left.W * right.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 operator *(Vec4 left, float right) =>
        new(left.X * right, left.Y * right, left.Z * right, left.W * right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 operator *(float left, Vec4 right) => right * left;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 operator /(Vec4 left, Vec4 right) =>
        new(left.X / right.X, left.Y / right.Y, left.Z / right.Z, left.W / right.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 operator /(Vec4 left, float right) =>
        new(left.X / right, left.Y / right, left.Z / right, left.W / right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 operator -(Vec4 value) => new(-value.X, -value.Y, -value.Z, -value.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 operator +(Vec4 value) => value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 operator &(Vec4 left, Vec4 right) =>
        new(
            BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(left.X) & BitConverter.SingleToUInt32Bits(right.X)),
            BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(left.Y) & BitConverter.SingleToUInt32Bits(right.Y)),
            BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(left.Z) & BitConverter.SingleToUInt32Bits(right.Z)),
            BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(left.W) & BitConverter.SingleToUInt32Bits(right.W))
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 operator |(Vec4 left, Vec4 right) =>
        new(
            BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(left.X) | BitConverter.SingleToUInt32Bits(right.X)),
            BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(left.Y) | BitConverter.SingleToUInt32Bits(right.Y)),
            BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(left.Z) | BitConverter.SingleToUInt32Bits(right.Z)),
            BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(left.W) | BitConverter.SingleToUInt32Bits(right.W))
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 operator ^(Vec4 left, Vec4 right) =>
        new(
            BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(left.X) ^ BitConverter.SingleToUInt32Bits(right.X)),
            BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(left.Y) ^ BitConverter.SingleToUInt32Bits(right.Y)),
            BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(left.Z) ^ BitConverter.SingleToUInt32Bits(right.Z)),
            BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(left.W) ^ BitConverter.SingleToUInt32Bits(right.W))
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 operator ~(Vec4 value) =>
        new(
            BitConverter.UInt32BitsToSingle(~BitConverter.SingleToUInt32Bits(value.X)),
            BitConverter.UInt32BitsToSingle(~BitConverter.SingleToUInt32Bits(value.Y)),
            BitConverter.UInt32BitsToSingle(~BitConverter.SingleToUInt32Bits(value.Z)),
            BitConverter.UInt32BitsToSingle(~BitConverter.SingleToUInt32Bits(value.W))
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 BitwiseAnd(Vec4 left, Vec4 right) => left & right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 BitwiseOr(Vec4 left, Vec4 right) => left | right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Xor(Vec4 left, Vec4 right) => left ^ right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 OnesComplement(Vec4 value) => ~value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 AndNot(Vec4 left, Vec4 right) => left & ~right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vec4 left, Vec4 right) =>
        left.X == right.X && left.Y == right.Y && left.Z == right.Z && left.W == right.W;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vec4 left, Vec4 right) => !(left == right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Add(Vec4 left, Vec4 right) => left + right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Subtract(Vec4 left, Vec4 right) => left - right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Multiply(Vec4 left, Vec4 right) => left * right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Multiply(Vec4 left, float right) => left * right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Multiply(float left, Vec4 right) => left * right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Divide(Vec4 left, Vec4 right) => left / right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Divide(Vec4 left, float right) => left / right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Negate(Vec4 value) => -value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 IsNaN(Vec4 vector) =>
        new(
            float.IsNaN(vector.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsNaN(vector.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsNaN(vector.Z) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsNaN(vector.W) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 IsFinite(Vec4 vector) =>
        new(
            float.IsFinite(vector.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsFinite(vector.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsFinite(vector.Z) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsFinite(vector.W) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 IsInfinity(Vec4 vector) =>
        new(
            float.IsInfinity(vector.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsInfinity(vector.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsInfinity(vector.Z) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsInfinity(vector.W) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 IsNormal(Vec4 vector) =>
        new(
            float.IsNormal(vector.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsNormal(vector.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsNormal(vector.Z) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsNormal(vector.W) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 IsSubnormal(Vec4 vector) =>
        new(
            float.IsSubnormal(vector.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsSubnormal(vector.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsSubnormal(vector.Z) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsSubnormal(vector.W) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 IsZero(Vec4 vector) =>
        new(
            vector.X == 0.0f ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            vector.Y == 0.0f ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            vector.Z == 0.0f ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            vector.W == 0.0f ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 IsNegative(Vec4 vector) =>
        new(
            float.IsNegative(vector.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsNegative(vector.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsNegative(vector.Z) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsNegative(vector.W) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 IsPositive(Vec4 vector) =>
        new(
            float.IsPositive(vector.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsPositive(vector.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsPositive(vector.Z) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsPositive(vector.W) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 IsNegativeInfinity(Vec4 vector) =>
        new(
            float.IsNegativeInfinity(vector.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsNegativeInfinity(vector.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsNegativeInfinity(vector.Z) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsNegativeInfinity(vector.W) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 IsPositiveInfinity(Vec4 vector) =>
        new(
            float.IsPositiveInfinity(vector.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsPositiveInfinity(vector.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsPositiveInfinity(vector.Z) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsPositiveInfinity(vector.W) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 IsInteger(Vec4 vector) =>
        new(
            float.IsInteger(vector.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsInteger(vector.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsInteger(vector.Z) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsInteger(vector.W) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 IsEvenInteger(Vec4 vector) =>
        new(
            float.IsEvenInteger(vector.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsEvenInteger(vector.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsEvenInteger(vector.Z) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsEvenInteger(vector.W) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 IsOddInteger(Vec4 vector) =>
        new(
            float.IsOddInteger(vector.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsOddInteger(vector.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsOddInteger(vector.Z) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsOddInteger(vector.W) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(Vec4 other) => this == other;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(Vec4 other, float tolerance) =>
        MathF.Abs(X - other.X) <= tolerance &&
        MathF.Abs(Y - other.Y) <= tolerance &&
        MathF.Abs(Z - other.Z) <= tolerance &&
        MathF.Abs(W - other.W) <= tolerance;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Equals(Vec4 left, Vec4 right, float tolerance = DefaultTolerance) =>
        left.Equals(right, tolerance);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool BitEquals(Vec4 other) =>
        BitConverter.SingleToUInt32Bits(X) == BitConverter.SingleToUInt32Bits(other.X) &&
        BitConverter.SingleToUInt32Bits(Y) == BitConverter.SingleToUInt32Bits(other.Y) &&
        BitConverter.SingleToUInt32Bits(Z) == BitConverter.SingleToUInt32Bits(other.Z) &&
        BitConverter.SingleToUInt32Bits(W) == BitConverter.SingleToUInt32Bits(other.W);

    public override readonly bool Equals([NotNullWhen(true)] object? obj) =>
        obj is Vec4 other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(X, Y, Z, W);
}
