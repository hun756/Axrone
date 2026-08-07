namespace Axrone.Utility.Singleton.Internal;

/// <summary>
/// Factory-based singleton storage using <see cref="StrongBox{T}"/> for stable ref access.
/// <see cref="StrongBox{T}"/> provides a heap-allocated mutable cell whose <c>.Value</c> field
/// can be used with <see cref="Volatile"/> without the <c>volatile</c> keyword.
/// </summary>
[SkipLocalsInit]
internal static class FactoryProvider<T> where T : class
{
    private static readonly StrongBox<T?> _instance = new();
    private static readonly object _syncRoot = new();
    private static int _initialized;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T GetOrCreateInstance(Func<T> factory)
    {
        var instance = Volatile.Read(ref _instance.Value);
        if (instance is not null) return instance;

        return InitializeInstance(factory);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T InitializeInstance(Func<T> factory)
    {
        lock (_syncRoot)
        {
            var instance = _instance.Value;
            if (instance is not null) return instance;

            try
            {
                using (NestingContext.EnterScope())
                {
                    instance = factory();

                    if (instance is null)
                        throw new SingletonCreationException(
                            $"Factory method returned null for type {typeof(T).FullName}", typeof(T));

                    if (instance is IInitializable initializable)
                        initializable.Initialize();

                    if (instance is IAsyncInitializable asyncInitializable)
                        asyncInitializable.InitializeAsync().GetAwaiter().GetResult();
                }
            }
            catch (Exception ex) when (ex is not SingletonCreationException)
            {
                throw new SingletonCreationException(
                    $"Failed to create singleton instance of type {typeof(T).FullName} using factory",
                    typeof(T), ex);
            }

            Interlocked.Exchange(ref _instance.Value, instance);
            Interlocked.Exchange(ref _initialized, 1);
            return instance;
        }
    }
}
