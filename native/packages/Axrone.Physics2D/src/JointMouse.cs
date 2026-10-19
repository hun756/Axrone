namespace Axrone.Physics2D;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable CA1062 // Policy entry points run behind JointStorage sweeps owned by JointStore; JointSolver validates both stores once per pass, so per-sweep revalidation would pessimise the hot loop without covering new ground.
/// <summary>One mouse servo: a body-frame grab point pulled toward a moving world target.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct MouseJointData
{
    /// <summary>Ground holder; usually static.</summary>
    public BodyId BodyA;

    /// <summary>Dragged body.</summary>
    public BodyId BodyB;

    /// <summary>Grab point in the dragged body frame.</summary>
    public Vec2 LocalAnchorB;

    /// <summary>Current world target.</summary>
    public Vec2 Target;

    /// <summary>Peak force; never negative.</summary>
    public float MaxForce;

    /// <summary>Convergence rate per second; never negative.</summary>
    public float Stiffness;

    /// <summary>Relative-velocity blend in [0, 1).</summary>
    public float Damping;

    /// <summary>Accumulated capped impulse.</summary>
    public Vec2 Impulse;

    /// <summary>Per-step impulse cap derived from the peak force.</summary>
    public float MaxImpulse;
}

/// <summary>Capped P-servo toward a moving target; the target is re-read every step.</summary>
public readonly struct MousePolicy : IJointPolicy<MousePolicy, MouseJointData>
{
    /// <inheritdoc/>
    public static JointKind Kind => JointKind.Mouse;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBodies(ref MouseJointData data, out BodyId bodyA, out BodyId bodyB)
    {
        bodyA = data.BodyA;
        bodyB = data.BodyB;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void InitVelocity(
        ref MouseJointData data, BodyStore bodies, JointStore store, float wakeTolerance, float dt)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        data.MaxImpulse = data.MaxForce * dt;

        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB);
        Vec2 anchor = stateB.Position + rB;
        if ((data.Target - anchor).LengthSquared() > wakeTolerance * wakeTolerance)
        {
            if (stateA.Type == BodyType.Dynamic)
            {
                bodies.SetAwake(data.BodyA, true);
            }

            if (stateB.Type == BodyType.Dynamic)
            {
                bodies.SetAwake(data.BodyB, true);
            }
        }

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        Vec2 impulse = data.Impulse;
        if (awakeA)
        {
            stateA.LinearVelocity -= impulse * stateA.InverseMass;
        }

        if (awakeB)
        {
            stateB.LinearVelocity += impulse * stateB.InverseMass;
            stateB.AngularVelocity += stateB.InverseInertia * Vec2.Cross(rB, impulse);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolveVelocity(ref MouseJointData data, BodyStore bodies, JointStore store)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;
        if (!awakeA && !awakeB)
        {
            return;
        }

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);
        Vec2 anchor = stateB.Position + MathPhysics.Rotate(stateB, data.LocalAnchorB);
        Vec2 error = data.Target - anchor;
        float errorSq = error.LengthSquared();
        if (errorSq < 1e-12f || data.MaxImpulse <= 0.0f)
        {
            data.Impulse = Vec2.Zero;
            return;
        }

        Vec2 direction = error * (1.0f / MathF.Sqrt(errorSq));
        float rnB = Vec2.Cross(rB, direction);
        float k = invMassA + invMassB + (invIB * rnB * rnB);
        if (k <= 0.0f)
        {
            return;
        }

        Vec2 desired = error * data.Stiffness;
        Vec2 vA = stateA.LinearVelocity;
        Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
        Vec2 relative = (vB - vA) * (1.0f - data.Damping);

        Vec2 impulse = (desired - relative) * (1.0f / k);
        Vec2 next = data.Impulse + impulse;
        float cap = data.MaxImpulse;
        if (cap > 0.0f && next.LengthSquared() > cap * cap)
        {
            next *= cap / MathF.Sqrt(next.LengthSquared());
        }

        Vec2 delta = next - data.Impulse;
        data.Impulse = next;

        if (awakeA)
        {
            stateA.LinearVelocity -= delta * invMassA;
        }

        if (awakeB)
        {
            stateB.LinearVelocity += delta * invMassB;
            stateB.AngularVelocity += invIB * Vec2.Cross(rB, delta);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolvePosition(ref MouseJointData data, BodyStore bodies, JointStore store)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;
        if (!awakeA && !awakeB)
        {
            return;
        }

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);
        Vec2 anchor = stateB.Position + MathPhysics.Rotate(stateB, data.LocalAnchorB);
        Vec2 axis = data.Target - anchor;
        float lengthSq = axis.LengthSquared();
        if (lengthSq < 1e-12f || data.MaxForce <= 0.0f)
        {
            return;
        }

        float length = MathF.Sqrt(lengthSq);
        Vec2 direction = axis * (1.0f / length);
        float rnB = Vec2.Cross(rB, direction);
        float k = invMassA + invMassB + (invIB * rnB * rnB);
        if (k <= 0.0f)
        {
            return;
        }

        float error = Math.Min(length, SolverTuning.MaxCorrection);
        Vec2 applied = direction * (error / k);

        if (awakeA)
        {
            stateA.Position -= applied * invMassA;
        }

        if (awakeB)
        {
            stateB.Position += applied * invMassB;
            stateB.AngularVelocity += invIB * Vec2.Cross(rB, applied);
        }
    }
}
#pragma warning restore CA1062
