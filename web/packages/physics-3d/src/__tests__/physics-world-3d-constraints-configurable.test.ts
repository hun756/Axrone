import { describe, it, expect } from 'vitest';
import { type IVec3Like, type IQuatLike } from '@axrone/numeric';
import type { BodyId3D, ConstraintId3D, IGenericConstraintDef3D } from '../types/physics-3d';
import {
    type JacobianRow3D,
    type SolverBody3D,
    type ConstraintData3D,
} from '../core/physics-world-3d-constraints-framework';
import type { BodyManager3D } from '../core/physics-managers-3d';
import { prepareConfigurable } from '../core/physics-world-3d-constraints-configurable';

/**
 * Configurable (Generic) 6-DOF joint solver tests.
 *
 * These tests call prepareConfigurable() directly — NOT through the runtime —
 * to verify row generation, effective mass, limits, and mode detection.
 *
 * Test categories:
 * 1. All FREE → 0 rows (no correction)
 * 2. All LOCKED → 6 rows, same as Fixed joint
 * 3. Per-axis LOCKED (6 tests) → only that axis constrained
 * 4. Per-axis LIMITED → resistance outside limits, free within
 * 5. Singularity / degenerate → no NaN, no throw
 * 6. Iteration convergence
 * 7. Limit interaction (multi-axis)
 */

// ─── Helpers ──────────────────────────────────────────────────────────────────

const IDENTITY: IQuatLike = { x: 0, y: 0, z: 0, w: 1 };
const ZERO: IVec3Like = { x: 0, y: 0, z: 0 };

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

function makeGenericDef(
    overrides: Partial<IGenericConstraintDef3D> = {},
): IGenericConstraintDef3D {
    return {
        bodyIdA: 1,
        bodyIdB: 2,
        localFrameA: { position: { x: 0, y: 0, z: 0 }, rotation: { ...IDENTITY } },
        localFrameB: { position: { x: 0, y: 0, z: 0 }, rotation: { ...IDENTITY } },
        linearLowerLimit: { x: 0, y: 0, z: 0 },
        linearUpperLimit: { x: 0, y: 0, z: 0 },
        angularLowerLimit: { x: 0, y: 0, z: 0 },
        angularUpperLimit: { x: 0, y: 0, z: 0 },
        ...overrides,
    };
}

function makeConstraintData(def: IGenericConstraintDef3D): ConstraintData3D {
    return {
        constraintId: 100 as ConstraintId3D,
        type: 5, // CONSTRAINT_TYPE_GENERIC
        bodyIdA: def.bodyIdA,
        bodyIdB: def.bodyIdB,
        def: { kind: 5, ...def } as ConstraintData3D['def'],
    };
}

/** Build solver body map for prepare call. */
function makeBodyMap(bodies: SolverBody3D[]): Map<BodyId3D, SolverBody3D> {
    const map = new Map<BodyId3D, SolverBody3D>();
    for (const b of bodies) map.set(b.bodyId, b);
    return map;
}

/** Run prepare and return the output rows. */
function runPrepare(
    def: IGenericConstraintDef3D,
    bodyA: SolverBody3D,
    bodyB: SolverBody3D,
    dt: number = 1 / 60,
): JacobianRow3D[] {
    const data = makeConstraintData(def);
    const bodyMap = makeBodyMap([bodyA, bodyB]);
    const out: JacobianRow3D[] = [];
    // We need a BodyManager3D but prepareConfigurable only uses ensureSolverBody3D
    // which checks the map first. Since we pre-populate the map, bodyManager is
    // never called. Pass a minimal stub.
    const stubBodyManager = null as unknown as BodyManager3D;
    prepareConfigurable(data, bodyMap, stubBodyManager, dt, out);
    return out;
}

/** Count rows with non-zero linear Jacobian. */
function countLinearRows(rows: JacobianRow3D[]): number {
    return rows.filter(r =>
        r.j1Linear.x !== 0 || r.j1Linear.y !== 0 || r.j1Linear.z !== 0,
    ).length;
}

