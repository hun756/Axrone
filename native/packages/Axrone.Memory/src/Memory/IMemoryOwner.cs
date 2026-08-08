namespace Axrone.Memory;

/// <summary>
/// Represents a rented memory buffer with deterministic disposal.
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

    /// <summary>Gets the element count of the rented buffer.</summary>
    int Length { get; }

    /// <summary>Creates a sliced view that shares the underlying buffer.</summary>
    IMemoryOwner<T> Slice(int start, int length);
}
