namespace Axrone.Memory.Arena;

public interface IBatchReservable<T> where T : unmanaged
{
    bool TryReserveWrite(int count, out BatchWriteReservation<T> reservation);
    bool TryReserveRead(int count, out BatchReadReservation<T> reservation);
}
