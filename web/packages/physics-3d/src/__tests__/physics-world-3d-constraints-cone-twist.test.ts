/**
 * Cone-twist (Character) joint solver tests.
 *
 * These tests call prepareConeTwist() / solveVelocityRow() DIRECTLY — NOT
 * through the runtime — because the runtime side-effect import is owned by
 * the integration task. The approach mirrors the configurable/sliders joint
 * test files: pre-populated solver-body maps, a null BodyManager3D stub,
 * manual velocity solve sweeps and manual integration.
 *
 * Scenario map (numbers refer to the task brief):
 *   1. Free swing inside the cone           -> no swing row, motion untouched
 *   2. Cone rim resistance                  -> swing never exceeds coneLimit
 *   3. Free twist inside the span           -> no twist row, motion untouched
 *   4. Twist limits (upper/lower/pull-back) -> unilateral resistance
 *   5. Swing/twist independence             -> rim of one leaves the other free
 *   6. Twist motor stalls at the limit      -> Box2D semantics, proven
 *   7. Anchor lock                          -> 3 linear rows hold anchors
 *   8. Negative control                     -> unconstrained body blows past
 *   9. Cone apex singularity                -> no NaN, no throw, stable
 *  10. Degenerate configurations            -> cone 0, cone π, zero axis
 *  11. Iteration convergence                -> more sweeps, tighter error
 *
 * Swing/twist angles are measured with a test-side implementation that uses
 * library quaternion operations, so the solver's own hand-expanded math is
 * never trusted for its own assertions.
 */

import { describe, expect, it } from 'vitest';
import { Quat, type IQuatLike, type IVec3Like } from '@axrone/numeric';
import type {
    BodyId3D,
    ConstraintId3D,
    IConeTwistConstraintDef3D,
} from '../types/physics-3d';
import {
    type JacobianRow3D,
    type SolverBody3D,
    type ConstraintData3D,
    solveVelocityRow,
    getPrepareFunction,
} from '../core/physics-world-3d-constraints-framework';
import { CONSTRAINT_TYPE_CONE_TWIST, transformPoint3D } from '../core/physics-world-3d-shared';
import type { BodyManager3D } from '../core/physics-managers-3d';
import {
    prepareConeTwist,
    decomposeSwingTwist,
} from '../core/physics-world-3d-constraints-cone-twist';

// ─── Constants ────────────────────────────────────────────────────────────────

const DEG = Math.PI / 180;
const CONE_30 = 30 * DEG;
const TWIST_SPAN_60 = 60 * DEG;

// ─── Body / def helpers ───────────────────────────────────────────────────────

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

function makeStaticBody(id: number, pos: IVec3Like, rot: IQuatLike = IDENTITY): SolverBody3D {
    return makeSolverBody(id, pos, rot, 0, { x: 0, y: 0, z: 0 });
}

type MotorOverrides = { motorSpeed?: number; maxMotorTorque?: number };

function makeConeDef(
    overrides: Partial<IConeTwistConstraintDef3D> & MotorOverrides = {},
): IConeTwistConstraintDef3D {
    return {
        bodyIdA: 1,
        bodyIdB: 2,
        localFrameA: { position: { ...ZERO }, rotation: { ...IDENTITY } },
        localFrameB: { position: { ...ZERO }, rotation: { ...IDENTITY } },
        ...overrides,
    };
}

function makeConstraintData(def: IConeTwistConstraintDef3D): ConstraintData3D {
    return {
        constraintId: 100 as ConstraintId3D,
        type: CONSTRAINT_TYPE_CONE_TWIST,
        bodyIdA: def.bodyIdA,
        bodyIdB: def.bodyIdB,
        def: { kind: CONSTRAINT_TYPE_CONE_TWIST, ...def } as ConstraintData3D['def'],
    };
}

function makeBodyMap(bodies: SolverBody3D[]): Map<BodyId3D, SolverBody3D> {
    const map = new Map<BodyId3D, SolverBody3D>();
    for (const b of bodies) map.set(b.bodyId, b);
    return map;
}

const STUB_MANAGER = null as unknown as BodyManager3D;

/** Run one prepare and return the freshly built rows. */
function runPrepare(
    def: IConeTwistConstraintDef3D,
    bodyA: SolverBody3D,
    bodyB: SolverBody3D,
    dt: number = 1 / 60,
): JacobianRow3D[] {
    const rows: JacobianRow3D[] = [];
    prepareConeTwist(makeConstraintData(def), makeBodyMap([bodyA, bodyB]), STUB_MANAGER, dt, rows);
    return rows;
}

// ─── Solve / integrate helpers ────────────────────────────────────────────────

function solveRows(
    rows: JacobianRow3D[],
    bodyA: SolverBody3D,
    bodyB: SolverBody3D,
    iterations: number,
): void {
    for (let i = 0; i < iterations; i++) {
        for (const row of rows) solveVelocityRow(row, bodyA, bodyB);
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
        const n = Math.sqrt(
            b.rotation.x * b.rotation.x +
                b.rotation.y * b.rotation.y +
                b.rotation.z * b.rotation.z +
                b.rotation.w * b.rotation.w,
        );
        b.rotation.x /= n;
        b.rotation.y /= n;
        b.rotation.z /= n;
        b.rotation.w /= n;
    }
}

