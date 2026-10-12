namespace Axrone.Geometry;

using System.Buffers;

/// <summary>
/// Tallies one culling pass: how many items ran, how many survived, and the
/// classification split behind the survivors.
/// </summary>
/// <param name="TotalCount">Items examined.</param>
/// <param name="VisibleCount">Items kept (inside or intersecting).</param>
/// <param name="OutsideCount">Items rejected.</param>
/// <param name="InsideCount">Items fully inside.</param>
/// <param name="IntersectCount">Items touching the boundary.</param>
public readonly record struct CullerStats(
    int TotalCount,
    int VisibleCount,
    int OutsideCount,
    int InsideCount,
    int IntersectCount);

/// <summary>Batching knobs of an asynchronous culling pass.</summary>
/// <param name="BatchSize">Items per slice between yields.</param>
/// <param name="CancellationToken">Token abandoning the pass.</param>
public readonly record struct CullingExecutionOptions(
    int BatchSize = 1024,
    CancellationToken CancellationToken = default);

/// <summary>
/// Records what a culling pass did. Instance-based (unlike the static tree
/// sinks) because the culler owns its sink as a field.
/// </summary>
public interface ICullingMetricsSink
{
    /// <summary>Records one finished pass and its split.</summary>
    void RecordBatch(int totalProcessed, int visibleCount, int outsideCount, int insideCount, int intersectCount);
}

/// <summary>Discards every pass tally, which is what production builds want.</summary>
public readonly struct NullCullingMetricsSink : ICullingMetricsSink
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordBatch(int totalProcessed, int visibleCount, int outsideCount, int insideCount, int intersectCount)
    {
    }
}

