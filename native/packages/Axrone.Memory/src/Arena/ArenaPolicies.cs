namespace Axrone.Memory.Arena;

/// <summary>Computes the next chunk size on growth.</summary>
public interface IArenaGrowthPolicy
{
    /// <summary>Returns the next capacity honoring the floor and the ceiling.</summary>
    static abstract ByteSize ComputeNextSize(ByteSize currentCapacity, ByteSize minimumRequired, ByteSize maxCapacity);
}

/// <summary>Waits between contended compare-exchange retries.</summary>
public interface IBackoffPolicy
{
    /// <summary>Backs off once and advances <paramref name="spinCount"/>.</summary>
    static abstract void Backoff(ref int spinCount);
}

/// <summary>Observability sink for chunk lifetime events.</summary>
public interface IArenaMetricsSink
{
    /// <summary>A chunk was committed.</summary>
    static abstract void OnChunkAllocated(ByteSize capacity);

    /// <summary>A chunk was released.</summary>
    static abstract void OnChunkFreed(ByteSize capacity);

    /// <summary>An allocation could not be satisfied.</summary>
    static abstract void OnAllocationFailed(ByteSize requestedBytes);

    /// <summary>The arena was reset; the argument is the reclaimed total.</summary>
    static abstract void OnReset(ByteSize totalAllocated);
}

/// <summary>Doubles the chunk size, clamped to the floor and the ceiling.</summary>
public readonly struct GeometricGrowthPolicy : IArenaGrowthPolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ByteSize ComputeNextSize(ByteSize currentCapacity, ByteSize minimumRequired, ByteSize maxCapacity)
    {
        nuint curr = currentCapacity.Value;
        nuint next = curr << 1;
        if (next < curr)
        {
            next = maxCapacity.Value;
        }

        if (next < minimumRequired.Value)
        {
            next = minimumRequired.Value;
        }

        nuint max = maxCapacity.Value;
        nuint minReq = minimumRequired.Value;
        nuint result = next > max ? (minReq > max ? minReq : max) : next;
        return new ByteSize(result);
    }
}

/// <summary>Grows the chunk size by half, clamped to the floor and the ceiling.</summary>
public readonly struct ExponentialGrowthPolicy : IArenaGrowthPolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ByteSize ComputeNextSize(ByteSize currentCapacity, ByteSize minimumRequired, ByteSize maxCapacity)
    {
        nuint curr = currentCapacity.Value;
        nuint next = curr + (curr >> 1);
        if (next < curr)
        {
            next = maxCapacity.Value;
        }

        if (next < minimumRequired.Value)
        {
            next = minimumRequired.Value;
        }

        nuint max = maxCapacity.Value;
        nuint minReq = minimumRequired.Value;
        nuint result = next > max ? (minReq > max ? minReq : max) : next;
        return new ByteSize(result);
    }
}

/// <summary>Keeps the chunk size, raising only to satisfy the floor.</summary>
public readonly struct FixedGrowthPolicy : IArenaGrowthPolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ByteSize ComputeNextSize(ByteSize currentCapacity, ByteSize minimumRequired, ByteSize maxCapacity)
    {
        nuint min = minimumRequired.Value;
        nuint curr = currentCapacity.Value;
        return new ByteSize(min > curr ? min : curr);
    }
}

/// <summary>Exponential spin for the first ten rounds, then yields.</summary>
public readonly struct AdaptiveSpinBackoff : IBackoffPolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Backoff(ref int spinCount)
    {
        int count = spinCount;
        if ((uint)count < 10)
        {
            Thread.SpinWait(1 << count);
        }
        else
        {
            Thread.Yield();
        }
        spinCount = count + 1;
    }
}

/// <summary>Always yields; for low-contention paths.</summary>
public readonly struct YieldBackoff : IBackoffPolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Backoff(ref int spinCount)
    {
        Thread.Yield();
        spinCount++;
    }
}

/// <summary>Metrics sink that the JIT eliminates.</summary>
public readonly struct NullMetricsSink : IArenaMetricsSink
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void OnChunkAllocated(ByteSize capacity) { }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void OnChunkFreed(ByteSize capacity) { }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void OnAllocationFailed(ByteSize requestedBytes) { }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void OnReset(ByteSize totalAllocated) { }
}