// ─── Independent measurement helpers ──────────────────────────────────────────

/**
 * Test-side swing-twist measurement. Same standard decomposition formula as
 * the solver, but implemented with library quaternion operations instead of
 * the solver's hand-expanded products, so solver bugs cannot hide behind the
 * measurement.
 */
function measureSwingTwist(qRel: IQuatLike): { swing: number; twist: number } {
    const s = qRel.w < 0 ? -1 : 1;
    const x = qRel.x * s;
    const y = qRel.y * s;
    const z = qRel.z * s;
    const w = qRel.w * s;
    const p = x;
    const norm2 = p * p + w * w;
    let twist = 0;
    let sx = x;
    let sy = y;
    let sz = z;
    let sw = w;
    if (norm2 > 1e-12) {
        const inv = 1 / Math.sqrt(norm2);
        const twX = p * inv;
        const twW = w * inv;
        twist = 2 * Math.atan2(twX, twW);
        const swingQ = Quat.multiply({ x, y, z, w } as IQuatLike, {
            x: -twX,
            y: 0,
            z: 0,
            w: twW,
        } as IQuatLike);
        sx = swingQ.x;
        sy = swingQ.y;
        sz = swingQ.z;
        sw = swingQ.w;
    }
    const len = Math.sqrt(sx * sx + sy * sy + sz * sz);
    return { swing: 2 * Math.atan2(len, sw), twist };
}

function measureJoint(
    def: IConeTwistConstraintDef3D,
    bodyA: SolverBody3D,
    bodyB: SolverBody3D,
): { swing: number; twist: number } {
    const fA = Quat.multiply(bodyA.rotation, def.localFrameA.rotation);
    const fB = Quat.multiply(bodyB.rotation, def.localFrameB.rotation);
    const qRel = Quat.multiply(Quat.conjugate(fA), fB);
    return measureSwingTwist(qRel);
}

function anchorGap(
    def: IConeTwistConstraintDef3D,
    bodyA: SolverBody3D,
    bodyB: SolverBody3D,
): number {
    const a = transformPoint3D(def.localFrameA.position, bodyA.position, bodyA.rotation);
    const b = transformPoint3D(def.localFrameB.position, bodyB.position, bodyB.rotation);
    const dx = b.x - a.x;
    const dy = b.y - a.y;
    const dz = b.z - a.z;
    return Math.sqrt(dx * dx + dy * dy + dz * dz);
}

function countAngularRows(rows: JacobianRow3D[]): number {
    return rows.filter(
        r =>
            r.j1Linear.x === 0 &&
            r.j1Linear.y === 0 &&
            r.j1Linear.z === 0 &&
            (r.j1Angular.x !== 0 || r.j1Angular.y !== 0 || r.j1Angular.z !== 0),
    ).length;
}

function rowsFinite(rows: JacobianRow3D[]): boolean {
    for (const r of rows) {
        const vecs = [r.j1Linear, r.j1Angular, r.j2Linear, r.j2Angular];
        for (const v of vecs) {
            if (!Number.isFinite(v.x) || !Number.isFinite(v.y) || !Number.isFinite(v.z)) {
                return false;
            }
        }
        // lowerLimit/upperLimit legitimately use ±Infinity for unilateral rows.
        if (
            !Number.isFinite(r.bias) ||
            !Number.isFinite(r.impulse) ||
            !Number.isFinite(r.effectiveMass) ||
            !Number.isFinite(r.positionError) ||
            !Number.isFinite(r.softness)
        ) {
            return false;
        }
    }
    return true;
}

function bodiesFinite(...bodies: SolverBody3D[]): boolean {
    for (const b of bodies) {
        const scalars = [
            b.position.x, b.position.y, b.position.z,
            b.rotation.x, b.rotation.y, b.rotation.z, b.rotation.w,
            b.linearVelocity.x, b.linearVelocity.y, b.linearVelocity.z,
            b.angularVelocity.x, b.angularVelocity.y, b.angularVelocity.z,
        ];
        for (const v of scalars) {
            if (!Number.isFinite(v)) return false;
        }
    }
    return true;
}

// ─── Simulation harness ───────────────────────────────────────────────────────

interface SimOptions {
    dt?: number;
    steps?: number;
    velocityIterations?: number;
    /** Constant world torque applied to body B every step. */
    torqueB?: IVec3Like;
    /** When false, no prepare/solve happens at all (negative control). */
    constrained?: boolean;
}

interface SimResult {
    bodyA: SolverBody3D;
    bodyB: SolverBody3D;
    /** Rows of a fresh prepare at the final state (introspection only). */
    finalRows: JacobianRow3D[];
    maxSwing: number;
    maxTwist: number;
    minTwist: number;
    finalSwing: number;
    finalTwist: number;
    maxAnchorGap: number;
    finalAnchorGap: number;
    maxAngularRows: number;
    allFinite: boolean;
}