/// <summary>
/// Batch frustum culler: classifies a span of items through a policy, keeps
/// the survivors in a pooled buffer, and reports the split. Synchronous
/// passes run under one lock; asynchronous passes slice the work with yields
/// between batches.
/// </summary>
/// <typeparam name="TItem">Item type being culled.</typeparam>
/// <typeparam name="TBounds">Bound shape extracted per item.</typeparam>
/// <typeparam name="TPolicy">Classification policy.</typeparam>
/// <typeparam name="TMetrics">Pass instrumentation sink.</typeparam>
public sealed class FrustumCuller<TItem, TBounds, TPolicy, TMetrics> : IDisposable
    where TBounds : struct
    where TPolicy : struct, IClassificationPolicy<TBounds>
    where TMetrics : struct, ICullingMetricsSink
{
    /// <summary>Extracts the bound shape of an item.</summary>
    /// <param name="item">The item to measure.</param>
    public delegate TBounds BoundsAccessor(in TItem item);

    private readonly BoundsAccessor _boundsAccessor;
    private TMetrics _metrics;
    private TItem[] _visibleBuffer;
    private int _visibleCount;
    private CullerStats _stats;
    private readonly Lock _syncRoot = new();

    /// <summary>Survivors of the last pass.</summary>
    public ReadOnlySpan<TItem> VisibleSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _visibleBuffer.AsSpan(0, _visibleCount);
    }

    /// <summary>Survivors of the last pass.</summary>
    public int VisibleCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _visibleCount;
    }

    /// <summary>Tally of the last pass.</summary>
    public CullerStats Stats
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _stats;
    }

    /// <summary>Creates a culler over <paramref name="boundsAccessor"/> with a pooled survivor buffer.</summary>
    public FrustumCuller(BoundsAccessor boundsAccessor, int initialCapacity = 256, TMetrics metrics = default)
    {
        ArgumentNullException.ThrowIfNull(boundsAccessor);
        _boundsAccessor = boundsAccessor;
        _metrics = metrics;
        int capacity = Math.Max(initialCapacity, 16);
        _visibleBuffer = ArrayPool<TItem>.Shared.Rent(capacity);
        _visibleCount = 0;
        _stats = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureBufferCapacity(int required)
    {
        if ((uint)required > (uint)_visibleBuffer.Length)
        {
            int newCap = (int)Math.Max((uint)_visibleBuffer.Length * 2u, (uint)required);
            TItem[] newBuffer = ArrayPool<TItem>.Shared.Rent(newCap);
            _visibleBuffer.AsSpan(0, _visibleCount).CopyTo(newBuffer);
            ArrayPool<TItem>.Shared.Return(_visibleBuffer, RuntimeHelpers.IsReferenceOrContainsReferences<TItem>());
            _visibleBuffer = newBuffer;
        }
    }

    /// <summary>Clears the survivors and the tally without releasing the buffer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<TItem>())
        {
            _visibleBuffer.AsSpan(0, _visibleCount).Clear();
        }

        _visibleCount = 0;
        _stats = default;
    }

    /// <summary>Classifies <paramref name="items"/> and keeps the survivors.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Cull(ReadOnlySpan<TItem> items, in CameraFrustum frustum)
    {
        lock (_syncRoot)
        {
            Reset();
            EnsureBufferCapacity(items.Length);

            int visibleIndex = 0;
            int outsideCount = 0;
            int insideCount = 0;
            int intersectCount = 0;

            ref TItem dstRef = ref MemoryMarshal.GetArrayDataReference(_visibleBuffer);
            ref readonly TItem srcRef = ref MemoryMarshal.GetReference(items);

            for (nuint i = 0; i < (nuint)items.Length; i++)
            {
                ref readonly TItem item = ref Unsafe.Add(ref Unsafe.AsRef(in srcRef), i);
                TBounds bounds = _boundsAccessor(in item);
                ContainmentType classification = TPolicy.Classify(in frustum, in bounds);

                switch (classification)
                {
                    case ContainmentType.Contains:
                        insideCount++;
                        Unsafe.Add(ref dstRef, (nuint)visibleIndex++) = item;
                        break;
                    case ContainmentType.Intersects:
                        intersectCount++;
                        Unsafe.Add(ref dstRef, (nuint)visibleIndex++) = item;
                        break;
                    case ContainmentType.Disjoint:
                    default:
                        outsideCount++;
                        break;
                }
            }

            _visibleCount = visibleIndex;
            _stats = new CullerStats(items.Length, visibleIndex, outsideCount, insideCount, intersectCount);
            _metrics.RecordBatch(items.Length, visibleIndex, outsideCount, insideCount, intersectCount);
        }
    }

    /// <summary>Classifies <paramref name="items"/> in slices, yielding between batches.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public async ValueTask CullAsync(
        ReadOnlyMemory<TItem> items,
        CameraFrustum frustum,
        CullingExecutionOptions options = default)
    {
        int batchSize = options.BatchSize > 0 ? options.BatchSize : 1024;
        CancellationToken cancellationToken = options.CancellationToken;

        lock (_syncRoot)
        {
            Reset();
            EnsureBufferCapacity(items.Length);
        }

        int totalCount = items.Length;
        int visibleIndex = 0;
        int outsideCount = 0;
        int insideCount = 0;
        int intersectCount = 0;
        int processed = 0;

        while (processed < totalCount)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int sliceSize = Math.Min(batchSize, totalCount - processed);
            ReadOnlySpan<TItem> spanSlice = items.Span.Slice(processed, sliceSize);

            ref readonly TItem srcRef = ref MemoryMarshal.GetReference(spanSlice);
            lock (_syncRoot)
            {
                ref TItem dstRef = ref MemoryMarshal.GetArrayDataReference(_visibleBuffer);

                for (nuint i = 0; i < (nuint)spanSlice.Length; i++)
                {
                    ref readonly TItem item = ref Unsafe.Add(ref Unsafe.AsRef(in srcRef), i);
                    TBounds bounds = _boundsAccessor(in item);
                    ContainmentType classification = TPolicy.Classify(in frustum, in bounds);

                    switch (classification)
                    {
                        case ContainmentType.Contains:
                            insideCount++;
                            Unsafe.Add(ref dstRef, (nuint)visibleIndex++) = item;
                            break;
                        case ContainmentType.Intersects:
                            intersectCount++;
                            Unsafe.Add(ref dstRef, (nuint)visibleIndex++) = item;
                            break;
                        case ContainmentType.Disjoint:
                        default:
                            outsideCount++;
                            break;
                    }
                }
            }

            processed += sliceSize;

            if (processed < totalCount)
            {
                await Task.Yield();
            }
        }

        lock (_syncRoot)
        {
            _visibleCount = visibleIndex;
            _stats = new CullerStats(totalCount, visibleIndex, outsideCount, insideCount, intersectCount);
            _metrics.RecordBatch(totalCount, visibleIndex, outsideCount, insideCount, intersectCount);
        }
    }

    /// <summary>Exposes the survivors as an allocation-free view.</summary>
    public CullerVisibleView<TItem, TBounds, TPolicy, TMetrics> VisibleView
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(this);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_visibleBuffer.Length > 0)
            {
                Reset();
                ArrayPool<TItem>.Shared.Return(_visibleBuffer, RuntimeHelpers.IsReferenceOrContainsReferences<TItem>());
                _visibleBuffer = [];
            }
        }
    }
}

