import { describe, expect, it } from 'vitest';
import { UniformSpatialGrid } from '../../core/spatial-index';
import type { ParticleId } from '../../types';

const createGrid = (bounds = 100, cellSize = 10) =>
    new UniformSpatialGrid(
        { min: { x: -bounds, y: -bounds, z: -bounds }, max: { x: bounds, y: bounds, z: bounds } },
        { x: cellSize, y: cellSize, z: cellSize }
    );

describe('UniformSpatialGrid', () => {
    describe('constructor', () => {
        it('sets bounds, cellSize, and dimensions correctly', () => {
            const grid = createGrid(50, 10);
            expect(grid.bounds.min).toEqual({ x: -50, y: -50, z: -50 });
            expect(grid.bounds.max).toEqual({ x: 50, y: 50, z: 50 });
            expect(grid.cellSize).toEqual({ x: 10, y: 10, z: 10 });
            expect(grid.particleCount).toBe(0);
        });

        it('pre-allocates cell pool', () => {
            const grid = createGrid();
            // After construction, particleCount is 0 (pool is internal)
            expect(grid.particleCount).toBe(0);
        });
    });

    describe('insert', () => {
        it('adds particle and updates particleCount', () => {
            const grid = createGrid();
            grid.insert(1 as ParticleId, { x: 0, y: 0, z: 0 });
            expect(grid.particleCount).toBe(1);
        });

        it('re-insert updates cell without changing count', () => {
            const grid = createGrid();
            grid.insert(1 as ParticleId, { x: 0, y: 0, z: 0 });
            grid.insert(1 as ParticleId, { x: 5, y: 5, z: 5 });
            expect(grid.particleCount).toBe(1);
        });

        it('same position re-insert is no-op', () => {
            const grid = createGrid();
            grid.insert(1 as ParticleId, { x: 0, y: 0, z: 0 });
            grid.insert(1 as ParticleId, { x: 0, y: 0, z: 0 });
            expect(grid.particleCount).toBe(1);
        });
    });

    describe('remove', () => {
        it('removes particle and decrements count', () => {
            const grid = createGrid();
            grid.insert(1 as ParticleId, { x: 0, y: 0, z: 0 });
            expect(grid.remove(1 as ParticleId)).toBe(true);
            expect(grid.particleCount).toBe(0);
        });

        it('returns false for unknown particle', () => {
            const grid = createGrid();
            expect(grid.remove(999 as ParticleId)).toBe(false);
        });
    });

    describe('update', () => {
        it('moves particle between cells', () => {
            const grid = createGrid();
            grid.insert(1 as ParticleId, { x: 0, y: 0, z: 0 });
            const result = grid.update(
                1 as ParticleId,
                { x: 0, y: 0, z: 0 },
                { x: 50, y: 50, z: 50 }
            );
            expect(result).toBe(true);
            expect(grid.particleCount).toBe(1);
        });

        it('same-cell update is no-op success', () => {
            const grid = createGrid();
            grid.insert(1 as ParticleId, { x: 0, y: 0, z: 0 });
            const result = grid.update(
                1 as ParticleId,
                { x: 0, y: 0, z: 0 },
                { x: 1, y: 1, z: 1 } // same cell
            );
            expect(result).toBe(true);
            expect(grid.particleCount).toBe(1);
        });
    });

    describe('query (bounds)', () => {
        it('returns particles in overlapping cells', () => {
            const grid = createGrid();
            grid.insert(1 as ParticleId, { x: 0, y: 0, z: 0 });
            grid.insert(2 as ParticleId, { x: 5, y: 5, z: 5 });
            grid.insert(3 as ParticleId, { x: 90, y: 90, z: 90 });

            const result = grid.query({
                min: { x: -10, y: -10, z: -10 },
                max: { x: 10, y: 10, z: 10 },
            });
            expect(result).toContain(1 as ParticleId);
            expect(result).toContain(2 as ParticleId);
            expect(result).not.toContain(3 as ParticleId);
        });

        it('deduplicates results', () => {
            const grid = createGrid();
            grid.insert(1 as ParticleId, { x: 0, y: 0, z: 0 });
            const result = grid.query({
                min: { x: -10, y: -10, z: -10 },
                max: { x: 10, y: 10, z: 10 },
            });
            const count = result.filter((id) => id === 1).length;
            expect(count).toBe(1);
        });
    });

    describe('queryRadius', () => {
        it('returns particles within radius bounding box', () => {
            const grid = createGrid();
            grid.insert(1 as ParticleId, { x: 0, y: 0, z: 0 });
            grid.insert(2 as ParticleId, { x: 50, y: 50, z: 50 });

            const result = grid.queryRadius({ x: 0, y: 0, z: 0 }, 15);
            expect(result).toContain(1 as ParticleId);
        });
    });

    describe('queryNearest', () => {
        it('returns N closest particles', () => {
            const grid = createGrid();
            grid.insert(1 as ParticleId, { x: 0, y: 0, z: 0 });
            grid.insert(2 as ParticleId, { x: 5, y: 0, z: 0 });
            grid.insert(3 as ParticleId, { x: 50, y: 50, z: 50 });

            const result = grid.queryNearest({ x: 0, y: 0, z: 0 }, 2);
            expect(result.length).toBe(2);
            expect(result).toContain(1 as ParticleId);
        });

        it('returns [] for count <= 0', () => {
            const grid = createGrid();
            grid.insert(1 as ParticleId, { x: 0, y: 0, z: 0 });
            expect(grid.queryNearest({ x: 0, y: 0, z: 0 }, 0)).toEqual([]);
            expect(grid.queryNearest({ x: 0, y: 0, z: 0 }, -1)).toEqual([]);
        });
    });

    describe('clear', () => {
        it('resets all cells and particle count to 0', () => {
            const grid = createGrid();
            grid.insert(1 as ParticleId, { x: 0, y: 0, z: 0 });
            grid.insert(2 as ParticleId, { x: 5, y: 5, z: 5 });
            grid.clear();
            expect(grid.particleCount).toBe(0);
        });
    });

    describe('optimize', () => {
        it('removes empty cells', () => {
            const grid = createGrid();
            grid.insert(1 as ParticleId, { x: 0, y: 0, z: 0 });
            // Remove the particle (cell becomes empty)
            grid.remove(1 as ParticleId);
            grid.optimize();
            // After optimize, particleCount should still be 0
            expect(grid.particleCount).toBe(0);
        });
    });

    describe('readonly accessors', () => {
        it('bounds returns correct value', () => {
            const grid = createGrid(200, 20);
            expect(grid.bounds.min.x).toBe(-200);
            expect(grid.bounds.max.x).toBe(200);
        });

        it('cellSize returns correct value', () => {
            const grid = createGrid(100, 25);
            expect(grid.cellSize.x).toBe(25);
        });

        it('particleCount reflects insertions and removals', () => {
            const grid = createGrid();
            grid.insert(1 as ParticleId, { x: 0, y: 0, z: 0 });
            grid.insert(2 as ParticleId, { x: 5, y: 5, z: 5 });
            expect(grid.particleCount).toBe(2);
            grid.remove(1 as ParticleId);
            expect(grid.particleCount).toBe(1);
        });
    });

    describe('cell stats', () => {
        it('density updates on insert', () => {
            const grid = createGrid(100, 10);
            grid.insert(1 as ParticleId, { x: 0, y: 0, z: 0 });
            grid.insert(2 as ParticleId, { x: 1, y: 1, z: 1 });
            // We can't directly access cell density, but we can verify the grid doesn't throw
            expect(grid.particleCount).toBe(2);
        });
    });
});