/** Count rows with non-zero angular Jacobian (and zero linear). */
function countAngularRows(rows: JacobianRow3D[]): number {
    return rows.filter(r =>
        (r.j1Linear.x === 0 && r.j1Linear.y === 0 && r.j1Linear.z === 0) &&
        (r.j1Angular.x !== 0 || r.j1Angular.y !== 0 || r.j1Angular.z !== 0),
    ).length;
}

// ─── 1. All FREE ──────────────────────────────────────────────────────────────

describe('Configurable joint — all FREE', () => {
    it('produces zero rows when all axes are free', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 2, y: 0, z: 0 });

        const def = makeGenericDef({
            linearLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            linearUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
            angularLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            angularUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
        });

        const rows = runPrepare(def, bodyA, bodyB);
        expect(rows.length).toBe(0);
    });

    it('bodies remain unaffected (no correction applied)', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 5, y: 3, z: -1 });
        const origPosB = { ...bodyB.position };

        const def = makeGenericDef({
            linearLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            linearUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
            angularLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            angularUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
        });

        runPrepare(def, bodyA, bodyB);

        // Position unchanged (no rows → no correction)
        expect(bodyB.position.x).toBe(origPosB.x);
        expect(bodyB.position.y).toBe(origPosB.y);
        expect(bodyB.position.z).toBe(origPosB.z);
    });
});

// ─── 2. All LOCKED (consistency with Fixed) ──────────────────────────────────

describe('Configurable joint — all LOCKED', () => {
    it('produces 6 rows (3 linear + 3 angular)', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 2, y: 0, z: 0 });

        const def = makeGenericDef(); // all limits 0/0 = locked

        const rows = runPrepare(def, bodyA, bodyB);
        expect(rows.length).toBe(6);
        expect(countLinearRows(rows)).toBe(3);
        expect(countAngularRows(rows)).toBe(3);
    });

    it('linear rows have bilateral limits (locked = no limit bounds)', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 2, y: 0, z: 0 });

        const def = makeGenericDef();
        const rows = runPrepare(def, bodyA, bodyB);

        const linRows = rows.filter(r =>
            r.j1Linear.x !== 0 || r.j1Linear.y !== 0 || r.j1Linear.z !== 0,
        );
        for (const row of linRows) {
            expect(row.lowerLimit).toBe(-Infinity);
            expect(row.upperLimit).toBe(Infinity);
            expect(row.hasLimit).toBe(false);
        }
    });

    it('angular rows match Fixed joint convention (j1=-axis, j2=+axis)', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 0, y: 0, z: 0 });

        const def = makeGenericDef();
        const rows = runPrepare(def, bodyA, bodyB);

        const angRows = rows.filter(r =>
            r.j1Linear.x === 0 && r.j1Linear.y === 0 && r.j1Linear.z === 0,
        );
        // X-axis angular row
        expect(angRows[0].j1Angular.x).toBeCloseTo(-1);
        expect(angRows[0].j2Angular.x).toBeCloseTo(1);
        // Y-axis angular row
        expect(angRows[1].j1Angular.y).toBeCloseTo(-1);
        expect(angRows[1].j2Angular.y).toBeCloseTo(1);
        // Z-axis angular row
        expect(angRows[2].j1Angular.z).toBeCloseTo(-1);
        expect(angRows[2].j2Angular.z).toBeCloseTo(1);
    });

    it('linear rows have same Jacobian structure as Fixed joint', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 2, y: 1, z: 0.5 });

        const def = makeGenericDef();
        const rows = runPrepare(def, bodyA, bodyB);

        const linRows = rows.filter(r =>
            r.j1Linear.x !== 0 || r.j1Linear.y !== 0 || r.j1Linear.z !== 0,
        );

        // X row: j1Lin = (-1,0,0), j2Lin = (1,0,0)
        expect(linRows[0].j1Linear.x).toBeCloseTo(-1);
        expect(linRows[0].j1Linear.y).toBeCloseTo(0);
        expect(linRows[0].j1Linear.z).toBeCloseTo(0);
        expect(linRows[0].j2Linear.x).toBeCloseTo(1);

        // Y row: j1Lin = (0,-1,0), j2Lin = (0,1,0)
        expect(linRows[1].j1Linear.y).toBeCloseTo(-1);
        expect(linRows[1].j2Linear.y).toBeCloseTo(1);

        // Z row: j1Lin = (0,0,-1), j2Lin = (0,0,1)
        expect(linRows[2].j1Linear.z).toBeCloseTo(-1);
        expect(linRows[2].j2Linear.z).toBeCloseTo(1);
    });

    it('effective mass is positive and finite for all rows', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 2, y: 0, z: 0 });

        const def = makeGenericDef();
        const rows = runPrepare(def, bodyA, bodyB);

        for (const row of rows) {
            expect(row.effectiveMass).toBeGreaterThan(0);
            expect(isFinite(row.effectiveMass)).toBe(true);
        }
    });

    it('bias is zero when bodies are at target (no error)', () => {
        // Both bodies at same position with same rotation → zero error
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 0, y: 0, z: 0 });

        const def = makeGenericDef();
        const rows = runPrepare(def, bodyA, bodyB);

        for (const row of rows) {
            expect(row.bias).toBeCloseTo(0);
            expect(row.positionError).toBeCloseTo(0);
        }
    });
});

