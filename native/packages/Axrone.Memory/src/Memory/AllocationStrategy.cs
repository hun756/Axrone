namespace Axrone.Memory;

/// <summary>
/// Strategy for generating bucket sizes in the memory pool.
/// </summary>
public enum AllocationStrategy : byte
{
    /// <summary>Bucket sizes are powers of two (16, 32, 64, 128, ...). Default.</summary>
    PowerOfTwo = 0,

    /// <summary>Only min, default, and max bucket sizes are created.</summary>
    Exact = 1,

    /// <summary>Bucket sizes are multiples of a configurable chunk size.</summary>
    Chunked = 2,

    /// <summary>Bucket sizes follow the Fibonacci sequence (16, 32, 48, 80, 128, ...).</summary>
    Fibonacci = 3,
}
