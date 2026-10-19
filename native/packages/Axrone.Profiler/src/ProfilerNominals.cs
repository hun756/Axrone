namespace Axrone.Profiler;

/// <summary>Nanosecond timestamp or duration.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct Nanoseconds(long Value) :
    IComparable<Nanoseconds>,
    IComparisonOperators<Nanoseconds, Nanoseconds, bool>,
    IAdditionOperators<Nanoseconds, Nanoseconds, Nanoseconds>,
    ISubtractionOperators<Nanoseconds, Nanoseconds, Nanoseconds>
{
    /// <summary>Zero duration.</summary>
    public static Nanoseconds Zero => new(0L);

    /// <summary>Converts stopwatch ticks using the given frequency.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Nanoseconds FromStopwatchTicks(long ticks, long frequency)
    {
        return new(ticks * 1_000_000_000L / frequency);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(Nanoseconds other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left timestamp sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Nanoseconds left, Nanoseconds right) => left.Value < right.Value;

    /// <summary>Determines whether the left timestamp sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Nanoseconds left, Nanoseconds right) => left.Value <= right.Value;

    /// <summary>Determines whether the left timestamp sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Nanoseconds left, Nanoseconds right) => left.Value > right.Value;

    /// <summary>Determines whether the left timestamp sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Nanoseconds left, Nanoseconds right) => left.Value >= right.Value;

    /// <summary>Adds durations.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Nanoseconds operator +(Nanoseconds left, Nanoseconds right) => new(left.Value + right.Value);

    /// <summary>Subtracts durations.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Nanoseconds operator -(Nanoseconds left, Nanoseconds right) => new(left.Value - right.Value);

    /// <inheritdoc/>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Value} ns");
}

/// <summary>Microsecond timestamp or duration.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct Microseconds(long Value) :
    IComparable<Microseconds>,
    IComparisonOperators<Microseconds, Microseconds, bool>
{
    /// <summary>Converts from nanoseconds, truncating toward zero.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Microseconds FromNanoseconds(Nanoseconds value) => new(value.Value / 1_000L);

    /// <summary>Converts to nanoseconds.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Nanoseconds ToNanoseconds() => new(Value * 1_000L);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(Microseconds other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left value sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Microseconds left, Microseconds right) => left.Value < right.Value;

    /// <summary>Determines whether the left value sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Microseconds left, Microseconds right) => left.Value <= right.Value;

    /// <summary>Determines whether the left value sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Microseconds left, Microseconds right) => left.Value > right.Value;

    /// <summary>Determines whether the left value sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Microseconds left, Microseconds right) => left.Value >= right.Value;

    /// <inheritdoc/>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Value} us");
}

/// <summary>Millisecond timestamp or duration.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct Milliseconds(double Value) :
    IComparable<Milliseconds>,
    IComparisonOperators<Milliseconds, Milliseconds, bool>
{
    /// <summary>Converts from nanoseconds.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Milliseconds FromNanoseconds(Nanoseconds value) => new(value.Value / 1_000_000.0);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(Milliseconds other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left value sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Milliseconds left, Milliseconds right) => left.Value < right.Value;

    /// <summary>Determines whether the left value sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Milliseconds left, Milliseconds right) => left.Value <= right.Value;

    /// <summary>Determines whether the left value sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Milliseconds left, Milliseconds right) => left.Value > right.Value;

    /// <summary>Determines whether the left value sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Milliseconds left, Milliseconds right) => left.Value >= right.Value;

    /// <inheritdoc/>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Value:F3} ms");
}

/// <summary>Second timestamp or duration.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct Seconds(double Value) :
    IComparable<Seconds>,
    IComparisonOperators<Seconds, Seconds, bool>
{
    /// <summary>Converts from nanoseconds.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Seconds FromNanoseconds(Nanoseconds value) => new(value.Value / 1_000_000_000.0);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(Seconds other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left value sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Seconds left, Seconds right) => left.Value < right.Value;

    /// <summary>Determines whether the left value sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Seconds left, Seconds right) => left.Value <= right.Value;

    /// <summary>Determines whether the left value sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Seconds left, Seconds right) => left.Value > right.Value;

    /// <summary>Determines whether the left value sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Seconds left, Seconds right) => left.Value >= right.Value;

    /// <inheritdoc/>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Value:F6} s");
}

/// <summary>A measured span with unit projections.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct Duration(Nanoseconds TotalNanoseconds)
{
    /// <summary>Zero span.</summary>
    public static Duration Zero => new(Nanoseconds.Zero);

    /// <summary>Whole microseconds, truncating toward zero.</summary>
    public long Microseconds => TotalNanoseconds.Value / 1_000L;

    /// <summary>Fractional milliseconds.</summary>
    public double Milliseconds => TotalNanoseconds.Value / 1_000_000.0;

    /// <summary>Fractional seconds.</summary>
    public double Seconds => TotalNanoseconds.Value / 1_000_000_000.0;
}

/// <summary>Nanoseconds per time unit.</summary>
public static class TimeConversion
{
    /// <summary>Nanoseconds per nanosecond.</summary>
    public const long Nanoseconds = 1L;

    /// <summary>Nanoseconds per microsecond.</summary>
    public const long Microseconds = 1_000L;

    /// <summary>Nanoseconds per millisecond.</summary>
    public const long Milliseconds = 1_000_000L;

    /// <summary>Nanoseconds per second.</summary>
    public const long Seconds = 1_000_000_000L;
}
