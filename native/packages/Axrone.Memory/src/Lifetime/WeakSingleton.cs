using Axrone.Memory.Lifetime.Internal;

namespace Axrone.Memory.Lifetime;

/// <summary>
/// GC-collectible singleton — uses <see cref="WeakReference{T}"/> so the instance can be
/// collected under memory pressure and re-created on next access.
/// Dispatches <see cref="ISingletonLifecycle"/> hooks on creation and re-creation.
/// </summary>
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public static class WeakSingleton<T> where T : class, new()
{
    private static WeakReference<T> _instance = new(CreateInstance());
    private static readonly object _lock = new();

    /// <summary>Get the instance, re-creating if it was collected.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T GetInstance()
    {
        if (_instance.TryGetTarget(out var instance))
            return instance;

        lock (_lock)
        {
            if (!_instance.TryGetTarget(out instance))
            {
                instance = CreateInstance();
                _instance.SetTarget(instance);
            }
            return instance;
        }
    }

    /// <summary>Try to get the instance without re-creating.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool TryGet([NotNullWhen(true)] out T? instance) =>
        _instance.TryGetTarget(out instance);

    /// <summary>Release the weak reference — instance will be re-created on next access.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Release()
    {
        lock (_lock)
        {
            _instance = new WeakReference<T>(default!);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T CreateInstance()
    {
        try
        {
            using (NestingContext.EnterScope())
            {
                var instance = new T();

                if (instance is ISingletonLifecycle lifecycle)
                {
                    lifecycle.Initialize();
                    var asyncTask = lifecycle.InitializeAsync();
                    if (!asyncTask.IsCompletedSuccessfully)
                        asyncTask.AsTask().GetAwaiter().GetResult();
                }

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
