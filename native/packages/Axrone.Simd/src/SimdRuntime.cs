namespace Axrone.Simd;

/// <summary>Capability-backed SIMD runtime: probing, policy checks, dispatch telemetry.</summary>
public interface ISimdRuntime
{
    /// <summary>Probed capabilities.</summary>
    ref readonly SimdCapabilities Capabilities { get; }

    /// <summary>Dispatch counters.</summary>
    ref readonly SimdTelemetryCounters Telemetry { get; }

    /// <summary>Point-in-time counter snapshot.</summary>
    SimdTelemetrySnapshot GetTelemetrySnapshot();

    /// <summary>Tests a single feature.</summary>
    bool IsSupported(SimdFeature feature);

    /// <summary>Tests a dispatch policy.</summary>
    bool IsPolicySatisfied<TPolicy>() where TPolicy : ISimdPolicy;

    /// <summary>Records one dispatch on the tier.</summary>
    void RecordDispatch(SimdDispatchTier tier);
}

/// <summary>Runtime over an explicit capability set (tests, simulators, forced tiers).</summary>
public sealed class ConfigurableSimdRuntime : ISimdRuntime
{
    private readonly SimdCapabilities _capabilities;
    private SimdTelemetryCounters _telemetry;

    /// <summary>Creates a runtime over fixed capabilities.</summary>
    public ConfigurableSimdRuntime(SimdCapabilities capabilities)
    {
        _capabilities = capabilities;
    }

    /// <inheritdoc/>
    public ref readonly SimdCapabilities Capabilities
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => ref _capabilities;
    }

    /// <inheritdoc/>
    public ref readonly SimdTelemetryCounters Telemetry
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => ref _telemetry;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public SimdTelemetrySnapshot GetTelemetrySnapshot() => _telemetry.CreateSnapshot();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool IsSupported(SimdFeature feature) => _capabilities.HasFeature(feature);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool IsPolicySatisfied<TPolicy>() where TPolicy : ISimdPolicy => _capabilities.SatisfiesPolicy<TPolicy>();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void RecordDispatch(SimdDispatchTier tier) => _telemetry.Increment(tier);
}
