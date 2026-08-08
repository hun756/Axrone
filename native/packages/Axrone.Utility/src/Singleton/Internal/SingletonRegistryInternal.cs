namespace Axrone.Utility.Singleton.Internal;

/// <summary>
/// Non-generic instance registry for type-keyed lookups without triggering creation.
/// Populated by <see cref="SingletonProvider{T}"/> and <see cref="FactoryProvider{T}"/> after creation.
/// </summary>
[SkipLocalsInit]
internal static class SingletonRegistryInternal
{
    private static readonly ConcurrentDictionary<Type, object> _instances = new();

    /// <summary>Register a created instance. Idempotent — overwrites any previous entry for the type.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Register<T>(T instance) where T : class =>
        _instances[typeof(T)] = instance;

    /// <summary>Try to get an already-created instance without triggering creation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGet<T>([NotNullWhen(true)] out T? instance) where T : class
    {
        if (_instances.TryGetValue(typeof(T), out var obj))
        {
            instance = (T)obj;
            return true;
        }

        instance = default;
        return false;
    }

    /// <summary>Number of registered instances.</summary>
    public static int Count => _instances.Count;
}
