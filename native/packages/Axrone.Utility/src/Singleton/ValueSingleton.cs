namespace Axrone.Utility.Singleton;

/// <summary>
/// Zero-copy value type singleton — provides <c>ref readonly T</c> access to a single struct instance.
/// </summary>
/// <typeparam name="T">Value type to singleton.</typeparam>
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public static class ValueSingleton<T> where T : struct
{
    private static T _value;
    private static int _isInitialized;

    /// <summary>Get a read-only reference to the singleton value.</summary>
    public static ref readonly T Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            EnsureInitialized();
            return ref _value;
        }
    }

    /// <summary>Whether the value has been initialized.</summary>
    public static bool IsInitialized => Volatile.Read(ref _isInitialized) == 1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void EnsureInitialized()
    {
        if (Interlocked.CompareExchange(ref _isInitialized, 1, 0) == 0)
        {
            if (typeof(IValueInitializable).IsAssignableFrom(typeof(T)))
            {
                object boxed = _value!;
                ((IValueInitializable)boxed).Initialize();
                _value = (T)boxed;
            }
        }
    }
}
