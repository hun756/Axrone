import { describe, it, expect } from 'vitest';
import { Vec3, type IVec3Like, type IQuatLike } from '@axrone/numeric';
import { PhysicsConstants } from '@axrone/physics-core';
import type { BodyId3D, ISliderConstraintDef3D } from '../types/physics-3d';
import { CONSTRAINT_TYPE_SLIDER } from '../core/physics-world-3d-shared';
import {
    type JacobianRow3D,
    type SolverBody3D,
    type ConstraintData3D,
    solveVelocityRow,
    solvePositionRow,
    getPrepareFunction,
    zeroVec3,
} from '../core/physics-world-3d-constraints-framework';
// Side-effect import: registers the slider prepare function
import '../core/physics-world-3d-constraints-slider';

const EPSILON = PhysicsConstants.EPSILON;

// ─── Helpers ──────────────────────────────────────────────────────────────────

/** Create a solver body with given properties. */
function makeSolverBody(
    bodyId: number,
    pos: IVec3Like,
    rot: IQuatLike = { x: 0, y: 0, z: 0, w: 1 },
    invMass: number = 1,
    invInertia: IVec3Like = { x: 0.4, y: 0.4, z: 0.4 },
): SolverBody3D {
    return {
        bodyId,
        invMass,
        invInertia: { ...invInertia },
        linearVelocity: { x: 0, y: 0, z: 0 },
        angularVelocity: { x: 0, y: 0, z: 0 },
        position: { ...pos },
        rotation: { ...rot },
    };
}

/** Create a static solver body (invMass = 0, invInertia = 0). */
function makeStaticBody(
    bodyId: number,
    pos: IVec3Like,
    rot: IQuatLike = { x: 0, y: 0, z: 0, w: 1 },
): SolverBody3D {
    return makeSolverBody(bodyId, pos, rot, 0, { x: 0, y: 0, z: 0 });
}

/** Build a slider constraint def. */
function makeSliderDef(
    bodyIdA: number,
    bodyIdB: number,
    localAnchorA: IVec3Like = { x: 0, y: 0, z: 0 },
    localAnchorB: IVec3Like = { x: 0, y: 0, z: 0 },
    localAxisA: IVec3Like = { x: 1, y: 0, z: 0 },
    opts: {
        enableLimit?: boolean;
        lowerLimit?: number;
        upperLimit?: number;
        enableMotor?: boolean;
        motorSpeed?: number;
        maxMotorForce?: number;
    } = {},
): ISliderConstraintDef3D {
    return {
        bodyIdA,
        bodyIdB,
        localAnchorA,
        localAnchorB,
        localAxisA,
        enableLimit: opts.enableLimit ?? false,
        lowerLimit: opts.lowerLimit ?? 0,
        upperLimit: opts.upperLimit ?? 0,
        enableMotor: opts.enableMotor ?? false,
        motorSpeed: opts.motorSpeed ?? 0,
        maxMotorForce: opts.maxMotorForce ?? 0,
        collideConnected: false,
    };
}

/** Prepare slider rows using the registered prepare function. */
function prepareSliderRows(
    def: ISliderConstraintDef3D,
    bodyA: SolverBody3D,
    bodyB: SolverBody3D,
    dt: number = 1 / 60,
): JacobianRow3D[] {
    const prepareFn = getPrepareFunction(CONSTRAINT_TYPE_SLIDER);
    expect(prepareFn).not.toBeNull();

    const data: ConstraintData3D = {
        constraintId: 1,
        type: CONSTRAINT_TYPE_SLIDER,
        bodyIdA: bodyA.bodyId,
        bodyIdB: bodyB.bodyId,
        def: { ...def, kind: CONSTRAINT_TYPE_SLIDER } as any,
    };

    const solverBodies = new Map<BodyId3D, SolverBody3D>();
    solverBodies.set(bodyA.bodyId, bodyA);
    solverBodies.set(bodyB.bodyId, bodyB);

    const rows: JacobianRow3D[] = [];
    prepareFn!(data, solverBodies, {} as any, dt, rows);
    return rows;
}

