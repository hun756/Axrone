namespace Axrone.Numeric;

public interface IArithmeticPolicy
{
    static abstract float MultiplyAdd(float a, float b, float c);
    static abstract float StrictMultiplyAdd(float a, float b, float c);
}

public readonly struct FmaArithmeticPolicy : IArithmeticPolicy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float MultiplyAdd(float a, float b, float c) => MathF.FusedMultiplyAdd(a, b, c);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float StrictMultiplyAdd(float a, float b, float c) => (a * b) + c;
}

public readonly struct StrictArithmeticPolicy : IArithmeticPolicy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float MultiplyAdd(float a, float b, float c) => (a * b) + c;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float StrictMultiplyAdd(float a, float b, float c) => (a * b) + c;
}

public interface ISingularityPolicy
{
    static abstract Quat OnSingularity(Quat original);
}

public readonly struct ReturnIdentityPolicy : ISingularityPolicy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat OnSingularity(Quat original) => Quat.Identity;
}

public readonly struct ReturnZeroPolicy : ISingularityPolicy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat OnSingularity(Quat original) => Quat.Zero;
}

public readonly struct ThrowOnSingularPolicy : ISingularityPolicy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat OnSingularity(Quat original)
    {
        NumericThrowHelper.ThrowDegenerateQuaternion();
        return Quat.Identity;
    }
}

public interface ISlerpPathPolicy
{
    static abstract Quat AdjustTarget(Quat target, float dot);
}

public readonly struct ShortestPathPolicy : ISlerpPathPolicy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat AdjustTarget(Quat target, float dot) =>
        dot < 0.0f ? new Quat(-target.X, -target.Y, -target.Z, -target.W) : target;
}

public readonly struct DirectPathPolicy : ISlerpPathPolicy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat AdjustTarget(Quat target, float dot) => target;
}

public interface IQuatVisitor<TState> where TState : allows ref struct
{
    static abstract void Visit(ref TState state, float component, nuint index);
}

public interface IQuatSpanConsumer<TContext> where TContext : allows ref struct
{
    static abstract void Consume(ReadOnlySpan<float> components, ref TContext context);
}

