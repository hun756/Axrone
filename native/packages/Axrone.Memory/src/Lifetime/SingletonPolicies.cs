namespace Axrone.Memory.Lifetime;

/// <summary>
/// Default factory that produces instances with the parameterless constructor. Used by
/// the builder entry point when the caller does not supply a custom factory.
/// </summary>
/// <typeparam name="T">The instance type to produce.</typeparam>
public readonly struct DefaultActivatorFactory<T> : ISingletonFactory<T>
    where T : class, new()
{
    /// <summary>Produces a new instance using the parameterless constructor.</summary>
    /// <returns>The produced instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Create() => new T();
}

/// <summary>
/// Backoff policy that grows the delay exponentially with the attempt number, capped at
/// a configurable maximum. Suitable for drain waits where held leases are expected to
/// release quickly at first and progressively more slowly.
/// </summary>
public readonly struct AdaptiveBackoff : IDelayPolicy
{
    private readonly int _maxAttempts;
    private readonly TimeSpan _maxDelay;
    private readonly double _baseDelayMilliseconds;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveBackoff"/> struct with
    /// default bounds.
    /// </summary>
    public AdaptiveBackoff()
        : this(64, 1.0, 25.0)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveBackoff"/> struct.
    /// </summary>
    /// <param name="maxAttempts">The maximum number of attempts to space.</param>
    /// <param name="baseDelayMilliseconds">The delay used for the first attempt, in milliseconds.</param>
    /// <param name="maxDelayMilliseconds">The upper bound for any computed delay, in milliseconds.</param>
    public AdaptiveBackoff(int maxAttempts = 64, double baseDelayMilliseconds = 1.0, double maxDelayMilliseconds = 25.0)
    {
        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "The maximum attempt count must be positive.");
        }

        if (baseDelayMilliseconds < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(baseDelayMilliseconds), baseDelayMilliseconds, "The base delay cannot be negative.");
        }

        if (maxDelayMilliseconds < baseDelayMilliseconds)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDelayMilliseconds), maxDelayMilliseconds, "The maximum delay cannot be smaller than the base delay.");
        }

        _maxAttempts = maxAttempts;
        _baseDelayMilliseconds = baseDelayMilliseconds;
        _maxDelay = TimeSpan.FromMilliseconds(maxDelayMilliseconds);
    }

    /// <summary>Gets the maximum number of attempts the policy will space.</summary>
    public int MaxAttempts => _maxAttempts;

    /// <summary>
    /// Computes an exponentially growing delay capped at the configured maximum.
    /// </summary>
    /// <param name="attempt">The zero-based attempt number.</param>
    /// <returns>The delay to observe before the next attempt.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TimeSpan NextDelay(int attempt)
    {
        if (attempt < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attempt), attempt, "The attempt number cannot be negative.");
        }

        var shift = Math.Min(attempt, 24);
        var delay = _baseDelayMilliseconds * Math.Pow(2.0, shift);
        if (double.IsInfinity(delay) || delay >= _maxDelay.TotalMilliseconds)
        {
            return _maxDelay;
        }

        return TimeSpan.FromMilliseconds(delay);
    }
}

/// <summary>
/// Backoff policy that busy-spins for the first few attempts and then falls back to
/// exponentially growing delays. Suitable for lease-release waits where the expected
/// release time is sub-millisecond.
/// </summary>
public readonly struct ExponentialSpinBackoff : IDelayPolicy
{
    private readonly int _maxAttempts;
    private readonly int _spinThreshold;
    private readonly TimeSpan _maxDelay;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExponentialSpinBackoff"/> struct
    /// with default bounds. Declared explicitly because <c>new()</c> on a struct
    /// without one yields default state and skips all-optional constructors.
    /// </summary>
    public ExponentialSpinBackoff()
        : this(64, 8, 10.0)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExponentialSpinBackoff"/> struct.
    /// </summary>
    /// <param name="maxAttempts">The maximum number of attempts to space.</param>
    /// <param name="spinThreshold">The number of initial attempts that busy-spin instead of sleeping.</param>
    /// <param name="maxDelayMilliseconds">The upper bound for any computed delay, in milliseconds.</param>
    public ExponentialSpinBackoff(int maxAttempts = 64, int spinThreshold = 8, double maxDelayMilliseconds = 10.0)
    {
        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "The maximum attempt count must be positive.");
        }

        if (spinThreshold < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(spinThreshold), spinThreshold, "The spin threshold cannot be negative.");
        }

        if (maxDelayMilliseconds < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDelayMilliseconds), maxDelayMilliseconds, "The maximum delay cannot be negative.");
        }

        _maxAttempts = maxAttempts;
        _spinThreshold = spinThreshold;
        _maxDelay = TimeSpan.FromMilliseconds(maxDelayMilliseconds);
    }

    /// <summary>Gets the maximum number of attempts the policy will space.</summary>
    public int MaxAttempts => _maxAttempts;

    /// <summary>
    /// Computes a zero delay for the spin phase and an exponential delay afterwards.
    /// </summary>
    /// <param name="attempt">The zero-based attempt number.</param>
    /// <returns>The delay to observe before the next attempt.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TimeSpan NextDelay(int attempt)
    {
        if (attempt < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attempt), attempt, "The attempt number cannot be negative.");
        }

        if (attempt < _spinThreshold)
        {
            return TimeSpan.Zero;
        }

        var shift = Math.Min(attempt - _spinThreshold, 24);
        var delay = Math.Pow(2.0, shift);
        if (double.IsInfinity(delay) || delay >= _maxDelay.TotalMilliseconds)
        {
            return _maxDelay;
        }

        return TimeSpan.FromMilliseconds(delay);
    }
}

/// <summary>
/// Diagnostics implementation that discards every callback. Used as the default policy
/// when the caller does not supply a custom diagnostics sink.
/// </summary>
public readonly struct NullDiagnostics : ISingletonDiagnostics
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NullDiagnostics"/> struct.
    /// </summary>
    public NullDiagnostics()
    {
    }

    /// <summary>Discards the instance-created notification.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnInstanceCreated(InstanceName name)
    {
    }

    /// <summary>Discards the instance-faulted notification.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnInstanceFaulted(InstanceName name, Exception error)
    {
    }

    /// <summary>Discards the lease-acquired notification.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnLeaseAcquired(InstanceName name, LeaseCount activeLeases)
    {
    }

    /// <summary>Discards the lease-released notification.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnLeaseReleased(InstanceName name, LeaseCount activeLeases)
    {
    }

    /// <summary>Discards the drain-started notification.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnDrainStarted(InstanceName name)
    {
    }

    /// <summary>Discards the drain-completed notification.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnDrainCompleted(InstanceName name, DrainResult result)
    {
    }

    /// <summary>Discards the disposed notification.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnDisposed(InstanceName name)
    {
    }
}
