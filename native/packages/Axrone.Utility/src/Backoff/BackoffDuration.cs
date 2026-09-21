namespace Axrone.Utility.Backoff;

[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct BackoffDuration : IComparable<BackoffDuration>, IEquatable<BackoffDuration>
{
    public const long NanosecondsPerTick = 100L;
    public const long NanosecondsPerMillisecond = 1_000_000L;
    public const long NanosecondsPerSecond = 1_000_000_000L;

    public readonly long Nanoseconds;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BackoffDuration(long nanoseconds)
    {
        if (nanoseconds < 0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(nanoseconds), "Nanoseconds cannot be negative.");
        }
        Nanoseconds = nanoseconds;
    }

    public static BackoffDuration Zero => new(0L);
    public static BackoffDuration MaxValue => new(long.MaxValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BackoffDuration FromNanoseconds(long nanoseconds) => new(nanoseconds);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BackoffDuration FromMicroseconds(long microseconds)
    {
        if (microseconds < 0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(microseconds), "Microseconds cannot be negative.");
        }
        if (microseconds > (long.MaxValue / 1_000L))
        {
            return MaxValue;
        }
        return new BackoffDuration(microseconds * 1_000L);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BackoffDuration FromMilliseconds(double milliseconds)
    {
        if (milliseconds < 0.0 || double.IsNaN(milliseconds))
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(milliseconds), "Invalid millisecond duration value.");
        }
        double ns = milliseconds * NanosecondsPerMillisecond;
        if (ns >= (double)long.MaxValue)
        {
            return MaxValue;
        }
        return new BackoffDuration((long)ns);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BackoffDuration FromSeconds(double seconds)
    {
        if (seconds < 0.0 || double.IsNaN(seconds))
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(seconds), "Invalid seconds value.");
        }
        double ns = seconds * NanosecondsPerSecond;
        if (ns >= (double)long.MaxValue)
        {
            return MaxValue;
        }
        return new BackoffDuration((long)ns);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BackoffDuration FromTimeSpan(TimeSpan timeSpan)
    {
        long ticks = timeSpan.Ticks;
        if (ticks < 0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(timeSpan), "TimeSpan cannot be negative.");
        }
        if (ticks > (long.MaxValue / NanosecondsPerTick))
        {
            return MaxValue;
        }
        return new BackoffDuration(ticks * NanosecondsPerTick);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TimeSpan ToTimeSpan() => TimeSpan.FromTicks(Nanoseconds / NanosecondsPerTick);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(BackoffDuration other) => Nanoseconds.CompareTo(other.Nanoseconds);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(BackoffDuration left, BackoffDuration right) => left.Nanoseconds < right.Nanoseconds;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(BackoffDuration left, BackoffDuration right) => left.Nanoseconds <= right.Nanoseconds;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(BackoffDuration left, BackoffDuration right) => left.Nanoseconds > right.Nanoseconds;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(BackoffDuration left, BackoffDuration right) => left.Nanoseconds >= right.Nanoseconds;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BackoffDuration operator +(BackoffDuration left, BackoffDuration right)
    {
        long sum = left.Nanoseconds + right.Nanoseconds;
        return new BackoffDuration(sum < left.Nanoseconds ? long.MaxValue : sum);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BackoffDuration operator -(BackoffDuration left, BackoffDuration right)
    {
        long diff = left.Nanoseconds - right.Nanoseconds;
        return new BackoffDuration(diff < 0L ? 0L : diff);
    }
}
