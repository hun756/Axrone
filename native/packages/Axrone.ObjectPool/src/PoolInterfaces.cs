namespace Axrone.ObjectPool;

public interface IClearable
{
    void Clear();
}

public interface IPoolable
{
    void Reset();
    ValueTask ResetAsync();
}

public interface IPoolValidator<T> where T : class
{
    bool Validate(T item);
    ValueTask<bool> ValidateAsync(T item, CancellationToken cancellationToken);
}

public interface IPoolableFactory<T, TResult> where T : class
{
    TResult Create(T item);
}

public interface IObjectPoolFactory<T> where T : class
{
    IObjectPool<T> CreatePool(PoolConfiguration configuration);
    IObjectPool<T> CreatePool(Func<T> factory, PoolConfiguration configuration);
    IObjectPool<T> CreatePool(Func<T> factory, Action<T>? reset = null, int initialCapacity = 0, int maxCapacity = 0);
}

public interface IObjectPool<T> : IDisposable, IAsyncDisposable where T : class
{
    T Rent();
    ValueTask<T> RentAsync(CancellationToken cancellationToken = default);
    void Return(T item);
    ValueTask ReturnAsync(T item, CancellationToken cancellationToken = default);
    void ReturnPoolable(int poolableId, T item);
    ValueTask ReturnPoolableAsync(int poolableId, T item);
    bool TryRent([NotNullWhen(true)] out T? item);
    ValueTask<(bool Success, T? Item)> TryRentAsync(CancellationToken cancellationToken = default);
    IPoolable<T> GetPoolable();
    ValueTask<IPoolable<T>> GetPoolableAsync(CancellationToken cancellationToken = default);
    void Clear();
    ValueTask ClearAsync(CancellationToken cancellationToken = default);
    bool Contains(T item);
    int Count { get; }
    int Capacity { get; }
    int Available { get; }
    PoolMetrics GetMetrics();
    PoolDiagnostics GetDiagnostics();
    void Reconfigure(PoolConfiguration configuration);
    void Trim(int targetCapacity = -1);
    ValueTask TrimAsync(int targetCapacity = -1, CancellationToken cancellationToken = default);
    void ResetMetrics();
    IPoolableFactory<T, TResult> CreateFactory<TResult>();
}

public interface IPoolable<T> : IDisposable, IAsyncDisposable where T : class
{
    T Instance { get; }
    DateTime RentTime { get; }
    bool IsDisposed { get; }
    TimeSpan Age { get; }
    void Detach();
}
