namespace Axrone.Memory;

/// <summary>
/// Specialized memory pool for <see cref="byte"/> buffers with optimized defaults
/// and a convenience <see cref="GetWriter"/> factory.
/// </summary>
public sealed class ByteMemoryPool : IMemoryPool<byte>
{
    /// <summary>Shared singleton instance with byte-optimized defaults.</summary>
    public static readonly ByteMemoryPool Shared = new();

    private readonly MemoryPool<byte> _inner;

    /// <summary>Creates a pool with byte-optimized default options.</summary>
    public ByteMemoryPool()
        : this(CreateDefaultOptions())
    {
    }

    /// <summary>Creates a pool with the specified options.</summary>
    public ByteMemoryPool(MemoryPoolOptions<byte> options)
    {
        _inner = new MemoryPool<byte>(options);
    }

    /// <inheritdoc/>
    public int MaxBufferSize => _inner.MaxBufferSize;

    /// <inheritdoc/>
    public long TotalAllocated => _inner.TotalAllocated;

    /// <inheritdoc/>
    public IMemoryOwner<byte> Rent(int minimumLength = -1) => _inner.Rent(minimumLength);

    /// <inheritdoc/>
    public bool TryRent(int minimumLength, [NotNullWhen(true)] out IMemoryOwner<byte>? memoryOwner)
        => _inner.TryRent(minimumLength, out memoryOwner);

    /// <inheritdoc/>
    public bool TryRentExact(int exactLength, [NotNullWhen(true)] out IMemoryOwner<byte>? memoryOwner)
        => _inner.TryRentExact(exactLength, out memoryOwner);

    /// <inheritdoc/>
    public IMemoryOwner<byte>[] RentMultiple(int count, int minimumLength = -1)
        => _inner.RentMultiple(count, minimumLength);

    /// <inheritdoc/>
    public bool ReturnMultiple(IMemoryOwner<byte>[] buffers) => _inner.ReturnMultiple(buffers);

    /// <inheritdoc/>
    public MemoryPoolMetrics GetMetrics() => _inner.GetMetrics();

    /// <inheritdoc/>
    public MemoryPoolDiagnostics GetDiagnostics() => _inner.GetDiagnostics();

    /// <inheritdoc/>
    public void Trim(double pressure = 0.5) => _inner.Trim(pressure);

    /// <inheritdoc/>
    public void ResetStatistics() => _inner.ResetStatistics();

    /// <inheritdoc/>
    public void Dispose()
    {
        if (ReferenceEquals(this, Shared)) return;
        _inner.Dispose();
    }

    /// <summary>Creates a new <see cref="ByteMemoryWriter"/> backed by this pool.</summary>
    /// <param name="initialCapacity">Initial buffer capacity in bytes.</param>
    /// <returns>A new auto-resizing byte writer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ByteMemoryWriter GetWriter(int initialCapacity = 4096) => new(this, initialCapacity);

    private static MemoryPoolOptions<byte> CreateDefaultOptions()
    {
        return new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.EnableThreadSafety | MemoryPoolFlags.OptimizeForCpuCache | MemoryPoolFlags.UseTlsCache)
            .WithAllocationStrategy(AllocationStrategy.PowerOfTwo);
    }
}
