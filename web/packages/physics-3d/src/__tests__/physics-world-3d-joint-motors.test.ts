/**
 * Motor tests for Configurable (Generic) and ConeTwist joints.
 *
 * Proves:
 *   1. Configurable per-axis motor approaches target velocity (real value assert)
 *   2. Configurable motor stops at limit (Box2D stall)
 *   3. Blow-through: without limits, same motor exceeds the limit
 *   4. Opposite-axis motors don't cancel
 *   5. FREE axis produces no motor row (row count assert)
 *   6. ConeTwist motor: type fields don't change behavior (26 existing tests)
 *      + twist motor stall with limit (direct-prepare)
 *
 * @module
 */

import { describe, it, expect } from 'vitest';
import { Quat, type IVec3Like, type IQuatLike } from '@axrone/numeric';
import type {
    BodyId3D,
    ConstraintId3D,
    IGenericConstraintDef3D,
    IConeTwistConstraintDef3D,
} from '../types/physics-3d';
import {
    type JacobianRow3D,
    type SolverBody3D,
    type ConstraintData3D,
    solveVelocityRow,
} from '../core/physics-world-3d-constraints-framework';
import { prepareConfigurable } from '../core/physics-world-3d-constraints-configurable';
import { prepareConeTwist } from '../core/physics-world-3d-constraints-cone-twist';
import type { BodyManager3D } from '../core/physics-managers-3d';

// ─── Helpers ──────────────────────────────────────────────────────────────────

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

function makeGenericDef(overrides: Partial<IGenericConstraintDef3D> = {}): IGenericConstraintDef3D {
    return {
        bodyIdA: 1,
        bodyIdB: 2,
        localFrameA: { position: { ...ZERO }, rotation: { ...IDENTITY } },
        localFrameB: { position: { ...ZERO }, rotation: { ...IDENTITY } },
        linearLowerLimit: { x: 0, y: 0, z: 0 },
        linearUpperLimit: { x: 0, y: 0, z: 0 },
        angularLowerLimit: { x: 0, y: 0, z: 0 },
        angularUpperLimit: { x: 0, y: 0, z: 0 },
        ...overrides,
    };
}

function makeConstraintData(type: number, def: any): ConstraintData3D {
    return {
        constraintId: 100 as ConstraintId3D,
        type,
        bodyIdA: def.bodyIdA,
        bodyIdB: def.bodyIdB,
        def: { kind: type, ...def } as ConstraintData3D['def'],
    };
}

function runPrepareGeneric(def: IGenericConstraintDef3D, bodyA: SolverBody3D, bodyB: SolverBody3D, dt = 1 / 60): JacobianRow3D[] {
    const data = makeConstraintData(5, def);
    const bodyMap = makeBodyMap([bodyA, bodyB]);
    const out: JacobianRow3D[] = [];
    prepareConfigurable(data, bodyMap, STUB_MANAGER, dt, out);
    return out;
}

function runPrepareConeTwist(def: IConeTwistConstraintDef3D, bodyA: SolverBody3D, bodyB: SolverBody3D, dt = 1 / 60): JacobianRow3D[] {
    const data = makeConstraintData(4, def);
    const bodyMap = makeBodyMap([bodyA, bodyB]);
    const out: JacobianRow3D[] = [];
    prepareConeTwist(data, bodyMap, STUB_MANAGER, dt, out);
    return out;
}

function solveRows(rows: JacobianRow3D[], bodyA: SolverBody3D, bodyB: SolverBody3D, iters: number): void {
    for (let i = 0; i < iters; i++) {
        for (const row of rows) {
            solveVelocityRow(row, bodyA, bodyB);
        }
    }
}

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
        const n = Math.sqrt(b.rotation.x ** 2 + b.rotation.y ** 2 + b.rotation.z ** 2 + b.rotation.w ** 2);
        b.rotation.x /= n;
        b.rotation.y /= n;
        b.rotation.z /= n;
        b.rotation.w /= n;
    }
}

// ─── 1. Configurable per-axis motor convergence ──────────────────────────────

