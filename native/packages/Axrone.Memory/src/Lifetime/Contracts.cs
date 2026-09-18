using System.Diagnostics.CodeAnalysis;

namespace Axrone.Memory.Lifetime;

/// <summary>Provides synchronous access to a singleton instance.</summary>
public interface ISingleton<out T>
{
    T Value { get; }
    bool IsValueCreated { get; }
    SingletonLifecycleState State { get; }
}

/// <summary>Provides asynchronous access to a singleton instance.</summary>
public interface IAsyncSingleton<T>
{
    ValueTask<T> GetValueAsync(CancellationToken cancellationToken = default);
    bool IsValueCreated { get; }
    SingletonLifecycleState State { get; }
}

/// <summary>Factory for creating singleton instances synchronously.</summary>
public interface ISingletonFactory<out T>
{
    T Create();
}

/// <summary>Factory for creating singleton instances asynchronously.</summary>
public interface IAsyncSingletonFactory<T>
{
    ValueTask<T> CreateAsync(CancellationToken cancellationToken = default);
}

/// <summary>Registry for managing multiple singleton instances with resolution and lifecycle control.</summary>
public interface ISingletonRegistry : IDisposable, IAsyncDisposable
{
    void Register<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>() where T : class, new();
    void Register<T>(Func<T> factory, LazyThreadSafetyMode mode = LazyThreadSafetyMode.ExecutionAndPublication) where T : class;
    void Register<T>(ISingleton<T> singleton) where T : class;
    void RegisterInstance<T>(T instance) where T : class;
    T Get<T>() where T : class;
    bool TryGet<T>([NotNullWhen(true)] out T? instance) where T : class;
    bool Contains<T>() where T : class;
}

/// <summary>A scoped singleton registry that can create child scopes.</summary>
public interface ISingletonScope : ISingletonRegistry
{
    ISingletonScope CreateChildScope();
}
