namespace Axrone.GameLoop;

using System.Globalization;

/// <summary>
/// Monotonic frame counter.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct FrameIndex(ulong Value) :
    IComparable<FrameIndex>,
    IComparisonOperators<FrameIndex, FrameIndex, bool>,
    IAdditionOperators<FrameIndex, ulong, FrameIndex>
{
    /// <summary>First frame.</summary>
    public static FrameIndex Zero => new(0UL);

    /// <summary>Next frame.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FrameIndex Next() => new(Value + 1UL);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(FrameIndex other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left index is smaller.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(FrameIndex left, FrameIndex right) => left.Value < right.Value;

    /// <summary>Determines whether the left index is smaller or equal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(FrameIndex left, FrameIndex right) => left.Value <= right.Value;

    /// <summary>Determines whether the left index is larger.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(FrameIndex left, FrameIndex right) => left.Value > right.Value;

    /// <summary>Determines whether the left index is larger or equal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(FrameIndex left, FrameIndex right) => left.Value >= right.Value;

    /// <summary>Advances by steps.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FrameIndex operator +(FrameIndex left, ulong right) => new(left.Value + right);

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Game time in milliseconds with domain arithmetic.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct GameTime(double TotalMilliseconds) :
    IComparable<GameTime>,
    IComparisonOperators<GameTime, GameTime, bool>,
    IAdditionOperators<GameTime, GameTime, GameTime>,
    ISubtractionOperators<GameTime, GameTime, GameTime>,
    IMultiplyOperators<GameTime, double, GameTime>,
    IDivisionOperators<GameTime, GameTime, double>
{
    /// <summary>Zero time.</summary>
    public static GameTime Zero => new(0.0);

    /// <summary>Creates from milliseconds.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameTime FromMilliseconds(double ms) => new(ms);

    /// <summary>Creates from seconds.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameTime FromSeconds(double seconds) => new(seconds * 1000.0);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(GameTime other) => TotalMilliseconds.CompareTo(other.TotalMilliseconds);

    /// <summary>Determines whether the left time is smaller.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(GameTime left, GameTime right) => left.TotalMilliseconds < right.TotalMilliseconds;

    /// <summary>Determines whether the left time is smaller or equal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(GameTime left, GameTime right) => left.TotalMilliseconds <= right.TotalMilliseconds;

    /// <summary>Determines whether the left time is larger.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(GameTime left, GameTime right) => left.TotalMilliseconds > right.TotalMilliseconds;

    /// <summary>Determines whether the left time is larger or equal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(GameTime left, GameTime right) => left.TotalMilliseconds >= right.TotalMilliseconds;

    /// <summary>Adds times.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameTime operator +(GameTime left, GameTime right) => new(left.TotalMilliseconds + right.TotalMilliseconds);

    /// <summary>Subtracts times.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameTime operator -(GameTime left, GameTime right) => new(left.TotalMilliseconds - right.TotalMilliseconds);

    /// <summary>Scales time.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameTime operator *(GameTime left, double right) => new(left.TotalMilliseconds * right);

    /// <summary>Divides times.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double operator /(GameTime left, GameTime right) => left.TotalMilliseconds / right.TotalMilliseconds;

    /// <inheritdoc/>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{TotalMilliseconds:F3} ms");
}

/// <summary>
/// Fixed simulation step; must be finite and positive.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct FixedStep :
    IComparable<FixedStep>,
    IComparisonOperators<FixedStep, FixedStep, bool>
{
    /// <summary>Step duration in milliseconds.</summary>
    public double TotalMilliseconds { get; }

    /// <summary>60 Hz step.</summary>
    public static FixedStep SixtyFps => new(1000.0 / 60.0);

    /// <summary>120 Hz step.</summary>
    public static FixedStep OneHundredTwentyFps => new(1000.0 / 120.0);

    /// <summary>Creates a step; validates the range.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedStep(double totalMilliseconds)
    {
        if (totalMilliseconds <= 0.0 || double.IsNaN(totalMilliseconds) || double.IsInfinity(totalMilliseconds))
        {
            GameLoopThrowHelper.ThrowInvalidFixedStep(totalMilliseconds);
        }

        TotalMilliseconds = totalMilliseconds;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(FixedStep other) => TotalMilliseconds.CompareTo(other.TotalMilliseconds);

    /// <summary>Determines whether the left step is smaller.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(FixedStep left, FixedStep right) => left.TotalMilliseconds < right.TotalMilliseconds;

    /// <summary>Determines whether the left step is smaller or equal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(FixedStep left, FixedStep right) => left.TotalMilliseconds <= right.TotalMilliseconds;

    /// <summary>Determines whether the left step is larger.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(FixedStep left, FixedStep right) => left.TotalMilliseconds > right.TotalMilliseconds;

    /// <summary>Determines whether the left step is larger or equal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(FixedStep left, FixedStep right) => left.TotalMilliseconds >= right.TotalMilliseconds;
}

/// <summary>
/// Time multiplier; non-negative and finite.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct TimeScale
{
    /// <summary>Multiplier value.</summary>
    public double Value { get; }

    /// <summary>Real time.</summary>
    public static TimeScale Identity => new(1.0);

    /// <summary>Frozen time.</summary>
    public static TimeScale Paused => new(0.0);

    /// <summary>Creates a scale; validates the range.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TimeScale(double value)
    {
        if (value < 0.0 || double.IsNaN(value) || double.IsInfinity(value))
        {
            GameLoopThrowHelper.ThrowInvalidTimeScale(value);
        }

        Value = value;
    }

    /// <summary>Unwraps the multiplier.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator double(TimeScale scale) => scale.Value;
}

/// <summary>
/// Render interpolation factor in [0, 1].
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct InterpolationAlpha
{
    /// <summary>Factor in [0, 1].</summary>
    public double Value { get; }

    /// <summary>No interpolation.</summary>
    public static InterpolationAlpha Zero => new(0.0);

    /// <summary>Full interpolation.</summary>
    public static InterpolationAlpha One => new(1.0);

    /// <summary>Creates a factor; clamps into range.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public InterpolationAlpha(double value)
    {
        Value = Math.Clamp(value, 0.0, 1.0);
    }

    /// <summary>Unwraps the factor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator double(InterpolationAlpha alpha) => alpha.Value;
}

/// <summary>
/// System identity: FNV-1a hash of the system name.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SystemId(uint Value) :
    IComparable<SystemId>,
    IComparisonOperators<SystemId, SystemId, bool>
{
    /// <summary>Hashes a name.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SystemId FromSpan(ReadOnlySpan<char> name)
    {
        uint hash = 2166136261U;
        for (int i = 0; i < name.Length; i++)
        {
            hash = (hash ^ name[i]) * 16777619U;
        }

        return new SystemId(hash);
    }

    /// <summary>Hashes a name.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SystemId FromString(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return FromSpan(name.AsSpan());
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(SystemId other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left identity sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(SystemId left, SystemId right) => left.Value > right.Value;

    /// <summary>Determines whether the left identity sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(SystemId left, SystemId right) => left.Value >= right.Value;

    /// <summary>Determines whether the left identity sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(SystemId left, SystemId right) => left.Value < right.Value;

    /// <summary>Determines whether the left identity sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(SystemId left, SystemId right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"0x{Value:X8}");
}

/// <summary>
/// System priority: higher runs first.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SystemPriority(int Value) :
    IComparable<SystemPriority>,
    IComparisonOperators<SystemPriority, SystemPriority, bool>
{
    /// <summary>Critical systems.</summary>
    public static SystemPriority Critical => new(1000);

    /// <summary>High priority.</summary>
    public static SystemPriority High => new(100);

    /// <summary>Default priority.</summary>
    public static SystemPriority Default => new(0);

    /// <summary>Low priority.</summary>
    public static SystemPriority Low => new(-100);

    /// <summary>Compares descending: higher priority sorts first.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(SystemPriority other) => other.Value.CompareTo(Value);

    /// <summary>Determines whether the left priority sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(SystemPriority left, SystemPriority right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether the left priority sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(SystemPriority left, SystemPriority right) => left.CompareTo(right) >= 0;

    /// <summary>Determines whether the left priority sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(SystemPriority left, SystemPriority right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether the left priority sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(SystemPriority left, SystemPriority right) => left.CompareTo(right) <= 0;
}

/// <summary>
/// Snapshot version.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SnapshotVersion(uint Value) :
    IComparable<SnapshotVersion>,
    IComparisonOperators<SnapshotVersion, SnapshotVersion, bool>
{
    /// <summary>First version.</summary>
    public static SnapshotVersion Initial => new(1U);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(SnapshotVersion other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left version is larger.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(SnapshotVersion left, SnapshotVersion right) => left.Value > right.Value;

    /// <summary>Determines whether the left version is larger or equal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(SnapshotVersion left, SnapshotVersion right) => left.Value >= right.Value;

    /// <summary>Determines whether the left version is smaller.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(SnapshotVersion left, SnapshotVersion right) => left.Value < right.Value;

    /// <summary>Determines whether the left version is smaller or equal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(SnapshotVersion left, SnapshotVersion right) => left.Value <= right.Value;

    /// <summary>Next version.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SnapshotVersion Next() => new(Value + 1U);
}
