namespace Axrone.Numeric;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Vec2
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
}
