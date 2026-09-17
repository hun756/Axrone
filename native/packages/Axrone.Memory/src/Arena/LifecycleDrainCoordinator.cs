namespace Axrone.Memory.Arena;

public sealed class LifecycleDrainCoordinator
{
    private enum DrainState : int
    {
        Active = 0,
        Completing = 1,
        Faulted = 2,
        Drained = 3,
        Disposed = 4,
    }

    private int _state;
    private readonly TaskCompletionSource _drainCompleted;

    public LifecycleDrainCoordinator()
    {
        _state = (int)DrainState.Active;
        _drainCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public bool IsActive => Volatile.Read(ref _state) == (int)DrainState.Active;

    public bool IsCompleting => Volatile.Read(ref _state) == (int)DrainState.Completing;

    public bool IsDrained => Volatile.Read(ref _state) == (int)DrainState.Drained;

    public bool IsDisposed => Volatile.Read(ref _state) == (int)DrainState.Disposed;

    public Task DrainCompleted => _drainCompleted.Task;

    public bool RequestCompletion()
    {
        int current = Volatile.Read(ref _state);
        if (current != (int)DrainState.Active) return false;
        return Interlocked.CompareExchange(ref _state, (int)DrainState.Completing, (int)DrainState.Active) == (int)DrainState.Active;
    }

    public void MarkFaulted()
    {
        int prior = Interlocked.Exchange(ref _state, (int)DrainState.Faulted);
        if (prior != (int)DrainState.Completing && prior != (int)DrainState.Active)
        {
            Interlocked.Exchange(ref _state, prior);
        }
    }

    public bool MarkDrained()
    {
        int current = Volatile.Read(ref _state);
        if (current != (int)DrainState.Completing && current != (int)DrainState.Faulted) return false;

        if (Interlocked.CompareExchange(ref _state, (int)DrainState.Drained, current) == current)
        {
            _drainCompleted.TrySetResult();
            return true;
        }

        return false;
    }

    public bool MarkDisposed()
    {
        int prior = Interlocked.Exchange(ref _state, (int)DrainState.Disposed);
        _drainCompleted.TrySetResult();
        return prior != (int)DrainState.Disposed;
    }
}
