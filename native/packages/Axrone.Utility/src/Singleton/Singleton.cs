using Axrone.Utility.Singleton.Internal;

namespace Axrone.Utility.Singleton;

/// <summary>
/// Static facade for singleton access. Primary entry point for the singleton subsystem.
/// </summary>
/// <remarks>
/// <para>Thread-safe, lock-free on the fast path (single volatile read).</para>
/// <para>Native AOT safe — uses <c>new()</c> constraint, no reflection.</para>
/// </remarks>
/// <example>
/// <code>
/// // Parameterless — requires new() constraint
/// var audio = Singleton.GetInstance&lt;AudioSystem&gt;();
///
/// // Factory — for types without parameterless constructors
/// var config = Singleton.GetInstance(() =&gt; new GameConfig(settings));
///
/// // Eager init from [ModuleInitializer]
/// Singleton.WarmUp&lt;AudioSystem&gt;();
/// </code>
/// </example>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
[SkipLocalsInit]
public static class Singleton
{
    /// <summary>Get or create the singleton instance of <typeparamref name="T"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T GetInstance<T>() where T : class, new() =>
        SingletonProvider<T>.Instance;

    /// <summary>Get or create a singleton using a custom factory.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T GetInstance<T>(Func<T> factory) where T : class =>
        FactoryProvider<T>.GetOrCreateInstance(factory);

    /// <summary>Check if a singleton type has been initialized.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool IsInitialized<T>() where T : class, new() =>
        SingletonProvider<T>.IsInitialized;

    /// <summary>Force eager initialization. Safe to call multiple times.</summary>
    /// <remarks>Call from <c>[ModuleInitializer]</c> for startup-time initialization.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void WarmUp<T>() where T : class, new() =>
        SingletonProvider<T>.WarmUp();
}
