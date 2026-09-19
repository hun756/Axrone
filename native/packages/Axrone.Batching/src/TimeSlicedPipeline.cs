namespace Axrone.Batching;

/// <summary>
/// Frame-budgeted execution over a <see cref="TripleBuffer{T}"/> with adaptive stride.
/// </summary>
/// <typeparam name="T">Element type.</typeparam>
/// <remarks>
/// <para>
/// Each chunk costs one clock read, reused for calibration, expiry and the next chunk's budget
/// estimate — three <see cref="Stopwatch.GetTimestamp"/> calls per chunk in the monolith became one.
/// Before each chunk the stride is clamped to what the remaining budget affords at the smoothed
/// per-item cost, so a slow kernel stops before overrunning instead of after.
/// </para>
/// <para>
/// A snapshot held across publishes goes stale; the remainder is abandoned and counted in
/// <see cref="DroppedItems"/>, because re-reading a recycled slot would tear. Copy slow consumers
/// out of the snapshot instead of holding a batch across frames.
/// </para>
/// </remarks>
public sealed partial class TimeSlicedPipeline<T> : IBatchProducer<T>, IBatchSlicer<T>, IDisposable
    where T : unmanaged
{
    /// <summary>Smallest chunk the calibrator may select.</summary>
    public const int MinStride = 16;

    /// <summary>Largest chunk the calibrator may select.</summary>
    public const int MaxStride = 1024;

    /// <summary>Stride before any sample exists.</summary>
    public const int InitialStride = 64;

    /// <summary>Target chunk cost as a fraction of the clock frequency (~125µs).</summary>
    public const int TargetSliceFrequencyDivisor = 8000;

    /// <summary>EMA weight of the history; the remainder is the new sample.</summary>
    public const double SmoothingFactor = 0.15;

    private readonly TripleBuffer<T> _buffer;
    private readonly BatchStride _strideBounds;
    private readonly BatchingTelemetry _telemetry;
    private int _disposed;
    private TripleBufferSnapshot<T> _active;
    private bool _hasActive;
    private int _cursor;
    private int _stride;
    private double _smoothedTicksPerItem;
    private long _droppedItems;

    /// <summary>Elements per buffer slot.</summary>
    public int Capacity => _buffer.Capacity;

    /// <summary>Whether an acquired batch still has unprocessed elements.</summary>
    public bool HasRemainingWork => _hasActive && _cursor < _active.Items.Length;

    /// <summary>Stale-remainder elements abandoned so far.</summary>
    public long DroppedItems => Interlocked.Read(ref _droppedItems);

    /// <summary>Creates a pipeline.</summary>
    /// <param name="capacity">Elements per buffer slot; must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not positive.</exception>
    public TimeSlicedPipeline(int capacity)
        : this(capacity, BatchStride.Default)
    {
    }

    /// <summary>Creates a pipeline with stride bounds.</summary>
    /// <param name="capacity">Elements per buffer slot; must be positive.</param>
    /// <param name="stride">Calibrator stride bounds.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not positive.</exception>
    public TimeSlicedPipeline(int capacity, BatchStride stride)
        : this(capacity, stride, "Axrone.Batching")
    {
    }

    /// <summary>Creates a pipeline with stride bounds and telemetry.</summary>
    /// <param name="capacity">Elements per buffer slot; must be positive.</param>
    /// <param name="stride">Calibrator stride bounds.</param>
    /// <param name="meterName">OpenTelemetry meter name.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not positive.</exception>
    public TimeSlicedPipeline(int capacity, BatchStride stride, string meterName)
    {
        _buffer = new TripleBuffer<T>(capacity);
        _strideBounds = stride;
        _telemetry = new BatchingTelemetry(meterName);
        _active = default;
        _hasActive = false;
        _cursor = 0;
        _stride = InitialStride;
        _smoothedTicksPerItem = 0d;
    }

    /// <summary>Appends one item to the producer slot.</summary>
    public bool TryWrite(in T item) => _buffer.TryWrite(in item);

    /// <summary>Appends items to the producer slot.</summary>
    public int WriteRange(ReadOnlySpan<T> items) => _buffer.WriteRange(items);

    /// <summary>Publishes the producer slot.</summary>
    public void SwapProducer() => _buffer.SwapProducer();

    /// <summary>
    /// Runs <paramref name="kernel"/> over the active batch within <paramref name="budget"/>.
    /// </summary>
    /// <typeparam name="TKernel">Kernel type, held by ref so the call devirtualizes.</typeparam>
    /// <param name="kernel">Batch kernel.</param>
    /// <param name="budget">Slice deadline.</param>
    /// <returns>How the pass ended and how much work remains.</returns>
    public SliceResult ExecuteSlice<TKernel>(ref TKernel kernel, in FrameBudget budget)
        where TKernel : struct, IBatchKernel<T>
    {
        if (!EnsureActive())
        {
            return SliceResult.Empty(budget.ElapsedMilliseconds);
        }

        var items = _active.Items.Span;
        var length = items.Length;
        var processed = 0;
        var timestamp = Stopwatch.GetTimestamp();

        while (_cursor < length)
        {
            if (budget.IsExpiredAt(timestamp))
            {
                break;
            }

            var chunk = _stride;
            var remaining = length - _cursor;
            if (chunk > remaining)
            {
                chunk = remaining;
            }

            if (_smoothedTicksPerItem > 0d)
            {
                var affordable = (int)(budget.RemainingTicksAt(timestamp) / _smoothedTicksPerItem);
                if (affordable <= 0)
                {
                    break;
                }

                if (chunk > affordable)
                {
                    chunk = affordable;
                }
            }

            kernel.Execute(items.Slice(_cursor, chunk));

            var now = Stopwatch.GetTimestamp();
            UpdateCalibration(now - timestamp, chunk);
            timestamp = now;

            _cursor += chunk;
            processed += chunk;
        }

        var elapsed = budget.ElapsedMilliseconds;
        _telemetry.RecordSlice(processed, elapsed);
        if (_cursor < length)
        {
            return SliceResult.BudgetExceeded(processed, length - _cursor, elapsed);
        }

        _hasActive = false;
        _cursor = 0;
        return SliceResult.Completed(processed, elapsed);
    }

    /// <summary>
    /// Runs <paramref name="kernel"/> per element over the active batch within <paramref name="budget"/>.
    /// </summary>
    /// <typeparam name="TKernel">Kernel type, held by ref so the call devirtualizes.</typeparam>
    /// <param name="kernel">Element kernel.</param>
    /// <param name="budget">Slice deadline.</param>
    /// <returns>How the pass ended and how much work remains.</returns>
    public SliceResult ExecuteElements<TKernel>(ref TKernel kernel, in FrameBudget budget)
        where TKernel : struct, IElementKernel<T>
    {
        if (!EnsureActive())
        {
            return SliceResult.Empty(budget.ElapsedMilliseconds);
        }

        var items = _active.Items.Span;
        var length = items.Length;
        var processed = 0;
        var timestamp = Stopwatch.GetTimestamp();

        while (_cursor < length)
        {
            if (budget.IsExpiredAt(timestamp))
            {
                break;
            }

            var chunk = _stride;
            var remaining = length - _cursor;
            if (chunk > remaining)
            {
                chunk = remaining;
            }

            if (_smoothedTicksPerItem > 0d)
            {
                var affordable = (int)(budget.RemainingTicksAt(timestamp) / _smoothedTicksPerItem);
                if (affordable <= 0)
                {
                    break;
                }

                if (chunk > affordable)
                {
                    chunk = affordable;
                }
            }

            var window = items.Slice(_cursor, chunk);
            for (var i = 0; i < window.Length; i++)
            {
                kernel.Execute(ref window[i]);
            }

            var now = Stopwatch.GetTimestamp();
            UpdateCalibration(now - timestamp, chunk);
            timestamp = now;

            _cursor += chunk;
            processed += chunk;
        }

        var elementElapsed = budget.ElapsedMilliseconds;
        _telemetry.RecordSlice(processed, elementElapsed);
        if (_cursor < length)
        {
            return SliceResult.BudgetExceeded(processed, length - _cursor, elementElapsed);
        }

        _hasActive = false;
        _cursor = 0;
        return SliceResult.Completed(processed, elementElapsed);
    }

    /// <summary>Takes an atomic inspection snapshot.</summary>
    public PipelineSnapshot GetSnapshot() => new(
        Capacity,
        _hasActive ? _active.Items.Length - _cursor : 0,
        Volatile.Read(ref _stride),
        Interlocked.Read(ref _droppedItems),
        HasRemainingWork);

    /// <summary>Releases telemetry. Idempotent and race-free.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _telemetry.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    private bool EnsureActive()
    {
        if (_hasActive)
        {
            if (_buffer.IsStillValid(in _active))
            {
                return _cursor < _active.Items.Length;
            }

            var abandoned = _active.Items.Length - _cursor;
            Interlocked.Add(ref _droppedItems, abandoned);
            _telemetry.RecordDropped(abandoned);
            _hasActive = false;
            _cursor = 0;
        }

        _active = _buffer.Acquire();
        _hasActive = true;
        _cursor = 0;
        return _cursor < _active.Items.Length;
    }

    private void UpdateCalibration(long ticksTaken, int itemsProcessed)
    {
        if (itemsProcessed <= 0 || ticksTaken <= 0)
        {
            return;
        }

        var sample = (double)ticksTaken / itemsProcessed;
        _smoothedTicksPerItem = _smoothedTicksPerItem <= 0d
            ? sample
            : (_smoothedTicksPerItem * (1d - SmoothingFactor)) + (sample * SmoothingFactor);

        var targetTicks = (double)Stopwatch.Frequency / TargetSliceFrequencyDivisor;
        _stride = _strideBounds.Clamp((int)(targetTicks / _smoothedTicksPerItem));
    }
}
