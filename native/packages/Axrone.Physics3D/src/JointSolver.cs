namespace Axrone.Physics3D;

/// <summary>
/// Joint constraint solver: distance rods as one scalar row, ball-sockets as
/// symmetric 3x3 point rows, welds as point rows plus orientation rows. All
/// rows are bilateral (no clamping); impulses accumulate across steps for
/// warm starting. Positions correct with the same Baumgarte hand as
/// contacts.
/// </summary>
public static class JointSolver
{
    private const float PositionBeta = 0.2f;

    /// <summary>Runs one velocity sweep over every live joint.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void SolveVelocityPass(BodyStore bodies, JointStore joints)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentNullException.ThrowIfNull(joints);

        for (int slot = 0; slot < joints.DistanceCapacity; slot++)
        {
            if (!joints.IsDistanceLive(slot))
            {
                continue;
            }

            SolveDistanceVelocity(bodies, ref joints.Distance(slot));
        }

        for (int slot = 0; slot < joints.BallSocketCapacity; slot++)
        {
            if (!joints.IsBallSocketLive(slot))
            {
                continue;
            }

            SolveBallSocketVelocity(bodies, ref joints.BallSocket(slot));
        }

        for (int slot = 0; slot < joints.WeldCapacity; slot++)
        {
            if (!joints.IsWeldLive(slot))
            {
                continue;
            }

            SolveWeldVelocity(bodies, ref joints.Weld(slot));
        }
    }

    /// <summary>Runs one position sweep over every live joint.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void SolvePositionPass(BodyStore bodies, JointStore joints)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentNullException.ThrowIfNull(joints);

        for (int slot = 0; slot < joints.DistanceCapacity; slot++)
        {
            if (!joints.IsDistanceLive(slot))
            {
                continue;
            }

            SolveDistancePosition(bodies, ref joints.Distance(slot));
        }

        for (int slot = 0; slot < joints.BallSocketCapacity; slot++)
        {
            if (!joints.IsBallSocketLive(slot))
            {
                continue;
            }

            SolveBallSocketPosition(bodies, ref joints.BallSocket(slot));
        }

        for (int slot = 0; slot < joints.WeldCapacity; slot++)
        {
            if (!joints.IsWeldLive(slot))
            {
                continue;
            }

            SolveWeldPosition(bodies, ref joints.Weld(slot));
        }
    }

    /// <summary>World anchor and lever arms of a joint end.</summary>
    private static void AnchorFrame(
        ref BodyState state, Vec3 localAnchor,
        out Vec3 worldAnchor, out Vec3 centerOfMass, out Vec3 lever)
    {
        var frame = new RigidTransform3D(state.Position, state.Rotation);
        worldAnchor = frame.TransformPoint(localAnchor);
        centerOfMass = state.Position + (state.Rotation * state.LocalCenter);
        lever = worldAnchor - centerOfMass;
    }

    /// <summary>Effective inverse masses, zeroing sleeping bodies.</summary>
    private static void InverseMasses(
        ref BodyState stateA, ref BodyState stateB,
        out bool awakeA, out bool awakeB,
        out float invMassA, out float invMassB, out Vec3 invIA, out Vec3 invIB)
    {
        awakeA = (stateA.Flags & BodyFlags.Awake) != 0;
        awakeB = (stateB.Flags & BodyFlags.Awake) != 0;
        invMassA = awakeA ? stateA.InverseMass : 0.0f;
        invMassB = awakeB ? stateB.InverseMass : 0.0f;
        invIA = awakeA ? stateA.InverseInertia : Vec3.Zero;
        invIB = awakeB ? stateB.InverseInertia : Vec3.Zero;
    }

    private static void SolveDistanceVelocity(BodyStore bodies, ref DistanceJointData joint)
    {
        ref BodyState stateA = ref bodies.Slot(joint.BodyA);
        ref BodyState stateB = ref bodies.Slot(joint.BodyB);
        InverseMasses(ref stateA, ref stateB, out bool awakeA, out bool awakeB,
            out float invMassA, out float invMassB, out Vec3 invIA, out Vec3 invIB);

        AnchorFrame(ref stateA, joint.LocalAnchorA, out Vec3 pa, out _, out Vec3 rA);
        AnchorFrame(ref stateB, joint.LocalAnchorB, out Vec3 pb, out _, out Vec3 rB);

        Vec3 delta = pa - pb;
        float dist = delta.Length();
        if (dist < 1e-9f)
        {
            return;
        }

        Vec3 axis = delta / dist;
        Vec3 rnA = Vec3.Cross(rA, axis);
        Vec3 rnB = Vec3.Cross(rB, axis);
        float k = invMassA + invMassB
            + Vec3.Dot(rnA, ContactSolver.ApplyInverseInertia(stateA.Rotation, invIA, rnA))
            + Vec3.Dot(rnB, ContactSolver.ApplyInverseInertia(stateB.Rotation, invIB, rnB));
        if (k <= 0.0f)
        {
            return;
        }

        Vec3 vA = stateA.LinearVelocity + Vec3.Cross(stateA.AngularVelocity, rA);
        Vec3 vB = stateB.LinearVelocity + Vec3.Cross(stateB.AngularVelocity, rB);
        float lambda = -(Vec3.Dot(axis, vA - vB)) / k;
        joint.Impulse += lambda;

        ApplyLinearRow(ref stateA, ref stateB, awakeA, awakeB, invMassA, invMassB, invIA, invIB, rA, rB, axis, lambda);
    }

    private static void SolveDistancePosition(BodyStore bodies, ref DistanceJointData joint)
    {
        ref BodyState stateA = ref bodies.Slot(joint.BodyA);
        ref BodyState stateB = ref bodies.Slot(joint.BodyB);
        InverseMasses(ref stateA, ref stateB, out bool awakeA, out bool awakeB,
            out float invMassA, out float invMassB, out _, out _);
        if ((!awakeA && !awakeB) || (invMassA + invMassB) <= 0.0f)
        {
            return;
        }

        AnchorFrame(ref stateA, joint.LocalAnchorA, out Vec3 pa, out _, out _);
        AnchorFrame(ref stateB, joint.LocalAnchorB, out Vec3 pb, out _, out _);

        Vec3 delta = pa - pb;
        float dist = delta.Length();
        if (dist < 1e-9f)
        {
            return;
        }

        float correction = PositionBeta * (dist - joint.Length) / ((invMassA + invMassB) * dist);
        if (awakeA)
        {
            stateA.Position -= delta * (correction * invMassA);
        }

        if (awakeB)
        {
            stateB.Position += delta * (correction * invMassB);
        }
    }

    private static void SolveBallSocketVelocity(BodyStore bodies, ref BallSocketJointData joint)
    {
        ref BodyState stateA = ref bodies.Slot(joint.BodyA);
        ref BodyState stateB = ref bodies.Slot(joint.BodyB);
        InverseMasses(ref stateA, ref stateB, out bool awakeA, out bool awakeB,
            out float invMassA, out float invMassB, out Vec3 invIA, out Vec3 invIB);

        AnchorFrame(ref stateA, joint.LocalAnchorA, out _, out _, out Vec3 rA);
        AnchorFrame(ref stateB, joint.LocalAnchorB, out _, out _, out Vec3 rB);

        BuildPointMatrix(invMassA, invMassB, invIA, invIB,
            stateA.Rotation, stateB.Rotation, rA, rB,
            out Vec3 col0, out Vec3 col1, out Vec3 col2);

        Vec3 vA = stateA.LinearVelocity + Vec3.Cross(stateA.AngularVelocity, rA);
        Vec3 vB = stateB.LinearVelocity + Vec3.Cross(stateB.AngularVelocity, rB);
        Vec3 bias = vA - vB;
        Vec3 lambda = SolveSymmetric3x3(col0, col1, col2, bias * -1.0f);
        joint.Impulse += lambda;

        ApplyPointImpulse(ref stateA, ref stateB, awakeA, awakeB, invMassA, invMassB, invIA, invIB, rA, rB, lambda);
    }

    private static void SolveBallSocketPosition(BodyStore bodies, ref BallSocketJointData joint)
    {
        ref BodyState stateA = ref bodies.Slot(joint.BodyA);
        ref BodyState stateB = ref bodies.Slot(joint.BodyB);
        InverseMasses(ref stateA, ref stateB, out bool awakeA, out bool awakeB,
            out float invMassA, out float invMassB, out _, out _);
        if ((!awakeA && !awakeB) || (invMassA + invMassB) <= 0.0f)
        {
            return;
        }

        AnchorFrame(ref stateA, joint.LocalAnchorA, out Vec3 pa, out _, out _);
        AnchorFrame(ref stateB, joint.LocalAnchorB, out Vec3 pb, out _, out _);

        Vec3 error = (pa - pb) * (PositionBeta / (invMassA + invMassB));
        if (awakeA)
        {
            stateA.Position -= error * invMassA;
        }

        if (awakeB)
        {
            stateB.Position += error * invMassB;
        }
    }

    private static void SolveWeldVelocity(BodyStore bodies, ref WeldJointData joint)
    {
        ref BodyState stateA = ref bodies.Slot(joint.BodyA);
        ref BodyState stateB = ref bodies.Slot(joint.BodyB);
        InverseMasses(ref stateA, ref stateB, out bool awakeA, out bool awakeB,
            out float invMassA, out float invMassB, out Vec3 invIA, out Vec3 invIB);

        AnchorFrame(ref stateA, joint.LocalAnchorA, out _, out _, out Vec3 rA);
        AnchorFrame(ref stateB, joint.LocalAnchorB, out _, out _, out Vec3 rB);

        BuildPointMatrix(invMassA, invMassB, invIA, invIB,
            stateA.Rotation, stateB.Rotation, rA, rB,
            out Vec3 col0, out Vec3 col1, out Vec3 col2);

        Vec3 vA = stateA.LinearVelocity + Vec3.Cross(stateA.AngularVelocity, rA);
        Vec3 vB = stateB.LinearVelocity + Vec3.Cross(stateB.AngularVelocity, rB);
        Vec3 linear = SolveSymmetric3x3(col0, col1, col2, (vA - vB) * -1.0f);
        joint.LinearImpulse += linear;
        ApplyPointImpulse(ref stateA, ref stateB, awakeA, awakeB, invMassA, invMassB, invIA, invIB, rA, rB, linear);

        BuildAngularMatrix(invIA, invIB, stateA.Rotation, stateB.Rotation,
            out Vec3 a0, out Vec3 a1, out Vec3 a2);
        Vec3 angular = SolveSymmetric3x3(a0, a1, a2, (stateA.AngularVelocity - stateB.AngularVelocity) * -1.0f);
        joint.AngularImpulse += angular;

        if (awakeA)
        {
            stateA.AngularVelocity += ContactSolver.ApplyInverseInertia(stateA.Rotation, invIA, angular);
        }

        if (awakeB)
        {
            stateB.AngularVelocity -= ContactSolver.ApplyInverseInertia(stateB.Rotation, invIB, angular);
        }
    }

    private static void SolveWeldPosition(BodyStore bodies, ref WeldJointData joint)
    {
        var point = new BallSocketJointData
        {
            BodyA = joint.BodyA,
            BodyB = joint.BodyB,
            LocalAnchorA = joint.LocalAnchorA,
            LocalAnchorB = joint.LocalAnchorB,
        };
        SolveBallSocketPosition(bodies, ref point);

        ref BodyState stateA = ref bodies.Slot(joint.BodyA);
        ref BodyState stateB = ref bodies.Slot(joint.BodyB);
        InverseMasses(ref stateA, ref stateB, out bool awakeA, out bool awakeB,
            out _, out _, out Vec3 invIA, out Vec3 invIB);
        if (!awakeA && !awakeB)
        {
            return;
        }

        Quat desired = stateA.Rotation * joint.ReferenceRotation;
        Quat error = stateB.Rotation.Conjugate() * desired;
        if (error.W < 0.0f)
        {
            error = new Quat(error.X * -1.0f, error.Y * -1.0f, error.Z * -1.0f, error.W * -1.0f);
        }

        Vec3 axis = new(error.X, error.Y, error.Z);
        float axisLen = axis.Length();
        if (axisLen < 1e-9f)
        {
            return;
        }

        float angle = 2.0f * MathF.Atan2(axisLen, error.W) * PositionBeta;
        Vec3 spin = (axis / axisLen) * angle;

        float weightA = awakeA ? 1.0f : 0.0f;
        float weightB = awakeB ? 1.0f : 0.0f;
        float total = weightA + weightB;
        if (awakeA)
        {
            stateA.Rotation = (Quat.CreateFromAxisAngle(spin / angle * -1.0f, angle * (weightA / total)) * stateA.Rotation).Normalize();
        }

        if (awakeB)
        {
            stateB.Rotation = (Quat.CreateFromAxisAngle(spin / angle, angle * (weightB / total)) * stateB.Rotation).Normalize();
        }
    }

    /// <summary>Builds the symmetric 3x3 point-constraint matrix as columns.</summary>
    private static void BuildPointMatrix(
        float invMassA, float invMassB, Vec3 invIA, Vec3 invIB,
        Quat rotA, Quat rotB, Vec3 rA, Vec3 rB,
        out Vec3 col0, out Vec3 col1, out Vec3 col2)
    {
        col0 = MatrixColumn(invMassA, invMassB, invIA, invIB, rotA, rotB, rA, rB, new Vec3(1, 0, 0));
        col1 = MatrixColumn(invMassA, invMassB, invIA, invIB, rotA, rotB, rA, rB, new Vec3(0, 1, 0));
        col2 = MatrixColumn(invMassA, invMassB, invIA, invIB, rotA, rotB, rA, rB, new Vec3(0, 0, 1));
    }

    /// <summary>One column of the point-constraint matrix along a basis vector.</summary>
    private static Vec3 MatrixColumn(
        float invMassA, float invMassB, Vec3 invIA, Vec3 invIB,
        Quat rotA, Quat rotB, Vec3 rA, Vec3 rB, Vec3 basis)
    {
        Vec3 angular = Vec3.Cross(rA, ContactSolver.ApplyInverseInertia(rotA, invIA, Vec3.Cross(rA, basis)))
            + Vec3.Cross(rB, ContactSolver.ApplyInverseInertia(rotB, invIB, Vec3.Cross(rB, basis)));
        return (basis * (invMassA + invMassB)) + angular;
    }

    /// <summary>Builds the symmetric 3x3 angular-constraint matrix as columns.</summary>
    private static void BuildAngularMatrix(
        Vec3 invIA, Vec3 invIB, Quat rotA, Quat rotB,
        out Vec3 col0, out Vec3 col1, out Vec3 col2)
    {
        col0 = ContactSolver.ApplyInverseInertia(rotA, invIA, new Vec3(1, 0, 0))
            + ContactSolver.ApplyInverseInertia(rotB, invIB, new Vec3(1, 0, 0));
        col1 = ContactSolver.ApplyInverseInertia(rotA, invIA, new Vec3(0, 1, 0))
            + ContactSolver.ApplyInverseInertia(rotB, invIB, new Vec3(0, 1, 0));
        col2 = ContactSolver.ApplyInverseInertia(rotA, invIA, new Vec3(0, 0, 1))
            + ContactSolver.ApplyInverseInertia(rotB, invIB, new Vec3(0, 0, 1));
    }

    /// <summary>Solves a symmetric 3x3 system by Cramer's rule; singular systems return zero.</summary>
    private static Vec3 SolveSymmetric3x3(Vec3 col0, Vec3 col1, Vec3 col2, Vec3 rhs)
    {
        Vec3 cross12 = Vec3.Cross(col1, col2);
        float det = Vec3.Dot(col0, cross12);
        if (MathF.Abs(det) < 1e-18f)
        {
            return Vec3.Zero;
        }

        float invDet = 1.0f / det;
        return new Vec3(
            Vec3.Dot(rhs, cross12) * invDet,
            Vec3.Dot(col0, Vec3.Cross(rhs, col2)) * invDet,
            Vec3.Dot(col0, Vec3.Cross(col1, rhs)) * invDet);
    }

    /// <summary>Applies a point impulse at the anchors.</summary>
    private static void ApplyPointImpulse(
        ref BodyState stateA, ref BodyState stateB, bool awakeA, bool awakeB,
        float invMassA, float invMassB, Vec3 invIA, Vec3 invIB,
        Vec3 rA, Vec3 rB, Vec3 impulse)
    {
        if (awakeA)
        {
            stateA.LinearVelocity += impulse * invMassA;
            stateA.AngularVelocity += ContactSolver.ApplyInverseInertia(stateA.Rotation, invIA, Vec3.Cross(rA, impulse));
        }

        if (awakeB)
        {
            stateB.LinearVelocity -= impulse * invMassB;
            stateB.AngularVelocity -= ContactSolver.ApplyInverseInertia(stateB.Rotation, invIB, Vec3.Cross(rB, impulse));
        }
    }

    /// <summary>Applies a scalar row impulse along an axis.</summary>
    private static void ApplyLinearRow(
        ref BodyState stateA, ref BodyState stateB, bool awakeA, bool awakeB,
        float invMassA, float invMassB, Vec3 invIA, Vec3 invIB,
        Vec3 rA, Vec3 rB, Vec3 axis, float lambda)
    {
        Vec3 impulse = axis * lambda;
        ApplyPointImpulse(ref stateA, ref stateB, awakeA, awakeB, invMassA, invMassB, invIA, invIB, rA, rB, impulse);
    }
}
