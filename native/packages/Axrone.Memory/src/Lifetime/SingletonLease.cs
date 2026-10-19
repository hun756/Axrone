namespace Axrone.Memory.Lifetime;

/// <summary>
/// RAII lease token representing one outstanding reference against the singleton
/// instance. Disposing the lease decrements the engine's lease count; disposing the
/// same lease twice is a no-op.
/// </summary>
/// <typeparam name="T">The singleton instance type.</typeparam>
/// <typeparam name="TFactory">The factory struct that produced the instance.</typeparam>
/// <typeparam name="TBackoff">The backoff policy struct used by the engine.</typeparam>
/// <typeparam name="TDiagnostics">The diagnostics sink struct used by the engine.</typeparam>
public readonly struct SingletonLease<T, TFactory, TBackoff, TDiagnostics> : IDisposable
    where T : class
    where TFactory : struct, ISingletonFactory<T>
    where TBackoff : struct, IDelayPolicy
    where TDiagnostics : struct, ISingletonDiagnostics
{
    private readonly Singleton<T, TFactory, TBackoff, TDiagnostics>? _engine;
    private readonly T? _instance;
    private readonly Iteration _iteration;

    /// <summary>
    /// Initializes a new instance of the <see cref="SingletonLease{T, TFactory, TBackoff, TDiagnostics}"/> struct.
    /// </summary>
    /// <param name="engine">The engine that granted the lease.</param>
    /// <param name="instance">The instance the lease guards.</param>
    /// <param name="iteration">The iteration stamp captured when the lease was granted.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal SingletonLease(Singleton<T, TFactory, TBackoff, TDiagnostics> engine, T instance, Iteration iteration)
    {
        _engine = engine;
        _instance = instance;
        _iteration = iteration;
    }

    /// <summary>Gets the instance guarded by this lease.</summary>
    public T Instance => _instance ?? SingletonThrowHelper.ThrowDisposed<T>();

    /// <summary>Gets the iteration stamp captured when the lease was granted.</summary>
    public Iteration Iteration => _iteration;

    /// <summary>Determines whether the lease is still bound to a live engine.</summary>
    public bool IsValid => _engine is not null;

    /// <summary>Releases the lease back to the engine. Releasing the same lease twice is a no-op.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        var engine = _engine;
        if (engine is null)
        {
            return;
        }

        engine.ReleaseLease();
    }
}
