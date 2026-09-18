namespace Axrone.Memory.Arena;

public interface IBatchVisitor<T, TContext>
    where T : unmanaged
    where TContext : allows ref struct
{
    void ProcessBatch(ReadOnlySpan<T> segment1, ReadOnlySpan<T> segment2, ref TContext context);
}
