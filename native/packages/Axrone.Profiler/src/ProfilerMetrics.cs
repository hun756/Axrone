namespace Axrone.Profiler;

/// <summary>Histogram statistics snapshot.</summary>
public readonly record struct HistogramStatistics(
    long Count,
    double MeanMs,
    double MinMs,
    double MaxMs,
    double P50Ms,
    double P75Ms,
    double P90Ms,
    double P95Ms,
    double P99Ms,
    double TotalDurationMs,
    System.Collections.Generic.IReadOnlyList<HistogramBucket> Buckets);

/// <summary>One log bucket: lower bound, count and mean.</summary>
public readonly record struct HistogramBucket(double LowerBoundMs, long Count, double MeanMs);

/// <summary>Logarithmic histogram with doubling bucket bounds for durations.</summary>
public sealed class LogHistogram : IDisposable
{
    private const int MaxBuckets = 256;

    private readonly double _minMs;
    private readonly double _maxMs;
    private readonly long[] _counts = new long[MaxBuckets];
    private readonly double[] _sums = new double[MaxBuckets];
    private long _totalCount;
    private double _totalSum;
    private double _minValue = double.PositiveInfinity;
    private double _maxValue = double.NegativeInfinity;
    private readonly Lock _gate = new();
    private int _disposed;

    /// <summary>Creates a histogram over [minMs, maxMs].</summary>
    public LogHistogram(double minMs = 0.0, double maxMs = 10_000.0)
    {
        if (!(minMs >= 0.0) || double.IsNaN(minMs) || double.IsInfinity(minMs))
        {
            ProfilerThrowHelper.ThrowArgument(nameof(minMs), "Minimum must be finite and non-negative.");
        }

        if (!(maxMs > minMs) || double.IsNaN(maxMs) || double.IsInfinity(maxMs))
        {
            ProfilerThrowHelper.ThrowArgument(nameof(maxMs), "Maximum must be finite and above the minimum.");
        }

        _minMs = minMs;
        _maxMs = maxMs;
    }

    /// <summary>Recorded sample count.</summary>
    public long Count => Volatile.Read(ref _totalCount);

    private static int BucketOf(double value)
    {
        if (!(value > 0.0))
        {
            return 0;
        }

        int bucket = 1 + (int)Math.Floor(Math.Log2(value));
        return Math.Clamp(bucket, 0, MaxBuckets - 1);
    }

    /// <summary>Records one duration.</summary>
    public void Record(double durationMs)
    {
        if (double.IsNaN(durationMs) || double.IsInfinity(durationMs) || durationMs < 0.0)
        {
            ProfilerThrowHelper.ThrowArgument(nameof(durationMs), "Duration must be a finite non-negative number.");
        }

        double clamped = Math.Min(durationMs, _maxMs);
        int bucket = BucketOf(Math.Max(clamped, _minMs <= 0.0 ? double.Epsilon : _minMs));

        lock (_gate)
        {
            _counts[bucket]++;
            _sums[bucket] += clamped;
            _totalCount++;
            _totalSum += clamped;
            if (clamped < _minValue)
            {
                _minValue = clamped;
            }

            if (clamped > _maxValue)
            {
                _maxValue = clamped;
            }
        }
    }

    /// <summary>Smallest recorded value; NaN when empty.</summary>
    public double MinValue()
    {
        lock (_gate)
        {
            return _totalCount == 0 ? double.NaN : _minValue;
        }
    }

    /// <summary>Largest recorded value; NaN when empty.</summary>
    public double MaxValue()
    {
        lock (_gate)
        {
            return _totalCount == 0 ? double.NaN : _maxValue;
        }
    }

    private double QuantileLocked(double quantile)
    {
        long rank = Math.Min(_totalCount - 1, (long)(quantile * _totalCount));
        long seen = 0;
        for (int i = 0; i < MaxBuckets; i++)
        {
            seen += _counts[i];
            if (seen > rank)
            {
                long inBucket = _counts[i];
                long bucketRank = rank - (seen - inBucket);
                double width = BucketWidth(i);
                double fraction = inBucket > 1 ? (double)bucketRank / (inBucket - 1) : 0.5;
                return BucketLower(i) + (width * fraction);
            }
        }

        return _maxValue;
    }

    private static double BucketLower(int bucket) => bucket == 0 ? 0.0 : Math.Pow(2.0, bucket - 1);

    private static double BucketWidth(int bucket) => bucket == 0 ? 1.0 : Math.Pow(2.0, bucket - 1);

    /// <summary>Bucket table: lower bound, count and mean per live bucket.</summary>
    public HistogramBucket[] GetBuckets()
    {
        lock (_gate)
        {
            return GetBucketsLocked();
        }
    }

    private HistogramBucket[] GetBucketsLocked()
    {
        var buckets = new System.Collections.Generic.List<HistogramBucket>();
        for (int i = 0; i < MaxBuckets; i++)
        {
            if (_counts[i] > 0)
            {
                buckets.Add(new HistogramBucket(BucketLower(i), _counts[i], _sums[i] / _counts[i]));
            }
        }

        return buckets.ToArray();
    }

    /// <summary>Computes summary statistics. Percentiles are bucket-quantized
    /// approximations, accurate within half a bucket width.</summary>
    public HistogramStatistics GetStatistics()
    {
        lock (_gate)
        {
            if (_totalCount == 0)
            {
                return new HistogramStatistics(0, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, 0.0, []);
            }

            return new HistogramStatistics(
                _totalCount,
                _totalSum / _totalCount,
                _minValue,
                _maxValue,
                QuantileLocked(0.50),
                QuantileLocked(0.75),
                QuantileLocked(0.90),
                QuantileLocked(0.95),
                QuantileLocked(0.99),
                _totalSum,
                GetBucketsLocked());
        }
    }

    /// <summary>Drops all recorded samples.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            Array.Clear(_counts);
            Array.Clear(_sums);
            _totalCount = 0;
            _totalSum = 0.0;
            _minValue = double.PositiveInfinity;
            _maxValue = double.NegativeInfinity;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            GC.SuppressFinalize(this);
        }
    }
}
