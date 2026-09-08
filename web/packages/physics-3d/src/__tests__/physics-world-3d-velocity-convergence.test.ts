/**
 * Velocity-only convergence tests for 3D constraint modules.
 *
 * These tests prove that the velocity phase of the constraint solver is
 * SELF-CONVERGENT — i.e., with legacy position correction disabled, the
 * constraint error monotonically decreases under velocity solve alone.
 *
 * This is the critical discriminative property: if the bias sign convention
 * is wrong, the velocity solve DIVERGES (error grows), and these tests FAIL.
 *
 * Pattern: for each module (Fixed, Hinge, Slider, Configurable), set up
 * two dynamic bodies with a constraint and an initial error, then run
 * multi-step velocity-only simulation (prepare → solve → integrate) and
 * verify the error shrinks monotonically.
 *
 * Template: Mira's coupledStepGap test in cone-twist (Scenario 11).
 *
 * @module
 */

import { describe, it, expect } from 'vitest';
import { Quat, Vec3, type IVec3Like, type IQuatLike } from '@axrone/numeric';
import type { BodyId3D, ConstraintId3D } from '../types/physics-3d';
import {
    type JacobianRow3D,
    type SolverBody3D,
    type ConstraintData3D,
    solveVelocityRow,
    getPrepareFunction,
} from '../core/physics-world-3d-constraints-framework';
import {
    CONSTRAINT_TYPE_FIXED,
    CONSTRAINT_TYPE_HINGE,
    CONSTRAINT_TYPE_SLIDER,
    CONSTRAINT_TYPE_GENERIC,
    transformPoint3D,
} from '../core/physics-world-3d-shared';
import type { BodyManager3D } from '../core/physics-managers-3d';
import type { IHingeConstraintDef3D, ISliderConstraintDef3D, IGenericConstraintDef3D } from '../types/physics-3d';
// Side-effect imports: register prepare functions
import '../core/physics-world-3d-constraints-fixed';
import '../core/physics-world-3d-constraints-hinge';
import '../core/physics-world-3d-constraints-slider';
import { prepareConfigurable } from '../core/physics-world-3d-constraints-configurable';

// ─── Shared Helpers ───────────────────────────────────────────────────────────

const IDENTITY: IQuatLike = { x: 0, y: 0, z: 0, w: 1 };
const ZERO: IVec3Like = { x: 0, y: 0, z: 0 };
const STUB_MANAGER = null as unknown as BodyManager3D;

function makeSolverBody(
    id: number,
    pos: IVec3Like,
    rot: IQuatLike = IDENTITY,
    invMass: number = 1,
    invInertia: IVec3Like = { x: 1, y: 1, z: 1 },
): SolverBody3D {
    return {
        bodyId: id,
        invMass,
        invInertia,
        linearVelocity: { ...ZERO },
        angularVelocity: { ...ZERO },
        position: { ...pos },
        rotation: { ...rot },
    };
}

function makeBodyMap(bodies: SolverBody3D[]): Map<BodyId3D, SolverBody3D> {
    const map = new Map<BodyId3D, SolverBody3D>();
    for (const b of bodies) map.set(b.bodyId, b);
    return map;
}

/** Integrate body position and rotation forward by dt. */
function integrateBody(b: SolverBody3D, dt: number): void {
    b.position.x += b.linearVelocity.x * dt;
    b.position.y += b.linearVelocity.y * dt;
    b.position.z += b.linearVelocity.z * dt;

    const w = b.angularVelocity;
    const speed = Math.sqrt(w.x * w.x + w.y * w.y + w.z * w.z);
    if (speed > 1e-9) {
        const axis: IVec3Like = { x: w.x / speed, y: w.y / speed, z: w.z / speed };
        const dq = Quat.fromAxisAngle(axis, speed * dt);
        const rotated = Quat.multiply(dq, b.rotation);
        b.rotation.x = rotated.x;
        b.rotation.y = rotated.y;
        b.rotation.z = rotated.z;
        b.rotation.w = rotated.w;
        const n = Math.sqrt(
            b.rotation.x ** 2 + b.rotation.y ** 2 + b.rotation.z ** 2 + b.rotation.w ** 2,
        );
        b.rotation.x /= n;
        b.rotation.y /= n;
        b.rotation.z /= n;
        b.rotation.w /= n;
    }
}