function simulate(
    def: IConeTwistConstraintDef3D,
    bodyA: SolverBody3D,
    bodyB: SolverBody3D,
    options: SimOptions = {},
): SimResult {
    const dt = options.dt ?? 1 / 60;
    const steps = options.steps ?? 60;
    const iterations = options.velocityIterations ?? 8;
    const constrained = options.constrained !== false;
    const data = makeConstraintData(def);
    const map = makeBodyMap([bodyA, bodyB]);

    const result: SimResult = {
        bodyA,
        bodyB,
        finalRows: [],
        maxSwing: 0,
        maxTwist: 0,
        minTwist: 0,
        finalSwing: 0,
        finalTwist: 0,
        maxAnchorGap: 0,
        finalAnchorGap: 0,
        maxAngularRows: 0,
        allFinite: true,
    };

    for (let step = 0; step < steps; step++) {
        if (options.torqueB) {
            bodyB.angularVelocity.x += bodyB.invInertia.x * options.torqueB.x * dt;
            bodyB.angularVelocity.y += bodyB.invInertia.y * options.torqueB.y * dt;
            bodyB.angularVelocity.z += bodyB.invInertia.z * options.torqueB.z * dt;
        }
        if (constrained) {
            const rows: JacobianRow3D[] = [];
            prepareConeTwist(data, map, STUB_MANAGER, dt, rows);
            solveRows(rows, bodyA, bodyB, iterations);
            if (!rowsFinite(rows)) result.allFinite = false;
            result.maxAngularRows = Math.max(result.maxAngularRows, countAngularRows(rows));
        }
        integrateBody(bodyA, dt);
        integrateBody(bodyB, dt);

        const m = measureJoint(def, bodyA, bodyB);
        if (!Number.isFinite(m.swing) || !Number.isFinite(m.twist)) result.allFinite = false;
        if (step === 0) {
            result.maxTwist = m.twist;
            result.minTwist = m.twist;
        }
        result.maxSwing = Math.max(result.maxSwing, m.swing);
        result.maxTwist = Math.max(result.maxTwist, m.twist);
        result.minTwist = Math.min(result.minTwist, m.twist);
        result.finalSwing = m.swing;
        result.finalTwist = m.twist;

        const gap = anchorGap(def, bodyA, bodyB);
        result.maxAnchorGap = Math.max(result.maxAnchorGap, gap);
        result.finalAnchorGap = gap;
        if (!bodiesFinite(bodyA, bodyB)) result.allFinite = false;
    }

    if (constrained) {
        result.finalRows = runPrepare(def, bodyA, bodyB, dt);
    }
    return result;
}

// ─── Swing-twist decomposition (unit) ─────────────────────────────────────────

describe('ConeTwist joint — swing-twist decomposition (unit)', () => {
    it('decomposes the identity into zero swing and zero twist with no usable direction', () => {
        const out = {
            swingAngle: NaN,
            twistAngle: NaN,
            swingDirFrame: { x: 7, y: 7, z: 7 },
            hasSwingDirection: true,
        };
        decomposeSwingTwist(IDENTITY, out);
        expect(out.swingAngle).toBeCloseTo(0, 12);
        expect(out.twistAngle).toBeCloseTo(0, 12);
        expect(out.hasSwingDirection).toBe(false);
        expect(Number.isFinite(out.swingAngle)).toBe(true);
        expect(Number.isFinite(out.twistAngle)).toBe(true);
    });

    it('decomposes pure twist about the twist axis with no swing', () => {
        const half = 15 * DEG;
        const qTwist: IQuatLike = { x: Math.sin(half), y: 0, z: 0, w: Math.cos(half) };
        const out = {
            swingAngle: 0,
            twistAngle: 0,
            swingDirFrame: { x: 0, y: 0, z: 0 },
            hasSwingDirection: false,
        };
        decomposeSwingTwist(qTwist, out);
        expect(out.twistAngle).toBeCloseTo(30 * DEG, 9);
        expect(out.swingAngle).toBeCloseTo(0, 9);
    });

    it('decomposes pure swing about a perpendicular axis with zero twist', () => {
        const half = 10 * DEG;
        const qSwing: IQuatLike = { x: 0, y: Math.sin(half), z: 0, w: Math.cos(half) };
        const out = {
            swingAngle: 0,
            twistAngle: 0,
            swingDirFrame: { x: 0, y: 0, z: 0 },
            hasSwingDirection: false,
        };
        decomposeSwingTwist(qSwing, out);
        expect(out.swingAngle).toBeCloseTo(20 * DEG, 9);
        expect(out.twistAngle).toBeCloseTo(0, 9);
        expect(out.hasSwingDirection).toBe(true);
        expect(out.swingDirFrame.y).toBeCloseTo(1, 9);
    });

    it('separates a combined swing-then-twist rotation into its two parts', () => {
        // qRel = Ry(20°) * Rx(30°)  (swing applied about Y, twist about X).
        const hs = 10 * DEG;
        const ht = 15 * DEG;
        const qRel = Quat.multiply(
            { x: 0, y: Math.sin(hs), z: 0, w: Math.cos(hs) } as IQuatLike,
            { x: Math.sin(ht), y: 0, z: 0, w: Math.cos(ht) } as IQuatLike,
        );
        const out = {
            swingAngle: 0,
            twistAngle: 0,
            swingDirFrame: { x: 0, y: 0, z: 0 },
            hasSwingDirection: false,
        };
        decomposeSwingTwist(qRel, out);
        expect(out.twistAngle).toBeCloseTo(30 * DEG, 9);
        expect(out.swingAngle).toBeCloseTo(20 * DEG, 9);
    });

    it('survives the 180-degree swing edge case without NaN', () => {
        const out = {
            swingAngle: 0,
            twistAngle: 0,
            swingDirFrame: { x: 0, y: 0, z: 0 },
            hasSwingDirection: false,
        };
        decomposeSwingTwist({ x: 0, y: 1, z: 0, w: 0 }, out);
        expect(out.swingAngle).toBeCloseTo(Math.PI, 9);
        expect(out.twistAngle).toBe(0);
        expect(Number.isFinite(out.swingAngle)).toBe(true);
    });

    it('canonicalizes the quaternion sign so -q measures the same as q', () => {
        const half = 10 * DEG;
        const q: IQuatLike = { x: 0, y: Math.sin(half), z: 0, w: Math.cos(half) };
        const neg: IQuatLike = { x: -q.x, y: -q.y, z: -q.z, w: -q.w };
        const outA = {
            swingAngle: 0,
            twistAngle: 0,
            swingDirFrame: { x: 0, y: 0, z: 0 },
            hasSwingDirection: false,
        };
        const outB = {
            swingAngle: 0,
            twistAngle: 0,
            swingDirFrame: { x: 0, y: 0, z: 0 },
            hasSwingDirection: false,
        };
        decomposeSwingTwist(q, outA);
        decomposeSwingTwist(neg, outB);
        expect(outB.swingAngle).toBeCloseTo(outA.swingAngle, 12);
        expect(outB.twistAngle).toBeCloseTo(outA.twistAngle, 12);
        expect(outA.swingAngle).toBeCloseTo(20 * DEG, 9);
    });
});

