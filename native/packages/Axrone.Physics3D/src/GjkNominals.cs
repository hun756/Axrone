namespace Axrone.Physics3D;

using System.Globalization;
using System.Numerics;

/// <summary>
/// Register-optimized, immutable IEEE 754 32-bit floating-point domain scalar wrapper.
/// Promoted to hardware floating-point registers with zero stack roundtrip.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct Scalar(float Value) :
    IEquatable<Scalar>,
    IComparable<Scalar>,
    IComparisonOperators<Scalar, Scalar, bool>,
    IAdditionOperators<Scalar, Scalar, Scalar>,
    ISubtractionOperators<Scalar, Scalar, Scalar>,
    IMultiplyOperators<Scalar, Scalar, Scalar>,
    IDivisionOperators<Scalar, Scalar, Scalar>,
    IUnaryNegationOperators<Scalar, Scalar>
{
    /// <summary>Additive identity.</summary>
    public static readonly Scalar Zero = new(0.0f);

    /// <summary>Multiplicative identity.</summary>
    public static readonly Scalar One = new(1.0f);

    /// <summary>Positive infinity sentinel.</summary>
    public static readonly Scalar PositiveInfinity = new(float.PositiveInfinity);

    /// <summary>Wraps a raw value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Scalar From(float value) => new(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >(Scalar left, Scalar right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >=(Scalar left, Scalar right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <(Scalar left, Scalar right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <=(Scalar left, Scalar right) => left.Value <= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Scalar operator +(Scalar left, Scalar right) => new(left.Value + right.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Scalar operator -(Scalar left, Scalar right) => new(left.Value - right.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Scalar operator *(Scalar left, Scalar right) => new(left.Value * right.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Scalar operator /(Scalar left, Scalar right) => new(left.Value / right.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Scalar operator -(Scalar value) => new(-value.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(Scalar other) => Value.CompareTo(other.Value);

    /// <summary>Non-negative square root as a distance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Distance Sqrt() => new(new Scalar(MathF.Sqrt(MathF.Max(0.0f, Value))));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override string ToString() => Value.ToString("G7", CultureInfo.InvariantCulture);
}

/// <summary>
/// Nominal strongly-typed Euclidean distance metric constrained to non-negative space.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct Distance(Scalar Value) :
    IEquatable<Distance>,
    IComparable<Distance>,
    IComparisonOperators<Distance, Distance, bool>
{
    /// <summary>Zero distance.</summary>
    public static readonly Distance Zero = new(Scalar.Zero);

    /// <summary>Wraps a raw value, rejecting negatives.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Distance From(float value)
    {
        if (value < 0.0f)
        {
            ThrowHelper.ThrowNegativeDistance();
        }
        return new Distance(Scalar.From(value));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >(Distance left, Distance right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >=(Distance left, Distance right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <(Distance left, Distance right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <=(Distance left, Distance right) => left.Value <= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(Distance other) => Value.CompareTo(other.Value);

    /// <summary>Squared form, avoiding a square root on the hot path.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public DistanceSquared Squared() => new(Value * Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Nominal squared distance value, avoiding expensive square-root calculations on the hot path.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct DistanceSquared(Scalar Value) :
    IEquatable<DistanceSquared>,
    IComparable<DistanceSquared>,
    IComparisonOperators<DistanceSquared, DistanceSquared, bool>
{
    /// <summary>Zero squared distance.</summary>
    public static readonly DistanceSquared Zero = new(Scalar.Zero);

    /// <summary>Infinite squared distance.</summary>
    public static readonly DistanceSquared PositiveInfinity = new(Scalar.PositiveInfinity);

    /// <summary>Wraps a raw value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static DistanceSquared From(float value) => new(Scalar.From(value));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >(DistanceSquared left, DistanceSquared right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >=(DistanceSquared left, DistanceSquared right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <(DistanceSquared left, DistanceSquared right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <=(DistanceSquared left, DistanceSquared right) => left.Value <= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(DistanceSquared other) => Value.CompareTo(other.Value);

    /// <summary>Square-root form as a distance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Distance ToDistance() => Value.Sqrt();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Strongly-typed 1-based monotonic iteration counter.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct IterationCount(uint Value) :
    IEquatable<IterationCount>,
    IComparable<IterationCount>,
    IComparisonOperators<IterationCount, IterationCount, bool>
{
    /// <summary>Zero iterations.</summary>
    public static readonly IterationCount Zero = new(0);

    /// <summary>Next iteration.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static IterationCount Increment(IterationCount current) => new(current.Value + 1);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >(IterationCount left, IterationCount right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >=(IterationCount left, IterationCount right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <(IterationCount left, IterationCount right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <=(IterationCount left, IterationCount right) => left.Value <= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(IterationCount other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Strictly validated iteration limit capacity.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct IterationLimit(uint Value) :
    IEquatable<IterationLimit>,
    IComparable<IterationLimit>,
    IComparisonOperators<IterationLimit, IterationLimit, bool>
{
    /// <summary>Default traversal budget.</summary>
    public static readonly IterationLimit Default = new(24);

    /// <summary>Wraps a raw value, rejecting zero.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static IterationLimit From(uint value)
    {
        if (value == 0)
        {
            ThrowHelper.ThrowInvalidIterationLimit();
        }
        return new IterationLimit(value);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >(IterationLimit left, IterationLimit right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >=(IterationLimit left, IterationLimit right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <(IterationLimit left, IterationLimit right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <=(IterationLimit left, IterationLimit right) => left.Value <= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(IterationLimit other) => Value.CompareTo(other.Value);

    /// <summary>Whether the counter reached this limit.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool HasReached(IterationCount count) => count.Value >= Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
