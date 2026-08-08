using Axrone.Utility.Singleton.Internal;

namespace Axrone.Utility.Singleton;

/// <summary>
/// Static facade for the singleton subsystem. Primary entry point for singleton access,
/// registration, diagnostics, and orderly shutdown.
/// </summary>
/// <remarks>
/// <para>Thread-safe, lock-free on the fast path (single volatile read).</para>
/// <para>Native AOT safe — uses <c>new()</c> constraint, no reflection on hot paths.</para>
/// </remarks>
/// <example>
/// <code>
/// // Parameterless — requires new() constraint
/// var audio = Singleton.GetInstance&lt;AudioSystem&gt;();
///
/// // Factory — for types without parameterless constructors
/// var config = Singleton.GetInstance(() =&gt; new GameConfig(settings));
///
/// // Pre-register an instance
/// Singleton.Register&lt;ILogger&gt;(new FileLogger("app.log"));
///
/// // Orderly shutdown at application exit
/// await Singleton.ShutdownAsync();
/// </code>
/// </example>
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
    public static bool IsInitialized<T>() where T : class =>
        SingletonRegistryInternal.TryGet(out T? _);

    /// <summary>
    /// Try to get an already-created instance without triggering creation.
    /// Returns <see langword="false"/> if the singleton has not been accessed yet.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool TryGet<T>([NotNullWhen(true)] out T? instance) where T : class =>
        SingletonRegistryInternal.TryGet(out instance);

    /// <summary>
    /// Register a pre-created instance for type <typeparamref name="T"/>.
    /// Subsequent calls to <see cref="GetInstance{T}()"/> will return this instance
    /// only if the provider has not already created one.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Register<T>(T instance) where T : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        SingletonRegistryInternal.Register(instance);
        SingletonShutdownRegistry.Register(instance);
    }

    /// <summary>Force eager initialization. Safe to call multiple times.</summary>
    /// <remarks>Call from <c>[ModuleInitializer]</c> for startup-time initialization.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void WarmUp<T>() where T : class, new() =>
        SingletonProvider<T>.WarmUp();

    /// <summary>Get diagnostics metrics for a monitored singleton type.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SingletonMetrics GetMetrics<T>() where T : class =>
        SingletonDiagnostics.GetMetrics<T>();

    /// <summary>
    /// Dispose all tracked singleton instances in reverse creation order.
    /// Call at application exit for deterministic teardown. Safe to call multiple times.
    /// </summary>
    public static ValueTask ShutdownAsync() =>
        SingletonShutdownRegistry.ShutdownAsync();
}