/** Prepare rows, solve velocity for `iter` iterations, return rows. */
function prepareAndSolve(
    prepareFn: (data: ConstraintData3D, bodies: Map<BodyId3D, SolverBody3D>, mgr: BodyManager3D, dt: number, out: JacobianRow3D[]) => void,
    data: ConstraintData3D,
    bodyA: SolverBody3D,
    bodyB: SolverBody3D,
    dt: number,
    iter: number,
): JacobianRow3D[] {
    const bodies = makeBodyMap([bodyA, bodyB]);
    const rows: JacobianRow3D[] = [];
    prepareFn(data, bodies, STUB_MANAGER, dt, rows);
    for (let i = 0; i < iter; i++) {
        for (const row of rows) {
            solveVelocityRow(row, bodyA, bodyB);
        }
    }
    return rows;
}

/** Compute anchor gap for a constraint with localFrameA/B.position or localAnchorA/B. */
function anchorGapFromAnchors(
    localA: IVec3Like, localB: IVec3Like,
    bodyA: SolverBody3D, bodyB: SolverBody3D,
): number {
    const wA = transformPoint3D(localA, bodyA.position, bodyA.rotation);
    const wB = transformPoint3D(localB, bodyB.position, bodyB.rotation);
    const dx = wB.x - wA.x;
    const dy = wB.y - wA.y;
    const dz = wB.z - wA.z;
    return Math.sqrt(dx * dx + dy * dy + dz * dz);
}

/** Compute relative rotation error magnitude (2 * |qRel.xyz|). */
function angularError(bodyA: SolverBody3D, bodyB: SolverBody3D): number {
    const conjA: IQuatLike = { x: -bodyA.rotation.x, y: -bodyA.rotation.y, z: -bodyA.rotation.z, w: bodyA.rotation.w };
    const qRel = Quat.multiply(conjA, bodyB.rotation);
    return 2.0 * Math.sqrt(qRel.x * qRel.x + qRel.y * qRel.y + qRel.z * qRel.z);
}

// ─── Multi-step velocity-only convergence simulation ──────────────────────────

/**
 * Run `steps` simulation steps. Each step:
 *   1. Prepare constraint rows from current body state
 *   2. Solve velocity rows for `velIter` iterations
 *   3. Integrate body positions/rotations
 *
 * Returns array of error measurements (one per step, plus initial).
 */
function simulateVelocityOnly(
    prepareFn: (data: ConstraintData3D, bodies: Map<BodyId3D, SolverBody3D>, mgr: BodyManager3D, dt: number, out: JacobianRow3D[]) => void,
    data: ConstraintData3D,
    bodyA: SolverBody3D,
    bodyB: SolverBody3D,
    steps: number,
    velIter: number,
    dt: number,
    errorFn: (bodyA: SolverBody3D, bodyB: SolverBody3D) => number,
): number[] {
    const errors: number[] = [errorFn(bodyA, bodyB)];
    for (let s = 0; s < steps; s++) {
        const bodies = makeBodyMap([bodyA, bodyB]);
        const rows: JacobianRow3D[] = [];
        prepareFn(data, bodies, STUB_MANAGER, dt, rows);
        for (let i = 0; i < velIter; i++) {
            for (const row of rows) {
                solveVelocityRow(row, bodyA, bodyB);
            }
        }
        integrateBody(bodyA, dt);
        integrateBody(bodyB, dt);
        errors.push(errorFn(bodyA, bodyB));
    }
    return errors;
}

// ─── Module-specific prepare wrappers ─────────────────────────────────────────

function prepareFixed(data: ConstraintData3D, bodies: Map<BodyId3D, SolverBody3D>, mgr: BodyManager3D, dt: number, out: JacobianRow3D[]): void {
    const fn = getPrepareFunction(CONSTRAINT_TYPE_FIXED);
    fn!(data, bodies, mgr, dt, out);
}

