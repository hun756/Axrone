namespace Axrone.Memory;

/// <summary>
/// Represents a rented memory buffer with deterministic disposal, pinning, and slicing.
/// </summary>
/// <typeparam name="T">Element type of the buffer.</typeparam>
public interface IMemoryOwner<T> : IDisposable
{
    /// <summary>Gets the rented memory region.</summary>
    Memory<T> Memory { get; }

    /// <summary>Gets a span over the rented memory region.</summary>
    Span<T> Span { get; }

    /// <summary>Gets a read-only view of the rented memory.</summary>
    ReadOnlyMemory<T> ReadOnlyMemory { get; }

    /// <summary>Gets a read-only span over the rented memory region.</summary>
    ReadOnlySpan<T> ReadOnlySpan { get; }

    /// <summary>Gets the element count of the rented buffer.</summary>
    int Length { get; }

    /// <summary>Gets the current reference count (1 for the owner, +1 per active slice).</summary>
    int ReferenceCount { get; }

    /// <summary>Creates a sliced view that shares the underlying buffer.</summary>
    /// <param name="start">Start offset within the buffer.</param>
    /// <param name="length">Number of elements in the slice.</param>
    /// <returns>A new owner representing the sliced view.</returns>
    IMemoryOwner<T> Slice(int start, int length);

    /// <summary>Pins the underlying memory and returns a <see cref="MemoryHandle"/>.</summary>
    /// <param name="elementIndex">The element index to pin at.</param>
    /// <returns>A handle that must be disposed to unpin.</returns>
    MemoryHandle Pin(int elementIndex = 0);

    /// <summary>Tries to get the underlying array segment.</summary>
    /// <param name="segment">When this method returns <c>true</c>, the array segment; otherwise a default segment.</param>
    /// <returns><c>true</c> if the buffer is backed by an array; otherwise <c>false</c>.</returns>
    bool TryGetArray(out ArraySegment<T> segment);
}
