namespace Axrone.Utility.Singleton.Internal;

/// <summary>
/// Lock-free singleton storage using <see cref="Interlocked"/> + <see cref="Volatile"/>.
/// Fast path: single volatile read for the initialized case.
/// Slow path: compare-exchange for first-time creation, <see cref="SpinWait"/> for contention.
/// Dispatches <see cref="ISingletonLifecycle"/> hooks and registers with the shutdown registry.
/// </summary>
[SkipLocalsInit]
internal static class SingletonProvider<T> where T : class, new()
{
    private static T? _instance;
    private static int _initializationState;

    private const int NotInitialized = 0;
    private const int Initializing = 1;
    private const int Initialized = 2;

    public static T Instance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            var instance = Volatile.Read(ref _instance);
            if (instance is not null)
            {
                SingletonDiagnostics.RecordAccess<T>();
                return instance;
            }

            if (Interlocked.CompareExchange(ref _initializationState, Initializing, NotInitialized) == NotInitialized)
            {
                T newInstance;
                try
                {
                    SingletonDiagnostics.RecordCreationStart<T>();
                    newInstance = CreateInstanceInternal();
                    PerformInitialization(newInstance);
                    SingletonDiagnostics.RecordCreationEnd<T>();
                }
                catch (Exception ex) when (ex is not SingletonCreationException)
                {
                    Interlocked.Exchange(ref _initializationState, NotInitialized);
                    throw new SingletonCreationException(
                        $"Failed to create singleton instance of type {typeof(T).FullName}", typeof(T), ex);
                }

                Volatile.Write(ref _instance, newInstance);
                SingletonRegistryInternal.Register(newInstance);
                SingletonShutdownRegistry.Register(newInstance);
                Interlocked.Exchange(ref _initializationState, Initialized);
                return newInstance;
            }

            SpinWait spinner = default;
            while (Volatile.Read(ref _initializationState) != Initialized)
                spinner.SpinOnce();

            var result = Volatile.Read(ref _instance)!;
            SingletonDiagnostics.RecordAccess<T>();
            return result;
        }
    }

    /// <summary>Whether the singleton has been initialized.</summary>
    public static bool IsInitialized
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _initializationState) == Initialized;
    }

    /// <summary>Force initialization. Safe to call multiple times.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void WarmUp() => _ = Instance;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T CreateInstanceInternal()
    {
        try
        {
            using (NestingContext.EnterScope())
                return new T();
        }
        catch (Exception ex)
        {
            throw new SingletonCreationException(
                $"Failed to create singleton instance of type {typeof(T).FullName}", typeof(T), ex);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PerformInitialization(T instance)
    {
        if (instance is ISingletonLifecycle lifecycle)
        {
            lifecycle.Initialize();
            var asyncTask = lifecycle.InitializeAsync();
            if (!asyncTask.IsCompletedSuccessfully)
                asyncTask.AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Eager initialization hook. Checks <see cref="SingletonAttributeCache{T}"/> for the eager flag.
    /// Call from a non-generic <c>[ModuleInitializer]</c> or from <c>Singleton.WarmUp</c>.
    /// </summary>
    internal static void EnsurePreInitialized()
    {
        if (SingletonAttributeCache<T>.IsEager)
            _ = Instance;
    }
}
