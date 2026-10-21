namespace Axrone.Lighting;

/// <summary>GPU buffer view over a pinned backing array.</summary>
/// <typeparam name="T">Element type.</typeparam>
public readonly struct GpuBufferView<T>
    where T : unmanaged
{
    /// <summary>Backing buffer.</summary>
    public readonly T[] Buffer;

    /// <summary>Element stride.</summary>
    public readonly int Stride;

    /// <summary>Selected element count.</summary>
    public readonly int ElementCount;

    /// <summary>Readable length (at least one stride).</summary>
    public readonly int ValidLength;

    /// <summary>Creates a view.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GpuBufferView(T[] buffer, int stride, int elementCount)
    {
        Buffer = buffer;
        Stride = stride;
        ElementCount = elementCount;
        ValidLength = Math.Max(1, elementCount * stride);
    }

    /// <summary>View as a span.</summary>
    public ReadOnlySpan<T> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Buffer.AsSpan(0, ValidLength);
    }

    /// <summary>View as memory.</summary>
    public ReadOnlyMemory<T> Memory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Buffer.AsMemory(0, ValidLength);
    }
}

/// <summary>Frame selection statistics.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct LightingStats(
    int TotalLightCount,
    int TotalDirectionalCount,
    int TotalPointCount,
    int TotalSpotCount,
    int SelectedDirectionalCount,
    int SelectedPointCount,
    int SelectedSpotCount,
    int SelectedLocalLightCount,
    int OmittedDirectionalCount,
    int OmittedPointCount,
    int OmittedSpotCount,
    int OmittedLocalLightCount);

/// <summary>GPU-ready frame selection state (reused across resolves).</summary>
public sealed class LightingSelectionState
{
    /// <summary>Directional directions.</summary>
    public GpuBufferView<float> DirectionalDirections { get; internal set; }

    /// <summary>Directional colors.</summary>
    public GpuBufferView<float> DirectionalColors { get; internal set; }

    /// <summary>Directional ambient colors.</summary>
    public GpuBufferView<float> DirectionalAmbientColors { get; internal set; }

    /// <summary>Directional intensities.</summary>
    public GpuBufferView<float> DirectionalIntensities { get; internal set; }

    /// <summary>Point positions.</summary>
    public GpuBufferView<float> PointPositions { get; internal set; }

    /// <summary>Point colors.</summary>
    public GpuBufferView<float> PointColors { get; internal set; }

    /// <summary>Point intensities.</summary>
    public GpuBufferView<float> PointIntensities { get; internal set; }

    /// <summary>Point ranges.</summary>
    public GpuBufferView<float> PointRanges { get; internal set; }

    /// <summary>Point attenuations.</summary>
    public GpuBufferView<float> PointAttenuations { get; internal set; }

    /// <summary>Spot positions.</summary>
    public GpuBufferView<float> SpotPositions { get; internal set; }

    /// <summary>Spot directions.</summary>
    public GpuBufferView<float> SpotDirections { get; internal set; }

    /// <summary>Spot colors.</summary>
    public GpuBufferView<float> SpotColors { get; internal set; }

    /// <summary>Spot intensities.</summary>
    public GpuBufferView<float> SpotIntensities { get; internal set; }

    /// <summary>Spot ranges.</summary>
    public GpuBufferView<float> SpotRanges { get; internal set; }

    /// <summary>Spot inner cone cosines.</summary>
    public GpuBufferView<float> SpotInnerConeCosines { get; internal set; }

    /// <summary>Spot outer cone cosines.</summary>
    public GpuBufferView<float> SpotOuterConeCosines { get; internal set; }

    /// <summary>Local kinds.</summary>
    public GpuBufferView<int> LocalKinds { get; internal set; }

    /// <summary>Local positions.</summary>
    public GpuBufferView<float> LocalPositions { get; internal set; }

    /// <summary>Local directions.</summary>
    public GpuBufferView<float> LocalDirections { get; internal set; }

    /// <summary>Local colors.</summary>
    public GpuBufferView<float> LocalColors { get; internal set; }

    /// <summary>Local intensities.</summary>
    public GpuBufferView<float> LocalIntensities { get; internal set; }

    /// <summary>Local ranges.</summary>
    public GpuBufferView<float> LocalRanges { get; internal set; }

    /// <summary>Local attenuations.</summary>
    public GpuBufferView<float> LocalAttenuations { get; internal set; }

    /// <summary>Local inner cone cosines.</summary>
    public GpuBufferView<float> LocalInnerConeCosines { get; internal set; }

    /// <summary>Local outer cone cosines.</summary>
    public GpuBufferView<float> LocalOuterConeCosines { get; internal set; }

    /// <summary>Selection statistics.</summary>
    public LightingStats Stats { get; internal set; }

    /// <summary>Resolved environment.</summary>
    public LightingEnvironment Environment { get; internal set; }
}
