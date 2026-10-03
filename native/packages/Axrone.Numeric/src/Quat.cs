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
    public bool IsSuccess => _tag == NormalizationOutcomeTag.Normalized;
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
public struct Quat :
    IEquatable<Quat>,
    IEqualityOperators<Quat, Quat, bool>,
    IAdditionOperators<Quat, Quat, Quat>,
    ISubtractionOperators<Quat, Quat, Quat>,
    IMultiplyOperators<Quat, Quat, Quat>,
    IMultiplyOperators<Quat, float, Quat>,
    IDivisionOperators<Quat, Quat, Quat>,
    IDivisionOperators<Quat, float, Quat>,
    IUnaryNegationOperators<Quat, Quat>,
    IUnaryPlusOperators<Quat, Quat>,
    IAdditiveIdentity<Quat, Quat>,
    IMultiplicativeIdentity<Quat, Quat>,
    IFormattable,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IParsable<Quat>,
    ISpanParsable<Quat>,
    IUtf8SpanParsable<Quat>,
    ISpatialVector<Quat>,
    IInnerProductSpace<Quat, float>,
    IInterpolatableSpace<Quat, float>
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Span<float> AsSpan() => MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in X), 4);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ReadOnlySpan<float> AsReadOnlySpan() => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in X), 4);

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
    public readonly void CopyTo(Span<byte> destination)
    {
        if (destination.Length < 16)
        {
            NumericThrowHelper.ThrowArgumentException(nameof(destination), "Destination must hold at least sixteen bytes.");
        }

        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(destination), this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(Span<Quat> destination)
    {
        if (destination.IsEmpty)
        {
            NumericThrowHelper.ThrowArgumentException(nameof(destination), "Destination must hold at least one quaternion.");
        }

        destination[0] = this;
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
    public readonly void CopyTo(Quat[] destination, int index)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if ((uint)index >= (uint)destination.Length)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
        }

        destination[index] = this;
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
    public readonly bool TryCopyTo(Span<byte> destination)
    {
        if (destination.Length < 16)
        {
            return false;
        }

        Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(destination), this);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quat Create(float x, float y, float z, float w) => new(x, y, z, w);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quat Create(ReadOnlySpan<float> values) => new(values);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quat Load(ReadOnlySpan<float> source)
    {
        if (source.Length < 4)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(source));
        }

        return new Quat(source[0], source[1], source[2], source[3]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Quat LoadAligned(ref readonly float source)
    {
        Vector128<float> wide = Vector128.LoadAligned((float*)Unsafe.AsPointer(ref Unsafe.AsRef(in source)));
        return FromVector128(wide);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Quat LoadAlignedNonTemporal(ref readonly float source)
    {
        Vector128<float> wide = Vector128.LoadAlignedNonTemporal((float*)Unsafe.AsPointer(ref Unsafe.AsRef(in source)));
        return FromVector128(wide);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Quat LoadUnsafe(void* source)
    {
        float* components = (float*)source;
        return new Quat(components[0], components[1], components[2], components[3]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Quat LoadUnsafe(void* source, int offset)
    {
        if (offset < 0)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(offset));
        }

        float* components = (float*)source + offset;
        return new Quat(components[0], components[1], components[2], components[3]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Quat Load(float* source) => new(source[0], source[1], source[2], source[3]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Quat LoadAligned(float* source) => FromVector128(Vector128.LoadAligned(source));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Quat LoadAlignedNonTemporal(float* source) => FromVector128(Vector128.LoadAlignedNonTemporal(source));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quat LoadUnsafe(ref readonly float source) =>
        new(source, Unsafe.Add(ref Unsafe.AsRef(in source), 1), Unsafe.Add(ref Unsafe.AsRef(in source), 2), Unsafe.Add(ref Unsafe.AsRef(in source), 3));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quat LoadUnsafe(ref readonly float source, nuint elementOffset) =>
        LoadUnsafe(ref Unsafe.Add(ref Unsafe.AsRef(in source), elementOffset));


    public static Quat AdditiveIdentity => Zero;

    public static Quat MultiplicativeIdentity => Identity;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly Vector128<float> AsVector128() =>
        Unsafe.BitCast<Quat, Vector128<float>>(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat FromVector128(Vector128<float> vector) =>
        Unsafe.BitCast<Vector128<float>, Quat>(vector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Quat(Vector128<float> vector) => FromVector128(vector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Vector128<float>(Quat quaternion) => quaternion.AsVector128();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator (float X, float Y, float Z, float W)(Quat value) =>
        (value.X, value.Y, value.Z, value.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Quat((float X, float Y, float Z, float W) value) =>
        new(value.X, value.Y, value.Z, value.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator System.Numerics.Quaternion(Quat q) =>
        Unsafe.BitCast<Quat, System.Numerics.Quaternion>(q);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Quat(System.Numerics.Quaternion q) =>
        Unsafe.BitCast<System.Numerics.Quaternion, Quat>(q);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly System.Numerics.Quaternion ToSystemNumerics() => (System.Numerics.Quaternion)this;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quat FromSystemNumerics(System.Numerics.Quaternion value) => (Quat)value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Inspect<TVisitor, TState>(ref TState state)
        where TVisitor : struct, IQuatVisitor<TState>
        where TState : allows ref struct
    {
        TVisitor.Visit(ref state, X, 0);
        TVisitor.Visit(ref state, Y, 1);
        TVisitor.Visit(ref state, Z, 2);
        TVisitor.Visit(ref state, W, 3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Project<TConsumer, TContext>(ref TContext context)
        where TConsumer : struct, IQuatSpanConsumer<TContext>
        where TContext : allows ref struct
    {
        TConsumer.Consume(AsReadOnlySpan(), ref context);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Vec4(Quat q) => new(q.X, q.Y, q.Z, q.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Quat(Vec4 v) => new(v.X, v.Y, v.Z, v.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float Dot(Quat left, Quat right)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            return Vector128.Dot(left.AsVector128(), right.AsVector128());
        }
        return MathF.FusedMultiplyAdd(left.X, right.X,
            MathF.FusedMultiplyAdd(left.Y, right.Y,
            MathF.FusedMultiplyAdd(left.Z, right.Z, left.W * right.W)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float Dot<TArithmetic>(Quat left, Quat right)
        where TArithmetic : struct, IArithmeticPolicy =>
        TArithmetic.MultiplyAdd(left.X, right.X,
            TArithmetic.MultiplyAdd(left.Y, right.Y,
            TArithmetic.MultiplyAdd(left.Z, right.Z, left.W * right.W)));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float DotStrict(Quat left, Quat right) =>
        (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z) + (left.W * right.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float LengthSquared() => Dot(this, this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float Length() => MathF.Sqrt(LengthSquared());

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly Quat Conjugate()
    {
        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> mask = Vector128.Create(-0.0f, -0.0f, -0.0f, 0.0f);
            return FromVector128(Vector128.Xor(AsVector128(), mask));
        }
        return new Quat(-X, -Y, -Z, W);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quat Conjugate(Quat value) => value.Conjugate();

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly Quat Inverse() => Inverse<ReturnZeroPolicy>(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat Inverse(Quat value) => Inverse<ReturnZeroPolicy>(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat Inverse<TSingularPolicy>(Quat value)
        where TSingularPolicy : struct, ISingularityPolicy
    {
        float lenSq = value.LengthSquared();
        if (lenSq > 0.0f)
        {
            float inv = 1.0f / lenSq;
            if (float.IsFinite(inv))
            {
                return new Quat(-value.X * inv, -value.Y * inv, -value.Z * inv, value.W * inv);
            }
        }
        return TSingularPolicy.OnSingularity(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly Quat Normalize() => Normalize<ReturnIdentityPolicy>(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat Normalize(Quat value) => Normalize<ReturnIdentityPolicy>(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat Normalize<TSingularPolicy>(Quat value)
        where TSingularPolicy : struct, ISingularityPolicy
    {
        float lengthSquared = value.LengthSquared();
        if (lengthSquared > 0.0f)
        {
            float invLength = 1.0f / MathF.Sqrt(lengthSquared);
            if (float.IsFinite(invLength))
            {
                return value * invLength;
            }
        }
        return TSingularPolicy.OnSingularity(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static NormalizationOutcome TryNormalizeExact(Quat value, float tolerance = 0.0f)
    {
        if (value.IsAnyNaN || value.IsAnyInfinity)
        {
            return NormalizationOutcome.Failure(NormalizationOutcomeTag.NonFinite);
        }

        float lenSq = value.LengthSquared();
        if (lenSq == 0.0f)
        {
            return NormalizationOutcome.Failure(NormalizationOutcomeTag.ZeroMagnitude);
        }

        float tolSq = tolerance > 0.0f ? tolerance * tolerance : 0.0f;
        if (lenSq <= tolSq)
        {
            return NormalizationOutcome.Failure(NormalizationOutcomeTag.Subnormal);
        }

        float invLen = 1.0f / MathF.Sqrt(lenSq);
        if (!float.IsFinite(invLen))
        {
            return NormalizationOutcome.Failure(NormalizationOutcomeTag.Subnormal);
        }

        return NormalizationOutcome.Success(value * invLen);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryNormalize(Quat value, out Quat result) =>
        TryNormalize(value, out result, 0.0f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryNormalize(Quat value, out Quat result, float tolerance) =>
        TryNormalizeExact(value, tolerance).TryGet(out result);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat CreateFromAxisAngle(Vec3 axis, float angleRadians)
    {
        var (sin, cos) = MathF.SinCos(angleRadians * 0.5f);
        return new Quat(axis.X * sin, axis.Y * sin, axis.Z * sin, cos);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat CreateFromAxisAngle(UnitAxis3 axis, AngleRadians angle)
    {
        var (sin, cos) = MathF.SinCos(angle.Value * 0.5f);
        return new Quat(axis.X * sin, axis.Y * sin, axis.Z * sin, cos);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat CreateFromYawPitchRoll(float yaw, float pitch, float roll) =>
        CreateFromYawPitchRoll(new AngleRadians(yaw), new AngleRadians(pitch), new AngleRadians(roll));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat CreateFromYawPitchRoll(AngleRadians yaw, AngleRadians pitch, AngleRadians roll)
    {
        var (sRoll, cRoll) = MathF.SinCos(roll.Value * 0.5f);
        var (sPitch, cPitch) = MathF.SinCos(pitch.Value * 0.5f);
        var (sYaw, cYaw) = MathF.SinCos(yaw.Value * 0.5f);

        return new Quat(
            MathF.FusedMultiplyAdd(cYaw * sPitch, cRoll, sYaw * cPitch * sRoll),
            MathF.FusedMultiplyAdd(sYaw * cPitch, cRoll, -(cYaw * sPitch * sRoll)),
            MathF.FusedMultiplyAdd(cYaw * cPitch, sRoll, -(sYaw * sPitch * cRoll)),
            MathF.FusedMultiplyAdd(cYaw * cPitch, cRoll, sYaw * sPitch * sRoll)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat CreateFromEuler(EulerAngles angles) =>
        CreateFromEuler(angles.X, angles.Y, angles.Z, angles.Order);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat CreateFromEuler(AngleRadians x, AngleRadians y, AngleRadians z, RotationOrder order)
    {
        var (sx, cx) = MathF.SinCos(x.Value * 0.5f);
        var (sy, cy) = MathF.SinCos(y.Value * 0.5f);
        var (sz, cz) = MathF.SinCos(z.Value * 0.5f);

        Quat qx = new(sx, 0.0f, 0.0f, cx);
        Quat qy = new(0.0f, sy, 0.0f, cy);
        Quat qz = new(0.0f, 0.0f, sz, cz);

        return order switch
        {
            RotationOrder.Zyx => qz * qy * qx,
            RotationOrder.Xyz => qx * qy * qz,
            RotationOrder.Xzy => qx * qz * qy,
            RotationOrder.Yxz => qy * qx * qz,
            RotationOrder.Yzx => qy * qz * qx,
            RotationOrder.Zxy => qz * qx * qy,
            _ => qz * qy * qx
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat CreateFromRotationMatrix(System.Numerics.Matrix4x4 matrix)
    {
        float trace = matrix.M11 + matrix.M22 + matrix.M33;

        if (trace > 0.0f)
        {
            float s = MathF.Sqrt(trace + 1.0f);
            float inv = 0.5f / s;
            return new Quat(
                (matrix.M23 - matrix.M32) * inv,
                (matrix.M31 - matrix.M13) * inv,
                (matrix.M12 - matrix.M21) * inv,
                s * 0.5f
            );
        }

        if (matrix.M11 >= matrix.M22 && matrix.M11 >= matrix.M33)
        {
            float s = MathF.Sqrt(MathF.Max(0.0f, 1.0f + matrix.M11 - matrix.M22 - matrix.M33));
            float inv = 0.5f / s;
            return new Quat(
                0.5f * s,
                (matrix.M12 + matrix.M21) * inv,
                (matrix.M13 + matrix.M31) * inv,
                (matrix.M23 - matrix.M32) * inv
            );
        }

        if (matrix.M22 > matrix.M33)
        {
            float s = MathF.Sqrt(MathF.Max(0.0f, 1.0f + matrix.M22 - matrix.M11 - matrix.M33));
            float inv = 0.5f / s;
            return new Quat(
                (matrix.M21 + matrix.M12) * inv,
                0.5f * s,
                (matrix.M32 + matrix.M23) * inv,
                (matrix.M31 - matrix.M13) * inv
            );
        }

        float sZ = MathF.Sqrt(MathF.Max(0.0f, 1.0f + matrix.M33 - matrix.M11 - matrix.M22));
        float invZ = 0.5f / sZ;
        return new Quat(
            (matrix.M31 + matrix.M13) * invZ,
            (matrix.M32 + matrix.M23) * invZ,
            0.5f * sZ,
            (matrix.M12 - matrix.M21) * invZ
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat FromToRotation(Vec3 fromDirection, Vec3 toDirection)
    {
        float dot = Vec3.Dot(fromDirection, toDirection);

        if (dot >= 0.999999f)
        {
            return Identity;
        }

        if (dot <= -0.999999f)
        {
            Vec3 axis = Vec3.GetOrthogonal(fromDirection);
            return new Quat(axis.X, axis.Y, axis.Z, 0.0f);
        }

        Vec3 cross = Vec3.Cross(fromDirection, toDirection);
        float s = MathF.Sqrt((1.0f + dot) * 2.0f);
        float inv = 1.0f / s;

        return new Quat(cross.X * inv, cross.Y * inv, cross.Z * inv, s * 0.5f);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static Quat LookRotation(Vec3 forward, Vec3 up)
    {
        forward = Vec3.Normalize(forward);
        Vec3 right = Vec3.Normalize(Vec3.Cross(up, forward));
        up = Vec3.Cross(forward, right);

        System.Numerics.Matrix4x4 m = new(
            right.X, right.Y, right.Z, 0.0f,
            up.X, up.Y, up.Z, 0.0f,
            forward.X, forward.Y, forward.Z, 0.0f,
            0.0f, 0.0f, 0.0f, 1.0f
        );

        return CreateFromRotationMatrix(m);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly void DecomposeSwingTwist(UnitAxis3 twistAxis, out Quat swing, out Quat twist)
    {
        Vec3 axis = twistAxis.AsVec3();
        Vec3 projection = axis * Vec3.Dot(new Vec3(X, Y, Z), axis);
        Quat rawTwist = new(projection.X, projection.Y, projection.Z, W);
        if (!TryNormalize(rawTwist, out twist))
        {
            twist = Identity;
        }
        swing = this * twist.Conjugate();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly void GetAxisAngle(out Vec3 axis, out float angleRadians)
    {
        float clampedW = Math.Clamp(W, -1.0f, 1.0f);
        angleRadians = MathF.Acos(clampedW) * 2.0f;
        float s = MathF.Sqrt(1.0f - (clampedW * clampedW));

        if (s <= 0.0001f)
        {
            axis = Vec3.UnitX;
        }
        else
        {
            axis = new Vec3(X / s, Y / s, Z / s);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly void GetAxisAngle(out UnitAxis3 axis, out AngleRadians angle)
    {
        GetAxisAngle(out Vec3 vAxis, out float angleRad);
        axis = UnitAxis3.CreateUnchecked(vAxis.X, vAxis.Y, vAxis.Z);
        angle = new AngleRadians(angleRad);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float Angle(Quat a, Quat b)
    {
        float dot = MathF.Abs(Dot(a, b));
        return dot > 0.999999f ? 0.0f : MathF.Acos(Math.Clamp(dot, -1.0f, 1.0f)) * 2.0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AngleRadians AngleBetween(Quat a, Quat b) => new(Angle(a, b));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat Lerp(Quat a, Quat b, float t)
    {
        float cosHalfTheta = Dot(a, b);
        float scale = cosHalfTheta < 0.0f ? -t : t;
        float invT = 1.0f - t;

        return Normalize(new Quat(
            MathF.FusedMultiplyAdd(b.X, scale, a.X * invT),
            MathF.FusedMultiplyAdd(b.Y, scale, a.Y * invT),
            MathF.FusedMultiplyAdd(b.Z, scale, a.Z * invT),
            MathF.FusedMultiplyAdd(b.W, scale, a.W * invT)
        ));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quat Slerp(Quat a, Quat b, float t) =>
        Slerp<ShortestPathPolicy>(a, b, t);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat Slerp<TPathPolicy>(Quat a, Quat b, float t)
        where TPathPolicy : struct, ISlerpPathPolicy
    {
        float cosHalfTheta = Dot(a, b);
        Quat target = TPathPolicy.AdjustTarget(b, cosHalfTheta);
        cosHalfTheta = MathF.Abs(cosHalfTheta);

        if (cosHalfTheta > 0.9995f)
        {
            return Lerp(a, target, t);
        }

        float halfTheta = MathF.Acos(Math.Clamp(cosHalfTheta, -1.0f, 1.0f));
        float sinHalfTheta = MathF.Sin(halfTheta);
        float invSin = 1.0f / sinHalfTheta;
        float ratioA = MathF.Sin((1.0f - t) * halfTheta) * invSin;
        float ratioB = MathF.Sin(t * halfTheta) * invSin;

        return new Quat(
            MathF.FusedMultiplyAdd(a.X, ratioA, target.X * ratioB),
            MathF.FusedMultiplyAdd(a.Y, ratioA, target.Y * ratioB),
            MathF.FusedMultiplyAdd(a.Z, ratioA, target.Z * ratioB),
            MathF.FusedMultiplyAdd(a.W, ratioA, target.W * ratioB)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat Squad(Quat p, Quat a, Quat b, Quat q, float t)
    {
        Quat slerpPq = Slerp(p, q, t);
        Quat slerpAb = Slerp(a, b, t);
        return Slerp(slerpPq, slerpAb, 2.0f * t * (1.0f - t));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quat Concatenate(Quat value1, Quat value2) => value2 * value1;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat operator +(Quat left, Quat right)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            return FromVector128(Vector128.Add(left.AsVector128(), right.AsVector128()));
        }
        return new(left.X + right.X, left.Y + right.Y, left.Z + right.Z, left.W + right.W);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat operator -(Quat left, Quat right)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            return FromVector128(Vector128.Subtract(left.AsVector128(), right.AsVector128()));
        }
        return new(left.X - right.X, left.Y - right.Y, left.Z - right.Z, left.W - right.W);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat operator *(Quat left, Quat right) =>
        new(
            MathF.FusedMultiplyAdd(left.W, right.X, MathF.FusedMultiplyAdd(left.X, right.W, MathF.FusedMultiplyAdd(left.Y, right.Z, -(left.Z * right.Y)))),
            MathF.FusedMultiplyAdd(left.W, right.Y, MathF.FusedMultiplyAdd(left.Y, right.W, MathF.FusedMultiplyAdd(left.Z, right.X, -(left.X * right.Z)))),
            MathF.FusedMultiplyAdd(left.W, right.Z, MathF.FusedMultiplyAdd(left.Z, right.W, MathF.FusedMultiplyAdd(left.X, right.Y, -(left.Y * right.X)))),
            MathF.FusedMultiplyAdd(left.W, right.W, -(MathF.FusedMultiplyAdd(left.X, right.X, MathF.FusedMultiplyAdd(left.Y, right.Y, left.Z * right.Z))))
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Vec3 operator *(Quat rotation, Vec3 point)
    {
        Vec3 qv = rotation.VectorPart;
        Vec3 t = Vec3.Cross(qv, point) * 2.0f;
        return point + (t * rotation.W) + Vec3.Cross(qv, t);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat operator *(Quat left, float right)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            return FromVector128(Vector128.Multiply(left.AsVector128(), Vector128.Create(right)));
        }
        return new(left.X * right, left.Y * right, left.Z * right, left.W * right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quat operator *(float left, Quat right) => right * left;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quat operator /(Quat left, Quat right) => left * right.Inverse();

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat operator /(Quat left, float right)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            return FromVector128(Vector128.Divide(left.AsVector128(), Vector128.Create(right)));
        }
        float inv = 1.0f / right;
        return new(left.X * inv, left.Y * inv, left.Z * inv, left.W * inv);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat operator -(Quat value)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            return FromVector128(Vector128.Negate(value.AsVector128()));
        }
        return new(-value.X, -value.Y, -value.Z, -value.W);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quat operator +(Quat value) => value;


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Quat left, Quat right)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            return Vector128.EqualsAll(left.AsVector128(), right.AsVector128());
        }
        return left.X == right.X && left.Y == right.Y && left.Z == right.Z && left.W == right.W;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Quat left, Quat right) => !(left == right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quat Add(Quat left, Quat right) => left + right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quat Subtract(Quat left, Quat right) => left - right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quat Multiply(Quat left, Quat right) => left * right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quat Multiply(Quat left, float right) => left * right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quat Multiply(float left, Quat right) => left * right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quat Divide(Quat left, Quat right) => left / right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quat Divide(Quat left, float right) => left / right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quat Negate(Quat value) => -value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat IsNaN(Quat quaternion)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            return FromVector128(Vector128.IsNaN(quaternion.AsVector128()));
        }
        return new(
            float.IsNaN(quaternion.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsNaN(quaternion.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsNaN(quaternion.Z) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsNaN(quaternion.W) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat IsFinite(Quat quaternion) =>
        new(
            float.IsFinite(quaternion.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsFinite(quaternion.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsFinite(quaternion.Z) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsFinite(quaternion.W) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat IsInfinity(Quat quaternion) =>
        new(
            float.IsInfinity(quaternion.X) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsInfinity(quaternion.Y) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsInfinity(quaternion.Z) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            float.IsInfinity(quaternion.W) ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Quat IsZero(Quat quaternion)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            return FromVector128(Vector128.Equals(quaternion.AsVector128(), Vector128<float>.Zero));
        }
        return new(
            quaternion.X == 0.0f ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            quaternion.Y == 0.0f ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            quaternion.Z == 0.0f ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f,
            quaternion.W == 0.0f ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFF) : 0.0f
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int CountWhereAllBitsSet(Quat value)
    {
        int count = 0;
        if (BitConverter.SingleToUInt32Bits(value.X) == 0xFFFF_FFFF) count++;
        if (BitConverter.SingleToUInt32Bits(value.Y) == 0xFFFF_FFFF) count++;
        if (BitConverter.SingleToUInt32Bits(value.Z) == 0xFFFF_FFFF) count++;
        if (BitConverter.SingleToUInt32Bits(value.W) == 0xFFFF_FFFF) count++;
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(Quat other) => this == other;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool Equals(Quat other, float tolerance) =>
        MathF.Abs(X - other.X) <= tolerance &&
        MathF.Abs(Y - other.Y) <= tolerance &&
        MathF.Abs(Z - other.Z) <= tolerance &&
        MathF.Abs(W - other.W) <= tolerance;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(Quat other, Tolerance tolerance) => Equals(other, tolerance.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Equals(Quat left, Quat right, float tolerance = DefaultTolerance) =>
        left.Equals(right, tolerance);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool BitEquals(Quat other)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<int> left = AsVector128().AsInt32();
            Vector128<int> right = other.AsVector128().AsInt32();
            return Vector128.EqualsAll(left, right);
        }
        return BitConverter.SingleToUInt32Bits(X) == BitConverter.SingleToUInt32Bits(other.X) &&
               BitConverter.SingleToUInt32Bits(Y) == BitConverter.SingleToUInt32Bits(other.Y) &&
               BitConverter.SingleToUInt32Bits(Z) == BitConverter.SingleToUInt32Bits(other.Z) &&
               BitConverter.SingleToUInt32Bits(W) == BitConverter.SingleToUInt32Bits(other.W);
    }

    public override readonly bool Equals([NotNullWhen(true)] object? obj) =>
        obj is Quat other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(X, Y, Z, W);

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

    public static Quat Parse(string s, IFormatProvider? provider = null)
    {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan(), provider);
    }

    public static Quat Parse(ReadOnlySpan<char> s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, provider, out Quat result))
        {
            NumericThrowHelper.ThrowFormatException("Expected four comma-separated floating-point components, for example \"1, 2, 3, 4\".");
        }

        return result;
    }

    public static Quat Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider = null)
    {
        if (!TryParse(utf8Text, provider, out Quat result))
        {
            NumericThrowHelper.ThrowFormatException("Expected four comma-separated floating-point components, for example \"1, 2, 3, 4\".");
        }

        return result;
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Quat result) =>
        TryParse(s.AsSpan(), provider, out result);

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Quat result)
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

        result = new Quat(components[0], components[1], components[2], components[3]);
        return true;
    }

    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Quat result)
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

        result = new Quat(components[0], components[1], components[2], components[3]);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public QuatComponentEnumerator GetEnumerator() => new(in this);
}

public ref struct QuatComponentEnumerator
{
    private readonly Quat _quat;
    private int _index;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal QuatComponentEnumerator(scoped ref readonly Quat quat)
    {
        _quat = quat;
        _index = -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        int next = _index + 1;
        if (next < 4)
        {
            _index = next;
            return true;
        }
        return false;
    }

    public readonly float Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _quat[(nuint)(uint)_index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() => _index = -1;
}