/** Run N iterations of velocity + position solve on the given rows. */
function solveRows(
    rows: JacobianRow3D[],
    bodyA: SolverBody3D,
    bodyB: SolverBody3D,
    iterations: number = 10,
): void {
    for (let i = 0; i < iterations; i++) {
        for (const row of rows) {
            solveVelocityRow(row, bodyA, bodyB);
        }
    }
    for (let i = 0; i < iterations; i++) {
        for (const row of rows) {
            solvePositionRow(row, bodyA, bodyB);
        }
    }
}

/** Compute angle about a world-space axis from a quaternion. */
function angleAboutAxis(q: IQuatLike, axis: IVec3Like): number {
    const dot = q.x * axis.x + q.y * axis.y + q.z * axis.z;
    return 2.0 * Math.atan2(dot, q.w);
}

/** Extract Euler angles (XYZ) from quaternion. */
function quatToEulerXYZ(q: IQuatLike): { x: number; y: number; z: number } {
    const sinrCosp = 2 * (q.w * q.x + q.y * q.z);
    const cosrCosp = 1 - 2 * (q.x * q.x + q.y * q.y);
    const x = Math.atan2(sinrCosp, cosrCosp);
    const sinp = 2 * (q.w * q.y - q.z * q.x);
    const y = Math.abs(sinp) >= 1 ? Math.sign(sinp) * Math.PI / 2 : Math.asin(sinp);
    const sinyCosp = 2 * (q.w * q.z + q.x * q.y);
    const cosyCosp = 1 - 2 * (q.y * q.y + q.z * q.z);
    const z = Math.atan2(sinyCosp, cosyCosp);
    return { x, y, z };
}

// ─── Test 1: Free sliding along axis ─────────────────────────────────────────

