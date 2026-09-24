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

/// <summary>Scoped custom-runtime activation; restores the previous runtime on dispose.</summary>
public readonly struct SimdRuntimeScope : IDisposable
{
    private readonly ISimdRuntime? _previousRuntime;

    internal SimdRuntimeScope(ISimdRuntime customRuntime)
    {
        _previousRuntime = SimdRuntime.SetCustomRuntime(customRuntime);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        SimdRuntime.SetCustomRuntime(_previousRuntime);
    }
}

/// <summary>
/// Process-wide SIMD runtime. Capabilities probe once at startup; a custom runtime can
/// override them (scoped, race-free) so tests simulate tiers the host lacks.
/// </summary>
public static class SimdRuntime
{
    private static readonly SimdCapabilities s_hardwareCapabilities = ProbeSystemCapabilities();
    private static SimdTelemetryCounters s_hardwareTelemetry;
    private static ISimdRuntime? s_customInstance;

    /// <summary>Active runtime: the custom override or the hardware default.</summary>
    public static ISimdRuntime Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => Volatile.Read(ref s_customInstance) ?? DefaultRuntime.Instance;
    }

    /// <summary>Active capabilities.</summary>
    public static ref readonly SimdCapabilities Capabilities
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            ISimdRuntime? custom = Volatile.Read(ref s_customInstance);
            return ref custom != null ? ref custom.Capabilities : ref s_hardwareCapabilities;
        }
    }

    /// <summary>Active dispatch counters.</summary>
    public static ref readonly SimdTelemetryCounters Telemetry
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            ISimdRuntime? custom = Volatile.Read(ref s_customInstance);
            return ref custom != null ? ref custom.Telemetry : ref s_hardwareTelemetry;
        }
    }

    /// <summary>Whether portable vectors may run the hardware path.</summary>
    internal static bool UseVectorPath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => IsSupported(SimdFeature.VectorHardwareAccelerated);
    }

    /// <summary>Whether the 128-bit portable path may run.</summary>
    internal static bool UseVector128Path
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => IsSupported(SimdFeature.Vector128HardwareAccelerated);
    }

    /// <summary>Whether the 256-bit portable path may run.</summary>
    internal static bool UseVector256Path
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => IsSupported(SimdFeature.Vector256HardwareAccelerated);
    }

    /// <summary>Whether the 512-bit portable path may run.</summary>
    internal static bool UseVector512Path
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => IsSupported(SimdFeature.Vector512HardwareAccelerated);
    }

    /// <summary>Point-in-time counter snapshot.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SimdTelemetrySnapshot GetTelemetrySnapshot() => Telemetry.CreateSnapshot();

    /// <summary>Tests a single feature against the active capabilities.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool IsSupported(SimdFeature feature) => Capabilities.HasFeature(feature);

    /// <summary>Tests a dispatch policy against the active capabilities.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool IsPolicySatisfied<TPolicy>() where TPolicy : ISimdPolicy => Capabilities.SatisfiesPolicy<TPolicy>();

    /// <summary>
    /// Installs a custom runtime with a full memory barrier. Null restores the default.
    /// </summary>
    /// <param name="customRuntime">Override runtime, or null for hardware default.</param>
    /// <returns>The previously active runtime.</returns>
    public static ISimdRuntime? SetCustomRuntime(ISimdRuntime? customRuntime)
    {
        return Interlocked.Exchange(ref s_customInstance, customRuntime);
    }

    /// <summary>Activates a custom runtime for a disposable scope.</summary>
    public static SimdRuntimeScope UseCustomRuntime(ISimdRuntime customRuntime)
    {
        ArgumentNullException.ThrowIfNull(customRuntime);
        return new SimdRuntimeScope(customRuntime);
    }

    /// <summary>Builds a custom runtime from explicit capabilities.</summary>
    public static ISimdRuntime CreateCustom(
        FeatureBitmask256 mask,
        SimdArchitecture arch,
        SimdIsaLevel level,
        SimdRegisterWidth width,
        SimdAlignment alignment)
    {
        return new ConfigurableSimdRuntime(new SimdCapabilities(mask, arch, level, width, alignment));
    }

    /// <summary>Probes the host once: generic flags plus the architecture probers.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static SimdCapabilities ProbeSystemCapabilities()
    {
        SimdArchitecture arch = TopologyClassifier.DetectArchitecture();
        FeatureBitmask256 mask = GenericVectorProber.Probe();

        if (arch is SimdArchitecture.X86 or SimdArchitecture.X64)
        {
            mask |= X8664Prober.Probe();
        }
        else if (arch is SimdArchitecture.Arm64)
        {
            mask |= Arm64Prober.Probe();
        }
        else if (arch is SimdArchitecture.Arm)
        {
            mask |= Arm32Prober.Probe();
        }
        else if (arch is SimdArchitecture.Wasm)
        {
            mask |= WasmProber.Probe();
        }
        else if (arch is SimdArchitecture.RiscV64)
        {
            mask |= RiscV64Prober.Probe();
        }
        else if (arch is SimdArchitecture.LoongArch64)
        {
            mask |= LoongArch64Prober.Probe();
        }

        SimdIsaLevel level = TopologyClassifier.ClassifyLevel(arch, mask);
        SimdRegisterWidth width = TopologyClassifier.ResolveMaxRegisterWidth(mask);
        SimdAlignment alignment = TopologyClassifier.ResolveAlignment(width);

        return new SimdCapabilities(mask, arch, level, width, alignment);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void RecordDispatch(SimdDispatchTier tier)
    {
        ISimdRuntime? custom = Volatile.Read(ref s_customInstance);
        if (custom != null)
        {
            custom.RecordDispatch(tier);
        }
        else
        {
            s_hardwareTelemetry.Increment(tier);
        }
    }

    private sealed class DefaultRuntime : ISimdRuntime
    {
        public static readonly DefaultRuntime Instance = new();

        public ref readonly SimdCapabilities Capabilities => ref s_hardwareCapabilities;
        public ref readonly SimdTelemetryCounters Telemetry => ref s_hardwareTelemetry;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SimdTelemetrySnapshot GetTelemetrySnapshot() => s_hardwareTelemetry.CreateSnapshot();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSupported(SimdFeature feature) => s_hardwareCapabilities.HasFeature(feature);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsPolicySatisfied<TPolicy>() where TPolicy : ISimdPolicy => s_hardwareCapabilities.SatisfiesPolicy<TPolicy>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordDispatch(SimdDispatchTier tier) => s_hardwareTelemetry.Increment(tier);
    }
}
