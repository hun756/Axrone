/**
 * 3D Constraint Solver Framework — Jacobian-row sequential impulse.
 *
 * Provides shared types, helper functions, and the sequential impulse kernel
 * for 3D joint constraint solving. Each joint type lives in its own module
 * (e.g. physics-world-3d-constraints-fixed.ts, physics-world-3d-constraints-hinge.ts)
 * and registers prepare/solve functions via the dispatch table.
 *
 * Architecture:
 * - prepareXxx() builds JacobianRow3D entries per constraint (once per step)
 * - solveVelocityRow() applies sequential impulse corrections to velocities
 * - solvePositionRow() applies Baumgarte position corrections
 *
 * Wave 3b decomposition: pure functions, state passed as parameters,
 * no `this` binding, no `as any`, no circular imports.
 *
 * @internal — Not exported from barrel. Internal to physics-3d.
 * @module
 */

import { Vec3, Quat, clamp, type IVec3Like, type IQuatLike } from '@axrone/numeric';
import { PhysicsConstants } from '@axrone/physics-core';
import type { BodyId3D, ConstraintId3D } from '../types/physics-3d';
import type { BodyManager3D, ConstraintManager3D } from './physics-managers-3d';
import {
    CONSTRAINT_TYPE_FIXED,
    CONSTRAINT_TYPE_HINGE,
    CONSTRAINT_TYPE_SLIDER,
    CONSTRAINT_TYPE_CONE_TWIST,
    CONSTRAINT_TYPE_GENERIC,
    CONSTRAINT_TYPE_SPRING,
    type SupportedConstraintDef3D,
} from './physics-world-3d-shared';
import { transformPoint3D } from './physics-world-3d-shared';

// ─── Constants ────────────────────────────────────────────────────────────────

const BAUMGARTE = PhysicsConstants.BAUMGARTE_FACTOR;
const EPSILON = PhysicsConstants.EPSILON;
const POSITION_SOLVER_TOLERANCE = 0.001;
/** Position correction bias — higher than typical because the velocity solve's
 *  Baumgarte bias can diverge position (moves bodies apart while correcting velocity).
 *  0.8 compensates by correcting 80% of the remaining error per position pass. */
const POSITION_BIAS_FACTOR = 0.8;

// ─── Types ────────────────────────────────────────────────────────────────────

/**
 * A single Jacobian row for a 3D constraint.
 *
 * Linear DOFs use scalar effective mass (J·M⁻¹·Jᵀ is scalar for linear).
 * Angular DOFs also use scalar effective mass here because each row
 * projects the 3D angular error onto a single axis, making the
 * effective mass scalar: j·(I⁻¹·jᵀ) where j is Vec3 and I⁻¹ is diagonal.
 */
export interface JacobianRow3D {
    bodyIdA: BodyId3D;
    bodyIdB: BodyId3D;
    /** Linear Jacobian for body A (world-space direction). */
    j1Linear: IVec3Like;
    /** Angular Jacobian for body A (world-space axis). */
    j1Angular: IVec3Like;
    /** Linear Jacobian for body B. */
    j2Linear: IVec3Like;
    /** Angular Jacobian for body B. */
    j2Angular: IVec3Like;
    /** Bias velocity (Baumgarte stabilization target). */
    bias: number;
    /** Accumulated impulse for sequential impulse clamping. */
    impulse: number;
    /** Lower impulse limit (-Infinity for bilateral). */
    lowerLimit: number;
    /** Upper impulse limit (+Infinity for bilateral). */
    upperLimit: number;
    /** Softness (regularization). 0 for rigid constraints. */
    softness: number;
    /** Position error for position solver (separate from velocity bias). */
    positionError: number;
    /** Effective mass (precomputed in prepare). Scalar because each row is 1-DOF. */
    effectiveMass: number;
    /** Whether this row has a limit. */
    hasLimit: boolean;
    /** Whether this row has a motor. */
    hasMotor: boolean;
}

/** Lightweight body state snapshot for the solver. */
export interface SolverBody3D {
    bodyId: BodyId3D;
    invMass: number;
    invInertia: IVec3Like;
    linearVelocity: IVec3Like;
    angularVelocity: IVec3Like;
    position: IVec3Like;
    rotation: IQuatLike;
}

/** Resolved constraint data for prepare functions. */
export interface ConstraintData3D {
    constraintId: ConstraintId3D;
    type: number;
    bodyIdA: BodyId3D;
    bodyIdB: BodyId3D;
    def: SupportedConstraintDef3D;
}

