namespace Axrone.Physics2D;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable CA1062 // Policy entry points run behind JointStorage sweeps owned by JointStore; JointSolver validates both stores once per pass, so per-sweep revalidation would pessimise the hot loop without covering new ground.
/// <summary>One servo target: capped force and torque toward a moving frame.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct MotorJointData
{
    /// <summary>First body; carries the target frame.</summary>
    public BodyId BodyA;

    /// <summary>Second body; servoed toward the target.</summary>
    public BodyId BodyB;

    /// <summary>Target offset in the first body frame.</summary>
    public Vec2 ServoOffset;

    /// <summary>Target relative angle.</summary>
    public float ServoAngle;

    /// <summary>Peak servo force; never negative.</summary>
    public float ServoMaxForce;

    /// <summary>Peak servo torque; never negative.</summary>
    public float ServoMaxTorque;

    /// <summary>Fraction of the pose error corrected per second; in (0, 1].</summary>
    public float ServoFactor;

    /// <summary>Servo response rate of the current step (factor over dt).</summary>
    public float ServoRate;

    /// <summary>Accumulated servo force impulse; warm-started every step.</summary>
    public Vec2 ServoLinearImpulse;

    /// <summary>Accumulated servo torque impulse; warm-started every step.</summary>
    public float ServoAngleImpulse;

    /// <summary>Servo force impulse cap of the current step.</summary>
    public float ServoMaxLinearImpulse;

    /// <summary>Servo torque impulse cap of the current step.</summary>
    public float ServoMaxAngleImpulse;
}

/// <summary>Capped P-servo toward the target pose; the error is re-derived every step.</summary>
public readonly struct MotorPolicy : IJointPolicy<MotorPolicy, MotorJointData>
{
    /// <inheritdoc/>
    public static JointKind Kind => JointKind.Motor;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBodies(ref MotorJointData data, out BodyId bodyA, out BodyId bodyB)
    {
        bodyA = data.BodyA;
        bodyB = data.BodyB;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void InitVelocity(
        ref MotorJointData data, BodyStore bodies, JointStore store, float wakeTolerance, float dt)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        data.ServoMaxLinearImpulse = data.ServoMaxForce * dt;
        data.ServoMaxAngleImpulse = data.ServoMaxTorque * dt;
        data.ServoRate = data.ServoFactor / dt;

        Vec2 target = stateA.Position + MathPhysics.Rotate(stateA, data.ServoOffset);
        Vec2 error = target - stateB.Position;
        if (error.LengthSquared() > wakeTolerance * wakeTolerance ||
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

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        if (awakeA)
        {
            stateA.LinearVelocity -= data.ServoLinearImpulse * stateA.InverseMass;
            stateA.AngularVelocity -= stateA.InverseInertia * data.ServoAngleImpulse;
        }

        if (awakeB)
        {
            stateB.LinearVelocity += data.ServoLinearImpulse * stateB.InverseMass;
            stateB.AngularVelocity += stateB.InverseInertia * data.ServoAngleImpulse;
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolveVelocity(ref MotorJointData data, BodyStore bodies, JointStore store)
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

        float linearMass = invMassA + invMassB;
        if (linearMass > 0.0f)
        {
            Vec2 target = stateA.Position + MathPhysics.Rotate(stateA, data.ServoOffset);
            Vec2 desired = (target - stateB.Position) * data.ServoRate;
            Vec2 relative = stateB.LinearVelocity - stateA.LinearVelocity;
            Vec2 impulse = (desired - relative) * (1.0f / linearMass);
            Vec2 next = data.ServoLinearImpulse + impulse;
            float cap = data.ServoMaxLinearImpulse;
            if (next.LengthSquared() > cap * cap)
            {
                next *= cap / MathF.Sqrt(next.LengthSquared());
            }

            Vec2 delta = next - data.ServoLinearImpulse;
            data.ServoLinearImpulse = next;

            if (awakeA)
            {
                stateA.LinearVelocity -= delta * invMassA;
            }

            if (awakeB)
            {
                stateB.LinearVelocity += delta * invMassB;
            }
        }

        float axialMass = invIA + invIB;
        if (axialMass > 0.0f)
        {
            float angleA = MathF.Atan2(stateA.Sin, stateA.Cos);
            float angleB = MathF.Atan2(stateB.Sin, stateB.Cos);
            float error = MathPhysics.NormalizeAngle((angleA + data.ServoAngle) - angleB);
            float desiredRate = (error * data.ServoRate) - (stateB.AngularVelocity - stateA.AngularVelocity);
            float impulse = desiredRate / axialMass;
            float next = Math.Clamp(
                data.ServoAngleImpulse + impulse,
                -data.ServoMaxAngleImpulse, data.ServoMaxAngleImpulse);
            float delta = next - data.ServoAngleImpulse;
            data.ServoAngleImpulse = next;

            if (awakeA)
            {
                stateA.AngularVelocity -= invIA * delta;
            }

            if (awakeB)
            {
                stateB.AngularVelocity += invIB * delta;
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SolvePosition(ref MotorJointData data, BodyStore bodies, JointStore store)
    {
        // Servo recomputes from pose each step; position correction is unconstrained.
    }
}
#pragma warning restore CA1062
