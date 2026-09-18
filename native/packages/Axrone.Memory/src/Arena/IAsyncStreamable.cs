namespace Axrone.Memory.Arena;

public interface IAsyncStreamable<T>
{
    ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default);
    ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = default);
    IAsyncEnumerable<T> ReadAllAsync(CancellationToken cancellationToken = default);
}
