using Axrone.Memory.Lifetime.Internal;

namespace Axrone.Memory.Lifetime;

/// <summary>
/// CRTP base for keyed singletons (multiton pattern). One instance per unique key,
/// created lazily on first request. Merges the former <c>NamedSingleton</c> and <c>Multiton</c> types.
/// </summary>
/// <typeparam name="TSelf">The concrete type (self-referencing generic).</typeparam>
/// <typeparam name="TKey">The key type — must be usable as a dictionary key.</typeparam>
/// <example>
/// <code>
/// public sealed class PlayerSession : KeyedSingleton&lt;PlayerSession, Guid&gt;, IKeyed&lt;Guid&gt;
/// {
///     public Guid Key { get; set; }
///     protected override void OnCreated() { /* allocate session resources */ }
/// }
///
/// // Usage:
/// var session = PlayerSession.GetInstance(playerId);
/// </code>
/// </example>
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public abstract class KeyedSingleton<TSelf, TKey> : IDisposable
    where TSelf : KeyedSingleton<TSelf, TKey>, new()
    where TKey : notnull
{
    private static readonly ConcurrentDictionary<TKey, TSelf> _instances = new();

    /// <summary>Get or create the instance for the given key.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static TSelf GetInstance(TKey key)
    {
        if (_instances.TryGetValue(key, out var existing))
            return existing;

        return GetOrAdd(key);
    }

    /// <summary>Try to get an existing instance without creating one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool TryGet(TKey key, [NotNullWhen(true)] out TSelf? instance) =>
        _instances.TryGetValue(key, out instance);

    /// <summary>Number of active keyed instances.</summary>
    public static int Count => _instances.Count;

    /// <summary>Remove and dispose a keyed instance.</summary>
    public static bool TryRemove(TKey key)
    {
        if (!_instances.TryRemove(key, out var instance))
            return false;

        if (instance is IDisposable disp)
            disp.Dispose();
        return true;
    }

    /// <summary>Remove and dispose all keyed instances.</summary>
    public static void Clear()
    {
        var keys = _instances.Keys;
        var toDispose = new List<TSelf>(keys.Count);

        foreach (var key in keys)
        {
            if (_instances.TryRemove(key, out var instance))
                toDispose.Add(instance);
        }

        foreach (var instance in toDispose)
        {
            if (instance is IDisposable disp)
                disp.Dispose();
        }
    }

    /// <summary>Called once after construction, before lifecycle hooks.</summary>
    protected virtual void OnCreated() { }

    /// <summary>Release resources held by this keyed instance.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Override to release managed/unmanaged resources.</summary>
    /// <param name="disposing"><see langword="true"/> from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing) { }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static TSelf GetOrAdd(TKey key)
    {
        return _instances.GetOrAdd(key, static k =>
        {
            TSelf instance;
            try
            {
                using (NestingContext.EnterScope())
                {
                    instance = new TSelf();

                    if (instance is IKeyed<TKey> keyed)
                        keyed.Key = k;

                    instance.OnCreated();

                    if (instance is ISingletonLifecycle lifecycle)
                    {
                        lifecycle.Initialize();
                        var asyncTask = lifecycle.InitializeAsync();
                        if (!asyncTask.IsCompletedSuccessfully)
                            asyncTask.AsTask().GetAwaiter().GetResult();
                    }
                }
            }
            catch (Exception ex) when (ex is not SingletonCreationException)
            {
                throw new SingletonCreationException(
                    $"Failed to create keyed singleton instance of type {typeof(TSelf).Name} with key '{k}'",
                    typeof(TSelf), ex);
            }

            SingletonShutdownRegistry.Register(instance);
            return instance;
        });
    }
}
