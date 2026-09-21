using System.Threading.Tasks.Sources;

namespace Axrone.Utility.Backoff;

internal sealed class ResettableAsyncDelayNode : IValueTaskSource<BackoffDuration>, IDisposable
{
    private ManualResetValueTaskSourceCore<BackoffDuration> _core;
    private readonly Timer _timer;
    private CancellationTokenRegistration _registration;
    private BackoffDuration _capturedDuration;
    private int _concurrencyGate;
    private static readonly TimerCallback S_OnTimeoutCallback = OnTimeoutFired;
    private static readonly Action<object?> S_OnCancelledCallback = OnCancelledTriggered;

    public ResettableAsyncDelayNode()
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
            ThrowHelper.ThrowInvalidOperationException("Async delay instance was re-entered concurrently.");
        }

        _core.Reset();
        _capturedDuration = duration;

        if (cancellationToken.IsCancellationRequested)
        {
            Volatile.Write(ref _concurrencyGate, 0);
            return ValueTask.FromCanceled<BackoffDuration>(cancellationToken);
        }

        long ms = duration.Nanoseconds / BackoffDuration.NanosecondsPerMillisecond;
        if (ms == 0L && duration.Nanoseconds > 0L)
        {
            ms = 1L;
        }

        if (cancellationToken.CanBeCanceled)
        {
            _registration = cancellationToken.UnsafeRegister(S_OnCancelledCallback, this);
        }

        _timer.Change(ms, Timeout.Infinite);
        return new ValueTask<BackoffDuration>(this, _core.Version);
    }

    private static void OnTimeoutFired(object? state)
    {
        var self = Unsafe.As<ResettableAsyncDelayNode>(state!);
        self._registration.Dispose();
        self._registration = default;

        if (Interlocked.CompareExchange(ref self._concurrencyGate, 2, 1) == 1)
        {
            self._core.SetResult(self._capturedDuration);
        }
    }

    private static void OnCancelledTriggered(object? state)
    {
        var self = Unsafe.As<ResettableAsyncDelayNode>(state!);
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
