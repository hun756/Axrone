namespace Axrone.Numeric;

public interface IVectorTransformer4<TState>
    where TState : allows ref struct
{
    static abstract Vec4 Transform(Vec4 value, scoped ref TState state);
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct ComponentIndex4 : IEquatable<ComponentIndex4>, IComparable<ComponentIndex4>
{
    public readonly byte Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ComponentIndex4(byte value) => Value = value;

    public static readonly ComponentIndex4 X = new(0);

    public static readonly ComponentIndex4 Y = new(1);

    public static readonly ComponentIndex4 Z = new(2);

    public static readonly ComponentIndex4 W = new(3);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ComponentIndex4 From(int index)
    {
        if ((uint)index >= 4U)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
        }

        return new ComponentIndex4((byte)index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator ComponentIndex4(int index) => From(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator int(ComponentIndex4 index) => index.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(ComponentIndex4 other) => Value.CompareTo(other.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <(ComponentIndex4 left, ComponentIndex4 right) => left.Value < right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >(ComponentIndex4 left, ComponentIndex4 right) => left.Value > right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <=(ComponentIndex4 left, ComponentIndex4 right) => left.Value <= right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >=(ComponentIndex4 left, ComponentIndex4 right) => left.Value >= right.Value;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct UnitVec4 :
    IEquatable<UnitVec4>,
    IFormattable,
    ISpanFormattable,
    IUtf8SpanFormattable
{
    public readonly float X;

    public readonly float Y;

    public readonly float Z;

    public readonly float W;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal UnitVec4(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public static UnitVec4 UnitX => new(1F, 0F, 0F, 0F);
    public static UnitVec4 UnitY => new(0F, 1F, 0F, 0F);
    public static UnitVec4 UnitZ => new(0F, 0F, 1F, 0F);
    public static UnitVec4 UnitW => new(0F, 0F, 0F, 1F);
    public static UnitVec4 NegativeUnitX => new(-1F, 0F, 0F, 0F);
    public static UnitVec4 NegativeUnitY => new(0F, -1F, 0F, 0F);
    public static UnitVec4 NegativeUnitZ => new(0F, 0F, -1F, 0F);
    public static UnitVec4 NegativeUnitW => new(0F, 0F, 0F, -1F);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator Vec4(UnitVec4 unit) => new(unit.X, unit.Y, unit.Z, unit.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static explicit operator UnitVec4(Vec4 value) => Vec4.ToUnit(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Vec4 AsVec4() => new(X, Y, Z, W);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Equals(UnitVec4 other) => X == other.X && Y == other.Y && Z == other.Z && W == other.W;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is UnitVec4 other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator ==(UnitVec4 left, UnitVec4 right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator !=(UnitVec4 left, UnitVec4 right) => !left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static UnitVec4 operator -(UnitVec4 value) => new(-value.X, -value.Y, -value.Z, -value.W);

    public override string ToString() => AsVec4().ToString();

    public string ToString(string? format, IFormatProvider? formatProvider) => AsVec4().ToString(format, formatProvider);

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        AsVec4().TryFormat(destination, out charsWritten, format, provider);

    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        AsVec4().TryFormat(utf8Destination, out bytesWritten, format, provider);
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct NormalizationResult4 : IEquatable<NormalizationResult4>
{
    public readonly UnitVec4 Value;
    public readonly NormalizationStatus Status;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public NormalizationResult4(UnitVec4 value)
    {
        Value = value;
        Status = NormalizationStatus.Success;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public NormalizationResult4(NormalizationStatus status)
    {
        Value = default;
        Status = status;
    }

    public bool IsSuccess => Status == NormalizationStatus.Success;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryGetUnit(out UnitVec4 unit)
    {
        unit = Value;
        return Status == NormalizationStatus.Success;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TResult Match<TResult>(
        Func<UnitVec4, TResult> onSuccess,
        Func<TResult> onDegenerateZero,
        Func<TResult> onNonFinite)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onDegenerateZero);
        ArgumentNullException.ThrowIfNull(onNonFinite);

        return Status switch
        {
            NormalizationStatus.Success => onSuccess(Value),
            NormalizationStatus.DegenerateZero => onDegenerateZero(),
            NormalizationStatus.NonFinite => onNonFinite(),
            _ => ThrowMatch(),
        };

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        static TResult ThrowMatch() =>
            throw new InvalidOperationException("Invalid normalization status discriminant.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Equals(NormalizationResult4 other) =>
        Status == other.Status && (Status != NormalizationStatus.Success || Value.Equals(other.Value));

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is NormalizationResult4 other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Value, Status);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator ==(NormalizationResult4 left, NormalizationResult4 right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator !=(NormalizationResult4 left, NormalizationResult4 right) => !left.Equals(right);
}

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
    IUnaryPlusOperators<Vec4, Vec4>,
    IAdditiveIdentity<Vec4, Vec4>,
    IMultiplicativeIdentity<Vec4, Vec4>,
    IFormattable,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IParsable<Vec4>,
    ISpanParsable<Vec4>,
    IUtf8SpanParsable<Vec4>,
    ISpatialVector<Vec4>,
    IInnerProductSpace<Vec4, float>
{
    public float X;
    public float Y;
    public float Z;
    public float W;

    public const float MachineEpsilon = 1.1920929E-07F;
    public const float DefaultTolerance = MachineEpsilon * 8F;

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
    public static Vec4 NegativeOne => new(-1.0f, -1.0f, -1.0f, -1.0f);
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

    #pragma warning disable CA1043 // ComponentIndex4 is the whole point: a 0..3 proof checked at construction.
    public float this[ComponentIndex4 index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => Unsafe.Add(ref X, (nint)index.Value);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set => Unsafe.Add(ref X, (nint)index.Value) = value;
    }
    #pragma warning restore CA1043

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
    public Span<float> AsSpan() => MemoryMarshal.CreateSpan(ref X, 4);

    public ReadOnlySpan<float> AsReadOnlySpan() => MemoryMarshal.CreateReadOnlySpan(ref X, 4);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly void CopyTo(Span<byte> destination)
    {
        if (destination.Length < 16)
        {
            NumericThrowHelper.ThrowArgumentException(nameof(destination), "Destination must hold at least sixteen bytes.");
        }

        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(destination), this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly void CopyTo(Span<Vec4> destination)
    {
        if (destination.IsEmpty)
        {
            NumericThrowHelper.ThrowArgumentException(nameof(destination), "Destination must hold at least one vector.");
        }

        destination[0] = this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly void CopyTo(Vec4[] destination, int index)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if ((uint)index >= (uint)destination.Length)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
        }

        destination[index] = this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool TryCopyTo(Span<byte> destination)
    {
        if (destination.Length < 16)
        {
            return false;
        }

        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(destination), this);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ComponentEnumerator4 GetEnumerator() => new(in this);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec4 Load(ReadOnlySpan<float> source)
    {
        if (source.Length < 4)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(source));
        }

        return new Vec4(source[0], source[1], source[2], source[3]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe Vec4 LoadAligned(ref readonly float source)
    {
        Vector128<float> wide = Vector128.LoadAligned((float*)Unsafe.AsPointer(ref Unsafe.AsRef(in source)));
        return new Vec4(wide.GetElement(0), wide.GetElement(1), wide.GetElement(2), wide.GetElement(3));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe Vec4 LoadAlignedNonTemporal(ref readonly float source)
    {
        Vector128<float> wide = Vector128.LoadAlignedNonTemporal((float*)Unsafe.AsPointer(ref Unsafe.AsRef(in source)));
        return new Vec4(wide.GetElement(0), wide.GetElement(1), wide.GetElement(2), wide.GetElement(3));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe Vec4 LoadUnsafe(void* source)
    {
        float* components = (float*)source;
        return new Vec4(components[0], components[1], components[2], components[3]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe Vec4 LoadUnsafe(void* source, int offset)
    {
        if (offset < 0)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(offset));
        }

        float* components = (float*)source + offset;
        return new Vec4(components[0], components[1], components[2], components[3]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec4 TransformCustom<TTransformer, TState>(Vec4 value, scoped ref TState state)
        where TTransformer : struct, IVectorTransformer4<TState>
        where TState : allows ref struct =>
        TTransformer.Transform(value, ref state);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool Equals(Vec4 other, Tolerance tolerance) => Equals(other, tolerance.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool Equals(Vec4 left, Vec4 right, Tolerance tolerance) =>
        left.Equals(right, tolerance.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool BitEquals(Vec4 other) =>
        BitConverter.SingleToUInt32Bits(X) == BitConverter.SingleToUInt32Bits(other.X) &&
        BitConverter.SingleToUInt32Bits(Y) == BitConverter.SingleToUInt32Bits(other.Y) &&
        BitConverter.SingleToUInt32Bits(Z) == BitConverter.SingleToUInt32Bits(other.Z) &&
        BitConverter.SingleToUInt32Bits(W) == BitConverter.SingleToUInt32Bits(other.W);

    public override readonly bool Equals([NotNullWhen(true)] object? obj) =>
        obj is Vec4 other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(X, Y, Z, W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(Vec4 left, Vec4 right) =>
        MathF.FusedMultiplyAdd(left.X, right.X, MathF.FusedMultiplyAdd(left.Y, right.Y, MathF.FusedMultiplyAdd(left.Z, right.Z, left.W * right.W)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DotStrict(Vec4 left, Vec4 right) =>
        (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z) + (left.W * right.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float LengthSquared() => Dot(this, this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float Length() => MathF.Sqrt(LengthSquared());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceSquared(Vec4 value1, Vec4 value2)
    {
        float dx = value1.X - value2.X;
        float dy = value1.Y - value2.Y;
        float dz = value1.Z - value2.Z;
        float dw = value1.W - value2.W;
        return (dx * dx) + (dy * dy) + (dz * dz) + (dw * dw);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Distance(Vec4 value1, Vec4 value2) =>
        MathF.Sqrt(DistanceSquared(value1, value2));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Normalize(Vec4 value) => Normalize<StrictIeeeStrategy>(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Normalize<TStrategy>(Vec4 value)
        where TStrategy : struct, INormalizationStrategy
    {
        float lengthSquared = value.LengthSquared();
        if (lengthSquared > 0.0f)
        {
            float invLength = TStrategy.ReciprocalSqrt(lengthSquared);
            if (float.IsFinite(invLength) && invLength > 0.0f)
            {
                return new Vec4(value.X * invLength, value.Y * invLength, value.Z * invLength, value.W * invLength);
            }
            float len = MathF.Sqrt(lengthSquared);
            if (len > 0.0f)
            {
                return new Vec4(value.X / len, value.Y / len, value.Z / len, value.W / len);
            }
        }
        return Zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryNormalize(Vec4 value, out Vec4 result) =>
        TryNormalize(value, out result, 0.0f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryNormalize(Vec4 value, out Vec4 result, float tolerance)
    {
        float lengthSquared = value.LengthSquared();
        float tolSquared = tolerance > 0.0f ? tolerance * tolerance : 0.0f;
        if (lengthSquared > tolSquared && lengthSquared > 0.0f)
        {
            float invLength = 1.0f / MathF.Sqrt(lengthSquared);
            if (float.IsFinite(invLength))
            {
                result = new Vec4(value.X * invLength, value.Y * invLength, value.Z * invLength, value.W * invLength);
                return true;
            }
            float len = MathF.Sqrt(lengthSquared);
            if (len > 0.0f)
            {
                result = new Vec4(value.X / len, value.Y / len, value.Z / len, value.W / len);
                return true;
            }
        }
        result = Zero;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool TryNormalize(Vec4 value, out Vec4 result, Tolerance tolerance) =>
        TryNormalize(value, out result, tolerance.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static UnitVec4 ToUnit<TStrategy>(Vec4 value)
        where TStrategy : struct, INormalizationStrategy
    {
        Vec4 normalized = Normalize<TStrategy>(value);
        return new UnitVec4(normalized.X, normalized.Y, normalized.Z, normalized.W);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static UnitVec4 ToUnit(Vec4 value) => ToUnit<StrictIeeeStrategy>(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static NormalizationResult4 TryNormalizeUnit(Vec4 value, Tolerance tolerance = default)
    {
        if (TryNormalize(value, out Vec4 result, tolerance.Value))
        {
            return new NormalizationResult4(new UnitVec4(result.X, result.Y, result.Z, result.W));
        }

        if (value.IsAnyNaN || value.IsAnyInfinity)
        {
            return new NormalizationResult4(NormalizationStatus.NonFinite);
        }

        return new NormalizationResult4(NormalizationStatus.DegenerateZero);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Reflect(Vec4 vector, Vec4 normal)
    {
        float dot2 = Dot(vector, normal) * 2.0f;
        return new Vec4(
            vector.X - (normal.X * dot2),
            vector.Y - (normal.Y * dot2),
            vector.Z - (normal.Z * dot2),
            vector.W - (normal.W * dot2)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Project(Vec4 vector, Vec4 onNormal)
    {
        float sqrMag = Dot(onNormal, onNormal);
        if (sqrMag <= 0.0f)
        {
            return Zero;
        }
        float scale = Dot(vector, onNormal) / sqrMag;
        return onNormal * scale;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec4 Reflect(Vec4 vector, UnitVec4 normal)
    {
        float dot2 = Dot(vector, normal) * 2.0f;
        return new Vec4(
            vector.X - (normal.X * dot2),
            vector.Y - (normal.Y * dot2),
            vector.Z - (normal.Z * dot2),
            vector.W - (normal.W * dot2)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec4 Project(Vec4 vector, UnitVec4 onNormal)
    {
        float scale = Dot(vector, onNormal);
        return (Vec4)onNormal * scale;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Slide(Vec4 vector, Vec4 normal)
    {
        float normalSq = Dot(normal, normal);
        if (normalSq <= 0.0f)
        {
            return vector;
        }
        return vector - (normal * (Dot(vector, normal) / normalSq));
    }

    public static float Angle(Vec4 from, Vec4 to)
    {
        float maxA = MathF.Max(MathF.Max(MathF.Abs(from.X), MathF.Abs(from.Y)), MathF.Max(MathF.Abs(from.Z), MathF.Abs(from.W)));
        float maxB = MathF.Max(MathF.Max(MathF.Abs(to.X), MathF.Abs(to.Y)), MathF.Max(MathF.Abs(to.Z), MathF.Abs(to.W)));
        if (maxA <= 0.0f || maxB <= 0.0f)
        {
            return 0.0f;
        }

        Vec4 a = from * (1.0f / maxA);
        Vec4 b = to * (1.0f / maxB);

        float lenA = a.Length();
        float lenB = b.Length();
        if (lenA <= 0.0f || lenB <= 0.0f)
        {
            return 0.0f;
        }

        float cos = Dot(a, b) / (lenA * lenB);
        return MathF.Acos(Math.Clamp(cos, -1.0f, 1.0f));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static AngleRadians AngleBetween(Vec4 from, Vec4 to) => new(Angle(from, to));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Clamp(Vec4 value, Vec4 min, Vec4 max)
    {
        float x = value.X;
        x = (x < min.X) ? min.X : x;
        x = (x > max.X) ? max.X : x;

        float y = value.Y;
        y = (y < min.Y) ? min.Y : y;
        y = (y > max.Y) ? max.Y : y;

        float z = value.Z;
        z = (z < min.Z) ? min.Z : z;
        z = (z > max.Z) ? max.Z : z;

        float w = value.W;
        w = (w < min.W) ? min.W : w;
        w = (w > max.W) ? max.W : w;

        return new Vec4(x, y, z, w);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 ClampNative(Vec4 value, Vec4 min, Vec4 max) => Clamp(value, min, max);

    public static Vec4 ClampLength(Vec4 value, float minLength, float maxLength)
    {
        float sqrMagnitude = value.LengthSquared();
        if (sqrMagnitude <= 0.0f)
        {
            return minLength > 0.0f ? new Vec4(minLength, 0.0f, 0.0f, 0.0f) : Zero;
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
    public static Vec4 Min(Vec4 left, Vec4 right) =>
        new(MathF.Min(left.X, right.X), MathF.Min(left.Y, right.Y), MathF.Min(left.Z, right.Z), MathF.Min(left.W, right.W));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Max(Vec4 left, Vec4 right) =>
        new(MathF.Max(left.X, right.X), MathF.Max(left.Y, right.Y), MathF.Max(left.Z, right.Z), MathF.Max(left.W, right.W));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 MinNative(Vec4 left, Vec4 right) => Min(left, right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 MaxNative(Vec4 left, Vec4 right) => Max(left, right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 MinNumber(Vec4 left, Vec4 right) =>
        new(float.MinNumber(left.X, right.X), float.MinNumber(left.Y, right.Y), float.MinNumber(left.Z, right.Z), float.MinNumber(left.W, right.W));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 MaxNumber(Vec4 left, Vec4 right) =>
        new(float.MaxNumber(left.X, right.X), float.MaxNumber(left.Y, right.Y), float.MaxNumber(left.Z, right.Z), float.MaxNumber(left.W, right.W));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 MinMagnitude(Vec4 left, Vec4 right) =>
        new(float.MinMagnitude(left.X, right.X), float.MinMagnitude(left.Y, right.Y), float.MinMagnitude(left.Z, right.Z), float.MinMagnitude(left.W, right.W));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 MaxMagnitude(Vec4 left, Vec4 right) =>
        new(float.MaxMagnitude(left.X, right.X), float.MaxMagnitude(left.Y, right.Y), float.MaxMagnitude(left.Z, right.Z), float.MaxMagnitude(left.W, right.W));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 MinMagnitudeNumber(Vec4 left, Vec4 right) =>
        new(float.MinMagnitudeNumber(left.X, right.X), float.MinMagnitudeNumber(left.Y, right.Y), float.MinMagnitudeNumber(left.Z, right.Z), float.MinMagnitudeNumber(left.W, right.W));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 MaxMagnitudeNumber(Vec4 left, Vec4 right) =>
        new(float.MaxMagnitudeNumber(left.X, right.X), float.MaxMagnitudeNumber(left.Y, right.Y), float.MaxMagnitudeNumber(left.Z, right.Z), float.MaxMagnitudeNumber(left.W, right.W));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 CopySign(Vec4 value, Vec4 sign) =>
        new(MathF.CopySign(value.X, sign.X), MathF.CopySign(value.Y, sign.Y), MathF.CopySign(value.Z, sign.Z), MathF.CopySign(value.W, sign.W));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 FusedMultiplyAdd(Vec4 left, Vec4 right, Vec4 addend) =>
        new(
            MathF.FusedMultiplyAdd(left.X, right.X, addend.X),
            MathF.FusedMultiplyAdd(left.Y, right.Y, addend.Y),
            MathF.FusedMultiplyAdd(left.Z, right.Z, addend.Z),
            MathF.FusedMultiplyAdd(left.W, right.W, addend.W)
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 MultiplyAddEstimate(Vec4 left, Vec4 right, Vec4 addend) =>
        FusedMultiplyAdd(left, right, addend);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Abs(Vec4 value) =>
        new(MathF.Abs(value.X), MathF.Abs(value.Y), MathF.Abs(value.Z), MathF.Abs(value.W));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Sqrt(Vec4 value) =>
        new(MathF.Sqrt(value.X), MathF.Sqrt(value.Y), MathF.Sqrt(value.Z), MathF.Sqrt(value.W));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 SquareRoot(Vec4 value) => Sqrt(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Sin(Vec4 vector) =>
        new(MathF.Sin(vector.X), MathF.Sin(vector.Y), MathF.Sin(vector.Z), MathF.Sin(vector.W));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Cos(Vec4 vector) =>
        new(MathF.Cos(vector.X), MathF.Cos(vector.Y), MathF.Cos(vector.Z), MathF.Cos(vector.W));

    public static (Vec4 Sin, Vec4 Cos) SinCos(Vec4 vector)
    {
        var (sX, cX) = MathF.SinCos(vector.X);
        var (sY, cY) = MathF.SinCos(vector.Y);
        var (sZ, cZ) = MathF.SinCos(vector.Z);
        var (sW, cW) = MathF.SinCos(vector.W);
        return (new Vec4(sX, sY, sZ, sW), new Vec4(cX, cY, cZ, cW));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Exp(Vec4 vector) =>
        new(MathF.Exp(vector.X), MathF.Exp(vector.Y), MathF.Exp(vector.Z), MathF.Exp(vector.W));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Log(Vec4 vector) =>
        new(MathF.Log(vector.X), MathF.Log(vector.Y), MathF.Log(vector.Z), MathF.Log(vector.W));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Log2(Vec4 vector) =>
        new(MathF.Log2(vector.X), MathF.Log2(vector.Y), MathF.Log2(vector.Z), MathF.Log2(vector.W));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Hypot(Vec4 x, Vec4 y) => Sqrt((x * x) + (y * y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 DegreesToRadians(Vec4 degrees) => degrees * (MathF.PI / 180.0f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 RadiansToDegrees(Vec4 radians) => radians * (180.0f / MathF.PI);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Floor(Vec4 value) =>
        new(MathF.Floor(value.X), MathF.Floor(value.Y), MathF.Floor(value.Z), MathF.Floor(value.W));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Ceiling(Vec4 value) =>
        new(MathF.Ceiling(value.X), MathF.Ceiling(value.Y), MathF.Ceiling(value.Z), MathF.Ceiling(value.W));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Round(Vec4 value) =>
        new(MathF.Round(value.X), MathF.Round(value.Y), MathF.Round(value.Z), MathF.Round(value.W));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Truncate(Vec4 value) =>
        new(MathF.Truncate(value.X), MathF.Truncate(value.Y), MathF.Truncate(value.Z), MathF.Truncate(value.W));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Lerp(Vec4 a, Vec4 b, float t) =>
        new(
            MathF.FusedMultiplyAdd(b.X - a.X, t, a.X),
            MathF.FusedMultiplyAdd(b.Y - a.Y, t, a.Y),
            MathF.FusedMultiplyAdd(b.Z - a.Z, t, a.Z),
            MathF.FusedMultiplyAdd(b.W - a.W, t, a.W)
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 LerpClamped(Vec4 a, Vec4 b, float t) =>
        Lerp(a, b, Math.Clamp(t, 0.0f, 1.0f));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 SmoothStep(Vec4 from, Vec4 to, float amount)
    {
        amount = Math.Clamp(amount, 0.0f, 1.0f);
        float factor = amount * amount * (3.0f - (2.0f * amount));
        return Lerp(from, to, factor);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 SmootherStep(Vec4 from, Vec4 to, float amount)
    {
        amount = Math.Clamp(amount, 0.0f, 1.0f);
        float inner = MathF.FusedMultiplyAdd(amount, 6.0f, -15.0f);
        float poly = MathF.FusedMultiplyAdd(amount, inner, 10.0f);
        float factor = amount * amount * amount * poly;
        return Lerp(from, to, factor);
    }

    public static Vec4 MoveTowards(Vec4 current, Vec4 target, float maxDistanceDelta)
    {
        float dx = target.X - current.X;
        float dy = target.Y - current.Y;
        float dz = target.Z - current.Z;
        float dw = target.W - current.W;
        float distSq = (dx * dx) + (dy * dy) + (dz * dz) + (dw * dw);

        if (distSq == 0.0f || (maxDistanceDelta >= 0.0f && distSq <= maxDistanceDelta * maxDistanceDelta))
        {
            return target;
        }

        float dist = MathF.Sqrt(distSq);
        float ratio = maxDistanceDelta / dist;
        return new Vec4(
            MathF.FusedMultiplyAdd(dx, ratio, current.X),
            MathF.FusedMultiplyAdd(dy, ratio, current.Y),
            MathF.FusedMultiplyAdd(dz, ratio, current.Z),
            MathF.FusedMultiplyAdd(dw, ratio, current.W)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Inverse(Vec4 vector) =>
        new(1.0f / vector.X, 1.0f / vector.Y, 1.0f / vector.Z, 1.0f / vector.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec4 Inverse() => Inverse(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 InverseSafe(Vec4 vector, float fallback = 0.0f)
    {
        float invX = 1.0f / vector.X;
        float invY = 1.0f / vector.Y;
        float invZ = 1.0f / vector.Z;
        float invW = 1.0f / vector.W;
        return new Vec4(
            float.IsFinite(invX) ? invX : fallback,
            float.IsFinite(invY) ? invY : fallback,
            float.IsFinite(invZ) ? invZ : fallback,
            float.IsFinite(invW) ? invW : fallback
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec4 InverseSafe(float fallback = 0.0f) => InverseSafe(this, fallback);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 DivideSafe(Vec4 left, Vec4 right, float fallback = 0.0f)
    {
        float x = left.X / right.X;
        float y = left.Y / right.Y;
        float z = left.Z / right.Z;
        float w = left.W / right.W;
        return new Vec4(
            float.IsFinite(x) ? x : fallback,
            float.IsFinite(y) ? y : fallback,
            float.IsFinite(z) ? z : fallback,
            float.IsFinite(w) ? w : fallback
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 DivideSafe(Vec4 left, float right, float fallback = 0.0f)
    {
        float inv = 1.0f / right;
        return float.IsFinite(inv) ? left * inv : new Vec4(fallback);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ManhattanDistance(Vec4 a, Vec4 b) =>
        MathF.Abs(a.X - b.X) + MathF.Abs(a.Y - b.Y) + MathF.Abs(a.Z - b.Z) + MathF.Abs(a.W - b.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float ManhattanDistance(Vec4 destination) => ManhattanDistance(this, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ChebyshevDistance(Vec4 a, Vec4 b) =>
        MathF.Max(MathF.Max(MathF.Abs(a.X - b.X), MathF.Abs(a.Y - b.Y)), MathF.Max(MathF.Abs(a.Z - b.Z), MathF.Abs(a.W - b.W)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float ChebyshevDistance(Vec4 destination) => ChebyshevDistance(this, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 AddScalar(Vec4 vector, float scalar) =>
        new(vector.X + scalar, vector.Y + scalar, vector.Z + scalar, vector.W + scalar);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec4 AddScalar(float scalar) => AddScalar(this, scalar);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 SubtractScalar(Vec4 vector, float scalar) =>
        new(vector.X - scalar, vector.Y - scalar, vector.Z - scalar, vector.W - scalar);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec4 SubtractScalar(float scalar) => SubtractScalar(this, scalar);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Transform(Vec2 position, System.Numerics.Matrix4x4 matrix) =>
        new(
            MathF.FusedMultiplyAdd(position.X, matrix.M11, MathF.FusedMultiplyAdd(position.Y, matrix.M21, matrix.M41)),
            MathF.FusedMultiplyAdd(position.X, matrix.M12, MathF.FusedMultiplyAdd(position.Y, matrix.M22, matrix.M42)),
            MathF.FusedMultiplyAdd(position.X, matrix.M13, MathF.FusedMultiplyAdd(position.Y, matrix.M23, matrix.M43)),
            MathF.FusedMultiplyAdd(position.X, matrix.M14, MathF.FusedMultiplyAdd(position.Y, matrix.M24, matrix.M44))
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Transform(Vec3 position, System.Numerics.Matrix4x4 matrix) =>
        new(
            MathF.FusedMultiplyAdd(position.X, matrix.M11, MathF.FusedMultiplyAdd(position.Y, matrix.M21, MathF.FusedMultiplyAdd(position.Z, matrix.M31, matrix.M41))),
            MathF.FusedMultiplyAdd(position.X, matrix.M12, MathF.FusedMultiplyAdd(position.Y, matrix.M22, MathF.FusedMultiplyAdd(position.Z, matrix.M32, matrix.M42))),
            MathF.FusedMultiplyAdd(position.X, matrix.M13, MathF.FusedMultiplyAdd(position.Y, matrix.M23, MathF.FusedMultiplyAdd(position.Z, matrix.M33, matrix.M43))),
            MathF.FusedMultiplyAdd(position.X, matrix.M14, MathF.FusedMultiplyAdd(position.Y, matrix.M24, MathF.FusedMultiplyAdd(position.Z, matrix.M34, matrix.M44)))
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Transform(Vec4 vector, System.Numerics.Matrix4x4 matrix) =>
        new(
            MathF.FusedMultiplyAdd(vector.X, matrix.M11, MathF.FusedMultiplyAdd(vector.Y, matrix.M21, MathF.FusedMultiplyAdd(vector.Z, matrix.M31, vector.W * matrix.M41))),
            MathF.FusedMultiplyAdd(vector.X, matrix.M12, MathF.FusedMultiplyAdd(vector.Y, matrix.M22, MathF.FusedMultiplyAdd(vector.Z, matrix.M32, vector.W * matrix.M42))),
            MathF.FusedMultiplyAdd(vector.X, matrix.M13, MathF.FusedMultiplyAdd(vector.Y, matrix.M23, MathF.FusedMultiplyAdd(vector.Z, matrix.M33, vector.W * matrix.M43))),
            MathF.FusedMultiplyAdd(vector.X, matrix.M14, MathF.FusedMultiplyAdd(vector.Y, matrix.M24, MathF.FusedMultiplyAdd(vector.Z, matrix.M34, vector.W * matrix.M44)))
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Transform(Vec2 value, System.Numerics.Quaternion rotation)
    {
        float x2 = rotation.X + rotation.X;
        float y2 = rotation.Y + rotation.Y;
        float z2 = rotation.Z + rotation.Z;

        float wx2 = rotation.W * x2;
        float wy2 = rotation.W * y2;
        float wz2 = rotation.W * z2;
        float xx2 = rotation.X * x2;
        float xy2 = rotation.X * y2;
        float xz2 = rotation.X * z2;
        float yy2 = rotation.Y * y2;
        float yz2 = rotation.Y * z2;
        float zz2 = rotation.Z * z2;

        return new Vec4(
            (value.X * ((1.0f - yy2) - zz2)) + (value.Y * (xy2 - wz2)),
            (value.X * (xy2 + wz2)) + (value.Y * ((1.0f - xx2) - zz2)),
            (value.X * (xz2 - wy2)) + (value.Y * (yz2 + wx2)),
            1.0f
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Transform(Vec3 value, System.Numerics.Quaternion rotation)
    {
        float x2 = rotation.X + rotation.X;
        float y2 = rotation.Y + rotation.Y;
        float z2 = rotation.Z + rotation.Z;

        float wx2 = rotation.W * x2;
        float wy2 = rotation.W * y2;
        float wz2 = rotation.W * z2;
        float xx2 = rotation.X * x2;
        float xy2 = rotation.X * y2;
        float xz2 = rotation.X * z2;
        float yy2 = rotation.Y * y2;
        float yz2 = rotation.Y * z2;
        float zz2 = rotation.Z * z2;

        return new Vec4(
            (value.X * ((1.0f - yy2) - zz2)) + (value.Y * (xy2 - wz2)) + (value.Z * (xz2 + wy2)),
            (value.X * (xy2 + wz2)) + (value.Y * ((1.0f - xx2) - zz2)) + (value.Z * (yz2 - wx2)),
            (value.X * (xz2 - wy2)) + (value.Y * (yz2 + wx2)) + (value.Z * ((1.0f - xx2) - yy2)),
            1.0f
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Transform(Vec4 value, System.Numerics.Quaternion rotation)
    {
        float x2 = rotation.X + rotation.X;
        float y2 = rotation.Y + rotation.Y;
        float z2 = rotation.Z + rotation.Z;

        float wx2 = rotation.W * x2;
        float wy2 = rotation.W * y2;
        float wz2 = rotation.W * z2;
        float xx2 = rotation.X * x2;
        float xy2 = rotation.X * y2;
        float xz2 = rotation.X * z2;
        float yy2 = rotation.Y * y2;
        float yz2 = rotation.Y * z2;
        float zz2 = rotation.Z * z2;

        return new Vec4(
            (value.X * ((1.0f - yy2) - zz2)) + (value.Y * (xy2 - wz2)) + (value.Z * (xz2 + wy2)),
            (value.X * (xy2 + wz2)) + (value.Y * ((1.0f - xx2) - zz2)) + (value.Z * (yz2 - wx2)),
            (value.X * (xz2 - wy2)) + (value.Y * (yz2 + wx2)) + (value.Z * ((1.0f - xx2) - yy2)),
            value.W
        );
    }

    public static Vec4 AdditiveIdentity => Zero;

    public static Vec4 MultiplicativeIdentity => One;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 GreaterThan(Vec4 left, Vec4 right) =>
        new(
            left.X > right.X ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            left.Y > right.Y ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            left.Z > right.Z ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            left.W > right.W ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GreaterThanAll(Vec4 left, Vec4 right) =>
        left.X > right.X && left.Y > right.Y && left.Z > right.Z && left.W > right.W;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GreaterThanAny(Vec4 left, Vec4 right) =>
        left.X > right.X || left.Y > right.Y || left.Z > right.Z || left.W > right.W;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 GreaterThanOrEqual(Vec4 left, Vec4 right) =>
        new(
            left.X >= right.X ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            left.Y >= right.Y ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            left.Z >= right.Z ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            left.W >= right.W ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GreaterThanOrEqualAll(Vec4 left, Vec4 right) =>
        left.X >= right.X && left.Y >= right.Y && left.Z >= right.Z && left.W >= right.W;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GreaterThanOrEqualAny(Vec4 left, Vec4 right) =>
        left.X >= right.X || left.Y >= right.Y || left.Z >= right.Z || left.W >= right.W;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 LessThan(Vec4 left, Vec4 right) =>
        new(
            left.X < right.X ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            left.Y < right.Y ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            left.Z < right.Z ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            left.W < right.W ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool LessThanAll(Vec4 left, Vec4 right) =>
        left.X < right.X && left.Y < right.Y && left.Z < right.Z && left.W < right.W;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool LessThanAny(Vec4 left, Vec4 right) =>
        left.X < right.X || left.Y < right.Y || left.Z < right.Z || left.W < right.W;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 LessThanOrEqual(Vec4 left, Vec4 right) =>
        new(
            left.X <= right.X ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            left.Y <= right.Y ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            left.Z <= right.Z ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            left.W <= right.W ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool LessThanOrEqualAll(Vec4 left, Vec4 right) =>
        left.X <= right.X && left.Y <= right.Y && left.Z <= right.Z && left.W <= right.W;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool LessThanOrEqualAny(Vec4 left, Vec4 right) =>
        left.X <= right.X || left.Y <= right.Y || left.Z <= right.Z || left.W <= right.W;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 ConditionalSelect(Vec4 condition, Vec4 left, Vec4 right) =>
        (condition & left) | (~condition & right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOf(Vec4 vector, float value)
    {
        if (vector.X == value) return 0;
        if (vector.Y == value) return 1;
        if (vector.Z == value) return 2;
        if (vector.W == value) return 3;
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LastIndexOf(Vec4 vector, float value)
    {
        if (vector.W == value) return 3;
        if (vector.Z == value) return 2;
        if (vector.Y == value) return 1;
        if (vector.X == value) return 0;
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOfWhereAllBitsSet(Vec4 vector)
    {
        if (BitConverter.SingleToUInt32Bits(vector.X) == 0xFFFF_FFFF) return 0;
        if (BitConverter.SingleToUInt32Bits(vector.Y) == 0xFFFF_FFFF) return 1;
        if (BitConverter.SingleToUInt32Bits(vector.Z) == 0xFFFF_FFFF) return 2;
        if (BitConverter.SingleToUInt32Bits(vector.W) == 0xFFFF_FFFF) return 3;
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LastIndexOfWhereAllBitsSet(Vec4 vector)
    {
        if (BitConverter.SingleToUInt32Bits(vector.W) == 0xFFFF_FFFF) return 3;
        if (BitConverter.SingleToUInt32Bits(vector.Z) == 0xFFFF_FFFF) return 2;
        if (BitConverter.SingleToUInt32Bits(vector.Y) == 0xFFFF_FFFF) return 1;
        if (BitConverter.SingleToUInt32Bits(vector.X) == 0xFFFF_FFFF) return 0;
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec4 Shuffle(Vec4 vector, byte xIndex, byte yIndex, byte zIndex, byte wIndex) =>
        new(vector[(int)xIndex], vector[(int)yIndex], vector[(int)zIndex], vector[(int)wIndex]);

    private const int StackTextCapacity = 256;

    public override readonly string ToString() => ToString(null, CultureInfo.InvariantCulture);

    public readonly string ToString(string? format) => ToString(format, CultureInfo.InvariantCulture);

    public readonly string ToString(string? format, IFormatProvider? formatProvider)
    {
        Span<char> buffer = stackalloc char[StackTextCapacity];
        if (TryFormat(buffer, out int charsWritten, format, formatProvider))
        {
            return new string(buffer.Slice(0, charsWritten));
        }

        return string.Create(
            formatProvider,
            $"{X.ToString(format, formatProvider)}, {Y.ToString(format, formatProvider)}, {Z.ToString(format, formatProvider)}, {W.ToString(format, formatProvider)}"
        );
    }

    public readonly bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        provider ??= CultureInfo.InvariantCulture;
        int offset = 0;

        if (!TryAppendComponentChar(destination, ref offset, X, format, provider) ||
            !TryAppendComponentChar(destination, ref offset, Y, format, provider) ||
            !TryAppendComponentChar(destination, ref offset, Z, format, provider) ||
            !TryAppendComponentChar(destination, ref offset, W, format, provider))
        {
            charsWritten = 0;
            return false;
        }

        charsWritten = offset;
        return true;
    }

    public readonly bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        provider ??= CultureInfo.InvariantCulture;
        int offset = 0;

        if (!TryAppendComponentUtf8(utf8Destination, ref offset, X, format, provider) ||
            !TryAppendComponentUtf8(utf8Destination, ref offset, Y, format, provider) ||
            !TryAppendComponentUtf8(utf8Destination, ref offset, Z, format, provider) ||
            !TryAppendComponentUtf8(utf8Destination, ref offset, W, format, provider))
        {
            bytesWritten = 0;
            return false;
        }

        bytesWritten = offset;
        return true;
    }

    public static Vec4 Parse(string s, IFormatProvider? provider = null)
    {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan(), provider);
    }

    public static Vec4 Parse(ReadOnlySpan<char> s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, provider, out Vec4 result))
        {
            NumericThrowHelper.ThrowFormatException("Expected four comma-separated floating-point components, for example \"1, 2, 3, 4\".");
        }

        return result;
    }

    public static Vec4 Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider = null)
    {
        if (!TryParse(utf8Text, provider, out Vec4 result))
        {
            NumericThrowHelper.ThrowFormatException("Expected four comma-separated floating-point components, for example \"1, 2, 3, 4\".");
        }

        return result;
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Vec4 result) =>
        TryParse(s.AsSpan(), provider, out result);

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Vec4 result)
    {
        provider ??= CultureInfo.InvariantCulture;
        s = s.Trim();

        if (s.Length >= 2 && IsOpeningBracket(s[0]) && IsClosingBracket(s[^1]))
        {
            s = s[1..^1];
        }

        Span<float> components = stackalloc float[4];
        ReadOnlySpan<char> remaining = s;
        bool sawTrailingSeparator = false;

        for (int index = 0; index < 4; index++)
        {
            int separator = remaining.IndexOf(',');
            ReadOnlySpan<char> component = (separator < 0 ? remaining : remaining[..separator]).Trim();

            if (!float.TryParse(component, NumberStyles.Float, provider, out components[index]))
            {
                result = default;
                return false;
            }

            sawTrailingSeparator = separator >= 0;
            remaining = separator < 0 ? default : remaining[(separator + 1)..];
        }

        if (sawTrailingSeparator || !remaining.Trim().IsEmpty)
        {
            result = default;
            return false;
        }

        result = new Vec4(components[0], components[1], components[2], components[3]);
        return true;
    }

    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Vec4 result)
    {
        provider ??= CultureInfo.InvariantCulture;
        ReadOnlySpan<byte> s = TrimUtf8(utf8Text);

        if (s.Length >= 2 && IsOpeningBracketUtf8(s[0]) && IsClosingBracketUtf8(s[^1]))
        {
            s = s[1..^1];
        }

        Span<float> components = stackalloc float[4];
        ReadOnlySpan<byte> remaining = s;
        bool sawTrailingSeparator = false;

        for (int index = 0; index < 4; index++)
        {
            int separator = remaining.IndexOf((byte)',');
            ReadOnlySpan<byte> component = TrimUtf8(separator < 0 ? remaining : remaining[..separator]);

            if (!float.TryParse(component, NumberStyles.Float, provider, out components[index]))
            {
                result = default;
                return false;
            }

            sawTrailingSeparator = separator >= 0;
            remaining = separator < 0 ? default : remaining[(separator + 1)..];
        }

        if (sawTrailingSeparator || !TrimUtf8(remaining).IsEmpty)
        {
            result = default;
            return false;
        }

        result = new Vec4(components[0], components[1], components[2], components[3]);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool IsOpeningBracket(char value) => value is '(' or '[' or '{' or '<';

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool IsClosingBracket(char value) => value is ')' or ']' or '}' or '>';

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool IsOpeningBracketUtf8(byte value) => value is (byte)'(' or (byte)'[' or (byte)'{' or (byte)'<';

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool IsClosingBracketUtf8(byte value) => value is (byte)')' or (byte)']' or (byte)'}' or (byte)'>';

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool IsWhiteSpaceUtf8(byte value) => value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static ReadOnlySpan<byte> TrimUtf8(ReadOnlySpan<byte> source)
    {
        int start = 0;
        while (start < source.Length && IsWhiteSpaceUtf8(source[start]))
        {
            start++;
        }

        int end = source.Length - 1;
        while (end >= start && IsWhiteSpaceUtf8(source[end]))
        {
            end--;
        }

        return source.Slice(start, end - start + 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool TryAppendComponentChar(Span<char> destination, ref int offset, float value, ReadOnlySpan<char> format, IFormatProvider provider)
    {
        if (offset > 0)
        {
            if ((uint)destination.Length <= (uint)(offset + 1))
            {
                return false;
            }

            destination[offset++] = ',';
            destination[offset++] = ' ';
        }

        if (!value.TryFormat(destination[offset..], out int written, format, provider))
        {
            return false;
        }

        offset += written;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool TryAppendComponentUtf8(Span<byte> destination, ref int offset, float value, ReadOnlySpan<char> format, IFormatProvider provider)
    {
        if (offset > 0)
        {
            if ((uint)destination.Length <= (uint)(offset + 1))
            {
                return false;
            }

            destination[offset++] = (byte)',';
            destination[offset++] = (byte)' ';
        }

        if (!value.TryFormat(destination[offset..], out int written, format, provider))
        {
            return false;
        }

        offset += written;
        return true;
    }
}

public ref struct ComponentEnumerator4
{
    private readonly Vec4 _vector;

    private int _index;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal ComponentEnumerator4(scoped ref readonly Vec4 vector)
    {
        _vector = vector;
        _index = -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool MoveNext() => ++_index < 4;

    public readonly ref readonly float Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            if ((uint)_index >= 4U)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(_index));
            }

            return ref Unsafe.Add(ref Unsafe.AsRef(in _vector.X), (nint)(uint)_index);
        }
    }
}