// ─── Registration ─────────────────────────────────────────────────────────────

describe('ConeTwist joint — module registration', () => {
    it('registers its prepare function under CONSTRAINT_TYPE_CONE_TWIST', () => {
        expect(getPrepareFunction(CONSTRAINT_TYPE_CONE_TWIST)).toBe(prepareConeTwist);
    });
});

// ─── 1. Free swing inside the cone ────────────────────────────────────────────

describe('Scenario 1 — free swing inside the cone', () => {
    it('generates no swing row and leaves swing motion untouched inside the cone', () => {
        const def = makeConeDef({ swingSpan1: CONE_30, swingSpan2: CONE_30 });
        const bodyA = makeStaticBody(1, { ...ZERO });
        const bodyB = makeSolverBody(2, { ...ZERO }, { x: 0, y: Math.sin(7.5 * DEG), z: 0, w: Math.cos(7.5 * DEG) });
        bodyB.angularVelocity = { x: 0, y: 1.5, z: 0 };

        const rows = runPrepare(def, bodyA, bodyB);
        expect(rows.length).toBe(3); // linear rows only — no swing row inside
        expect(countAngularRows(rows)).toBe(0);

        solveRows(rows, bodyA, bodyB, 8);
        expect(bodyB.angularVelocity.y).toBeCloseTo(1.5, 9); // untouched
        for (const row of rows) expect(row.impulse).toBe(0);

        const result = simulate(def, bodyA, bodyB, { steps: 5 });
        expect(result.finalSwing).toBeGreaterThan(20 * DEG); // motion continued
        expect(result.finalSwing).toBeLessThan(24 * DEG);    // ≈ 15° + 5 × 1.43°
        expect(result.maxAngularRows).toBe(0);               // never resisted
        expect(result.allFinite).toBe(true);
    });
});

// ─── 2. Cone rim resistance ───────────────────────────────────────────────────

describe('Scenario 2 — cone rim resistance', () => {
    it('stops the body at the cone rim without exceeding coneLimit', () => {
        const def = makeConeDef({ swingSpan1: CONE_30, swingSpan2: CONE_30 });
        const bodyA = makeStaticBody(1, { ...ZERO });
        const bodyB = makeSolverBody(2, { ...ZERO }, { x: 0, y: Math.sin(12.5 * DEG), z: 0, w: Math.cos(12.5 * DEG) });
        bodyB.angularVelocity = { x: 0, y: 1.5, z: 0 };

        const result = simulate(def, bodyA, bodyB, { steps: 60 });

        // Rim reached (non-vacuous) but never exceeded beyond numerical slop.
        expect(result.maxSwing).toBeGreaterThan(CONE_30 - 0.2);
        expect(result.maxSwing).toBeLessThanOrEqual(CONE_30 + 0.02);
        expect(result.finalSwing).toBeLessThanOrEqual(CONE_30 + 0.02);
        expect(result.allFinite).toBe(true);
    });
});

