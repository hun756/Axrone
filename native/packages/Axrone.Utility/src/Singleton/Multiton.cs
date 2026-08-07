using Axrone.Utility.Singleton.Internal;

namespace Axrone.Utility.Singleton;

/// <summary>
/// Generic multiton — one instance per typed key. AOT-safe: uses <c>new()</c> constraint
/// instead of reflection-based constructor lookup.
/// </summary>
/// <example>
/// <code>
/// // Get or create a PlayerData for each player ID
/// var data = Multiton&lt;PlayerData, int&gt;.GetOrCreate(42);
/// </code>
/// </example>
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public static class Multiton<T, TKey> where T : class, new() where TKey : notnull
{
    private static readonly ConcurrentDictionary<TKey, T> _instances = new();

    /// <summary>Get or create an instance for the given key.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T GetOrCreate(TKey key) =>
        _instances.GetOrAdd(key, static k => CreateInstance(k));

    /// <summary>Try to get an existing instance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool TryGet(TKey key, [NotNullWhen(true)] out T? instance) =>
        _instances.TryGetValue(key, out instance);

    /// <summary>Remove an instance for a key.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool TryRemove(TKey key, [NotNullWhen(true)] out T? instance) =>
        _instances.TryRemove(key, out instance);

    /// <summary>Clear all instances.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Clear() => _instances.Clear();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T CreateInstance(TKey key)
    {
        try
        {
            using (NestingContext.EnterScope())
            {
                var instance = new T();
                if (instance is IInitializable initializable)
                    initializable.Initialize();
                if (instance is IKeyed<TKey> keyed)
                    keyed.Key = key;
                return instance;
            }
        }
        catch (Exception ex) when (ex is not SingletonCreationException)
        {
            throw new SingletonCreationException(
                $"Failed to create multiton instance of type {typeof(T).Name} with key {key}",
                typeof(T), ex);
        }
    }
}
