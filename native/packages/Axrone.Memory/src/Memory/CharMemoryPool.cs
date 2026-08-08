namespace Axrone.Memory;

/// <summary>
/// Specialized memory pool for <see cref="char"/> buffers with optimized defaults
/// and a convenience <see cref="GetWriter"/> factory.
/// </summary>
public sealed class CharMemoryPool : IMemoryPool<char>
{
    /// <summary>Shared singleton instance with char-optimized defaults.</summary>
    public static readonly CharMemoryPool Shared = new();

    private readonly MemoryPool<char> _inner;

    /// <summary>Creates a pool with char-optimized default options.</summary>
    public CharMemoryPool()
        : this(CreateDefaultOptions())
    {
    }

    /// <summary>Creates a pool with the specified options.</summary>
    public CharMemoryPool(MemoryPoolOptions<char> options)
    {
        _inner = new MemoryPool<char>(options);
    }

    /// <inheritdoc/>
    public int MaxBufferSize => _inner.MaxBufferSize;

    /// <inheritdoc/>
    public long TotalAllocated => _inner.TotalAllocated;

    /// <inheritdoc/>
    public IMemoryOwner<char> Rent(int minimumLength = -1) => _inner.Rent(minimumLength);

    /// <inheritdoc/>
    public bool TryRent(int minimumLength, [NotNullWhen(true)] out IMemoryOwner<char>? memoryOwner)
        => _inner.TryRent(minimumLength, out memoryOwner);

    /// <inheritdoc/>
    public bool TryRentExact(int exactLength, [NotNullWhen(true)] out IMemoryOwner<char>? memoryOwner)
        => _inner.TryRentExact(exactLength, out memoryOwner);

    /// <inheritdoc/>
    public IMemoryOwner<char>[] RentMultiple(int count, int minimumLength = -1)
        => _inner.RentMultiple(count, minimumLength);

    /// <inheritdoc/>
    public bool ReturnMultiple(IMemoryOwner<char>[] buffers) => _inner.ReturnMultiple(buffers);

    /// <inheritdoc/>
    public MemoryPoolMetrics GetMetrics() => _inner.GetMetrics();

    /// <inheritdoc/>
    public MemoryPoolDiagnostics GetDiagnostics() => _inner.GetDiagnostics();

    /// <inheritdoc/>
    public void Trim(double pressure = 0.5) => _inner.Trim(pressure);

    /// <inheritdoc/>
    public void ResetStatistics() => _inner.ResetStatistics();

    /// <inheritdoc/>
    public void Dispose() => _inner.Dispose();

    /// <summary>Creates a new <see cref="CharMemoryWriter"/> backed by this pool.</summary>
    /// <param name="initialCapacity">Initial buffer capacity in chars.</param>
    /// <returns>A new auto-resizing char writer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CharMemoryWriter GetWriter(int initialCapacity = 1024) => new(this, initialCapacity);

    private static MemoryPoolOptions<char> CreateDefaultOptions()
    {
        return new MemoryPoolOptions<char>()
            .WithFlags(MemoryPoolFlags.EnableThreadSafety | MemoryPoolFlags.UseTlsCache)
            .WithAllocationStrategy(AllocationStrategy.PowerOfTwo);
    }
}