/** Prepare function signature. */
export type PrepareFn = (
    data: ConstraintData3D,
    solverBodies: Map<BodyId3D, SolverBody3D>,
    bodyManager: BodyManager3D,
    dt: number,
    out: JacobianRow3D[],
) => void;

// ─── Scratch vectors (module-level, zero allocation) ──────────────────────────

const _sA: IVec3Like = { x: 0, y: 0, z: 0 };
const _sB: IVec3Like = { x: 0, y: 0, z: 0 };
const _sC: IVec3Like = { x: 0, y: 0, z: 0 };
const _sD: IVec3Like = { x: 0, y: 0, z: 0 };
const _sE: IVec3Like = { x: 0, y: 0, z: 0 };
const _sF: IVec3Like = { x: 0, y: 0, z: 0 };

// ─── Solver Body Helpers ──────────────────────────────────────────────────────

/** Get or create a solver body snapshot from the body manager. */
export function ensureSolverBody3D(
    bodyId: BodyId3D,
    bodyManager: BodyManager3D,
    map: Map<BodyId3D, SolverBody3D>,
): SolverBody3D {
    const existing = map.get(bodyId);
    if (existing) return existing;

    const bodyType = bodyManager.getBodyType(bodyId);
    const isDynamic = bodyType === 2; // BODY_TYPE_DYNAMIC
    const isEnabled = bodyManager.isEnabled(bodyId);
    const isFixedRot = bodyManager.isFixedRotation(bodyId);

    const invMass = (isDynamic && isEnabled) ? bodyManager.getInverseMass(bodyId) : 0;
    const invInertia: IVec3Like = (isDynamic && isEnabled && !isFixedRot)
        ? bodyManager.getInverseInertia(bodyId)
        : { x: 0, y: 0, z: 0 };

    const body: SolverBody3D = {
        bodyId,
        invMass,
        invInertia,
        linearVelocity: { ...bodyManager.getLinearVelocity(bodyId) },
        angularVelocity: { ...bodyManager.getAngularVelocity(bodyId) },
        position: { ...bodyManager.getPosition(bodyId) },
        rotation: { ...bodyManager.getRotation(bodyId) },
    };
    map.set(bodyId, body);
    return body;
}

/** Write solver body velocities back to the body manager (after velocity solve). */
export function commitSolverVelocities3D(
    solverBodies: Map<BodyId3D, SolverBody3D>,
    bodyManager: BodyManager3D,
): void {
    for (const sb of solverBodies.values()) {
        bodyManager.setLinearVelocity(sb.bodyId, sb.linearVelocity);
        bodyManager.setAngularVelocity(sb.bodyId, sb.angularVelocity);
    }
}

/**
 * Sync solver body velocities FROM the body manager.
 * Called before each constraint iteration to ensure solver bodies reflect
 * the latest contact solve state (contacts modify body manager directly).
 */
export function syncSolverBodiesFromManager(
    solverBodies: Map<BodyId3D, SolverBody3D>,
    bodyManager: BodyManager3D,
): void {
    for (const sb of solverBodies.values()) {
        const vel = bodyManager.getLinearVelocity(sb.bodyId);
        sb.linearVelocity.x = vel.x;
        sb.linearVelocity.y = vel.y;
        sb.linearVelocity.z = vel.z;
        const angVel = bodyManager.getAngularVelocity(sb.bodyId);
        sb.angularVelocity.x = angVel.x;
        sb.angularVelocity.y = angVel.y;
        sb.angularVelocity.z = angVel.z;
    }
}

/** Write solver body state back to the body manager (after position solve). */
export function commitSolverBodies3D(
    solverBodies: Map<BodyId3D, SolverBody3D>,
    bodyManager: BodyManager3D,
): void {
    for (const sb of solverBodies.values()) {
        bodyManager.setLinearVelocity(sb.bodyId, sb.linearVelocity);
        bodyManager.setAngularVelocity(sb.bodyId, sb.angularVelocity);
        bodyManager.setPosition(sb.bodyId, sb.position);
        bodyManager.setRotation(sb.bodyId, sb.rotation);
    }
}

// ─── Quaternion → Axis-Angle Helpers ──────────────────────────────────────────

