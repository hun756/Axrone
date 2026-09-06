namespace Axrone.Collections;

public interface IWaitStrategy
{
    void Reset();
    void Wait();
}

public sealed class BusySpinWaitStrategy : IWaitStrategy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Wait() => Thread.SpinWait(1);
}

public sealed class YieldWaitStrategy : IWaitStrategy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Wait() => Thread.Yield();
}

public sealed class SpinWaitStrategy : IWaitStrategy
{
    private SpinWait _spinner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() => _spinner.Reset();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Wait() => _spinner.SpinOnce();
}

public sealed class AdaptiveWaitStrategy : IWaitStrategy
{
    private int _step;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() => _step = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Wait()
    {
        int current = _step++;
        if (current < 16)
        {
            Thread.SpinWait(1 << current);
        }
        else if (current < 32)
        {
            Thread.Yield();
        }
        else if (current < 48)
        {
            Thread.Sleep(0);
        }
        else
        {
            Thread.Sleep(1);
        }
    }
}
