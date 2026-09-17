namespace Axrone.Memory.Arena;

public interface IBatchVisitor<T, TContext> where TContext : allows ref struct
{
    void VisitBatch(scoped ReadOnlySpan<T> batch, scoped ref TContext context);
}
