namespace Axrone.Memory.Pools;

/// <summary>
/// Fixed-capacity object pool with factory, initializer, and reset callbacks.
/// Thread-safe via double-checked locking.
/// </summary>
/// <typeparam name="T">Pooled object type.</typeparam>
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public sealed class ObjectPool<T> where T : class
{
    private readonly T?[] _items;
    private readonly Func<T> _factory;
    private readonly Action<T>? _initializer;
    private readonly Action<T>? _reset;
    private readonly object _syncRoot = new();
    private int _count;

    public ObjectPool(
        Func<T> factory,
        Action<T>? initializer = null,
        Action<T>? reset = null,
        int size = 32)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        _initializer = initializer;
        _reset = reset;
        _items = new T?[size];
    }

    /// <summary>Get an object from the pool, or create a new one if empty.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public T Get()
    {
        T? item = default;

        if (Volatile.Read(ref _count) > 0)
        {
            lock (_syncRoot)
            {
                if (_count > 0)
                {
                    item = _items[--_count];
                    _items[_count] = null;
                }
            }
        }

        item ??= _factory();
        _initializer?.Invoke(item);
        return item;
    }

    /// <summary>Return an object to the pool. Resets it before storage.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Return(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _reset?.Invoke(item);

        if (Volatile.Read(ref _count) < _items.Length)
        {
            lock (_syncRoot)
            {
                if (_count < _items.Length)
                {
                    _items[_count++] = item;
                }
            }
        }
    }
}
