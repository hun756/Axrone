namespace Axrone.Memory.Arena;

public readonly struct ProgressiveBackoff : IBackoffPolicy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Step(ref int spinCount)
    {
        if (spinCount < 10)
        {
            Thread.SpinWait(1 << spinCount);
        }
        else if (spinCount < 20)
        {
            Thread.Yield();
        }
        else
        {
            Thread.Sleep(0);
        }
        spinCount++;
    }
}
