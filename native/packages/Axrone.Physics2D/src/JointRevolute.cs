namespace Axrone.Physics2D;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable CA1062 // Policy entry points run behind JointStorage sweeps owned by JointStore; JointSolver validates both stores once per pass, so per-sweep revalidation would pessimise the hot loop without covering new ground.
/// <summary>One pinned hinge with optional angular stops and a speed motor.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct RevoluteJointData
{
    /// <summary>First body.</summary>
    public BodyId BodyA;

    /// <summary>Second body.</summary>
    public BodyId BodyB;

    /// <summary>Anchor in the first body frame.</summary>
    public Vec2 LocalAnchorA;

    /// <summary>Anchor in the second body frame.</summary>
    public Vec2 LocalAnchorB;

    /// <summary>Reference angle (second minus first) at creation.</summary>
    public float ReferenceAngle;

    /// <summary>Lower angular stop in radians.</summary>
    public float LowerAngle;

    /// <summary>Upper angular stop in radians.</summary>
    public float UpperAngle;

    /// <summary>Target relative angular velocity.</summary>
    public float MotorSpeed;

    /// <summary>Peak motor torque; never negative.</summary>
    public float MaxMotorTorque;

    /// <summary>Accumulated point impulse; warm-started every step.</summary>
    public Vec2 PointImpulse;

    /// <summary>Accumulated motor impulse; warm-started every step.</summary>
    public float MotorImpulse;

    /// <summary>Accumulated limit impulse; warm-started every step.</summary>
    public float LimitImpulse;

    /// <summary>Motor impulse cap of the current step (torque times dt).</summary>
    public float MotorMaxImpulse;

    /// <summary>Resolved limit state of the current step.</summary>
    public LimitState LimitState;

    /// <summary>Whether angular stops apply.</summary>
    public bool EnableLimit;

    /// <summary>Whether the motor drives.</summary>
    public bool EnableMotor;
}

