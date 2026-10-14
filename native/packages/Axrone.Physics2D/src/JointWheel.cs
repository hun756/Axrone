namespace Axrone.Physics2D;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable CA1062 // Policy entry points run behind JointStorage sweeps owned by JointStore; JointSolver validates both stores once per pass, so per-sweep revalidation would pessimise the hot loop without covering new ground.
/// <summary>One sprung suspension with a free-spinning hub and a lateral lock.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct WheelJointData
{
    /// <summary>First body; carries the suspension.</summary>
    public BodyId BodyA;

    /// <summary>Second body; the wheel.</summary>
    public BodyId BodyB;

    /// <summary>Anchor in the first body frame.</summary>
    public Vec2 LocalAnchorA;

    /// <summary>Anchor in the second body frame.</summary>
    public Vec2 LocalAnchorB;

    /// <summary>Suspension axis in the first body frame.</summary>
    public Vec2 LocalAxisA;

    /// <summary>Along-axis separation at creation; the spring rest length.</summary>
    public float ReferenceTranslation;

    /// <summary>Suspension frequency in hertz.</summary>
    public float SuspensionFrequency;

    /// <summary>Suspension damping ratio.</summary>
    public float SuspensionDamping;

    /// <summary>Target hub speed.</summary>
    public float MotorSpeed;

    /// <summary>Peak hub torque; never negative.</summary>
    public float MaxMotorTorque;

    /// <summary>Accumulated hub motor impulse; warm-started every step.</summary>
    public float MotorImpulse;

    /// <summary>Hub motor impulse cap of the current step.</summary>
    public float MotorMaxImpulse;

    /// <summary>Accumulated lateral impulse; warm-started every step.</summary>
    public float PerpImpulse;

    /// <summary>Whether the hub motor drives.</summary>
    public bool EnableMotor;
}

