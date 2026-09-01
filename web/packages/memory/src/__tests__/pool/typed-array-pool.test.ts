import { describe, expect, it } from 'vitest';
import { TypedArrayPool, TypedArrayPools, type PoolableTypedArray } from '../../pool/typed-array-pool';

describe('TypedArrayPool', () => {
    describe('constructor and basic acquire', () => {
        it('creates a pool with default options', () => {
            const pool = new TypedArrayPool({
                arrayConstructor: Float32Array,
                sizeBuckets: [64, 256, 1024],
            });
            expect(pool).toBeDefined();
        });

        it('acquires a typed array with default length', () => {
            const pool = new TypedArrayPool({
                arrayConstructor: Float32Array,
                defaultLength: 64,
                sizeBuckets: [64, 256],
            });
            const arr = pool.acquire();
            expect(arr).toBeDefined();
            expect(arr.array).toBeInstanceOf(Float32Array);
            expect(arr.length).toBe(64);
            expect(arr.bytesPerElement).toBe(4);
        });

        it('acquires a typed array with custom length', () => {
            const pool = new TypedArrayPool({
                arrayConstructor: Uint8Array,
                sizeBuckets: [64, 256, 1024],
            });
            const arr = pool.acquire(100);
            expect(arr).toBeDefined();
            expect(arr.array).toBeInstanceOf(Uint8Array);
        });
    });

    describe('release()', () => {
        it('releases array back to pool', () => {
            const pool = new TypedArrayPool({
                arrayConstructor: Float32Array,
                sizeBuckets: [64, 256],
                zeroOnRelease: true,
            });
            const arr = pool.acquire(64);
            arr.array[0] = 42;
            pool.release(arr);
            // After release with zeroOnRelease, the array should be zeroed
            expect(arr.array[0]).toBe(0);
        });

        it('re-releases into the owning bucket pool', () => {
            const pool = new TypedArrayPool({
                arrayConstructor: Float32Array,
                sizeBuckets: [64, 256],
                preallocate: false,
                initialCapacity: 1,
                maxCapacity: 4,
                allocationStrategy: 'first-available',
            });
            const arr = pool.acquire(64);
            arr.array[0] = 42;
            pool.release(arr);

            const again = pool.acquire(64);
            expect(again).toBe(arr);
            expect(again.array[0]).toBe(0);
        });
    });

    describe('growth strategies', () => {
        it('supports exact strategy for uncached sizes', () => {
            const pool = new TypedArrayPool({
                arrayConstructor: Float32Array,
                growthStrategy: 'exact',
                sizeBuckets: [64, 256],
                maxPoolableLength: 10000,
                preallocate: false,
            });

            const arr = pool.acquire(1000);
            expect(arr.length).toBe(1000);
            arr.fill(1.5);
            expect(() => pool.release(arr)).not.toThrow();
            expect(pool.acquire(1000).length).toBe(1000);
        });

        it('supports exponential strategy for uncached sizes', () => {
            const pool = new TypedArrayPool({
                arrayConstructor: Float32Array,
                growthStrategy: 'exponential',
                growthFactor: 2,
                sizeBuckets: [64, 256],
                maxPoolableLength: 10000,
                preallocate: false,
            });

            const arr = pool.acquire(300);
            expect(arr.length).toBe(300);
            expect(() => pool.release(arr)).not.toThrow();
        });

        it('fails fast when request exceeds pooled capacity', () => {
            const pool = new TypedArrayPool({
                arrayConstructor: Float32Array,
                sizeBuckets: [64, 256],
                maxPoolableLength: 10000,
            });

            expect(() => pool.acquire(1000)).toThrow(/exceeds pooled capacity/);
        });
    });

    describe('acquireWithData()', () => {
        it('copies source data into pooled array', () => {
            const pool = new TypedArrayPool({
                arrayConstructor: Float32Array,
                sizeBuckets: [64, 256],
            });
            const source = new Float32Array([1.0, 2.0, 3.0, 4.0]);
            const arr = pool.acquireWithData(source);
            expect(arr.array[0]).toBe(1.0);
            expect(arr.array[1]).toBe(2.0);
            expect(arr.array[2]).toBe(3.0);
            expect(arr.array[3]).toBe(4.0);
        });
    });

    describe('getStats()', () => {
        it('returns valid stats structure', () => {
            const pool = new TypedArrayPool({
                arrayConstructor: Float32Array,
                sizeBuckets: [64, 256],
                enableMetrics: true,
            });
            pool.acquire(64);
            const stats = pool.getStats();
            expect(stats).toBeDefined();
            expect(stats.arrayStats).toBeDefined();
            expect(stats.performanceStats).toBeDefined();
            expect(stats.performanceStats.allocationTime).toBeDefined();
            expect(stats.performanceStats.zeroingTime).toBeDefined();
            expect(stats.performanceStats.copyTime).toBeDefined();
        });
    });

    describe('clear()', () => {
        it('clears all pools and resets stats', () => {
            const pool = new TypedArrayPool({
                arrayConstructor: Float32Array,
                sizeBuckets: [64, 256],
            });
            const arr = pool.acquire(64);
            pool.release(arr); // Release before clearing
            pool.clear();
            const stats = pool.getStats();
            expect(stats.arrayStats.totalArrays).toBe(0);
        });
    });

    describe('dispose()', () => {
        it('disposes all internal pools', () => {
            const pool = new TypedArrayPool({
                arrayConstructor: Float32Array,
                sizeBuckets: [64],
            });
            const arr = pool.acquire(64);
            pool.release(arr); // Release before disposing
            pool.dispose();
            const stats = pool.getStats();
            expect(stats.arrayStats.totalArrays).toBe(0);
        });
    });

    describe('growth strategies', () => {
        it('exact strategy creates pool with default bucket config', () => {
            const pool = new TypedArrayPool({
                arrayConstructor: Float32Array,
                sizeBuckets: [64, 256, 1024],
                growthStrategy: 'exact',
            });
            // Acquire within existing bucket range
            const arr = pool.acquire(64);
            expect(arr).toBeDefined();
        });

        it('exponential strategy applies growth factor', () => {
            const pool = new TypedArrayPool({
                arrayConstructor: Float32Array,
                sizeBuckets: [64, 256, 1024],
                growthStrategy: 'exponential',
                growthFactor: 2.0,
            });
            // Acquire within existing bucket range
            const arr = pool.acquire(256);
            expect(arr).toBeDefined();
        });
    });

    describe('alignment handling', () => {
        it('creates aligned buffers when alignment > 0', () => {
            const pool = new TypedArrayPool({
                arrayConstructor: Float32Array,
                sizeBuckets: [64],
                alignment: 16,
            });
            const arr = pool.acquire(64);
            expect(arr.isAligned).toBe(true);
            expect(arr.alignment).toBe(16);
        });

        it('creates non-aligned buffers when alignment is 0', () => {
            const pool = new TypedArrayPool({
                arrayConstructor: Float32Array,
                sizeBuckets: [64],
                alignment: 0,
            });
            const arr = pool.acquire(64);
            expect(arr.isAligned).toBe(false);
        });
    });

    describe('validateIntegrity', () => {
        it('validates array integrity on release', () => {
            const pool = new TypedArrayPool({
                arrayConstructor: Float32Array,
                sizeBuckets: [64],
                validateIntegrity: true,
            });
            const arr = pool.acquire(64);
            // Should not throw
            pool.release(arr);
        });
    });

    describe('poolable array operations', () => {
        it('zero() fills array with zeros', () => {
            const pool = new TypedArrayPool({
                arrayConstructor: Float32Array,
                sizeBuckets: [64],
            });
            const arr = pool.acquire(64);
            arr.array[0] = 42;
            arr.array[1] = 99;
            arr.zero();
            expect(arr.array[0]).toBe(0);
            expect(arr.array[1]).toBe(0);
        });

        it('fill() fills with specified value', () => {
            const pool = new TypedArrayPool({
                arrayConstructor: Float32Array,
                sizeBuckets: [64],
            });
            const arr = pool.acquire(64);
            arr.fill(7);
            expect(arr.array[0]).toBe(7);
        });

        it('subarray() returns a view', () => {
            const pool = new TypedArrayPool({
                arrayConstructor: Float32Array,
                sizeBuckets: [64],
            });
            const arr = pool.acquire(64);
            arr.fill(5, 0, 10);
            const sub = arr.subarray(0, 5);
            expect(sub.length).toBe(5);
            expect(sub[0]).toBe(5);
        });

        it('copyFrom() copies data from source', () => {
            const pool = new TypedArrayPool({
                arrayConstructor: Float32Array,
                sizeBuckets: [64],
            });
            const arr = pool.acquire(64);
            arr.copyFrom([10, 20, 30]);
            expect(arr.array[0]).toBe(10);
            expect(arr.array[1]).toBe(20);
            expect(arr.array[2]).toBe(30);
        });

        it('resize() shrinks array view', () => {
            const pool = new TypedArrayPool({
                arrayConstructor: Float32Array,
                sizeBuckets: [64],
            });
            const arr = pool.acquire(64);
            const result = arr.resize(32);
            expect(result).toBe(true);
            expect(arr.length).toBe(32);
        });
    });
});

describe('TypedArrayPools singletons', () => {
    it('Float32 singleton is available', () => {
        expect(TypedArrayPools.Float32).toBeInstanceOf(TypedArrayPool);
    });

    it('Float64 singleton is available', () => {
        expect(TypedArrayPools.Float64).toBeInstanceOf(TypedArrayPool);
    });

    it('Uint32 singleton is available', () => {
        expect(TypedArrayPools.Uint32).toBeInstanceOf(TypedArrayPool);
    });

    it('Uint16 singleton is available', () => {
        expect(TypedArrayPools.Uint16).toBeInstanceOf(TypedArrayPool);
    });

    it('Uint8 singleton is available', () => {
        expect(TypedArrayPools.Uint8).toBeInstanceOf(TypedArrayPool);
    });
});