function prepareHinge(data: ConstraintData3D, bodies: Map<BodyId3D, SolverBody3D>, mgr: BodyManager3D, dt: number, out: JacobianRow3D[]): void {
    const fn = getPrepareFunction(CONSTRAINT_TYPE_HINGE);
    fn!(data, bodies, mgr, dt, out);
}

function prepareSlider(data: ConstraintData3D, bodies: Map<BodyId3D, SolverBody3D>, mgr: BodyManager3D, dt: number, out: JacobianRow3D[]): void {
    const fn = getPrepareFunction(CONSTRAINT_TYPE_SLIDER);
    fn!(data, bodies, mgr, dt, out);
}

function makeConstraintData(type: number, bodyIdA: number, bodyIdB: number, def: any): ConstraintData3D {
    return {
        constraintId: 100 as ConstraintId3D,
        type,
        bodyIdA,
        bodyIdB,
        def: { kind: type, ...def } as ConstraintData3D['def'],
    };
}

// ─── FIXED: Linear + Angular convergence ──────────────────────────────────────

describe('Velocity-only convergence — Fixed constraint', () => {
    const DT = 1 / 60;
    const STEPS = 10;
    const VEL_ITER = 8;

    it('linear rows: anchor gap monotonically decreases', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 0.5, y: 0.3, z: 0.2 });

        const def = {
            bodyIdA: 1, bodyIdB: 2,
            localAnchorA: { x: 0, y: 0, z: 0 },
            localAnchorB: { x: 0, y: 0, z: 0 },
            collideConnected: false,
        };
        const data = makeConstraintData(CONSTRAINT_TYPE_FIXED, 1, 2, def);

        const errors = simulateVelocityOnly(prepareFixed, data, bodyA, bodyB, STEPS, VEL_ITER, DT,
            (a, b) => anchorGapFromAnchors({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, a, b));

        // Initial error
        expect(errors[0]).toBeGreaterThan(0.1);
        // Final error < initial error (convergent)
        expect(errors[STEPS]).toBeLessThan(errors[0]);
        // Monotonically non-increasing (allow tiny numerical noise)
        for (let i = 1; i < errors.length; i++) {
            expect(errors[i]).toBeLessThan(errors[i - 1] + 1e-10);
        }
    });

    it('angular rows: relative rotation error decreases', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        // Body B has a small rotation offset
        const qOffset = Quat.fromAxisAngle({ x: 0, y: 1, z: 0 }, 0.3);
        const bodyB = makeSolverBody(2, { x: 0, y: 0, z: 0 }, qOffset);

        const def = {
            bodyIdA: 1, bodyIdB: 2,
            localAnchorA: { x: 0, y: 0, z: 0 },
            localAnchorB: { x: 0, y: 0, z: 0 },
            collideConnected: false,
        };
        const data = makeConstraintData(CONSTRAINT_TYPE_FIXED, 1, 2, def);

        const errors = simulateVelocityOnly(prepareFixed, data, bodyA, bodyB, STEPS, VEL_ITER, DT,
            (a, b) => angularError(a, b));

        expect(errors[0]).toBeGreaterThan(0.1);
        expect(errors[STEPS]).toBeLessThan(errors[0]);
    });
});

// ─── HINGE: Linear + Angular lock + Limit convergence ─────────────────────────

