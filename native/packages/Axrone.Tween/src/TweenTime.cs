namespace Axrone.Tween;

/// <summary>Monotonic nanosecond timestamp from the engine clock.</summary>
public readonly record struct TimestampNs(long Value) : IComparable<TimestampNs>
{
    /// <summary>Creates a timestamp; rejects NaN and negatives.</summary>
    public static TimestampNs FromSeconds(double seconds)
    {
        if (double.IsNaN(seconds) || seconds < 0.0)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(seconds));
        }

        return new TimestampNs((long)(seconds * 1_000_000_000.0));
    }

    /// <summary>Creates a timestamp; rejects NaN and negatives.</summary>
    public static TimestampNs FromMilliseconds(double milliseconds)
    {
        if (double.IsNaN(milliseconds) || milliseconds < 0.0)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(milliseconds));
        }

        return new TimestampNs((long)(milliseconds * 1_000_000.0));
    }

    /// <summary>Converts to seconds.</summary>
    public double ToSeconds() => Value * 1e-9;

    /// <inheritdoc/>
    public int CompareTo(TimestampNs other) => Value.CompareTo(other.Value);

    /// <summary>Adds a duration.</summary>
    public TimestampNs Add(DurationNs duration) => this + duration;

    /// <summary>Subtracts a duration.</summary>
    public TimestampNs Subtract(DurationNs duration) => this - duration;

    /// <summary>Elapsed time between two timestamps.</summary>
    public DurationNs Subtract(TimestampNs other) => this - other;

    public static TimestampNs operator +(TimestampNs left, DurationNs right) =>
        new(unchecked(left.Value + right.Value));

    public static TimestampNs operator -(TimestampNs left, DurationNs right) =>
        new(unchecked(left.Value - right.Value));

    public static DurationNs operator -(TimestampNs left, TimestampNs right) =>
        new(unchecked(left.Value - right.Value));

    public static bool operator <(TimestampNs left, TimestampNs right) => left.Value < right.Value;

    public static bool operator <=(TimestampNs left, TimestampNs right) => left.Value <= right.Value;

    public static bool operator >(TimestampNs left, TimestampNs right) => left.Value > right.Value;

    public static bool operator >=(TimestampNs left, TimestampNs right) => left.Value >= right.Value;
}

/// <summary>Nanosecond duration for delays, durations, and steps.</summary>
public readonly record struct DurationNs(long Value) : IComparable<DurationNs>
{
    /// <summary>Zero duration.</summary>
    public static DurationNs Zero => new(0);

    /// <summary>Creates a duration; rejects NaN and negatives.</summary>
    public static DurationNs FromSeconds(float seconds)
    {
        if (float.IsNaN(seconds) || seconds < 0.0f)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(seconds));
        }

        return new DurationNs((long)(seconds * 1_000_000_000.0f));
    }

    /// <summary>Creates a duration; rejects NaN and negatives.</summary>
    public static DurationNs FromMilliseconds(float milliseconds)
    {
        if (float.IsNaN(milliseconds) || milliseconds < 0.0f)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(milliseconds));
        }

        return new DurationNs((long)(milliseconds * 1_000_000.0f));
    }

    /// <summary>Converts to seconds.</summary>
    public float ToSeconds() => (float)(Value * 1e-9);

    /// <inheritdoc/>
    public int CompareTo(DurationNs other) => Value.CompareTo(other.Value);

    /// <summary>Adds a duration.</summary>
    public DurationNs Add(DurationNs other) => this + other;

    /// <summary>Subtracts a duration.</summary>
    public DurationNs Subtract(DurationNs other) => this - other;

    public static DurationNs operator +(DurationNs left, DurationNs right) =>
        new(unchecked(left.Value + right.Value));

    public static DurationNs operator -(DurationNs left, DurationNs right) =>
        new(unchecked(left.Value - right.Value));

    public static bool operator <(DurationNs left, DurationNs right) => left.Value < right.Value;

    public static bool operator <=(DurationNs left, DurationNs right) => left.Value <= right.Value;

    public static bool operator >(DurationNs left, DurationNs right) => left.Value > right.Value;

    public static bool operator >=(DurationNs left, DurationNs right) => left.Value >= right.Value;
}