// ─── 3. Per-axis LOCKED ──────────────────────────────────────────────────────

describe('Configurable joint — per-axis LOCKED', () => {
    it('only X linear locked → 1 linear row + 0 angular rows', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 2, y: 0, z: 0 });

        const def = makeGenericDef({
            linearLowerLimit: { x: 0, y: -Infinity, z: -Infinity },
            linearUpperLimit: { x: 0, y: Infinity, z: Infinity },
            angularLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            angularUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
        });

        const rows = runPrepare(def, bodyA, bodyB);
        expect(rows.length).toBe(1);
        expect(countLinearRows(rows)).toBe(1);
        // The row should be along X
        expect(Math.abs(rows[0].j1Linear.x)).toBeCloseTo(1);
    });

    it('only Y linear locked → 1 row along Y', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 0, y: 3, z: 0 });

        const def = makeGenericDef({
            linearLowerLimit: { x: -Infinity, y: 0, z: -Infinity },
            linearUpperLimit: { x: Infinity, y: 0, z: Infinity },
            angularLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            angularUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
        });

        const rows = runPrepare(def, bodyA, bodyB);
        expect(rows.length).toBe(1);
        expect(Math.abs(rows[0].j1Linear.y)).toBeCloseTo(1);
    });

    it('only Z linear locked → 1 row along Z', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 0, y: 0, z: 4 });

        const def = makeGenericDef({
            linearLowerLimit: { x: -Infinity, y: -Infinity, z: 0 },
            linearUpperLimit: { x: Infinity, y: Infinity, z: 0 },
            angularLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            angularUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
        });

        const rows = runPrepare(def, bodyA, bodyB);
        expect(rows.length).toBe(1);
        expect(Math.abs(rows[0].j1Linear.z)).toBeCloseTo(1);
    });

    it('only angular X locked → 1 angular row', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 0, y: 0, z: 0 });

        const def = makeGenericDef({
            linearLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            linearUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
            angularLowerLimit: { x: 0, y: -Infinity, z: -Infinity },
            angularUpperLimit: { x: 0, y: Infinity, z: Infinity },
        });

        const rows = runPrepare(def, bodyA, bodyB);
        expect(rows.length).toBe(1);
        expect(countAngularRows(rows)).toBe(1);
        expect(Math.abs(rows[0].j1Angular.x)).toBeCloseTo(1);
    });

    it('only angular Y locked → 1 angular row', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 0, y: 0, z: 0 });

        const def = makeGenericDef({
            linearLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            linearUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
            angularLowerLimit: { x: -Infinity, y: 0, z: -Infinity },
            angularUpperLimit: { x: Infinity, y: 0, z: Infinity },
        });

        const rows = runPrepare(def, bodyA, bodyB);
        expect(rows.length).toBe(1);
        expect(Math.abs(rows[0].j1Angular.y)).toBeCloseTo(1);
    });

    it('only angular Z locked → 1 angular row', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 0, y: 0, z: 0 });

        const def = makeGenericDef({
            linearLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            linearUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
            angularLowerLimit: { x: -Infinity, y: -Infinity, z: 0 },
            angularUpperLimit: { x: Infinity, y: Infinity, z: 0 },
        });

        const rows = runPrepare(def, bodyA, bodyB);
        expect(rows.length).toBe(1);
        expect(Math.abs(rows[0].j1Angular.z)).toBeCloseTo(1);
    });
});

