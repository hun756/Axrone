namespace Axrone.ObjectPool;

[StructLayout(LayoutKind.Auto)]
public struct Poolable<T> : IPoolable<T> where T : class
{
    private static int s_nextPoolableId;

    private readonly T _instance;
    private readonly IObjectPool<T>? _pool;
    private readonly DateTime _rentTime;
    private readonly bool _trackRentTime;
    private readonly Action<T, TimeSpan>? _onDispose;
    private readonly int _poolableId;
    private bool _isDisposed;
    private bool _isDetached;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Poolable(
        T instance,
        IObjectPool<T>? pool,
        bool trackRentTime = false,
        Action<T, TimeSpan>? onDispose = null)
    {
        _instance = instance;
        _pool = pool;
        _rentTime = DateTime.UtcNow;
        _trackRentTime = trackRentTime;
        _onDispose = onDispose;
        _poolableId = Interlocked.Increment(ref s_nextPoolableId);
        _isDisposed = false;
        _isDetached = false;
    }

    internal int PoolableId
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _poolableId;
    }

    public T Instance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ThrowIfDisposed();
            return _instance;
        }
    }

    public DateTime RentTime => _rentTime;

    public bool IsDisposed => _isDisposed;

    public TimeSpan Age => DateTime.UtcNow - _rentTime;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Detach()
    {
        _isDetached = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_isDisposed || _isDetached)
            return;

        _isDisposed = true;

        if (_pool == null)
            return;

        if (_trackRentTime && _onDispose != null)
        {
            TimeSpan rentDuration = DateTime.UtcNow - _rentTime;
            _onDispose(_instance, rentDuration);
        }

        _pool.ReturnPoolable(_poolableId, _instance);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask DisposeAsync()
    {
        if (_isDisposed || _isDetached)
            return ValueTask.CompletedTask;

        _isDisposed = true;

        if (_pool == null)
            return ValueTask.CompletedTask;

        if (_trackRentTime && _onDispose != null)
        {
            TimeSpan rentDuration = DateTime.UtcNow - _rentTime;
            _onDispose(_instance, rentDuration);
        }

        return _pool.ReturnPoolableAsync(_poolableId, _instance);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            ThrowHelper.ThrowObjectDisposedException(nameof(Poolable<T>));
        }
    }
}