/**
 * Extract the signed angle of rotation about a world-space axis from a
 * relative quaternion. Uses the projection of the quaternion onto the axis.
 *
 * Method: project q onto axis → q_proj = (dot(q.xyz, axis), q.w).
 * Then angle = 2 * atan2(|q_proj.xyz|, q_proj.w) with sign from dot(q.xyz, axis).
 *
 * This avoids the acos branch cut at ±π and handles the wrap-around correctly.
 *
 * @param qRel - Relative rotation quaternion (quatB * conj(quatA) or similar)
 * @param axis - World-space hinge axis (unit vector)
 * @returns Signed angle in [-π, π]
 */
export function extractAxisAngle(qRel: IQuatLike, axis: IVec3Like): number {
    // Project quaternion onto the axis
    const dot = qRel.x * axis.x + qRel.y * axis.y + qRel.z * axis.z;
    // The projected scalar part
    const sinHalf = dot;
    // The projected vector magnitude (using full xyz)
    const cosHalf = qRel.w;
    // atan2 gives the full angle from half-angle representation
    const angle = 2.0 * Math.atan2(sinHalf, cosHalf);
    return angle;
}

/**
 * Compute the relative rotation quaternion: qRel = conj(qA) * qB.
 * This represents B's rotation relative to A's frame.
 */
export function relativeQuaternion(qA: IQuatLike, qB: IQuatLike, out: IQuatLike): IQuatLike {
    const conjA: IQuatLike = { x: -qA.x, y: -qA.y, z: -qA.z, w: qA.w };
    return Quat.multiply(conjA, qB, out as IQuatLike) ?? Quat.multiply(conjA, qB);
}

// ─── Orthonormal Basis ────────────────────────────────────────────────────────

/**
 * Build two vectors perpendicular to the given unit axis.
 * Uses the smallest-component trick for numerical stability.
 */
export function buildPerpendicularAxes(axis: IVec3Like, out1: IVec3Like, out2: IVec3Like): void {
    const ax = axis.x, ay = axis.y, az = axis.z;
    let perpX: number, perpY: number, perpZ: number;

    if (Math.abs(ax) < 0.9) {
        // Cross with (1,0,0)
        perpX = 0;
        perpY = -az;
        perpZ = ay;
    } else {
        // Cross with (0,1,0)
        perpX = az;
        perpY = 0;
        perpZ = -ax;
    }

    const len = Math.sqrt(perpX * perpX + perpY * perpY + perpZ * perpZ);
    if (len > EPSILON) {
        perpX /= len;
        perpY /= len;
        perpZ /= len;
    } else {
        perpX = 1; perpY = 0; perpZ = 0;
    }

    out1.x = perpX; out1.y = perpY; out1.z = perpZ;

    // Second perpendicular = axis × first
    out2.x = ay * perpZ - az * perpY;
    out2.y = az * perpX - ax * perpZ;
    out2.z = ax * perpY - ay * perpX;
}

// ─── Effective Mass ───────────────────────────────────────────────────────────

/**
 * Compute the effective mass for a single Jacobian row.
 *
 * For linear parts: J_lin · M⁻¹ · J_linᵀ = invMassA * |j1Lin|² + invMassB * |j2Lin|²
 * For angular parts: J_ang · I⁻¹ · J_angᵀ = Σ invI_i * j_ang_i²  (diagonal inertia)
 *
 * Since each row is 1-DOF (scalar constraint), the effective mass is scalar.
 * This is correct because we project the 3D angular error onto individual axes.
 */
export function computeEffectiveMass(
    row: JacobianRow3D,
    bodyA: SolverBody3D,
    bodyB: SolverBody3D,
): number {
    const j1l = row.j1Linear;
    const j1a = row.j1Angular;
    const j2l = row.j2Linear;
    const j2a = row.j2Angular;

    let sum = 0;

    // Linear contribution: invMass * |j_linear|²
    sum += bodyA.invMass * (j1l.x * j1l.x + j1l.y * j1l.y + j1l.z * j1l.z);
    sum += bodyB.invMass * (j2l.x * j2l.x + j2l.y * j2l.y + j2l.z * j2l.z);

    // Angular contribution: invI * (j_angular)² per component (diagonal inertia)
    sum += bodyA.invInertia.x * j1a.x * j1a.x + bodyA.invInertia.y * j1a.y * j1a.y + bodyA.invInertia.z * j1a.z * j1a.z;
    sum += bodyB.invInertia.x * j2a.x * j2a.x + bodyB.invInertia.y * j2a.y * j2a.y + bodyB.invInertia.z * j2a.z * j2a.z;

    return sum;
}

