namespace Axrone.Utility.Backoff.SpinPolicies;

public readonly struct ProgressiveSpinBackoff : ISpinBackoff
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Initialize(out int state) => state = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Advance(ref int state)
    {
        if ((uint)state < 10)
        {
            Thread.SpinWait(1 << state);
        }
        else if ((uint)state < 20)
        {
            Thread.Yield();
        }
        else
        {
            Thread.Sleep(0);
        }
        state++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Reset(ref int state) => state = 0;
}
