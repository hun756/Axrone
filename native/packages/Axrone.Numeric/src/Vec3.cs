namespace Axrone.Numeric;

public interface ISpatialVector<TSelf>
    where TSelf : struct, ISpatialVector<TSelf>
{
    float Length();

    float LengthSquared();

    static abstract TSelf Normalize(TSelf value);
}

public interface IInnerProductSpace<TSelf, TScalar>
    where TSelf : struct, IInnerProductSpace<TSelf, TScalar>
    where TScalar : struct
{
    static abstract TScalar Dot(TSelf left, TSelf right);
}

public interface ICrossProductSpace<TSelf>
    where TSelf : struct, ICrossProductSpace<TSelf>
{
    static abstract TSelf Cross(TSelf left, TSelf right);
}

public interface IInterpolatableSpace<TSelf, TScalar>
    where TSelf : struct, IInterpolatableSpace<TSelf, TScalar>
    where TScalar : struct
{
    static abstract TSelf Lerp(TSelf a, TSelf b, TScalar t);

    static abstract TSelf Slerp(TSelf a, TSelf b, TScalar t);
}

public interface INormalizationStrategy
{
    static abstract float ReciprocalSqrt(float lengthSquared);
}

public readonly struct StrictIeeeStrategy : INormalizationStrategy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ReciprocalSqrt(float lengthSquared) => 1.0f / MathF.Sqrt(lengthSquared);
}

public readonly struct FastApproximationStrategy : INormalizationStrategy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ReciprocalSqrt(float lengthSquared)
    {
        float estimate = MathF.ReciprocalSqrtEstimate(lengthSquared);
        return estimate * MathF.FusedMultiplyAdd(-0.5f * lengthSquared, estimate * estimate, 1.5f);
    }
}

public readonly struct HardwareIntrinsicsStrategy : INormalizationStrategy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ReciprocalSqrt(float lengthSquared)
    {
        if (System.Runtime.Intrinsics.X86.Sse.IsSupported)
        {
            Vector128<float> vec = Vector128.CreateScalar(lengthSquared);
            Vector128<float> estimate = System.Runtime.Intrinsics.X86.Sse.ReciprocalSqrt(vec);
            Vector128<float> half = Vector128.CreateScalar(0.5f);
            Vector128<float> onePointFive = Vector128.CreateScalar(1.5f);
            Vector128<float> halfLen = Vector128.Multiply(half, vec);
            Vector128<float> estSquared = Vector128.Multiply(estimate, estimate);
            Vector128<float> factor = Vector128.Subtract(onePointFive, Vector128.Multiply(halfLen, estSquared));
            return Vector128.Multiply(estimate, factor).ToScalar();
        }

        return FastApproximationStrategy.ReciprocalSqrt(lengthSquared);
    }
}

public interface IVectorTransformer<TState>
    where TState : allows ref struct
{
    static abstract Vec3 Transform(Vec3 value, scoped ref TState state);
}