describe('Velocity-only convergence — Hinge constraint', () => {
    const DT = 1 / 60;
    const STEPS = 10;
    const VEL_ITER = 8;

    function makeHingeDef(opts: Partial<IHingeConstraintDef3D> = {}): IHingeConstraintDef3D {
        return {
            bodyIdA: 1, bodyIdB: 2,
            localAnchorA: { x: 0, y: 0, z: 0 },
            localAnchorB: { x: 0, y: 0, z: 0 },
            localAxisA: { x: 0, y: 1, z: 0 },
            localAxisB: { x: 0, y: 1, z: 0 },
            collideConnected: false,
            ...opts,
        };
    }

    it('linear rows: anchor gap monotonically decreases', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 0.4, y: 0.2, z: 0.1 });

        const data = makeConstraintData(CONSTRAINT_TYPE_HINGE, 1, 2, makeHingeDef());

        const errors = simulateVelocityOnly(prepareHinge, data, bodyA, bodyB, STEPS, VEL_ITER, DT,
            (a, b) => anchorGapFromAnchors({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, a, b));

        expect(errors[0]).toBeGreaterThan(0.1);
        expect(errors[STEPS]).toBeLessThan(errors[0]);
        for (let i = 1; i < errors.length; i++) {
            expect(errors[i]).toBeLessThan(errors[i - 1] + 1e-10);
        }
    });

    it('angular lock rows: perpendicular rotation error decreases', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        // Rotate B about X axis (perpendicular to hinge axis Y) — should be locked
        const qOffset = Quat.fromAxisAngle({ x: 1, y: 0, z: 0 }, 0.25);
        const bodyB = makeSolverBody(2, { x: 0, y: 0, z: 0 }, qOffset);

        const data = makeConstraintData(CONSTRAINT_TYPE_HINGE, 1, 2, makeHingeDef());

        const errors = simulateVelocityOnly(prepareHinge, data, bodyA, bodyB, STEPS, VEL_ITER, DT,
            (a, b) => angularError(a, b));

        expect(errors[0]).toBeGreaterThan(0.1);
        expect(errors[STEPS]).toBeLessThan(errors[0]);
    });

    it('limit row: angle beyond upper limit is pulled back', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        // Rotate B about Y (hinge axis) beyond the upper limit
        const limitAngle = Math.PI / 6; // 30 degrees
        const startAngle = limitAngle + 0.3; // 0.3 rad beyond limit
        const qOffset = Quat.fromAxisAngle({ x: 0, y: 1, z: 0 }, startAngle);
        const bodyB = makeSolverBody(2, { x: 0, y: 0, z: 0 }, qOffset);

        const def = makeHingeDef({
            enableLimit: true,
            lowerLimit: -limitAngle,
            upperLimit: limitAngle,
        });
        const data = makeConstraintData(CONSTRAINT_TYPE_HINGE, 1, 2, def);

        // Measure angle about hinge axis
        const measureAngle = (a: SolverBody3D, b: SolverBody3D): number => {
            const conjA: IQuatLike = { x: -a.rotation.x, y: -a.rotation.y, z: -a.rotation.z, w: a.rotation.w };
            const qRel = Quat.multiply(conjA, b.rotation);
            const dot = qRel.x * 0 + qRel.y * 1 + qRel.z * 0; // dot with Y axis
            return 2.0 * Math.atan2(dot, qRel.w);
        };

        const errors = simulateVelocityOnly(prepareHinge, data, bodyA, bodyB, STEPS, VEL_ITER, DT,
            (a, b) => Math.abs(measureAngle(a, b)));

        // The angle should decrease toward the limit
        expect(errors[0]).toBeGreaterThan(limitAngle);
        expect(errors[STEPS]).toBeLessThan(errors[0]);
    });
});

// ─── SLIDER: Lateral + Angular + Axial convergence ────────────────────────────