/// <summary>Pinned hinge: motor, then limit, then the 2x2 point block.</summary>
public readonly struct RevolutePolicy : IJointPolicy<RevolutePolicy, RevoluteJointData>
{
    /// <inheritdoc/>
    public static JointKind Kind => JointKind.Revolute;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBodies(ref RevoluteJointData data, out BodyId bodyA, out BodyId bodyB)
    {
        bodyA = data.BodyA;
        bodyB = data.BodyB;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void InitVelocity(
        ref RevoluteJointData data, BodyStore bodies, JointStore store, float wakeTolerance, float dt)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        float angleA = MathF.Atan2(stateA.Sin, stateA.Cos);
        float angleB = MathF.Atan2(stateB.Sin, stateB.Cos);
        float jointAngle = MathPhysics.NormalizeAngle((angleB - angleA) - data.ReferenceAngle);

        LimitState previous = data.LimitState;
        data.LimitState = MathPhysics.ResolveLimitState(data.EnableLimit, data.LowerAngle, data.UpperAngle, jointAngle, SolverTuning.AngularSlop);
        if (data.LimitState != previous)
        {
            data.LimitImpulse = 0.0f;
        }

        data.MotorMaxImpulse = data.EnableMotor ? data.MaxMotorTorque * dt : 0.0f;

        Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);

        Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
        Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
        if ((vB - vA).LengthSquared() > wakeTolerance * wakeTolerance ||
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

        Vec2 pointImpulse = data.PointImpulse;
        float totalTorque = data.MotorImpulse + data.LimitImpulse;
        if (awakeA)
        {
            stateA.LinearVelocity -= pointImpulse * stateA.InverseMass;
            stateA.AngularVelocity -= stateA.InverseInertia * (Vec2.Cross(rA, pointImpulse) + totalTorque);
        }

        if (awakeB)
        {
            stateB.LinearVelocity += pointImpulse * stateB.InverseMass;
            stateB.AngularVelocity += stateB.InverseInertia * (Vec2.Cross(rB, pointImpulse) + totalTorque);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolveVelocity(ref RevoluteJointData data, BodyStore bodies, JointStore store)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;
        float axialMass = 1.0f / (invIA + invIB);

        if (data.EnableMotor && (awakeA || awakeB))
        {
            float rate = (stateB.AngularVelocity - stateA.AngularVelocity) - data.MotorSpeed;
            float impulse = -axialMass * rate;
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

        if (data.LimitState != LimitState.Inactive && (awakeA || awakeB))
        {
            float rate = stateB.AngularVelocity - stateA.AngularVelocity;
            float impulse = -axialMass * rate;
            float old = data.LimitImpulse;
            float next = data.LimitState switch
            {
                LimitState.AtLower => MathF.Max(old + impulse, 0.0f),
                LimitState.AtUpper => MathF.Min(old + impulse, 0.0f),
                _ => old + impulse,
            };
            float delta = next - old;
            data.LimitImpulse = next;

            if (awakeA)
            {
                stateA.AngularVelocity -= invIA * delta;
            }

            if (awakeB)
            {
                stateB.AngularVelocity += invIB * delta;
            }
        }

        Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);

        float k11 = invMassA + invMassB + (invIA * rA.Y * rA.Y) + (invIB * rB.Y * rB.Y);
        float k22 = invMassA + invMassB + (invIA * rA.X * rA.X) + (invIB * rB.X * rB.X);
        float k12 = (invIA * rA.X * rA.Y) - (invIB * rB.X * rB.Y);
        float det = (k11 * k22) - (k12 * k12);
        if (det <= 0.0f)
        {
            return;
        }

        Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
        Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
        Vec2 accumulated = data.PointImpulse;
        Vec2 residual = (vB - vA) - new Vec2(
            (k11 * accumulated.X) + (k12 * accumulated.Y),
            (k12 * accumulated.X) + (k22 * accumulated.Y));

        float invDet = 1.0f / det;
        Vec2 solved = new(
            ((k12 * residual.Y) - (k22 * residual.X)) * invDet,
            ((k12 * residual.X) - (k11 * residual.Y)) * invDet);
        Vec2 deltaImpulse = solved - accumulated;
        data.PointImpulse = solved;

        if (awakeA)
        {
            stateA.LinearVelocity -= deltaImpulse * invMassA;
            stateA.AngularVelocity -= invIA * Vec2.Cross(rA, deltaImpulse);
        }

        if (awakeB)
        {
            stateB.LinearVelocity += deltaImpulse * invMassB;
            stateB.AngularVelocity += invIB * Vec2.Cross(rB, deltaImpulse);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolvePosition(ref RevoluteJointData data, BodyStore bodies, JointStore store)
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

        Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);

        float k11 = invMassA + invMassB + (invIA * rA.Y * rA.Y) + (invIB * rB.Y * rB.Y);
        float k22 = invMassA + invMassB + (invIA * rA.X * rA.X) + (invIB * rB.X * rB.X);
        float k12 = (invIA * rA.X * rA.Y) - (invIB * rB.X * rB.Y);
        float det = (k11 * k22) - (k12 * k12);

        if (det > 0.0f)
        {
            Vec2 pA = stateA.Position + MathPhysics.Rotate(stateA, data.LocalAnchorA);
            Vec2 pB = stateB.Position + MathPhysics.Rotate(stateB, data.LocalAnchorB);
            Vec2 error = pB - pA;
            float invDet = 1.0f / det;
            Vec2 correction = new(
                ((k12 * error.Y) - (k22 * error.X)) * invDet,
                ((k12 * error.X) - (k11 * error.Y)) * invDet);
            MathPhysics.ApplyPositionCorrection(ref stateA, ref stateB, rA, rB, correction, invMassA, invMassB, invIA, invIB, awakeA, awakeB);
        }

        if (data.LimitState == LimitState.Inactive)
        {
            return;
        }

        float axialMass = 1.0f / (invIA + invIB);
        float angleA = MathF.Atan2(stateA.Sin, stateA.Cos);
        float angleB = MathF.Atan2(stateB.Sin, stateB.Cos);
        float jointAngle = MathPhysics.NormalizeAngle((angleB - angleA) - data.ReferenceAngle);

        float angularError = data.LimitState switch
        {
            LimitState.AtLower => jointAngle - data.LowerAngle + SolverTuning.AngularSlop,
            LimitState.AtUpper => jointAngle - data.UpperAngle - SolverTuning.AngularSlop,
            _ => jointAngle - data.LowerAngle,
        };

        bool violates = data.LimitState switch
        {
            LimitState.AtLower => angularError < 0.0f,
            LimitState.AtUpper => angularError > 0.0f,
            _ => angularError != 0.0f,
        };

        if (!violates)
        {
            return;
        }

        float limitCorrection = -axialMass * angularError;
        if (awakeA)
        {
            float correctedA = MathF.Atan2(stateA.Sin, stateA.Cos) - (invIA * limitCorrection);
            stateA.Cos = MathF.Cos(correctedA);
            stateA.Sin = MathF.Sin(correctedA);
        }

        if (awakeB)
        {
            float correctedB = MathF.Atan2(stateB.Sin, stateB.Cos) + (invIB * limitCorrection);
            stateB.Cos = MathF.Cos(correctedB);
            stateB.Sin = MathF.Sin(correctedB);
        }
    }
}
#pragma warning restore CA1062
