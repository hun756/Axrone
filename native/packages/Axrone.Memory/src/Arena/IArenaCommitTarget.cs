namespace Axrone.Memory.Arena;

public interface IArenaCommitTarget
{
    long CommittedWriteSequence { get; }

    long CommittedReadSequence { get; }
}
