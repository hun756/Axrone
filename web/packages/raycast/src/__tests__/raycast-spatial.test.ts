import { describe, it, expect } from 'vitest';
import { SpatialHashGrid3D, SpatialOctree } from '../index';
import type { IVec3Like } from '@axrone/numeric';

const v3 = (x: number, y: number, z: number): IVec3Like => ({ x, y, z });

describe('SpatialHashGrid3D', () => {
    it('inserts an item and finds it within an overlapping query', () => {
        const grid = new SpatialHashGrid3D<number>(1);
        grid.insert(1, v3(0, 0, 0), v3(1, 1, 1));

        const res = grid.query(v3(0.5, 0.5, 0.5), v3(1.5, 1.5, 1.5));
        expect(res).toContain(1);
    });

    it('does not return items whose cells do not overlap the query', () => {
        const grid = new SpatialHashGrid3D<number>(1);
        grid.insert(1, v3(0, 0, 0), v3(1, 1, 1));

        const res = grid.query(v3(5, 5, 5), v3(6, 6, 6));
        expect(res).not.toContain(1);
    });

    it('removes an item so it is no longer returned', () => {
        const grid = new SpatialHashGrid3D<number>(1);
        grid.insert(1, v3(0, 0, 0), v3(1, 1, 1));
        grid.remove(1);

        const res = grid.query(v3(0, 0, 0), v3(2, 2, 2));
        expect(res).not.toContain(1);
    });

    it('returns each inserted item exactly once (Set-based, no duplicates)', () => {
        const grid = new SpatialHashGrid3D<number>(1);
        grid.insert(1, v3(4, 0, 0), v3(5, 1, 1));
        grid.insert(2, v3(4, 0, 0), v3(5, 1, 1));

        const res = grid.queryRay(v3(-5, 0.5, 0.5), v3(1, 0, 0), 100);
        expect(res).toContain(1);
        expect(res).toContain(2);
        expect(res.filter((x) => x === 1).length).toBe(1);
        expect(res.filter((x) => x === 2).length).toBe(1);
    });

    it('keeps far-apart cells independent (integer cell hashing)', () => {
        const grid = new SpatialHashGrid3D<number>(1);
        grid.insert(1, v3(0, 0, 0), v3(1, 1, 1));
        grid.insert(2, v3(1000, 1000, 1000), v3(1001, 1001, 1001));

        const near = grid.query(v3(0, 0, 0), v3(2, 2, 2));
        expect(near).toContain(1);
        expect(near).not.toContain(2);

        const far = grid.query(v3(1000, 1000, 1000), v3(1002, 1002, 1002));
        expect(far).toContain(2);
        expect(far).not.toContain(1);
    });

    it("updates an item's cells when re-inserted via update()", () => {
        const grid = new SpatialHashGrid3D<number>(1);
        grid.insert(1, v3(0, 0, 0), v3(1, 1, 1));
        grid.update(1, v3(50, 50, 50), v3(51, 51, 51));

        expect(grid.query(v3(0, 0, 0), v3(2, 2, 2))).not.toContain(1);
        expect(grid.query(v3(50, 50, 50), v3(52, 52, 52))).toContain(1);
    });
});

// ── SpatialOctree ────────────────────────────────────────────────────

describe('SpatialOctree', () => {
    it('inserts an item and finds it via AABB query', () => {
        const octree = new SpatialOctree<number>(v3(0, 0, 0), 100);
        octree.insert(1, v3(-1, -1, -1), v3(1, 1, 1));

        const res = octree.query(v3(-2, -2, -2), v3(2, 2, 2));
        expect(res).toContain(1);
    });

    it('does not return items outside the query region', () => {
        const octree = new SpatialOctree<number>(v3(0, 0, 0), 100);
        octree.insert(1, v3(-1, -1, -1), v3(1, 1, 1));

        const res = octree.query(v3(50, 50, 50), v3(60, 60, 60));
        expect(res).not.toContain(1);
    });

    it('removes an item so it is no longer returned', () => {
        const octree = new SpatialOctree<number>(v3(0, 0, 0), 100);
        octree.insert(1, v3(-1, -1, -1), v3(1, 1, 1));
        expect(octree.remove(1)).toBe(true);

        const res = octree.query(v3(-2, -2, -2), v3(2, 2, 2));
        expect(res).not.toContain(1);
    });

    it('returns false when removing a non-existent item', () => {
        const octree = new SpatialOctree<number>(v3(0, 0, 0), 100);
        expect(octree.remove(999)).toBe(false);
    });

    it('clears all items', () => {
        const octree = new SpatialOctree<number>(v3(0, 0, 0), 100);
        octree.insert(1, v3(-1, -1, -1), v3(1, 1, 1));
        octree.insert(2, v3(10, 10, 10), v3(11, 11, 11));
        octree.clear();

        expect(octree.itemCount).toBe(0);
        expect(octree.query(v3(-2, -2, -2), v3(2, 2, 2))).not.toContain(1);
    });

    it('reports correct itemCount after insertions and removals', () => {
        const octree = new SpatialOctree<number>(v3(0, 0, 0), 100);
        expect(octree.itemCount).toBe(0);

        octree.insert(1, v3(-1, -1, -1), v3(1, 1, 1));
        expect(octree.itemCount).toBe(1);

        octree.insert(2, v3(10, 10, 10), v3(11, 11, 11));
        expect(octree.itemCount).toBe(2);

        octree.remove(1);
        expect(octree.itemCount).toBe(1);
    });

    it('handles multiple items in overlapping regions', () => {
        const octree = new SpatialOctree<number>(v3(0, 0, 0), 100);
        octree.insert(1, v3(-1, -1, -1), v3(1, 1, 1));
        octree.insert(2, v3(-0.5, -0.5, -0.5), v3(0.5, 0.5, 0.5));

        const res = octree.query(v3(-2, -2, -2), v3(2, 2, 2));
        expect(res).toContain(1);
        expect(res).toContain(2);
    });
});
