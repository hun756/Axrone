using System.Threading.Tasks.Sources;

namespace Axrone.Utility.Backoff;

internal sealed class HybridAsyncDelayNode : IValueTaskSource<BackoffDuration>, IDisposable
{
    private ManualResetValueTaskSourceCore<BackoffDuration> _core;
    private readonly Timer _timer;
    private CancellationTokenRegistration _registration;
    private BackoffDuration _capturedDuration;
    private int _concurrencyGate;
    private static readonly TimerCallback S_OnTimeoutCallback = OnTimeoutFired;
    private static readonly Action<object?> S_OnCancelledCallback = OnCancelledTriggered;

    private const long SpinWaitThresholdNs = 5_000_000L;

    public HybridAsyncDelayNode()
    {
        _core = new ManualResetValueTaskSourceCore<BackoffDuration>
        {
            RunContinuationsAsynchronously = true
        };
        _timer = new Timer(S_OnTimeoutCallback, this, Timeout.Infinite, Timeout.Infinite);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<BackoffDuration> StartDelay(BackoffDuration duration, CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _concurrencyGate, 1, 0) != 0)
        {
            ThrowHelper.ThrowInvalidOperationException("Hybrid async delay node was re-entered concurrently.");
        }

        _core.Reset();
        _capturedDuration = duration;

        if (cancellationToken.IsCancellationRequested)
        {
            Volatile.Write(ref _concurrencyGate, 0);
            return ValueTask.FromCanceled<BackoffDuration>(cancellationToken);
        }

        if (cancellationToken.CanBeCanceled)
        {
            _registration = cancellationToken.UnsafeRegister(S_OnCancelledCallback, this);
        }

        long ns = duration.Nanoseconds;

        if (ns <= SpinWaitThresholdNs)
        {
            SpinWaitAndComplete(ns);
        }
        else
        {
            long ms = ns / BackoffDuration.NanosecondsPerMillisecond;
            if (ms == 0L) ms = 1L;
            _timer.Change(ms, Timeout.Infinite);
        }

        return new ValueTask<BackoffDuration>(this, _core.Version);
    }

    private void SpinWaitAndComplete(long targetNs)
    {
        ThreadPool.UnsafeQueueUserWorkItem(static state =>
        {
            var self = Unsafe.As<HybridAsyncDelayNode>(state!);
            long targetTicks = (long)(self._capturedDuration.Nanoseconds * (Stopwatch.Frequency / 1_000_000_000.0));
            if (targetTicks < 1) targetTicks = 1;

            long startTicks = Stopwatch.GetTimestamp();
            long elapsed = 0;

            while (elapsed < targetTicks)
            {
                if (Volatile.Read(ref self._concurrencyGate) != 1)
                {
                    return;
                }

                long remaining = targetTicks - elapsed;
                if (remaining > Stopwatch.Frequency / 1000)
                {
                    Thread.Yield();
                }
                else
                {
                    MicroPause.Execute(64);
                }

                elapsed = Stopwatch.GetTimestamp() - startTicks;
            }

            if (Interlocked.CompareExchange(ref self._concurrencyGate, 2, 1) == 1)
            {
                self._registration.Dispose();
                self._registration = default;
                self._core.SetResult(self._capturedDuration);
            }
        }, this);
    }

    private static void OnTimeoutFired(object? state)
    {
        var self = Unsafe.As<HybridAsyncDelayNode>(state!);
        self._registration.Dispose();
        self._registration = default;

        if (Interlocked.CompareExchange(ref self._concurrencyGate, 2, 1) == 1)
        {
            self._core.SetResult(self._capturedDuration);
        }
    }

    private static void OnCancelledTriggered(object? state)
    {
        var self = Unsafe.As<HybridAsyncDelayNode>(state!);
        self._timer.Change(Timeout.Infinite, Timeout.Infinite);
        self._registration.Dispose();
        self._registration = default;

        if (Interlocked.CompareExchange(ref self._concurrencyGate, 2, 1) == 1)
        {
            self._core.SetException(new OperationCanceledException());
        }
    }

    public BackoffDuration GetResult(short token)
    {
        try
        {
            return _core.GetResult(token);
        }
        finally
        {
            Volatile.Write(ref _concurrencyGate, 0);
        }
    }

    public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);

    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags) =>
        _core.OnCompleted(continuation, state, token, flags);

    public void Dispose()
    {
        _timer.Dispose();
        _registration.Dispose();
    }
}
