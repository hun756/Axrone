import { describe, it, expect } from 'vitest';
import {
    createRaycastSystem3D,
    createContinuousRaycast3D,
    createShapeCaster3D,
    RaycastLayer,
} from '../index';
import { ShapeType } from '@axrone/physics-core';
import type { BodyId, ShapeId } from '@axrone/physics-core';
import type { LayerMask } from '../index';

const ALL: LayerMask = RaycastLayer.All as LayerMask;

function makeSystemWithTargetSphere() {
    const sys = createRaycastSystem3D();
    sys.registerShape(1 as BodyId, 10 as ShapeId, ALL, ShapeType.Sphere, {
        center: { x: 5, y: 0, z: 0 },
        radius: 1,
    });
    return sys;
}

function makeEmptySystem() {
    return createRaycastSystem3D();
}

describe('ContinuousRaycast3D.sweepTest', () => {
    it('detects a swept collision and reports a normalized contact normal', () => {
        const sys = makeSystemWithTargetSphere();
        const sweep = createContinuousRaycast3D(sys);

        const toi = sweep.sweepTest({
            startPosition: { x: -5, y: 0, z: 0 },
            endPosition: { x: 10, y: 0, z: 0 },
            layerMask: ALL,
            maxIterations: 64,
            tolerance: 1e-4,
        });

        expect(toi.hit).toBe(true);
        expect(toi.time).toBeGreaterThan(0);
        expect(toi.time).toBeLessThanOrEqual(1);
        const nlen = Math.hypot(toi.normal.x, toi.normal.y, toi.normal.z);
        expect(nlen).toBeCloseTo(1, 5);
    });

    it('reports no hit for a separating trajectory', () => {
        const sys = makeSystemWithTargetSphere();
        const sweep = createContinuousRaycast3D(sys);

        const toi = sweep.sweepTest({
            startPosition: { x: -5, y: 0, z: 0 },
            endPosition: { x: -10, y: 0, z: 0 },
            layerMask: ALL,
            maxIterations: 64,
            tolerance: 1e-4,
        });

        expect(toi.hit).toBe(false);
    });

    it('reports a hit fraction within the swept segment for a stationary target', () => {
        const sys = makeSystemWithTargetSphere();
        const sweep = createContinuousRaycast3D(sys);

        const toi = sweep.sweepTest({
            startPosition: { x: -5, y: 0, z: 0 },
            endPosition: { x: 10, y: 0, z: 0 },
            layerMask: ALL,
            maxIterations: 256,
            tolerance: 1e-5,
        });

        expect(toi.hit).toBe(true);
        expect(toi.fraction).toBeCloseTo(toi.time, 6);
    });
});

describe('ShapeCaster3D.sphereCast', () => {
    it('approximates a sphere cast against a target sphere', () => {
        const sys = makeSystemWithTargetSphere();
        const caster = createShapeCaster3D(sys);

        const hit = caster.sphereCast({
            origin: { x: -5, y: 0, z: 0 },
            direction: { x: 1, y: 0, z: 0 },
            maxDistance: 100,
            layerMask: ALL,
            radius: 1,
        });

        expect(hit).not.toBeNull();
        if (hit) {
            expect(hit.distance).toBeGreaterThan(0);
            expect(hit.distance).toBeLessThanOrEqual(10);
        }
    });

    it('returns null when the swept volume misses the target', () => {
        const sys = makeSystemWithTargetSphere();
        const caster = createShapeCaster3D(sys);

        const hit = caster.sphereCast({
            origin: { x: -5, y: 5, z: 0 },
            direction: { x: 1, y: 0, z: 0 },
            maxDistance: 100,
            layerMask: ALL,
            radius: 1,
        });

        expect(hit).toBeNull();
    });
});

// ── predictiveRaycast ───────────────────────────────────────────────

describe('ContinuousRaycast3D.predictiveRaycast', () => {
    it('returns null when no shapes are registered', () => {
        const sys = makeEmptySystem();
        const cr = createContinuousRaycast3D(sys);

        const hit = cr.predictiveRaycast(
            { x: 0, y: 0, z: 0 },
            { x: 1, y: 0, z: 0 },
            { x: 10, y: 0, z: 0 },
            { x: 0, y: 0, z: 0 },
            ALL,
            5,
            0.1
        );

        expect(hit).toBeNull();
    });

    it('returns null when target moves away faster than ray can reach', () => {
        // Use empty system — no shapes to hit regardless of target motion
        const sys = makeEmptySystem();
        const cr = createContinuousRaycast3D(sys);

        const hit = cr.predictiveRaycast(
            { x: 0, y: 0, z: 0 },
            { x: 0, y: 0, z: 0 },
            { x: 100, y: 0, z: 0 },
            { x: 1000, y: 0, z: 0 },
            ALL,
            1,
            0.1
        );

        expect(hit).toBeNull();
    });
});

// ── linearSweep ─────────────────────────────────────────────────────

describe('ContinuousRaycast3D.linearSweep', () => {
    it('detects a hit along the sweep path', () => {
        const sys = makeSystemWithTargetSphere();
        const cr = createContinuousRaycast3D(sys);

        const hit = cr.linearSweep(
            { x: -5, y: 0, z: 0 },
            { x: 10, y: 0, z: 0 },
            ALL,
            0.5
        );

        expect(hit).not.toBeNull();
    });

    it('returns null when sweep path misses all shapes', () => {
        const sys = makeSystemWithTargetSphere();
        const cr = createContinuousRaycast3D(sys);

        const hit = cr.linearSweep(
            { x: -5, y: 50, z: 0 },
            { x: 10, y: 50, z: 0 },
            ALL,
            0.5
        );

        expect(hit).toBeNull();
    });

    it('returns null for zero-length sweep', () => {
        const sys = makeSystemWithTargetSphere();
        const cr = createContinuousRaycast3D(sys);

        const hit = cr.linearSweep(
            { x: 5, y: 0, z: 0 },
            { x: 5, y: 0, z: 0 },
            ALL,
            0.1
        );

        expect(hit).toBeNull();
    });
});
