import { describe, it, expect } from 'vitest';
import {
    RaycastCache2D,
    RaycastCache3D,
    RaycastBatcher2D,
    RaycastBatcher3D,
    RaycastStatistics,
    RaycastFlags,
} from '../index';
import type { IRaycastHit2D, IRaycastHit3D, LayerMask } from '../index';
import type { IVec2Like, IVec3Like } from '@axrone/numeric';

const v2 = (x: number, y: number): IVec2Like => ({ x, y });
const v3 = (x: number, y: number, z: number): IVec3Like => ({ x, y, z });

function makeHit2D(distance = 5): IRaycastHit2D {
    return {
        bodyId: 0,
        shapeId: 0,
        point: v2(0, 0),
        normal: v2(0, 1),
        distance,
        fraction: 0.5,
        layer: 0,
    };
}

function makeHit3D(distance = 5): IRaycastHit3D {
    return {
        bodyId: 0,
        shapeId: 0,
        point: v3(0, 0, 0),
        normal: v3(0, 1, 0),
        distance,
        fraction: 0.5,
        triangleIndex: -1,
        barycentric: null,
        layer: 0,
    };
}

describe('RaycastCache2D', () => {
    it('returns null for a cache miss', () => {
        const cache = new RaycastCache2D();
        const result = cache.get(v2(0, 0), v2(1, 0), 100, 0 as LayerMask);
        expect(result).toBeNull();
    });

    it('returns cached hit on same frame after set', () => {
        const cache = new RaycastCache2D();
        const hit = makeHit2D(7);
        cache.set(v2(0, 0), v2(1, 0), 100, 0 as LayerMask, hit);
        const cached = cache.get(v2(0, 0), v2(1, 0), 100, 0 as LayerMask);
        expect(cached).not.toBeNull();
        expect(cached!.distance).toBe(7);
    });

    it('returns null after advanceFrame (different frame)', () => {
        const cache = new RaycastCache2D();
        cache.set(v2(0, 0), v2(1, 0), 100, 0 as LayerMask, makeHit2D());
        cache.advanceFrame();
        expect(cache.get(v2(0, 0), v2(1, 0), 100, 0 as LayerMask)).toBeNull();
    });

    it('clear empties the cache', () => {
        const cache = new RaycastCache2D();
        cache.set(v2(0, 0), v2(1, 0), 100, 0 as LayerMask, makeHit2D());
        cache.clear();
        expect(cache.get(v2(0, 0), v2(1, 0), 100, 0 as LayerMask)).toBeNull();
    });

    it('evicts oldest entry when maxSize exceeded', () => {
        const cache = new RaycastCache2D(2);
        // Fill cache to capacity in frame 0
        cache.set(v2(0, 0), v2(1, 0), 100, 0 as LayerMask, makeHit2D(1));
        cache.set(v2(1, 0), v2(1, 0), 100, 0 as LayerMask, makeHit2D(2));
        // Both retrievable in same frame
        expect(cache.get(v2(0, 0), v2(1, 0), 100, 0 as LayerMask)).not.toBeNull();
        expect(cache.get(v2(1, 0), v2(1, 0), 100, 0 as LayerMask)).not.toBeNull();

        // Advance frame and add a third entry — triggers eviction of oldest
        cache.advanceFrame();
        cache.set(v2(2, 0), v2(1, 0), 100, 0 as LayerMask, makeHit2D(3));

        // Only the current-frame entry is returned by get();
        // the evicted entry is gone, and the other is stale (different frame)
        expect(cache.get(v2(2, 0), v2(1, 0), 100, 0 as LayerMask)).not.toBeNull();
        // At least one earlier entry was evicted (no longer in the map at all)
        // and the remaining old entry is stale (wrong frame)
        let currentFrameHits = 0;
        if (cache.get(v2(0, 0), v2(1, 0), 100, 0 as LayerMask)) currentFrameHits++;
        if (cache.get(v2(1, 0), v2(1, 0), 100, 0 as LayerMask)) currentFrameHits++;
        if (cache.get(v2(2, 0), v2(1, 0), 100, 0 as LayerMask)) currentFrameHits++;
        expect(currentFrameHits).toBe(1); // only the entry from current frame
    });
});

