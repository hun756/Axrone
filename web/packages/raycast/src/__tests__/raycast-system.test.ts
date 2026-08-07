import { describe, it, expect } from 'vitest';
import { createRaycastSystem2D, createRaycastSystem3D, RaycastFlags, RaycastLayer, InvalidRayError } from '../index';
import { ShapeType } from '@axrone/physics-core';
import type { BodyId, ShapeId } from '@axrone/physics-core';
import type { LayerMask } from '../index';
import type { IVec2Like, IVec3Like } from '@axrone/numeric';

const v2 = (x: number, y: number): IVec2Like => ({ x, y });
const v3 = (x: number, y: number, z: number): IVec3Like => ({ x, y, z });

const ALL: LayerMask = RaycastLayer.All as LayerMask;
const bid = (n: number) => n as BodyId;
const sid = (n: number) => n as ShapeId;

describe('RaycastSystem2D — caching', () => {
    it('serves an identical ray from the cache on the second call', () => {
        const sys = createRaycastSystem2D();
        sys.registerShape(bid(1), sid(10), ALL, ShapeType.Circle, { center: { x: 5, y: 0 }, radius: 1 });

        const first = sys.raycast(v2(-5, 0), v2(1, 0), 100, ALL);
        const second = sys.raycast(v2(-5, 0), v2(1, 0), 100, ALL);

        expect(first).not.toBeNull();
        expect(second).not.toBeNull();
        expect(sys.statistics.cacheHits).toBeGreaterThanOrEqual(1);
    });

    it('does not serve a cached hit for a different ray', () => {
        const sys = createRaycastSystem2D();
        sys.registerShape(bid(1), sid(10), ALL, ShapeType.Circle, { center: { x: 5, y: 0 }, radius: 1 });

        sys.raycast(v2(-5, 0), v2(1, 0), 100, ALL);
        const other = sys.raycast(v2(-5, 5), v2(1, 0), 100, ALL);

        expect(other).toBeNull();
        expect(sys.statistics.cacheHits).toBe(0);
    });
});

describe('RaycastSystem2D — async batching', () => {
    it('queues async rays and invokes their callbacks on flush', () => {
        const sys = createRaycastSystem2D();
        sys.registerShape(bid(1), sid(10), ALL, ShapeType.Circle, { center: { x: 5, y: 0 }, radius: 1 });
        sys.enableBatching = true;

        const results: Array<unknown | null> = [];
        sys.raycastAsync(v2(-5, 0), v2(1, 0), 100, ALL, RaycastFlags.ClosestOnly, (h) => results.push(h));

        expect(results.length).toBe(0);
        sys.flushBatch();
        expect(results.length).toBe(1);
        expect(results[0]).not.toBeNull();
    });

    it('invokes the callback immediately when batching is disabled', () => {
        const sys = createRaycastSystem2D();
        sys.registerShape(bid(1), sid(10), ALL, ShapeType.Circle, { center: { x: 5, y: 0 }, radius: 1 });
        sys.enableBatching = false;

        let called = false;
        sys.raycastAsync(v2(-5, 0), v2(1, 0), 100, ALL, RaycastFlags.ClosestOnly, (h) => {
            called = true;
            expect(h).not.toBeNull();
        });

        expect(called).toBe(true);
    });
});

// ── 3D System ────────────────────────────────────────────────────────

describe('RaycastSystem3D — caching and raycasting', () => {
    it('serves an identical 3D ray from the cache on the second call', () => {
        const sys = createRaycastSystem3D();
        sys.registerShape(bid(1), sid(10), ALL, ShapeType.Sphere, { center: { x: 5, y: 0, z: 0 }, radius: 1 });

        const first = sys.raycast(v3(-5, 0, 0), v3(1, 0, 0), 100, ALL);
        const second = sys.raycast(v3(-5, 0, 0), v3(1, 0, 0), 100, ALL);

        expect(first).not.toBeNull();
        expect(second).not.toBeNull();
        expect(sys.statistics.cacheHits).toBeGreaterThanOrEqual(1);
    });

    it('does not serve a cached hit for a different 3D ray', () => {
        const sys = createRaycastSystem3D();
        sys.registerShape(bid(1), sid(10), ALL, ShapeType.Sphere, { center: { x: 5, y: 0, z: 0 }, radius: 1 });

        sys.raycast(v3(-5, 0, 0), v3(1, 0, 0), 100, ALL);
        const other = sys.raycast(v3(-5, 5, 0), v3(1, 0, 0), 100, ALL);

        expect(other).toBeNull();
        expect(sys.statistics.cacheHits).toBe(0);
    });
});

// ── Validation ───────────────────────────────────────────────────────

describe('RaycastSystem2D — validation', () => {
    it('throws InvalidRayError for NaN origin', () => {
        const sys = createRaycastSystem2D();
        expect(() => sys.raycast(v2(NaN, 0), v2(1, 0), 100, ALL)).toThrow(InvalidRayError);
    });

    it('throws InvalidRayError for zero-length direction', () => {
        const sys = createRaycastSystem2D();
        expect(() => sys.raycast(v2(0, 0), v2(0, 0), 100, ALL)).toThrow(InvalidRayError);
    });

    it('throws InvalidRayError for negative maxDistance', () => {
        const sys = createRaycastSystem2D();
        expect(() => sys.raycast(v2(0, 0), v2(1, 0), -1, ALL)).toThrow(InvalidRayError);
    });

    it('throws InvalidRayError for Infinity maxDistance', () => {
        const sys = createRaycastSystem2D();
        expect(() => sys.raycast(v2(0, 0), v2(1, 0), Infinity, ALL)).toThrow(InvalidRayError);
    });
});

describe('RaycastSystem3D — validation', () => {
    it('throws InvalidRayError for NaN direction in 3D', () => {
        const sys = createRaycastSystem3D();
        expect(() => sys.raycast(v3(0, 0, 0), v3(NaN, 0, 0), 100, ALL)).toThrow(InvalidRayError);
    });

    it('throws InvalidRayError for zero-length direction in 3D', () => {
        const sys = createRaycastSystem3D();
        expect(() => sys.raycast(v3(0, 0, 0), v3(0, 0, 0), 100, ALL)).toThrow(InvalidRayError);
    });

    it('throws InvalidRayError for negative maxDistance in 3D', () => {
        const sys = createRaycastSystem3D();
        expect(() => sys.raycast(v3(0, 0, 0), v3(1, 0, 0), -5, ALL)).toThrow(InvalidRayError);
    });
});

// ── Cache control ────────────────────────────────────────────────────

describe('RaycastSystem2D — cache control', () => {
    it('clearCache forces re-computation on next raycast', () => {
        const sys = createRaycastSystem2D();
        sys.registerShape(bid(1), sid(10), ALL, ShapeType.Circle, { center: { x: 5, y: 0 }, radius: 1 });

        sys.raycast(v2(-5, 0), v2(1, 0), 100, ALL);
        sys.clearCache();
        expect(sys.statistics.cacheHits).toBe(0);

        const hit = sys.raycast(v2(-5, 0), v2(1, 0), 100, ALL);
        expect(hit).not.toBeNull();
    });

    it('disabling cache prevents caching', () => {
        const sys = createRaycastSystem2D();
        sys.registerShape(bid(1), sid(10), ALL, ShapeType.Circle, { center: { x: 5, y: 0 }, radius: 1 });
        sys.enableCache = false;

        sys.raycast(v2(-5, 0), v2(1, 0), 100, ALL);
        sys.raycast(v2(-5, 0), v2(1, 0), 100, ALL);

        expect(sys.statistics.cacheHits).toBe(0);
    });
});
