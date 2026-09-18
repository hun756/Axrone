using System.Threading.Tasks.Sources;

namespace Axrone.Memory.Arena;

internal sealed class AsyncAutoResetSignal : IValueTaskSource<bool>
{
    private ManualResetValueTaskSourceCore<bool> _core;
    private readonly Lock _gate = new();
    private bool _signaled;

    public AsyncAutoResetSignal()
    {
        _core.RunContinuationsAsynchronously = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask<bool> WaitAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenRegistration registration = default;

        ValueTask<bool> task;
        using (_gate.EnterScope())
        {
            if (_signaled)
            {
                _signaled = false;
                return true;
            }

            _core.Reset();

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.ThrowIfCancellationRequested();
                registration = cancellationToken.UnsafeRegister(static (state, ct) =>
                {
                    var self = (AsyncAutoResetSignal)state!;
                    self.SignalFault(new OperationCanceledException(ct));
                }, this);
            }

            task = new ValueTask<bool>(this, _core.Version);
        }

        try
        {
            return await task.ConfigureAwait(false);
        }
        finally
        {
            await registration.DisposeAsync().ConfigureAwait(false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Signal()
    {
        using (_gate.EnterScope())
        {
            if (_core.GetStatus(_core.Version) == ValueTaskSourceStatus.Pending)
            {
                _core.SetResult(true);
            }
            else
            {
                _signaled = true;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SignalFault(Exception exception)
    {
        using (_gate.EnterScope())
        {
            if (_core.GetStatus(_core.Version) == ValueTaskSourceStatus.Pending)
            {
                _core.SetException(exception);
            }
        }
    }

    public bool GetResult(short token)
    {
        using (_gate.EnterScope())
        {
            return _core.GetResult(token);
        }
    }

    public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);

    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
    {
        _core.OnCompleted(continuation, state, token, flags);
    }
}