describe('Configurable motor — velocity convergence', () => {
    it('linear motor drives body B toward target velocity on X axis', () => {
        const bodyA = makeSolverBody(1, { ...ZERO });
        bodyA.invMass = 0; // static
        const bodyB = makeSolverBody(2, { x: 1, y: 0, z: 0 });

        const targetSpeed = 5.0; // m/s
        const maxForce = 100; // N

        const def = makeGenericDef({
            linearLowerLimit: { x: -1000, y: 0, z: 0 },
            linearUpperLimit: { x: 1000, y: 0, z: 0 },
            motorSpeed: { x: targetSpeed, y: 0, z: 0 },
            maxMotorForce: { x: maxForce, y: 0, z: 0 },
        });

        // Simulate multi-step
        const dt = 1 / 60;
        for (let step = 0; step < 60; step++) {
            const rows = runPrepareGeneric(def, bodyA, bodyB, dt);
            solveRows(rows, bodyA, bodyB, 10);
            integrateBody(bodyB, dt);
        }

        // Body B should approach the target velocity (real value, not toBeDefined)
        // With 100N force and 1kg mass, max acceleration = 100 m/s²
        // After 1s, target 5 m/s should be reached well within 60 steps
        expect(bodyB.linearVelocity.x).toBeGreaterThan(3.0);
        expect(bodyB.linearVelocity.x).toBeLessThan(7.0);
    });

    it('angular motor drives body B toward target angular velocity on Z axis', () => {
        const bodyA = makeSolverBody(1, { ...ZERO });
        bodyA.invMass = 0;
        const bodyB = makeSolverBody(2, { ...ZERO });

        const targetSpeed = 3.0; // rad/s
        const maxTorque = 50; // N·m

        const def = makeGenericDef({
            angularLowerLimit: { x: -100, y: -100, z: -100 },
            angularUpperLimit: { x: 100, y: 100, z: 100 },
            angularMotorSpeed: { x: 0, y: 0, z: targetSpeed },
            angularMaxMotorTorque: { x: 0, y: 0, z: maxTorque },
        });

        const dt = 1 / 60;
        for (let step = 0; step < 60; step++) {
            const rows = runPrepareGeneric(def, bodyA, bodyB, dt);
            solveRows(rows, bodyA, bodyB, 10);
            integrateBody(bodyB, dt);
        }

        expect(bodyB.angularVelocity.z).toBeGreaterThan(1.0);
        expect(bodyB.angularVelocity.z).toBeLessThan(4.5);
    });
});

// ─── 2. Configurable motor stops at limit (Box2D stall) ──────────────────────

describe('Configurable motor — Box2D stall at limit', () => {
    it('motor cannot push past upper linear limit', () => {
        const bodyA = makeSolverBody(1, { ...ZERO });
        bodyA.invMass = 0;
        // Body B starts near the upper limit
        const bodyB = makeSolverBody(2, { x: 2.9, y: 0, z: 0 });

        const motorSpeed = 10.0; // pushes in +X direction
        const maxForce = 200;

        const def = makeGenericDef({
            linearLowerLimit: { x: -5, y: 0, z: 0 },
            linearUpperLimit: { x: 3, y: 0, z: 0 },
            motorSpeed: { x: motorSpeed, y: 0, z: 0 },
            maxMotorForce: { x: maxForce, y: 0, z: 0 },
        });

        const dt = 1 / 60;
        for (let step = 0; step < 30; step++) {
            const rows = runPrepareGeneric(def, bodyA, bodyB, dt);
            solveRows(rows, bodyA, bodyB, 10);
            integrateBody(bodyB, dt);
        }

        // Body B should NOT have passed the upper limit significantly
        expect(bodyB.position.x).toBeLessThan(3.2);
        // Velocity should be stalled well below the 10 rad/s motor target
        expect(bodyB.linearVelocity.x).toBeLessThan(2.0);
    });

    it('motor cannot push past lower linear limit', () => {
        const bodyA = makeSolverBody(1, { ...ZERO });
        bodyA.invMass = 0;
        // Body B starts near the lower limit
        const bodyB = makeSolverBody(2, { x: -2.9, y: 0, z: 0 });

        const motorSpeed = -10.0; // pushes in -X direction
        const maxForce = 200;

        const def = makeGenericDef({
            linearLowerLimit: { x: -3, y: 0, z: 0 },
            linearUpperLimit: { x: 5, y: 0, z: 0 },
            motorSpeed: { x: motorSpeed, y: 0, z: 0 },
            maxMotorForce: { x: maxForce, y: 0, z: 0 },
        });

        const dt = 1 / 60;
        for (let step = 0; step < 30; step++) {
            const rows = runPrepareGeneric(def, bodyA, bodyB, dt);
            solveRows(rows, bodyA, bodyB, 10);
            integrateBody(bodyB, dt);
        }

        // Body B should NOT have passed the lower limit
        expect(bodyB.position.x).toBeGreaterThan(-3.2);
    });

    it('motor can push away from the limit it is at', () => {
        const bodyA = makeSolverBody(1, { ...ZERO });
        bodyA.invMass = 0;
        // Body B starts at the lower limit
        const bodyB = makeSolverBody(2, { x: -3, y: 0, z: 0 });

        const motorSpeed = 5.0; // pushes AWAY from lower limit (+X direction)
        const maxForce = 100;

        const def = makeGenericDef({
            linearLowerLimit: { x: -3, y: 0, z: 0 },
            linearUpperLimit: { x: 5, y: 0, z: 0 },
            motorSpeed: { x: motorSpeed, y: 0, z: 0 },
            maxMotorForce: { x: maxForce, y: 0, z: 0 },
        });

        const dt = 1 / 60;
        for (let step = 0; step < 30; step++) {
            const rows = runPrepareGeneric(def, bodyA, bodyB, dt);
            solveRows(rows, bodyA, bodyB, 10);
            integrateBody(bodyB, dt);
        }

        // Body B should have moved away from the lower limit
        expect(bodyB.position.x).toBeGreaterThan(-2.5);
        expect(bodyB.linearVelocity.x).toBeGreaterThan(1.0);
    });
});

