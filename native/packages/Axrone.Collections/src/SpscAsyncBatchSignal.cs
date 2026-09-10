namespace Axrone.Collections;

using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

public sealed class SpscAsyncBatchSignal : IValueTaskSource<int>
{
    private ManualResetValueTaskSourceCore<int> _source;
    private int _gate;

    public SpscAsyncBatchSignal()
    {
        _source.RunContinuationsAsynchronously = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<int> WaitAsync()
    {
        if (Interlocked.CompareExchange(ref _gate, 1, 0) != 0)
        {
            ThrowHelper.ThrowConcurrentWaitNotSupported();
        }
        return new ValueTask<int>(this, _source.Version);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Signal(int itemsTransferred)
    {
        _source.SetResult(itemsTransferred);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        Volatile.Write(ref _gate, 0);
        _source.Reset();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetResult(short token) => _source.GetResult(token);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTaskSourceStatus GetStatus(short token) => _source.GetStatus(token);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _source.OnCompleted(continuation, state, token, flags);
}