// ─── 3. Free twist inside the span ────────────────────────────────────────────

describe('Scenario 3 — free twist inside the span', () => {
    it('generates no twist row and leaves twist motion untouched inside the span', () => {
        const def = makeConeDef({ swingSpan1: CONE_30, swingSpan2: CONE_30, twistSpan: TWIST_SPAN_60 });
        const bodyA = makeStaticBody(1, { ...ZERO });
        const bodyB = makeSolverBody(2, { ...ZERO }, { x: Math.sin(5 * DEG), y: 0, z: 0, w: Math.cos(5 * DEG) });
        bodyB.angularVelocity = { x: 2.5, y: 0, z: 0 };

        const rows = runPrepare(def, bodyA, bodyB);
        expect(rows.length).toBe(3);
        expect(countAngularRows(rows)).toBe(0); // twist 10° is well inside ±28°

        solveRows(rows, bodyA, bodyB, 8);
        expect(bodyB.angularVelocity.x).toBeCloseTo(2.5, 9); // untouched
        for (const row of rows) expect(row.impulse).toBe(0);

        const result = simulate(def, bodyA, bodyB, { steps: 5 });
        expect(result.finalTwist).toBeGreaterThan(19 * DEG);  // ≈ 10° + 5 × 2.38°
        expect(result.finalTwist).toBeLessThan(25 * DEG);
        expect(result.maxAngularRows).toBe(0);
        expect(result.bodyB.angularVelocity.x).toBeCloseTo(2.5, 6);
        expect(result.allFinite).toBe(true);
    });
});

// ─── 4. Twist limits ──────────────────────────────────────────────────────────

describe('Scenario 4 — twist limits', () => {
    const def = makeConeDef({ swingSpan1: CONE_30, swingSpan2: CONE_30, twistSpan: TWIST_SPAN_60 });

    it('stops outward twist at the upper rim without exceeding it', () => {
        const bodyA = makeStaticBody(1, { ...ZERO });
        const bodyB = makeSolverBody(2, { ...ZERO }, { x: Math.sin(10 * DEG), y: 0, z: 0, w: Math.cos(10 * DEG) });
        bodyB.angularVelocity = { x: 1.5, y: 0, z: 0 };

        const result = simulate(def, bodyA, bodyB, { steps: 60 });
        const upper = TWIST_SPAN_60 / 2;

        expect(result.maxTwist).toBeGreaterThan(upper - 0.15);      // rim reached
        expect(result.maxTwist).toBeLessThanOrEqual(upper + 0.02);  // never exceeded
        expect(result.finalTwist).toBeLessThanOrEqual(upper + 0.02);
        expect(result.allFinite).toBe(true);
    });

    it('pulls a twist violation beyond the rim back toward the limit', () => {
        const bodyA = makeStaticBody(1, { ...ZERO });
        const start = 40 * DEG; // 10° beyond the +30° rim
        const bodyB = makeSolverBody(2, { ...ZERO }, { x: Math.sin(start / 2), y: 0, z: 0, w: Math.cos(start / 2) });

        const result = simulate(def, bodyA, bodyB, { steps: 60 });
        const upper = TWIST_SPAN_60 / 2;

        expect(result.maxTwist).toBeLessThanOrEqual(start + 0.01); // never grows
        expect(result.finalTwist).toBeLessThan(start - 5 * DEG);   // pulled back
        expect(result.finalTwist).toBeGreaterThan(upper - 0.15);   // settles at the rim
        expect(result.finalTwist).toBeLessThanOrEqual(upper + 0.05);
        expect(result.allFinite).toBe(true);
    });

    it('stops outward twist at the lower rim without exceeding it', () => {
        const bodyA = makeStaticBody(1, { ...ZERO });
        const bodyB = makeSolverBody(2, { ...ZERO }, { x: Math.sin(-10 * DEG), y: 0, z: 0, w: Math.cos(-10 * DEG) });
        bodyB.angularVelocity = { x: -1.5, y: 0, z: 0 };

        const result = simulate(def, bodyA, bodyB, { steps: 60 });
        const lower = -TWIST_SPAN_60 / 2;

        expect(result.minTwist).toBeLessThan(lower + 0.15);       // rim reached
        expect(result.minTwist).toBeGreaterThanOrEqual(lower - 0.02); // never exceeded
        expect(result.finalTwist).toBeGreaterThanOrEqual(lower - 0.02);
        expect(result.allFinite).toBe(true);
    });
});

// ─── 5. Swing / twist independence ────────────────────────────────────────────