describe('RaycastCache3D', () => {
    it('returns null for a cache miss', () => {
        const cache = new RaycastCache3D();
        expect(cache.get(v3(0, 0, 0), v3(1, 0, 0), 100, 0 as LayerMask)).toBeNull();
    });

    it('returns cached hit on same frame after set', () => {
        const cache = new RaycastCache3D();
        cache.set(v3(0, 0, 0), v3(1, 0, 0), 100, 0 as LayerMask, makeHit3D(12));
        const cached = cache.get(v3(0, 0, 0), v3(1, 0, 0), 100, 0 as LayerMask);
        expect(cached).not.toBeNull();
        expect(cached!.distance).toBe(12);
    });

    it('returns null after advanceFrame', () => {
        const cache = new RaycastCache3D();
        cache.set(v3(0, 0, 0), v3(1, 0, 0), 100, 0 as LayerMask, makeHit3D());
        cache.advanceFrame();
        expect(cache.get(v3(0, 0, 0), v3(1, 0, 0), 100, 0 as LayerMask)).toBeNull();
    });

    it('clear empties the cache', () => {
        const cache = new RaycastCache3D();
        cache.set(v3(0, 0, 0), v3(1, 0, 0), 100, 0 as LayerMask, makeHit3D());
        cache.clear();
        expect(cache.get(v3(0, 0, 0), v3(1, 0, 0), 100, 0 as LayerMask)).toBeNull();
    });
});

describe('RaycastBatcher2D', () => {
    it('add increments pendingCount', () => {
        const batcher = new RaycastBatcher2D();
        expect(batcher.pendingCount).toBe(0);
        batcher.add(v2(0, 0), v2(1, 0), 100, 0 as LayerMask, RaycastFlags.ClosestOnly, () => {});
        expect(batcher.pendingCount).toBe(1);
    });

    it('flush invokes all callbacks', () => {
        const batcher = new RaycastBatcher2D();
        const results: unknown[] = [];
        batcher.add(v2(0, 0), v2(1, 0), 100, 0 as LayerMask, RaycastFlags.ClosestOnly, (h) => results.push(h));
        batcher.add(v2(1, 0), v2(1, 0), 100, 0 as LayerMask, RaycastFlags.ClosestOnly, (h) => results.push(h));
        batcher.flush();
        expect(results.length).toBe(2);
        expect(batcher.pendingCount).toBe(0);
    });

    it('flush with no raycaster returns null to callbacks', () => {
        const batcher = new RaycastBatcher2D();
        let received: unknown = 'unset';
        batcher.add(v2(0, 0), v2(1, 0), 100, 0 as LayerMask, RaycastFlags.ClosestOnly, (h) => { received = h; });
        batcher.flush();
        expect(received).toBeNull();
    });

    it('flush with raycaster invokes it', () => {
        const batcher = new RaycastBatcher2D();
        const mockHit = makeHit2D(42);
        batcher.setRaycaster(() => mockHit);
        let received: IRaycastHit2D | null = null;
        batcher.add(v2(0, 0), v2(1, 0), 100, 0 as LayerMask, RaycastFlags.ClosestOnly, (h) => { received = h; });
        batcher.flush();
        expect(received).toBe(mockHit);
    });

    it('auto-flushes when pending reaches batchSize', () => {
        const batcher = new RaycastBatcher2D(2);
        let callCount = 0;
        const cb = () => { callCount++; };
        batcher.add(v2(0, 0), v2(1, 0), 100, 0 as LayerMask, RaycastFlags.ClosestOnly, cb);
        expect(callCount).toBe(0);
        batcher.add(v2(1, 0), v2(1, 0), 100, 0 as LayerMask, RaycastFlags.ClosestOnly, cb);
        expect(callCount).toBe(2);
    });

    it('flush on empty batch is a no-op', () => {
        const batcher = new RaycastBatcher2D();
        batcher.flush();
        expect(batcher.pendingCount).toBe(0);
    });
});

