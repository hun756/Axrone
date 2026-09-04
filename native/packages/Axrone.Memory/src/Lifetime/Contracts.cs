using System.Diagnostics.CodeAnalysis;

namespace Enterprise.Patterns.Singleton;

public interface ISingleton<out T>
{
    T Value { get; }
    bool IsValueCreated { get; }
    SingletonLifecycleState State { get; }
}

public interface IAsyncSingleton<T>
{
    ValueTask<T> GetValueAsync(CancellationToken cancellationToken = default);
    bool IsValueCreated { get; }
    SingletonLifecycleState State { get; }
}

public interface ISingletonFactory<out T>
{
    T Create();
}

public interface IAsyncSingletonFactory<T>
{
    ValueTask<T> CreateAsync(CancellationToken cancellationToken = default);
}

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

public interface ISingletonScope : ISingletonRegistry
{
    ISingletonScope CreateChildScope();
}
