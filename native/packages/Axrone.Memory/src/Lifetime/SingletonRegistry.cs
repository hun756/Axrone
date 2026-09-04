using System.Diagnostics.CodeAnalysis;

namespace Enterprise.Patterns.Singleton;

public sealed class SingletonRegistry : ISingletonRegistry
{
    private readonly ConcurrentDictionary<Type, IRegistryEntry> _entries = new();
    private readonly ConcurrentStack<object> _disposables = new();
    private int _isDisposed;

    private interface IRegistryEntry
    {
        object Resolve();
        bool HasInstance { get; }
    }

    private sealed class LazyRegistryEntry<T> : IRegistryEntry where T : class
    {
        private readonly Lazy<T> _lazy;

        public LazyRegistryEntry(Func<T> factory, Action<object> onInstanceCreated, LazyThreadSafetyMode mode)
        {
            _lazy = new Lazy<T>(() =>
            {
                var instance = factory();
                if (instance is null)
                {
                    throw new SingletonInitializationException(typeof(T).FullName ?? nameof(T));
                }

                onInstanceCreated(instance);
                return instance;
            }, mode);
        }

        public bool HasInstance => _lazy.IsValueCreated;

        public object Resolve()
        {
            try
            {
                return _lazy.Value;
            }
            catch (Exception ex) when (ex is not SingletonException)
            {
                throw new SingletonInitializationException(typeof(T).FullName ?? nameof(T), ex);
            }
        }
    }

    private sealed class SingletonWrapperRegistryEntry<T> : IRegistryEntry where T : class
    {
        private readonly ISingleton<T> _singleton;
        private readonly Action<object> _onInstanceCreated;
        private int _tracked;

        public SingletonWrapperRegistryEntry(ISingleton<T> singleton, Action<object> onInstanceCreated)
        {
            _singleton = singleton;
            _onInstanceCreated = onInstanceCreated;
        }

        public bool HasInstance => _singleton.IsValueCreated;

        public object Resolve()
        {
            var val = _singleton.Value;
            if (Interlocked.Exchange(ref _tracked, 1) == 0)
            {
                _onInstanceCreated(val);
            }

            return val;
        }
    }

    private sealed class DirectInstanceRegistryEntry<T> : IRegistryEntry where T : class
    {
        private readonly T _instance;

        public DirectInstanceRegistryEntry(T instance)
        {
            _instance = instance;
        }

        public bool HasInstance => true;

        public object Resolve() => _instance;
    }

    public void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>() where T : class, new()
    {
        ThrowIfDisposed();
        Register(static () => new T());
    }

    public void Register<T>(Func<T> factory, LazyThreadSafetyMode mode = LazyThreadSafetyMode.ExecutionAndPublication) where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        ThrowIfDisposed();

        var entry = new LazyRegistryEntry<T>(factory, TrackDisposable, mode);
        if (!_entries.TryAdd(typeof(T), entry))
        {
            throw new SingletonAlreadyInitializedException(typeof(T).FullName ?? nameof(T));
        }
    }

    public void Register<T>(ISingleton<T> singleton) where T : class
    {
        ArgumentNullException.ThrowIfNull(singleton);
        ThrowIfDisposed();

        var entry = new SingletonWrapperRegistryEntry<T>(singleton, TrackDisposable);
        if (!_entries.TryAdd(typeof(T), entry))
        {
            throw new SingletonAlreadyInitializedException(typeof(T).FullName ?? nameof(T));
        }
    }

    public void RegisterInstance<T>(T instance) where T : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        ThrowIfDisposed();

        TrackDisposable(instance);
        var entry = new DirectInstanceRegistryEntry<T>(instance);
        if (!_entries.TryAdd(typeof(T), entry))
        {
            throw new SingletonAlreadyInitializedException(typeof(T).FullName ?? nameof(T));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Get<T>() where T : class
    {
        ThrowIfDisposed();

        if (_entries.TryGetValue(typeof(T), out var entry))
        {
            return (T)entry.Resolve();
        }

        throw new KeyNotFoundException(typeof(T).FullName ?? nameof(T));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet<T>([NotNullWhen(true)] out T? instance) where T : class
    {
        if (Volatile.Read(ref _isDisposed) == 0 && _entries.TryGetValue(typeof(T), out var entry))
        {
            instance = (T)entry.Resolve();
            return true;
        }

        instance = null;
        return false;
    }

    public bool Contains<T>() where T : class
    {
        ThrowIfDisposed();
        return _entries.ContainsKey(typeof(T));
    }

    private void TrackDisposable(object instance)
    {
        if (instance is IDisposable or IAsyncDisposable)
        {
            _disposables.Push(instance);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _isDisposed) != 0)
        {
            throw new SingletonDisposedException(nameof(SingletonRegistry));
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        _entries.Clear();

        while (_disposables.TryPop(out var item))
        {
            if (item is IDisposable disposable)
            {
                disposable.Dispose();
            }
            else if (item is IAsyncDisposable asyncDisposable)
            {
                asyncDisposable.DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        _entries.Clear();

        while (_disposables.TryPop(out var item))
        {
            if (item is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else if (item is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
