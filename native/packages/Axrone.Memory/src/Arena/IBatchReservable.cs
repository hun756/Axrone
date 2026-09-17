namespace Axrone.Memory.Arena;

public interface IBatchReservable<T>
{
    int ReserveWriteBatch(int requestedCount, out Span<T> reservedSpan);

    int ReserveReadBatch(int requestedCount, out Span<T> reservedSpan);

    void CommitWrite(int committedCount);

    void CommitRead(int committedCount);
}
