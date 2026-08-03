import { describe, expect, it } from 'vitest';
import { UniformCache } from '../uniform-cache';

describe('UniformCache', () => {
    describe('record', () => {
        it('returns true for a new entry (miss)', () => {
            const cache = new UniformCache();
            expect(cache.record('u_Model', 'FLOAT_MAT4', 0, null)).toBe(true);
        });

        it('returns false when same value is recorded again (hit)', () => {
            const cache = new UniformCache();
            cache.record('u_Model', 'FLOAT_MAT4', 0, 42);
            expect(cache.record('u_Model', 'FLOAT_MAT4', 0, 42)).toBe(false);
        });

        it('returns true when value changes (miss)', () => {
            const cache = new UniformCache();
            cache.record('u_Color', 'FLOAT_VEC4', 0, new Float32Array([1, 0, 0, 1]));
            expect(cache.record('u_Color', 'FLOAT_VEC4', 0, new Float32Array([0, 1, 0, 1]))).toBe(true);
        });

        it('returns true when type changes for same name', () => {
            const cache = new UniformCache();
            cache.record('u_Val', 'FLOAT', 0, 1.0);
            expect(cache.record('u_Val', 'INT', 0, 1)).toBe(true);
        });
    });

    describe('hash correctness for different value types', () => {
        it('distinguishes number values', () => {
            const cache = new UniformCache();
            cache.record('u', 'FLOAT', 0, 1.0);
            expect(cache.record('u', 'FLOAT', 0, 2.0)).toBe(true);
        });

        it('distinguishes boolean values', () => {
            const cache = new UniformCache();
            cache.record('u', 'BOOL', 0, true);
            expect(cache.record('u', 'BOOL', 0, false)).toBe(true);
        });

        it('treats null as a valid value', () => {
            const cache = new UniformCache();
            cache.record('u', 'SAMPLER_2D', 0, null);
            expect(cache.record('u', 'SAMPLER_2D', 0, null)).toBe(false);
        });

        it('distinguishes Float32Array values', () => {
            const cache = new UniformCache();
            cache.record('u', 'FLOAT_VEC3', 0, new Float32Array([1, 2, 3]));
            expect(cache.record('u', 'FLOAT_VEC3', 0, new Float32Array([1, 2, 4]))).toBe(true);
            // After recording [1,2,4], recording [1,2,4] again is a hit (returns false)
            expect(cache.record('u', 'FLOAT_VEC3', 0, new Float32Array([1, 2, 4]))).toBe(false);
        });

        it('distinguishes Int32Array values', () => {
            const cache = new UniformCache();
            cache.record('u', 'INT_VEC2', 0, new Int32Array([1, 2]));
            expect(cache.record('u', 'INT_VEC2', 0, new Int32Array([1, 3]))).toBe(true);
        });

        it('distinguishes Uint32Array values', () => {
            const cache = new UniformCache();
            cache.record('u', 'UNSIGNED_INT', 0, new Uint32Array([10]));
            expect(cache.record('u', 'UNSIGNED_INT', 0, new Uint32Array([20]))).toBe(true);
        });

        it('distinguishes plain array values', () => {
            const cache = new UniformCache();
            cache.record('u', 'FLOAT_VEC3', 0, [1, 2, 3]);
            expect(cache.record('u', 'FLOAT_VEC3', 0, [1, 2, 4])).toBe(true);
        });
    });

    describe('eviction', () => {
        it('evicts oldest entry when maxEntries is reached', () => {
            const cache = new UniformCache(3);
            cache.record('a', 'FLOAT', 0, 1);
            cache.record('b', 'FLOAT', 0, 2);
            cache.record('c', 'FLOAT', 0, 3);
            cache.record('d', 'FLOAT', 0, 4);
            expect(cache.size()).toBe(3);
            expect(cache.has('a')).toBe(false);
            expect(cache.has('d')).toBe(true);
        });
    });

    describe('lookup / has', () => {
        it('lookup returns the last recorded value', () => {
            const cache = new UniformCache();
            cache.record('u', 'FLOAT', 0, 42);
            expect(cache.lookup('u')).toBe(42);
        });

        it('lookup returns undefined for unknown name', () => {
            const cache = new UniformCache();
            expect(cache.lookup('nope')).toBeUndefined();
        });

        it('has returns true/false correctly', () => {
            const cache = new UniformCache();
            cache.record('u', 'FLOAT', 0, 1);
            expect(cache.has('u')).toBe(true);
            expect(cache.has('v')).toBe(false);
        });
    });

    describe('invalidate / invalidateAll', () => {
        it('invalidate removes a single entry', () => {
            const cache = new UniformCache();
            cache.record('a', 'FLOAT', 0, 1);
            cache.record('b', 'FLOAT', 0, 2);
            cache.invalidate('a');
            expect(cache.has('a')).toBe(false);
            expect(cache.has('b')).toBe(true);
        });

        it('invalidateAll clears everything', () => {
            const cache = new UniformCache();
            cache.record('a', 'FLOAT', 0, 1);
            cache.record('b', 'FLOAT', 0, 2);
            cache.invalidateAll();
            expect(cache.size()).toBe(0);
        });
    });

    describe('getStats', () => {
        it('returns correct hit/miss stats', () => {
            const cache = new UniformCache();
            cache.record('a', 'FLOAT', 0, 1);
            cache.record('a', 'FLOAT', 0, 1);
            cache.record('b', 'FLOAT', 0, 2);

            const stats = cache.getStats();
            expect(stats.hits).toBe(1);
            expect(stats.misses).toBe(2);
            expect(stats.size).toBe(2);
            expect(stats.hitRate).toBeCloseTo(1 / 3);
        });

        it('returns hitRate 0 when no operations', () => {
            const cache = new UniformCache();
            expect(cache.getStats().hitRate).toBe(0);
        });
    });
});
