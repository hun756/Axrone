namespace Axrone.Physics2D;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable CA1062 // Policy entry points run behind JointStorage sweeps owned by JointStore; JointSolver validates both stores once per pass, so per-sweep revalidation would pessimise the hot loop without covering new ground.
/// <summary>One slider along an axis with optional stops, motor and angle lock.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct PrismaticJointData
{
    /// <summary>First body.</summary>
    public BodyId BodyA;

    /// <summary>Second body.</summary>
    public BodyId BodyB;

    /// <summary>Anchor in the first body frame.</summary>
    public Vec2 LocalAnchorA;

    /// <summary>Anchor in the second body frame.</summary>
    public Vec2 LocalAnchorB;

    /// <summary>Slide axis in the first body frame.</summary>
    public Vec2 LocalAxisA;

    /// <summary>Reference angle (second minus first) at creation.</summary>
    public float ReferenceAngle;

    /// <summary>Along-axis separation at creation; the slide zero.</summary>
    public float ReferenceTranslation;

    /// <summary>Lower slide stop along the axis.</summary>
    public float LowerTranslation;

    /// <summary>Upper slide stop along the axis.</summary>
    public float UpperTranslation;

    /// <summary>Target slide speed along the axis.</summary>
    public float MotorSpeed;

    /// <summary>Peak slide force; never negative.</summary>
    public float MaxMotorForce;

    /// <summary>Accumulated lateral impulse (.Y); the axis stays free except limit/motor.</summary>
    public Vec2 PrismaticImpulse;

    /// <summary>Accumulated slide motor impulse; warm-started every step.</summary>
    public float MotorImpulse;

    /// <summary>Accumulated slide limit impulse; warm-started every step.</summary>
    public float LimitImpulse;

    /// <summary>Slide motor impulse cap of the current step (force times dt).</summary>
    public float MotorMaxImpulse;

    /// <summary>Accumulated angle impulse; warm-started every step.</summary>
    public float WeldAngleImpulse;

    /// <summary>Resolved slide limit state of the current step.</summary>
    public LimitState LimitState;

    /// <summary>Whether slide stops apply.</summary>
    public bool EnableLimit;

    /// <summary>Whether the slide motor drives.</summary>
    public bool EnableMotor;
}

