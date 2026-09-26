namespace Axrone.Animation;

/// <summary>
/// Zero-allocation palette consumer contract: static dispatch over a caller-owned
/// context (GPU upload, frustum culling, debug draw). Implementations must be
/// stateless structs accumulating into <typeparamref name="TContext"/> — the
/// visitor instance itself is a throwaway <c>default</c>; only the context survives.
/// Consumed through <see cref="Rig.AcceptPalette{TVisitor, TContext}"/> so the call
/// stays constrained (no boxing, no interface dispatch).
/// </summary>
/// <typeparam name="TContext">Caller-owned accumulator; may be a ref struct.</typeparam>
public interface IRigPaletteVisitor<TContext>
    where TContext : allows ref struct
{
    /// <summary>Consumes the world matrices into the context.</summary>
    void Visit(ref TContext context, ReadOnlySpan<Matrix4x4> worldMatrices);
}
