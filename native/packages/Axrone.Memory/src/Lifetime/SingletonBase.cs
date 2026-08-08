using Axrone.Memory.Lifetime.Internal;

namespace Axrone.Memory.Lifetime;

/// <summary>
/// CRTP base class for type-safe singletons. Provides <c>MySystem.Instance</c> access pattern.
/// Supports <see cref="IDisposable"/>, GCHandle pinning via <c>[Singleton(Pinned = true)]</c>,
/// and <see cref="ISingletonLifecycle"/> dispatch.
/// </summary>
/// <typeparam name="TSelf">The concrete singleton type (self-referencing generic).</typeparam>
/// <example>
/// <code>
/// public sealed class AudioSystem : SingletonBase&lt;AudioSystem&gt;, ISingletonLifecycle
/// {
///     protected override void OnCreated() { /* init audio device */ }
///     void ISingletonLifecycle.Initialize() { /* load sound banks */ }
/// }
///
/// // Usage:
/// AudioSystem.Instance.Play(sound);
/// </code>
/// </example>
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public abstract class SingletonBase<TSelf> : IDisposable where TSelf : SingletonBase<TSelf>, new()
{
    private static TSelf? _instance;
    private static int _status;
    private static GCHandle _pinHandle;
    private static int _disposed;

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
            if (status == Initialized)
            {
                SingletonDiagnostics.RecordAccess<TSelf>();
                return Volatile.Read(ref _instance)!;
            }
            return InitializeInstance();
        }
    }

    /// <summary>Whether the instance has been created.</summary>
    public static bool IsInitialized
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _status) == Initialized;
    }

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

    /// <summary>Called once after construction, before lifecycle hooks.</summary>
    protected virtual void OnCreated() { }

    /// <summary>Release resources held by this singleton. Idempotent.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Override to release managed/unmanaged resources.</summary>
    /// <param name="disposing"><see langword="true"/> from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        if (_pinHandle.IsAllocated)
            _pinHandle.Free();
    }

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
        while (true)
        {
            if (Interlocked.CompareExchange(ref _status, Initializing, Uninitialized) == Uninitialized)
            {
                TSelf newInstance;
                try
                {
                    SingletonDiagnostics.RecordCreationStart<TSelf>();
                    using (NestingContext.EnterScope())
                    {
                        newInstance = new TSelf();
                        newInstance.OnCreated();

                        if (newInstance is ISingletonLifecycle lifecycle)
                        {
                            lifecycle.Initialize();
                            var asyncTask = lifecycle.InitializeAsync();
                            if (!asyncTask.IsCompletedSuccessfully)
                                asyncTask.AsTask().GetAwaiter().GetResult();
                        }
                    }

                    if (SingletonAttributeCache<TSelf>.Pinned)
                        _pinHandle = GCHandle.Alloc(newInstance, GCHandleType.Normal);

                    SingletonDiagnostics.RecordCreationEnd<TSelf>();
                }
                catch (Exception ex) when (ex is not SingletonCreationException)
                {
                    Interlocked.Exchange(ref _status, Uninitialized);
                    throw new SingletonCreationException(
                        $"Failed to create singleton instance of type {typeof(TSelf).Name}", typeof(TSelf), ex);
                }

                Volatile.Write(ref _instance, newInstance);
                SingletonRegistryInternal.Register(newInstance);
                SingletonShutdownRegistry.Register(newInstance);
                Volatile.Write(ref _status, Initialized);
                return newInstance;
            }

            SpinWait spinner = default;
            while (Volatile.Read(ref _status) == Initializing)
                spinner.SpinOnce();

            if (Volatile.Read(ref _status) == Initialized)
            {
                var result = Volatile.Read(ref _instance)!;
                SingletonDiagnostics.RecordAccess<TSelf>();
                return result;
            }
        }
    }
}