public enum RotationOrder : byte
{
    Zyx = 0,
    Xyz = 1,
    Xzy = 2,
    Yxz = 3,
    Yzx = 4,
    Zxy = 5
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct EulerAngles(AngleRadians X, AngleRadians Y, AngleRadians Z, RotationOrder Order)
{
    public static EulerAngles Zero => new(AngleRadians.Zero, AngleRadians.Zero, AngleRadians.Zero, RotationOrder.Zyx);
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct UnitAxis3 : IEquatable<UnitAxis3>
{
    public float X { get; }
    public float Y { get; }
    public float Z { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private UnitAxis3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static UnitAxis3 UnitX => new(1.0f, 0.0f, 0.0f);
    public static UnitAxis3 UnitY => new(0.0f, 1.0f, 0.0f);
    public static UnitAxis3 UnitZ => new(0.0f, 0.0f, 1.0f);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool TryCreate(Vec3 vector, out UnitAxis3 axis, float tolerance = 1e-6f)
    {
        float lenSq = vector.LengthSquared();
        float tolSq = tolerance > 0.0f ? tolerance * tolerance : 0.0f;
        if (lenSq > tolSq && float.IsFinite(lenSq))
        {
            float invLen = 1.0f / MathF.Sqrt(lenSq);
            if (float.IsFinite(invLen))
            {
                axis = new UnitAxis3(vector.X * invLen, vector.Y * invLen, vector.Z * invLen);
                return true;
            }
        }
        axis = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static UnitAxis3 Create(Vec3 vector)
    {
        if (!TryCreate(vector, out UnitAxis3 axis))
        {
            NumericThrowHelper.ThrowInvalidAxis();
        }
        return axis;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UnitAxis3 CreateUnchecked(float x, float y, float z) => new(x, y, z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec3 AsVec3() => new(X, Y, Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Vec3(UnitAxis3 axis) => axis.AsVec3();
}

public enum NormalizationOutcomeTag : byte
{
    Normalized = 0,
    ZeroMagnitude = 1,
    Subnormal = 2,
    NonFinite = 3
}

[StructLayout(LayoutKind.Explicit, Size = 20)]
public readonly struct NormalizationOutcome : IEquatable<NormalizationOutcome>
{
    [FieldOffset(0)] private readonly Quat _value;
    [FieldOffset(16)] private readonly NormalizationOutcomeTag _tag;

    public NormalizationOutcomeTag Tag => _tag;
    public bool IsNormalized => _tag == NormalizationOutcomeTag.Normalized;
    public Quat Value => _value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal NormalizationOutcome(Quat value, NormalizationOutcomeTag tag)
    {
        _value = value;
        _tag = tag;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizationOutcome Success(Quat normalized) =>
        new(normalized, NormalizationOutcomeTag.Normalized);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NormalizationOutcome Failure(NormalizationOutcomeTag tag) =>
        new(Quat.Identity, tag);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet(out Quat result)
    {
        result = _value;
        return _tag == NormalizationOutcomeTag.Normalized;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Quat UnwrapOr(Quat fallback) =>
        _tag == NormalizationOutcomeTag.Normalized ? _value : fallback;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TResult Match<TResult>(
        Func<Quat, TResult> onSuccess,
        Func<TResult> onDegenerateZero,
        Func<TResult> onNonFinite)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onDegenerateZero);
        ArgumentNullException.ThrowIfNull(onNonFinite);

        return Tag switch
        {
            NormalizationOutcomeTag.Normalized => onSuccess(Value),
            NormalizationOutcomeTag.ZeroMagnitude => onDegenerateZero(),
            NormalizationOutcomeTag.NonFinite => onNonFinite(),
            _ => ThrowMatch(),
        };

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        static TResult ThrowMatch() =>
            throw new InvalidOperationException("Invalid normalization status discriminant.");
    }

    public bool Equals(NormalizationOutcome other) =>
        Tag == other.Tag && (Tag != NormalizationOutcomeTag.Normalized || Value.Equals(other.Value));

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is NormalizationOutcome other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Value, Tag);

    public static bool operator ==(NormalizationOutcome left, NormalizationOutcome right) => left.Equals(right);

    public static bool operator !=(NormalizationOutcome left, NormalizationOutcome right) => !left.Equals(right);
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Quat
{
    public readonly float X;

    public readonly float Y;

    public readonly float Z;

    public readonly float W;

    public const float MachineEpsilon = 1.1920929E-07F;

    public const float DefaultTolerance = MachineEpsilon * 8F;

    public static Quat Identity => new(0.0f, 0.0f, 0.0f, 1.0f);

    public static Quat Zero => default;

    public static Quat NegativeOne => new(-1.0f, -1.0f, -1.0f, -1.0f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Quat(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Quat(Vec3 vectorPart, float scalarPart)
    {
        X = vectorPart.X;
        Y = vectorPart.Y;
        Z = vectorPart.Z;
        W = scalarPart;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Quat(ReadOnlySpan<float> values)
    {
        if (values.Length < 4)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(values));
        }

        X = values[0];
        Y = values[1];
        Z = values[2];
        W = values[3];
    }

    public float this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= 4U)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
            }

            return Unsafe.Add(ref Unsafe.AsRef(in X), (nuint)(uint)index);
        }
    }

    #pragma warning disable CA1043 // nuint indexes SIMD lane code that counts in native words.
    public float this[nuint index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            if (index >= 4)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
            }
            return Unsafe.Add(ref Unsafe.AsRef(in X), index);
        }
    }
    #pragma warning restore CA1043

    public readonly bool IsIdentity => X == 0.0f && Y == 0.0f && Z == 0.0f && W == 1.0f;
    public readonly bool IsAllZero => X == 0.0f && Y == 0.0f && Z == 0.0f && W == 0.0f;
    public readonly bool IsAllFinite => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z) && float.IsFinite(W);
    public readonly bool IsAnyNaN => float.IsNaN(X) || float.IsNaN(Y) || float.IsNaN(Z) || float.IsNaN(W);
    public readonly bool IsAnyInfinity => float.IsInfinity(X) || float.IsInfinity(Y) || float.IsInfinity(Z) || float.IsInfinity(W);

    public readonly Vec3 VectorPart
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(X, Y, Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Deconstruct(out float x, out float y, out float z, out float w)
    {
        x = X;
        y = Y;
        z = Z;
        w = W;
    }
}
