namespace Axrone.Numeric;

/// <summary>Nominal scalar strictly within [0.0, 1.0]. Register-promoted (4 bytes).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 4)]
public readonly record struct NormalizedFloat :
    IEquatable<NormalizedFloat>,
    IComparable<NormalizedFloat>,
    IComparisonOperators<NormalizedFloat, NormalizedFloat, bool>
{
    /// <summary>Clamped value.</summary>
    public readonly float Value;

    /// <summary>Creates a clamped normalized scalar.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public NormalizedFloat(float value) => Value = Math.Clamp(value, 0.0f, 1.0f);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator float(NormalizedFloat value) => value.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static explicit operator NormalizedFloat(float value) => new(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(NormalizedFloat other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    public static bool operator >(NormalizedFloat left, NormalizedFloat right) => left.Value > right.Value;

    /// <inheritdoc/>
    public static bool operator >=(NormalizedFloat left, NormalizedFloat right) => left.Value >= right.Value;

    /// <inheritdoc/>
    public static bool operator <(NormalizedFloat left, NormalizedFloat right) => left.Value < right.Value;

    /// <inheritdoc/>
    public static bool operator <=(NormalizedFloat left, NormalizedFloat right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>Nominal hue in degrees normalized to [0, 360).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 4)]
public readonly record struct HueDegrees :
    IEquatable<HueDegrees>,
    IComparable<HueDegrees>,
    IComparisonOperators<HueDegrees, HueDegrees, bool>
{
    /// <summary>Normalized degrees.</summary>
    public readonly float Value;

    /// <summary>Creates a normalized hue.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public HueDegrees(float degrees)
    {
        float mod = degrees % 360.0f;
        Value = mod < 0.0f ? mod + 360.0f : mod;
    }

    /// <summary>Converts to radians.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float ToRadians() => Value * (MathF.PI / 180.0f);

    /// <summary>Creates from radians.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static HueDegrees FromRadians(float radians) => new(radians * (180.0f / MathF.PI));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator float(HueDegrees hue) => hue.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static explicit operator HueDegrees(float degrees) => new(degrees);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(HueDegrees other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    public static bool operator >(HueDegrees left, HueDegrees right) => left.Value > right.Value;

    /// <inheritdoc/>
    public static bool operator >=(HueDegrees left, HueDegrees right) => left.Value >= right.Value;

    /// <inheritdoc/>
    public static bool operator <(HueDegrees left, HueDegrees right) => left.Value < right.Value;

    /// <inheritdoc/>
    public static bool operator <=(HueDegrees left, HueDegrees right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>Nominal unbounded perceptual chroma (C &gt;= 0).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 4)]
public readonly record struct ChromaFloat :
    IEquatable<ChromaFloat>,
    IComparable<ChromaFloat>,
    IComparisonOperators<ChromaFloat, ChromaFloat, bool>
{
    /// <summary>Clamped value.</summary>
    public readonly float Value;

    /// <summary>Creates a chroma value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ChromaFloat(float value) => Value = MathF.Max(0.0f, value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator float(ChromaFloat value) => value.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static explicit operator ChromaFloat(float value) => new(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(ChromaFloat other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    public static bool operator >(ChromaFloat left, ChromaFloat right) => left.Value > right.Value;

    /// <inheritdoc/>
    public static bool operator >=(ChromaFloat left, ChromaFloat right) => left.Value >= right.Value;

    /// <inheritdoc/>
    public static bool operator <(ChromaFloat left, ChromaFloat right) => left.Value < right.Value;

    /// <inheritdoc/>
    public static bool operator <=(ChromaFloat left, ChromaFloat right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
}