public interface IVectorAction<TState>
    where TState : allows ref struct
{
    static abstract void Invoke(scoped ref readonly Vec3 value, scoped ref TState state);
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct ComponentIndex : IEquatable<ComponentIndex>, IComparable<ComponentIndex>
{
    public readonly byte Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ComponentIndex(byte value) => Value = value;

    public static readonly ComponentIndex X = new(0);

    public static readonly ComponentIndex Y = new(1);

    public static readonly ComponentIndex Z = new(2);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ComponentIndex From(int index)
    {
        if ((uint)index >= 3U)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
        }

        return new ComponentIndex((byte)index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator ComponentIndex(int index) => From(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator int(ComponentIndex index) => index.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(ComponentIndex other) => Value.CompareTo(other.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <(ComponentIndex left, ComponentIndex right) => left.Value < right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >(ComponentIndex left, ComponentIndex right) => left.Value > right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <=(ComponentIndex left, ComponentIndex right) => left.Value <= right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >=(ComponentIndex left, ComponentIndex right) => left.Value >= right.Value;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct Tolerance : IEquatable<Tolerance>, IComparable<Tolerance>
{
    public readonly float Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Tolerance(float value)
    {
        if (value < 0.0f || float.IsNaN(value))
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(value));
        }

        Value = value;
    }

    public static Tolerance Zero => new(0.0f);

    public static Tolerance Machine => new(Vec3.MachineEpsilon);

    public static Tolerance Default => new(Vec3.DefaultTolerance);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator Tolerance(float value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator float(Tolerance tolerance) => tolerance.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(Tolerance other) => Value.CompareTo(other.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <(Tolerance left, Tolerance right) => left.Value < right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >(Tolerance left, Tolerance right) => left.Value > right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <=(Tolerance left, Tolerance right) => left.Value <= right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >=(Tolerance left, Tolerance right) => left.Value >= right.Value;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct AngleRadians : IEquatable<AngleRadians>, IComparable<AngleRadians>
{
    public readonly float Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public AngleRadians(float radians) => Value = radians;

    public static AngleRadians Zero => new(0.0f);

    public static AngleRadians Pi => new(MathF.PI);

    public static AngleRadians TwoPi => new(MathF.PI * 2.0f);

    public static AngleRadians HalfPi => new(MathF.PI * 0.5f);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static AngleRadians FromDegrees(float degrees) => new(degrees * (MathF.PI / 180.0f));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float ToDegrees() => Value * (180.0f / MathF.PI);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator AngleRadians(float radians) => new(radians);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator float(AngleRadians angle) => angle.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(AngleRadians other) => Value.CompareTo(other.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static AngleRadians operator +(AngleRadians left, AngleRadians right) => new(left.Value + right.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static AngleRadians operator -(AngleRadians left, AngleRadians right) => new(left.Value - right.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static AngleRadians operator -(AngleRadians angle) => new(-angle.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <(AngleRadians left, AngleRadians right) => left.Value < right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >(AngleRadians left, AngleRadians right) => left.Value > right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <=(AngleRadians left, AngleRadians right) => left.Value <= right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >=(AngleRadians left, AngleRadians right) => left.Value >= right.Value;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct UnitVec3 :
    IEquatable<UnitVec3>,
    IFormattable,
    ISpanFormattable,
    IUtf8SpanFormattable
{
    public readonly float X;

    public readonly float Y;

    public readonly float Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal UnitVec3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static UnitVec3 UnitX => new(1F, 0F, 0F);

    public static UnitVec3 UnitY => new(0F, 1F, 0F);

    public static UnitVec3 UnitZ => new(0F, 0F, 1F);

    public static UnitVec3 NegativeUnitX => new(-1F, 0F, 0F);

    public static UnitVec3 NegativeUnitY => new(0F, -1F, 0F);

    public static UnitVec3 NegativeUnitZ => new(0F, 0F, -1F);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator Vec3(UnitVec3 unit) => new(unit.X, unit.Y, unit.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static explicit operator UnitVec3(Vec3 value) => Vec3.ToUnit(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Vec3 AsVec3() => new(X, Y, Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Equals(UnitVec3 other) => X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is UnitVec3 other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator ==(UnitVec3 left, UnitVec3 right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator !=(UnitVec3 left, UnitVec3 right) => !left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static UnitVec3 operator -(UnitVec3 value) => new(-value.X, -value.Y, -value.Z);

    public override string ToString() => AsVec3().ToString();

    public string ToString(string? format, IFormatProvider? formatProvider) => AsVec3().ToString(format, formatProvider);

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        AsVec3().TryFormat(destination, out charsWritten, format, provider);

    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        AsVec3().TryFormat(utf8Destination, out bytesWritten, format, provider);
}

public enum NormalizationStatus : byte
{
    Success = 0,
    DegenerateZero = 1,
    NonFinite = 2
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct NormalizationResult : IEquatable<NormalizationResult>
{
    public readonly UnitVec3 Value;

    public readonly NormalizationStatus Status;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public NormalizationResult(UnitVec3 value)
    {
        Value = value;
        Status = NormalizationStatus.Success;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public NormalizationResult(NormalizationStatus status)
    {
        Value = default;
        Status = status;
    }

    public bool IsSuccess => Status == NormalizationStatus.Success;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryGetUnit(out UnitVec3 unit)
    {
        unit = Value;
        return Status == NormalizationStatus.Success;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TResult Match<TResult>(
        Func<UnitVec3, TResult> onSuccess,
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
    public bool Equals(NormalizationResult other) =>
        Status == other.Status && (Status != NormalizationStatus.Success || Value.Equals(other.Value));

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is NormalizationResult other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Value, Status);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator ==(NormalizationResult left, NormalizationResult right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator !=(NormalizationResult left, NormalizationResult right) => !left.Equals(right);
}
public sealed class Vec3FormattingOptions
{
    public string Separator
    {
        get;
        set => field = value ?? throw new ArgumentNullException(nameof(value));
    } = ", ";

    public string Prefix
    {
        get;
        set => field = value ?? throw new ArgumentNullException(nameof(value));
    } = "";

    public string Suffix
    {
        get;
        set => field = value ?? throw new ArgumentNullException(nameof(value));
    } = "";
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Vec3 :
    IEquatable<Vec3>,
    IAdditionOperators<Vec3, Vec3, Vec3>,
    IAdditiveIdentity<Vec3, Vec3>,
    ISubtractionOperators<Vec3, Vec3, Vec3>,
    IMultiplyOperators<Vec3, Vec3, Vec3>,
    IMultiplicativeIdentity<Vec3, Vec3>,
    IDivisionOperators<Vec3, Vec3, Vec3>,
    IUnaryNegationOperators<Vec3, Vec3>,
    IUnaryPlusOperators<Vec3, Vec3>,
    IBitwiseOperators<Vec3, Vec3, Vec3>,
    IFormattable,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IParsable<Vec3>,
    ISpanParsable<Vec3>,
    IUtf8SpanParsable<Vec3>,
    ISpatialVector<Vec3>,
    IInnerProductSpace<Vec3, float>,
    ICrossProductSpace<Vec3>,
    IInterpolatableSpace<Vec3, float>
{
    public const float MachineEpsilon = 1.1920929E-07F;

    public const float DefaultTolerance = MachineEpsilon * 8F;

    private const int StackTextCapacity = 256;

    private static readonly float s_allBitsSet = BitConverter.UInt32BitsToSingle(uint.MaxValue);

    public static readonly Vec3 Zero = new(0F, 0F, 0F);

    public static readonly Vec3 One = new(1F, 1F, 1F);

    public static readonly Vec3 UnitX = new(1F, 0F, 0F);

    public static readonly Vec3 UnitY = new(0F, 1F, 0F);

    public static readonly Vec3 UnitZ = new(0F, 0F, 1F);

    public static readonly Vec3 NegativeOne = new(-1F, -1F, -1F);

    public static readonly Vec3 NegativeUnitX = new(-1F, 0F, 0F);

    public static readonly Vec3 NegativeUnitY = new(0F, -1F, 0F);

    public static readonly Vec3 NegativeUnitZ = new(0F, 0F, -1F);

    public static readonly Vec3 NegativeZero = new(-0F, -0F, -0F);

    public static readonly Vec3 PositiveInfinity = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

    public static readonly Vec3 NegativeInfinity = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

    public static readonly Vec3 NaN = new(float.NaN, float.NaN, float.NaN);

    public static readonly Vec3 Epsilon = new(float.Epsilon, float.Epsilon, float.Epsilon);

    public static readonly Vec3 Pi = new(MathF.PI, MathF.PI, MathF.PI);

    public static readonly Vec3 Tau = new(MathF.Tau, MathF.Tau, MathF.Tau);

    public static readonly Vec3 E = new(MathF.E, MathF.E, MathF.E);

    public static readonly Vec3 AllBitsSet = new(s_allBitsSet, s_allBitsSet, s_allBitsSet);

    public float X;

    public float Y;

    public float Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec3(float value) => X = Y = Z = value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Vec3(ReadOnlySpan<float> values)
    {
        if (values.Length < 3)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(values));
        }

        X = values[0];
        Y = values[1];
        Z = values[2];
    }

    // vec2 slice: Vec3(vec2 xy, float z) — deferred until Axrone.Numeric.vec2 exists.

    public float this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= 3U)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
            }

            return Unsafe.Add(ref X, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if ((uint)index >= 3U)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
            }

            Unsafe.Add(ref X, index) = value;
        }
    }

    #pragma warning disable CA1043 // ComponentIndex is the whole point: a 0..2 proof checked at construction.
    public float this[ComponentIndex index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => Unsafe.Add(ref X, (nint)index.Value);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set => Unsafe.Add(ref X, (nint)index.Value) = value;
    }
    #pragma warning restore CA1043

    public static Vec3 AdditiveIdentity => Zero;

    public static Vec3 MultiplicativeIdentity => One;

    public readonly bool IsAllZero => X == 0F && Y == 0F && Z == 0F;

    public readonly bool IsAllFinite => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z);

    public readonly bool IsAnyNaN => float.IsNaN(X) || float.IsNaN(Y) || float.IsNaN(Z);

    public readonly bool IsAnyInfinity => float.IsInfinity(X) || float.IsInfinity(Y) || float.IsInfinity(Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Deconstruct(out float x, out float y, out float z)
    {
        x = X;
        y = Y;
        z = Z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ComponentEnumerator GetEnumerator() => new(in this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<float> AsSpan() => MemoryMarshal.CreateSpan(ref X, 3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<float> AsReadOnlySpan() => MemoryMarshal.CreateReadOnlySpan(ref X, 3);

    public readonly void CopyTo(Span<float> destination)
    {
        if (destination.Length < 3)
        {
            NumericThrowHelper.ThrowArgumentException("Destination must hold at least three components.");
        }

        destination[0] = X;
        destination[1] = Y;
        destination[2] = Z;
    }

    public readonly void CopyTo(Span<byte> destination)
    {
        if (destination.Length < 12)
        {
            NumericThrowHelper.ThrowArgumentException("Destination must hold at least twelve bytes.");
        }

        float x = X;
        float y = Y;
        float z = Z;
        MemoryMarshal.Write(destination, in x);
        MemoryMarshal.Write(destination[4..], in y);
        MemoryMarshal.Write(destination[8..], in z);
    }

    public readonly void CopyTo(Span<Vec3> destination)
    {
        if (destination.IsEmpty)
        {
            NumericThrowHelper.ThrowArgumentException("Destination must hold at least one vector.");
        }

        destination[0] = this;
    }

    public readonly void CopyTo(Vec3[] destination, int index)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if ((uint)index >= (uint)destination.Length)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
        }

        destination[index] = this;
    }

    public readonly bool TryCopyTo(Span<float> destination)
    {
        if (destination.Length < 3)
        {
            return false;
        }

        destination[0] = X;
        destination[1] = Y;
        destination[2] = Z;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector128<float> AsVector128() => Vector128.Create(X, Y, Z, 0F);

    public static explicit operator Vector128<float>(Vec3 value) => value.AsVector128();

    public static explicit operator Vec3(Vector128<float> value) =>
        new(value.GetElement(0), value.GetElement(1), value.GetElement(2));

    public static implicit operator Vec3((float X, float Y, float Z) value) => new(value.X, value.Y, value.Z);

    public static implicit operator (float X, float Y, float Z)(Vec3 value) => (value.X, value.Y, value.Z);

    public static explicit operator System.Numerics.Vector3(Vec3 value) => Unsafe.BitCast<Vec3, System.Numerics.Vector3>(value);

    public static explicit operator Vec3(System.Numerics.Vector3 value) => Unsafe.BitCast<System.Numerics.Vector3, Vec3>(value);

    public readonly System.Numerics.Vector3 ToSystemNumerics() => (System.Numerics.Vector3)this;

    public static Vec3 FromSystemNumerics(System.Numerics.Vector3 value) => (Vec3)value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Create(float x, float y, float z) => new(x, y, z);

    // vec2 slice: Create(vec2 xy, float z) — deferred until Axrone.Numeric.vec2 exists.

    // vec2 slice: Transform(Vec3 value, Matrix4x4 transform) — deferred to the mat4 slice.
    // vec2 slice: TransformNormal(Vec3 value, Matrix4x4 transform) — deferred to the mat4 slice.
    // vec2 slice: Transform(Vec3 value, Quaternion rotation) — deferred to the quat slice.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 CreateScalar(float value) => new(value, value, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vec3 CreateScalarUnsafe(float value)
    {
        Unsafe.SkipInit(out Vec3 result);
        float* destination = (float*)Unsafe.AsPointer(ref result);
        destination[0] = value;
        destination[1] = value;
        destination[2] = value;
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Load(ReadOnlySpan<float> source)
    {
        if (source.Length < 3)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(source));
        }

        return new Vec3(source[0], source[1], source[2]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vec3 LoadAligned(ref readonly float source)
    {
        Vector128<float> wide = Vector128.LoadAligned((float*)Unsafe.AsPointer(ref Unsafe.AsRef(in source)));
        return new Vec3(wide.GetElement(0), wide.GetElement(1), wide.GetElement(2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vec3 LoadAlignedNonTemporal(ref readonly float source)
    {
        Vector128<float> wide = Vector128.LoadAlignedNonTemporal((float*)Unsafe.AsPointer(ref Unsafe.AsRef(in source)));
        return new Vec3(wide.GetElement(0), wide.GetElement(1), wide.GetElement(2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vec3 LoadUnsafe(void* source)
    {
        float* components = (float*)source;
        return new Vec3(components[0], components[1], components[2]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vec3 LoadUnsafe(void* source, int offset)
    {
        if (offset < 0)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(offset));
        }

        float* components = (float*)source + offset;
        return new Vec3(components[0], components[1], components[2]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Normalize<TStrategy>(Vec3 value)
        where TStrategy : struct, INormalizationStrategy
    {
        float lengthSquared = value.X * value.X + value.Y * value.Y + value.Z * value.Z;
        if (lengthSquared > 0.0f)
        {
            float invLength = TStrategy.ReciprocalSqrt(lengthSquared);
            if (float.IsFinite(invLength) && invLength > 0.0f)
            {
                return new Vec3(value.X * invLength, value.Y * invLength, value.Z * invLength);
            }
            float len = MathF.Sqrt(lengthSquared);
            if (len > 0.0f)
            {
                return new Vec3(value.X / len, value.Y / len, value.Z / len);
            }
        }
        return Zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator +(Vec3 left, Vec3 right) =>
        new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator -(Vec3 left, Vec3 right) =>
        new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator *(Vec3 left, Vec3 right) =>
        new(left.X * right.X, left.Y * right.Y, left.Z * right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator *(Vec3 left, float right) =>
        new(left.X * right, left.Y * right, left.Z * right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator *(float left, Vec3 right) =>
        new(left * right.X, left * right.Y, left * right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator /(Vec3 left, Vec3 right) =>
        new(left.X / right.X, left.Y / right.Y, left.Z / right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator /(Vec3 left, float right) =>
        new(left.X / right, left.Y / right, left.Z / right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator -(Vec3 value) => new(-value.X, -value.Y, -value.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator +(Vec3 value) => value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator ~(Vec3 value) =>
        new(BitConverter.Int32BitsToSingle(~BitConverter.SingleToInt32Bits(value.X)),
            BitConverter.Int32BitsToSingle(~BitConverter.SingleToInt32Bits(value.Y)),
            BitConverter.Int32BitsToSingle(~BitConverter.SingleToInt32Bits(value.Z)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator &(Vec3 left, Vec3 right) =>
        new(BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(left.X) & BitConverter.SingleToInt32Bits(right.X)),
            BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(left.Y) & BitConverter.SingleToInt32Bits(right.Y)),
            BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(left.Z) & BitConverter.SingleToInt32Bits(right.Z)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator |(Vec3 left, Vec3 right) =>
        new(BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(left.X) | BitConverter.SingleToInt32Bits(right.X)),
            BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(left.Y) | BitConverter.SingleToInt32Bits(right.Y)),
            BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(left.Z) | BitConverter.SingleToInt32Bits(right.Z)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 operator ^(Vec3 left, Vec3 right) =>
        new(BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(left.X) ^ BitConverter.SingleToInt32Bits(right.X)),
            BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(left.Y) ^ BitConverter.SingleToInt32Bits(right.Y)),
            BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(left.Z) ^ BitConverter.SingleToInt32Bits(right.Z)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vec3 left, Vec3 right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vec3 left, Vec3 right) => !left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(Vec3 other) => X == other.X && Y == other.Y && Z == other.Z;

    public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is Vec3 other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(X, Y, Z);

    public override string ToString() => ToString(null, CultureInfo.InvariantCulture);

    public string ToString(string? format) => ToString(format, CultureInfo.InvariantCulture);

    public string ToString(Vec3FormattingOptions? options)
    {
        options ??= new Vec3FormattingOptions();
        return options.Prefix
            + X.ToString(null, CultureInfo.InvariantCulture) + options.Separator
            + Y.ToString(null, CultureInfo.InvariantCulture) + options.Separator
            + Z.ToString(null, CultureInfo.InvariantCulture) + options.Suffix;
    }

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        Span<char> buffer = stackalloc char[StackTextCapacity];
        if (TryFormat(buffer, out int charsWritten, format, formatProvider))
        {
            return buffer[..charsWritten].ToString();
        }

        return string.Join(", ", X.ToString(format, formatProvider), Y.ToString(format, formatProvider), Z.ToString(format, formatProvider));
    }

    public readonly bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        provider ??= CultureInfo.CurrentCulture;
        int offset = 0;

        if (!TryAppendComponent(destination, ref offset, X, format, provider) ||
            !TryAppendComponent(destination, ref offset, Y, format, provider) ||
            !TryAppendComponent(destination, ref offset, Z, format, provider))
        {
            charsWritten = 0;
            return false;
        }

        charsWritten = offset;
        return true;
    }

    public readonly bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        Span<char> characters = stackalloc char[StackTextCapacity];
        if (!TryFormat(characters, out int charsWritten, format, provider))
        {
            bytesWritten = 0;
            return false;
        }

        ReadOnlySpan<char> text = characters[..charsWritten];
        int required = System.Text.Encoding.UTF8.GetByteCount(text);
        if (required > utf8Destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        System.Text.Encoding.UTF8.GetBytes(text, utf8Destination);
        bytesWritten = required;
        return true;
    }

    public static Vec3 Parse(string s, IFormatProvider? provider = null)
    {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan(), provider);
    }

    public static Vec3 Parse(ReadOnlySpan<char> s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, provider, out Vec3 result))
        {
            NumericThrowHelper.ThrowFormatException("Expected three comma-separated floating-point components, for example \"1, 2, 3\".");
        }

        return result;
    }

    public static Vec3 Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider = null)
    {
        if (!TryParse(utf8Text, provider, out Vec3 result))
        {
            NumericThrowHelper.ThrowFormatException("Expected three comma-separated floating-point components, for example \"1, 2, 3\".");
        }

        return result;
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Vec3 result) =>
        TryParse(s.AsSpan(), provider, out result);

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Vec3 result)
    {
        provider ??= CultureInfo.CurrentCulture;
        s = s.Trim();

        if (s.Length >= 2 && IsOpeningBracket(s[0]) && IsClosingBracket(s[^1]))
        {
            s = s[1..^1];
        }

        Span<float> components = stackalloc float[3];
        ReadOnlySpan<char> remaining = s;
        bool sawTrailingSeparator = false;

        for (int index = 0; index < 3; index++)
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

        result = new Vec3(components[0], components[1], components[2]);
        return true;
    }

    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Vec3 result)
    {
        int required = System.Text.Encoding.UTF8.GetCharCount(utf8Text);
        Span<char> characters = required <= StackTextCapacity ? stackalloc char[StackTextCapacity] : new char[required];
        int charsWritten = System.Text.Encoding.UTF8.GetChars(utf8Text, characters);
        return TryParse(characters[..charsWritten], provider, out result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsOpeningBracket(char value) => value is '(' or '[' or '{';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsClosingBracket(char value) => value is ')' or ']' or '}';

    private static bool TryAppendComponent(Span<char> destination, ref int offset, float value, ReadOnlySpan<char> format, IFormatProvider provider)
    {
        Span<char> component = stackalloc char[64];

        if (!value.TryFormat(component, out int componentLength, format, provider))
        {
            return false;
        }

        if (offset > 0)
        {
            if (destination.Length <= offset)
            {
                return false;
            }

            destination[offset] = ',';
            offset++;
        }

        if (componentLength > destination.Length - offset)
        {
            return false;
        }

        component[..componentLength].CopyTo(destination[offset..]);
        offset += componentLength;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(Vec3 left, Vec3 right) =>
        MathF.FusedMultiplyAdd(left.X, right.X, MathF.FusedMultiplyAdd(left.Y, right.Y, left.Z * right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DotStrict(Vec3 left, Vec3 right) =>
        (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Cross(Vec3 left, Vec3 right) =>
        new(
            (left.Y * right.Z) - (left.Z * right.Y),
            (left.Z * right.X) - (left.X * right.Z),
            (left.X * right.Y) - (left.Y * right.X)
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float LengthSquared() => Dot(this, this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float Length() => MathF.Sqrt(LengthSquared());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceSquared(Vec3 value1, Vec3 value2)
    {
        float dx = value1.X - value2.X;
        float dy = value1.Y - value2.Y;
        float dz = value1.Z - value2.Z;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Distance(Vec3 value1, Vec3 value2) =>
        MathF.Sqrt(DistanceSquared(value1, value2));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Normalize(Vec3 value)
    {
        float lengthSquared = value.LengthSquared();
        if (lengthSquared > 0.0f)
        {
            float invLength = 1.0f / MathF.Sqrt(lengthSquared);
            if (float.IsFinite(invLength) && invLength > 0.0f)
            {
                return new Vec3(value.X * invLength, value.Y * invLength, value.Z * invLength);
            }

            float len = MathF.Sqrt(lengthSquared);
            if (len > 0.0f)
            {
                return new Vec3(value.X / len, value.Y / len, value.Z / len);
            }
        }

        return Zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryNormalize(Vec3 value, out Vec3 result) =>
        TryNormalize(value, out result, 0.0f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryNormalize(Vec3 value, out Vec3 result, float tolerance)
    {
        float lengthSquared = value.LengthSquared();
        float tolSquared = tolerance > 0.0f ? tolerance * tolerance : 0.0f;
        if (lengthSquared > tolSquared && lengthSquared > 0.0f)
        {
            float invLength = 1.0f / MathF.Sqrt(lengthSquared);
            if (float.IsFinite(invLength) && invLength > 0.0f)
            {
                result = new Vec3(value.X * invLength, value.Y * invLength, value.Z * invLength);
                return true;
            }

            float len = MathF.Sqrt(lengthSquared);
            if (len > 0.0f)
            {
                result = new Vec3(value.X / len, value.Y / len, value.Z / len);
                return true;
            }
        }

        result = Zero;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool TryNormalize(Vec3 value, out Vec3 result, Tolerance tolerance) =>
        TryNormalize(value, out result, tolerance.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static UnitVec3 ToUnit<TStrategy>(Vec3 value)
        where TStrategy : struct, INormalizationStrategy
    {
        Vec3 normalized = Normalize<TStrategy>(value);
        return new UnitVec3(normalized.X, normalized.Y, normalized.Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static UnitVec3 ToUnit(Vec3 value) => ToUnit<StrictIeeeStrategy>(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static NormalizationResult TryNormalizeUnit(Vec3 value, Tolerance tolerance = default)
    {
        if (TryNormalize(value, out Vec3 result, tolerance.Value))
        {
            return new NormalizationResult(new UnitVec3(result.X, result.Y, result.Z));
        }

        if (value.IsAnyNaN || value.IsAnyInfinity)
        {
            return new NormalizationResult(NormalizationStatus.NonFinite);
        }

        return new NormalizationResult(NormalizationStatus.DegenerateZero);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Reflect(Vec3 vector, Vec3 normal)
    {
        float dot2 = Dot(vector, normal) * 2.0f;
        return new Vec3(
            vector.X - (normal.X * dot2),
            vector.Y - (normal.Y * dot2),
            vector.Z - (normal.Z * dot2)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Project(Vec3 vector, Vec3 onNormal)
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
    public static Vec3 Reflect(Vec3 vector, UnitVec3 normal)
    {
        float dot2 = Dot(vector, normal) * 2.0f;
        return new Vec3(
            vector.X - (normal.X * dot2),
            vector.Y - (normal.Y * dot2),
            vector.Z - (normal.Z * dot2)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec3 Project(Vec3 vector, UnitVec3 onNormal)
    {
        float scale = Dot(vector, onNormal);
        return (Vec3)onNormal * scale;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 ProjectOnPlane(Vec3 vector, Vec3 planeNormal) =>
        vector - Project(vector, planeNormal);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec3 ProjectOnPlane(Vec3 vector, UnitVec3 planeNormal) =>
        vector - Project(vector, planeNormal);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec3 Slide(Vec3 vector, UnitVec3 normal) =>
        vector - ((Vec3)normal * Dot(vector, normal));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Slide(Vec3 vector, Vec3 normal)
    {
        float normalSq = Dot(normal, normal);
        if (normalSq <= 0.0f)
        {
            return vector;
        }

        return vector - (normal * (Dot(vector, normal) / normalSq));
    }

    public static float Angle(Vec3 from, Vec3 to)
    {
        float maxA = MathF.Max(MathF.Abs(from.X), MathF.Max(MathF.Abs(from.Y), MathF.Abs(from.Z)));
        float maxB = MathF.Max(MathF.Abs(to.X), MathF.Max(MathF.Abs(to.Y), MathF.Abs(to.Z)));
        if (maxA <= 0.0f || maxB <= 0.0f)
        {
            return 0.0f;
        }

        Vec3 a = from * (1.0f / maxA);
        Vec3 b = to * (1.0f / maxB);

        float lenA = a.Length();
        float lenB = b.Length();
        if (lenA <= 0.0f || lenB <= 0.0f)
        {
            return 0.0f;
        }

        float cos = Dot(a, b) / (lenA * lenB);
        return MathF.Acos(Math.Clamp(cos, -1.0f, 1.0f));
    }

    public static float SignedAngle(Vec3 from, Vec3 to, Vec3 axis)
    {
        float unsignedAngle = Angle(from, to);
        Vec3 cross = Cross(from, to);
        float sign = Dot(axis, cross);
        return sign < 0.0f ? -unsignedAngle : unsignedAngle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static AngleRadians AngleBetween(Vec3 from, Vec3 to) => new(Angle(from, to));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float Angle(UnitVec3 from, UnitVec3 to) =>
        MathF.Acos(Math.Clamp(
            from.X * to.X + from.Y * to.Y + from.Z * to.Z,
            -1.0f, 1.0f));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec3 Slerp(UnitVec3 a, UnitVec3 b, float t)
    {
        float dot = Math.Clamp(
            a.X * b.X + a.Y * b.Y + a.Z * b.Z,
            -1.0f, 1.0f);

        if (dot > 0.9995f)
        {
            return Normalize(Lerp(a, b, t));
        }

        if (dot < -0.9995f)
        {
            Vec3 ortho = GetOrthogonal(a);
            float angle = MathF.PI * t;
            return ((Vec3)a * MathF.Cos(angle)) + (ortho * MathF.Sin(angle));
        }

        float theta = MathF.Acos(dot);
        float sinTheta = MathF.Sin(theta);
        float factorA = MathF.Sin((1.0f - t) * theta) / sinTheta;
        float factorB = MathF.Sin(t * theta) / sinTheta;

        return ((Vec3)a * factorA) + ((Vec3)b * factorB);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static AngleRadians SignedAngleBetween(Vec3 from, Vec3 to, Vec3 axis) => new(SignedAngle(from, to, axis));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Clamp(Vec3 value, Vec3 min, Vec3 max)
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

        return new Vec3(x, y, z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 ClampNative(Vec3 value, Vec3 min, Vec3 max) => Clamp(value, min, max);

    public static Vec3 ClampLength(Vec3 value, float minLength, float maxLength)
    {
        float sqrMagnitude = value.LengthSquared();
        if (sqrMagnitude <= 0.0f)
        {
            return minLength > 0.0f ? new Vec3(minLength, 0.0f, 0.0f) : Zero;
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
    public static Vec3 Min(Vec3 left, Vec3 right) =>
        new(MathF.Min(left.X, right.X), MathF.Min(left.Y, right.Y), MathF.Min(left.Z, right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Max(Vec3 left, Vec3 right) =>
        new(MathF.Max(left.X, right.X), MathF.Max(left.Y, right.Y), MathF.Max(left.Z, right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 MinNative(Vec3 left, Vec3 right) => Min(left, right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 MaxNative(Vec3 left, Vec3 right) => Max(left, right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 MinNumber(Vec3 left, Vec3 right) =>
        new(float.MinNumber(left.X, right.X), float.MinNumber(left.Y, right.Y), float.MinNumber(left.Z, right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 MaxNumber(Vec3 left, Vec3 right) =>
        new(float.MaxNumber(left.X, right.X), float.MaxNumber(left.Y, right.Y), float.MaxNumber(left.Z, right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 MinMagnitude(Vec3 left, Vec3 right) =>
        new(float.MinMagnitude(left.X, right.X), float.MinMagnitude(left.Y, right.Y), float.MinMagnitude(left.Z, right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 MaxMagnitude(Vec3 left, Vec3 right) =>
        new(float.MaxMagnitude(left.X, right.X), float.MaxMagnitude(left.Y, right.Y), float.MaxMagnitude(left.Z, right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 MinMagnitudeNumber(Vec3 left, Vec3 right) =>
        new(float.MinMagnitudeNumber(left.X, right.X), float.MinMagnitudeNumber(left.Y, right.Y), float.MinMagnitudeNumber(left.Z, right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 MaxMagnitudeNumber(Vec3 left, Vec3 right) =>
        new(float.MaxMagnitudeNumber(left.X, right.X), float.MaxMagnitudeNumber(left.Y, right.Y), float.MaxMagnitudeNumber(left.Z, right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 CopySign(Vec3 value, Vec3 sign) =>
        new(MathF.CopySign(value.X, sign.X), MathF.CopySign(value.Y, sign.Y), MathF.CopySign(value.Z, sign.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 FusedMultiplyAdd(Vec3 left, Vec3 right, Vec3 addend) =>
        new(
            MathF.FusedMultiplyAdd(left.X, right.X, addend.X),
            MathF.FusedMultiplyAdd(left.Y, right.Y, addend.Y),
            MathF.FusedMultiplyAdd(left.Z, right.Z, addend.Z)
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 MultiplyAddEstimate(Vec3 left, Vec3 right, Vec3 addend) =>
        FusedMultiplyAdd(left, right, addend);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Abs(Vec3 value) =>
        new(MathF.Abs(value.X), MathF.Abs(value.Y), MathF.Abs(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Sqrt(Vec3 value) =>
        new(MathF.Sqrt(value.X), MathF.Sqrt(value.Y), MathF.Sqrt(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 SquareRoot(Vec3 value) => Sqrt(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Sin(Vec3 vector) =>
        new(MathF.Sin(vector.X), MathF.Sin(vector.Y), MathF.Sin(vector.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Cos(Vec3 vector) =>
        new(MathF.Cos(vector.X), MathF.Cos(vector.Y), MathF.Cos(vector.Z));

    public static (Vec3 Sin, Vec3 Cos) SinCos(Vec3 vector)
    {
        (float sX, float cX) = MathF.SinCos(vector.X);
        (float sY, float cY) = MathF.SinCos(vector.Y);
        (float sZ, float cZ) = MathF.SinCos(vector.Z);
        return (new Vec3(sX, sY, sZ), new Vec3(cX, cY, cZ));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Exp(Vec3 vector) =>
        new(MathF.Exp(vector.X), MathF.Exp(vector.Y), MathF.Exp(vector.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Log(Vec3 vector) =>
        new(MathF.Log(vector.X), MathF.Log(vector.Y), MathF.Log(vector.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Log2(Vec3 vector) =>
        new(MathF.Log2(vector.X), MathF.Log2(vector.Y), MathF.Log2(vector.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Hypot(Vec3 x, Vec3 y) => Sqrt((x * x) + (y * y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 DegreesToRadians(Vec3 degrees) => degrees * (MathF.PI / 180.0f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 RadiansToDegrees(Vec3 radians) => radians * (180.0f / MathF.PI);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Floor(Vec3 value) =>
        new(MathF.Floor(value.X), MathF.Floor(value.Y), MathF.Floor(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Ceiling(Vec3 value) =>
        new(MathF.Ceiling(value.X), MathF.Ceiling(value.Y), MathF.Ceiling(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Round(Vec3 value) =>
        new(MathF.Round(value.X), MathF.Round(value.Y), MathF.Round(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Truncate(Vec3 value) =>
        new(MathF.Truncate(value.X), MathF.Truncate(value.Y), MathF.Truncate(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Lerp(Vec3 a, Vec3 b, float t) =>
        new(
            MathF.FusedMultiplyAdd(b.X - a.X, t, a.X),
            MathF.FusedMultiplyAdd(b.Y - a.Y, t, a.Y),
            MathF.FusedMultiplyAdd(b.Z - a.Z, t, a.Z)
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 LerpClamped(Vec3 a, Vec3 b, float t) =>
        Lerp(a, b, Math.Clamp(t, 0.0f, 1.0f));

    public static Vec3 SmoothStep(Vec3 from, Vec3 to, float amount)
    {
        amount = Math.Clamp(amount, 0.0f, 1.0f);
        float factor = amount * amount * (3.0f - (2.0f * amount));
        return Lerp(from, to, factor);
    }

    public static Vec3 Slerp(Vec3 a, Vec3 b, float t)
    {
        float lenA = a.Length();
        float lenB = b.Length();

        if (lenA <= 0.0f || lenB <= 0.0f)
        {
            return Lerp(a, b, t);
        }

        float targetLength = MathF.FusedMultiplyAdd(lenB - lenA, t, lenA);
        Vec3 unitA = a / lenA;
        Vec3 unitB = b / lenB;

        float dot = Math.Clamp(Dot(unitA, unitB), -1.0f, 1.0f);

        if (dot > 0.9995f)
        {
            Vec3 result = Lerp(unitA, unitB, t);
            return Normalize(result) * targetLength;
        }

        if (dot < -0.9995f)
        {
            Vec3 ortho = GetOrthogonal(unitA);
            float angle = MathF.PI * t;
            return ((unitA * MathF.Cos(angle)) + (ortho * MathF.Sin(angle))) * targetLength;
        }

        float theta = MathF.Acos(dot);
        float sinTheta = MathF.Sin(theta);
        float factorA = MathF.Sin((1.0f - t) * theta) / sinTheta;
        float factorB = MathF.Sin(t * theta) / sinTheta;

        return ((unitA * factorA) + (unitB * factorB)) * targetLength;
    }

    public static Vec3 GetOrthogonal(Vec3 v)
    {
        if (v.LengthSquared() <= 0.0f)
        {
            return UnitX;
        }

        Vec3 abs = Abs(v);
        Vec3 other = abs.X < abs.Y
            ? (abs.X < abs.Z ? UnitX : UnitZ)
            : (abs.Y < abs.Z ? UnitY : UnitZ);
        return Normalize(Cross(v, other));
    }

    public static Vec3 MoveTowards(Vec3 current, Vec3 target, float maxDistanceDelta)
    {
        float dx = target.X - current.X;
        float dy = target.Y - current.Y;
        float dz = target.Z - current.Z;
        float distSq = (dx * dx) + (dy * dy) + (dz * dz);

        if (distSq == 0.0f || (maxDistanceDelta >= 0.0f && distSq <= maxDistanceDelta * maxDistanceDelta))
        {
            return target;
        }

        float dist = MathF.Sqrt(distSq);
        float ratio = maxDistanceDelta / dist;
        return new Vec3(
            MathF.FusedMultiplyAdd(dx, ratio, current.X),
            MathF.FusedMultiplyAdd(dy, ratio, current.Y),
            MathF.FusedMultiplyAdd(dz, ratio, current.Z)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 TransformCustom<TTransformer, TState>(Vec3 value, scoped ref TState state)
        where TTransformer : IVectorTransformer<TState>
        where TState : allows ref struct =>
        TTransformer.Transform(value, ref state);

    public static Vec3 SumAll(params ReadOnlySpan<Vec3> vectors)
    {
        Vec3 accumulator = Zero;
        for (int i = 0; i < vectors.Length; i++)
        {
            accumulator += vectors[i];
        }

        return accumulator;
    }

    public static Vec3 Average(params ReadOnlySpan<Vec3> vectors)
    {
        if (vectors.IsEmpty)
        {
            NumericThrowHelper.ThrowArgumentException("Average requires at least one vector.");
        }

        return SumAll(vectors) / (float)vectors.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sum(Vec3 vector) => vector.X + vector.Y + vector.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool All(Vec3 vector) => vector.X != 0.0f && vector.Y != 0.0f && vector.Z != 0.0f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AllWhereAllBitsSet(Vec3 vector) =>
        BitConverter.SingleToUInt32Bits(vector.X) == 0xFFFF_FFFF &&
        BitConverter.SingleToUInt32Bits(vector.Y) == 0xFFFF_FFFF &&
        BitConverter.SingleToUInt32Bits(vector.Z) == 0xFFFF_FFFF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Any(Vec3 vector) => vector.X != 0.0f || vector.Y != 0.0f || vector.Z != 0.0f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AnyWhereAllBitsSet(Vec3 vector) =>
        BitConverter.SingleToUInt32Bits(vector.X) == 0xFFFF_FFFF ||
        BitConverter.SingleToUInt32Bits(vector.Y) == 0xFFFF_FFFF ||
        BitConverter.SingleToUInt32Bits(vector.Z) == 0xFFFF_FFFF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool None(Vec3 vector) => !Any(vector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NoneWhereAllBitsSet(Vec3 vector) => !AnyWhereAllBitsSet(vector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Count(Vec3 vector)
    {
        int count = 0;
        if (vector.X != 0.0f)
        {
            count++;
        }

        if (vector.Y != 0.0f)
        {
            count++;
        }

        if (vector.Z != 0.0f)
        {
            count++;
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountWhereAllBitsSet(Vec3 vector)
    {
        int count = 0;
        if (BitConverter.SingleToUInt32Bits(vector.X) == 0xFFFF_FFFF)
        {
            count++;
        }

        if (BitConverter.SingleToUInt32Bits(vector.Y) == 0xFFFF_FFFF)
        {
            count++;
        }

        if (BitConverter.SingleToUInt32Bits(vector.Z) == 0xFFFF_FFFF)
        {
            count++;
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsAll(Vec3 left, Vec3 right) => left == right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsAny(Vec3 left, Vec3 right) =>
        left.X == right.X || left.Y == right.Y || left.Z == right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(Vec3 other, float tolerance) =>
        MathF.Abs(X - other.X) <= tolerance &&
        MathF.Abs(Y - other.Y) <= tolerance &&
        MathF.Abs(Z - other.Z) <= tolerance;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool Equals(Vec3 other, Tolerance tolerance) => Equals(other, tolerance.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool Equals(Vec3 left, Vec3 right, Tolerance tolerance) =>
        left.Equals(right, tolerance.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Equals(Vec3 left, Vec3 right, float tolerance = DefaultTolerance) =>
        left.Equals(right, tolerance);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool BitEquals(Vec3 other) =>
        BitConverter.SingleToUInt32Bits(X) == BitConverter.SingleToUInt32Bits(other.X) &&
        BitConverter.SingleToUInt32Bits(Y) == BitConverter.SingleToUInt32Bits(other.Y) &&
        BitConverter.SingleToUInt32Bits(Z) == BitConverter.SingleToUInt32Bits(other.Z);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Add(Vec3 left, Vec3 right) => left + right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Subtract(Vec3 left, Vec3 right) => left - right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Multiply(Vec3 left, Vec3 right) => left * right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Multiply(Vec3 value, float scalar) => value * scalar;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Multiply(float scalar, Vec3 value) => scalar * value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Divide(Vec3 left, Vec3 right) => left / right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Divide(Vec3 value, float scalar) => value / scalar;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Negate(Vec3 value) => -value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 BitwiseAnd(Vec3 left, Vec3 right) => left & right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 BitwiseOr(Vec3 left, Vec3 right) => left | right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Xor(Vec3 left, Vec3 right) => left ^ right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 OnesComplement(Vec3 value) => ~value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 AndNot(Vec3 left, Vec3 right) => left & ~right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vec3 Mask(bool x, bool y, bool z) => new(
        x ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFFu) : 0F,
        y ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFFu) : 0F,
        z ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFFu) : 0F);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 IsNaN(Vec3 value) => Mask(float.IsNaN(value.X), float.IsNaN(value.Y), float.IsNaN(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 IsFinite(Vec3 value) => Mask(float.IsFinite(value.X), float.IsFinite(value.Y), float.IsFinite(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 IsInfinity(Vec3 value) => Mask(float.IsInfinity(value.X), float.IsInfinity(value.Y), float.IsInfinity(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 IsNormal(Vec3 value) => Mask(float.IsNormal(value.X), float.IsNormal(value.Y), float.IsNormal(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 IsSubnormal(Vec3 value) => Mask(float.IsSubnormal(value.X), float.IsSubnormal(value.Y), float.IsSubnormal(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 IsZero(Vec3 value) => Mask(value.X == 0F, value.Y == 0F, value.Z == 0F);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 IsNegative(Vec3 value) => Mask(float.IsNegative(value.X), float.IsNegative(value.Y), float.IsNegative(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 IsPositive(Vec3 value) => Mask(float.IsPositive(value.X), float.IsPositive(value.Y), float.IsPositive(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 IsNegativeInfinity(Vec3 value) => Mask(float.IsNegativeInfinity(value.X), float.IsNegativeInfinity(value.Y), float.IsNegativeInfinity(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 IsPositiveInfinity(Vec3 value) => Mask(float.IsPositiveInfinity(value.X), float.IsPositiveInfinity(value.Y), float.IsPositiveInfinity(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 IsInteger(Vec3 value) => Mask(float.IsInteger(value.X), float.IsInteger(value.Y), float.IsInteger(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 IsEvenInteger(Vec3 value) => Mask(float.IsEvenInteger(value.X), float.IsEvenInteger(value.Y), float.IsEvenInteger(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 IsOddInteger(Vec3 value) => Mask(float.IsOddInteger(value.X), float.IsOddInteger(value.Y), float.IsOddInteger(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 GreaterThan(Vec3 left, Vec3 right) => Mask(left.X > right.X, left.Y > right.Y, left.Z > right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GreaterThanAll(Vec3 left, Vec3 right) => left.X > right.X && left.Y > right.Y && left.Z > right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GreaterThanAny(Vec3 left, Vec3 right) => left.X > right.X || left.Y > right.Y || left.Z > right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 GreaterThanOrEqual(Vec3 left, Vec3 right) => Mask(left.X >= right.X, left.Y >= right.Y, left.Z >= right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GreaterThanOrEqualAll(Vec3 left, Vec3 right) => left.X >= right.X && left.Y >= right.Y && left.Z >= right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GreaterThanOrEqualAny(Vec3 left, Vec3 right) => left.X >= right.X || left.Y >= right.Y || left.Z >= right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 LessThan(Vec3 left, Vec3 right) => Mask(left.X < right.X, left.Y < right.Y, left.Z < right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool LessThanAll(Vec3 left, Vec3 right) => left.X < right.X && left.Y < right.Y && left.Z < right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool LessThanAny(Vec3 left, Vec3 right) => left.X < right.X || left.Y < right.Y || left.Z < right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 LessThanOrEqual(Vec3 left, Vec3 right) => Mask(left.X <= right.X, left.Y <= right.Y, left.Z <= right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool LessThanOrEqualAll(Vec3 left, Vec3 right) => left.X <= right.X && left.Y <= right.Y && left.Z <= right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool LessThanOrEqualAny(Vec3 left, Vec3 right) => left.X <= right.X || left.Y <= right.Y || left.Z <= right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 ConditionalSelect(Vec3 condition, Vec3 trueValue, Vec3 falseValue) =>
        (condition & trueValue) | (~condition & falseValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOf(Vec3 value, Vec3 target)
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
    public static int LastIndexOf(Vec3 value, Vec3 target)
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
    public static int IndexOfWhereAllBitsSet(Vec3 value)
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
    public static int LastIndexOfWhereAllBitsSet(Vec3 value)
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
    public static Vec3 Shuffle(Vec3 value, byte x, byte y, byte z)
    {
        float rx = x == 0 ? value.X : x == 1 ? value.Y : x == 2 ? value.Z : 0F;
        float ry = y == 0 ? value.X : y == 1 ? value.Y : y == 2 ? value.Z : 0F;
        float rz = z == 0 ? value.X : z == 1 ? value.Y : z == 2 ? value.Z : 0F;
        return new Vec3(rx, ry, rz);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 RotateX(Vec3 vector, float angleRadians)
    {
        var (sin, cos) = MathF.SinCos(angleRadians);
        return new Vec3(
            vector.X,
            MathF.FusedMultiplyAdd(vector.Y, cos, -(vector.Z * sin)),
            MathF.FusedMultiplyAdd(vector.Y, sin, vector.Z * cos)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec3 RotateX(float angleRadians) => RotateX(this, angleRadians);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 RotateY(Vec3 vector, float angleRadians)
    {
        var (sin, cos) = MathF.SinCos(angleRadians);
        return new Vec3(
            MathF.FusedMultiplyAdd(vector.X, cos, vector.Z * sin),
            vector.Y,
            MathF.FusedMultiplyAdd(vector.Z, cos, -(vector.X * sin))
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec3 RotateY(float angleRadians) => RotateY(this, angleRadians);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 RotateZ(Vec3 vector, float angleRadians)
    {
        var (sin, cos) = MathF.SinCos(angleRadians);
        return new Vec3(
            MathF.FusedMultiplyAdd(vector.X, cos, -(vector.Y * sin)),
            MathF.FusedMultiplyAdd(vector.X, sin, vector.Y * cos),
            vector.Z
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec3 RotateZ(float angleRadians) => RotateZ(this, angleRadians);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 RotateAxis(Vec3 vector, Vec3 axis, float angleRadians)
    {
        var (sin, cos) = MathF.SinCos(angleRadians);
        float oneMinusCos = 1.0f - cos;
        float dot = Dot(axis, vector);
        Vec3 cross = Cross(axis, vector);
        float scale = dot * oneMinusCos;

        return new Vec3(
            MathF.FusedMultiplyAdd(vector.X, cos, MathF.FusedMultiplyAdd(cross.X, sin, axis.X * scale)),
            MathF.FusedMultiplyAdd(vector.Y, cos, MathF.FusedMultiplyAdd(cross.Y, sin, axis.Y * scale)),
            MathF.FusedMultiplyAdd(vector.Z, cos, MathF.FusedMultiplyAdd(cross.Z, sin, axis.Z * scale))
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec3 RotateAxis(Vec3 axis, float angleRadians) => RotateAxis(this, axis, angleRadians);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec3 RotateX(Vec3 vector, AngleRadians angle) => RotateX(vector, angle.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly Vec3 RotateX(AngleRadians angle) => RotateX(this, angle.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec3 RotateY(Vec3 vector, AngleRadians angle) => RotateY(vector, angle.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly Vec3 RotateY(AngleRadians angle) => RotateY(this, angle.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec3 RotateZ(Vec3 vector, AngleRadians angle) => RotateZ(vector, angle.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly Vec3 RotateZ(AngleRadians angle) => RotateZ(this, angle.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec3 RotateAxis(Vec3 vector, Vec3 axis, AngleRadians angle) =>
        RotateAxis(vector, axis, angle.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly Vec3 RotateAxis(Vec3 axis, AngleRadians angle) => RotateAxis(this, axis, angle.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 Inverse(Vec3 vector) =>
        new(1.0f / vector.X, 1.0f / vector.Y, 1.0f / vector.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec3 Inverse() => Inverse(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 InverseSafe(Vec3 vector, float fallback = 0.0f)
    {
        float invX = 1.0f / vector.X;
        float invY = 1.0f / vector.Y;
        float invZ = 1.0f / vector.Z;

        return new Vec3(
            float.IsFinite(invX) ? invX : fallback,
            float.IsFinite(invY) ? invY : fallback,
            float.IsFinite(invZ) ? invZ : fallback
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec3 InverseSafe(float fallback = 0.0f) => InverseSafe(this, fallback);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 DivideSafe(Vec3 left, Vec3 right, float fallback = 0.0f)
    {
        float x = left.X / right.X;
        float y = left.Y / right.Y;
        float z = left.Z / right.Z;

        return new Vec3(
            float.IsFinite(x) ? x : fallback,
            float.IsFinite(y) ? y : fallback,
            float.IsFinite(z) ? z : fallback
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 DivideSafe(Vec3 left, float right, float fallback = 0.0f)
    {
        float inv = 1.0f / right;
        return float.IsFinite(inv) ? left * inv : new Vec3(fallback);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ManhattanDistance(Vec3 a, Vec3 b) =>
        MathF.Abs(a.X - b.X) + MathF.Abs(a.Y - b.Y) + MathF.Abs(a.Z - b.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float ManhattanDistance(Vec3 destination) => ManhattanDistance(this, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ChebyshevDistance(Vec3 a, Vec3 b)
    {
        float dx = MathF.Abs(a.X - b.X);
        float dy = MathF.Abs(a.Y - b.Y);
        float dz = MathF.Abs(a.Z - b.Z);
        return MathF.Max(dx, MathF.Max(dy, dz));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float ChebyshevDistance(Vec3 destination) => ChebyshevDistance(this, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 SmootherStep(Vec3 from, Vec3 to, float amount)
    {
        amount = Math.Clamp(amount, 0.0f, 1.0f);
        float inner = MathF.FusedMultiplyAdd(amount, 6.0f, -15.0f);
        float poly = MathF.FusedMultiplyAdd(amount, inner, 10.0f);
        float factor = amount * amount * amount * poly;

        return Lerp(from, to, factor);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 AddScalar(Vec3 vector, float scalar) =>
        new(vector.X + scalar, vector.Y + scalar, vector.Z + scalar);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec3 AddScalar(float scalar) => AddScalar(this, scalar);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec3 SubtractScalar(Vec3 vector, float scalar) =>
        new(vector.X - scalar, vector.Y - scalar, vector.Z - scalar);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vec3 SubtractScalar(float scalar) => SubtractScalar(this, scalar);
}

public ref struct ComponentEnumerator
{
    private readonly Vec3 _vector;

    private int _index;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal ComponentEnumerator(scoped ref readonly Vec3 vector)
    {
        _vector = vector;
        _index = -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool MoveNext() => ++_index < 3;

    public readonly ref readonly float Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            if ((uint)_index >= 3U)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(_index));
            }

            return ref Unsafe.Add(ref Unsafe.AsRef(in _vector.X), (nint)(uint)_index);
        }
    }
}
