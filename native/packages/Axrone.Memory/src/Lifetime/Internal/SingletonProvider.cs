namespace Axrone.Memory.Lifetime.Internal;

/// <summary>
/// Lock-free singleton storage with thread-safety strategy dispatch.
/// Routes to CAS, ThreadStatic, or Weak storage based on <see cref="SingletonAttributeCache{T}"/>.
/// Fast path: single volatile read for the initialized case.
/// Slow path: compare-exchange for first-time creation, <see cref="SpinWait"/> for contention.
/// </summary>
[SkipLocalsInit]
internal static class SingletonProvider<T> where T : class, new()
{
    // ── Standard (CAS) storage ───────────────────────────────────────
    private static T? _instance;
    private static int _initializationState;

    private const int NotInitialized = 0;
    private const int Initializing = 1;
    private const int Initialized = 2;

    // ── Pre-registered instance (from Singleton.Register<T>) ─────────
    private static T? _registered;

    // ── ThreadStatic storage ─────────────────────────────────────────
    [ThreadStatic]
    private static T? _tsInstance;

    // ── Weak storage ─────────────────────────────────────────────────
    private static WeakReference<T>? _weakRef;
    private static readonly object _weakLock = new();

    /// <summary>Get the singleton instance, dispatching by thread-safety strategy.</summary>
    public static T Instance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => SingletonAttributeCache<T>.ThreadSafety switch
        {
            SingletonThreadSafety.ThreadStatic => GetThreadStaticInstance(),
            SingletonThreadSafety.Weak => GetWeakInstance(),
            _ => GetStandardInstance(),
        };
    }

    /// <summary>Whether the standard singleton has been initialized.</summary>
    public static bool IsInitialized
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _initializationState) == Initialized;
    }

    /// <summary>Force initialization. Safe to call multiple times.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void WarmUp() => _ = Instance;

    /// <summary>
    /// Pre-register an instance. Only succeeds if the provider has not yet created one.
    /// Returns <see langword="true"/> if the instance was accepted.
    /// </summary>
    public static bool TryRegister(T instance)
    {
        if (Volatile.Read(ref _initializationState) != NotInitialized) return false;

        Volatile.Write(ref _registered, instance);
        if (Interlocked.CompareExchange(ref _initializationState, Initializing, NotInitialized) == NotInitialized)
        {
            Volatile.Write(ref _instance, instance);
            SingletonRegistryInternal.Register(instance);
            SingletonShutdownRegistry.Register(instance);
            Volatile.Write(ref _initializationState, Initialized);
            return true;
        }

        Volatile.Write(ref _registered, null);
        return false;
    }

    /// <summary>Try to get an already-created instance without triggering creation.</summary>
    public static bool TryGet([NotNullWhen(true)] out T? instance)
    {
        if (Volatile.Read(ref _initializationState) == Initialized)
        {
            instance = Volatile.Read(ref _instance)!;
            return true;
        }

        instance = default;
        return false;
    }

    // ── Standard (CAS) path ──────────────────────────────────────────

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T GetStandardInstance()
    {
        var registered = Volatile.Read(ref _registered);
        if (registered is not null) return registered;

        var instance = Volatile.Read(ref _instance);
        if (instance is not null)
        {
            SingletonDiagnostics.RecordAccess<T>();
            return instance;
        }

        if (Volatile.Read(ref _initializationState) != Initialized &&
            SingletonRegistryInternal.TryGet(out T? preRegistered))
        {
            Volatile.Write(ref _instance, preRegistered);
            Interlocked.Exchange(ref _initializationState, Initialized);
            SingletonDiagnostics.RecordAccess<T>();
            return preRegistered;
        }

        return CreateStandardSlow();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T CreateStandardSlow()
    {
        while (true)
        {
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
            while (Volatile.Read(ref _initializationState) == Initializing)
                spinner.SpinOnce();

            if (Volatile.Read(ref _initializationState) == Initialized)
            {
                SingletonDiagnostics.RecordAccess<T>();
                return Volatile.Read(ref _instance)!;
            }
        }
    }

    // ── ThreadStatic path ────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T GetThreadStaticInstance()
    {
        var instance = _tsInstance;
        if (instance is not null)
        {
            SingletonDiagnostics.RecordAccess<T>();
            return instance;
        }

        return CreateThreadStaticInstance();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T CreateThreadStaticInstance()
    {
        T newInstance;
        try
        {
            SingletonDiagnostics.RecordCreationStart<T>();
            using (NestingContext.EnterScope())
                newInstance = new T();
            PerformInitialization(newInstance);
            SingletonDiagnostics.RecordCreationEnd<T>();
        }
        catch (Exception ex) when (ex is not SingletonCreationException)
        {
            throw new SingletonCreationException(
                $"Failed to create thread-static instance of type {typeof(T).FullName}", typeof(T), ex);
        }

        _tsInstance = newInstance;
        SingletonShutdownRegistry.Register(newInstance);
        return newInstance;
    }

    // ── Weak path ────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T GetWeakInstance()
    {
        var weak = Volatile.Read(ref _weakRef);
        if (weak is not null && weak.TryGetTarget(out var existing))
        {
            SingletonDiagnostics.RecordAccess<T>();
            return existing;
        }

        return CreateWeakInstance();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T CreateWeakInstance()
    {
        lock (_weakLock)
        {
            if (_weakRef is not null && _weakRef.TryGetTarget(out var existing))
                return existing;

            T newInstance;
            try
            {
                SingletonDiagnostics.RecordCreationStart<T>();
                using (NestingContext.EnterScope())
                    newInstance = new T();
                PerformInitialization(newInstance);
                SingletonDiagnostics.RecordCreationEnd<T>();
            }
            catch (Exception ex) when (ex is not SingletonCreationException)
            {
                throw new SingletonCreationException(
                    $"Failed to create weak-referenced instance of type {typeof(T).FullName}", typeof(T), ex);
            }

            Volatile.Write(ref _weakRef, new WeakReference<T>(newInstance));
            SingletonShutdownRegistry.Register(newInstance);
            return newInstance;
        }
    }

    // ── Shared helpers ───────────────────────────────────────────────

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
    /// </summary>
    internal static void EnsurePreInitialized()
    {
        if (SingletonAttributeCache<T>.IsEager)
            _ = Instance;
    }
}
