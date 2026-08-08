namespace Axrone.Utility.Singleton.Internal;

/// <summary>
/// Factory-based singleton storage using <see cref="StrongBox{T}"/> for stable ref access.
/// <see cref="StrongBox{T}"/> provides a heap-allocated mutable cell whose <c>.Value</c> field
/// can be used with <see cref="Volatile"/> without the <c>volatile</c> keyword.
/// Dispatches <see cref="ISingletonLifecycle"/> hooks and registers with the shutdown registry.
/// </summary>
[SkipLocalsInit]
internal static class FactoryProvider<T> where T : class
{
    private static T? _instance;
    private static readonly object _syncRoot = new();
    private static int _initialized;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T GetOrCreateInstance(Func<T> factory)
    {
        var instance = Volatile.Read(ref _instance);
        if (instance is not null)
        {
            SingletonDiagnostics.RecordAccess<T>();
            return instance;
        }

        return InitializeInstance(factory);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T InitializeInstance(Func<T> factory)
    {
        lock (_syncRoot)
        {
            var instance = Volatile.Read(ref _instance);
            if (instance is not null) return instance;

            try
            {
                SingletonDiagnostics.RecordCreationStart<T>();
                using (NestingContext.EnterScope())
                {
                    instance = factory();

                    if (instance is null)
                        throw new SingletonCreationException(
                            $"Factory method returned null for type {typeof(T).FullName}", typeof(T));

                    if (instance is ISingletonLifecycle lifecycle)
                    {
                        lifecycle.Initialize();
                        var asyncTask = lifecycle.InitializeAsync();
                        if (!asyncTask.IsCompletedSuccessfully)
                            asyncTask.AsTask().GetAwaiter().GetResult();
                    }
                }

                SingletonDiagnostics.RecordCreationEnd<T>();
            }
            catch (Exception ex) when (ex is not SingletonCreationException)
            {
                throw new SingletonCreationException(
                    $"Failed to create singleton instance of type {typeof(T).FullName} using factory",
                    typeof(T), ex);
            }

            Volatile.Write(ref _instance, instance);
            SingletonRegistryInternal.Register(instance);
            SingletonShutdownRegistry.Register(instance);
            Interlocked.Exchange(ref _initialized, 1);
            return instance;
        }
    }
}
