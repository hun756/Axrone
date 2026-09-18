namespace Axrone.Memory.Arena;

public interface IArenaProducer<T>
{
    bool TryWrite(in T item);
    void WriteSpinning(in T item, CancellationToken cancellationToken = default);
    ValueTask WriteAsync(T item, CancellationToken cancellationToken = default);
    bool TryWriteMany(params ReadOnlySpan<T> items);
}