// ─── Sequential Impulse Kernel ────────────────────────────────────────────────

/**
 * Solve a single Jacobian row using sequential impulse with clamping.
 *
 * This is the velocity-level correction:
 *   λ = -effectiveMass * (J·v + bias + softness * accumulated_impulse)
 *   clamped to [lowerLimit, upperLimit]
 *   Δλ = clamped_λ - accumulated_λ
 *   v += M⁻¹ · Jᵀ · Δλ
 *
 * @returns The impulse delta applied.
 */
export function solveVelocityRow(
    row: JacobianRow3D,
    bodyA: SolverBody3D,
    bodyB: SolverBody3D,
): number {
    // Velocity along the Jacobian
    const jv =
        row.j1Linear.x * bodyA.linearVelocity.x + row.j1Linear.y * bodyA.linearVelocity.y + row.j1Linear.z * bodyA.linearVelocity.z +
        row.j1Angular.x * bodyA.angularVelocity.x + row.j1Angular.y * bodyA.angularVelocity.y + row.j1Angular.z * bodyA.angularVelocity.z +
        row.j2Linear.x * bodyB.linearVelocity.x + row.j2Linear.y * bodyB.linearVelocity.y + row.j2Linear.z * bodyB.linearVelocity.z +
        row.j2Angular.x * bodyB.angularVelocity.x + row.j2Angular.y * bodyB.angularVelocity.y + row.j2Angular.z * bodyB.angularVelocity.z;

    const totalInvMass = row.effectiveMass + row.softness;
    if (totalInvMass <= EPSILON) return 0;

    const effMass = 1.0 / totalInvMass;
    const prevImpulse = row.impulse;

    // Compute the raw impulse
    let uncapped = prevImpulse - effMass * (jv + row.bias + row.softness * prevImpulse);

    // Clamp to limits
    let clamped = uncapped;
    if (isFinite(row.lowerLimit) && isFinite(row.upperLimit)) {
        clamped = clamp(uncapped, row.lowerLimit, row.upperLimit);
    } else if (isFinite(row.lowerLimit)) {
        clamped = Math.max(uncapped, row.lowerLimit);
    } else if (isFinite(row.upperLimit)) {
        clamped = Math.min(uncapped, row.upperLimit);
    }
    row.impulse = clamped;
    const deltaImpulse = clamped - prevImpulse;

    if (Math.abs(deltaImpulse) <= EPSILON) return 0;

    // Apply impulse to body A
    if (bodyA.invMass > 0) {
        bodyA.linearVelocity.x += bodyA.invMass * deltaImpulse * row.j1Linear.x;
        bodyA.linearVelocity.y += bodyA.invMass * deltaImpulse * row.j1Linear.y;
        bodyA.linearVelocity.z += bodyA.invMass * deltaImpulse * row.j1Linear.z;
    }
    if (bodyA.invInertia.x > 0 || bodyA.invInertia.y > 0 || bodyA.invInertia.z > 0) {
        bodyA.angularVelocity.x += bodyA.invInertia.x * deltaImpulse * row.j1Angular.x;
        bodyA.angularVelocity.y += bodyA.invInertia.y * deltaImpulse * row.j1Angular.y;
        bodyA.angularVelocity.z += bodyA.invInertia.z * deltaImpulse * row.j1Angular.z;
    }

    // Apply impulse to body B
    if (bodyB.invMass > 0) {
        bodyB.linearVelocity.x += bodyB.invMass * deltaImpulse * row.j2Linear.x;
        bodyB.linearVelocity.y += bodyB.invMass * deltaImpulse * row.j2Linear.y;
        bodyB.linearVelocity.z += bodyB.invMass * deltaImpulse * row.j2Linear.z;
    }
    if (bodyB.invInertia.x > 0 || bodyB.invInertia.y > 0 || bodyB.invInertia.z > 0) {
        bodyB.angularVelocity.x += bodyB.invInertia.x * deltaImpulse * row.j2Angular.x;
        bodyB.angularVelocity.y += bodyB.invInertia.y * deltaImpulse * row.j2Angular.y;
        bodyB.angularVelocity.z += bodyB.invInertia.z * deltaImpulse * row.j2Angular.z;
    }

    return deltaImpulse;
}

