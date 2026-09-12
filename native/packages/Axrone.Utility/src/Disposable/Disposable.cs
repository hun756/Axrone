namespace Axrone.Utility.Disposable;

/// <summary>
/// A composable disposable that invokes a delegate on disposal.
/// Thread-safety: <see cref="Dispose"/> is idempotent.
/// </summary>
public sealed class ActionDisposable : IDisposable
{
    private Action? _onDispose;

    public ActionDisposable(Action onDispose)
    {
        ArgumentNullException.ThrowIfNull(onDispose);
        _onDispose = onDispose;
    }

    public bool IsDisposed => Volatile.Read(ref _onDispose) is null;

    public void Dispose()
    {
        var action = Interlocked.Exchange(ref _onDispose, null);
        action?.Invoke();
    }
}

/// <summary>
/// Accumulates multiple <see cref="IDisposable"/> instances and disposes them in reverse order.
/// Thread-safe for concurrent Add and Dispose calls.
/// </summary>
public sealed class CompositeDisposable : IDisposable
{
    private readonly List<IDisposable> _disposables = [];
    private readonly object _gate = new();
    private int _disposed;

    public int Count
    {
        get { lock (_gate) { return _disposables.Count; } }
    }

    public bool IsDisposed => Volatile.Read(ref _disposed) == 1;

    public void Add(IDisposable disposable)
    {
        ArgumentNullException.ThrowIfNull(disposable);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed == 1, this);
            _disposables.Add(disposable);
        }
    }

    public void AddRange(IEnumerable<IDisposable> disposables)
    {
        ArgumentNullException.ThrowIfNull(disposables);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed == 1, this);
            foreach (var d in disposables)
                _disposables.Add(d);
        }
    }

    public void Dispose()
    {
        IDisposable[] snapshot;
        lock (_gate)
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
            snapshot = [.. _disposables];
            _disposables.Clear();
        }

        for (var i = snapshot.Length - 1; i >= 0; i--)
            snapshot[i].Dispose();
    }
}

/// <summary>
/// Tracks disposal state for types that implement <see cref="IDisposable"/>.
/// </summary>
public struct DisposalTracker
{
    private int _state;

    public bool IsDisposed => Volatile.Read(ref _state) == 1;

    public bool TryDispose() => Interlocked.Exchange(ref _state, 1) == 0;

    public void ThrowIfDisposed(string? objectName = null)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(objectName);
    }
}

/// <summary>
/// Thread-safe composite disposable using <see cref="ConcurrentStack{T}"/>.
/// Disposes in reverse registration order. Safe for concurrent Add/Dispose from multiple threads.
/// </summary>
public sealed class ConcurrentCompositeDisposable : IDisposable, IAsyncDisposable
{
    private readonly ConcurrentStack<IDisposable> _disposables = new();
    private int _disposed;

    public int Count => _disposables.Count;
    public bool IsDisposed => Volatile.Read(ref _disposed) == 1;

    public void Add(IDisposable disposable)
    {
        ArgumentNullException.ThrowIfNull(disposable);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        _disposables.Push(disposable);
        if (Volatile.Read(ref _disposed) != 0)
        {
            IDisposable? orphaned = null;
            try
            {
                if (_disposables.TryPop(out orphaned))
                    orphaned?.Dispose();
            }
            finally
            {
                orphaned = null;
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        while (_disposables.TryPop(out var disposable))
        {
            try { disposable.Dispose(); }
            catch (Exception) { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        while (_disposables.TryPop(out var disposable))
        {
            try
            {
                if (disposable is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                else
                    disposable.Dispose();
            }
            catch (Exception) { }
        }
    }
}