/// <summary>Slider: motor, then limit, then lateral lock, then angle lock.</summary>
public readonly struct PrismaticPolicy : IJointPolicy<PrismaticPolicy, PrismaticJointData>
{
    /// <inheritdoc/>
    public static JointKind Kind => JointKind.Prismatic;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBodies(ref PrismaticJointData data, out BodyId bodyA, out BodyId bodyB)
    {
        bodyA = data.BodyA;
        bodyB = data.BodyB;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void InitVelocity(
        ref PrismaticJointData data, BodyStore bodies, JointStore store, float wakeTolerance, float dt)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        Vec2 axis = MathPhysics.Rotate(stateA, data.LocalAxisA);
        float axisLenSq = axis.LengthSquared();
        if (axisLenSq > 1e-12f)
        {
            axis *= 1.0f / MathF.Sqrt(axisLenSq);
        }

        Vec2 pA = stateA.Position + MathPhysics.Rotate(stateA, data.LocalAnchorA);
        Vec2 pB = stateB.Position + MathPhysics.Rotate(stateB, data.LocalAnchorB);
        float translation = Vec2.Dot(pB - pA, axis) - data.ReferenceTranslation;

        LimitState previous = data.LimitState;
        data.LimitState = MathPhysics.ResolveLimitState(data.EnableLimit, data.LowerTranslation, data.UpperTranslation, translation, SolverTuning.AngularSlop);
        if (data.LimitState != previous)
        {
            data.LimitImpulse = 0.0f;
        }

        data.MotorMaxImpulse = data.EnableMotor ? data.MaxMotorForce * dt : 0.0f;

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

        Vec2 perp = MathPhysics.Perp(axis);
        Vec2 impulse = (axis * (data.MotorImpulse + data.LimitImpulse)) + (perp * data.PrismaticImpulse.Y);
        if (awakeA)
        {
            stateA.LinearVelocity -= impulse * stateA.InverseMass;
            stateA.AngularVelocity -= stateA.InverseInertia * (Vec2.Cross(rA, impulse) + data.WeldAngleImpulse);
        }

        if (awakeB)
        {
            stateB.LinearVelocity += impulse * stateB.InverseMass;
            stateB.AngularVelocity += stateB.InverseInertia * (Vec2.Cross(rB, impulse) + data.WeldAngleImpulse);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolveVelocity(ref PrismaticJointData data, BodyStore bodies, JointStore store)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        Vec2 axis = MathPhysics.Rotate(stateA, data.LocalAxisA);
        if (axis.LengthSquared() > 1e-12f)
        {
            axis *= 1.0f / MathF.Sqrt(axis.LengthSquared());
        }

        Vec2 perp = MathPhysics.Perp(axis);
        Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);

        float axisA = Vec2.Cross(rA, axis);
        float axisB = Vec2.Cross(rB, axis);
        float axisMass = invMassA + invMassB + (invIA * axisA * axisA) + (invIB * axisB * axisB);

        if (data.EnableMotor && axisMass > 0.0f && (awakeA || awakeB))
        {
            Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
            Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
            float rate = Vec2.Dot(vB - vA, axis) - data.MotorSpeed;
            float impulse = -rate / axisMass;
            float old = data.MotorImpulse;
            float next = Math.Clamp(old + impulse, -data.MotorMaxImpulse, data.MotorMaxImpulse);
            Vec2 applied = axis * (next - old);
            data.MotorImpulse = next;

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

        if (data.LimitState != LimitState.Inactive && axisMass > 0.0f && (awakeA || awakeB))
        {
            Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
            Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
            float rate = Vec2.Dot(vB - vA, axis);
            float impulse = -rate / axisMass;
            float old = data.LimitImpulse;
            float next = data.LimitState switch
            {
                LimitState.AtLower => MathF.Max(old + impulse, 0.0f),
                LimitState.AtUpper => MathF.Min(old + impulse, 0.0f),
                _ => old + impulse,
            };
            Vec2 applied = axis * (next - old);
            data.LimitImpulse = next;

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

        float perpA = Vec2.Cross(rA, perp);
        float perpB = Vec2.Cross(rB, perp);
        float perpMass = invMassA + invMassB + (invIA * perpA * perpA) + (invIB * perpB * perpB);
        if (perpMass > 0.0f && (awakeA || awakeB))
        {
            Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
            Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
            float rate = Vec2.Dot(vB - vA, perp);
            float delta = -rate / perpMass;
            data.PrismaticImpulse = new Vec2(data.PrismaticImpulse.X, data.PrismaticImpulse.Y + delta);
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

        float axialMass = invIA + invIB;
        if (axialMass > 0.0f && (awakeA || awakeB))
        {
            float rate = stateB.AngularVelocity - stateA.AngularVelocity;
            float delta = -rate / axialMass;
            data.WeldAngleImpulse += delta;

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
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolvePosition(ref PrismaticJointData data, BodyStore bodies, JointStore store)
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

        Vec2 axis = MathPhysics.Rotate(stateA, data.LocalAxisA);
        if (axis.LengthSquared() > 1e-12f)
        {
            axis *= 1.0f / MathF.Sqrt(axis.LengthSquared());
        }

        Vec2 perp = MathPhysics.Perp(axis);
        Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);
        Vec2 pA = stateA.Position + MathPhysics.Rotate(stateA, data.LocalAnchorA);
        Vec2 pB = stateB.Position + MathPhysics.Rotate(stateB, data.LocalAnchorB);
        Vec2 error = pB - pA;

        float perpA = Vec2.Cross(rA, perp);
        float perpB = Vec2.Cross(rB, perp);
        float perpK = invMassA + invMassB + (invIA * perpA * perpA) + (invIB * perpB * perpB);
        if (perpK > 0.0f)
        {
            float perpError = Math.Clamp(Vec2.Dot(error, perp), -SolverTuning.MaxCorrection, SolverTuning.MaxCorrection);
            Vec2 applied = perp * (-perpError / perpK);
            MathPhysics.ApplyPositionCorrection(ref stateA, ref stateB, rA, rB, applied, invMassA, invMassB, invIA, invIB, awakeA, awakeB);
        }

        float axialMass = invIA + invIB;
        if (axialMass > 0.0f)
        {
            float angleA = MathF.Atan2(stateA.Sin, stateA.Cos);
            float angleB = MathF.Atan2(stateB.Sin, stateB.Cos);
            float angleError = MathPhysics.NormalizeAngle((angleB - angleA) - data.ReferenceAngle);
            if (MathF.Abs(angleError) >= SolverTuning.AngularSlop)
            {
                float correction = -angleError / axialMass;
                if (awakeA)
                {
                    float correctedA = angleA - (invIA * correction);
                    stateA.Cos = MathF.Cos(correctedA);
                    stateA.Sin = MathF.Sin(correctedA);
                }

                if (awakeB)
                {
                    float correctedB = angleB + (invIB * correction);
                    stateB.Cos = MathF.Cos(correctedB);
                    stateB.Sin = MathF.Sin(correctedB);
                }
            }
        }

        if (data.LimitState == LimitState.Inactive)
        {
            return;
        }

        float axisA = Vec2.Cross(rA, axis);
        float axisB = Vec2.Cross(rB, axis);
        float axisK = invMassA + invMassB + (invIA * axisA * axisA) + (invIB * axisB * axisB);
        if (axisK <= 0.0f)
        {
            return;
        }

        float translation = Vec2.Dot(error, axis) - data.ReferenceTranslation;
        float limitError = data.LimitState switch
        {
            LimitState.AtLower => translation - data.LowerTranslation + SolverTuning.LinearSlop,
            LimitState.AtUpper => translation - data.UpperTranslation - SolverTuning.LinearSlop,
            _ => translation - data.LowerTranslation,
        };

        bool violates = data.LimitState switch
        {
            LimitState.AtLower => limitError < 0.0f,
            LimitState.AtUpper => limitError > 0.0f,
            _ => limitError != 0.0f,
        };

        if (!violates)
        {
            return;
        }

        Vec2 limitApplied = axis * (-Math.Clamp(limitError, -SolverTuning.MaxCorrection, SolverTuning.MaxCorrection) / axisK);
        MathPhysics.ApplyPositionCorrection(ref stateA, ref stateB, rA, rB, limitApplied, invMassA, invMassB, invIA, invIB, awakeA, awakeB);
    }
}
#pragma warning restore CA1062
