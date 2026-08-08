namespace Axrone.Utility.Singleton.Internal;

/// <summary>
/// Non-generic instance registry for type-keyed lookups without triggering creation.
/// Uses a copy-on-write immutable array for lock-free reads on the hot path.
/// Populated by <see cref="SingletonProvider{T}"/> and <see cref="FactoryProvider{T}"/> after creation.
/// </summary>
[SkipLocalsInit]
internal static class SingletonRegistryInternal
{
    private static KeyValuePair<Type, object>[] _snapshot = [];
    private static readonly object _writeLock = new();

    /// <summary>Register a created instance. Idempotent — overwrites any previous entry for the type.</summary>
    public static void Register<T>(T instance) where T : class
    {
        lock (_writeLock)
        {
            var old = _snapshot;
            var type = typeof(T);

            for (var i = 0; i < old.Length; i++)
            {
                if (old[i].Key == type)
                {
                    var updated = (KeyValuePair<Type, object>[])old.Clone();
                    updated[i] = new KeyValuePair<Type, object>(type, instance);
                    Volatile.Write(ref _snapshot, updated);
                    return;
                }
            }

            var newArray = new KeyValuePair<Type, object>[old.Length + 1];
            Array.Copy(old, newArray, old.Length);
            newArray[old.Length] = new KeyValuePair<Type, object>(type, instance);
            Volatile.Write(ref _snapshot, newArray);
        }
    }

    /// <summary>Try to get an already-created instance without triggering creation. Lock-free.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGet<T>([NotNullWhen(true)] out T? instance) where T : class
    {
        var snapshot = Volatile.Read(ref _snapshot);
        var type = typeof(T);

        for (var i = 0; i < snapshot.Length; i++)
        {
            if (snapshot[i].Key == type)
            {
                instance = (T)snapshot[i].Value;
                return true;
            }
        }

        instance = default;
        return false;
    }

    /// <summary>Number of registered instances.</summary>
    public static int Count => Volatile.Read(ref _snapshot).Length;
}
