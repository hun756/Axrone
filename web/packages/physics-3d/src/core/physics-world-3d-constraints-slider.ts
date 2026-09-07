/**
 * Slider (Prismatic) constraint solver for 3D.
 *
 * Slider joint: allows translation along a single axis, constrains all other DOFs.
 *
 * Rows:
 *   - 2 lateral linear rows: lock translation perpendicular to slide axis
 *   - 3 angular lock rows: lock all rotation (slider must not rotate)
 *   - 1 axial limit/motor row: translation along slide axis with limit and/or motor
 *
 * Total: 6 rows (or 5 without limit/motor) — full 6-DOF constraint with 1 free linear DOF.
 *
 * Motor + limit interaction (Box2D behavior):
 *   When the motor is active and the slider reaches a limit, the motor cannot
 *   push past the limit. This is achieved by making the impulse clamp asymmetric
 *   at the limit boundary: at the lower limit, impulse ∈ [0, maxImpulse] (motor
 *   can only push away from lower limit); at the upper limit, impulse ∈ [-maxImpulse, 0].
 *
 * @internal
 * @module
 */

import { Vec3, Quat, type IVec3Like, type IQuatLike } from '@axrone/numeric';
import { PhysicsConstants } from '@axrone/physics-core';
import type { BodyId3D, ISliderConstraintDef3D } from '../types/physics-3d';
import {
    CONSTRAINT_TYPE_SLIDER,
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
    buildPerpendicularAxes,
} from './physics-world-3d-constraints-framework';
import type { BodyManager3D } from './physics-managers-3d';

const BAUMGARTE = PhysicsConstants.BAUMGARTE_FACTOR;
const EPSILON = PhysicsConstants.EPSILON;
const LINEAR_SLOP = PhysicsConstants.LINEAR_SLOP;

// ─── Local scratch vectors (module-level, zero allocation) ────────────────────

const _perp1: IVec3Like = { x: 0, y: 0, z: 0 };
const _perp2: IVec3Like = { x: 0, y: 0, z: 0 };

/**
 * Prepare slider constraint Jacobian rows.
 *
 * Builds 6 rows total:
 *   - 2 lateral linear rows (perpendicular to slide axis)
 *   - 3 angular lock rows (prevent all rotation)
 *   - 1 axial limit/motor row (conditional)
 */