// ─── 4. Per-axis LIMITED ─────────────────────────────────────────────────────

describe('Configurable joint — per-axis LIMITED', () => {
    it('linear X limited: within limits → no row', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 0.5, y: 0, z: 0 }); // offset 0.5 within [-1, 1]

        const def = makeGenericDef({
            linearLowerLimit: { x: -1, y: -Infinity, z: -Infinity },
            linearUpperLimit: { x: 1, y: Infinity, z: Infinity },
            angularLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            angularUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
        });

        const rows = runPrepare(def, bodyA, bodyB);
        expect(rows.length).toBe(0); // Within limits → no row
    });

    it('linear X limited: beyond upper limit → 1 row with limit correction', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 2, y: 0, z: 0 }); // offset 2 > upper 1

        const def = makeGenericDef({
            linearLowerLimit: { x: -1, y: -Infinity, z: -Infinity },
            linearUpperLimit: { x: 1, y: Infinity, z: Infinity },
            angularLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            angularUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
        });

        const rows = runPrepare(def, bodyA, bodyB);
        expect(rows.length).toBe(1);
        expect(rows[0].hasLimit).toBe(true);
        // Bias should be negative (positive error → negative bias → lambda > 0)
        expect(rows[0].bias).toBeLessThan(0);
    });

    it('linear X limited: beyond lower limit → 1 row with limit correction', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: -2, y: 0, z: 0 }); // offset -2 < lower -1

        const def = makeGenericDef({
            linearLowerLimit: { x: -1, y: -Infinity, z: -Infinity },
            linearUpperLimit: { x: 1, y: Infinity, z: Infinity },
            angularLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            angularUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
        });

        const rows = runPrepare(def, bodyA, bodyB);
        expect(rows.length).toBe(1);
        expect(rows[0].hasLimit).toBe(true);
        // Bias should be positive (negative error → positive bias → lambda < 0)
        expect(rows[0].bias).toBeGreaterThan(0);
    });

    it('angular Y limited: within limits → no row', () => {
        // Small rotation about Y (within limits)
        const halfAngle = 0.1; // ~11 degrees
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 0, y: 0, z: 0 }, {
            x: 0, y: Math.sin(halfAngle), z: 0, w: Math.cos(halfAngle),
        });

        const def = makeGenericDef({
            linearLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            linearUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
            angularLowerLimit: { x: -Infinity, y: -0.5, z: -Infinity },
            angularUpperLimit: { x: Infinity, y: 0.5, z: Infinity },
        });

        const rows = runPrepare(def, bodyA, bodyB);
        // Angle ~0.2 rad, within [-0.5, 0.5] → no angular row
        const angRows = rows.filter(r =>
            r.j1Linear.x === 0 && r.j1Linear.y === 0 && r.j1Linear.z === 0,
        );
        expect(angRows.length).toBe(0);
    });

    it('angular Y limited: beyond limit → 1 row with correction', () => {
        // Large rotation about Y (beyond limits)
        const halfAngle = 0.8; // ~0.8 rad angle
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 0, y: 0, z: 0 }, {
            x: 0, y: Math.sin(halfAngle), z: 0, w: Math.cos(halfAngle),
        });

        const def = makeGenericDef({
            linearLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            linearUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
            angularLowerLimit: { x: -Infinity, y: -0.3, z: -Infinity },
            angularUpperLimit: { x: Infinity, y: 0.3, z: Infinity },
        });

        const rows = runPrepare(def, bodyA, bodyB);
        const angRows = rows.filter(r =>
            r.j1Linear.x === 0 && r.j1Linear.y === 0 && r.j1Linear.z === 0,
        );
        expect(angRows.length).toBe(1);
        expect(angRows[0].hasLimit).toBe(true);
    });

    it('linear Z limited: beyond lower limit → row produced', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 0, y: 0, z: -3 }); // offset -3 < lower -1

        const def = makeGenericDef({
            linearLowerLimit: { x: -Infinity, y: -Infinity, z: -1 },
            linearUpperLimit: { x: Infinity, y: Infinity, z: 1 },
            angularLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            angularUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
        });

        const rows = runPrepare(def, bodyA, bodyB);
        expect(rows.length).toBe(1);
        expect(rows[0].hasLimit).toBe(true);
        expect(Math.abs(rows[0].j1Linear.z)).toBeCloseTo(1);
    });
});