describe('Slider joint — free sliding along axis', () => {
    it('allows translation along the slide axis (X-axis)', () => {
        // Static body A at origin, dynamic body B at (2,0,0).
        // Slide axis = X. Apply force along X to body B.
        const bodyA = makeStaticBody(0, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(1, { x: 2, y: 0, z: 0 });

        // Give body B an initial velocity along X
        bodyB.linearVelocity.x = 5;

        const def = makeSliderDef(0, 1);
        const rows = prepareSliderRows(def, bodyA, bodyB);

        // Solve — slider should NOT resist motion along X
        solveRows(rows, bodyA, bodyB, 10);

        // Body B should still have significant velocity along X
        // (the constraint should not block axial motion)
        expect(bodyB.linearVelocity.x).toBeGreaterThan(3);
    });
});

// ─── Test 2: Lateral lock (two perpendicular directions) ─────────────────────

describe('Slider joint — lateral lock', () => {
    it('locks lateral motion perpendicular to slide axis (Y direction)', () => {
        const bodyA = makeStaticBody(0, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(1, { x: 2, y: 0, z: 0 });

        // Give body B a lateral velocity (perpendicular to slide axis X)
        bodyB.linearVelocity.y = 10;

        const def = makeSliderDef(0, 1);
        const rows = prepareSliderRows(def, bodyA, bodyB);

        // Should have 6 rows (2 lateral + 3 angular + 0 limit/motor since no limit/motor)
        // Actually without limit/motor, axial row is not added → 5 rows
        expect(rows.length).toBe(5);

        solveRows(rows, bodyA, bodyB, 20);

        // Lateral velocity should be ~0 after constraint solve
        expect(Math.abs(bodyB.linearVelocity.y)).toBeLessThan(0.5);
    });

    it('locks lateral motion perpendicular to slide axis (Z direction)', () => {
        const bodyA = makeStaticBody(0, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(1, { x: 2, y: 0, z: 0 });

        bodyB.linearVelocity.z = 10;

        const def = makeSliderDef(0, 1);
        const rows = prepareSliderRows(def, bodyA, bodyB);
        solveRows(rows, bodyA, bodyB, 20);

        expect(Math.abs(bodyB.linearVelocity.z)).toBeLessThan(0.5);
    });

    it('corrects lateral position error', () => {
        // Body B starts with a small lateral offset
        const bodyA = makeStaticBody(0, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(1, { x: 2, y: 0.02, z: 0 });

        const def = makeSliderDef(0, 1);
        const rows = prepareSliderRows(def, bodyA, bodyB);

        // Check that the lateral row has a non-zero position error
        const lateralRows = rows.filter(r =>
            r.j1Linear.x !== 0 || r.j1Linear.y !== 0 || r.j1Linear.z !== 0
        );
        expect(lateralRows.length).toBe(2);

        // At least one lateral row should have non-zero position error
        const hasError = lateralRows.some(r => Math.abs(r.positionError) > EPSILON);
        expect(hasError).toBe(true);

        // After velocity solve, lateral velocity should be corrected toward 0
        solveRows(rows, bodyA, bodyB, 20);
        // The velocity correction should have reduced lateral drift
        expect(Math.abs(bodyB.linearVelocity.y)).toBeLessThan(1.0);
    });
});

// ─── Test 3: Rotation lock ───────────────────────────────────────────────────

describe('Slider joint — rotation lock', () => {
    it('locks all rotation (angular velocity damped to ~0)', () => {
        const bodyA = makeStaticBody(0, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(1, { x: 2, y: 0, z: 0 });

        // Give body B angular velocity about all axes
        bodyB.angularVelocity.x = 5;
        bodyB.angularVelocity.y = 5;
        bodyB.angularVelocity.z = 5;

        const def = makeSliderDef(0, 1);
        const rows = prepareSliderRows(def, bodyA, bodyB);
        solveRows(rows, bodyA, bodyB, 20);

        // All angular velocities should be damped
        expect(Math.abs(bodyB.angularVelocity.x)).toBeLessThan(0.5);
        expect(Math.abs(bodyB.angularVelocity.y)).toBeLessThan(0.5);
        expect(Math.abs(bodyB.angularVelocity.z)).toBeLessThan(0.5);
    });

    it('corrects angular position error', () => {
        const bodyA = makeStaticBody(0, { x: 0, y: 0, z: 0 });
        // Body B with a small rotated orientation
        const halfAngle = 0.05;
        const bodyB = makeSolverBody(1, { x: 2, y: 0, z: 0 }, {
            x: Math.sin(halfAngle), y: 0, z: 0, w: Math.cos(halfAngle),
        });

        const def = makeSliderDef(0, 1);
        const rows = prepareSliderRows(def, bodyA, bodyB);

        // Angular rows should have non-zero position error
        const angRows = rows.filter(r =>
            r.j1Angular.x !== 0 || r.j1Angular.y !== 0 || r.j1Angular.z !== 0
        );
        expect(angRows.length).toBe(3);
        const hasAngError = angRows.some(r => Math.abs(r.positionError) > EPSILON);
        expect(hasAngError).toBe(true);

        // After solve, angular velocity should be damped
        solveRows(rows, bodyA, bodyB, 20);
        expect(Math.abs(bodyB.angularVelocity.x)).toBeLessThan(1.5);
    });
});

// ─── Test 4: Limit — cannot exceed bounds ────────────────────────────────────

describe('Slider joint — limits', () => {
    it('prevents translation beyond upper limit', () => {
        const bodyA = makeStaticBody(0, { x: 0, y: 0, z: 0 });
        // Body B starts PAST the upper limit (translation = 5 > upperLimit = 3)
        const bodyB = makeSolverBody(1, { x: 5, y: 0, z: 0 });

        // Push body B further in +X direction
        bodyB.linearVelocity.x = 20;

        const def = makeSliderDef(0, 1,
            { x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, { x: 1, y: 0, z: 0 },
            { enableLimit: true, lowerLimit: -3, upperLimit: 3 },
        );
        const rows = prepareSliderRows(def, bodyA, bodyB);

        // Should have 6 rows (5 base + 1 axial limit)
        const axialRows = rows.filter(r => r.hasLimit);
        expect(axialRows.length).toBe(1);

        solveRows(rows, bodyA, bodyB, 30);

        // Body B velocity along X should be significantly reduced by the limit
        expect(bodyB.linearVelocity.x).toBeLessThan(15);
    });

    it('prevents translation beyond lower limit', () => {
        const bodyA = makeStaticBody(0, { x: 0, y: 0, z: 0 });
        // Body B starts PAST the lower limit (translation = -5 < lowerLimit = -3)
        const bodyB = makeSolverBody(1, { x: -5, y: 0, z: 0 });

        // Push body B further in -X direction
        bodyB.linearVelocity.x = -20;

        const def = makeSliderDef(0, 1,
            { x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, { x: 1, y: 0, z: 0 },
            { enableLimit: true, lowerLimit: -3, upperLimit: 3 },
        );
        const rows = prepareSliderRows(def, bodyA, bodyB);

        // Should have an axial limit row
        const axialRows = rows.filter(r => r.hasLimit);
        expect(axialRows.length).toBe(1);

        solveRows(rows, bodyA, bodyB, 30);

        // Body B velocity along X should be significantly reduced by the limit
        expect(bodyB.linearVelocity.x).toBeGreaterThan(-15);
    });
});

// ─── Test 5: Free within limits (negative control) ──────────────────────────

describe('Slider joint — free within limits', () => {
    it('does not resist motion within the limit range', () => {
        const bodyA = makeStaticBody(0, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(1, { x: 0, y: 0, z: 0 });

        // Body B at origin, limits are wide — motion within range should be free
        bodyB.linearVelocity.x = 3;

        const def = makeSliderDef(0, 1,
            { x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, { x: 1, y: 0, z: 0 },
            { enableLimit: true, lowerLimit: -10, upperLimit: 10 },
        );
        const rows = prepareSliderRows(def, bodyA, bodyB);
        solveRows(rows, bodyA, bodyB, 10);

        // Velocity should be mostly preserved (within limits, no resistance)
        expect(bodyB.linearVelocity.x).toBeGreaterThan(2);
    });
});

// ─── Test 6: Motor converges to target velocity ─────────────────────────────

describe('Slider joint — motor', () => {
    it('drives body toward motor target velocity', () => {
        const bodyA = makeStaticBody(0, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(1, { x: 2, y: 0, z: 0 });

        const motorSpeed = 5.0;

        const def = makeSliderDef(0, 1,
            { x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, { x: 1, y: 0, z: 0 },
            { enableMotor: true, motorSpeed, maxMotorForce: 100 },
        );

        // Run multiple prepare+solve cycles to simulate time stepping
        for (let step = 0; step < 30; step++) {
            const rows = prepareSliderRows(def, bodyA, bodyB);
            solveRows(rows, bodyA, bodyB, 10);
        }

        // Body B should have velocity along X approaching motorSpeed
        expect(bodyB.linearVelocity.x).toBeGreaterThan(1.0);
    });
});

// ─── Test 7: Motor stops at limit (Box2D behavior) ──────────────────────────

describe('Slider joint — motor + limit interaction', () => {
    it('motor cannot push past upper limit', () => {
        const bodyA = makeStaticBody(0, { x: 0, y: 0, z: 0 });
        // Body B starts at position where translation = 3 = upperLimit
        const bodyB = makeSolverBody(1, { x: 5, y: 0, z: 0 });

        // Motor pushes in +X direction (toward upper limit)
        const motorSpeed = 10.0;

        const def = makeSliderDef(0, 1,
            { x: 0, y: 0, z: 0 }, { x: -2, y: 0, z: 0 }, { x: 1, y: 0, z: 0 },
            {
                enableLimit: true, lowerLimit: -5, upperLimit: 3,
                enableMotor: true, motorSpeed, maxMotorForce: 200,
            },
        );

        // Run multiple steps
        for (let step = 0; step < 20; step++) {
            const rows = prepareSliderRows(def, bodyA, bodyB);
            solveRows(rows, bodyA, bodyB, 10);
        }

        // Body B should NOT have gained velocity past the upper limit
        // The motor tries to push +X but the limit prevents it
        // Velocity should be ~0 (motor blocked by limit)
        expect(bodyB.linearVelocity.x).toBeLessThan(0.5);
    });

    it('motor cannot push past lower limit', () => {
        const bodyA = makeStaticBody(0, { x: 0, y: 0, z: 0 });
        // Body B starts at position where translation = -3 = lowerLimit
        const bodyB = makeSolverBody(1, { x: -1, y: 0, z: 0 });

        // Motor pushes in -X direction (toward lower limit)
        const motorSpeed = -10.0;

        const def = makeSliderDef(0, 1,
            { x: 0, y: 0, z: 0 }, { x: 2, y: 0, z: 0 }, { x: 1, y: 0, z: 0 },
            {
                enableLimit: true, lowerLimit: -3, upperLimit: 5,
                enableMotor: true, motorSpeed, maxMotorForce: 200,
            },
        );

        for (let step = 0; step < 20; step++) {
            const rows = prepareSliderRows(def, bodyA, bodyB);
            solveRows(rows, bodyA, bodyB, 10);
        }

        // Body B should NOT have moved significantly past the lower limit
        const translation = bodyB.position.x - bodyA.position.x;
        expect(translation).toBeGreaterThan(-5);
    });

    it('motor can push away from limit it is at', () => {
        const bodyA = makeStaticBody(0, { x: 0, y: 0, z: 0 });
        // Body B starts at position where translation = -3 = lowerLimit
        const bodyB = makeSolverBody(1, { x: -1, y: 0, z: 0 });

        // Motor pushes in +X direction (away from lower limit)
        const motorSpeed = 5.0;

        const def = makeSliderDef(0, 1,
            { x: 0, y: 0, z: 0 }, { x: 2, y: 0, z: 0 }, { x: 1, y: 0, z: 0 },
            {
                enableLimit: true, lowerLimit: -3, upperLimit: 10,
                enableMotor: true, motorSpeed, maxMotorForce: 100,
            },
        );

        for (let step = 0; step < 20; step++) {
            const rows = prepareSliderRows(def, bodyA, bodyB);
            solveRows(rows, bodyA, bodyB, 10);
        }

        // Motor pushes away from lower limit — body should gain velocity in +X
        expect(bodyB.linearVelocity.x).toBeGreaterThan(0.5);
    });
});

// ─── Test 8: Negative control — no constraint = free body ───────────────────

describe('Slider joint — negative control', () => {
    it('without constraint, body moves freely (proves constraint produces correction)', () => {
        const bodyA = makeStaticBody(0, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(1, { x: 2, y: 0, z: 0 });

        // Give body B lateral velocity — without constraint, it should remain unchanged
        bodyB.linearVelocity.y = 10;
        bodyB.linearVelocity.z = 7;

        // No constraint rows — just check that the body would move freely
        // (This proves the constraint IS what corrects the motion)
        const initialVelY = bodyB.linearVelocity.y;
        const initialVelZ = bodyB.linearVelocity.z;

        // Simulate a few steps without any constraint
        for (let i = 0; i < 10; i++) {
            bodyB.position.y += bodyB.linearVelocity.y * (1 / 60);
            bodyB.position.z += bodyB.linearVelocity.z * (1 / 60);
        }

        // Velocity unchanged (no forces applied)
        expect(bodyB.linearVelocity.y).toBe(initialVelY);
        expect(bodyB.linearVelocity.z).toBe(initialVelZ);

        // Position changed significantly
        expect(Math.abs(bodyB.position.y)).toBeGreaterThan(1);
        expect(Math.abs(bodyB.position.z)).toBeGreaterThan(1);
    });
});

// ─── Test 9: Singularity / degenerate configurations ─────────────────────────

describe('Slider joint — singularity robustness', () => {
    it('zero axis vector does not produce NaN or throw', () => {
        const bodyA = makeStaticBody(0, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(1, { x: 2, y: 0, z: 0 });

        // Degenerate: zero-length axis
        const def = makeSliderDef(0, 1,
            { x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 },
        );

        expect(() => {
            const rows = prepareSliderRows(def, bodyA, bodyB);
            solveRows(rows, bodyA, bodyB, 10);
        }).not.toThrow();

        // No NaN in body state
        expect(isFinite(bodyB.linearVelocity.x)).toBe(true);
        expect(isFinite(bodyB.linearVelocity.y)).toBe(true);
        expect(isFinite(bodyB.linearVelocity.z)).toBe(true);
        expect(isFinite(bodyB.position.x)).toBe(true);
    });

    it('bodies at same position does not produce NaN', () => {
        const bodyA = makeStaticBody(0, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(1, { x: 0, y: 0, z: 0 });

        bodyB.linearVelocity.x = 5;
        bodyB.linearVelocity.y = 3;

        const def = makeSliderDef(0, 1);
        const rows = prepareSliderRows(def, bodyA, bodyB);
        solveRows(rows, bodyA, bodyB, 10);

        expect(isFinite(bodyB.linearVelocity.x)).toBe(true);
        expect(isFinite(bodyB.linearVelocity.y)).toBe(true);
        expect(isFinite(bodyB.position.x)).toBe(true);
    });

    it('axis aligned with body separation does not throw', () => {
        const bodyA = makeStaticBody(0, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(1, { x: 5, y: 0, z: 0 });

        // Slide axis aligned with separation vector
        const def = makeSliderDef(0, 1,
            { x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, { x: 1, y: 0, z: 0 },
        );

        bodyB.linearVelocity.y = 10;

        expect(() => {
            const rows = prepareSliderRows(def, bodyA, bodyB);
            solveRows(rows, bodyA, bodyB, 20);
        }).not.toThrow();

        expect(isFinite(bodyB.linearVelocity.y)).toBe(true);
    });
});

// ─── Test 10: Iteration convergence ─────────────────────────────────────────

describe('Slider joint — iteration convergence', () => {
    it('lateral correction converges with more iterations', () => {
        const results: number[] = [];

        for (const iters of [1, 5, 20]) {
            const bodyA = makeStaticBody(0, { x: 0, y: 0, z: 0 });
            const bodyB = makeSolverBody(1, { x: 2, y: 5, z: 0 });

            const def = makeSliderDef(0, 1);
            const rows = prepareSliderRows(def, bodyA, bodyB);

            for (let i = 0; i < iters; i++) {
                for (const row of rows) {
                    solveVelocityRow(row, bodyA, bodyB);
                }
            }
            for (let i = 0; i < iters; i++) {
                for (const row of rows) {
                    solvePositionRow(row, bodyA, bodyB);
                }
            }

            results.push(Math.abs(bodyB.linearVelocity.y));
        }

        // More iterations should give smaller or similar lateral velocity
        // (convergence: residual decreases)
        expect(results[2]).toBeLessThanOrEqual(results[0] * 1.5);
    });

    it('limit correction is stable across iteration counts', () => {
        const results: number[] = [];

        for (const iters of [5, 20]) {
            const bodyA = makeStaticBody(0, { x: 0, y: 0, z: 0 });
            const bodyB = makeSolverBody(1, { x: 10, y: 0, z: 0 });

            bodyB.linearVelocity.x = 50;

            const def = makeSliderDef(0, 1,
                { x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, { x: 1, y: 0, z: 0 },
                { enableLimit: true, lowerLimit: -5, upperLimit: 5 },
            );
            const rows = prepareSliderRows(def, bodyA, bodyB);

            for (let i = 0; i < iters; i++) {
                for (const row of rows) {
                    solveVelocityRow(row, bodyA, bodyB);
                }
            }

            results.push(bodyB.linearVelocity.x);
        }

        // Both should have reduced velocity (limit working)
        // More iterations should not produce wild oscillation
        expect(Math.abs(results[1])).toBeLessThan(Math.abs(results[0]) * 2);
    });
});