// ─── 3. Blow-through control ─────────────────────────────────────────────────

describe('Configurable motor — blow-through control', () => {
    it('without limits, same motor exceeds the limit position', () => {
        const bodyA = makeSolverBody(1, { ...ZERO });
        bodyA.invMass = 0;
        const bodyB = makeSolverBody(2, { x: 2.9, y: 0, z: 0 });

        const motorSpeed = 10.0;
        const maxForce = 200;

        // Wide LIMITED axis (no effective limit) — motor should push body past x=3
        const def = makeGenericDef({
            linearLowerLimit: { x: -1000, y: 0, z: 0 },
            linearUpperLimit: { x: 1000, y: 0, z: 0 },
            motorSpeed: { x: motorSpeed, y: 0, z: 0 },
            maxMotorForce: { x: maxForce, y: 0, z: 0 },
        });

        const dt = 1 / 60;
        for (let step = 0; step < 30; step++) {
            const rows = runPrepareGeneric(def, bodyA, bodyB, dt);
            solveRows(rows, bodyA, bodyB, 10);
            integrateBody(bodyB, dt);
        }

        // Without limits, body B should have blown well past x=3
        expect(bodyB.position.x).toBeGreaterThan(4.0);
        // Velocity should be close to motor target
        expect(bodyB.linearVelocity.x).toBeGreaterThan(5.0);
    });
});

// ─── 4. Opposite axis motors don't cancel ────────────────────────────────────

describe('Configurable motor — opposite axis independence', () => {
    it('motors on X and Y axes run independently', () => {
        const bodyA = makeSolverBody(1, { ...ZERO });
        bodyA.invMass = 0;
        const bodyB = makeSolverBody(2, { x: 1, y: 1, z: 0 });

        // Use LIMITED axes with wide bounds (motor works within limits)
        const def = makeGenericDef({
            linearLowerLimit: { x: -1000, y: -1000, z: 0 },
            linearUpperLimit: { x: 1000, y: 1000, z: 0 },
            motorSpeed: { x: 5.0, y: -3.0, z: 0 },
            maxMotorForce: { x: 100, y: 100, z: 0 },
        });

        const dt = 1 / 60;
        for (let step = 0; step < 60; step++) {
            const rows = runPrepareGeneric(def, bodyA, bodyB, dt);
            solveRows(rows, bodyA, bodyB, 10);
            integrateBody(bodyB, dt);
        }

        // X motor pushed B in +X, Y motor pushed B in -Y
        expect(bodyB.linearVelocity.x).toBeGreaterThan(2.0);
        expect(bodyB.linearVelocity.y).toBeLessThan(-0.5);
        // Neither axis should be zero (they don't cancel)
        expect(Math.abs(bodyB.linearVelocity.x)).toBeGreaterThan(0.5);
        expect(Math.abs(bodyB.linearVelocity.y)).toBeGreaterThan(0.5);
    });
});

// ─── 5. FREE axis produces no motor row ──────────────────────────────────────

