namespace Axrone.Physics2D;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable CA1062 // Policy entry points run behind JointStorage sweeps owned by JointStore; JointSolver validates both stores once per pass, so per-sweep revalidation would pessimise the hot loop without covering new ground.
/// <summary>One rope over two ground pulleys with a length ratio.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct PulleyJointData
{
    /// <summary>First body.</summary>
    public BodyId BodyA;

    /// <summary>Second body.</summary>
    public BodyId BodyB;

    /// <summary>Anchor in the first body frame.</summary>
    public Vec2 LocalAnchorA;

    /// <summary>Anchor in the second body frame.</summary>
    public Vec2 LocalAnchorB;

    /// <summary>Fixed world pulley of the first rope.</summary>
    public Vec2 PulleyGroundA;

    /// <summary>Fixed world pulley of the second rope.</summary>
    public Vec2 PulleyGroundB;

    /// <summary>Rope length ratio; strictly positive.</summary>
    public float PulleyRatio;

    /// <summary>Fixed total rope length captured at creation.</summary>
    public float PulleyConstant;

    /// <summary>Accumulated rope impulse; warm-started every step.</summary>
    public float PulleyImpulse;
}

/// <summary>Rope: one bilateral impulse holding the weighted total length.</summary>
public readonly struct PulleyPolicy : IJointPolicy<PulleyPolicy, PulleyJointData>
{
    /// <inheritdoc/>
    public static JointKind Kind => JointKind.Pulley;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GetBodies(ref PulleyJointData data, out BodyId bodyA, out BodyId bodyB)
    {
        bodyA = data.BodyA;
        bodyB = data.BodyB;
    }

    /// <summary>Rope directions from ground pulleys to anchors; false while either rope is slack.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal static bool GetDirections(in PulleyJointData data, Vec2 pA, Vec2 pB, out Vec2 uA, out Vec2 uB)
    {
        Vec2 dA = pA - data.PulleyGroundA;
        Vec2 dB = pB - data.PulleyGroundB;
        float lenASq = dA.LengthSquared();
        float lenBSq = dB.LengthSquared();

        if (lenASq <= SolverTuning.LinearSlop * SolverTuning.LinearSlop ||
            lenBSq <= SolverTuning.LinearSlop * SolverTuning.LinearSlop)
        {
            uA = default;
            uB = default;
            return false;
        }

        uA = dA * (1.0f / MathF.Sqrt(lenASq));
        uB = dB * (1.0f / MathF.Sqrt(lenBSq));
        return true;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void InitVelocity(
        ref PulleyJointData data, BodyStore bodies, JointStore store, float wakeTolerance, float dt)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        Vec2 pA = stateA.Position + MathPhysics.Rotate(stateA, data.LocalAnchorA);
        Vec2 pB = stateB.Position + MathPhysics.Rotate(stateB, data.LocalAnchorB);
        Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);

        Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
        Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
        if ((vB - vA).LengthSquared() > wakeTolerance * wakeTolerance)
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

        if (GetDirections(in data, pA, pB, out Vec2 uA, out Vec2 uB))
        {
            Vec2 impulseA = uA * data.PulleyImpulse;
            Vec2 impulseB = uB * (data.PulleyImpulse * data.PulleyRatio);
            if (awakeA)
            {
                stateA.LinearVelocity += impulseA * stateA.InverseMass;
                stateA.AngularVelocity += stateA.InverseInertia * Vec2.Cross(rA, impulseA);
            }

            if (awakeB)
            {
                stateB.LinearVelocity += impulseB * stateB.InverseMass;
                stateB.AngularVelocity += stateB.InverseInertia * Vec2.Cross(rB, impulseB);
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolveVelocity(ref PulleyJointData data, BodyStore bodies, JointStore store)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;
        if (!awakeA && !awakeB)
        {
            return;
        }

        Vec2 pA = stateA.Position + MathPhysics.Rotate(stateA, data.LocalAnchorA);
        Vec2 pB = stateB.Position + MathPhysics.Rotate(stateB, data.LocalAnchorB);
        if (!GetDirections(in data, pA, pB, out Vec2 uA, out Vec2 uB))
        {
            return;
        }

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);

        float ratio = data.PulleyRatio;
        float rnA = Vec2.Cross(rA, uA);
        float rnB = Vec2.Cross(rB, uB);
        float k = invMassA + (ratio * ratio * invMassB) + (invIA * rnA * rnA) + (ratio * ratio * invIB * rnB * rnB);
        if (k <= 0.0f)
        {
            return;
        }

        Vec2 vA = stateA.LinearVelocity + (stateA.AngularVelocity * MathPhysics.Perp(rA));
        Vec2 vB = stateB.LinearVelocity + (stateB.AngularVelocity * MathPhysics.Perp(rB));
        float rate = Vec2.Dot(uA, vA) + (ratio * Vec2.Dot(uB, vB));
        float delta = -rate / k;
        data.PulleyImpulse += delta;

        Vec2 appliedA = uA * delta;
        Vec2 appliedB = uB * (delta * ratio);
        if (awakeA)
        {
            stateA.LinearVelocity += appliedA * invMassA;
            stateA.AngularVelocity += invIA * Vec2.Cross(rA, appliedA);
        }

        if (awakeB)
        {
            stateB.LinearVelocity += appliedB * invMassB;
            stateB.AngularVelocity += invIB * Vec2.Cross(rB, appliedB);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void SolvePosition(ref PulleyJointData data, BodyStore bodies, JointStore store)
    {
        ref BodyState stateA = ref bodies.Slot(data.BodyA);
        ref BodyState stateB = ref bodies.Slot(data.BodyB);

        bool awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        bool awakeB = (stateB.Flags & BodyFlags.Awake) != 0;
        if (!awakeA && !awakeB)
        {
            return;
        }

        Vec2 pA = stateA.Position + MathPhysics.Rotate(stateA, data.LocalAnchorA);
        Vec2 pB = stateB.Position + MathPhysics.Rotate(stateB, data.LocalAnchorB);
        if (!GetDirections(in data, pA, pB, out Vec2 uA, out Vec2 uB))
        {
            return;
        }

        float invMassA = awakeA ? stateA.InverseMass : 0.0f;
        float invMassB = awakeB ? stateB.InverseMass : 0.0f;
        float invIA = awakeA ? stateA.InverseInertia : 0.0f;
        float invIB = awakeB ? stateB.InverseInertia : 0.0f;

        Vec2 rA = MathPhysics.Rotate(stateA, data.LocalAnchorA) - MathPhysics.Rotate(stateA, stateA.LocalCenter);
        Vec2 rB = MathPhysics.Rotate(stateB, data.LocalAnchorB) - MathPhysics.Rotate(stateB, stateB.LocalCenter);

        float ratio = data.PulleyRatio;
        float rnA = Vec2.Cross(rA, uA);
        float rnB = Vec2.Cross(rB, uB);
        float k = invMassA + (ratio * ratio * invMassB) + (invIA * rnA * rnA) + (ratio * ratio * invIB * rnB * rnB);
        if (k <= 0.0f)
        {
            return;
        }

        float lengthA = (pA - data.PulleyGroundA).Length();
        float lengthB = (pB - data.PulleyGroundB).Length();
        float error = Math.Clamp(
            (lengthA + (ratio * lengthB)) - data.PulleyConstant,
            -SolverTuning.MaxCorrection, SolverTuning.MaxCorrection);

        Vec2 appliedA = uA * (-error / k);
        Vec2 appliedB = uB * (-error * ratio / k);
        if (awakeA)
        {
            stateA.Position += appliedA * invMassA;
            float angleA = MathF.Atan2(stateA.Sin, stateA.Cos) + (invIA * Vec2.Cross(rA, appliedA));
            stateA.Cos = MathF.Cos(angleA);
            stateA.Sin = MathF.Sin(angleA);
        }

        if (awakeB)
        {
            stateB.Position += appliedB * invMassB;
            float angleB = MathF.Atan2(stateB.Sin, stateB.Cos) + (invIB * Vec2.Cross(rB, appliedB));
            stateB.Cos = MathF.Cos(angleB);
            stateB.Sin = MathF.Sin(angleB);
        }
    }
}
#pragma warning restore CA1062
