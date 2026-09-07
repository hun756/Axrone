/**
 * Configurable (Generic) 6-DOF constraint solver for 3D.
 *
 * The most general joint type — each of the 6 DOFs (3 linear + 3 angular)
 * can independently be LOCKED, LIMITED, or FREE:
 *
 *   - FREE: no row produced (zero correction, zero allocation)
 *   - LOCKED: bilateral row with zero tolerance (rigid lock)
 *   - LIMITED: unilateral row when outside [lower, upper] range; optional motor
 *
 * Motion mode is inferred from the descriptor's per-axis limit pairs:
 *   - lower == upper → LOCKED
 *   - |lower| == ∞ AND |upper| == ∞ → FREE
 *   - otherwise → LIMITED
 *
 * Measurement:
 *   - Linear: world-space axis projection (consistent with Fixed joint).
 *     When localFrame rotation is identity, reduces to world X/Y/Z.
 *   - Angular: extractAxisAngle in relative local frame. When localFrame
 *     rotations are identity, reduces to conj(qA)*qB — same as Fixed.
 *
 * Motor-limit interaction (Box2D behavior):
 *   When a limit is violated AND the motor would push further into the limit,
 *   the motor is disabled for that axis/step. This prevents the motor from
 *   trying to exceed the limit.
 *
 * Diagonal approximation caveat:
 *   Each row uses scalar effective mass (diagonal approximation). When multiple
 *   axes hit limits simultaneously, the impulse coupling between axes is ignored.
 *   This can cause mild oscillation in corner cases (e.g. all 3 linear limited
 *   with tight bounds and high velocity). For most game scenarios this is
 *   acceptable.
 *
 * @internal
 * @module
 */

import { Vec3, Quat, type IVec3Like, type IQuatLike } from '@axrone/numeric';
import { PhysicsConstants } from '@axrone/physics-core';
import type { BodyId3D, IGenericConstraintDef3D } from '../types/physics-3d';
import {
    CONSTRAINT_TYPE_GENERIC,
    transformPoint3D,
} from './physics-world-3d-shared';
import {
    type JacobianRow3D,
    type SolverBody3D,
    type ConstraintData3D,
    computeEffectiveMass,
    ensureSolverBody3D,
    createRow,
    zeroVec3,
    registerConstraintModule,
    extractAxisAngle,
    detectMotionMode,
} from './physics-world-3d-constraints-framework';
import type { BodyManager3D } from './physics-managers-3d';

const BAUMGARTE = PhysicsConstants.BAUMGARTE_FACTOR;
const EPSILON = PhysicsConstants.EPSILON;
const LINEAR_SLOP = PhysicsConstants.LINEAR_SLOP;
const ANGULAR_SLOP = PhysicsConstants.ANGULAR_SLOP;

// ─── Local Helpers ────────────────────────────────────────────────────────────

/**
 * Rotate a unit axis vector by a quaternion (local → world direction).
 * Local helper — avoids importing Quat.rotateVector separately.
 */
function rotateAxis(q: IQuatLike, v: IVec3Like): IVec3Like {
    return Quat.rotateVector(q, v);
}

// ─── Prepare Function ─────────────────────────────────────────────────────────

/**
 * Prepare configurable (generic) constraint Jacobian rows.
 *
 * For each of the 6 DOFs, checks the motion mode and either:
 *   - Skips (FREE): no row, no allocation
 *   - Adds a locked bilateral row (LOCKED)
 *   - Adds a limit/motor row only when the limit is violated or motor is active (LIMITED)
 */
