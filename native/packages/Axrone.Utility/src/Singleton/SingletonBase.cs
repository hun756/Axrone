using Axrone.Utility.Singleton.Internal;

namespace Axrone.Utility.Singleton;

/// <summary>
/// CRTP base class for type-safe singletons. Provides <c>MySystem.Instance</c> access pattern.
/// </summary>
/// <typeparam name="TSelf">The concrete singleton type (self-referencing generic).</typeparam>
/// <example>
/// <code>
/// public sealed class AudioSystem : SingletonBase&lt;AudioSystem&gt;
/// {
///     protected override void OnCreated() { /* init audio device */ }
/// }
///
/// // Usage:
/// AudioSystem.Instance.Play(sound);
/// </code>
/// </example>
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public abstract class SingletonBase<TSelf> where TSelf : SingletonBase<TSelf>, new()
{
    private static TSelf? _instance;
    private static int _status;

    private const int Uninitialized = 0;
    private const int Initializing = 1;
    private const int Initialized = 2;

    /// <summary>Get the singleton instance. Creates and initializes on first access.</summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public static TSelf Instance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            var status = Volatile.Read(ref _status);
            if (status == Initialized) return Volatile.Read(ref _instance)!;
            return InitializeInstance();
        }
    }

    /// <summary>Whether the instance has been created.</summary>
    public static bool IsInitialized => Volatile.Read(ref _status) == Initialized;

    /// <summary>Force eager initialization.</summary>
    public static void WarmUp() => _ = Instance;

    /// <summary>
    /// Constructor guard. Throws if called directly instead of through <see cref="Instance"/>.
    /// </summary>
    protected SingletonBase()
    {
        if (Volatile.Read(ref _status) != Initializing && !NestingContext.IsNested)
            ThrowInvalidConstruction();
    }

    /// <summary>Called once after construction, before the instance is published.</summary>
    protected virtual void OnCreated() { }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [DoesNotReturn]
    private static void ThrowInvalidConstruction()
    {
        throw new InvalidOperationException(
            $"Singleton type {typeof(TSelf).Name} must be accessed through the Instance property, not constructed directly.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static TSelf InitializeInstance()
    {
        if (Interlocked.CompareExchange(ref _status, Initializing, Uninitialized) == Uninitialized)
        {
            TSelf newInstance;
            try
            {
                using (NestingContext.EnterScope())
                {
                    newInstance = new TSelf();
                    newInstance.OnCreated();

                    if (newInstance is IInitializable initializable)
                        initializable.Initialize();

                    if (newInstance is IAsyncInitializable asyncInitializable)
                        asyncInitializable.InitializeAsync().GetAwaiter().GetResult();
                }
            }
            catch (Exception ex) when (ex is not SingletonCreationException)
            {
                Interlocked.Exchange(ref _status, Uninitialized);
                throw new SingletonCreationException(
                    $"Failed to create singleton instance of type {typeof(TSelf).Name}", typeof(TSelf), ex);
            }

            Volatile.Write(ref _instance, newInstance);
            Volatile.Write(ref _status, Initialized);
            return newInstance;
        }

        SpinWait spinner = default;
        while (Volatile.Read(ref _status) != Initialized)
            spinner.SpinOnce();

        return Volatile.Read(ref _instance)!;
    }

    /// <summary>
    /// Eager initialization hook. Call from a non-generic <c>[ModuleInitializer]</c> or from <c>WarmUp()</c>.
    /// </summary>
    internal static void RegisterForAotInitialization()
    {
        if (typeof(IEagerSingleton).IsAssignableFrom(typeof(TSelf)))
            _ = Instance;
    }
}