describe('Configurable motor — FREE axis row count', () => {
    it('no motor row produced on FREE linear axis even with motor fields', () => {
        const bodyA = makeSolverBody(1, { ...ZERO });
        const bodyB = makeSolverBody(2, { x: 1, y: 0, z: 0 });

        const def = makeGenericDef({
            // X: LIMITED with motor, Y: FREE, Z: FREE
            linearLowerLimit: { x: -2, y: -Infinity, z: -Infinity },
            linearUpperLimit: { x: 2, y: Infinity, z: Infinity },
            // All angular FREE
            angularLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            angularUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
            motorSpeed: { x: 5.0, y: 3.0, z: 0 },
            maxMotorForce: { x: 100, y: 100, z: 0 },
        });

        const rows = runPrepareGeneric(def, bodyA, bodyB);

        // Only X axis should produce a motor row (Y and Z are FREE)
        const motorRows = rows.filter(r => r.hasMotor);
        expect(motorRows.length).toBe(1);

        // The motor row should be along X direction
        const motorRow = motorRows[0];
        expect(Math.abs(motorRow.j1Linear.x)).toBeGreaterThan(0.5);
        expect(Math.abs(motorRow.j1Linear.y)).toBeLessThan(0.1);
    });

    it('no motor row produced when motor fields are undefined', () => {
        const bodyA = makeSolverBody(1, { ...ZERO });
        const bodyB = makeSolverBody(2, { x: 1, y: 0, z: 0 });

        // LIMITED axis but NO motor fields
        const def = makeGenericDef({
            linearLowerLimit: { x: -2, y: 0, z: 0 },
            linearUpperLimit: { x: 2, y: 0, z: 0 },
            angularLowerLimit: { x: 0, y: 0, z: 0 },
            angularUpperLimit: { x: 0, y: 0, z: 0 },
            // No motorSpeed, no maxMotorForce
        });

        const rows = runPrepareGeneric(def, bodyA, bodyB);
        const motorRows = rows.filter(r => r.hasMotor);
        expect(motorRows.length).toBe(0);
    });
});

// ─── 6. ConeTwist motor — type fields don't change behavior ──────────────────

describe('ConeTwist motor — type safety verification', () => {
    it('twist motor still stalls at twist limit (typed fields)', () => {
        const bodyA = makeSolverBody(1, { ...ZERO });
        bodyA.invMass = 0;
        const bodyB = makeSolverBody(2, { ...ZERO });

        const twistSpan = Math.PI / 3; // 60°
        const def: IConeTwistConstraintDef3D = {
            bodyIdA: 1 as BodyId3D,
            bodyIdB: 2 as BodyId3D,
            localFrameA: { position: { ...ZERO }, rotation: { ...IDENTITY } },
            localFrameB: { position: { ...ZERO }, rotation: { ...IDENTITY } },
            swingSpan1: Math.PI / 6,
            swingSpan2: Math.PI / 6,
            twistSpan,
            motorSpeed: 6,
            maxMotorTorque: 25,
        };

        const dt = 1 / 60;
        const upper = twistSpan / 2;

        // Simulate
        for (let step = 0; step < 120; step++) {
            const rows = runPrepareConeTwist(def, bodyA, bodyB, dt);
            solveRows(rows, bodyA, bodyB, 10);
            integrateBody(bodyB, dt);
        }

        // Twist angle should be near the upper limit but not past it
        // Measure twist via quaternion decomposition
        const conjA: IQuatLike = { x: -bodyA.rotation.x, y: -bodyA.rotation.y, z: -bodyA.rotation.z, w: bodyA.rotation.w };
        const qRel = Quat.multiply(conjA, bodyB.rotation);
        // Twist = 2 * atan2(qRel.x, qRel.w) (about X axis)
        const twist = 2 * Math.atan2(qRel.x, qRel.w);

        // Motor drove twist near the rim
        expect(Math.abs(twist)).toBeGreaterThan(upper - 0.3);
        // But NOT past the rim + tolerance
        expect(Math.abs(twist)).toBeLessThanOrEqual(upper + 0.1);
        // Angular velocity stalled well below motor target
        expect(Math.abs(bodyB.angularVelocity.x)).toBeLessThan(2.0);
    });

    it('motor fields undefined produces no motor row', () => {
        const bodyA = makeSolverBody(1, { ...ZERO });
        const bodyB = makeSolverBody(2, { ...ZERO });

        const def: IConeTwistConstraintDef3D = {
            bodyIdA: 1 as BodyId3D,
            bodyIdB: 2 as BodyId3D,
            localFrameA: { position: { ...ZERO }, rotation: { ...IDENTITY } },
            localFrameB: { position: { ...ZERO }, rotation: { ...IDENTITY } },
            swingSpan1: Math.PI / 6,
            swingSpan2: Math.PI / 6,
            twistSpan: Math.PI / 3,
            // No motorSpeed, no maxMotorTorque
        };

        const rows = runPrepareConeTwist(def, bodyA, bodyB);
        const motorRows = rows.filter(r => r.hasMotor);
        expect(motorRows.length).toBe(0);
    });
});
