namespace Axrone.Memory.Lifetime;

public sealed class AsyncSingleton<T> : IAsyncSingleton<T>, IDisposable, IAsyncDisposable where T : class
{
    private readonly Lazy<Task<T>> _lazy;
    private readonly object _disposeGate = new();
    private int _state = (int)SingletonLifecycleState.Uninitialized;

    public AsyncSingleton(Func<CancellationToken, ValueTask<T>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _lazy = new Lazy<Task<T>>(async () =>
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
                var val = await factory(CancellationToken.None).ConfigureAwait(false);
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
                    if (created is IAsyncDisposable ad)
                    {
                        ad.DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                    else if (created is IDisposable d)
                    {
                        d.Dispose();
                    }

                    throw new SingletonDisposedException(typeof(T).FullName ?? nameof(T));
                }

                Volatile.Write(ref _state, (int)SingletonLifecycleState.Initialized);
                return created;
            }
        }, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public AsyncSingleton(Func<Task<T>> factory)
        : this(factory is not null ? _ => new ValueTask<T>(factory()) : throw new ArgumentNullException(nameof(factory)))
    {
    }

    public AsyncSingleton(IAsyncSingletonFactory<T> factory)
        : this(factory is not null ? ct => factory.CreateAsync(ct) : throw new ArgumentNullException(nameof(factory)))
    {
    }

    public bool IsValueCreated => _lazy.IsValueCreated && _lazy.Value.IsCompletedSuccessfully;

    public SingletonLifecycleState State => (SingletonLifecycleState)Volatile.Read(ref _state);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<T> GetValueAsync(CancellationToken cancellationToken = default)
    {
        var currentState = (SingletonLifecycleState)Volatile.Read(ref _state);
        if (currentState is SingletonLifecycleState.Disposing or SingletonLifecycleState.Disposed)
        {
            return ValueTask.FromException<T>(new SingletonDisposedException(typeof(T).FullName ?? nameof(T)));
        }

        var task = _lazy.Value;
        if (task.IsCompletedSuccessfully)
        {
            return new ValueTask<T>(task.Result);
        }

        return AwaitSlowAsync(task, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async ValueTask<T> AwaitSlowAsync(Task<T> task, CancellationToken cancellationToken)
    {
        try
        {
            return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not SingletonException and not OperationCanceledException)
        {
            throw new SingletonInitializationException(typeof(T).FullName ?? nameof(T), ex);
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

            if (_lazy.IsValueCreated && _lazy.Value.IsCompletedSuccessfully)
            {
                try
                {
                    var instance = _lazy.Value.Result;
                    if (instance is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    else if (instance is IAsyncDisposable asyncDisposable)
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

            if (_lazy.IsValueCreated && _lazy.Value.IsCompletedSuccessfully)
            {
                try
                {
                    targetToDispose = _lazy.Value.Result;
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
