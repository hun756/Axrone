namespace Axrone.Utility.Backoff;

public ref struct ProgressiveValueSpinner
{
    private uint _iteration;
    private readonly uint _spinThreshold;
    private readonly uint _yieldThreshold;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ProgressiveValueSpinner(uint spinThreshold, uint yieldThreshold)
    {
        _iteration = 0U;
        _spinThreshold = spinThreshold;
        _yieldThreshold = Math.Max(yieldThreshold, spinThreshold);
    }

    public readonly uint Iteration
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _iteration;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void SpinOnce()
    {
        if (_iteration < _spinThreshold)
        {
            uint pauseCount = 1U << (int)Math.Min(_iteration, 10U);
            MicroPause.Execute(pauseCount);
        }
        else if (_iteration < _yieldThreshold)
        {
            Thread.Yield();
        }
        else
        {
            Thread.Sleep(0);
        }

        _iteration = _iteration < uint.MaxValue ? _iteration + 1U : _yieldThreshold;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TrySpinOnce(long deadlineTimestamp)
    {
        if (Stopwatch.GetTimestamp() >= deadlineTimestamp)
        {
            return false;
        }

        SpinOnce();
        return true;
    }

    public bool SpinUntil(Func<bool> predicate, TimeSpan timeout)
    {
        long frequency = Stopwatch.Frequency;
        long startTimestamp = Stopwatch.GetTimestamp();
        long deadlineTimestamp = startTimestamp + (long)(timeout.TotalSeconds * frequency);

        while (!predicate())
        {
            if (Stopwatch.GetTimestamp() >= deadlineTimestamp)
            {
                return false;
            }

            SpinOnce();
        }

        return true;
    }

    public bool SpinUntil(Func<bool> predicate, TimeSpan timeout, CancellationToken cancellationToken)
    {
        long frequency = Stopwatch.Frequency;
        long startTimestamp = Stopwatch.GetTimestamp();
        long deadlineTimestamp = startTimestamp + (long)(timeout.TotalSeconds * frequency);

        while (!predicate())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            if (Stopwatch.GetTimestamp() >= deadlineTimestamp)
            {
                return false;
            }

            SpinOnce();
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() => _iteration = 0U;
}
