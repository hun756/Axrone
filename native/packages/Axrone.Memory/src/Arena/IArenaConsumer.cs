namespace Axrone.Memory.Arena;

public interface IArenaConsumer<T>
{
    bool TryRead(out T item);
    bool TryReadSpinning(out T item, CancellationToken cancellationToken = default);
    ValueTask<T> ReadAsync(CancellationToken cancellationToken = default);
}
