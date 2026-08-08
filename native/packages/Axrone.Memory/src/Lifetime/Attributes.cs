namespace Axrone.Memory.Lifetime;

/// <summary>Declarative singleton metadata — lifetime, thread-safety strategy, pinning, and diagnostics.</summary>
/// <remarks>
/// Consolidates the former <c>ThreadSafeSingletonAttribute</c> into a single attribute.
/// Read once per closed type by <c>SingletonAttributeCache&lt;T&gt;</c>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SingletonAttribute : Attribute
{
    /// <summary>Declare singleton metadata with the specified lifetime.</summary>
    /// <param name="lifetime">Singleton lifetime semantics. Defaults to <see cref="SingletonLifetime.Singleton"/>.</param>
    public SingletonAttribute(SingletonLifetime lifetime = SingletonLifetime.Singleton)
    {
        Lifetime = lifetime;
    }

    /// <summary>Singleton lifetime semantics.</summary>
    public SingletonLifetime Lifetime { get; }

    /// <summary>Thread-safety strategy for singleton storage.</summary>
    public SingletonThreadSafety ThreadSafety { get; init; } = SingletonThreadSafety.ExecutionAndPublication;

    /// <summary>
    /// When <see langword="true"/>, the instance is pinned via <see cref="GCHandle"/>
    /// so native code can hold a stable reference.
    /// </summary>
    public bool Pinned { get; init; }

    /// <summary>
    /// When <see langword="true"/>, creation time, access count, and access duration
    /// are tracked via <c>SingletonDiagnostics</c>.
    /// </summary>
    public bool Monitored { get; init; }
}

/// <summary>Singleton lifetime semantics.</summary>
public enum SingletonLifetime
{
    /// <summary>One instance per application domain.</summary>
    Singleton,

    /// <summary>One instance per scope.</summary>
    Scoped,

    /// <summary>New instance on every request.</summary>
    Transient,
}

/// <summary>Thread-safety strategy for singleton storage.</summary>
public enum SingletonThreadSafety
{
    /// <summary>
    /// Standard lock-free CAS + <see cref="SpinWait"/>. One instance globally, safe for concurrent access.
    /// </summary>
    ExecutionAndPublication,

    /// <summary>One instance per thread via <c>[ThreadStatic]</c>.</summary>
    ThreadStatic,

    /// <summary>GC-collectible via <see cref="WeakReference{T}"/> — re-created after collection.</summary>
    Weak,
}
