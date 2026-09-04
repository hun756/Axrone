namespace Axrone.Memory.Lifetime;

public static class Singleton<T> where T : class
{
    private static Lazy<T>? s_lazy;
    private static readonly object s_gate = new();
    private static int s_state = (int)SingletonLifecycleState.Uninitialized;

    public static bool IsInitialized => Volatile.Read(ref s_state) == (int)SingletonLifecycleState.Initialized;

    public static T Instance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var lazy = Volatile.Read(ref s_lazy);
            if (lazy is null)
            {
                throw new SingletonInitializationException(
                    $"Singleton of type '{typeof(T).FullName}' has not been configured. Call SetFactory or SetInstance prior to access.");
            }

            try
            {
                return lazy.Value;
            }
            catch (Exception ex) when (ex is not SingletonException)
            {
                throw new SingletonInitializationException(typeof(T).FullName ?? nameof(T), ex);
            }
        }
    }

    public static void SetFactory(Func<T> factory, LazyThreadSafetyMode mode = LazyThreadSafetyMode.ExecutionAndPublication)
    {
        ArgumentNullException.ThrowIfNull(factory);

        lock (s_gate)
        {
            if (s_lazy is not null && s_lazy.IsValueCreated)
            {
                throw new SingletonAlreadyInitializedException(typeof(T).FullName ?? nameof(T));
            }

            s_lazy = new Lazy<T>(() =>
            {
                Volatile.Write(ref s_state, (int)SingletonLifecycleState.Initializing);
                try
                {
                    var instance = factory();
                    if (instance is null)
                    {
                        throw new SingletonInitializationException(typeof(T).FullName ?? nameof(T));
                    }

                    Volatile.Write(ref s_state, (int)SingletonLifecycleState.Initialized);
                    return instance;
                }
                catch
                {
                    Volatile.Write(ref s_state, (int)SingletonLifecycleState.Faulted);
                    throw;
                }
            }, mode);

            Volatile.Write(ref s_state, (int)SingletonLifecycleState.Uninitialized);
        }
    }

    public static void SetInstance(T instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        lock (s_gate)
        {
            if (s_lazy is not null && s_lazy.IsValueCreated)
            {
                throw new SingletonAlreadyInitializedException(typeof(T).FullName ?? nameof(T));
            }

            s_lazy = new Lazy<T>(() => instance, LazyThreadSafetyMode.PublicationOnly);
            _ = s_lazy.Value;
            Volatile.Write(ref s_state, (int)SingletonLifecycleState.Initialized);
        }
    }

    public static void Reset()
    {
        lock (s_gate)
        {
            if (s_lazy is not null && s_lazy.IsValueCreated)
            {
                try
                {
                    var instance = s_lazy.Value;
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

            s_lazy = null;
            Volatile.Write(ref s_state, (int)SingletonLifecycleState.Uninitialized);
        }
    }
}