/// <summary>Suspension: explicit spring force in init, hub motor plus lateral lock in the sweep.</summary>
public readonly struct WheelPolicy : IJointPolicy<WheelPolicy, WheelJointData>
{
    /// <inheritdoc/>
    public static JointKind Kind => JointKind.Wheel;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBodies(ref WheelJointData data, out BodyId bodyA, out BodyId bodyB)
    {
        bodyA = data.BodyA;
        bodyB = data.BodyB;
    }

    /// <summary>Shared suspension frame: axis, lateral, COM-relative arms and anchor points.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void Frame(
        in BodyState stateA, in BodyState stateB,
        Vec2 localAnchorA, Vec2 localAnchorB, Vec2 localAxisA,
        out Vec2 axis, out Vec2 perp, out Vec2 rA, out Vec2 rB, out Vec2 pA, out Vec2 pB)
    {
        axis = MathPhysics.Rotate(stateA, localAxisA);
        if (axis.LengthSquared() > 1e-12f)
        {
            axis *= 1.0f / MathF.Sqrt(axis.LengthSquared());
        }

        perp = MathPhysics.Perp(axis);
        rA = MathPhysics.Rotate(stateA, localAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
        rB = MathPhysics.Rotate(stateB, localAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);
        pA = stateA.Position + MathPhysics.Rotate(stateA, localAnchorA);
        pB = stateB.Position + MathPhysics.Rotate(stateB, localAnchorB);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void InitVelocity(
        ref WheelJointData data, BodyStore bodies, JointStore store, float wakeTolerance, float dt)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        Frame(in stateA, in stateB, data.LocalAnchorA, data.LocalAnchorB, data.LocalAxisA,
            out Vec2 axis, out Vec2 perp, out Vec2 rA, out Vec2 rB, out Vec2 pA, out Vec2 pB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;
        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        float axisA = Vec2.Cross(rA, axis);
        float axisB = Vec2.Cross(rB, axis);
        float springInverseMass = invMassA + invMassB + (invIA * axisA * axisA) + (invIB * axisB * axisB);
        if (springInverseMass > 0.0f)
        {
            float mass = 1.0f / springInverseMass;
            float omega = 2.0f * MathF.PI * data.SuspensionFrequency;
            float stiffness = mass * omega * omega;
            float damping = 2.0f * mass * data.SuspensionDamping * omega;

            float stretch = Vec2.Dot(pB - pA, axis) - data.ReferenceTranslation;
            Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
            Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
            float rate = Vec2.Dot(vB - vA, axis);

            Vec2 spring = axis * ((-(stiffness * stretch) - (damping * rate)) * dt);
            if (awakeA)
            {
                stateA.LinearVelocity -= spring * invMassA;
                stateA.AngularVelocity -= invIA * Vec2.Cross(rA, spring);
            }

            if (awakeB)
            {
                stateB.LinearVelocity += spring * invMassB;
                stateB.AngularVelocity += invIB * Vec2.Cross(rB, spring);
            }
        }

        data.MotorMaxImpulse = data.EnableMotor ? data.MaxMotorTorque * dt : 0.0f;

        Vec2 anchorA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
        Vec2 anchorB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
        if ((anchorB - anchorA).LengthSquared() > wakeTolerance * wakeTolerance ||
            MathF.Abs(stateB.AngularVelocity - stateA.AngularVelocity) > wakeTolerance)
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

        awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        Vec2 impulse = perp * data.PerpImpulse;
        if (awakeA)
        {
            stateA.LinearVelocity -= impulse * stateA.InverseMass;
            stateA.AngularVelocity -= stateA.InverseInertia * (Vec2.Cross(rA, impulse) + data.MotorImpulse);
        }

        if (awakeB)
        {
            stateB.LinearVelocity += impulse * stateB.InverseMass;
            stateB.AngularVelocity += stateB.InverseInertia * (Vec2.Cross(rB, impulse) + data.MotorImpulse);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolveVelocity(ref WheelJointData data, BodyStore bodies, JointStore store)
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
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        Frame(in stateA, in stateB, data.LocalAnchorA, data.LocalAnchorB, data.LocalAxisA,
            out _, out Vec2 perp, out Vec2 rA, out Vec2 rB, out _, out _);

        float axialMass = invIA + invIB;
        if (data.EnableMotor && axialMass > 0.0f)
        {
            float rate = (stateB.AngularVelocity - stateA.AngularVelocity) - data.MotorSpeed;
            float impulse = -rate / axialMass;
            float old = data.MotorImpulse;
            float next = Math.Clamp(old + impulse, -data.MotorMaxImpulse, data.MotorMaxImpulse);
            float delta = next - old;
            data.MotorImpulse = next;

            if (awakeA)
            {
                stateA.AngularVelocity -= invIA * delta;
            }

            if (awakeB)
            {
                stateB.AngularVelocity += invIB * delta;
            }
        }

        float perpA = Vec2.Cross(rA, perp);
        float perpB = Vec2.Cross(rB, perp);
        float perpMass = invMassA + invMassB + (invIA * perpA * perpA) + (invIB * perpB * perpB);
        if (perpMass > 0.0f)
        {
            Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
            Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
            float rate = Vec2.Dot(vB - vA, perp);
            float delta = -rate / perpMass;
            data.PerpImpulse += delta;
            Vec2 applied = perp * delta;

            if (awakeA)
            {
                stateA.LinearVelocity -= applied * invMassA;
                stateA.AngularVelocity -= invIA * Vec2.Cross(rA, applied);
            }

            if (awakeB)
            {
                stateB.LinearVelocity += applied * invMassB;
                stateB.AngularVelocity += invIB * Vec2.Cross(rB, applied);
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolvePosition(ref WheelJointData data, BodyStore bodies, JointStore store)
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
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        Frame(in stateA, in stateB, data.LocalAnchorA, data.LocalAnchorB, data.LocalAxisA,
            out _, out Vec2 perp, out Vec2 rA, out Vec2 rB, out Vec2 pA, out Vec2 pB);

        float perpA = Vec2.Cross(rA, perp);
        float perpB = Vec2.Cross(rB, perp);
        float perpK = invMassA + invMassB + (invIA * perpA * perpA) + (invIB * perpB * perpB);
        if (perpK <= 0.0f)
        {
            return;
        }

        float error = Math.Clamp(Vec2.Dot(pB - pA, perp), -SolverTuning.MaxCorrection, SolverTuning.MaxCorrection);
        Vec2 applied = perp * (-error / perpK);
        MathPhysics.ApplyPositionCorrection(ref stateA, ref stateB, rA, rB, applied, invMassA, invMassB, invIA, invIB, awakeA, awakeB);
    }
}
#pragma warning restore CA1062
