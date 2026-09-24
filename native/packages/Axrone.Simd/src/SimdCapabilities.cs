namespace Axrone.Simd;

using System.Collections.Generic;

/// <summary>Probed platform capabilities: feature mask plus topology.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly struct SimdCapabilities : IFeatureQuery, ITopologyDescriptor, IEquatable<SimdCapabilities>
{
    private readonly FeatureBitmask256 _mask;
    private readonly SimdAlignment _alignment;
    private readonly SimdRegisterWidth _maxRegisterWidth;
    private readonly SimdIsaLevel _isaLevel;
    private readonly SimdArchitecture _architecture;

    /// <summary>Creates a capability snapshot.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public SimdCapabilities(
        FeatureBitmask256 mask,
        SimdArchitecture architecture,
        SimdIsaLevel isaLevel,
        SimdRegisterWidth maxRegisterWidth,
        SimdAlignment alignment)
    {
        _mask = mask;
        _architecture = architecture;
        _isaLevel = isaLevel;
        _maxRegisterWidth = maxRegisterWidth;
        _alignment = alignment;
    }

    /// <summary>Feature mask, returned by copy (32 bytes, register-friendly).</summary>
    public FeatureBitmask256 Mask
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => _mask;
    }

    /// <inheritdoc/>
    public SimdArchitecture Architecture
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => _architecture;
    }

    /// <inheritdoc/>
    public SimdIsaLevel IsaLevel
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => _isaLevel;
    }

    /// <inheritdoc/>
    public SimdRegisterWidth MaxRegisterWidth
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => _maxRegisterWidth;
    }

    /// <inheritdoc/>
    public SimdAlignment PreferredAlignment
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => _alignment;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool HasFeature(SimdFeature feature) => _mask.Contains(feature);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Satisfies(FeatureBitmask256 mask) => _mask.ContainsAll(mask);

    /// <summary>Tests a dispatch policy: features plus minimum width.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool SatisfiesPolicy<TPolicy>() where TPolicy : ISimdPolicy
    {
        return _mask.ContainsAll(TPolicy.RequiredFeatures) && (_maxRegisterWidth >= TPolicy.MinimumWidth);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool HasAll(params ReadOnlySpan<SimdFeature> features)
    {
        ref SimdFeature ptr = ref MemoryMarshal.GetReference(features);
        nuint len = (nuint)features.Length;
        ulong p0 = 0, p1 = 0, p2 = 0, p3 = 0;

        for (nuint i = 0; i < len; i++)
        {
            byte index = (byte)Unsafe.Add(ref ptr, i);
            nuint partition = (nuint)(index >>> 6);
            ulong bit = 1UL << (index & 63);

            switch (partition)
            {
                case 0: p0 |= bit; break;
                case 1: p1 |= bit; break;
                case 2: p2 |= bit; break;
                case 3: p3 |= bit; break;
            }
        }

        return ((_mask.Partition0 & p0) == p0) &&
               ((_mask.Partition1 & p1) == p1) &&
               ((_mask.Partition2 & p2) == p2) &&
               ((_mask.Partition3 & p3) == p3);
    }

    /// <summary>Enumerates set features with zero-allocating bit manipulation.</summary>
    public IEnumerable<SimdFeature> EnumerateFeatures()
    {
        ulong p0 = _mask.Partition0;
        while (p0 != 0)
        {
            int bit = BitOperations.TrailingZeroCount(p0);
            yield return (SimdFeature)(byte)bit;
            p0 &= p0 - 1;
        }

        ulong p1 = _mask.Partition1;
        while (p1 != 0)
        {
            int bit = BitOperations.TrailingZeroCount(p1);
            yield return (SimdFeature)(byte)(64 + bit);
            p1 &= p1 - 1;
        }

        ulong p2 = _mask.Partition2;
        while (p2 != 0)
        {
            int bit = BitOperations.TrailingZeroCount(p2);
            yield return (SimdFeature)(byte)(128 + bit);
            p2 &= p2 - 1;
        }

        ulong p3 = _mask.Partition3;
        while (p3 != 0)
        {
            int bit = BitOperations.TrailingZeroCount(p3);
            yield return (SimdFeature)(byte)(192 + bit);
            p3 &= p3 - 1;
        }
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"{_architecture} [{_isaLevel}] (Width: {_maxRegisterWidth.BitWidth}-bit, Align: {_alignment.BoundaryBytes}B, Active: {_mask.PopCount()})";

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Equals(SimdCapabilities other)
    {
        return _mask.Equals(other._mask) &&
               (_architecture == other._architecture) &&
               (_isaLevel == other._isaLevel) &&
               (_maxRegisterWidth == other._maxRegisterWidth) &&
               (_alignment == other._alignment);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SimdCapabilities cap && Equals(cap);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = default;
        hash.Add(_mask);
        hash.Add((byte)_architecture);
        hash.Add((byte)_isaLevel);
        hash.Add(_maxRegisterWidth.BitWidth);
        hash.Add(_alignment.BoundaryBytes);
        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    public static bool operator ==(SimdCapabilities left, SimdCapabilities right) => left.Equals(right);

    /// <inheritdoc/>
    public static bool operator !=(SimdCapabilities left, SimdCapabilities right) => !left.Equals(right);
}

/// <summary>Point-in-time dispatch counters.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct SimdTelemetrySnapshot(
    long Avx512Dispatches,
    long Avx2Dispatches,
    long Vector128Dispatches,
    long ScalarDispatches)
{
    /// <summary>All dispatches.</summary>
    public long TotalDispatches => Avx512Dispatches + Avx2Dispatches + Vector128Dispatches + ScalarDispatches;

    /// <inheritdoc/>
    public override string ToString() =>
        $"AVX-512: {Avx512Dispatches}, AVX2: {Avx2Dispatches}, Vector128: {Vector128Dispatches}, Scalar: {ScalarDispatches} (Total: {TotalDispatches})";
}

/// <summary>
/// Dispatch counters with cache-line separation: wide-vector counters live on one line,
/// base/scalar counters on another, so heterogeneous multithreaded workloads never share.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 128)]
public struct SimdTelemetryCounters
{
    [FieldOffset(0)]
    private long _avx512Dispatches;

    [FieldOffset(8)]
    private long _avx2Dispatches;

    [FieldOffset(64)]
    private long _vector128Dispatches;

    [FieldOffset(72)]
    private long _scalarDispatches;

    /// <summary>Records one dispatch on the tier.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Increment(SimdDispatchTier tier)
    {
        switch (tier)
        {
            case SimdDispatchTier.Avx512:
                Interlocked.Increment(ref _avx512Dispatches);
                break;
            case SimdDispatchTier.Avx2:
                Interlocked.Increment(ref _avx2Dispatches);
                break;
            case SimdDispatchTier.Vector128:
                Interlocked.Increment(ref _vector128Dispatches);
                break;
            case SimdDispatchTier.Scalar:
                Interlocked.Increment(ref _scalarDispatches);
                break;
        }
    }

    /// <summary>Records one AVX-512 dispatch.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void IncrementAvx512() => Interlocked.Increment(ref _avx512Dispatches);

    /// <summary>Records one AVX2 dispatch.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void IncrementAvx2() => Interlocked.Increment(ref _avx2Dispatches);

    /// <summary>Records one 128-bit dispatch.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void IncrementVector128() => Interlocked.Increment(ref _vector128Dispatches);

    /// <summary>Records one scalar dispatch.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void IncrementScalar() => Interlocked.Increment(ref _scalarDispatches);

    /// <summary>AVX-512 dispatches.</summary>
    public long Avx512Dispatches
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _avx512Dispatches);
    }

    /// <summary>AVX2 dispatches.</summary>
    public long Avx2Dispatches
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _avx2Dispatches);
    }

    /// <summary>128-bit dispatches.</summary>
    public long Vector128Dispatches
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _vector128Dispatches);
    }

    /// <summary>Scalar dispatches.</summary>
    public long ScalarDispatches
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _scalarDispatches);
    }

    /// <summary>Captures a snapshot.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SimdTelemetrySnapshot CreateSnapshot() =>
        new(Avx512Dispatches, Avx2Dispatches, Vector128Dispatches, ScalarDispatches);
}
