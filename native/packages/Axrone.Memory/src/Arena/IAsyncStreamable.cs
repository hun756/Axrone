namespace Axrone.Memory.Arena;

public interface IAsyncStreamable<T>
{
    ValueTask WriteAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default);

    ValueTask<int> ReadAsync(Memory<T> destination, CancellationToken cancellationToken = default);
}
