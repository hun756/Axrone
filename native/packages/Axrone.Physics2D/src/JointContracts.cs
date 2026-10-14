namespace Axrone.Physics2D;

/// <summary>Public joint registry role: count, liveness and teardown.</summary>
public interface IJointRegistry
{
    /// <summary>Live joints across all archetypes.</summary>
    int Count { get; }

    /// <summary>Whether an identity names a live joint.</summary>
    /// <param name="id">The identity.</param>
    bool IsValid(JointId id);

    /// <summary>Drops a joint; unknown identities are ignored.</summary>
    /// <param name="id">The identity.</param>
    void Destroy(JointId id);
}

/// <summary>Zero-virtual visitor over every live joint.</summary>
/// <typeparam name="TContext">The visitor context.</typeparam>
public interface IJointVisitor<TContext> where TContext : allows ref struct
{
    /// <summary>Visits one live joint.</summary>
    /// <typeparam name="TPolicy">The constraint archetype.</typeparam>
    /// <typeparam name="TData">The constraint storage layout.</typeparam>
    /// <param name="id">The joint identity.</param>
    /// <param name="data">The live constraint data.</param>
    /// <param name="context">The visitor context.</param>
    void Visit<TPolicy, TData>(JointId id, ref TData data, ref TContext context)
        where TPolicy : struct, IJointPolicy<TPolicy, TData>
        where TData : struct;
}
