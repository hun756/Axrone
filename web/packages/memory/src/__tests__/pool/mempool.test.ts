import { afterEach, describe, expect, it, vi } from 'vitest';
import { MemoryPool, MemoryPoolError, PoolableObject } from './../../pool/mempool';

describe('MemoryPool', () => {
    class TestObject implements PoolableObject {
        __poolId?: number;
        __poolStatus?: 'free' | 'allocated' | 'reserved';
        __lastAccessed?: number;
        __allocCount?: number;
        value: number;
        constructor(value = 0) {
            this.value = value;
        }
        reset() {
            this.value = 0;
        }
    }

    const createPool = (options = {}) =>
        new MemoryPool<TestObject>({
            factory: () => new TestObject(),
            ...options,
        });

    it('should allocate and release objects', () => {
        const pool = createPool({ initialCapacity: 2 });
        const obj1 = pool.acquire();
        const obj2 = pool.acquire();
        expect(obj1).not.toBe(obj2);
        expect(pool.getAllocatedCount()).toBe(2);
        pool.release(obj1);
        expect(pool.getAllocatedCount()).toBe(1);
        pool.release(obj2);
        expect(pool.getAllocatedCount()).toBe(0);
    });

    it('should reuse released objects', () => {
        const pool = createPool({ initialCapacity: 1 });
        const obj1 = pool.acquire();
        pool.release(obj1);
        const obj2 = pool.acquire();
        expect(obj1).toBe(obj2);
    });

    it('should throw when acquiring from disposed pool', () => {
        const pool = createPool();
        pool[Symbol.dispose]();
        expect(() => pool.acquire()).toThrow(MemoryPoolError);
    });

    it('should throw when releasing foreign object', () => {
        const pool = createPool();
        const foreign = new TestObject();
        expect(() => pool.release(foreign)).toThrow(MemoryPoolError);
    });

    it('should throw when releasing already released object', () => {
        const pool = createPool();
        const obj = pool.acquire();
        pool.release(obj);
        expect(() => pool.release(obj)).toThrow(MemoryPoolError);
    });

    it('should support tryAcquire and return null if depleted', () => {
        const pool = createPool({ initialCapacity: 1, maxCapacity: 1, autoExpand: false });
        const obj1 = pool.acquire();
        expect(pool.tryAcquire()).toBeNull();
        pool.release(obj1);
        expect(pool.tryAcquire()).not.toBeNull();
    });

    it('should clear only if all objects are released', () => {
        const pool = createPool({ initialCapacity: 2 });
        const obj1 = pool.acquire();
        pool.release(obj1);
        pool.clear();
        expect(pool.getAllocatedCount()).toBe(0);
        const obj2 = pool.acquire();
        expect(obj2).toBeDefined();
    });

    it('should throw if clear is called with allocated objects', () => {
        const pool = createPool({ initialCapacity: 1 });
        pool.acquire();
        expect(() => pool.clear()).toThrow(MemoryPoolError);
    });

    it('should resize pool and preserve allocated objects', () => {
        const pool = createPool({ initialCapacity: 2 });
        const obj1 = pool.acquire();
        pool.resize(4);
        expect(pool.getTotalCount()).toBe(4);
        expect(pool.isFromPool(obj1)).toBe(true);
        pool.resize(1);
        expect(pool.getTotalCount()).toBe(1);
        expect(pool.isFromPool(obj1)).toBe(true);
    });

    it('should provide metrics', () => {
        const pool = createPool({ initialCapacity: 1, enableMetrics: true });
        pool.acquire();
        pool.releaseAll();
        const metrics = pool.getMetrics();
        expect(metrics.capacity).toBe(1);
        expect(metrics.allocations).toBeGreaterThanOrEqual(1);
        expect(metrics.releases).toBeGreaterThanOrEqual(1);
    });

    it('should support async acquire/release', async () => {
        const pool = createPool({ initialCapacity: 1 });
        const obj = await pool.acquireAsync();
        await pool.releaseAsync(obj);
        expect(pool.getAllocatedCount()).toBe(0);
    });

    it('should support tryAcquireAsync with timeout', async () => {
        vi.useFakeTimers();
        const pool = createPool({ initialCapacity: 1, maxCapacity: 1, autoExpand: false });
        pool.acquire();
        const promise = pool.tryAcquireAsync(50);
        await vi.advanceTimersByTimeAsync(50);
        const result = await promise;
        expect(result).toBeNull();
        vi.useRealTimers();
    });

    it('should force compact and not throw', () => {
        const pool = createPool({ initialCapacity: 2 });
        expect(() => pool.forceCompact()).not.toThrow();
    });

    it('should drain and preserve allocated objects', () => {
        const pool = createPool({ initialCapacity: 3 });
        const obj1 = pool.acquire();
        const obj2 = pool.acquire();
        pool.release(obj2);
        pool.drain();
        expect(pool.isFromPool(obj1)).toBe(true);
        expect(pool.getAllocatedCount()).toBe(1);
    });
    it('should call validator and reject invalid objects', () => {
        let called = false;
        const pool = createPool({
            initialCapacity: 1,
            validator: (obj: TestObject) => {
                called = true;
                return false;
            },
        });
        expect(() => pool.acquire()).toThrow(MemoryPoolError);
        expect(called).toBe(true);
    });

    it('should call onAcquire and onRelease callbacks', () => {
        let acquired = false;
        let released = false;
        const pool = createPool({
            initialCapacity: 1,
            onAcquire: () => {
                acquired = true;
            },
            onRelease: () => {
                released = true;
            },
        });
        const obj = pool.acquire();
        expect(acquired).toBe(true);
        pool.release(obj);
        expect(released).toBe(true);
    });

    it('should handle factory errors gracefully', () => {
        expect(() => createPool({
            initialCapacity: 1,
            factory: () => {
                throw new Error('factory fail');
            },
        })).toThrow();
    });

    it('should expand before evicting live objects', () => {
        const pool = createPool({ initialCapacity: 2, evictionPolicy: 'lru' });
        const a = pool.acquire();
        const b = pool.acquire();
        pool.acquire();

        expect(pool.isFromPool(a)).toBe(true);
        expect(pool.isFromPool(b)).toBe(true);
        expect(a.__poolStatus).toBe('allocated');
        expect(b.__poolStatus).toBe('allocated');
        expect(pool.getAllocatedCount()).toBe(3);
    });

    it('should keep free list consistent across shrink compaction', () => {
        const pool = createPool({ initialCapacity: 8, preallocate: true });
        pool.acquire();
        pool.acquire();
        pool.resize(4);

        const obj = pool.acquire();
        expect(obj).toBeDefined();
        expect(pool.getAllocatedCount()).toBe(3);
        expect(pool.getTotalCount()).toBe(4);
        expect(() => pool.release(obj)).not.toThrow();
    });

    it('should not leak memory on rapid acquire/release cycles', () => {
        const pool = createPool({ initialCapacity: 10, maxCapacity: 100 });
        for (let i = 0; i < 1000; ++i) {
            const obj = pool.acquire();
            pool.release(obj);
        }
        expect(pool.getAllocatedCount()).toBe(0);
        expect(pool.getTotalCount()).toBeLessThanOrEqual(100);
    });

    it('should support asyncFactory and preallocate', async () => {
        vi.useFakeTimers();
        let created = 0;
        const pool = new MemoryPool<TestObject>({
            initialCapacity: 2,
            preallocate: true,
            asyncFactory: async () => {
                created++;
                return new TestObject();
            },
            factory: () => new TestObject(),
        });
        // Wait for preallocation
        await vi.advanceTimersByTimeAsync(50);
        expect(created).toBeGreaterThanOrEqual(2);
        vi.useRealTimers();
    });

    it('should respect TTL eviction policy', async () => {
        vi.useFakeTimers();
        const pool = createPool({
            initialCapacity: 1,
            evictionPolicy: 'ttl',
            ttl: 10,
        });
        const obj = pool.acquire();
        pool.release(obj);
        await vi.advanceTimersByTimeAsync(20);
        expect(() => pool.acquire()).not.toThrow();
        vi.useRealTimers();
    });

    it('should not allow operations after dispose', () => {
        const pool = createPool({ initialCapacity: 1 });
        pool[Symbol.dispose]();
        expect(() => pool.acquire()).toThrow();
        expect(() => pool.releaseAll()).not.toThrow();
        expect(() => pool.drain()).not.toThrow();
    });

    it('should update metrics correctly after many operations', () => {
        const pool = createPool({ initialCapacity: 2, enableMetrics: true });
        for (let i = 0; i < 100; ++i) {
            const obj = pool.acquire();
            pool.release(obj);
        }
        const metrics = pool.getMetrics();
        expect(metrics.allocations).toBeGreaterThan(50);
        expect(metrics.releases).toBeGreaterThan(50);
        expect(metrics.highWaterMark).toBeLessThanOrEqual(2);
    });

    it('should handle concurrent async acquires and releases', async () => {
        const pool = createPool({ initialCapacity: 2 });
        const results: TestObject[] = [];
        const acquires = [pool.acquireAsync(), pool.acquireAsync()];
        results.push(await acquires[0]);
        results.push(await acquires[1]);
        expect(pool.getAllocatedCount()).toBe(2);
        await Promise.all(results.map((obj) => pool.releaseAsync(obj)));
        expect(pool.getAllocatedCount()).toBe(0);
    });

    it('should not call reset if resetOnRecycle is false', () => {
        let resetCalled = false;
        class NoResetObject extends TestObject {
            reset() {
                resetCalled = true;
            }
        }
        const pool = new MemoryPool<NoResetObject>({
            factory: () => new NoResetObject(),
            initialCapacity: 1,
            resetOnRecycle: false,
        });
        const obj = pool.acquire();
        pool.release(obj);
        expect(resetCalled).toBe(false);
    });

    describe('onOutOfMemory callback', () => {
        it('should call onOutOfMemory when pool is fully depleted with no eviction', () => {
            const onOutOfMemory = vi.fn();
            const pool = createPool({
                initialCapacity: 2,
                maxCapacity: 2,
                autoExpand: false,
                evictionPolicy: 'none',
                onOutOfMemory,
            });

            const obj1 = pool.acquire();
            const obj2 = pool.acquire();

            // Pool is now fully depleted — next acquire should trigger onOutOfMemory
            expect(() => pool.acquire()).toThrow(MemoryPoolError);
            expect(onOutOfMemory).toHaveBeenCalledTimes(1);
            expect(onOutOfMemory).toHaveBeenCalledWith(1, 0);

            pool.release(obj1);
            pool.release(obj2);
        });

        it('should call onOutOfMemory with correct requested and available args', () => {
            const onOutOfMemory = vi.fn();
            const pool = createPool({
                initialCapacity: 1,
                maxCapacity: 1,
                autoExpand: false,
                evictionPolicy: 'none',
                onOutOfMemory,
            });

            pool.acquire();
            expect(() => pool.acquire()).toThrow();
            expect(onOutOfMemory).toHaveBeenCalledWith(1, 0);
        });

        it('should not call onOutOfMemory when pool can still expand', () => {
            const onOutOfMemory = vi.fn();
            const pool = createPool({
                initialCapacity: 1,
                maxCapacity: 10,
                autoExpand: true,
                onOutOfMemory,
            });

            // Should auto-expand without triggering onOutOfMemory
            const obj1 = pool.acquire();
            const obj2 = pool.acquire();
            expect(onOutOfMemory).not.toHaveBeenCalled();

            pool.release(obj1);
            pool.release(obj2);
        });
    });

    describe('Expansion strategies', () => {
        it('fibonacci strategy grows pool following fibonacci sequence', () => {
            const pool = createPool({
                initialCapacity: 4,
                maxCapacity: 100,
                autoExpand: true,
                expansionStrategy: 'fibonacci',
            });

            // Initial capacity is 4. Acquire all 4 to fill the pool.
            const objs: TestObject[] = [];
            for (let i = 0; i < 4; i++) {
                objs.push(pool.acquire());
            }
            expect(pool.getTotalCount()).toBe(4);

            // 5th acquire triggers expansion. Fibonacci above 4 is 5.
            objs.push(pool.acquire());
            expect(pool.getTotalCount()).toBe(5);

            // 6th acquire triggers another expansion. Fibonacci above 5 is 8.
            // Pool now has 8 total slots, 6 allocated, 2 free.
            objs.push(pool.acquire());
            expect(pool.getTotalCount()).toBe(8);

            // 2 more acquires use the remaining free slots (no expansion).
            objs.push(pool.acquire());
            objs.push(pool.acquire());
            expect(pool.getTotalCount()).toBe(8);
            expect(pool.getAllocatedCount()).toBe(8);

            // 9th acquire triggers expansion. Fibonacci above 8 is 13.
            objs.push(pool.acquire());
            expect(pool.getTotalCount()).toBe(13);

            objs.forEach((o) => pool.release(o));
        });

        it('prime strategy grows pool to prime-based capacity', () => {
            const pool = createPool({
                initialCapacity: 4,
                maxCapacity: 200,
                autoExpand: true,
                expansionStrategy: 'prime',
                expansionFactor: 2,
            });

            const objs: TestObject[] = [];
            for (let i = 0; i < 4; i++) {
                objs.push(pool.acquire());
            }

            // 5th acquire triggers expansion.
            // prime(4 * 2) = prime(8) = 11 (next prime >= 8)
            objs.push(pool.acquire());
            expect(pool.getTotalCount()).toBe(11);

            objs.forEach((o) => pool.release(o));
        });

        it('fixed strategy grows pool by fixed increment', () => {
            const pool = createPool({
                initialCapacity: 4,
                maxCapacity: 100,
                autoExpand: true,
                expansionStrategy: 'fixed',
                expansionRate: 8,
            });

            const objs: TestObject[] = [];
            for (let i = 0; i < 4; i++) {
                objs.push(pool.acquire());
            }

            // 5th acquire triggers expansion: 4 + 8 = 12
            objs.push(pool.acquire());
            expect(pool.getTotalCount()).toBe(12);

            objs.forEach((o) => pool.release(o));
        });

        it('multiplicative strategy grows pool by factor', () => {
            const pool = createPool({
                initialCapacity: 4,
                maxCapacity: 100,
                autoExpand: true,
                expansionStrategy: 'multiplicative',
                expansionFactor: 2,
            });

            const objs: TestObject[] = [];
            for (let i = 0; i < 4; i++) {
                objs.push(pool.acquire());
            }

            // 5th acquire triggers expansion: ceil(4 * 2) = 8
            objs.push(pool.acquire());
            expect(pool.getTotalCount()).toBe(8);

            objs.forEach((o) => pool.release(o));
        });
    });

    it('should resolve waiter when object is released', async () => {
        const pool = createPool({ initialCapacity: 1, maxCapacity: 1 });
        const obj1 = pool.acquire();

        // Start async acquire — will wait because pool is at max capacity
        const acquirePromise = pool.acquireAsync();

        // Release obj1 — should resolve the waiter with the same object
        pool.release(obj1);

        const obj2 = await acquirePromise;
        expect(obj2).toBe(obj1);
    });

    it('should reject pending waiters on dispose', async () => {
        const pool = createPool({ initialCapacity: 1, maxCapacity: 1 });
        pool.acquire(); // Fill pool to capacity

        // Start async acquire — will wait because pool is full
        const acquirePromise = pool.acquireAsync();

        // Dispose pool — should reject the pending waiter
        pool[Symbol.dispose]();

        await expect(acquirePromise).rejects.toThrow(MemoryPoolError);
    });

    describe('dispose onEvict notification', () => {
        it('should call onEvict for each allocated object when pool is disposed', () => {
            const evicted: TestObject[] = [];
            const pool = createPool({
                initialCapacity: 4,
                onEvict: (obj: TestObject) => {
                    evicted.push(obj);
                },
            });

            const obj1 = pool.acquire();
            const obj2 = pool.acquire();
            const obj3 = pool.acquire();

            // Release one so it is free — only 2 are allocated at dispose time
            pool.release(obj3);

            expect(pool.getAllocatedCount()).toBe(2);

            pool[Symbol.dispose]();

            // onEvict must fire for the 2 still-allocated objects, not the free one
            expect(evicted).toHaveLength(2);
            expect(evicted).toContain(obj1);
            expect(evicted).toContain(obj2);
            expect(evicted).not.toContain(obj3);
        });

        it('should not throw if onEvict handler throws during dispose', () => {
            const pool = createPool({
                initialCapacity: 2,
                onEvict: () => {
                    throw new Error('onEvict boom');
                },
            });

            pool.acquire();
            pool.acquire();

            // Must not throw even though onEvict throws
            expect(() => pool[Symbol.dispose]()).not.toThrow();
        });

        it('should not call onEvict for free slots during dispose', () => {
            const evictCount = { value: 0 };
            const pool = createPool({
                initialCapacity: 3,
                onEvict: () => {
                    evictCount.value++;
                },
            });

            const obj = pool.acquire();
            pool.release(obj);

            pool[Symbol.dispose]();

            // All objects are free — onEvict should not fire
            expect(evictCount.value).toBe(0);
        });
    });
});
