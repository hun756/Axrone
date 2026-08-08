namespace Axrone.Memory;

/// <summary>
/// A memory owner that supports pinning for native interop, exposing the raw pointer.
/// </summary>
/// <typeparam name="T">Element type of the buffer.</typeparam>
public interface IPinnedMemoryOwner<T> : IMemoryOwner<T>
{
    /// <summary>Gets the raw pointer to the pinned memory.</summary>
    IntPtr Pointer { get; }

    /// <summary>Gets whether the memory is currently pinned.</summary>
    bool IsPinned { get; }
}
