namespace Axrone.Utility.Backoff.SpinPolicies;

public readonly struct AggressiveSpinBackoff : ISpinBackoff
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Initialize(out int state) => state = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Advance(ref int state)
    {
        MicroPause.Execute(4);
        state++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Reset(ref int state) => state = 0;
}