/**
 * Position-level correction using the same Jacobian rows.
 *
 * Uses the stored positionError (computed during prepare) and applies
 * Baumgarte-style position correction with effective mass.
 *
 * @returns true if correction was applied (error above tolerance).
 */
export function solvePositionRow(
    row: JacobianRow3D,
    bodyA: SolverBody3D,
    bodyB: SolverBody3D,
): boolean {
    const error = row.positionError;
    if (Math.abs(error) <= POSITION_SOLVER_TOLERANCE) return false;

    // Recompute effective mass for position solve (same formula)
    const sumJInvJ = computeEffectiveMass(row, bodyA, bodyB);
    if (sumJInvJ <= EPSILON) return false;

    const totalInvMass = sumJInvJ + row.softness;
    const effMass = totalInvMass > EPSILON ? 1.0 / totalInvMass : 0;

    // Position impulse with correct sign convention for constraints.
    // impulse > 0 when error > 0. Application: body A along +j1, body B along -j2.
    let impulse = effMass * error;

    // Unilateral constraint guard
    if (!isFinite(row.lowerLimit) || impulse >= row.lowerLimit) {
        if (isFinite(row.upperLimit)) {
            impulse = Math.min(impulse, row.upperLimit);
        }
    } else {
        return false;
    }

    // Apply position correction to body A (along +j1 direction → toward body B)
    if (bodyA.invMass > 0) {
        bodyA.position.x += bodyA.invMass * impulse * row.j1Linear.x;
        bodyA.position.y += bodyA.invMass * impulse * row.j1Linear.y;
        bodyA.position.z += bodyA.invMass * impulse * row.j1Linear.z;
    }
    if (bodyA.invInertia.x > 0 || bodyA.invInertia.y > 0 || bodyA.invInertia.z > 0) {
        bodyA.rotation = _applyAngularPositionCorrection(
            bodyA.rotation, bodyA.invInertia, row.j1Angular, impulse,
        );
    }

    // Apply position correction to body B (along -j2 direction → toward body A)
    if (bodyB.invMass > 0) {
        bodyB.position.x -= bodyB.invMass * impulse * row.j2Linear.x;
        bodyB.position.y -= bodyB.invMass * impulse * row.j2Linear.y;
        bodyB.position.z -= bodyB.invMass * impulse * row.j2Linear.z;
    }
    if (bodyB.invInertia.x > 0 || bodyB.invInertia.y > 0 || bodyB.invInertia.z > 0) {
        bodyB.rotation = _applyAngularPositionCorrection(
            bodyB.rotation, bodyB.invInertia, row.j2Angular, -impulse,
        );
    }

    return true;
}

/**
 * Apply a small angular position correction via quaternion integration.
 * Δq = 0.5 * (axis * impulse * invI) * q, then normalize.
 */
function _applyAngularPositionCorrection(
    q: IQuatLike,
    invI: IVec3Like,
    jAngular: IVec3Like,
    impulse: number,
): IQuatLike {
    const dqx = invI.x * jAngular.x * impulse * 0.5;
    const dqy = invI.y * jAngular.y * impulse * 0.5;
    const dqz = invI.z * jAngular.z * impulse * 0.5;

    // δq = (dqx, dqy, dqz, 0) * q (quaternion multiply, scalar part of δq is 0)
    const result = Quat.multiply(
        { x: dqx, y: dqy, z: dqz, w: 0 },
        q,
    );
    // Add to original quaternion
    const newQ: IQuatLike = {
        x: q.x + result.x,
        y: q.y + result.y,
        z: q.z + result.z,
        w: q.w + result.w,
    };
    return Quat.normalize(newQ);
}

/**
 * Apply direct position corrections to body manager AFTER position integration.
 *
 * This matches the legacy _solveDistanceConstraints behavior:
 *   correction = dir * error * BIAS / invMassSum
 *   A.position += invMassA * correction  (A toward B)
 *   B.position -= invMassB * correction  (B away from A)
 *
 * The correction is applied per-axis using the 3 linear Jacobian rows.
 * After position correction, body velocities are adjusted to be consistent
 * with the position change, preventing position integration from undoing
 * the correction in the next step.
 *
 * Called from solvePosition() after position integration and contact corrections.
 */
