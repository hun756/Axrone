/**
 * Cone-twist (Character) constraint solver for 3D.
 *
 * A cone-twist joint locks the anchor points (3 linear DOF), allows rotation
 * about a designated twist axis within independent limits, and restricts the
 * remaining swing rotation to a cone around that axis. There is no 2D
 * counterpart — this solver is written from scratch on the Jacobian-row
 * framework (physics-world-3d-constraints-framework.ts).
 *
 * Rows (6):
 *   1-3. Linear rows: anchor alignment (same pattern as Fixed/Hinge).
 *   4.   Swing limit row: unilateral row along the world swing direction that
 *        resists growth of the axis-independent total swing deviation
 *        `swingAngle` beyond the cone limit (inscribed cone of swingSpan1/2).
 *   5.   Twist motor row (optional): drives the twist rate toward motorSpeed,
 *        impulse capped by maxMotorTorque * h.
 *   6.   Twist limit row: unilateral row on the twist axis, active near/beyond
 *        the twist span. Solved AFTER the motor row so the limit strips any
 *        motor impulse that would push past the rim (Box2D motor-stall
 *        semantics). This is why limit and motor are separate rows: a single
 *        merged row cannot be simultaneously rigid (limit) and torque-capped
 *        (motor) — the exact deficiency the hinge implementation carries.
 *
 * Quaternion swing-twist decomposition:
 *   With qRel = conj(frameA_world) * frameB_world (identity when the joint
 *   frames coincide), the rotation is split about the frame X axis (the twist
 *   axis, Bullet convention):
 *     qTwist = normalize((dot(qRel.xyz, X), 0, 0, qRel.w))
 *     qSwing = qRel * conj(qTwist)
 *   - twistAngle = 2 * atan2(qRel.x, qRel.w)            (signed, [-π, π])
 *   - swingAngle = 2 * atan2(|qSwing.xyz|, qSwing.w)    (total, [0, π])
 *     — an axis-INDEPENDENT measure of the total deviation from the twist
 *       axis (not a single-axis projection like the hinge angle).
 *   qSwing.xyz is orthogonal to the twist axis by construction, so qSwing is
 *   a pure swing rotation.
 *
 * Cone apex singularity:
 *   At swingAngle ≈ 0 the swing direction is undefined (classic cone-apex
 *   singularity). The swing limit row is SKIPPED when swingAngle <= EPSILON —
 *   no NaN, no throw. Ragdoll character joints idle exactly in this region;
 *   the row re-engages automatically once the swing exceeds EPSILON.
 *
 * Rim restoration policy (empirically driven):
 *   While a limit is actually VIOLATED its row is bilateral: Baumgarte
 *   restoration must brake the residual approach velocity, not only push the
 *   joint back into range. A permanently one-sided clamp freezes the
 *   corrective velocity once the bias decays and lets the joint coast across
 *   the whole span to the opposite rim (observed in step traces). Inside the
 *   unviolated margin zone (within ANGULAR_SLOP of the rim) the row stays
 *   one-sided, so free motion away from the rim is never resisted.
 *
 * Bias sign convention:
 *   This module uses the EMPIRICALLY VERIFIED convergent convention for the
 *   framework kernel: solveVelocityRow drives J*v to -bias, so a violation V
 *   (whose rate of change equals J*v) is corrected with bias = +BAUMGARTE*V/h
 *   (target rate -BAUMGARTE*V/h decays the violation). Verified against the
 *   kernel: gap 1.0 -> 0.8 with +, diverges 1.0 -> 1.2 with -.
 *
 * Def mapping (IConeTwistConstraintDef3D):
 *   - localFrameA/B.position -> anchor points (transformed by BODY rotation).
 *   - localFrameA/B.rotation -> joint frames; the frame X axis is the twist
 *     axis (world axis taken from frame A).
 *   - swingSpan1/swingSpan2 -> cone half-angles about the two perpendicular
 *     axes. The solver enforces the INSCRIBED circular cone
 *     coneLimit = min(span1, span2) — conservative (never escapes the
 *     elliptical cone). Undefined spans mean no swing limit.
 *   - twistSpan -> symmetric twist range [-span/2, +span/2] (the def carries
 *     only the span; the center is zero). Undefined means free twist.
 *   - softness -> framework softness regularization on all rows.
 *   - biasFactor / relaxationFactor are NOT used; the framework standard
 *     PhysicsConstants.BAUMGARTE_FACTOR keeps every module uniform.
 *
 * Warm starting: intentionally not used (framework decision); rows and
 * accumulated impulses are rebuilt every step in prepare.
 *
 * Allocation: prepare allocates (once per step); the solve iterations only
 * touch row fields through the framework kernel (zero allocation).
 *
 * @internal — Not exported from barrel. Internal to physics-3d.
 * @module
 */

