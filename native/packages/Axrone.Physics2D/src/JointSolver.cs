namespace Axrone.Physics2D;

using System.Runtime.CompilerServices;

/// <summary>
/// Sequential-impulse joint solver: init freezes the effective masses and
/// warm-starts the stored impulses (waking sleeping dynamics on real relative
/// motion), then sweeps hold every constraint in velocity and position.
/// Sleeping endpoints read as infinite mass.
/// </summary>
public static class JointSolver
{
    /// <summary>
    /// Per-step init: wakes sleeping dynamics on genuine relative motion and
    /// applies the stored impulses to velocities. Call once per step before
    /// the iteration loop.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void InitVelocity(BodyStore bodies, JointStore joints, float wakeTolerance, float dt)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        ArgumentNullException.ThrowIfNull(joints);

        joints.InitVelocityPass(bodies, wakeTolerance, dt);
    }

    /// <summary>Runs <paramref name="iterations"/> sweeps of velocity impulses.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void SolveVelocityPass(BodyStore bodies, JointStore joints, int iterations)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        ArgumentNullException.ThrowIfNull(joints);

        for (int i = 0; i < iterations; i++)
        {
            joints.SolveVelocitySweep(bodies);
        }
    }

    /// <summary>Runs <paramref name="iterations"/> sweeps of position correction.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void SolvePositionPass(BodyStore bodies, JointStore joints, int iterations)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        ArgumentNullException.ThrowIfNull(joints);

        for (int i = 0; i < iterations; i++)
        {
            joints.SolvePositionSweep(bodies);
        }
    }
}