export function applyConstraintPositionCorrection(
    jacobianCache: Map<ConstraintId3D, JacobianRow3D[]>,
    bodyManager: BodyManager3D,
    constraintManager: ConstraintManager3D,
    dt: number,
): void {
    const BIAS = POSITION_BIAS_FACTOR;

    for (const [cid, rows] of jacobianCache) {
        // Collect the 3 linear rows (X, Y, Z axes)
        let firstLinRow: JacobianRow3D | null = null;
        for (const row of rows) {
            const j1 = row.j1Linear;
            if (j1.x !== 0 || j1.y !== 0 || j1.z !== 0) {
                if (!firstLinRow) firstLinRow = row;
            }
        }
        if (!firstLinRow) continue;

        const bodyIdA = firstLinRow.bodyIdA;
        const bodyIdB = firstLinRow.bodyIdB;
        const invMassA = bodyManager.getInverseMass(bodyIdA);
        const invMassB = bodyManager.getInverseMass(bodyIdB);
        const invMassSum = invMassA + invMassB;
        if (invMassSum <= EPSILON) continue;

        // Recompute world-space anchors from CURRENT positions (after integration)
        const localAnchorA = constraintManager.getConstraintLocalAnchorA(cid);
        const localAnchorB = constraintManager.getConstraintLocalAnchorB(cid);
        const posA = bodyManager.getPosition(bodyIdA);
        const posB = bodyManager.getPosition(bodyIdB);
        const rotA = bodyManager.getRotation(bodyIdA);
        const rotB = bodyManager.getRotation(bodyIdB);

        const wA = transformPoint3D(localAnchorA, posA, rotA);
        const wB = transformPoint3D(localAnchorB, posB, rotB);

        // Compute distance (always positive) and direction from A to B.
        // This matches the legacy _solveDistanceConstraints which uses:
        //   delta = worldAnchorB - worldAnchorA
        //   error = |delta|  (always positive)
        //   dir = normalize(delta)
        //   correction = dir * error * BIAS / invMassSum
        //   A += invMassA * correction  (A toward B)
        //   B -= invMassB * correction  (B away from A)
        const dx = wB.x - wA.x, dy = wB.y - wA.y, dz = wB.z - wA.z;
        const dist = Math.sqrt(dx * dx + dy * dy + dz * dz);
        if (dist < POSITION_SOLVER_TOLERANCE) continue;

        const invDirX = dx / dist, invDirY = dy / dist, invDirZ = dz / dist;
        const corrMag = dist * BIAS / invMassSum;

        let dpAx = 0, dpAy = 0, dpAz = 0;
        let dpBx = 0, dpBy = 0, dpBz = 0;

        // Legacy direction: A along +dir (toward B), B along -dir (toward A)
        // Both bodies converge: distance reduces by factor (1 - 2*BIAS) per pass
        if (invMassA > 0) {
            dpAx = invMassA * corrMag * invDirX;
            dpAy = invMassA * corrMag * invDirY;
            dpAz = invMassA * corrMag * invDirZ;
        }
        if (invMassB > 0) {
            dpBx = invMassB * corrMag * invDirX;
            dpBy = invMassB * corrMag * invDirY;
            dpBz = invMassB * corrMag * invDirZ;
        }

        // Apply position corrections directly (matches legacy direct position override).
        // Legacy: A += correction * invMassA, B -= correction * invMassB
        // Both bodies converge toward each other.
        if (dpAx !== 0 || dpAy !== 0 || dpAz !== 0) {
            bodyManager.setPosition(bodyIdA, {
                x: posA.x + dpAx, y: posA.y + dpAy, z: posA.z + dpAz,
            });
        }
        if (dpBx !== 0 || dpBy !== 0 || dpBz !== 0) {
            bodyManager.setPosition(bodyIdB, {
                x: posB.x - dpBx, y: posB.y - dpBy, z: posB.z - dpBz,
            });
        }
    }
}

// ─── Constraint Data Resolution ───────────────────────────────────────────────

/**
 * Resolve constraint data from the descriptor map and constraint manager.
 * Reads the full definition including type-specific parameters (limits, motor, axes).
 */
export function resolveConstraintData(
    constraintId: ConstraintId3D,
    constraintManager: ConstraintManager3D,
    constraintDescriptors: ReadonlyMap<ConstraintId3D, { id: ConstraintId3D; type: number; def: SupportedConstraintDef3D; enabled: boolean; collideConnected: boolean }>,
): ConstraintData3D | null {
    const type = constraintManager.getConstraintType(constraintId);
    const { bodyIdA, bodyIdB } = constraintManager.getConstraintBodyIds(constraintId);
    const descriptor = constraintDescriptors.get(constraintId);
    if (!descriptor) return null;

    return {
        constraintId,
        type,
        bodyIdA,
        bodyIdB,
        def: descriptor.def,
    };
}

