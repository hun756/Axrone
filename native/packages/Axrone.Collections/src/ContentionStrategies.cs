namespace Axrone.Collections;

using System.Runtime.CompilerServices;

public interface IContentionStrategy
{
    static abstract void Backoff(uint iteration);
}

public readonly struct AdaptiveBackoff : IContentionStrategy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Backoff(uint iteration)
    {
        if (iteration < 4)
        {
            Thread.SpinWait(1 << (int)iteration);
        }
        else if (iteration < 16)
        {
            Thread.SpinWait(1 << 5);
        }
        else if (iteration < 32)
        {
            Thread.Yield();
        }
        else
        {
            Thread.Sleep(0);
        }
    }
}

public readonly struct PinnedCoreSpinBackoff : IContentionStrategy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Backoff(uint iteration)
    {
        Thread.SpinWait(Math.Min((int)iteration + 1, 32));
    }
}
