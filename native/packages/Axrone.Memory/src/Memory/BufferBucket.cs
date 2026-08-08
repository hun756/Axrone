namespace Axrone.Memory;

/// <summary>
/// Internal bucket that manages pooled buffers for a specific size class.
/// Uses a ConcurrentStack for fast lock-free pops with ConcurrentQueue as fallback.
/// Supports optional profiling for rent/return timing.
/// </summary>
/// <typeparam name="T">Element type of the pooled buffers.</typeparam>
internal sealed class BufferBucket<T>
{
    private readonly ConcurrentQueue<IMemoryOwner<T>> _buffers;
    private readonly ConcurrentStack<IMemoryOwner<T>> _fastBuffers;
    private readonly int _bufferSize;
    private readonly int _maxCount;
    private readonly bool _enableProfiling;
    private readonly bool _nonThreadSafe;
    private readonly object? _lock;
    private int _count;
    private long _totalRents;
    private long _totalReturns;
    private long _totalRentTimeTicks;
    private long _totalReturnTimeTicks;
    private int _isDisposed;

    internal BufferBucket(int bufferSize, int maxCount, bool enableProfiling, bool nonThreadSafe)
    {
        _bufferSize = bufferSize;
        _maxCount = maxCount;
        _enableProfiling = enableProfiling;
        _nonThreadSafe = nonThreadSafe;
        _count = 0;

        if (_nonThreadSafe)
        {
            _lock = new object();
            _fastBuffers = null!;
        }
        else
        {
            _lock = null;
            _fastBuffers = new ConcurrentStack<IMemoryOwner<T>>();
        }

        _buffers = new ConcurrentQueue<IMemoryOwner<T>>();
    }

    /// <summary>Gets the current number of idle buffers in this bucket.</summary>
    public int Count => Volatile.Read(ref _count);

    /// <summary>Gets the buffer size class for this bucket.</summary>
    public int BufferSize => _bufferSize;

    /// <summary>Gets the maximum number of idle buffers allowed.</summary>
    public int MaxCount => _maxCount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryTake([NotNullWhen(true)] out IMemoryOwner<T>? buffer)
    {
        if (Volatile.Read(ref _isDisposed) != 0)
        {
            buffer = null;
            return false;
        }

        long startTimestamp = 0;
        if (_enableProfiling)
            startTimestamp = Stopwatch.GetTimestamp();

        bool result;

        if (_nonThreadSafe)
        {
            lock (_lock!)
            {
                result = _buffers.TryDequeue(out buffer);
                if (result)
                    Interlocked.Decrement(ref _count);
                else
                    buffer = null;
            }
        }
        else
        {
            if (_fastBuffers.TryPop(out buffer))
            {
                Interlocked.Decrement(ref _count);
                result = true;
            }
            else if (_buffers.TryDequeue(out buffer))
            {
                Interlocked.Decrement(ref _count);
                result = true;
            }
            else
            {
                buffer = null;
                result = false;
            }
        }

        if (result && _enableProfiling)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            Interlocked.Add(ref _totalRentTimeTicks, elapsedTicks);
            Interlocked.Increment(ref _totalRents);
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryAdd(IMemoryOwner<T> buffer)
    {
        if (Volatile.Read(ref _isDisposed) != 0)
            return false;

        long startTimestamp = 0;
        if (_enableProfiling)
            startTimestamp = Stopwatch.GetTimestamp();

        bool result;

        if (_nonThreadSafe)
        {
            lock (_lock!)
            {
                if (Volatile.Read(ref _count) < _maxCount)
                {
                    _buffers.Enqueue(buffer);
                    Interlocked.Increment(ref _count);
                    result = true;
                }
                else
                {
                    result = false;
                }
            }
        }
        else
        {
            result = true;
            int currentCount;
            do
            {
                currentCount = Volatile.Read(ref _count);
                if (currentCount >= _maxCount)
                {
                    result = false;
                    break;
                }
            } while (Interlocked.CompareExchange(ref _count, currentCount + 1, currentCount) != currentCount);

            if (result)
            {
                _fastBuffers.Push(buffer);
            }
        }

        if (result && _enableProfiling)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            Interlocked.Add(ref _totalReturnTimeTicks, elapsedTicks);
            Interlocked.Increment(ref _totalReturns);
        }

        return result;
    }

    internal void Trim(float percentage)
    {
        int currentCount = Volatile.Read(ref _count);
        int targetRemoveCount = (int)(currentCount * percentage);
        if (targetRemoveCount <= 0)
            return;

        int removed = 0;

        if (_nonThreadSafe)
        {
            lock (_lock!)
            {
                for (int i = 0; i < targetRemoveCount && _buffers.TryDequeue(out _); i++)
                {
                    Interlocked.Decrement(ref _count);
                    removed++;
                }
            }
        }
        else
        {
            for (int i = 0; i < targetRemoveCount && _fastBuffers.TryPop(out _); i++)
            {
                Interlocked.Decrement(ref _count);
                removed++;
            }

            for (int i = removed; i < targetRemoveCount && _buffers.TryDequeue(out _); i++)
            {
                Interlocked.Decrement(ref _count);
                removed++;
            }
        }
    }

    internal BucketDiagnostics GetDiagnostics()
    {
        int count = Volatile.Read(ref _count);
        long totalRents = Interlocked.Read(ref _totalRents);
        long totalReturns = Interlocked.Read(ref _totalReturns);
        long totalRentTimeTicks = Interlocked.Read(ref _totalRentTimeTicks);
        long totalReturnTimeTicks = Interlocked.Read(ref _totalReturnTimeTicks);

        double nsPerTick = 1_000_000_000.0 / Stopwatch.Frequency;

        return new BucketDiagnostics(
            bufferSize: _bufferSize,
            currentCount: count,
            maxCount: _maxCount,
            totalRents: totalRents,
            totalReturns: totalReturns,
            totalMemory: _bufferSize * (long)Unsafe.SizeOf<T>() * count,
            averageRentTimeNs: totalRents > 0 ? (totalRentTimeTicks * nsPerTick) / totalRents : 0,
            averageReturnTimeNs: totalReturns > 0 ? (totalReturnTimeTicks * nsPerTick) / totalReturns : 0);
    }

    internal void ResetStatistics()
    {
        Interlocked.Exchange(ref _totalRents, 0);
        Interlocked.Exchange(ref _totalReturns, 0);
        Interlocked.Exchange(ref _totalRentTimeTicks, 0);
        Interlocked.Exchange(ref _totalReturnTimeTicks, 0);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        Interlocked.Exchange(ref _count, 0);

        if (_nonThreadSafe)
        {
            lock (_lock!)
            {
                while (_buffers.TryDequeue(out _)) { }
            }
        }
        else
        {
            while (_fastBuffers.TryPop(out _)) { }
            while (_buffers.TryDequeue(out _)) { }
        }
    }
}
