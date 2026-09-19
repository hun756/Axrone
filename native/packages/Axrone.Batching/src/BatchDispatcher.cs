namespace Axrone.Batching;

/// <summary>
/// Splits a batch into contiguous chunks and runs a kernel per chunk, in order.
/// </summary>
/// <remarks>
/// This is the sound half of the monolith's partition dispatcher. The parallel half took a
/// <c>delegate* unmanaged</c> scheduler that could not carry the generic kernel, so the kernel was
/// silently dropped for every multi-chunk batch; it is not ported. Parallel fan-out belongs to the
/// job system consuming this package, which owns the kernel's lifetime across threads.
/// </remarks>
public readonly struct BatchDispatcher : IEquatable<BatchDispatcher>
{
    /// <summary>Default chunk size when none is supplied.</summary>
    public const int DefaultMinChunkSize = 64;

    private readonly int _minChunkSize;

    /// <summary>Chunk size used to split batches.</summary>
    public int MinChunkSize => _minChunkSize;

    /// <summary>Creates a dispatcher.</summary>
    /// <param name="minChunkSize">Elements per chunk; must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minChunkSize"/> is not positive.</exception>
    public BatchDispatcher(int minChunkSize = DefaultMinChunkSize)
    {
        if (minChunkSize <= 0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(minChunkSize));
        }

        _minChunkSize = minChunkSize;
    }

    /// <summary>
    /// Runs <paramref name="kernel"/> over <paramref name="batch"/> in contiguous chunks.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <typeparam name="TKernel">Kernel type, held by ref so the call devirtualizes and stateful kernels keep their state.</typeparam>
    /// <param name="batch">Elements to process.</param>
    /// <param name="kernel">Kernel run once per chunk, in order.</param>
    public void Dispatch<T, TKernel>(Span<T> batch, ref TKernel kernel)
        where T : unmanaged
        where TKernel : struct, IBatchKernel<T>
    {
        if (batch.IsEmpty)
        {
            return;
        }

        if (batch.Length <= _minChunkSize)
        {
            kernel.Execute(batch);
            return;
        }

        var full = batch.Length / _minChunkSize;
        for (var i = 0; i < full; i++)
        {
            kernel.Execute(batch.Slice(i * _minChunkSize, _minChunkSize));
        }

        var remainder = batch.Length % _minChunkSize;
        if (remainder > 0)
        {
            kernel.Execute(batch.Slice(full * _minChunkSize, remainder));
        }
    }

    /// <inheritdoc />
    public bool Equals(BatchDispatcher other) => _minChunkSize == other._minChunkSize;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is BatchDispatcher other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _minChunkSize.GetHashCode();

    /// <summary>Value equality.</summary>
    public static bool operator ==(BatchDispatcher left, BatchDispatcher right) => left.Equals(right);

    /// <summary>Value inequality.</summary>
    public static bool operator !=(BatchDispatcher left, BatchDispatcher right) => !left.Equals(right);
}
