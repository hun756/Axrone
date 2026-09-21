namespace Axrone.Utility.Backoff.SpinPolicies;

public readonly struct YieldingBackoff : ISpinBackoff
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