describe('Scenario 5 — swing and twist independence', () => {
    it('keeps twist free while the swing row is actively resisting at the rim', () => {
        const def = makeConeDef({ swingSpan1: CONE_30, swingSpan2: CONE_30, twistSpan: TWIST_SPAN_60 });
        // qRel = Ry(29°) * Rx(10°): swing 29° (inside the 2° rim margin → row
        // active), twist 10° (well inside ±30°).
        const hs = 14.5 * DEG;
        const ht = 5 * DEG;
        const qB = Quat.multiply(
            { x: 0, y: Math.sin(hs), z: 0, w: Math.cos(hs) } as IQuatLike,
            { x: Math.sin(ht), y: 0, z: 0, w: Math.cos(ht) } as IQuatLike,
        );
        const bodyA = makeStaticBody(1, { ...ZERO });
        const bodyB = makeSolverBody(2, { ...ZERO }, qB);
        bodyB.angularVelocity = { x: 2, y: 2, z: 0 };

        const rows = runPrepare(def, bodyA, bodyB);
        expect(countAngularRows(rows)).toBe(1); // exactly the swing row

        solveRows(rows, bodyA, bodyB, 8);
        expect(bodyB.angularVelocity.y).toBeCloseTo(0, 6);  // swing resisted
        expect(bodyB.angularVelocity.x).toBeCloseTo(2, 6);  // twist still free
    });

    it('keeps swing free while the twist row is actively resisting at the rim', () => {
        const def = makeConeDef({ swingSpan1: CONE_30, swingSpan2: CONE_30, twistSpan: TWIST_SPAN_60 });
        // qRel = Ry(10°) * Rx(29°): twist 29° (rim margin → twist row active),
        // swing 10° (well inside the 30° cone and below its margin).
        const hs = 5 * DEG;
        const ht = 14.5 * DEG;
        const qB = Quat.multiply(
            { x: 0, y: Math.sin(hs), z: 0, w: Math.cos(hs) } as IQuatLike,
            { x: Math.sin(ht), y: 0, z: 0, w: Math.cos(ht) } as IQuatLike,
        );
        const bodyA = makeStaticBody(1, { ...ZERO });
        const bodyB = makeSolverBody(2, { ...ZERO }, qB);
        bodyB.angularVelocity = { x: 2, y: 2, z: 0 };

        const rows = runPrepare(def, bodyA, bodyB);
        expect(countAngularRows(rows)).toBe(1); // exactly the twist limit row

        solveRows(rows, bodyA, bodyB, 8);
        expect(bodyB.angularVelocity.x).toBeCloseTo(0, 6);  // twist resisted
        expect(bodyB.angularVelocity.y).toBeCloseTo(2, 6);  // swing still free
    });
});

// ─── 6. Twist motor stalls at the limit ───────────────────────────────────────

describe('Scenario 6 — twist motor stalls at the twist limit', () => {
    const makeMotorDef = (withLimit: boolean): IConeTwistConstraintDef3D =>
        makeConeDef({
            swingSpan1: CONE_30,
            swingSpan2: CONE_30,
            twistSpan: withLimit ? TWIST_SPAN_60 : undefined,
            motorSpeed: 6,
            maxMotorTorque: 25,
        } as Partial<IConeTwistConstraintDef3D> & MotorOverrides);

    it('cuts the twist motor at the rim instead of pushing through it', () => {
        const bodyA = makeStaticBody(1, { ...ZERO });
        const bodyB = makeSolverBody(2, { ...ZERO });
        const upper = TWIST_SPAN_60 / 2;

        const result = simulate(makeMotorDef(true), bodyA, bodyB, { steps: 120 });

        // Motor drove the twist to the rim (non-vacuous)…
        expect(result.finalTwist).toBeGreaterThan(upper - 0.25);
        // …but never through it, despite the motor still being active…
        expect(result.maxTwist).toBeLessThanOrEqual(upper + 0.05);
        expect(result.finalTwist).toBeLessThanOrEqual(upper + 0.05);
        // …and the twist rate is stalled far below the 6 rad/s motor target.
        expect(Math.abs(result.bodyB.angularVelocity.x)).toBeLessThan(1.5);

        // Final prepare still contains BOTH the motor row and the limit row.
        const motorRows = result.finalRows.filter(r => r.hasMotor);
        const limitRows = result.finalRows.filter(r => r.hasLimit);
        expect(motorRows.length).toBe(1);
        expect(limitRows.length).toBeGreaterThanOrEqual(1);

        // One more sweep: the motor injects impulse, the limit row strips it —
        // the twist rate stays pinned at zero (Box2D motor-stall semantics).
        solveRows(result.finalRows, bodyA, bodyB, 1);
        expect(Math.abs(bodyB.angularVelocity.x)).toBeLessThan(0.5);
        expect(result.allFinite).toBe(true);
    });

    it('would spin far past the limit without the twist span (control)', () => {
        const bodyA = makeStaticBody(1, { ...ZERO });
        const bodyB = makeSolverBody(2, { ...ZERO });

        // 24 steps ≈ 96° of accumulated twist at the 25 rad/s² motor ramp —
        // far past the rim but short of the ±180° measurement wrap.
        const result = simulate(makeMotorDef(false), bodyA, bodyB, { steps: 24 });
        const upper = TWIST_SPAN_60 / 2;

        // Same motor, no limit: the twist blows far past where the rim would be
        // (no wrap: still inside the principal branch).
        expect(result.finalTwist).toBeGreaterThan(upper + 0.5);
        expect(result.finalTwist).toBeLessThan(Math.PI - 0.2);
        expect(Math.abs(result.bodyB.angularVelocity.x)).toBeGreaterThan(2);
    });
});

