using Axrone.Utility.Singleton.Internal;

namespace Axrone.Utility.Singleton;

/// <summary>
/// Lock-free singleton — CAS-based creation without locks.
/// Multiple threads may create instances concurrently, but only one wins via <see cref="Interlocked"/>.
/// </summary>
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public static class LockFreeSingleton<T> where T : class, new()
{
    private static T? _instance;

    /// <summary>Get or create the singleton via atomic compare-and-swap.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T GetInstance()
    {
        var instance = Volatile.Read(ref _instance);
        if (instance is not null) return instance;

        var newInstance = CreateInstance();
        var winner = Interlocked.CompareExchange(ref _instance, newInstance, null);
        return winner ?? newInstance;
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
                $"Failed to create instance of type {typeof(T).Name}", typeof(T), ex);
        }
    }
}
