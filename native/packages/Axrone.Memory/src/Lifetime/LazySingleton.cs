namespace Axrone.Memory.Lifetime;

public sealed class LazySingleton<T> : ISingleton<T>, IDisposable, IAsyncDisposable where T : class
{
    private readonly Lazy<T> _lazy;
    private readonly object _disposeGate = new();
    private int _state = (int)SingletonLifecycleState.Uninitialized;

    public LazySingleton(Func<T> factory, LazyThreadSafetyMode mode = LazyThreadSafetyMode.ExecutionAndPublication)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _lazy = new Lazy<T>(() =>
        {
            lock (_disposeGate)
            {
                if (Volatile.Read(ref _state) is (int)SingletonLifecycleState.Disposing or (int)SingletonLifecycleState.Disposed)
                {
                    throw new SingletonDisposedException(typeof(T).FullName ?? nameof(T));
                }

                Volatile.Write(ref _state, (int)SingletonLifecycleState.Initializing);
            }

            T created;
            try
            {
                var val = factory();
                if (val is null)
                {
                    throw new SingletonInitializationException(typeof(T).FullName ?? nameof(T));
                }

                created = val;
            }
            catch
            {
                lock (_disposeGate)
                {
                    Volatile.Write(ref _state, (int)SingletonLifecycleState.Faulted);
                }
                throw;
            }

            lock (_disposeGate)
            {
                if (Volatile.Read(ref _state) is (int)SingletonLifecycleState.Disposing or (int)SingletonLifecycleState.Disposed)
                {
                    if (created is IDisposable d)
                    {
                        d.Dispose();
                    }
                    else if (created is IAsyncDisposable ad)
                    {
                        ad.DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
                    }

                    throw new SingletonDisposedException(typeof(T).FullName ?? nameof(T));
                }

                Volatile.Write(ref _state, (int)SingletonLifecycleState.Initialized);
                return created;
            }
        }, mode);
    }

    public LazySingleton(ISingletonFactory<T> factory, LazyThreadSafetyMode mode = LazyThreadSafetyMode.ExecutionAndPublication)
        : this(factory is not null ? factory.Create : throw new ArgumentNullException(nameof(factory)), mode)
    {
    }

    public bool IsValueCreated => _lazy.IsValueCreated;

    public SingletonLifecycleState State => (SingletonLifecycleState)Volatile.Read(ref _state);

    public T Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var currentState = (SingletonLifecycleState)Volatile.Read(ref _state);
            if (currentState is SingletonLifecycleState.Disposing or SingletonLifecycleState.Disposed)
            {
                throw new SingletonDisposedException(typeof(T).FullName ?? nameof(T));
            }

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

    public void Dispose()
    {
        lock (_disposeGate)
        {
            var current = (SingletonLifecycleState)Volatile.Read(ref _state);
            if (current is SingletonLifecycleState.Disposing or SingletonLifecycleState.Disposed)
            {
                return;
            }

            Volatile.Write(ref _state, (int)SingletonLifecycleState.Disposing);

            if (_lazy.IsValueCreated)
            {
                try
                {
                    var val = _lazy.Value;
                    if (val is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    else if (val is IAsyncDisposable asyncDisposable)
                    {
                        asyncDisposable.DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                }
                catch
                {
                }
            }

            Volatile.Write(ref _state, (int)SingletonLifecycleState.Disposed);
        }
    }

    public async ValueTask DisposeAsync()
    {
        T? targetToDispose = null;

        lock (_disposeGate)
        {
            var current = (SingletonLifecycleState)Volatile.Read(ref _state);
            if (current is SingletonLifecycleState.Disposing or SingletonLifecycleState.Disposed)
            {
                return;
            }

            Volatile.Write(ref _state, (int)SingletonLifecycleState.Disposing);

            if (_lazy.IsValueCreated)
            {
                try
                {
                    targetToDispose = _lazy.Value;
                }
                catch
                {
                }
            }
        }

        if (targetToDispose is not null)
        {
            if (targetToDispose is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else if (targetToDispose is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        lock (_disposeGate)
        {
            Volatile.Write(ref _state, (int)SingletonLifecycleState.Disposed);
        }
    }
}
