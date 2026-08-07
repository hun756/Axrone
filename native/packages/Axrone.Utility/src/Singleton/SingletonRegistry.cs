namespace Axrone.Utility.Singleton;

/// <summary>
/// Static type-keyed singleton registry with <see cref="Lazy{T}"/> backing.
/// Thread-safe, lock-free on the fast path.
/// </summary>
/// <remarks>
/// Native AOT safe — uses <c>new()</c> constraint, no reflection.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public static class SingletonRegistry
{
    private static readonly ConcurrentDictionary<Type, Lazy<object>> _instances = new();

    /// <summary>Get or create a singleton for type <typeparamref name="T"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T GetOrAdd<T>() where T : class, new() =>
        (T)_instances.GetOrAdd(typeof(T),
            static _ => new Lazy<object>(static () => new T(), LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    /// <summary>Get or create a singleton using a factory.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T GetOrAdd<T>(Func<T> factory) where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        return (T)_instances.GetOrAdd(typeof(T),
            _ => new Lazy<object>(() => factory(), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    /// <summary>Register a pre-created instance for type <typeparamref name="T"/>.</summary>
    public static void Register<T>(T instance) where T : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        _instances[typeof(T)] = new Lazy<object>(
            () => instance, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Check if a type has been registered or initialized.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool IsRegistered<T>() where T : class =>
        _instances.ContainsKey(typeof(T));

    /// <summary>Try to get an already-created instance without triggering creation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool TryGet<T>([NotNullWhen(true)] out T? instance) where T : class
    {
        if (_instances.TryGetValue(typeof(T), out var lazy) && lazy.IsValueCreated)
        {
            instance = (T)lazy.Value;
            return true;
        }

        instance = default;
        return false;
    }

    /// <summary>Remove a type from the registry.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool TryRemove<T>([NotNullWhen(true)] out T? instance) where T : class
    {
        if (_instances.TryRemove(typeof(T), out var lazy) && lazy.IsValueCreated)
        {
            instance = (T)lazy.Value;
            return true;
        }

        instance = default;
        return false;
    }

    /// <summary>Clear all registrations. Does not dispose instances.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Clear() => _instances.Clear();
}