describe('Velocity-only convergence — Slider constraint', () => {
    const DT = 1 / 60;
    const STEPS = 10;
    const VEL_ITER = 8;

    function makeSliderDef(opts: Partial<ISliderConstraintDef3D> = {}): ISliderConstraintDef3D {
        return {
            bodyIdA: 1, bodyIdB: 2,
            localAnchorA: { x: 0, y: 0, z: 0 },
            localAnchorB: { x: 0, y: 0, z: 0 },
            localAxisA: { x: 1, y: 0, z: 0 },
            collideConnected: false,
            ...opts,
        };
    }

    it('lateral rows: perpendicular offset decreases', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        // Offset B along Y (perpendicular to slide axis X) — should be corrected
        const bodyB = makeSolverBody(2, { x: 1, y: 0.3, z: 0.2 });

        const data = makeConstraintData(CONSTRAINT_TYPE_SLIDER, 1, 2, makeSliderDef());

        // Measure lateral error (perpendicular to slide axis)
        const lateralError = (a: SolverBody3D, b: SolverBody3D): number => {
            const wA = transformPoint3D({ x: 0, y: 0, z: 0 }, a.position, a.rotation);
            const wB = transformPoint3D({ x: 0, y: 0, z: 0 }, b.position, b.rotation);
            const dx = wB.x - wA.x;
            const dy = wB.y - wA.y;
            const dz = wB.z - wA.z;
            // Get slide axis from body A rotation
            const axis = Quat.rotateVector(a.rotation, { x: 1, y: 0, z: 0 });
            const axialProj = dx * axis.x + dy * axis.y + dz * axis.z;
            // Lateral = total - axial component
            return Math.sqrt(Math.max(0, dx * dx + dy * dy + dz * dz - axialProj * axialProj));
        };

        const errors = simulateVelocityOnly(prepareSlider, data, bodyA, bodyB, STEPS, VEL_ITER, DT, lateralError);

        expect(errors[0]).toBeGreaterThan(0.1);
        expect(errors[STEPS]).toBeLessThan(errors[0]);
        for (let i = 1; i < errors.length; i++) {
            expect(errors[i]).toBeLessThan(errors[i - 1] + 1e-10);
        }
    });

    it('angular lock rows: rotation error decreases', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const qOffset = Quat.fromAxisAngle({ x: 0, y: 0, z: 1 }, 0.2);
        const bodyB = makeSolverBody(2, { x: 0, y: 0, z: 0 }, qOffset);

        const data = makeConstraintData(CONSTRAINT_TYPE_SLIDER, 1, 2, makeSliderDef());

        const errors = simulateVelocityOnly(prepareSlider, data, bodyA, bodyB, STEPS, VEL_ITER, DT,
            (a, b) => angularError(a, b));

        expect(errors[0]).toBeGreaterThan(0.1);
        expect(errors[STEPS]).toBeLessThan(errors[0]);
    });

    it('axial limit row: translation beyond limit is pulled back', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        // B is beyond the upper limit along X (slide axis)
        const bodyB = makeSolverBody(2, { x: 2.5, y: 0, z: 0 });

        const def = makeSliderDef({
            enableLimit: true,
            lowerLimit: -1,
            upperLimit: 2,
        });
        const data = makeConstraintData(CONSTRAINT_TYPE_SLIDER, 1, 2, def);

        // Measure axial translation
        const axialTranslation = (a: SolverBody3D, b: SolverBody3D): number => {
            const wA = transformPoint3D({ x: 0, y: 0, z: 0 }, a.position, a.rotation);
            const wB = transformPoint3D({ x: 0, y: 0, z: 0 }, b.position, b.rotation);
            const axis = Quat.rotateVector(a.rotation, { x: 1, y: 0, z: 0 });
            return (wB.x - wA.x) * axis.x + (wB.y - wA.y) * axis.y + (wB.z - wA.z) * axis.z;
        };

        const errors = simulateVelocityOnly(prepareSlider, data, bodyA, bodyB, STEPS, VEL_ITER, DT,
            (a, b) => Math.abs(axialTranslation(a, b)));

        // Should decrease toward the upper limit (2.0)
        expect(errors[0]).toBeGreaterThan(2);
        expect(errors[STEPS]).toBeLessThan(errors[0]);
    });
});

// ─── CONFIGURABLE: Locked + Limited convergence ───────────────────────────────

