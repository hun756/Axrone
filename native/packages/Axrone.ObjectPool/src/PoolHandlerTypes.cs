namespace Axrone.ObjectPool;

public readonly struct PoolObjectInfo<T> where T : class
{
    public T Instance { get; init; }
    public DateTime CreationTime { get; init; }
    public int Generation { get; init; }
    public long RentCount { get; init; }
    public TimeSpan TotalRentTime { get; init; }
    public bool IsPoolable { get; init; }
    public bool IsActive { get; init; }
    public Guid Id { get; init; }
}

[StructLayout(LayoutKind.Auto)]
public readonly struct PoolableHandler<T> where T : class
{
    public readonly Func<T> Factory { get; init; }
    public readonly Action<T>? Reset { get; init; }
    public readonly Func<T, ValueTask>? ResetAsync { get; init; }
    public readonly Action<T>? OnRent { get; init; }
    public readonly Action<T>? OnReturn { get; init; }
    public readonly Action<T>? OnDispose { get; init; }
    public readonly Func<T, ValueTask>? OnDisposeAsync { get; init; }
    public readonly Func<T, bool>? Validator { get; init; }
    public readonly Func<T, ValueTask<bool>>? ValidatorAsync { get; init; }
    public readonly Action<PoolObjectInfo<T>>? OnCreate { get; init; }
    public readonly Action<PoolObjectInfo<T>>? OnDestroy { get; init; }
}
