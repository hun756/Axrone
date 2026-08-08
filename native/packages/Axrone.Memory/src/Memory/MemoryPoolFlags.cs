namespace Axrone.Memory;

/// <summary>
/// Feature toggle flags for <see cref="MemoryPool{T}"/> configuration.
/// </summary>
[Flags]
public enum MemoryPoolFlags : int
{
    /// <summary>No flags enabled.</summary>
    None = 0,

    /// <summary>Enable statistics tracking (hits, misses, allocations).</summary>
    EnableStatistics = 1 << 0,

    /// <summary>Enable profiling (per-operation timing).</summary>
    EnableProfiling = 1 << 1,

    /// <summary>Always zero memory on allocation.</summary>
    ZeroMemory = 1 << 2,

    /// <summary>Use thread-local caching for faster per-thread access.</summary>
    UseTlsCache = 1 << 3,

    /// <summary>Enable memory compression for large buffers.</summary>
    UseCompression = 1 << 4,

    /// <summary>Allocate non-movable buffers (pinned by default).</summary>
    AllocateNonMovable = 1 << 5,

    /// <summary>Prevent buffer fragmentation via size alignment.</summary>
    PreventFragmentation = 1 << 6,

    /// <summary>Enable batch rent/return operations.</summary>
    EnableBatchOperations = 1 << 7,

    /// <summary>Pin all buffers for native interop.</summary>
    PinBuffers = 1 << 8,

    /// <summary>Optimize buffer layout for CPU cache lines.</summary>
    OptimizeForCpuCache = 1 << 9,

    /// <summary>Enable memory guard padding for buffer overflow detection.</summary>
    EnableMemoryGuard = 1 << 10,

    /// <summary>Enable thread-safe operations (disable for single-threaded scenarios).</summary>
    EnableThreadSafety = 1 << 11,
}