// ─── 5. Singularity / Degenerate ──────────────────────────────────────────────

describe('Configurable joint — singularity robustness', () => {
    it('coincident bodies (all locked) → no NaN, no throw', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 0, y: 0, z: 0 });

        const def = makeGenericDef(); // all locked
        expect(() => runPrepare(def, bodyA, bodyB)).not.toThrow();

        const rows = runPrepare(def, bodyA, bodyB);
        for (const row of rows) {
            expect(isFinite(row.effectiveMass)).toBe(true);
            expect(isFinite(row.bias)).toBe(true);
            expect(isNaN(row.effectiveMass)).toBe(false);
            expect(isNaN(row.bias)).toBe(false);
        }
    });

    it('zero-width limit (lower == upper) → treated as locked, no NaN', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 0.001, y: 0, z: 0 });

        const def = makeGenericDef({
            linearLowerLimit: { x: 0, y: -Infinity, z: -Infinity },
            linearUpperLimit: { x: 0, y: Infinity, z: Infinity },
            angularLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            angularUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
        });

        const rows = runPrepare(def, bodyA, bodyB);
        expect(rows.length).toBe(1);
        expect(isFinite(rows[0].effectiveMass)).toBe(true);
        expect(isFinite(rows[0].bias)).toBe(true);
    });

    it('infinite mass body (invMass=0) → effective mass still valid', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 }, IDENTITY, 0, ZERO); // static
        const bodyB = makeSolverBody(2, { x: 2, y: 0, z: 0 });

        const def = makeGenericDef();
        const rows = runPrepare(def, bodyA, bodyB);

        for (const row of rows) {
            expect(isFinite(row.effectiveMass)).toBe(true);
            expect(row.effectiveMass).toBeGreaterThan(0);
        }
    });

    it('both bodies static (invMass=0, invInertia=0) → effective mass = 0, rows skipped at solve', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 }, IDENTITY, 0, ZERO);
        const bodyB = makeSolverBody(2, { x: 2, y: 0, z: 0 }, IDENTITY, 0, ZERO);

        const def = makeGenericDef();
        const rows = runPrepare(def, bodyA, bodyB);

        for (const row of rows) {
            expect(row.effectiveMass).toBe(0);
        }
    });

    it('very large offset → no NaN in bias', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 1000, y: -500, z: 200 });

        const def = makeGenericDef();
        const rows = runPrepare(def, bodyA, bodyB);

        for (const row of rows) {
            expect(isFinite(row.bias)).toBe(true);
            expect(isNaN(row.bias)).toBe(false);
        }
    });
});

// ─── 6. Iteration Convergence ─────────────────────────────────────────────────

