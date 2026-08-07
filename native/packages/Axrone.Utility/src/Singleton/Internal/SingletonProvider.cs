namespace Axrone.Utility.Singleton.Internal;

/// <summary>
/// Lock-free singleton storage using <see cref="Interlocked"/> + <see cref="Volatile"/>.
/// Fast path: single volatile read for the initialized case.
/// Slow path: compare-exchange for first-time creation, <see cref="SpinWait"/> for contention.
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
            if (instance is not null) return instance;

            if (Interlocked.CompareExchange(ref _initializationState, Initializing, NotInitialized) == NotInitialized)
            {
                T newInstance;
                try
                {
                    newInstance = CreateInstanceInternal();
                    PerformInitialization(newInstance);
                }
                catch (Exception ex) when (ex is not SingletonCreationException)
                {
                    Interlocked.Exchange(ref _initializationState, NotInitialized);
                    throw new SingletonCreationException(
                        $"Failed to create singleton instance of type {typeof(T).FullName}", ex);
                }

                Volatile.Write(ref _instance, newInstance);
                Interlocked.Exchange(ref _initializationState, Initialized);
                return newInstance;
            }

            SpinWait spinner = default;
            while (Volatile.Read(ref _initializationState) != Initialized)
                spinner.SpinOnce();

            return Volatile.Read(ref _instance)!;
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
                $"Failed to create singleton instance of type {typeof(T).FullName}", ex);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PerformInitialization(T instance)
    {
        if (instance is IInitializable initializable)
            initializable.Initialize();

        if (instance is IAsyncInitializable asyncInitializable)
            asyncInitializable.InitializeAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Eager initialization hook. Call from a non-generic <c>[ModuleInitializer]</c> or from <c>Singleton.WarmUp</c>.
    /// </summary>
    internal static void EnsurePreInitialized()
    {
        if (typeof(IEagerSingleton).IsAssignableFrom(typeof(T)))
            _ = Instance;
    }
}
