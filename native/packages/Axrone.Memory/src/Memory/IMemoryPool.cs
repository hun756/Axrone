namespace Axrone.Memory;

/// <summary>
/// Contract for a generic memory pool with rent/return, metrics, diagnostics, and trim operations.
/// </summary>
/// <typeparam name="T">Element type of the pooled buffers.</typeparam>
public interface IMemoryPool<T> : IDisposable
{
    /// <summary>Rents a buffer with at least <paramref name="minimumLength"/> elements.</summary>
    /// <param name="minimumLength">Minimum number of elements. Use -1 for the default buffer size.</param>
    /// <returns>An <see cref="IMemoryOwner{T}"/> wrapping the rented buffer.</returns>
    IMemoryOwner<T> Rent(int minimumLength = -1);

    /// <summary>Tries to rent a buffer with at least <paramref name="minimumLength"/> elements.</summary>
    /// <param name="minimumLength">Minimum number of elements.</param>
    /// <param name="memoryOwner">When this method returns <c>true</c>, the rented buffer owner; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> if a buffer was successfully rented; otherwise <c>false</c>.</returns>
    bool TryRent(int minimumLength, [NotNullWhen(true)] out IMemoryOwner<T>? memoryOwner);

    /// <summary>Tries to rent a buffer with exactly <paramref name="exactLength"/> elements.</summary>
    /// <param name="exactLength">Exact number of elements required.</param>
    /// <param name="memoryOwner">When this method returns <c>true</c>, the rented buffer owner; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> if a buffer was successfully rented; otherwise <c>false</c>.</returns>
    bool TryRentExact(int exactLength, [NotNullWhen(true)] out IMemoryOwner<T>? memoryOwner);

    /// <summary>Rents multiple buffers at once.</summary>
    /// <param name="count">Number of buffers to rent.</param>
    /// <param name="minimumLength">Minimum number of elements per buffer.</param>
    /// <returns>An array of rented buffer owners.</returns>
    IMemoryOwner<T>[] RentMultiple(int count, int minimumLength = -1);

    /// <summary>Returns multiple buffers to the pool at once.</summary>
    /// <param name="buffers">The buffers to return.</param>
    /// <returns><c>true</c> if all buffers were successfully returned; otherwise <c>false</c>.</returns>
    bool ReturnMultiple(IMemoryOwner<T>[] buffers);

    /// <summary>Gets the maximum buffer size this pool will allocate.</summary>
    int MaxBufferSize { get; }

    /// <summary>Gets the total number of bytes currently allocated by the pool.</summary>
    long TotalAllocated { get; }

    /// <summary>Gets a snapshot of the pool's performance metrics.</summary>
    /// <returns>A <see cref="MemoryPoolMetrics"/> snapshot.</returns>
    MemoryPoolMetrics GetMetrics();

    /// <summary>Gets a comprehensive diagnostic snapshot of the pool's internal state.</summary>
    /// <returns>A <see cref="MemoryPoolDiagnostics"/> snapshot.</returns>
    MemoryPoolDiagnostics GetDiagnostics();

    /// <summary>Removes a fraction of idle buffers from the pool.</summary>
    /// <param name="pressure">Fraction to remove: 0.0 removes none, 1.0 removes all.</param>
    void Trim(double pressure = 0.5);

    /// <summary>Resets all statistics counters to zero.</summary>
    void ResetStatistics();
}