// ─── Dispatch Table ───────────────────────────────────────────────────────────

/**
 * Constraint type → prepare function mapping.
 * Adding a new joint type = adding one row to this table + creating the module.
 *
 * Wave 3b decomposition: each prepare function is a pure function in its own
 * module file. The dispatch table is the single integration point.
 */
export interface ConstraintModuleEntry {
    prepare: PrepareFn;
}

const _dispatchTable = new Map<number, ConstraintModuleEntry>();

/**
 * Register a constraint module for a given type.
 * Called by each constraint module at import time.
 */
export function registerConstraintModule(type: number, entry: ConstraintModuleEntry): void {
    _dispatchTable.set(type, entry);
}

/**
 * Get the prepare function for a constraint type.
 * Returns null if the type is not registered (unsupported).
 */
export function getPrepareFunction(type: number): PrepareFn | null {
    return _dispatchTable.get(type)?.prepare ?? null;
}

// ─── Batch Prepare ────────────────────────────────────────────────────────────

/**
 * Prepare all constraints for the solve phase.
 * Builds JacobianRow3D entries for each active constraint.
 *
 * @returns Map of constraintId → JacobianRow3D[] for the solve phase.
 */
export function prepareAllConstraints(
    constraintIds: readonly ConstraintId3D[],
    constraintManager: ConstraintManager3D,
    constraintDescriptors: ReadonlyMap<ConstraintId3D, { id: ConstraintId3D; type: number; def: SupportedConstraintDef3D; enabled: boolean; collideConnected: boolean }>,
    bodyManager: BodyManager3D,
    solverBodies: Map<BodyId3D, SolverBody3D>,
    dt: number,
): Map<ConstraintId3D, JacobianRow3D[]> {
    const result = new Map<ConstraintId3D, JacobianRow3D[]>();

    for (const cid of constraintIds) {
        const type = constraintManager.getConstraintType(cid);

        // Skip spring (handled separately) and unsupported types
        if (type === CONSTRAINT_TYPE_SPRING) continue;

        const prepareFn = getPrepareFunction(type);
        if (!prepareFn) continue;

        const data = resolveConstraintData(cid, constraintManager, constraintDescriptors);
        if (!data) continue;

        const rows: JacobianRow3D[] = [];
        prepareFn(data, solverBodies, bodyManager, dt, rows);

        if (rows.length > 0) {
            result.set(cid, rows);
        }
    }

    return result;
}

/**
 * Solve all constraints for one velocity iteration.
 * Applies ONLY velocity-level sequential impulse corrections.
 * Position corrections are applied separately via applyConstraintPositionCorrection()
 * after position integration, matching the legacy direct-correction behavior.
 */
export function solveAllVelocityConstraints(
    jacobianCache: Map<ConstraintId3D, JacobianRow3D[]>,
    solverBodies: Map<BodyId3D, SolverBody3D>,
): void {
    for (const rows of jacobianCache.values()) {
        for (const row of rows) {
            const bodyA = solverBodies.get(row.bodyIdA);
            const bodyB = solverBodies.get(row.bodyIdB);
            if (!bodyA || !bodyB) continue;

            solveVelocityRow(row, bodyA, bodyB);
        }
    }
}

/**
 * Solve all position constraints for one iteration.
 */
export function solveAllPositionConstraints(
    jacobianCache: Map<ConstraintId3D, JacobianRow3D[]>,
    solverBodies: Map<BodyId3D, SolverBody3D>,
): boolean {
    let minError = Infinity;

    for (const rows of jacobianCache.values()) {
        for (const row of rows) {
            const bodyA = solverBodies.get(row.bodyIdA);
            const bodyB = solverBodies.get(row.bodyIdB);
            if (!bodyA || !bodyB) continue;

            const error = Math.abs(row.positionError);
            minError = Math.min(minError, error);

            solvePositionRow(row, bodyA, bodyB);
        }
    }

    return minError <= POSITION_SOLVER_TOLERANCE;
}

/**
 * Reset accumulated impulses (called at prepare time, not per iteration).
 */
export function resetImpulses(jacobianCache: Map<ConstraintId3D, JacobianRow3D[]>): void {
    for (const rows of jacobianCache.values()) {
        for (const row of rows) {
            row.impulse = 0;
        }
    }
}