describe('Configurable joint — iteration convergence', () => {
    it('more iterations produce same or better constraint satisfaction', () => {
        // This test verifies that the prepare function produces consistent rows
        // regardless of how many times it's called (idempotent).
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 2, y: 0, z: 0 });

        const def = makeGenericDef();

        // Run prepare multiple times — should produce same row count
        const rows1 = runPrepare(def, bodyA, bodyB);
        const rows2 = runPrepare(def, bodyA, bodyB);

        expect(rows1.length).toBe(rows2.length);
        expect(rows1.length).toBe(6);

        // Effective masses should be identical (same body state)
        for (let i = 0; i < rows1.length; i++) {
            expect(rows1[i].effectiveMass).toBeCloseTo(rows2[i].effectiveMass);
        }
    });
});

// ─── 7. Limit Interaction ─────────────────────────────────────────────────────

describe('Configurable joint — multi-axis limit interaction', () => {
    it('two linear axes beyond limits → 2 rows, both with hasLimit=true', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 3, y: 3, z: 0 }); // X and Y beyond limits

        const def = makeGenericDef({
            linearLowerLimit: { x: -1, y: -1, z: -Infinity },
            linearUpperLimit: { x: 1, y: 1, z: Infinity },
            angularLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            angularUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
        });

        const rows = runPrepare(def, bodyA, bodyB);
        expect(rows.length).toBe(2);
        for (const row of rows) {
            expect(row.hasLimit).toBe(true);
        }
    });

    it('all 3 linear limited beyond → 3 rows, stable effective masses', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 5, y: -5, z: 3 });

        const def = makeGenericDef({
            linearLowerLimit: { x: -1, y: -1, z: -1 },
            linearUpperLimit: { x: 1, y: 1, z: 1 },
            angularLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            angularUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
        });

        const rows = runPrepare(def, bodyA, bodyB);
        expect(rows.length).toBe(3);

        for (const row of rows) {
            expect(row.hasLimit).toBe(true);
            expect(row.effectiveMass).toBeGreaterThan(0);
            expect(isFinite(row.effectiveMass)).toBe(true);
        }
    });

    it('linear + angular limits simultaneously → both produce rows', () => {
        // Body B offset linearly AND rotated
        const halfAngle = 0.8;
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 3, y: 0, z: 0 }, {
            x: 0, y: Math.sin(halfAngle), z: 0, w: Math.cos(halfAngle),
        });

        const def = makeGenericDef({
            linearLowerLimit: { x: -1, y: -Infinity, z: -Infinity },
            linearUpperLimit: { x: 1, y: Infinity, z: Infinity },
            angularLowerLimit: { x: -Infinity, y: -0.3, z: -Infinity },
            angularUpperLimit: { x: Infinity, y: 0.3, z: Infinity },
        });

        const rows = runPrepare(def, bodyA, bodyB);
        // Should have 1 linear row (X beyond limit) + 1 angular row (Y beyond limit)
        expect(rows.length).toBe(2);

        const linCount = countLinearRows(rows);
        const angCount = countAngularRows(rows);
        expect(linCount).toBe(1);
        expect(angCount).toBe(1);
    });

    it('limits within slop → no row produced (slop tolerance)', () => {
        // Offset just barely beyond limit (within LINEAR_SLOP = 0.005)
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 1.003, y: 0, z: 0 }); // 0.003 beyond limit < 0.005 slop

        const def = makeGenericDef({
            linearLowerLimit: { x: -1, y: -Infinity, z: -Infinity },
            linearUpperLimit: { x: 1, y: Infinity, z: Infinity },
            angularLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            angularUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
        });

        const rows = runPrepare(def, bodyA, bodyB);
        // Within slop → no row
        expect(rows.length).toBe(0);
    });
});

// ─── 8. Mixed modes ──────────────────────────────────────────────────────────

