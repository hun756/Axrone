import { describe, it, expect } from 'vitest';
import { SpatialHashBroadphase3D, OctreeBroadphase3D } from '../index';
import type { IBroadphaseItem3D } from '../index';
import type { IVec3Like } from '@axrone/numeric';

const v3 = (x: number, y: number, z: number): IVec3Like => ({ x, y, z });

interface TestItem extends IBroadphaseItem3D {
    id: number;
    label: string;
}

const item = (id: number): TestItem => ({ id, label: `item_${id}` });

describe('SpatialHashBroadphase3D', () => {
    it('insert + queryAABB returns the item', () => {
        const bp = new SpatialHashBroadphase3D<TestItem>(10);
        const a = item(1);
        bp.insert(a, v3(0, 0, 0), v3(2, 2, 2));
        const result = bp.queryAABB(v3(0, 0, 0), v3(3, 3, 3));
        expect(result).toContain(a);
    });

    it('queryAABB does not return distant items', () => {
        const bp = new SpatialHashBroadphase3D<TestItem>(10);
        const a = item(1);
        bp.insert(a, v3(0, 0, 0), v3(2, 2, 2));
        const result = bp.queryAABB(v3(100, 100, 100), v3(102, 102, 102));
        expect(result).not.toContain(a);
    });

    it('update moves item to new location', () => {
        const bp = new SpatialHashBroadphase3D<TestItem>(10);
        const a = item(1);
        bp.insert(a, v3(0, 0, 0), v3(2, 2, 2));
        bp.update(a, v3(50, 50, 50), v3(52, 52, 52));
        expect(bp.queryAABB(v3(0, 0, 0), v3(3, 3, 3))).not.toContain(a);
        expect(bp.queryAABB(v3(50, 50, 50), v3(53, 53, 53))).toContain(a);
    });

    it('remove hides the item', () => {
        const bp = new SpatialHashBroadphase3D<TestItem>(10);
        const a = item(1);
        bp.insert(a, v3(0, 0, 0), v3(2, 2, 2));
        bp.remove(a);
        expect(bp.queryAABB(v3(0, 0, 0), v3(3, 3, 3))).not.toContain(a);
    });

    it('queryPairs finds overlapping pairs without duplicates', () => {
        const bp = new SpatialHashBroadphase3D<TestItem>(10);
        const a = item(1);
        const b = item(2);
        bp.insert(a, v3(0, 0, 0), v3(2, 2, 2));
        bp.insert(b, v3(1, 1, 1), v3(3, 3, 3));
        const pairs = bp.queryPairs();
        expect(pairs.length).toBeGreaterThanOrEqual(1);
        const hasAB = pairs.some(
            (p) => (p.itemA === a && p.itemB === b) || (p.itemA === b && p.itemB === a)
        );
        expect(hasAB).toBe(true);
    });

    it('queryPairs does not include self-pairs', () => {
        const bp = new SpatialHashBroadphase3D<TestItem>(10);
        const a = item(1);
        bp.insert(a, v3(0, 0, 0), v3(2, 2, 2));
        const pairs = bp.queryPairs();
        for (const p of pairs) {
            expect(p.itemA).not.toBe(p.itemB);
        }
    });

    it('queryRay returns items along the ray', () => {
        const bp = new SpatialHashBroadphase3D<TestItem>(10);
        const a = item(1);
        bp.insert(a, v3(4, -1, -1), v3(6, 1, 1));
        const result = bp.queryRay(v3(0, 0, 0), v3(1, 0, 0), 100);
        expect(result).toContain(a);
    });

    it('clear empties the structure', () => {
        const bp = new SpatialHashBroadphase3D<TestItem>(10);
        bp.insert(item(1), v3(0, 0, 0), v3(2, 2, 2));
        bp.clear();
        expect(bp.itemCount).toBe(0);
    });

    it('itemCount tracks insertions and removals', () => {
        const bp = new SpatialHashBroadphase3D<TestItem>(10);
        const a = item(1);
        const b = item(2);
        bp.insert(a, v3(0, 0, 0), v3(1, 1, 1));
        bp.insert(b, v3(5, 5, 5), v3(6, 6, 6));
        expect(bp.itemCount).toBe(2);
        bp.remove(a);
        expect(bp.itemCount).toBe(1);
    });
});

