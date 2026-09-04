namespace Axrone.Memory.Lifetime;

public static class SingletonRegistryExtensions
{
    public static SingletonHandle<T> GetHandle<T>(this ISingletonRegistry registry) where T : class
    {
        ArgumentNullException.ThrowIfNull(registry);
        return new SingletonHandle<T>(new RegistrySingletonProxy<T>(registry));
    }

    private sealed class RegistrySingletonProxy<T> : ISingleton<T> where T : class
    {
        private readonly ISingletonRegistry _registry;

        public RegistrySingletonProxy(ISingletonRegistry registry)
        {
            _registry = registry;
        }

        public T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _registry.Get<T>();
        }

        public bool IsValueCreated => _registry.TryGet<T>(out _);

        public SingletonLifecycleState State => _registry.Contains<T>() ? SingletonLifecycleState.Initialized : SingletonLifecycleState.Uninitialized;
    }
}