describe('RaycastBatcher3D', () => {
    it('add increments pendingCount and flush invokes callbacks', () => {
        const batcher = new RaycastBatcher3D();
        const results: unknown[] = [];
        batcher.add(v3(0, 0, 0), v3(1, 0, 0), 100, 0 as LayerMask, RaycastFlags.ClosestOnly, (h) => results.push(h));
        expect(batcher.pendingCount).toBe(1);
        batcher.flush();
        expect(results.length).toBe(1);
        expect(batcher.pendingCount).toBe(0);
    });

    it('flush with raycaster invokes it', () => {
        const batcher = new RaycastBatcher3D();
        const mockHit = makeHit3D(99);
        batcher.setRaycaster(() => mockHit);
        let received: IRaycastHit3D | null = null;
        batcher.add(v3(0, 0, 0), v3(1, 0, 0), 100, 0 as LayerMask, RaycastFlags.ClosestOnly, (h) => { received = h; });
        batcher.flush();
        expect(received).toBe(mockHit);
    });
});

describe('RaycastStatistics', () => {
    it('starts with all zeroes', () => {
        const stats = new RaycastStatistics();
        expect(stats.totalRaycasts).toBe(0);
        expect(stats.hitCount).toBe(0);
        expect(stats.missCount).toBe(0);
        expect(stats.cacheHits).toBe(0);
        expect(stats.hitRate).toBe(0);
        expect(stats.cacheHitRate).toBe(0);
        expect(stats.averageTestsPerRay).toBe(0);
        expect(stats.frameRaycasts).toBe(0);
    });

    it('recordRaycast increments counters correctly', () => {
        const stats = new RaycastStatistics();
        stats.recordRaycast(true, 3);
        stats.recordRaycast(false, 1);
        expect(stats.totalRaycasts).toBe(2);
        expect(stats.hitCount).toBe(1);
        expect(stats.missCount).toBe(1);
        expect(stats.frameRaycasts).toBe(2);
    });

    it('hitRate computes correctly', () => {
        const stats = new RaycastStatistics();
        stats.recordRaycast(true, 1);
        stats.recordRaycast(true, 1);
        stats.recordRaycast(false, 1);
        expect(stats.hitRate).toBeCloseTo(2 / 3, 5);
    });

    it('cacheHitRate computes correctly', () => {
        const stats = new RaycastStatistics();
        stats.recordRaycast(true, 1);
        stats.recordRaycast(false, 1);
        stats.recordCacheHit();
        expect(stats.cacheHitRate).toBeCloseTo(0.5, 5);
    });

    it('averageTestsPerRay computes correctly', () => {
        const stats = new RaycastStatistics();
        stats.recordRaycast(true, 4);
        stats.recordRaycast(false, 2);
        expect(stats.averageTestsPerRay).toBeCloseTo(3, 5);
    });

    it('endFrame resets frameRaycasts', () => {
        const stats = new RaycastStatistics();
        stats.recordRaycast(true, 1);
        stats.recordRaycast(true, 1);
        expect(stats.frameRaycasts).toBe(2);
        stats.endFrame();
        expect(stats.frameRaycasts).toBe(0);
        expect(stats.totalRaycasts).toBe(2);
    });

    it('reset zeroes all counters', () => {
        const stats = new RaycastStatistics();
        stats.recordRaycast(true, 5);
        stats.recordCacheHit();
        stats.reset();
        expect(stats.totalRaycasts).toBe(0);
        expect(stats.hitCount).toBe(0);
        expect(stats.missCount).toBe(0);
        expect(stats.cacheHits).toBe(0);
        expect(stats.averageTestsPerRay).toBe(0);
    });
});
