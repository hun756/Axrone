namespace Axrone.Numeric;

public partial struct vec3
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Add(vec3 left, vec3 right) => left + right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Subtract(vec3 left, vec3 right) => left - right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Multiply(vec3 left, vec3 right) => left * right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Multiply(vec3 value, float scalar) => value * scalar;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Multiply(float scalar, vec3 value) => scalar * value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Divide(vec3 left, vec3 right) => left / right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Divide(vec3 value, float scalar) => value / scalar;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Negate(vec3 value) => -value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 BitwiseAnd(vec3 left, vec3 right) => left & right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 BitwiseOr(vec3 left, vec3 right) => left | right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Xor(vec3 left, vec3 right) => left ^ right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 OnesComplement(vec3 value) => ~value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 AndNot(vec3 left, vec3 right) => left & ~right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static vec3 Mask(bool x, bool y, bool z) => new(
        x ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFFu) : 0F,
        y ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFFu) : 0F,
        z ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFFu) : 0F);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsNaN(vec3 value) => Mask(float.IsNaN(value.X), float.IsNaN(value.Y), float.IsNaN(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsFinite(vec3 value) => Mask(float.IsFinite(value.X), float.IsFinite(value.Y), float.IsFinite(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsInfinity(vec3 value) => Mask(float.IsInfinity(value.X), float.IsInfinity(value.Y), float.IsInfinity(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsNormal(vec3 value) => Mask(float.IsNormal(value.X), float.IsNormal(value.Y), float.IsNormal(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsSubnormal(vec3 value) => Mask(float.IsSubnormal(value.X), float.IsSubnormal(value.Y), float.IsSubnormal(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsZero(vec3 value) => Mask(value.X == 0F, value.Y == 0F, value.Z == 0F);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsNegative(vec3 value) => Mask(float.IsNegative(value.X), float.IsNegative(value.Y), float.IsNegative(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsPositive(vec3 value) => Mask(float.IsPositive(value.X), float.IsPositive(value.Y), float.IsPositive(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsNegativeInfinity(vec3 value) => Mask(float.IsNegativeInfinity(value.X), float.IsNegativeInfinity(value.Y), float.IsNegativeInfinity(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsPositiveInfinity(vec3 value) => Mask(float.IsPositiveInfinity(value.X), float.IsPositiveInfinity(value.Y), float.IsPositiveInfinity(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsInteger(vec3 value) => Mask(float.IsInteger(value.X), float.IsInteger(value.Y), float.IsInteger(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsEvenInteger(vec3 value) => Mask(float.IsEvenInteger(value.X), float.IsEvenInteger(value.Y), float.IsEvenInteger(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsOddInteger(vec3 value) => Mask(float.IsOddInteger(value.X), float.IsOddInteger(value.Y), float.IsOddInteger(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 GreaterThan(vec3 left, vec3 right) => Mask(left.X > right.X, left.Y > right.Y, left.Z > right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GreaterThanAll(vec3 left, vec3 right) => left.X > right.X && left.Y > right.Y && left.Z > right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GreaterThanAny(vec3 left, vec3 right) => left.X > right.X || left.Y > right.Y || left.Z > right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 GreaterThanOrEqual(vec3 left, vec3 right) => Mask(left.X >= right.X, left.Y >= right.Y, left.Z >= right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GreaterThanOrEqualAll(vec3 left, vec3 right) => left.X >= right.X && left.Y >= right.Y && left.Z >= right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GreaterThanOrEqualAny(vec3 left, vec3 right) => left.X >= right.X || left.Y >= right.Y || left.Z >= right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 LessThan(vec3 left, vec3 right) => Mask(left.X < right.X, left.Y < right.Y, left.Z < right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool LessThanAll(vec3 left, vec3 right) => left.X < right.X && left.Y < right.Y && left.Z < right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool LessThanAny(vec3 left, vec3 right) => left.X < right.X || left.Y < right.Y || left.Z < right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 LessThanOrEqual(vec3 left, vec3 right) => Mask(left.X <= right.X, left.Y <= right.Y, left.Z <= right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool LessThanOrEqualAll(vec3 left, vec3 right) => left.X <= right.X && left.Y <= right.Y && left.Z <= right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool LessThanOrEqualAny(vec3 left, vec3 right) => left.X <= right.X || left.Y <= right.Y || left.Z <= right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 ConditionalSelect(vec3 condition, vec3 trueValue, vec3 falseValue) =>
        (condition & trueValue) | (~condition & falseValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOf(vec3 value, vec3 target)
    {
        if (value.X == target.X)
        {
            return 0;
        }

        if (value.Y == target.Y)
        {
            return 1;
        }

        if (value.Z == target.Z)
        {
            return 2;
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LastIndexOf(vec3 value, vec3 target)
    {
        if (value.Z == target.Z)
        {
            return 2;
        }

        if (value.Y == target.Y)
        {
            return 1;
        }

        if (value.X == target.X)
        {
            return 0;
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOfWhereAllBitsSet(vec3 value)
    {
        if (BitConverter.SingleToUInt32Bits(value.X) == 0xFFFF_FFFFu)
        {
            return 0;
        }

        if (BitConverter.SingleToUInt32Bits(value.Y) == 0xFFFF_FFFFu)
        {
            return 1;
        }

        if (BitConverter.SingleToUInt32Bits(value.Z) == 0xFFFF_FFFFu)
        {
            return 2;
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LastIndexOfWhereAllBitsSet(vec3 value)
    {
        if (BitConverter.SingleToUInt32Bits(value.Z) == 0xFFFF_FFFFu)
        {
            return 2;
        }

        if (BitConverter.SingleToUInt32Bits(value.Y) == 0xFFFF_FFFFu)
        {
            return 1;
        }

        if (BitConverter.SingleToUInt32Bits(value.X) == 0xFFFF_FFFFu)
        {
            return 0;
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Shuffle(vec3 value, byte x, byte y, byte z)
    {
        float rx = x == 0 ? value.X : x == 1 ? value.Y : x == 2 ? value.Z : 0F;
        float ry = y == 0 ? value.X : y == 1 ? value.Y : y == 2 ? value.Z : 0F;
        float rz = z == 0 ? value.X : z == 1 ? value.Y : z == 2 ? value.Z : 0F;
        return new vec3(rx, ry, rz);
    }
}
