import { describe, it, expect } from 'vitest';
import { BoundingVolumeHierarchy } from '../index';
import type { IVec3Like } from '@axrone/numeric';

const v3 = (x: number, y: number, z: number): IVec3Like => ({ x, y, z });

function makePrimitive(index: number, cx: number, cy: number, cz: number, halfSize = 0.5) {
    return {
        index,
        centroid: v3(cx, cy, cz),
        bounds: {
            min: v3(cx - halfSize, cy - halfSize, cz - halfSize),
            max: v3(cx + halfSize, cy + halfSize, cz + halfSize),
        },
    };
}

describe('BoundingVolumeHierarchy', () => {
    describe('build', () => {
        it('empty primitives produces zero nodes', () => {
            const bvh = new BoundingVolumeHierarchy();
            bvh.build([]);
            expect(bvh.nodeCount).toBe(0);
            expect(bvh.leafCount).toBe(0);
        });

        it('single primitive produces single leaf node', () => {
            const bvh = new BoundingVolumeHierarchy();
            bvh.build([makePrimitive(0, 0, 0, 0)]);
            expect(bvh.nodeCount).toBe(1);
            expect(bvh.leafCount).toBe(1);
            expect(bvh.maxDepth).toBe(0);
        });

        it('multiple primitives produce valid tree', () => {
            const bvh = new BoundingVolumeHierarchy();
            const prims = [
                makePrimitive(0, 0, 0, 0),
                makePrimitive(1, 10, 0, 0),
                makePrimitive(2, 20, 0, 0),
                makePrimitive(3, 30, 0, 0),
                makePrimitive(4, 40, 0, 0),
            ];
            bvh.build(prims);
            expect(bvh.nodeCount).toBeGreaterThan(0);
            expect(bvh.leafCount).toBeGreaterThan(0);
        });

        it('many primitives produce tree with depth > 0', () => {
            const bvh = new BoundingVolumeHierarchy();
            const prims = Array.from({ length: 50 }, (_, i) => makePrimitive(i, i * 3, 0, 0));
            bvh.build(prims);
            expect(bvh.maxDepth).toBeGreaterThanOrEqual(1);
        });
    });

    describe('intersect', () => {
        it('ray hitting a primitive fires callback with correct primIndex', () => {
            const bvh = new BoundingVolumeHierarchy();
            const prims = [
                makePrimitive(0, 5, 0, 0, 1),
                makePrimitive(1, 50, 0, 0, 1),
            ];
            bvh.build(prims);

            const invDir = v3(1, Number.MAX_VALUE, Number.MAX_VALUE);
            const hitIndices: number[] = [];
            const found = bvh.intersect(v3(0, 0, 0), invDir, 100, (primIndex) => {
                hitIndices.push(primIndex);
                return true;
            });

            expect(found).toBe(true);
            expect(hitIndices).toContain(0);
        });

        it('ray missing all primitives returns false', () => {
            const bvh = new BoundingVolumeHierarchy();
            bvh.build([makePrimitive(0, 5, 100, 0, 1)]);

            const invDir = v3(1, Number.MAX_VALUE, Number.MAX_VALUE);
            const found = bvh.intersect(v3(0, 0, 0), invDir, 100, () => true);
            expect(found).toBe(false);
        });

        it('callback returning false does not set foundHit', () => {
            const bvh = new BoundingVolumeHierarchy();
            bvh.build([makePrimitive(0, 5, 0, 0, 1)]);

            const invDir = v3(1, Number.MAX_VALUE, Number.MAX_VALUE);
            const found = bvh.intersect(v3(0, 0, 0), invDir, 100, () => false);
            expect(found).toBe(false);
        });

        it('intersect on empty BVH returns false', () => {
            const bvh = new BoundingVolumeHierarchy();
            bvh.build([]);
            const found = bvh.intersect(v3(0, 0, 0), v3(1, 0, 0), 100, () => true);
            expect(found).toBe(false);
        });
    });

    describe('split methods', () => {
        it('builds valid tree with Middle split (enum value 0)', () => {
            const bvh = new BoundingVolumeHierarchy(0 as any);
            const prims = Array.from({ length: 20 }, (_, i) => makePrimitive(i, i * 2, 0, 0));
            bvh.build(prims);
            expect(bvh.nodeCount).toBeGreaterThan(0);
            expect(bvh.leafCount).toBeGreaterThan(0);
        });

        it('builds valid tree with EqualCounts split (enum value 2)', () => {
            const bvh = new BoundingVolumeHierarchy(2 as any);
            const prims = Array.from({ length: 20 }, (_, i) => makePrimitive(i, i * 2, 0, 0));
            bvh.build(prims);
            expect(bvh.nodeCount).toBeGreaterThan(0);
            expect(bvh.leafCount).toBeGreaterThan(0);
        });
    });

    describe('degenerate cases', () => {
        it('all coincident centroids produce valid tree (single leaf)', () => {
            const bvh = new BoundingVolumeHierarchy();
            const prims = Array.from({ length: 10 }, (_, i) => makePrimitive(i, 0, 0, 0, 1));
            bvh.build(prims);
            expect(bvh.nodeCount).toBeGreaterThan(0);
            expect(bvh.leafCount).toBeGreaterThanOrEqual(1);
        });

        it('collinear centroids (all same Y,Z) produce valid tree', () => {
            const bvh = new BoundingVolumeHierarchy();
            const prims = Array.from({ length: 15 }, (_, i) => makePrimitive(i, i * 2, 0, 0));
            bvh.build(prims);
            expect(bvh.nodeCount).toBeGreaterThan(0);
        });
    });
});
