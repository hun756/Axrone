import { describe, it, expect } from 'vitest';
import { SpatialHashGrid3D } from '../index';
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
