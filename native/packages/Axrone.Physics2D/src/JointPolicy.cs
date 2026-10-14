namespace Axrone.Physics2D;

public interface IJointPolicy<TSelf, TData>
    where TSelf : struct, IJointPolicy<TSelf, TData>
    where TData : struct
{
    /// <summary>The constraint archetype.</summary>
    static abstract JointKind Kind { get; }

    /// <summary>Reads the coordinated body pair for island union.</summary>
    static abstract void GetBodies(ref TData data, out BodyId bodyA, out BodyId bodyB);

    /// <summary>
    /// Per-step init: wakes sleeping dynamics on genuine relative motion and
    /// applies the stored impulse to velocities. Runs once per step before
    /// the iteration loop.
    /// </summary>
    static abstract void InitVelocity(
        ref TData data,
        BodyStore bodies,
        JointStore store,
        float wakeTolerance,
        float dt);

    /// <summary>Runs one sweep of velocity impulses.</summary>
    static abstract void SolveVelocity(
        ref TData data,
        BodyStore bodies,
        JointStore store);

    /// <summary>Runs one sweep of position correction.</summary>
    static abstract void SolvePosition(
        ref TData data,
        BodyStore bodies,
        JointStore store);
}
