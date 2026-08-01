import { describe, expect, it } from 'vitest';
import { SOAParticleBuffer } from '../../core/particle-buffer';
import type { ParticleId } from '../../types';

describe('SOAParticleBuffer', () => {
    describe('lifecycle', () => {
        it('allocate() initializes all arrays and sets capacity', () => {
            const buf = new SOAParticleBuffer();
            expect(buf.allocate(100)).toBe(true);
            expect(buf.capacity).toBe(100);
            expect(buf.allocated).toBe(true);
            expect(buf.count).toBe(0);
        });

        it('double-allocate deallocates first', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(50);
            buf.addParticle({ x: 1, y: 2, z: 3 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xff00ff00);
            expect(buf.count).toBe(1);

            buf.allocate(200);
            expect(buf.capacity).toBe(200);
            expect(buf.count).toBe(0);
        });

        it('deallocate() resets all state', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(100);
            buf.addParticle({ x: 1, y: 2, z: 3 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff);
            buf.deallocate();
            expect(buf.allocated).toBe(false);
            expect(buf.capacity).toBe(0);
            expect(buf.count).toBe(0);
        });

        it('deallocate() is no-op when not allocated', () => {
            const buf = new SOAParticleBuffer();
            buf.deallocate(); // should not throw
            expect(buf.allocated).toBe(false);
        });
    });

    describe('resize', () => {
        it('grows buffer preserving existing data', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(4);
            buf.addParticle({ x: 10, y: 20, z: 30 }, { x: 1, y: 2, z: 3 }, 5, 0.5, 0xff0000ff)!;

            expect(buf.resize(8)).toBe(true);
            expect(buf.capacity).toBe(8);

            // After resize, raw array data is preserved at index 0
            expect(buf.positions[0]).toBe(10);
            expect(buf.positions[1]).toBe(20);
            expect(buf.positions[2]).toBe(30);
        });

        it('no-op when shrinking', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(10);
            expect(buf.resize(5)).toBe(true);
            expect(buf.capacity).toBe(10); // unchanged
        });

        it('allocates if not yet allocated', () => {
            const buf = new SOAParticleBuffer();
            expect(buf.resize(50)).toBe(true);
            expect(buf.allocated).toBe(true);
            expect(buf.capacity).toBe(50);
        });
    });

    describe('addParticle', () => {
        it('stores position, velocity, lifetime, size, color correctly', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(10);
            const id = buf.addParticle(
                { x: 1, y: 2, z: 3 },
                { x: 4, y: 5, z: 6 },
                10,
                2.0,
                0xff804020
            )!;

            const idx = buf.getParticleIndex(id);
            expect(idx).toBeGreaterThanOrEqual(0);
            expect(buf.getPosition(idx)).toEqual({ x: 1, y: 2, z: 3 });
            expect(buf.getVelocity(idx)).toEqual({ x: 4, y: 5, z: 6 });
            expect(buf.getLifetime(idx)).toBe(10);
            expect(buf.getAge(idx)).toBe(0);
            expect(buf.getSize(idx)).toBe(2.0);
        });

        it('returns incrementing ParticleId', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(10);
            const id1 = buf.addParticle({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff)!;
            const id2 = buf.addParticle({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff)!;
            expect(id2).toBeGreaterThan(id1);
        });

        it('auto-resizes when full', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(2);
            buf.addParticle({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff);
            buf.addParticle({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff);
            // Third particle should trigger auto-resize
            const id3 = buf.addParticle({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff);
            expect(id3).not.toBeNull();
            expect(buf.capacity).toBeGreaterThan(2);
        });

        it('throws when not allocated', () => {
            const buf = new SOAParticleBuffer();
            expect(() =>
                buf.addParticle({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff)
            ).toThrow();
        });
    });

    describe('removeParticle / killParticle', () => {
        it('removeParticle marks slot dead and decrements count', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(10);
            const id = buf.addParticle({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff)!;
            const idx = buf.getParticleIndex(id);
            expect(buf.removeParticle(idx)).toBe(true);
            expect(buf.count).toBe(0);
        });

        it('removeParticle returns false for invalid index', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(10);
            expect(buf.removeParticle(-1)).toBe(false);
            expect(buf.removeParticle(100)).toBe(false);
        });

        it('killParticle finds by ID and removes', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(10);
            const id = buf.addParticle({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff)!;
            expect(buf.killParticle(id)).toBe(true);
            expect(buf.count).toBe(0);
        });

        it('killParticle returns false for unknown ID', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(10);
            expect(buf.killParticle(999 as ParticleId)).toBe(false);
        });
    });

    describe('getParticleIndex / getParticleId', () => {
        it('correct mapping', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(10);
            const id = buf.addParticle({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff)!;
            const idx = buf.getParticleIndex(id);
            expect(idx).toBeGreaterThanOrEqual(0);
            expect(buf.getParticleId(idx)).toBe(id);
        });

        it('returns -1 for missing particleId', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(10);
            expect(buf.getParticleIndex(999 as ParticleId)).toBe(-1);
        });

        it('returns 0 for missing index', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(10);
            expect(buf.getParticleId(99)).toBe(0);
        });
    });

    describe('getters/setters round-trip', () => {
        it('position', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(10);
            const id = buf.addParticle({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff)!;
            const idx = buf.getParticleIndex(id);
            buf.setPosition(idx, { x: 7, y: 8, z: 9 });
            expect(buf.getPosition(idx)).toEqual({ x: 7, y: 8, z: 9 });
        });

        it('velocity', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(10);
            const id = buf.addParticle({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff)!;
            const idx = buf.getParticleIndex(id);
            buf.setVelocity(idx, { x: 11, y: 12, z: 13 });
            expect(buf.getVelocity(idx)).toEqual({ x: 11, y: 12, z: 13 });
        });

        it('lifetime and age', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(10);
            const id = buf.addParticle({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff)!;
            const idx = buf.getParticleIndex(id);
            buf.setLifetime(idx, 20);
            expect(buf.getLifetime(idx)).toBe(20);
            buf.setAge(idx, 3.5);
            expect(buf.getAge(idx)).toBe(3.5);
        });

        it('size', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(10);
            const id = buf.addParticle({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff)!;
            const idx = buf.getParticleIndex(id);
            buf.setSize(idx, 3.5);
            expect(buf.getSize(idx)).toBe(3.5);
        });

        it('color encoding round-trip', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(10);
            const id = buf.addParticle({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xff804020)!;
            const idx = buf.getParticleIndex(id);
            const color = buf.getColor(idx);
            // Bitwise ops in JS produce signed 32-bit integers
            expect(color).toBe((0xff804020 | 0));
        });

        it('setColor/getColor round-trip', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(10);
            const id = buf.addParticle({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff)!;
            const idx = buf.getParticleIndex(id);
            buf.setColor(idx, 0x80808080);
            expect(buf.getColor(idx)).toBe((0x80808080 | 0));
        });
    });

    describe('custom data', () => {
        it('getCustomData/setCustomData with valid slots', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(10);
            const id = buf.addParticle({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff)!;
            const idx = buf.getParticleIndex(id);
            const data = new Float32Array([1.0, 2.0, 3.0, 4.0]);
            buf.setCustomData(idx, 0, data);
            const result = buf.getCustomData(idx, 0);
            expect(result[0]).toBeCloseTo(1.0);
            expect(result[1]).toBeCloseTo(2.0);
            expect(result[2]).toBeCloseTo(3.0);
            expect(result[3]).toBeCloseTo(4.0);
        });

        it('throws for invalid slot index', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(10);
            const id = buf.addParticle({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff)!;
            const idx = buf.getParticleIndex(id);
            expect(() => buf.getCustomData(idx, -1)).toThrow();
            expect(() => buf.getCustomData(idx, 4)).toThrow();
            expect(() => buf.setCustomData(idx, -1, new Float32Array(4))).toThrow();
        });
    });

    describe('clear', () => {
        it('resets count and alive flags, keeps capacity', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(10);
            buf.addParticle({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff);
            buf.addParticle({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff);
            buf.clear();
            expect(buf.count).toBe(0);
            expect(buf.capacity).toBe(10);
        });
    });

    describe('compact', () => {
        it('defragments alive particles to front', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(10);
            buf.addParticle({ x: 1, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff)!;
            buf.addParticle({ x: 2, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff)!;
            buf.addParticle({ x: 3, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff)!;

            // Remove middle particle (index 1)
            buf.removeParticle(1);
            expect(buf.count).toBe(2);

            buf.compact();
            expect(buf.count).toBe(2);
            // Alive particles should be at indices 0 and 1
            expect(buf.alive[0]).toBe(1);
            expect(buf.alive[1]).toBe(1);
            expect(buf.alive[2]).toBe(0);
        });
    });

    describe('sort', () => {
        it('sorts alive particles by age (default)', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(10);
            const id1 = buf.addParticle({ x: 1, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff)!;
            const id2 = buf.addParticle({ x: 2, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff)!;
            const id3 = buf.addParticle({ x: 3, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff)!;

            // Set different ages
            buf.setAge(buf.getParticleIndex(id1), 3.0);
            buf.setAge(buf.getParticleIndex(id2), 1.0);
            buf.setAge(buf.getParticleIndex(id3), 2.0);

            buf.sort();

            // After sort by age: id2(1.0), id3(2.0), id1(3.0)
            expect(buf.getAge(0)).toBe(1.0);
            expect(buf.getAge(1)).toBe(2.0);
            expect(buf.getAge(2)).toBe(3.0);
        });

        it('sorts with custom comparator', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(10);
            const id1 = buf.addParticle({ x: 1, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff)!;
            const id2 = buf.addParticle({ x: 2, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff)!;

            buf.setAge(buf.getParticleIndex(id1), 1.0);
            buf.setAge(buf.getParticleIndex(id2), 3.0);

            // Sort descending by age
            buf.sort((a, b) => buf.ages[b] - buf.ages[a]);
            expect(buf.getAge(0)).toBe(3.0);
            expect(buf.getAge(1)).toBe(1.0);
        });
    });

    describe('readonly accessors', () => {
        it('positions returns correct typed array view', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(10);
            buf.addParticle({ x: 5, y: 6, z: 7 }, { x: 0, y: 0, z: 0 }, 5, 1, 0xffffffff);
            const positions = buf.positions;
            expect(positions[0]).toBe(5);
            expect(positions[1]).toBe(6);
            expect(positions[2]).toBe(7);
        });

        it('velocities returns correct typed array view', () => {
            const buf = new SOAParticleBuffer();
            buf.allocate(10);
            buf.addParticle({ x: 0, y: 0, z: 0 }, { x: 8, y: 9, z: 10 }, 5, 1, 0xffffffff);
            const velocities = buf.velocities;
            expect(velocities[0]).toBe(8);
            expect(velocities[1]).toBe(9);
            expect(velocities[2]).toBe(10);
        });
    });
});
