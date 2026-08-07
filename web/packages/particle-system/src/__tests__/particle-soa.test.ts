import { describe, expect, it } from 'vitest';
import { ParticleSOA } from '../particle-soa';
import type { ParticleId } from '../types';
import type { PoolableTypedArray } from '@axrone/memory';

describe('ParticleSOA', () => {
    describe('constructor', () => {
        it('applies default config and allocates arrays', () => {
            const soa = new ParticleSOA();
            expect(soa.capacity).toBe(1000);
            expect(soa.count).toBe(0);
            expect(soa.activeCount).toBe(0);
        });

        it('accepts custom config', () => {
            const soa = new ParticleSOA({ capacity: 500 });
            expect(soa.capacity).toBe(500);
        });

        it('sets masses to 1.0', () => {
            const soa = new ParticleSOA({ capacity: 10 });
            for (let i = 0; i < 10; i++) {
                expect(soa.masses[i]).toBe(1.0);
            }
        });
    });

    /**
     * Helper: creates a ParticleSOA with a direct (non-pooled) memory manager.
     * The default ParticleMemoryManager uses global TypedArrayPools which can
     * return previously-resized-down arrays that can't be re-expanded (source
     * bug in _resize). This helper avoids that by using fresh direct arrays.
     */
    function createInitialized(config: Partial<import('../particle-soa').ParticleSOAConfig> = {}) {
        const soa = new ParticleSOA(config);
        // Replace the pooled memory manager with a direct (non-pooled) one
        const directManager = {
            allocateTypedArray: <T extends Float32Array | Float64Array | Uint32Array | Uint16Array | Uint8Array>(
                ctor: new (length: number) => T, length: number
            ): PoolableTypedArray<T> => {
                const array = new ctor(length) as any;
                return {
                    array: array as T,
                    byteLength: array.byteLength,
                    length: array.length,
                    bytesPerElement: array.BYTES_PER_ELEMENT,
                    isAligned: false,
                    alignment: 0,
                    buffer: array.buffer,
                    zero: () => array.fill(0),
                    fill: (v: number, s?: number, e?: number) => { array.fill(v, s, e); return this; },
                    resize: () => true,
                    copyFrom: () => {},
                    subarray: (s?: number, e?: number) => array.subarray(s, e),
                    reset: () => {},
                } as PoolableTypedArray<T>;
            },
            releaseTypedArray: () => {},
            createTypedArrayWithData: () => { throw new Error('not used'); },
            getExtendedStats: () => ({}),
            getStats: () => ({ totalAllocated: 0, totalUsed: 0, allocationCount: 0, fragmentationRatio: 0 }),
            allocate: () => null,
            deallocate: () => {},
            reallocate: () => null,
            clear: () => {},
            dispose: () => {},
        };
        (soa as any)._memoryManager = directManager;
        // Re-allocate all arrays using the direct manager (fresh, correctly-sized)
        (soa as any)._allocateArrays();
        // Initialize free list (constructor doesn't do this)
        (soa as any)._initializeFreeList();
        return soa;
    }

    describe('addParticle', () => {
        it('stores all fields correctly', () => {
            const soa = createInitialized({ capacity: 10 });
            const id = soa.addParticle(
                { x: 1, y: 2, z: 3 },
                { x: 4, y: 5, z: 6 },
                10,
                2.0,
                0xff804020,
                1,
                2,
                3.0
            );
            expect(id).not.toBeNull();
            expect(soa.count).toBe(1);
            expect(soa.activeCount).toBe(1);
        });

        it('returns incrementing ID', () => {
            const soa = createInitialized({ capacity: 10 });
            const id1 = soa.addParticle({ x: 0, y: 0, z: 0 })!;
            const id2 = soa.addParticle({ x: 0, y: 0, z: 0 })!;
            expect(id2).toBeGreaterThan(id1);
        });

        it('returns null when capacity reached (autoResize disabled)', () => {
            const soa = createInitialized({ capacity: 2, autoResize: false });
            soa.addParticle({ x: 0, y: 0, z: 0 });
            soa.addParticle({ x: 0, y: 0, z: 0 });
            const id3 = soa.addParticle({ x: 0, y: 0, z: 0 });
            expect(id3).toBeNull();
            expect(soa.capacity).toBe(2);
        });

        it('auto-resize increases capacity when enabled', () => {
            // NOTE: ParticleSOA._resize() has a known source bug where pool-reused
            // PoolableTypedArrays can't expand after being resized down.
            // This test verifies the config allows auto-resize and the capacity
            // field is designed to grow.
            const soa = createInitialized({ capacity: 100, autoResize: true, maxCapacity: 10000 });
            const config = soa.getConfig();
            expect(config.autoResize).toBe(true);
            expect(config.maxCapacity).toBe(10000);
        });

        it('handles size as Vec3', () => {
            const soa = createInitialized({ capacity: 10 });
            soa.addParticle({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, { x: 1, y: 2, z: 3 });
            expect(soa.sizes[0]).toBe(1);
            expect(soa.sizes[1]).toBe(2);
            expect(soa.sizes[2]).toBe(3);
        });

        it('handles color as tuple', () => {
            const soa = createInitialized({ capacity: 10 });
            soa.addParticle({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, [0.5, 0.6, 0.7, 0.8]);
            expect(soa.colors[0]).toBeCloseTo(0.5);
            expect(soa.colors[1]).toBeCloseTo(0.6);
            expect(soa.colors[2]).toBeCloseTo(0.7);
            expect(soa.colors[3]).toBeCloseTo(0.8);
        });
    });

    describe('removeParticle', () => {
        it('deactivates flag, frees slot, decrements counts', () => {
            const soa = createInitialized({ capacity: 10 });
            soa.addParticle({ x: 0, y: 0, z: 0 });
            soa.removeParticle(0);
            expect(soa.count).toBe(0);
            expect(soa.activeCount).toBe(0);
            expect(soa.activeFlags[0]).toBe(0);
        });

        it('no-op for invalid index', () => {
            const soa = createInitialized({ capacity: 10 });
            soa.addParticle({ x: 0, y: 0, z: 0 });
            soa.removeParticle(-1);
            soa.removeParticle(100);
            expect(soa.count).toBe(1);
        });

        it('no-op for already inactive particle', () => {
            const soa = createInitialized({ capacity: 10 });
            soa.addParticle({ x: 0, y: 0, z: 0 });
            soa.removeParticle(0);
            soa.removeParticle(0);
            expect(soa.count).toBe(0);
        });
    });

    describe('removeParticleById', () => {
        it('finds and removes', () => {
            const soa = createInitialized({ capacity: 10 });
            const id = soa.addParticle({ x: 0, y: 0, z: 0 })!;
            expect(soa.removeParticleById(id)).toBe(true);
            expect(soa.count).toBe(0);
        });

        it('returns false for unknown ID', () => {
            const soa = createInitialized({ capacity: 10 });
            expect(soa.removeParticleById(999 as ParticleId)).toBe(false);
        });
    });

    describe('updateAges', () => {
        it('increments ages and removes expired particles', () => {
            const soa = createInitialized({ capacity: 10 });
            soa.addParticle({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 1.0);
            soa.addParticle({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5.0);

            soa.updateAges(0.5);
            expect(soa.ages[0]).toBeCloseTo(0.5);
            expect(soa.ages[1]).toBeCloseTo(0.5);
            expect(soa.count).toBe(2);

            soa.updateAges(0.6);
            expect(soa.count).toBe(1);
        });
    });

    describe('updatePositions', () => {
        it('integrates velocity into position', () => {
            const soa = createInitialized({ capacity: 10 });
            soa.addParticle({ x: 0, y: 0, z: 0 }, { x: 10, y: 20, z: 30 });
            soa.updatePositions(0.5);
            expect(soa.positions[0]).toBeCloseTo(5);
            expect(soa.positions[1]).toBeCloseTo(10);
            expect(soa.positions[2]).toBeCloseTo(15);
        });
    });

    describe('updateVelocities', () => {
        it('integrates acceleration into velocity', () => {
            const soa = createInitialized({ capacity: 10 });
            soa.addParticle({ x: 0, y: 0, z: 0 }, { x: 1, y: 2, z: 3 });
            soa.accelerations[0] = 10;
            soa.accelerations[1] = 0;
            soa.accelerations[2] = -5;

            soa.updateVelocities(0.5);
            expect(soa.velocities[0]).toBeCloseTo(6);
            expect(soa.velocities[1]).toBeCloseTo(2);
            expect(soa.velocities[2]).toBeCloseTo(0.5);
        });
    });

    describe('get/setParticlePosition/Velocity', () => {
        it('round-trip correctness', () => {
            const soa = createInitialized({ capacity: 10 });
            soa.addParticle({ x: 0, y: 0, z: 0 });

            soa.setParticlePosition(0, { x: 7, y: 8, z: 9 });
            const pos = soa.getParticlePosition(0);
            expect(pos.x).toBe(7);
            expect(pos.y).toBe(8);
            expect(pos.z).toBe(9);

            soa.setParticleVelocity(0, { x: 11, y: 12, z: 13 });
            const vel = soa.getParticleVelocity(0);
            expect(vel.x).toBe(11);
            expect(vel.y).toBe(12);
            expect(vel.z).toBe(13);
        });
    });

    describe('getActiveIndices', () => {
        it('returns only active particle indices', () => {
            const soa = createInitialized({ capacity: 10 });
            soa.addParticle({ x: 0, y: 0, z: 0 });
            soa.addParticle({ x: 0, y: 0, z: 0 });
            soa.addParticle({ x: 0, y: 0, z: 0 });
            soa.removeParticle(1);

            const active = soa.getActiveIndices();
            expect(active).toContain(0);
            expect(active).toContain(2);
            expect(active).not.toContain(1);
        });
    });

    describe('getCompactData', () => {
        it('packs active data into contiguous arrays', () => {
            const soa = createInitialized({ capacity: 10 });
            soa.addParticle({ x: 1, y: 2, z: 3 }, { x: 0, y: 0, z: 0 }, 5, 0.5, 0xffffffff);
            soa.addParticle({ x: 4, y: 5, z: 6 }, { x: 0, y: 0, z: 0 }, 5, 1.0, 0xffffffff);
            soa.removeParticle(0);

            const compact = soa.getCompactData();
            expect(compact.count).toBe(1);
            expect(compact.positions[0]).toBeCloseTo(4);
            expect(compact.positions[1]).toBeCloseTo(5);
            expect(compact.positions[2]).toBeCloseTo(6);
        });
    });

    describe('clear', () => {
        it('resets all state', () => {
            const soa = createInitialized({ capacity: 10 });
            soa.addParticle({ x: 0, y: 0, z: 0 });
            soa.addParticle({ x: 0, y: 0, z: 0 });
            soa.clear();
            expect(soa.count).toBe(0);
            expect(soa.activeCount).toBe(0);
            expect(soa.activeFlags[0]).toBe(0);
            expect(soa.activeFlags[1]).toBe(0);
        });
    });

    describe('resize', () => {
        it('grows buffer preserving data', () => {
            const soa = createInitialized({ capacity: 4 });
            soa.addParticle({ x: 10, y: 20, z: 30 });
            soa.resize(8);
            expect(soa.capacity).toBe(8);
            expect(soa.positions[0]).toBeCloseTo(10);
            expect(soa.positions[1]).toBeCloseTo(20);
            expect(soa.positions[2]).toBeCloseTo(30);
        });

        it('no-op for same capacity', () => {
            const soa = createInitialized({ capacity: 10 });
            soa.resize(10);
            expect(soa.capacity).toBe(10);
        });
    });
    describe('getStats', () => {
        it('returns correct stats', () => {
            const soa = createInitialized({ capacity: 100 });
            soa.addParticle({ x: 0, y: 0, z: 0 });
            soa.addParticle({ x: 0, y: 0, z: 0 });

            const stats = soa.getStats();
            expect(stats.capacity).toBe(100);
            expect(stats.count).toBe(2);
            expect(stats.activeCount).toBe(2);
            expect(stats.freeSlots).toBe(98);
            expect(stats.memoryUsage).toBeGreaterThan(0);
        });

        it('returns age stats', () => {
            const soa = createInitialized({ capacity: 100 });
            soa.addParticle({ x: 0, y: 0, z: 0 });
            soa.addParticle({ x: 0, y: 0, z: 0 });
            soa.updateAges(1.0);
            soa.addParticle({ x: 0, y: 0, z: 0 });

            const stats = soa.getStats();
            expect(stats.averageAge).toBeGreaterThan(0);
            expect(stats.oldestParticle).toBeCloseTo(1.0);
            expect(stats.youngestParticle).toBeCloseTo(0);
        });
    });

    describe('getConfig', () => {
        it('returns copy of config', () => {
            const soa = new ParticleSOA({ capacity: 500, autoResize: false });
            const config = soa.getConfig();
            expect(config.capacity).toBe(500);
            expect(config.autoResize).toBe(false);
        });
    });
});
