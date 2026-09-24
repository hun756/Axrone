namespace Axrone.Utility.Concurrency;

using System.Threading.Tasks.Sources;

/// <summary>Pooled waiter node completing through <see cref="ManualResetValueTaskSourceCore{T}"/>.</summary>
internal sealed class AsyncWaitNode : IValueTaskSource
{
    private ManualResetValueTaskSourceCore<bool> _core;
    private CancellationTokenRegistration _registration;
    private AsyncWaitTable? _table;
    private int _bucket;

    public AsyncWaitNode? Next { get; set; }

    public AsyncWaitNode() => _core.RunContinuationsAsynchronously = true;

    public short Version => _core.Version;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset(AsyncWaitTable table, int bucket)
    {
        _registration.Dispose();
        _registration = default;
        _core.Reset();
        _table = table;
        _bucket = bucket;
        Next = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void HookCancellation(CancellationToken cancellationToken)
    {
        if (cancellationToken.CanBeCanceled)
        {
            _registration = cancellationToken.UnsafeRegister(static (state, token) =>
            {
                var self = (AsyncWaitNode)state!;
                self._table?.TryCancel(self, self._bucket, token);
            }, this);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Complete() => _core.SetResult(true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Fail(Exception exception) => _core.SetException(exception);

    public ValueTask AsTask() => new(this, _core.Version);

    public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);

    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags) =>
        _core.OnCompleted(continuation, state, token, flags);

    void IValueTaskSource.GetResult(short token)
    {
        try
        {
            _core.GetResult(token);
        }
        finally
        {
            _registration.Dispose();
            _registration = default;
            _table?.Release(this);
        }
    }
}
