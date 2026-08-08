namespace Axrone.Memory.Lifetime;

/// <summary>
/// Unified lifecycle contract for singleton types. All methods are optional via default interface methods —
/// implement only the hooks your type needs.
/// </summary>
/// <remarks>
/// <para><b>Initialization ordering:</b> <see cref="Initialize"/> is always called before
/// <see cref="InitializeAsync"/>. If you implement only async setup, the sync hook runs as a no-op first.</para>
/// <para><b>Native AOT safety:</b> The default interface methods used here have trivial bodies
/// (empty or <c>return default</c>) and are fully supported under Native AOT compilation.
/// No complex DIM dispatch or interface-constrained generic calls are involved.</para>
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
    /// <summary>Called once after construction, before the instance is published. Always called before <see cref="InitializeAsync"/>.</summary>
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
/// <remarks>
/// The <see cref="Key"/> property is set by <c>KeyedSingleton&lt;TSelf, TKey&gt;</c> immediately
/// after construction and before <see cref="ISingletonLifecycle.Initialize"/>. Do not mutate
/// the key after initialization — it is used as the dictionary lookup identity.
/// </remarks>
/// <typeparam name="TKey">The key type.</typeparam>
public interface IKeyed<TKey>
{
    /// <summary>The key identifying this instance. Set once by the subsystem during creation.</summary>
    TKey Key { get; set; }
}
