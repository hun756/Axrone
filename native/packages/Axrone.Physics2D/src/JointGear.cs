namespace Axrone.Physics2D;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable CA1062 // Policy entry points run behind JointStorage sweeps owned by JointStore; JointSolver validates both stores once per pass, so per-sweep revalidation would pessimise the hot loop without covering new ground.
/// <summary>One ratio lock between two geared coordinates; bridges two islands.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct GearJointData
{
    /// <summary>First body of the first geared joint; island bridge endpoint.</summary>
    public BodyId BodyA;

    /// <summary>First body of the second geared joint; island bridge endpoint.</summary>
    public BodyId BodyB;

    /// <summary>First geared joint; never a gear itself.</summary>
    public JointId GearJointA;

    /// <summary>Second geared joint; never a gear itself.</summary>
    public JointId GearJointB;

    /// <summary>Coordinate ratio of the second joint; finite and non-zero.</summary>
    public float GearRatio;

    /// <summary>Fixed coordinate sum captured at creation.</summary>
    public float GearConstant;

    /// <summary>Accumulated gear impulse; warm-started every step.</summary>
    public float GearImpulse;
}

/// <summary>Ratio lock: one bilateral impulse over the summed coordinates.</summary>
public readonly struct GearPolicy : IJointPolicy<GearPolicy, GearJointData>
{
    /// <inheritdoc/>
    public static JointKind Kind => JointKind.Gear;

    /// <inheritdoc/>
    public static void GetBodies(ref GearJointData data, out BodyId bodyA, out BodyId bodyB)
    {
        bodyA = data.BodyA;
        bodyB = data.BodyB;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void InitVelocity(
        ref GearJointData data, BodyStore bodies, JointStore store, float wakeTolerance, float dt)
    {
        if (!store.IsValid(data.GearJointA) || !store.IsValid(data.GearJointB))
        {
            return;
        }

        float rateA = store.GetCoordinateRate(bodies, data.GearJointA);
        float rateB = store.GetCoordinateRate(bodies, data.GearJointB);
        if (MathF.Abs(rateA + (data.GearRatio * rateB)) > wakeTolerance)
        {
            store.WakeGearedBodies(bodies, data.GearJointA);
            store.WakeGearedBodies(bodies, data.GearJointB);
        }

        store.ApplyGearedVelocity(bodies, data.GearJointA, data.GearImpulse);
        store.ApplyGearedVelocity(bodies, data.GearJointB, data.GearImpulse * data.GearRatio);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolveVelocity(ref GearJointData data, BodyStore bodies, JointStore store)
    {
        if (!store.IsValid(data.GearJointA) || !store.IsValid(data.GearJointB))
        {
            return;
        }

        float ratio = data.GearRatio;
        float massA = store.GetEffectiveCoordinateMass(bodies, data.GearJointA);
        float massB = store.GetEffectiveCoordinateMass(bodies, data.GearJointB);
        float k = massA + (ratio * ratio * massB);
        if (k <= 0.0f)
        {
            return;
        }

        float rate = store.GetCoordinateRate(bodies, data.GearJointA) + (ratio * store.GetCoordinateRate(bodies, data.GearJointB));
        float delta = -rate / k;
        data.GearImpulse += delta;

        store.ApplyGearedVelocity(bodies, data.GearJointA, delta);
        store.ApplyGearedVelocity(bodies, data.GearJointB, delta * ratio);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolvePosition(ref GearJointData data, BodyStore bodies, JointStore store)
    {
        if (!store.IsValid(data.GearJointA) || !store.IsValid(data.GearJointB))
        {
            return;
        }

        float ratio = data.GearRatio;
        float massA = store.GetEffectiveCoordinateMass(bodies, data.GearJointA);
        float massB = store.GetEffectiveCoordinateMass(bodies, data.GearJointB);
        float k = massA + (ratio * ratio * massB);
        if (k <= 0.0f)
        {
            return;
        }

        float error = (store.GetCoordinate(bodies, data.GearJointA) + (ratio * store.GetCoordinate(bodies, data.GearJointB))) - data.GearConstant;
        if (MathF.Abs(error) < SolverTuning.LinearSlop)
        {
            return;
        }

        float correction = -error / k;
        store.ApplyGearedPosition(bodies, data.GearJointA, correction);
        store.ApplyGearedPosition(bodies, data.GearJointB, correction * ratio);
    }
}
#pragma warning restore CA1062