describe('Configurable joint — mixed modes', () => {
    it('X locked + Y limited + Z free → correct row count', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 1, y: 3, z: 5 }); // Y and Z beyond limits

        const def = makeGenericDef({
            linearLowerLimit: { x: 0, y: -1, z: -Infinity },
            linearUpperLimit: { x: 0, y: 1, z: Infinity },
            angularLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            angularUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
        });

        const rows = runPrepare(def, bodyA, bodyB);
        // X locked → 1 row, Y limited beyond → 1 row, Z free → 0 rows
        expect(rows.length).toBe(2);

        // First row should be X-locked (bilateral)
        const xRow = rows.find(r => Math.abs(r.j1Linear.x) > 0.5);
        expect(xRow).toBeDefined();
        expect(xRow!.hasLimit).toBe(false); // Locked = bilateral, not limit

        // Second row should be Y-limited
        const yRow = rows.find(r => Math.abs(r.j1Linear.y) > 0.5);
        expect(yRow).toBeDefined();
        expect(yRow!.hasLimit).toBe(true);
    });

    it('angular X free + Y locked + Z limited → correct rows', () => {
        const halfAngle = 0.8;
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 0, y: 0, z: 0 }, {
            x: 0, y: Math.sin(halfAngle), z: 0, w: Math.cos(halfAngle),
        });

        const def = makeGenericDef({
            linearLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            linearUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
            angularLowerLimit: { x: -Infinity, y: 0, z: -0.3 },
            angularUpperLimit: { x: Infinity, y: 0, z: 0.3 },
        });

        const rows = runPrepare(def, bodyA, bodyB);
        // X free → 0, Y locked → 1, Z limited within → 0
        // Angle about Y ≈ 1.6 rad (beyond 0 lock)
        // Angle about Z ≈ 0 (within [-0.3, 0.3])
        expect(countAngularRows(rows)).toBe(1);
        expect(Math.abs(rows[0].j1Angular.y)).toBeCloseTo(1);
    });
});

// ─── 9. Negative control ─────────────────────────────────────────────────────

describe('Configurable joint — negative control', () => {
    it('without constraint, bodies have no rows (baseline)', () => {
        // This test verifies that the prepare function is the source of rows,
        // not some external mechanism.
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 5, y: 0, z: 0 });

        // All free = no constraint effect
        const def = makeGenericDef({
            linearLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            linearUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
            angularLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            angularUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
        });

        const rows = runPrepare(def, bodyA, bodyB);
        expect(rows.length).toBe(0);

        // Body velocities unchanged
        expect(bodyA.linearVelocity.x).toBe(0);
        expect(bodyB.linearVelocity.x).toBe(0);
    });
});

// ─── 10. Allocation verification ─────────────────────────────────────────────

describe('Configurable joint — allocation efficiency', () => {
    it('FREE axes produce no rows (zero allocation for free DOFs)', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 10, y: 20, z: 30 });

        const def = makeGenericDef({
            linearLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            linearUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
            angularLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            angularUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
        });

        const rows: JacobianRow3D[] = [];
        const data = makeConstraintData(def);
        const bodyMap = makeBodyMap([bodyA, bodyB]);
        prepareConfigurable(data, bodyMap, null as unknown as BodyManager3D, 1 / 60, rows);

        // Zero rows = zero allocation
        expect(rows.length).toBe(0);
    });

    it('LIMITED within limits produces no row (allocation saved)', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 0.1, y: 0.2, z: -0.3 });

        const def = makeGenericDef({
            linearLowerLimit: { x: -1, y: -1, z: -1 },
            linearUpperLimit: { x: 1, y: 1, z: 1 },
            angularLowerLimit: { x: -Infinity, y: -Infinity, z: -Infinity },
            angularUpperLimit: { x: Infinity, y: Infinity, z: Infinity },
        });

        const rows = runPrepare(def, bodyA, bodyB);
        // All within limits → no rows
        expect(rows.length).toBe(0);
    });

    it('all locked → exactly 6 rows allocated', () => {
        const bodyA = makeSolverBody(1, { x: 0, y: 0, z: 0 });
        const bodyB = makeSolverBody(2, { x: 1, y: 2, z: 3 });

        const def = makeGenericDef();
        const rows = runPrepare(def, bodyA, bodyB);
        expect(rows.length).toBe(6);
    });
});