function prepareSlider(
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
    const sliderDef = def as ISliderConstraintDef3D;

    // ─── Transform anchors and axis to world space ────────────────────
    const worldAnchorA = transformPoint3D(sliderDef.localAnchorA, bodyA.position, bodyA.rotation);
    const worldAnchorB = transformPoint3D(sliderDef.localAnchorB, bodyB.position, bodyB.rotation);

    // Transform local slide axis to world space using body A's rotation
    const worldAxisRaw = Quat.rotateVector(bodyA.rotation, sliderDef.localAxisA);
    const axisLen = Vec3.len(worldAxisRaw);
    const worldAxis: IVec3Like = axisLen > EPSILON
        ? Vec3.multiplyScalar(worldAxisRaw, 1.0 / axisLen)
        : { x: 1, y: 0, z: 0 };

    // r vectors for angular Jacobian computation
    const rA = Vec3.subtract(worldAnchorA, bodyA.position);
    const rB = Vec3.subtract(worldAnchorB, bodyB.position);

    // World-space anchor delta (from A to B)
    const worldDelta = Vec3.subtract(worldAnchorB, worldAnchorA);

    // ─── Orthonormal basis: two vectors perpendicular to slide axis ───
    buildPerpendicularAxes(worldAxis, _perp1, _perp2);

    // ─── 2 Lateral Linear Rows (lock translation perpendicular to axis) ──
    // For each perpendicular direction p:
    //   error = dot(worldDelta, p) — lateral offset
    //   J*v = dot(p, velB + ωB×rB) - dot(p, velA + ωA×rA)
    //   j1Linear = -p, j1Angular = -(rA × p)
    //   j2Linear = +p, j2Angular = +(rB × p)
    const lateralError1 = Vec3.dot(worldDelta, _perp1);
    const lateralError2 = Vec3.dot(worldDelta, _perp2);

    // Cross products for angular Jacobians: rA × perp, rB × perp
    const rAxP1 = Vec3.cross(rA, _perp1);
    const rBxP1 = Vec3.cross(rB, _perp1);
    const rAxP2 = Vec3.cross(rA, _perp2);
    const rBxP2 = Vec3.cross(rB, _perp2);

    // Lateral row 1: constrain motion along perp1
    out.push(createRow(
        bodyIdA, bodyIdB,
        { x: -_perp1.x, y: -_perp1.y, z: -_perp1.z },
        { x: -rAxP1.x, y: -rAxP1.y, z: -rAxP1.z },
        { x: _perp1.x, y: _perp1.y, z: _perp1.z },
        { x: rBxP1.x, y: rBxP1.y, z: rBxP1.z },
        -BAUMGARTE * lateralError1 / h,
        -lateralError1,
    ));

    // Lateral row 2: constrain motion along perp2
    out.push(createRow(
        bodyIdA, bodyIdB,
        { x: -_perp2.x, y: -_perp2.y, z: -_perp2.z },
        { x: -rAxP2.x, y: -rAxP2.y, z: -rAxP2.z },
        { x: _perp2.x, y: _perp2.y, z: _perp2.z },
        { x: rBxP2.x, y: rBxP2.y, z: rBxP2.z },
        -BAUMGARTE * lateralError2 / h,
        -lateralError2,
    ));

    // ─── 3 Angular Lock Rows (prevent all rotation) ───────────────────
    // Same approach as Fixed joint: qRel = conj(qA) * qB, error ≈ 2*qRel.xyz
    // Using perp1, perp2, and worldAxis as three orthogonal rotation axes.
    const qRel: IQuatLike = Quat.multiply(
        { x: -bodyA.rotation.x, y: -bodyA.rotation.y, z: -bodyA.rotation.z, w: bodyA.rotation.w },
        bodyB.rotation,
    );

    const angError1 = 2.0 * (qRel.x * _perp1.x + qRel.y * _perp1.y + qRel.z * _perp1.z);
    const angError2 = 2.0 * (qRel.x * _perp2.x + qRel.y * _perp2.y + qRel.z * _perp2.z);
    const angErrorAxis = 2.0 * (qRel.x * worldAxis.x + qRel.y * worldAxis.y + qRel.z * worldAxis.z);

    // Angular lock row 1: prevent rotation about perp1
    out.push(createRow(
        bodyIdA, bodyIdB,
        zeroVec3(), { x: -_perp1.x, y: -_perp1.y, z: -_perp1.z },
        zeroVec3(), { x: _perp1.x, y: _perp1.y, z: _perp1.z },
        -BAUMGARTE * angError1 / h,
        -angError1,
    ));

    // Angular lock row 2: prevent rotation about perp2
    out.push(createRow(
        bodyIdA, bodyIdB,
        zeroVec3(), { x: -_perp2.x, y: -_perp2.y, z: -_perp2.z },
        zeroVec3(), { x: _perp2.x, y: _perp2.y, z: _perp2.z },
        -BAUMGARTE * angError2 / h,
        -angError2,
    ));

    // Angular lock row 3: prevent rotation about slide axis
    out.push(createRow(
        bodyIdA, bodyIdB,
        zeroVec3(), { x: -worldAxis.x, y: -worldAxis.y, z: -worldAxis.z },
        zeroVec3(), { x: worldAxis.x, y: worldAxis.y, z: worldAxis.z },
        -BAUMGARTE * angErrorAxis / h,
        -angErrorAxis,
    ));

    // ─── 1 Axial Row (limit + motor along slide axis) ─────────────────
    const enableLimit = sliderDef.enableLimit ?? false;
    const enableMotor = sliderDef.enableMotor ?? false;
    const lowerLimit = sliderDef.lowerLimit ?? 0;
    const upperLimit = sliderDef.upperLimit ?? 0;
    const motorSpeed = sliderDef.motorSpeed ?? 0;
    const maxMotorForce = sliderDef.maxMotorForce ?? 0;

    // Current translation along the slide axis
    const translation = Vec3.dot(worldDelta, worldAxis);

    // Cross products for axial angular Jacobians
    const rAxAxis = Vec3.cross(rA, worldAxis);
    const rBxAxis = Vec3.cross(rB, worldAxis);

    let axBias = 0;
    let axPosError = 0;
    let axLower = -Infinity;
    let axUpper = Infinity;
    let hasLimit = false;
    let hasMotor = false;

    const maxImpulse = Math.abs(maxMotorForce as number) * h;
    const atLowerLimit = enableLimit && translation <= lowerLimit + LINEAR_SLOP;
    const atUpperLimit = enableLimit && translation >= upperLimit - LINEAR_SLOP;

    // ── Limit component ──
    // The axial Jacobian uses j1Lin=+axis, j2Lin=-axis (matching the fixed joint
    // convention: j1Lin=-dir, j2Lin=+dir where dir=-axis). This gives
    // J*v = axis·vA - axis·vB = -(vB_axis - vA_axis).
    // Standard bias convention: bias = -BAUMGARTE * error / h.
    let limitActive = false;
    if (enableLimit) {
        hasLimit = true;
        let limitError = 0;

        if (translation < lowerLimit) {
            limitError = translation - lowerLimit;
        } else if (translation > upperLimit) {
            limitError = translation - upperLimit;
        }

        if (Math.abs(limitError) > LINEAR_SLOP) {
            limitActive = true;
            axBias = -BAUMGARTE * limitError / h;
            axPosError = -limitError;
            axLower = -Infinity;
            axUpper = Infinity;
        }
    }

    // ── Motor component + Box2D limit interaction ──
    // Motor bias = -motorSpeed. With J*v = -(vB_axis - vA_axis):
    //   lambda = -effMass*(jv + bias) = -effMass*(0 + (-motorSpeed)) = effMass*motorSpeed
    //   If motorSpeed > 0: lambda > 0 → deltaVelB_x = -lambda*invMassB*axis.x < 0... 
    //   Wait — we need the motor to INCREASE vB_axis. With the flipped Jacobian,
    //   lambda > 0 pushes body A along +axis and body B along -axis.
    //   So for positive motorSpeed: we need lambda < 0 → bias = +motorSpeed? No.
    //   Actually: deltaVelB_axis = -lambda*invMassB. For lambda > 0: deltaVelB_axis < 0.
    //   But we want deltaVelB_axis > 0 (positive motor). So lambda must be < 0.
    //   lambda = -effMass*(jv + bias). For lambda < 0: jv + bias > 0. With jv=0: bias > 0.
    //   So bias = +motorSpeed for the flipped Jacobian.
    //
    // BUT the limit bias uses the standard convention (bias = -BAUMGARTE*error/h).
    // The motor and limit have DIFFERENT sign conventions with this Jacobian!
    //
    // Resolution: use bias = -motorSpeed and flip the motor impulse clamp.
    // Actually, simplest: motor bias = -motorSpeed, which gives lambda = effMass*motorSpeed > 0.
    // deltaVelB_axis = -lambda*invMassB < 0. This DECREASES vB_axis.
    // For positive motorSpeed, this is WRONG.
    //
    // CORRECT: motor bias = +motorSpeed for the flipped Jacobian.
    // lambda = -effMass*(0 + motorSpeed) = -effMass*motorSpeed < 0.
    // deltaVelB_axis = -(-effMass*motorSpeed)*invMassB = effMass*motorSpeed*invMassB > 0. ✓
    if (enableMotor && Math.abs(maxMotorForce as number) > EPSILON) {
        hasMotor = true;

        if (atLowerLimit) {
            // At lower limit: motor cannot push further into the limit.
            // With flipped Jacobian: lambda < 0 pushes body B in +axis direction (away from lower).
            // Motor at lower limit: only allow lambda ≤ 0 (push away from lower limit).
            axBias = motorSpeed as number;
            axPosError = 0;
            axLower = -maxImpulse;
            axUpper = 0;
            limitActive = true;
        } else if (atUpperLimit) {
            // At upper limit: motor cannot push further into the limit.
            // Motor at upper limit: only allow lambda ≥ 0 (push away from upper limit).
            axBias = motorSpeed as number;
            axPosError = 0;
            axLower = 0;
            axUpper = maxImpulse;
            limitActive = true;
        } else {
            // Between limits or no limit: motor runs freely.
            axBias = motorSpeed as number;
            axPosError = 0;
            axLower = -maxImpulse;
            axUpper = maxImpulse;
        }
    }

    // Only add the axial row if there's an active limit correction or a motor.
    // When limit is enabled but the slider is within the range (no limit error)
    // and no motor is active, the slider should slide freely along the axis.
    if (limitActive || hasMotor) {
        const axialRow = createRow(
            bodyIdA, bodyIdB,
            // Flipped Jacobian: j1Lin=+axis, j2Lin=-axis
            // This matches the fixed joint convention for consistent bias signs.
            { x: worldAxis.x, y: worldAxis.y, z: worldAxis.z },
            { x: rAxAxis.x, y: rAxAxis.y, z: rAxAxis.z },
            { x: -worldAxis.x, y: -worldAxis.y, z: -worldAxis.z },
            { x: -rBxAxis.x, y: -rBxAxis.y, z: -rBxAxis.z },
            axBias,
            axPosError,
            axLower,
            axUpper,
        );
        axialRow.hasLimit = hasLimit;
        axialRow.hasMotor = hasMotor;
        out.push(axialRow);
    }

    // ─── Compute effective masses ─────────────────────────────────────
    for (const row of out) {
        row.effectiveMass = computeEffectiveMass(row, bodyA, bodyB);
    }
}

// ─── Registration ─────────────────────────────────────────────────────────────

registerConstraintModule(CONSTRAINT_TYPE_SLIDER, {
    prepare: prepareSlider,
});