// ─── 7. Anchor lock (linear rows) ─────────────────────────────────────────────

describe('Scenario 7 — anchor lock through the three linear rows', () => {
    it('drives separated anchors together and keeps them together', () => {
        const def = makeConeDef({ swingSpan1: CONE_30, swingSpan2: CONE_30, twistSpan: TWIST_SPAN_60 });
        const bodyA = makeStaticBody(1, { ...ZERO });
        const bodyB = makeSolverBody(2, { x: 0.08, y: 0.03, z: 0.05 });
        bodyB.linearVelocity = { x: 0.5, y: -0.2, z: 0.3 };

        const initialGap = anchorGap(def, bodyA, bodyB);
        expect(initialGap).toBeGreaterThan(0.09);

        const result = simulate(def, bodyA, bodyB, { steps: 60 });
        expect(result.finalAnchorGap).toBeLessThan(0.01);
        expect(result.maxAnchorGap).toBeLessThan(initialGap + 0.02);
        expect(result.allFinite).toBe(true);
    });
});

// ─── 8. Negative control ──────────────────────────────────────────────────────

describe('Scenario 8 — negative control without the constraint', () => {
    it('spins freely past the cone unconstrained and is held with the constraint', () => {
        const def = makeConeDef({ swingSpan1: CONE_30, swingSpan2: CONE_30 });

        const freeA = makeStaticBody(1, { ...ZERO });
        const freeB = makeSolverBody(2, { ...ZERO }, { x: 0, y: Math.sin(5 * DEG), z: 0, w: Math.cos(5 * DEG) });
        freeB.angularVelocity = { x: 0, y: 1.5, z: 0 };
        const unconstrained = simulate(def, freeA, freeB, { steps: 60, constrained: false });

        const heldA = makeStaticBody(1, { ...ZERO });
        const heldB = makeSolverBody(2, { ...ZERO }, { x: 0, y: Math.sin(5 * DEG), z: 0, w: Math.cos(5 * DEG) });
        heldB.angularVelocity = { x: 0, y: 1.5, z: 0 };
        const constrained = simulate(def, heldA, heldB, { steps: 60 });

        // Unconstrained: blows far past the cone (proof the sim actually moves).
        expect(unconstrained.finalSwing).toBeGreaterThan(CONE_30 + 0.5);
        // Constrained: pinned at the rim.
        expect(constrained.finalSwing).toBeLessThanOrEqual(CONE_30 + 0.02);
        // The constraint made a decisive difference.
        expect(unconstrained.finalSwing).toBeGreaterThan(constrained.finalSwing + 0.4);
    });
});

// ─── 9. Cone apex singularity ─────────────────────────────────────────────────

describe('Scenario 9 — cone apex singularity', () => {
    const def = makeConeDef({ swingSpan1: CONE_30, swingSpan2: CONE_30, twistSpan: TWIST_SPAN_60 });

    it('skips the swing row at the exact apex and stays stable under torque', () => {
        const bodyA = makeStaticBody(1, { ...ZERO });
        const bodyB = makeSolverBody(2, { ...ZERO }); // qRel = identity → apex

        const rows = runPrepare(def, bodyA, bodyB);
        expect(rows.length).toBe(3); // swing row skipped at the apex
        expect(countAngularRows(rows)).toBe(0);
        expect(rowsFinite(rows)).toBe(true);

        // Drive the joint from the apex toward and past the rim: the row must
        // re-engage cleanly with no NaN anywhere in the trajectory.
        const result = simulate(def, bodyA, bodyB, { steps: 60, torqueB: { x: 0, y: 6, z: 3 } });
        expect(result.allFinite).toBe(true);
        expect(result.maxSwing).toBeLessThanOrEqual(CONE_30 + 0.06); // caught at the rim
        expect(bodiesFinite(result.bodyA, result.bodyB)).toBe(true);
    });

    it('decomposeSwingTwist stays finite for near-apex rotations', () => {
        const makeOut = () => ({
            swingAngle: 0,
            twistAngle: 0,
            swingDirFrame: { x: 0, y: 0, z: 0 },
            hasSwingDirection: false,
        });

        const tiny: IQuatLike = { x: 0, y: Math.sin(5e-9), z: 0, w: Math.cos(5e-9) };
        const outTiny = makeOut();
        decomposeSwingTwist(tiny, outTiny);
        expect(Number.isFinite(outTiny.swingAngle)).toBe(true);
        expect(Number.isFinite(outTiny.twistAngle)).toBe(true);
        expect(outTiny.swingAngle).toBeLessThan(1e-6);
        expect(outTiny.hasSwingDirection).toBe(false); // direction unusable → row skipped
    });
});

// ─── 10. Degenerate configurations ────────────────────────────────────────────

