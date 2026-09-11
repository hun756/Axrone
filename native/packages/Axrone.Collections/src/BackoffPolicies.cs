namespace Axrone.Collections;

using System.Runtime.CompilerServices;

/// <summary>
/// Adaptive spin-wait backoff optimized for multicore pipeline topologies.
/// Progresses through SpinWait → long spin → yield phases.
/// </summary>
public readonly struct AdaptiveSpinBackoff : IBackoffPolicy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Initialize(out int state) => state = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Advance(ref int state)
    {
        if ((uint)state < 10)
        {
            Thread.SpinWait(1 << state);
        }
        else if ((uint)state < 20)
        {
            Thread.SpinWait(1024);
        }
        else
        {
            Thread.Yield();
        }
        state++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Reset(ref int state) => state = 0;
}

/// <summary>
/// Aggressive spin-wait strategy for dedicated-core, ultra-low-latency execution.
/// Minimal backoff — prioritizes lowest latency over CPU utilization.
/// </summary>
public readonly struct AggressiveSpinBackoff : IBackoffPolicy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Initialize(out int state) => state = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Advance(ref int state) => Thread.SpinWait(4);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Reset(ref int state) => state = 0;
}

/// <summary>
/// Yielding backoff strategy designed for oversubscribed environments.
/// Immediately yields the time-slice on every contention step.
/// </summary>
public readonly struct YieldingBackoff : IBackoffPolicy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Initialize(out int state) => state = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Advance(ref int state)
    {
        Thread.Yield();
        state++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Reset(ref int state) => state = 0;
}