import { Vec3, Quat, type IVec3Like, type IQuatLike } from '@axrone/numeric';
import { PhysicsConstants } from '@axrone/physics-core';
import type { BodyId3D, IConeTwistConstraintDef3D } from '../types/physics-3d';
import {
    CONSTRAINT_TYPE_CONE_TWIST,
    transformPoint3D,
} from './physics-world-3d-shared';
import {
    type JacobianRow3D,
    type SolverBody3D,
    type ConstraintData3D,
    type ISwingTwistSplit,
    computeEffectiveMass,
    ensureSolverBody3D,
    createRow,
    zeroVec3,
    registerConstraintModule,
    relativeQuaternion,
    decomposeSwingTwist,
} from './physics-world-3d-constraints-framework';
import type { BodyManager3D } from './physics-managers-3d';

const BAUMGARTE = PhysicsConstants.BAUMGARTE_FACTOR;
const EPSILON = PhysicsConstants.EPSILON;
const ANGULAR_SLOP = PhysicsConstants.ANGULAR_SLOP;
const MAX_ANGULAR_CORRECTION = PhysicsConstants.MAX_ANGULAR_CORRECTION;

/** The twist axis in joint-frame space: the frame's local X axis. */
const TWIST_AXIS_FRAME: IVec3Like = { x: 1, y: 0, z: 0 };

// ─── Module-level prepare scratch (prepare runs once per step, non-reentrant) ─

const _split: ISwingTwistSplit = {
    swingAngle: 0,
    twistAngle: 0,
    swingDirFrame: { x: 0, y: 0, z: 0 },
    hasSwingDirection: false,
};
const _frameARot: IQuatLike = { x: 0, y: 0, z: 0, w: 1 };
const _frameBRot: IQuatLike = { x: 0, y: 0, z: 0, w: 1 };
const _qRel: IQuatLike = { x: 0, y: 0, z: 0, w: 1 };
const _twistAxisW: IVec3Like = { x: 1, y: 0, z: 0 };
const _swingDirW: IVec3Like = { x: 0, y: 0, z: 0 };
const _rA: IVec3Like = { x: 0, y: 0, z: 0 };
const _rB: IVec3Like = { x: 0, y: 0, z: 0 };

// ─── Prepare ──────────────────────────────────────────────────────────────────

/**
 * Prepare cone-twist constraint Jacobian rows (once per step).
 *
 * Row order matters: the twist MOTOR row precedes the twist LIMIT row so that
 * within every sequential-impulse iteration the limit sees the post-motor
 * velocities and strips any component that would push past the rim.
 */
