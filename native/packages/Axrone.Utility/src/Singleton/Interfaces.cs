namespace Axrone.Utility.Singleton;

/// <summary>
/// Unified lifecycle contract for singleton types. All methods are optional via default interface methods —
/// implement only the hooks your type needs.
/// </summary>
/// <remarks>
/// Replaces the separate <c>IInitializable</c>, <c>IAsyncInitializable</c>, and <c>IEagerSingleton</c> interfaces
/// with a single coherent contract. The subsystem dispatches to these methods during singleton creation.
/// </remarks>
/// <example>
/// <code>
/// public sealed class AudioSystem : SingletonBase&lt;AudioSystem&gt;, ISingletonLifecycle
/// {
///     void ISingletonLifecycle.Initialize() { /* sync init */ }
///     bool ISingletonLifecycle.IsEager =&gt; true; /* init at startup */
/// }
/// </code>
/// </example>
public interface ISingletonLifecycle
{
    /// <summary>Called once after construction, before the instance is published.</summary>
    void Initialize() { }

    /// <summary>Called once after <see cref="Initialize"/> for async setup (device handles, file loads).</summary>
    ValueTask InitializeAsync() => default;

    /// <summary>
    /// When <see langword="true"/>, the subsystem eagerly creates this instance at startup
    /// rather than waiting for first access.
    /// </summary>
    bool IsEager => false;
}

/// <summary>Keyed identity — for multiton/keyed types that receive their key at construction.</summary>
/// <typeparam name="TKey">The key type.</typeparam>
public interface IKeyed<TKey>
{
    TKey Key { get; set; }
}
