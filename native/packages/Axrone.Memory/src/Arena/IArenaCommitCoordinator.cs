namespace Axrone.Memory.Arena;

internal interface IArenaCommitCoordinator<T> where T : unmanaged
{
    void CommitWrite(ulong sequence, int count);
    void AbandonWrite(ulong sequence, int count);
    void CommitRead(ulong sequence, int count);
    void AbandonRead(ulong sequence, int count);
}
