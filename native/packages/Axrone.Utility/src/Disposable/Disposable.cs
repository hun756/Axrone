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
/// </summary>
public sealed class CompositeDisposable : IDisposable
{
    private readonly List<IDisposable> _disposables = [];
    private bool _disposed;

    public int Count => _disposables.Count;
    public bool IsDisposed => _disposed;

    public void Add(IDisposable disposable)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(disposable);
        _disposables.Add(disposable);
    }

    public void AddRange(IEnumerable<IDisposable> disposables)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(disposables);
        foreach (var d in disposables)
            _disposables.Add(d);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        for (var i = _disposables.Count - 1; i >= 0; i--)
            _disposables[i].Dispose();

        _disposables.Clear();
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
