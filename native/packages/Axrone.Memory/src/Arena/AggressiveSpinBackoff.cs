namespace Axrone.Memory.Arena;

public readonly struct AggressiveSpinBackoff : IBackoffPolicy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Step(ref int spinCount)
    {
        Thread.SpinWait(4);
        spinCount++;
    }
}