describe('Scenario 10 — degenerate configurations', () => {
    it('cone limit 0 fully locks the swing without NaN', () => {
        const def = makeConeDef({ swingSpan1: 0, swingSpan2: 0 });
        const bodyA = makeStaticBody(1, { ...ZERO });
        const bodyB = makeSolverBody(2, { ...ZERO }, { x: 0, y: Math.sin(5 * DEG), z: 0, w: Math.cos(5 * DEG) });
        bodyB.angularVelocity = { x: 0, y: 1, z: 0 };

        const result = simulate(def, bodyA, bodyB, { steps: 40 });
        expect(result.finalSwing).toBeLessThan(2 * DEG);  // forced to the apex
        expect(result.finalSwing).toBeLessThan(10 * DEG); // decreased from start
        expect(result.allFinite).toBe(true);
    });

    it('cone limit π leaves the swing completely free without NaN', () => {
        const def = makeConeDef({ swingSpan1: Math.PI, swingSpan2: Math.PI });
        const bodyA = makeStaticBody(1, { ...ZERO });
        const bodyB = makeSolverBody(2, { ...ZERO }, { x: 0, y: Math.sin(45 * DEG), z: 0, w: Math.cos(45 * DEG) });
        bodyB.angularVelocity = { x: 0, y: 1, z: 0 };

        const result = simulate(def, bodyA, bodyB, { steps: 10 });
        expect(result.maxAngularRows).toBe(0);              // never any swing row
        expect(result.finalSwing).toBeGreaterThan(95 * DEG); // kept rotating
        expect(result.allFinite).toBe(true);
    });

    it('a zero twist axis (degenerate frame rotation) produces no NaN or throw', () => {
        const def = makeConeDef({
            swingSpan1: CONE_30,
            swingSpan2: CONE_30,
            twistSpan: TWIST_SPAN_60,
            localFrameA: { position: { ...ZERO }, rotation: { x: 0, y: 0, z: 0, w: 0 } }, // invalid zero quat
        });
        const bodyA = makeStaticBody(1, { ...ZERO });
        const bodyB = makeSolverBody(2, { ...ZERO });

        let rows: JacobianRow3D[] = [];
        expect(() => {
            rows = runPrepare(def, bodyA, bodyB);
        }).not.toThrow();
        expect(rowsFinite(rows)).toBe(true);

        const result = simulate(def, bodyA, bodyB, { steps: 10, torqueB: { x: 0, y: 2, z: 0 } });
        expect(result.allFinite).toBe(true);
    });

    it('no limits and no motor yields exactly the three linear rows', () => {
        const def = makeConeDef();
        const bodyA = makeStaticBody(1, { ...ZERO });
        const bodyB = makeSolverBody(2, { ...ZERO }, { x: 0, y: Math.sin(17.5 * DEG), z: 0, w: Math.cos(17.5 * DEG) });

        const rows = runPrepare(def, bodyA, bodyB);
        expect(rows.length).toBe(3);
        expect(countAngularRows(rows)).toBe(0);
        expect(rowsFinite(rows)).toBe(true);
    });
});

// ─── 11. Iteration convergence ────────────────────────────────────────────────

describe('Scenario 11 — iteration convergence', () => {
    /**
     * One coupled step: both bodies dynamic, large moment arms so the three
     * linear rows interact through the shared angular response, and initial
     * angular velocities so every row has work to do. Returns the anchor gap
     * after a single step solved with `iterations` Gauss-Seidel sweeps.
     */
    function coupledStepGap(iterations: number): number {
        const def = makeConeDef(); // no angular limits — isolate the linear rows
        const frame: { position: IVec3Like; rotation: IQuatLike } = {
            position: { x: 0.3, y: 0.25, z: 0.2 },
            rotation: { ...IDENTITY },
        };
        const bodyA = makeSolverBody(1, { ...ZERO });
        const bodyB = makeSolverBody(2, { x: 0.1, y: 0.08, z: 0.06 });
        bodyA.angularVelocity = { x: 0.2, y: -0.1, z: 0.05 };
        bodyB.angularVelocity = { x: -0.15, y: 0.2, z: -0.1 };
        const constrained = makeConeDef({ localFrameA: frame, localFrameB: frame });

        const rows = runPrepare(constrained, bodyA, bodyB, 1 / 60);
        expect(rows.length).toBe(3);
        solveRows(rows, bodyA, bodyB, iterations);
        integrateBody(bodyA, 1 / 60);
        integrateBody(bodyB, 1 / 60);
        void def;
        return anchorGap(constrained, bodyA, bodyB);
    }

    it('converges the anchor error tighter with more solver iterations', () => {
        const gap0 = 0.141; // |(0.1, 0.08, 0.06)| initial anchor separation
        const gap1 = coupledStepGap(1);
        const gap12 = coupledStepGap(12);

        expect(Number.isFinite(gap1)).toBe(true);
        expect(Number.isFinite(gap12)).toBe(true);
        expect(gap1).toBeLessThan(gap0);   // one sweep already helps
        expect(gap12).toBeLessThan(gap1);  // more sweeps converge tighter
    });
});