// ─── Shared Scratch Access ────────────────────────────────────────────────────

/** Module-level scratch vectors for use by constraint modules. */
export const SCRATCH = {
    A: _sA,
    B: _sB,
    C: _sC,
    D: _sD,
    E: _sE,
    F: _sF,
} as const;

/** Helper: create a zero Vec3. */
export function zeroVec3(): IVec3Like {
    return { x: 0, y: 0, z: 0 };
}

/** Helper: create a JacobianRow3D with defaults. */
export function createRow(
    bodyIdA: BodyId3D,
    bodyIdB: BodyId3D,
    j1Linear: IVec3Like,
    j1Angular: IVec3Like,
    j2Linear: IVec3Like,
    j2Angular: IVec3Like,
    bias: number,
    positionError: number,
    lowerLimit: number = -Infinity,
    upperLimit: number = Infinity,
    softness: number = 0,
): JacobianRow3D {
    return {
        bodyIdA,
        bodyIdB,
        j1Linear,
        j1Angular,
        j2Linear,
        j2Angular,
        bias,
        impulse: 0,
        lowerLimit,
        upperLimit,
        softness,
        positionError,
        hasLimit: false,
        hasMotor: false,
        effectiveMass: 0,
    };
}

// ─── Swing-Twist Decomposition ────────────────────────────────────────────────

/** Result of the swing-twist decomposition of a relative rotation. */
export interface ISwingTwistSplit {
    /** Total swing deviation from the twist axis, in [0, π] (axis-independent). */
    swingAngle: number;
    /** Signed twist angle about the twist axis, in [-π, π]. */
    twistAngle: number;
    /** Unit swing direction in frame-A space (zero vector at the apex). */
    swingDirFrame: IVec3Like;
    /** True when the swing direction is usable (swingAngle > EPSILON). */
    hasSwingDirection: boolean;
}

/**
 * Decompose a relative rotation into swing and twist components about the
 * frame X axis. Pure function writing into `out` (no allocation).
 *
 * Handles the degenerate 180°-swing case (qRel.w ≈ 0 and qRel.x ≈ 0) by
 * treating the twist as identity — the decomposition stays finite.
 */
export function decomposeSwingTwist(
    qRelIn: IQuatLike,
    out: ISwingTwistSplit,
): ISwingTwistSplit {
    const s = qRelIn.w < 0 ? -1 : 1;
    const qx = qRelIn.x * s;
    const qy = qRelIn.y * s;
    const qz = qRelIn.z * s;
    const qw = qRelIn.w * s;

    const p = qx;
    const norm2 = p * p + qw * qw;

    let swingX = qx;
    let swingY = qy;
    let swingZ = qz;
    let swingW = qw;

    if (norm2 > EPSILON * EPSILON) {
        const invN = 1.0 / Math.sqrt(norm2);
        const twX = p * invN;
        const twW = qw * invN;

        swingW = qw * twW - (qx * -twX);
        swingX = qw * -twX + twW * qx;
        swingY = twW * qy + (qz * -twX - qx * 0);
        swingZ = twW * qz + (qx * 0 - qy * -twX);

        out.twistAngle = 2.0 * Math.atan2(twX, twW);
    } else {
        out.twistAngle = 0;
    }

    const swingLen = Math.sqrt(swingX * swingX + swingY * swingY + swingZ * swingZ);
    out.swingAngle = 2.0 * Math.atan2(swingLen, swingW);

    if (swingLen > EPSILON) {
        out.swingDirFrame.x = swingX / swingLen;
        out.swingDirFrame.y = swingY / swingLen;
        out.swingDirFrame.z = swingZ / swingLen;
        out.hasSwingDirection = true;
    } else {
        out.swingDirFrame.x = 0;
        out.swingDirFrame.y = 0;
        out.swingDirFrame.z = 0;
        out.hasSwingDirection = false;
    }
    return out;
}

// ─── Motion Mode Detection ────────────────────────────────────────────────────

/**
 * Determine the motion mode for a single DOF from its limit pair.
 *
 *   - lower == upper → 'locked'
 *   - both infinite  → 'free'
 *   - otherwise      → 'limited'
 */
export function detectMotionMode(lower: number, upper: number): 'locked' | 'free' | 'limited' {
    if (Math.abs(lower - upper) <= EPSILON) return 'locked';
    if (!isFinite(lower) && !isFinite(upper)) return 'free';
    return 'limited';
}