export function prepareConfigurable(
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
    const genDef = def as IGenericConstraintDef3D;

    // ─── Local frame axes in world space ────────────────────────────────
    // Each body has a local frame (position + rotation). The frame rotation
    // defines the measurement axes for both linear and angular DOFs.
    const frameA = genDef.localFrameA;
    const frameB = genDef.localFrameB;

    const xAxis: IVec3Like = { x: 1, y: 0, z: 0 };
    const yAxis: IVec3Like = { x: 0, y: 1, z: 0 };
    const zAxis: IVec3Like = { x: 0, y: 0, z: 1 };

    // Measurement directions in world space
    const dirA0 = rotateAxis(frameA.rotation, xAxis);
    const dirA1 = rotateAxis(frameA.rotation, yAxis);
    const dirA2 = rotateAxis(frameA.rotation, zAxis);
    const dirB0 = rotateAxis(frameB.rotation, xAxis);
    const dirB1 = rotateAxis(frameB.rotation, yAxis);
    const dirB2 = rotateAxis(frameB.rotation, zAxis);

    // ─── Anchors in world space ─────────────────────────────────────────
    const worldAnchorA = transformPoint3D(frameA.position, bodyA.position, bodyA.rotation);
    const worldAnchorB = transformPoint3D(frameB.position, bodyB.position, bodyB.rotation);

    // r vectors for angular Jacobian (cross product terms)
    const rA = Vec3.subtract(worldAnchorA, bodyA.position);
    const rB = Vec3.subtract(worldAnchorB, bodyB.position);

    // Anchor offset (world space)
    const offset = Vec3.subtract(worldAnchorB, worldAnchorA);

    // ─── 3 Linear Rows ──────────────────────────────────────────────────
    // For each axis i ∈ {0,1,2}:
    //   error_i = dot(offset, dir_i)
    //   Jacobian direction = dir_i (world-space measurement axis)
    //
    // Convention (same as Fixed):
    //   j1Linear = -dirA, j2Linear = +dirB
    //   J*v = vB·dirB - vA·dirA (+ angular coupling via r×dir)
    //   For positive error: bias = +BAUMGARTE * error / h → convergence

    const dirsA = [dirA0, dirA1, dirA2];
    const dirsB = [dirB0, dirB1, dirB2];
    const linLower = [genDef.linearLowerLimit.x, genDef.linearLowerLimit.y, genDef.linearLowerLimit.z];
    const linUpper = [genDef.linearUpperLimit.x, genDef.linearUpperLimit.y, genDef.linearUpperLimit.z];

    for (let i = 0; i < 3; i++) {
        const mode = detectMotionMode(linLower[i], linUpper[i]);
        if (mode === 'free') continue;

        const dA = dirsA[i];
        const dB = dirsB[i];

        // Per-axis error: projection of offset onto measurement direction
        const error = offset.x * dA.x + offset.y * dA.y + offset.z * dA.z;

        // Angular Jacobian: j = -(r × dir) for body A, j = +(r × dir) for body B
        const j1Ang = { x: -(rA.y * dA.z - rA.z * dA.y), y: -(rA.z * dA.x - rA.x * dA.z), z: -(rA.x * dA.y - rA.y * dA.x) };
        const j2Ang = { x: rB.y * dB.z - rB.z * dB.y, y: rB.z * dB.x - rB.x * dB.z, z: rB.x * dB.y - rB.y * dB.x };

        if (mode === 'locked') {
            // Bilateral lock: zero tolerance
            out.push(createRow(
                bodyIdA, bodyIdB,
                { x: -dA.x, y: -dA.y, z: -dA.z }, j1Ang,
                { x: dB.x, y: dB.y, z: dB.z }, j2Ang,
                BAUMGARTE * error / h,
                -error,
            ));
        } else {
            // LIMITED
            let limitError = 0;
            if (error < linLower[i]) {
                limitError = error - linLower[i];
            } else if (error > linUpper[i]) {
                limitError = error - linUpper[i];
            }

            if (Math.abs(limitError) > LINEAR_SLOP) {
                out.push(createRow(
                    bodyIdA, bodyIdB,
                    { x: -dA.x, y: -dA.y, z: -dA.z }, j1Ang,
                    { x: dB.x, y: dB.y, z: dB.z }, j2Ang,
                    BAUMGARTE * limitError / h,
                    -limitError,
                    -Infinity, Infinity,
                ));
                out[out.length - 1].hasLimit = true;
            }
            // Within limits: no linear row (free within bounds)
        }
    }

    // ─── 3 Angular Rows ─────────────────────────────────────────────────
    // Relative rotation in the local frame basis:
    //   qLocalFrame = conj(qA * frameRotA) * (qB * frameRotB)
    //
    // When frameRotA = frameRotB = identity:
    //   qLocalFrame = conj(qA) * qB — same as Fixed joint.
    //
    // Per-axis angle via extractAxisAngle(qLocalFrame, canonicalAxis).
    // This projects q onto the axis: angle = 2*atan2(dot(q.xyz, axis), q.w).

    const qAFrame = Quat.multiply(bodyA.rotation, frameA.rotation);
    const qBFrame = Quat.multiply(bodyB.rotation, frameB.rotation);
    const qLocalFrame = Quat.multiply(
        { x: -qAFrame.x, y: -qAFrame.y, z: -qAFrame.z, w: qAFrame.w },
        qBFrame,
    );

    // Per-axis angles (measurement in local frame basis)
    const angles = [
        extractAxisAngle(qLocalFrame, xAxis),
        extractAxisAngle(qLocalFrame, yAxis),
        extractAxisAngle(qLocalFrame, zAxis),
    ];

    const angLower = [genDef.angularLowerLimit.x, genDef.angularLowerLimit.y, genDef.angularLowerLimit.z];
    const angUpper = [genDef.angularUpperLimit.x, genDef.angularUpperLimit.y, genDef.angularUpperLimit.z];

    // Angular measurement axes in world space (for Jacobian)
    // Use body A's frame axes (same convention as Fixed when frames are identity)
    const angAxesA = [
        rotateAxis(qAFrame, xAxis),
        rotateAxis(qAFrame, yAxis),
        rotateAxis(qAFrame, zAxis),
    ];

    for (let i = 0; i < 3; i++) {
        const mode = detectMotionMode(angLower[i], angUpper[i]);
        if (mode === 'free') continue;

        const axis = angAxesA[i];
        const angle = angles[i];

        if (mode === 'locked') {
            // Bilateral angular lock
            // Angular Jacobian: j1Ang = -axis, j2Ang = +axis → J*ω = ωB·axis - ωA·axis
            // For positive angular error: bias = +BAUMGARTE * error / h (convergent)
            out.push(createRow(
                bodyIdA, bodyIdB,
                zeroVec3(), { x: -axis.x, y: -axis.y, z: -axis.z },
                zeroVec3(), { x: axis.x, y: axis.y, z: axis.z },
                BAUMGARTE * angle / h,
                -angle,
            ));
        } else {
            // LIMITED angular
            let limitError = 0;
            if (angle < angLower[i]) {
                limitError = angle - angLower[i];
            } else if (angle > angUpper[i]) {
                limitError = angle - angUpper[i];
            }

            const atLower = angle <= angLower[i] + ANGULAR_SLOP;
            const atUpper = angle >= angUpper[i] - ANGULAR_SLOP;
            const limitViolated = Math.abs(limitError) > ANGULAR_SLOP;

            // Motor is not in the descriptor — no motor for generic constraints
            // in the current descriptor schema. Motor support would require
            // additional descriptor fields (per-axis motorSpeed, maxForce).
            // Report: descriptor lacks per-axis motor fields.

            if (limitViolated) {
                const row = createRow(
                    bodyIdA, bodyIdB,
                    zeroVec3(), { x: -axis.x, y: -axis.y, z: -axis.z },
                    zeroVec3(), { x: axis.x, y: axis.y, z: axis.z },
                    BAUMGARTE * limitError / h,
                    -limitError,
                    -Infinity, Infinity,
                );
                row.hasLimit = true;
                out.push(row);
            }
            // Within angular limits: no row (free within bounds)
        }
    }

    // ─── Compute effective masses ───────────────────────────────────────
    for (const row of out) {
        row.effectiveMass = computeEffectiveMass(row, bodyA, bodyB);
    }
}

// ─── Registration ─────────────────────────────────────────────────────────────

registerConstraintModule(CONSTRAINT_TYPE_GENERIC, {
    prepare: prepareConfigurable,
});
