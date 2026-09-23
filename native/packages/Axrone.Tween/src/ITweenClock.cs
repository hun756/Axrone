namespace Axrone.Tween;

/// <summary>Time source for the engine pump; manual clocks make ticks deterministic.</summary>
public interface ITweenClock
{
    /// <summary>Current time.</summary>
    TimestampNs Now();

    /// <summary>Elapsed time since the previous tick.</summary>
    DurationNs Tick();
}