/// <summary>Allocation-free view over the survivors of the last culling pass.</summary>
/// <typeparam name="TItem">Item type being culled.</typeparam>
/// <typeparam name="TBounds">Bound shape extracted per item.</typeparam>
/// <typeparam name="TPolicy">Classification policy.</typeparam>
/// <typeparam name="TMetrics">Pass instrumentation sink.</typeparam>
public readonly ref struct CullerVisibleView<TItem, TBounds, TPolicy, TMetrics>
    where TBounds : struct
    where TPolicy : struct, IClassificationPolicy<TBounds>
    where TMetrics : struct, ICullingMetricsSink
{
    private readonly ReadOnlySpan<TItem> _span;

    /// <summary>Creates a view; prefer <c>VisibleView</c> on the culler.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CullerVisibleView(FrustumCuller<TItem, TBounds, TPolicy, TMetrics> culler)
    {
        _span = culler.VisibleSpan;
    }

    /// <summary>Survivors in view.</summary>
    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _span.Length;
    }

    /// <summary>Survivor at <paramref name="index"/>.</summary>
    public ref readonly TItem this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _span[index];
    }

    /// <summary>Enumerates the survivors.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<TItem>.Enumerator GetEnumerator() => _span.GetEnumerator();
}

/// <summary>Entry points over the batch culler with default metrics.</summary>
public static class FrustumCullerFactory
{
    /// <summary>Creates a culler dispatching through the volume contract.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FrustumCuller<TItem, TBounds, DirectVolumePolicy<TBounds>, NullCullingMetricsSink> Create<TItem, TBounds>(
        FrustumCuller<TItem, TBounds, DirectVolumePolicy<TBounds>, NullCullingMetricsSink>.BoundsAccessor accessor,
        int initialCapacity = 256)
        where TBounds : struct, IBoundingVolume<TBounds> =>
        new(accessor, initialCapacity, default);

    /// <summary>Creates a culler over sphere bounds.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FrustumCuller<TItem, BoundingSphere3, SphereVolumePolicy, NullCullingMetricsSink> CreateForSpheres<TItem>(
        FrustumCuller<TItem, BoundingSphere3, SphereVolumePolicy, NullCullingMetricsSink>.BoundsAccessor accessor,
        int initialCapacity = 256) =>
        new(accessor, initialCapacity, default);

    /// <summary>Creates a culler over box bounds.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FrustumCuller<TItem, Aabb3D, AabbVolumePolicy, NullCullingMetricsSink> CreateForAabbs<TItem>(
        FrustumCuller<TItem, Aabb3D, AabbVolumePolicy, NullCullingMetricsSink>.BoundsAccessor accessor,
        int initialCapacity = 256) =>
        new(accessor, initialCapacity, default);
}
