namespace Axrone.Utility.Singleton;

/// <summary>
/// Thread-safe lazy singleton with optional disposal support.
/// </summary>
/// <typeparam name="T">The singleton value type.</typeparam>
public sealed class Singleton<T> : IDisposable where T : class
{
    private readonly Lazy<T> _lazy;
    private readonly Action<T>? _disposer;
    private int _disposed;

    public Singleton(Func<T> factory, Action<T>? disposer = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _disposer = disposer;
        _lazy = new Lazy<T>(factory, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public static Singleton<T> FromValue(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Singleton<T>(() => value);
    }

    public T Instance
    {
        get
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
            return _lazy.Value;
        }
    }

    public bool IsCreated => _lazy.IsValueCreated;
    public bool IsDisposed => Volatile.Read(ref _disposed) == 1;

    public bool TryGetInstance([NotNullWhen(true)] out T? value)
    {
        if (IsDisposed || !_lazy.IsValueCreated)
        {
            value = default;
            return false;
        }
        value = _lazy.Value;
        return true;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        if (_disposer is not null && _lazy.IsValueCreated)
            _disposer(_lazy.Value);
    }
}

/// <summary>
/// Async singleton — lazily initializes a value via an async factory.
/// </summary>
public sealed class AsyncSingleton<T> : IAsyncDisposable where T : class
{
    private readonly Func<CancellationToken, ValueTask<T>> _factory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private T? _value;
    private volatile bool _initialized;
    private volatile bool _faulted;
    private Exception? _exception;

    public AsyncSingleton(Func<CancellationToken, ValueTask<T>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    public bool IsInitialized => _initialized;
    public bool IsFaulted => _faulted;

    public async ValueTask<T> GetInstanceAsync(CancellationToken ct = default)
    {
        if (_initialized) return _value!;

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_initialized) return _value!;
            if (_faulted) throw _exception!;

            _value = await _factory(ct).ConfigureAwait(false);
            _initialized = true;
            return _value;
        }
        catch (Exception ex)
        {
            _faulted = true;
            _exception = ex;
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_value is IAsyncDisposable asyncDisp)
                await asyncDisp.DisposeAsync().ConfigureAwait(false);
            else if (_value is IDisposable disp)
                disp.Dispose();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}

/// <summary>
/// Scoped singleton registry — manages singleton instances within a named scope.
/// </summary>
public sealed class SingletonScope : IDisposable
{
    private readonly Dictionary<string, object> _instances = new();
    private readonly Dictionary<string, Func<object>> _factories = new();
    private readonly List<Action> _disposers = [];
    private readonly SingletonScope? _parent;
    private readonly object _lock = new();
    private bool _disposed;

    public string Name { get; }

    public SingletonScope(string name) => Name = name;

    public SingletonScope(string name, SingletonScope parent)
    {
        Name = name;
        _parent = parent;
    }

    public void Register<T>(string key, Func<T> factory, Action<T>? disposer = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);
        lock (_lock)
        {
            _factories[key] = () => factory();
            if (disposer is not null)
                _disposers.Add(() =>
                {
                    if (_instances.TryGetValue(key, out var val) && val is T typed)
                        disposer(typed);
                });
        }
    }

    public T Resolve<T>(string key) where T : class
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_lock)
        {
            if (_instances.TryGetValue(key, out var existing))
                return (T)existing;

            if (!_factories.TryGetValue(key, out var factory))
            {
                if (_parent is not null)
                    return _parent.Resolve<T>(key);
                throw new KeyNotFoundException($"No registration found for key '{key}' in scope '{Name}'.");
            }

            var instance = (T)factory();
            _instances[key] = instance;
            return instance;
        }
    }

    public SingletonScope CreateChild(string childName) => new(childName, this);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        for (var i = _disposers.Count - 1; i >= 0; i--)
            _disposers[i]();

        _disposers.Clear();
        _instances.Clear();
        _factories.Clear();
    }
}
