namespace Axrone.Memory;

/// <summary>
/// Zero-allocation memory pool for fixed-size buffer reuse.
/// </summary>
/// <typeparam name="T">Element type of the pooled buffers.</typeparam>
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public sealed class MemoryPool<T>
{
}