export function prepareConeTwist(
    data: ConstraintData3D,
    solverBodies: Map<BodyId3D, SolverBody3D>,
    bodyManager: BodyManager3D,
    dt: number,
    out: JacobianRow3D[],
): void {
    const { bodyIdA, bodyIdB, def } = data;
    const bodyA = ensureSolverBody3D(bodyIdA, bodyManager, solverBodies);
    const bodyB = ensureSolverBody3D(bodyIdB, bodyManager, solverBodies);

    const h = Math.max(dt, EPSILON);
    const coneDef = def as IConeTwistConstraintDef3D;
    const softness = coneDef.softness ?? 0;

    // ─── World frames, twist axis, and swing-twist decomposition ──────
    Quat.multiply(bodyA.rotation, coneDef.localFrameA.rotation, _frameARot);
    Quat.multiply(bodyB.rotation, coneDef.localFrameB.rotation, _frameBRot);
    relativeQuaternion(_frameARot, _frameBRot, _qRel);
    decomposeSwingTwist(_qRel, _split);

    // World twist axis = frame A's X axis in world space (frame A is the
    // reference frame, matching Bullet's cone-twist convention).
    const twistAxisLen = Vec3.len(Quat.rotateVector(_frameARot, TWIST_AXIS_FRAME, _twistAxisW));
    if (twistAxisLen > EPSILON) {
        _twistAxisW.x /= twistAxisLen;
        _twistAxisW.y /= twistAxisLen;
        _twistAxisW.z /= twistAxisLen;
    } else {
        // Degenerate frame rotation — fall back to world X (no NaN).
        _twistAxisW.x = 1; _twistAxisW.y = 0; _twistAxisW.z = 0;
    }

    // World swing direction (frame A space -> world).
    let hasSwingDir = false;
    if (_split.hasSwingDirection) {
        Quat.rotateVector(_frameARot, _split.swingDirFrame, _swingDirW);
        const swingDirLen = Vec3.len(_swingDirW);
        if (swingDirLen > EPSILON) {
            _swingDirW.x /= swingDirLen;
            _swingDirW.y /= swingDirLen;
            _swingDirW.z /= swingDirLen;
            hasSwingDir = true;
        }
    }

    // ─── 3 Linear Rows (anchor alignment) ─────────────────────────────
    // Correct point-constraint kinematics: C = (pB + ωB×rB) - (pA + ωA×rA),
    // so j1Linear = -n, j1Angular = -(rA × n), j2Linear = +n, j2Angular = +(rB × n).
    // (The angular signs match computeEffectiveMass; see module doc.)
    const worldAnchorA = transformPoint3D(coneDef.localFrameA.position, bodyA.position, bodyA.rotation);
    const worldAnchorB = transformPoint3D(coneDef.localFrameB.position, bodyB.position, bodyB.rotation);
    _rA.x = worldAnchorA.x - bodyA.position.x;
    _rA.y = worldAnchorA.y - bodyA.position.y;
    _rA.z = worldAnchorA.z - bodyA.position.z;
    _rB.x = worldAnchorB.x - bodyB.position.x;
    _rB.y = worldAnchorB.y - bodyB.position.y;
    _rB.z = worldAnchorB.z - bodyB.position.z;

    const axes: readonly IVec3Like[] = [
        { x: 1, y: 0, z: 0 },
        { x: 0, y: 1, z: 0 },
        { x: 0, y: 0, z: 1 },
    ];
    for (const n of axes) {
        const rAxN = Vec3.cross(_rA, n);
        const rBxN = Vec3.cross(_rB, n);
        const error =
            (worldAnchorB.x - worldAnchorA.x) * n.x +
            (worldAnchorB.y - worldAnchorA.y) * n.y +
            (worldAnchorB.z - worldAnchorA.z) * n.z;

        const row = createRow(
            bodyIdA, bodyIdB,
            { x: -n.x, y: -n.y, z: -n.z },
            { x: -rAxN.x, y: -rAxN.y, z: -rAxN.z },
            { x: n.x, y: n.y, z: n.z },
            { x: rBxN.x, y: rBxN.y, z: rBxN.z },
            BAUMGARTE * error / h,
            -error,
        );
        row.softness = softness;
        out.push(row);
    }

    // ─── Swing Limit Row (total deviation vs inscribed cone) ──────────
    // Active within ANGULAR_SLOP of the rim (unilateral velocity guard) and
    // beyond it (Baumgarte pull-back). Skipped at the cone apex.
    const coneLimit = resolveConeLimit(coneDef);
    if (coneLimit !== null && hasSwingDir && _split.swingAngle > coneLimit - ANGULAR_SLOP) {
        const violation = Math.max(0, _split.swingAngle - coneLimit);
        const clampedViolation = Math.min(violation, MAX_ANGULAR_CORRECTION);
        // Bilateral while actually violated (restoration must brake residual
        // velocity); one-sided inside the unviolated margin zone.
        const violated = violation > 0;
        const row = createRow(
            bodyIdA, bodyIdB,
            zeroVec3(),
            { x: -_swingDirW.x, y: -_swingDirW.y, z: -_swingDirW.z },
            zeroVec3(),
            { x: _swingDirW.x, y: _swingDirW.y, z: _swingDirW.z },
            BAUMGARTE * clampedViolation / h,
            -clampedViolation,
            -Infinity, violated ? Infinity : 0,
        );
        row.hasLimit = true;
        row.softness = softness;
        out.push(row);
    }

    // ─── Twist Motor Row (optional, before the limit row) ─────────────
    const twistSpan = coneDef.twistSpan;
    const hasTwistLimit = twistSpan !== undefined
        && isFinite(twistSpan) && twistSpan < 2 * Math.PI - 2 * ANGULAR_SLOP;
    const twistLower = hasTwistLimit ? -twistSpan / 2 : 0;
    const twistUpper = hasTwistLimit ? +twistSpan / 2 : 0;

    // Motor: drives the twist rate toward motorSpeed (equilibrium J*v = -bias).
    // Motor fields are defined in IConeTwistConstraintDef3D (motorSpeed,
    // maxMotorTorque). When both are provided and maxMotorTorque > 0, the
    // motor row is produced.
    const motorSpeed = coneDef.motorSpeed;
    const maxMotorTorque = coneDef.maxMotorTorque;
    const enableTwistMotor = motorSpeed !== undefined
        && maxMotorTorque !== undefined
        && Math.abs(maxMotorTorque) > EPSILON;

    if (enableTwistMotor) {
        const maxImpulse = Math.abs(maxMotorTorque!) * h;
        const row = createRow(
            bodyIdA, bodyIdB,
            zeroVec3(),
            { x: -_twistAxisW.x, y: -_twistAxisW.y, z: -_twistAxisW.z },
            zeroVec3(),
            { x: _twistAxisW.x, y: _twistAxisW.y, z: _twistAxisW.z },
            -(motorSpeed!),
            0,
            -maxImpulse, maxImpulse,
        );
        row.hasMotor = true;
        row.softness = softness;
        out.push(row);
    }

    // ─── Twist Limit Row (after the motor row → Box2D motor-stall) ────
    if (hasTwistLimit) {
        if (twistUpper - twistLower < 2 * ANGULAR_SLOP) {
            // Degenerate span: bilateral lock at the span midpoint.
            const mid = 0.5 * (twistLower + twistUpper);
            const error = _split.twistAngle - mid;
            const clampedError = Math.max(-MAX_ANGULAR_CORRECTION,
                Math.min(MAX_ANGULAR_CORRECTION, error));
            const row = createRow(
                bodyIdA, bodyIdB,
                zeroVec3(),
                { x: -_twistAxisW.x, y: -_twistAxisW.y, z: -_twistAxisW.z },
                zeroVec3(),
                { x: _twistAxisW.x, y: _twistAxisW.y, z: _twistAxisW.z },
                BAUMGARTE * clampedError / h,
                -clampedError,
            );
            row.hasLimit = true;
            row.softness = softness;
            out.push(row);
        } else if (_split.twistAngle > twistUpper - ANGULAR_SLOP) {
            // At/beyond the upper rim. Bilateral while violated (restoration
            // must brake residual velocity), one-sided in the margin zone.
            const violation = Math.max(0, _split.twistAngle - twistUpper);
            const clampedViolation = Math.min(violation, MAX_ANGULAR_CORRECTION);
            const violated = violation > 0;
            const row = createRow(
                bodyIdA, bodyIdB,
                zeroVec3(),
                { x: -_twistAxisW.x, y: -_twistAxisW.y, z: -_twistAxisW.z },
                zeroVec3(),
                { x: _twistAxisW.x, y: _twistAxisW.y, z: _twistAxisW.z },
                BAUMGARTE * clampedViolation / h,
                -clampedViolation,
                -Infinity, violated ? Infinity : 0,
            );
            row.hasLimit = true;
            row.softness = softness;
            out.push(row);
        } else if (_split.twistAngle < twistLower + ANGULAR_SLOP) {
            // At/beyond the lower rim. Bilateral while violated, one-sided
            // (resists twist reduction only) in the margin zone.
            const violation = Math.min(0, _split.twistAngle - twistLower);
            const clampedViolation = Math.max(-MAX_ANGULAR_CORRECTION, violation);
            const violated = violation < 0;
            const row = createRow(
                bodyIdA, bodyIdB,
                zeroVec3(),
                { x: -_twistAxisW.x, y: -_twistAxisW.y, z: -_twistAxisW.z },
                zeroVec3(),
                { x: _twistAxisW.x, y: _twistAxisW.y, z: _twistAxisW.z },
                BAUMGARTE * clampedViolation / h,
                -clampedViolation,
                violated ? -Infinity : 0, Infinity,
            );
            row.hasLimit = true;
            row.softness = softness;
            out.push(row);
        }
    }

    // ─── Compute effective masses ─────────────────────────────────────
    for (const row of out) {
        row.effectiveMass = computeEffectiveMass(row, bodyA, bodyB);
    }
}

/**
 * Resolve the inscribed circular cone half-angle from the swing spans.
 * Returns null when no swing limit is defined (free ball-socket swing).
 */
function resolveConeLimit(def: IConeTwistConstraintDef3D): number | null {
    const spans: number[] = [];
    if (def.swingSpan1 !== undefined && isFinite(def.swingSpan1)) {
        spans.push(Math.max(0, def.swingSpan1));
    }
    if (def.swingSpan2 !== undefined && isFinite(def.swingSpan2)) {
        spans.push(Math.max(0, def.swingSpan2));
    }
    if (spans.length === 0) return null;
    return Math.min(...spans);
}

// ─── Registration ─────────────────────────────────────────────────────────────

registerConstraintModule(CONSTRAINT_TYPE_CONE_TWIST, {
    prepare: prepareConeTwist,
});
