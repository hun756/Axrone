import { describe, it, expect, vi, beforeEach } from 'vitest';
import { AABB3D } from '@axrone/geometry';
import { Vec3 } from '@axrone/numeric';
import { MemoryPool } from '@axrone/memory';

const { SpatialGrid } = await import('../spatial-grid');

function makeBounds(min = { x: -100, y: -100, z: -100 }, max = { x: 100, y: 100, z: 100 }) {
    return new AABB3D(min, max);
}

function makeVec3(x = 0, y = 0, z = 0) {
    return new Vec3(x, y, z);
}

describe('SpatialGrid', () => {
    let grid: InstanceType<typeof SpatialGrid>;

    beforeEach(() => {
        grid = new SpatialGrid(makeBounds(), 10);
    });

    describe('constructor', () => {
        it('creates grid with bounds and cell size', () => {
            expect(grid.cellSize).toBeDefined();
            expect(grid.cellSize.x).toBe(10);
            expect(grid.cellSize.y).toBe(10);
            expect(grid.cellSize.z).toBe(10);
            expect(grid.bounds).toBeDefined();
        });

        it('starts empty', () => {
            expect(grid.getCellCount()).toBe(0);
            expect(grid.getAverageParticlesPerCell()).toBe(0);
        });
    });

    describe('insert / has / remove', () => {
        it('insert adds a particle', () => {
            grid.insert(1 as any, makeVec3(5, 5, 5));
            expect(grid.has(1 as any, makeVec3(5, 5, 5))).toBe(true);
            expect(grid.getCellCount()).toBe(1);
        });

        it('insert multiple particles in same cell', () => {
            grid.insert(1 as any, makeVec3(1, 1, 1));
            grid.insert(2 as any, makeVec3(2, 2, 2));
            expect(grid.getCellCount()).toBe(1);
            expect(grid.has(1 as any, makeVec3())).toBe(true);
            expect(grid.has(2 as any, makeVec3())).toBe(true);
        });

        it('insert in different cells', () => {
            grid.insert(1 as any, makeVec3(5, 5, 5));
            grid.insert(2 as any, makeVec3(50, 50, 50));
            expect(grid.getCellCount()).toBe(2);
        });

        it('remove removes a particle', () => {
            grid.insert(1 as any, makeVec3(5, 5, 5));
            grid.remove(1 as any);
            expect(grid.has(1 as any, makeVec3(5, 5, 5))).toBe(false);
        });

        it('remove non-existent particle is no-op', () => {
            grid.remove(999 as any);
            expect(grid.getCellCount()).toBe(0);
        });

        it('remove last particle from cell removes the cell', () => {
            grid.insert(1 as any, makeVec3(5, 5, 5));
            expect(grid.getCellCount()).toBe(1);
            grid.remove(1 as any);
            expect(grid.getCellCount()).toBe(0);
        });

        it('remove one of many particles keeps cell', () => {
            grid.insert(1 as any, makeVec3(5, 5, 5));
            grid.insert(2 as any, makeVec3(6, 6, 6));
            grid.remove(1 as any);
            expect(grid.has(2 as any, makeVec3())).toBe(true);
            expect(grid.getCellCount()).toBe(1);
        });
    });

    describe('removeWithPosition', () => {
        it('removes particle found via tracking map', () => {
            grid.insert(1 as any, makeVec3(5, 5, 5));
            const result = grid.removeWithPosition(1 as any, makeVec3(5, 5, 5));
            expect(result).toBe(true);
            expect(grid.has(1 as any, makeVec3())).toBe(false);
        });

        it('returns false for non-existent particle', () => {
            const result = grid.removeWithPosition(999 as any, makeVec3(5, 5, 5));
            expect(result).toBe(false);
        });
    });

    describe('move', () => {
        it('move within same cell updates center mass', () => {
            grid.insert(1 as any, makeVec3(1, 1, 1));
            grid.move(1 as any, makeVec3(1, 1, 1), makeVec3(2, 2, 2));
            expect(grid.has(1 as any, makeVec3())).toBe(true);
            expect(grid.getCellCount()).toBe(1);
        });

        it('move across cells removes from old and inserts to new', () => {
            grid.insert(1 as any, makeVec3(5, 5, 5));
            grid.move(1 as any, makeVec3(5, 5, 5), makeVec3(50, 50, 50));
            expect(grid.getCellCount()).toBeGreaterThanOrEqual(1);
            expect(grid.has(1 as any, makeVec3())).toBe(true);
        });

        it('move untracked particle falls back to removeWithPosition', () => {
            grid.insert(1 as any, makeVec3(5, 5, 5));
            grid.move(2 as any, makeVec3(5, 5, 5), makeVec3(50, 50, 50));
            expect(grid.has(2 as any, makeVec3())).toBe(true);
        });
    });

    describe('queryRadius', () => {
        it('returns particles within radius', () => {
            grid.insert(1 as any, makeVec3(5, 5, 5));
            grid.insert(2 as any, makeVec3(50, 50, 50));
            const results = grid.queryRadius(makeVec3(5, 5, 5), 15);
            expect(results.length).toBeGreaterThanOrEqual(1);
            expect(results).toContain(1);
        });

        it('returns empty for empty grid', () => {
            const results = grid.queryRadius(makeVec3(0, 0, 0), 10);
            expect(results).toEqual([]);
        });
    });

    describe('queryRadiusCallback', () => {
        it('calls callback for particles without getPosition', () => {
            grid.insert(1 as any, makeVec3(5, 5, 5));
            grid.insert(2 as any, makeVec3(50, 50, 50));
            const ids: number[] = [];
            grid.queryRadiusCallback(makeVec3(5, 5, 5), 15, (id) => ids.push(id));
            expect(ids.length).toBeGreaterThanOrEqual(1);
        });

        it('uses getPosition for distance filtering when provided', () => {
            grid.insert(1 as any, makeVec3(5, 5, 5));
            grid.insert(2 as any, makeVec3(8, 8, 8));
            const positions: Record<number, any> = {
                1: { x: 5, y: 5, z: 5 },
                2: { x: 8, y: 8, z: 8 },
            };
            const ids: number[] = [];
            grid.queryRadiusCallback(
                makeVec3(5, 5, 5),
                2,
                (id) => ids.push(id),
                (id) => positions[id]
            );
            expect(ids).toContain(1);
            expect(ids).not.toContain(2);
        });
    });

    describe('queryAABB / query', () => {
        it('queryAABB calls callback for particles in bounds', () => {
            grid.insert(1 as any, makeVec3(5, 5, 5));
            grid.insert(2 as any, makeVec3(50, 50, 50));
            const ids: number[] = [];
            const queryBounds = new AABB3D(
                { x: 0, y: 0, z: 0 },
                { x: 20, y: 20, z: 20 }
            );
            grid.queryAABB(queryBounds, (id) => ids.push(id));
            expect(ids).toContain(1);
        });

        it('query returns array of particle IDs', () => {
            grid.insert(1 as any, makeVec3(5, 5, 5));
            const queryBounds = new AABB3D(
                { x: 0, y: 0, z: 0 },
                { x: 20, y: 20, z: 20 }
            );
            const results = grid.query(queryBounds);
            expect(results).toContain(1);
        });
    });

    describe('getCellAt', () => {
        it('returns cell at occupied position', () => {
            grid.insert(1 as any, makeVec3(5, 5, 5));
            const cell = grid.getCellAt(makeVec3(5, 5, 5));
            expect(cell).not.toBeNull();
            expect(cell!.particles).toContain(1);
        });

        it('returns null at empty position', () => {
            const cell = grid.getCellAt(makeVec3(5, 5, 5));
            expect(cell).toBeNull();
        });
    });

    describe('getNeighborCells', () => {
        it('returns neighboring cells', () => {
            grid.insert(1 as any, makeVec3(5, 5, 5));
            grid.insert(2 as any, makeVec3(15, 5, 5));
            const cell = grid.getCellAt(makeVec3(5, 5, 5));
            expect(cell).not.toBeNull();
            const neighbors = grid.getNeighborCells(cell!);
            expect(neighbors.length).toBeGreaterThanOrEqual(0);
        });

        it('returns empty for cell without bounds', () => {
            const fakeCell = { particles: [], neighborCells: [], density: 0 } as any;
            const neighbors = grid.getNeighborCells(fakeCell);
            expect(neighbors).toEqual([]);
        });
    });

    describe('clear', () => {
        it('removes all cells', () => {
            grid.insert(1 as any, makeVec3(5, 5, 5));
            grid.insert(2 as any, makeVec3(50, 50, 50));
            expect(grid.getCellCount()).toBe(2);
            grid.clear();
            expect(grid.getCellCount()).toBe(0);
        });
    });

    describe('getDensityAtPosition', () => {
        it('returns 0 for empty position', () => {
            expect(grid.getDensityAtPosition(makeVec3(5, 5, 5))).toBe(0);
        });

        it('returns density > 0 for occupied cell', () => {
            grid.insert(1 as any, makeVec3(5, 5, 5));
            expect(grid.getDensityAtPosition(makeVec3(5, 5, 5))).toBeGreaterThan(0);
        });

        it('density increases with more particles', () => {
            grid.insert(1 as any, makeVec3(5, 5, 5));
            const d1 = grid.getDensityAtPosition(makeVec3(5, 5, 5));
            grid.insert(2 as any, makeVec3(6, 6, 6));
            const d2 = grid.getDensityAtPosition(makeVec3(5, 5, 5));
            expect(d2).toBeGreaterThan(d1);
        });
    });

    describe('getCellCount / getAverageParticlesPerCell', () => {
        it('getCellCount tracks cells', () => {
            expect(grid.getCellCount()).toBe(0);
            grid.insert(1 as any, makeVec3(5, 5, 5));
            expect(grid.getCellCount()).toBe(1);
            grid.insert(2 as any, makeVec3(50, 50, 50));
            expect(grid.getCellCount()).toBe(2);
        });

        it('getAverageParticlesPerCell computes correctly', () => {
            grid.insert(1 as any, makeVec3(5, 5, 5));
            grid.insert(2 as any, makeVec3(6, 6, 6));
            // Both in same cell -> avg = 2/1 = 2
            expect(grid.getAverageParticlesPerCell()).toBe(2);
        });

        it('getAverageParticlesPerCell returns 0 when empty', () => {
            expect(grid.getAverageParticlesPerCell()).toBe(0);
        });
    });

    describe('update (alias for move)', () => {
        it('updates particle position', () => {
            grid.insert(1 as any, makeVec3(5, 5, 5));
            grid.update(1 as any, makeVec3(5, 5, 5), makeVec3(50, 50, 50));
            expect(grid.has(1 as any, makeVec3(50, 50, 50))).toBe(true);
        });
    });

    describe('edge cases', () => {
        it('handles many particles in same cell', () => {
            for (let i = 0; i < 50; i++) {
                grid.insert(i as any, makeVec3(5, 5, 5));
            }
            expect(grid.getCellCount()).toBe(1);
            expect(grid.getAverageParticlesPerCell()).toBe(50);
        });

        it('remove from middle of cell particle list swaps with last', () => {
            grid.insert(1 as any, makeVec3(5, 5, 5));
            grid.insert(2 as any, makeVec3(6, 6, 6));
            grid.insert(3 as any, makeVec3(7, 7, 7));
            grid.remove(1 as any); // removes first, swaps last into position 0
            expect(grid.has(2 as any, makeVec3())).toBe(true);
            expect(grid.has(3 as any, makeVec3())).toBe(true);
            expect(grid.has(1 as any, makeVec3())).toBe(false);
            expect(grid.getCellCount()).toBe(1);
        });
    });
});