describe('OctreeBroadphase3D', () => {
    it('insert + queryAABB returns the item', () => {
        const bp = new OctreeBroadphase3D<TestItem>(v3(50, 50, 50), 100);
        const a = item(1);
        bp.insert(a, v3(0, 0, 0), v3(2, 2, 2));
        const result = bp.queryAABB(v3(0, 0, 0), v3(3, 3, 3));
        expect(result).toContain(a);
    });

    it('queryAABB does not return distant items', () => {
        const bp = new OctreeBroadphase3D<TestItem>(v3(50, 50, 50), 100);
        const a = item(1);
        bp.insert(a, v3(0, 0, 0), v3(2, 2, 2));
        const result = bp.queryAABB(v3(80, 80, 80), v3(82, 82, 82));
        expect(result).not.toContain(a);
    });

    it('update moves item', () => {
        const bp = new OctreeBroadphase3D<TestItem>(v3(50, 50, 50), 100);
        const a = item(1);
        bp.insert(a, v3(0, 0, 0), v3(2, 2, 2));
        bp.update(a, v3(40, 40, 40), v3(42, 42, 42));
        expect(bp.queryAABB(v3(0, 0, 0), v3(3, 3, 3))).not.toContain(a);
        expect(bp.queryAABB(v3(40, 40, 40), v3(43, 43, 43))).toContain(a);
    });

    it('remove hides the item', () => {
        const bp = new OctreeBroadphase3D<TestItem>(v3(50, 50, 50), 100);
        const a = item(1);
        bp.insert(a, v3(10, 10, 10), v3(12, 12, 12));
        bp.remove(a);
        expect(bp.queryAABB(v3(10, 10, 10), v3(13, 13, 13))).not.toContain(a);
    });

    it('queryPairs finds overlapping pairs', () => {
        const bp = new OctreeBroadphase3D<TestItem>(v3(5, 5, 5), 10);
        const a = item(1);
        const b = item(2);
        bp.insert(a, v3(0, 0, 0), v3(3, 3, 3));
        bp.insert(b, v3(1, 1, 1), v3(4, 4, 4));
        const pairs = bp.queryPairs();
        const hasAB = pairs.some(
            (p) => (p.itemA === a && p.itemB === b) || (p.itemA === b && p.itemB === a)
        );
        expect(hasAB).toBe(true);
    });

    it('queryRay returns items along ray', () => {
        const bp = new OctreeBroadphase3D<TestItem>(v3(50, 50, 50), 100);
        const a = item(1);
        bp.insert(a, v3(4, -1, -1), v3(6, 1, 1));
        const result = bp.queryRay(v3(0, 0, 0), v3(1, 0, 0), 100);
        expect(result).toContain(a);
    });

    it('clear empties structure', () => {
        const bp = new OctreeBroadphase3D<TestItem>(v3(50, 50, 50), 100);
        bp.insert(item(1), v3(10, 10, 10), v3(12, 12, 12));
        bp.clear();
        expect(bp.itemCount).toBe(0);
    });

    it('itemCount tracks correctly', () => {
        const bp = new OctreeBroadphase3D<TestItem>(v3(50, 50, 50), 100);
        const a = item(1);
        const b = item(2);
        bp.insert(a, v3(10, 10, 10), v3(12, 12, 12));
        bp.insert(b, v3(20, 20, 20), v3(22, 22, 22));
        expect(bp.itemCount).toBe(2);
        bp.remove(a);
        expect(bp.itemCount).toBe(1);
    });
});