describe('Velocity-only convergence — Configurable constraint', () => {
    const DT = 1 / 60;
    const STEPS = 10;
    const VEL_ITER = 8;

    function makeGenericDef(overrides: Partial<IGenericConstraintDef3D> = {}): IGenericConstraintDef3D {
        return {
            bodyIdA: 1, bodyIdB: 2,
            localFrameA: { position: { x: 0, y: 0, z: 0 }, rotation: { ...IDENTITY } },
            localFrameB: { position: { x: 0, y: 0, z: 0 }, rotation: { ...IDENTITY } },
            linearLowerLimit: { x: 0, y: 0, z: 0 },
            linearUpperLimit: { x: 0, y: 0, z: 0 },
            angularLowerLimit: { x: 0, y: 0, z: 0 },
            angularUpperLimit: { x: 0, y: 0, z: 0 },
            ...overrides,
        };
    }

    function prepareConfigurableWrapper(data: ConstraintData3D, bodies: Map<BodyId3D, SolverBody3D>, mgr: BodyManager3D, dt: number, out: JacobianRow3D[]): void {
        prepareConfigurable(data, bodies, mgr, dt, out);
    }

    it('linear locked rows: anchor gap monotonically decreases', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 0.4, y: 0.3, z: 0.2 });

        // All locked
        const def = makeGenericDef();
        const data = makeConstraintData(CONSTRAINT_TYPE_GENERIC, 1, 2, def);

        const errors = simulateVelocityOnly(prepareConfigurableWrapper, data, bodyA, bodyB, STEPS, VEL_ITER, DT,
            (a, b) => anchorGapFromAnchors({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, a, b));

        expect(errors[0]).toBeGreaterThan(0.1);
        expect(errors[STEPS]).toBeLessThan(errors[0]);
        for (let i = 1; i < errors.length; i++) {
            expect(errors[i]).toBeLessThan(errors[i - 1] + 1e-10);
        }
    });

    it('angular locked rows: relative rotation error decreases', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const qOffset = Quat.fromAxisAngle({ x: 0, y: 1, z: 0 }, 0.25);
        const bodyB = makeSolverBody(2, { x: 0, y: 0, z: 0 }, qOffset);

        const def = makeGenericDef();
        const data = makeConstraintData(CONSTRAINT_TYPE_GENERIC, 1, 2, def);

        const errors = simulateVelocityOnly(prepareConfigurableWrapper, data, bodyA, bodyB, STEPS, VEL_ITER, DT,
            (a, b) => angularError(a, b));

        expect(errors[0]).toBeGreaterThan(0.1);
        expect(errors[STEPS]).toBeLessThan(errors[0]);
    });

    it('linear limited rows: violation beyond limit is corrected', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        // B is at x=1.5, but linear X limit is [-0.5, 0.5]
        const bodyB = makeSolverBody(2, { x: 1.5, y: 0, z: 0 });

        const def = makeGenericDef({
            linearLowerLimit: { x: -0.5, y: -Infinity, z: -Infinity },
            linearUpperLimit: { x: 0.5, y: Infinity, z: Infinity },
            angularLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            angularUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
        });
        const data = makeConstraintData(CONSTRAINT_TYPE_GENERIC, 1, 2, def);

        // Measure X error (the limited axis)
        const xError = (a: SolverBody3D, b: SolverBody3D): number => {
            return Math.abs(b.position.x - a.position.x);
        };

        const errors = simulateVelocityOnly(prepareConfigurableWrapper, data, bodyA, bodyB, STEPS, VEL_ITER, DT, xError);

        // Initial X separation is 1.5, should decrease toward 0.5 (the limit)
        expect(errors[0]).toBeGreaterThan(1);
        expect(errors[STEPS]).toBeLessThan(errors[0]);
    });

    it('angular limited rows: violation beyond limit is corrected', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        // B rotated 0.5 rad about Y, but angular Y limit is [-0.2, 0.2]
        const qOffset = Quat.fromAxisAngle({ x: 0, y: 1, z: 0 }, 0.5);
        const bodyB = makeSolverBody(2, { x: 0, y: 0, z: 0 }, qOffset);

        const def = makeGenericDef({
            linearLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            linearUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
            angularLowerLimit: { x: -Infinity, y: -0.2, z: -Infinity },
            angularUpperLimit: { x: Infinity, y: 0.2, z: Infinity },
        });
        const data = makeConstraintData(CONSTRAINT_TYPE_GENERIC, 1, 2, def);

        // Measure angle about Y
        const angleAboutY = (a: SolverBody3D, b: SolverBody3D): number => {
            const conjA: IQuatLike = { x: -a.rotation.x, y: -a.rotation.y, z: -a.rotation.z, w: a.rotation.w };
            const qRel = Quat.multiply(conjA, b.rotation);
            const dot = qRel.y; // dot with Y axis
            return Math.abs(2.0 * Math.atan2(dot, qRel.w));
        };

        const errors = simulateVelocityOnly(prepareConfigurableWrapper, data, bodyA, bodyB, STEPS, VEL_ITER, DT, angleAboutY);

        expect(errors[0]).toBeGreaterThan(0.3);
        expect(errors[STEPS]).toBeLessThan(errors[0]);
    });
});
