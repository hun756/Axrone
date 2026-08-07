using Axrone.Utility.Singleton.Internal;

namespace Axrone.Utility.Singleton;

/// <summary>
/// Per-thread singleton — each thread gets its own lazily-initialized instance.
/// Useful for render-thread, physics-thread, or other thread-local state.
/// </summary>
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public static class ThreadStaticSingleton<T> where T : class, new()
{
    [ThreadStatic]
    private static T? _instance;

    /// <summary>Get the thread-local singleton instance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T GetInstance()
    {
        if (_instance is null)
        {
            _instance = CreateInstance();
        }
        return _instance;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T CreateInstance()
    {
        try
        {
            using (NestingContext.EnterScope())
            {
                var instance = new T();
                if (instance is IInitializable initializable)
                    initializable.Initialize();
                return instance;
            }
        }
        catch (Exception ex) when (ex is not SingletonCreationException)
        {
            throw new SingletonCreationException(
                $"Failed to create thread-local instance of type {typeof(T).Name}", typeof(T), ex);
        }
    }
}
