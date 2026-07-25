import { describe, it, expect } from 'vitest';
import { createRaycastSystem2D, ShapeType, RaycastFlags, RaycastLayer } from '@axrone/physics';
import type { BodyId, ShapeId, LayerMask } from '@axrone/physics';
import type { IVec2Like } from '@axrone/numeric';

const v2 = (x: number, y: number): IVec2Like => ({ x, y });

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
