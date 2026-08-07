namespace Axrone.Utility.Singleton;

/// <summary>Declarative singleton lifetime metadata.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SingletonAttribute : Attribute
{
    public SingletonLifetime Lifetime { get; }

    public SingletonAttribute(SingletonLifetime lifetime = SingletonLifetime.Singleton)
    {
        Lifetime = lifetime;
    }
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

/// <summary>Thread-safety mode hint for singleton creation.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ThreadSafeSingletonAttribute : Attribute
{
    public ThreadSafeSingletonAttribute(
        LazyThreadSafetyMode threadSafetyMode = LazyThreadSafetyMode.ExecutionAndPublication)
    {
        ThreadSafetyMode = threadSafetyMode;
    }

    public LazyThreadSafetyMode ThreadSafetyMode { get; }
}
