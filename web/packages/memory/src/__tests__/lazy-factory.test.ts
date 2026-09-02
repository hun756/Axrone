import { describe, expect, it } from 'vitest';
import { LazyFactoryImpl } from '../lazy/lazy-factory';

describe('LazyFactoryImpl', () => {
    describe('cache bounds', () => {
        it('keeps unbounded cache and access order aligned', () => {
            const factory = new LazyFactoryImpl<string, number>(
                (key: string) => key.length,
                (key: string) => key,
                Number.POSITIVE_INFINITY
            );

            for (let i = 0; i < 2000; i++) {
                factory.getOrAdd(`key-${i}`);
            }

            expect(factory.cacheSize).toBe(2000);
            expect(factory.invalidate('key-0')).toBe(true);
            expect(factory.invalidate('key-1999')).toBe(true);
        });

        it('evicts least recently used entries at the cache bound', () => {
            const factory = new LazyFactoryImpl<string, number>(
                (key: string) => key.length,
                (key: string) => key,
                3
            );

            factory.getOrAdd('a');
            factory.getOrAdd('b');
            factory.getOrAdd('c');
            factory.getOrAdd('a');
            factory.getOrAdd('d');

            expect(factory.cacheSize).toBe(3);
            expect(factory.invalidate('a')).toBe(true);
            expect(factory.invalidate('b')).toBe(false);
        });
    });

    describe('getOrAdd()', () => {
        it('caches by key and returns same value on repeated calls', () => {
            let callCount = 0;
            const factory = new LazyFactoryImpl(
                (a: number, b: number) => {
                    callCount++;
                    return a + b;
                }
            );

            expect(factory.getOrAdd(1, 2)).toBe(3);
            expect(factory.getOrAdd(1, 2)).toBe(3);
            expect(callCount).toBe(1);
        });

        it('uses different keys for different args', () => {
            const factory = new LazyFactoryImpl((x: number) => x * 10);

            expect(factory.getOrAdd(1)).toBe(10);
            expect(factory.getOrAdd(2)).toBe(20);
            expect(factory.getOrAdd(3)).toBe(30);
            expect(factory.cacheSize).toBe(3);
        });

        it('supports custom key selector', () => {
            const factory = new LazyFactoryImpl(
                (obj: { id: number; name: string }) => obj.name,
                (obj) => String(obj.id)
            );

            expect(factory.getOrAdd({ id: 1, name: 'first' })).toBe('first');
            // Same id => same key, returns cached value
            expect(factory.getOrAdd({ id: 1, name: 'different' })).toBe('first');
            expect(factory.cacheSize).toBe(1);
        });
    });

    describe('tryGetValue()', () => {
        it('returns [false, undefined] for missing key', () => {
            const factory = new LazyFactoryImpl((x: number) => x);
            expect(factory.tryGetValue(42)).toEqual([false, undefined]);
        });

        it('returns [true, value] for cached key', () => {
            const factory = new LazyFactoryImpl((x: number) => x * 2);
            factory.getOrAdd(5);
            expect(factory.tryGetValue(5)).toEqual([true, 10]);
        });
    });

    describe('invalidate()', () => {
        it('removes entry and returns true', () => {
            const factory = new LazyFactoryImpl((x: number) => x);
            factory.getOrAdd(10);
            expect(factory.cacheSize).toBe(1);
            expect(factory.invalidate(10)).toBe(true);
            expect(factory.cacheSize).toBe(0);
        });

        it('returns false for non-existent key', () => {
            const factory = new LazyFactoryImpl((x: number) => x);
            expect(factory.invalidate(999)).toBe(false);
        });
    });

    describe('clear()', () => {
        it('empties cache', () => {
            const factory = new LazyFactoryImpl((x: number) => x);
            factory.getOrAdd(1);
            factory.getOrAdd(2);
            factory.getOrAdd(3);
            expect(factory.cacheSize).toBe(3);
            factory.clear();
            expect(factory.cacheSize).toBe(0);
        });
    });

    describe('LRU eviction', () => {
        it('evicts least recently used when maxCacheSize exceeded', () => {
            const factory = new LazyFactoryImpl(
                (x: number) => x * 100,
                (x) => String(x),
                3
            );

            factory.getOrAdd(1); // key "1"
            factory.getOrAdd(2); // key "2"
            factory.getOrAdd(3); // key "3"
            expect(factory.cacheSize).toBe(3);

            // Adding 4th entry should evict LRU (key "1")
            factory.getOrAdd(4);
            expect(factory.cacheSize).toBe(3);

            // Key "1" should have been evicted
            const [found1] = factory.tryGetValue(1);
            expect(found1).toBe(false);

            // Others should still be present
            const [found2, val2] = factory.tryGetValue(2);
            expect(found2).toBe(true);
            expect(val2).toBe(200);
        });
    });

    describe('create()', () => {
        it('returns a lazy wrapper around getOrAdd', () => {
            const factory = new LazyFactoryImpl((x: number) => x + 1);
            const lazy = factory.create(5);
            expect(lazy.force()).toBe(6);
        });

        it('lazy result is cached through factory', () => {
            let callCount = 0;
            const factory = new LazyFactoryImpl((x: number) => {
                callCount++;
                return x * 2;
            });
            const lazy1 = factory.create(3);
            const lazy2 = factory.create(3);

            expect(lazy1.force()).toBe(6);
            expect(lazy2.force()).toBe(6);
            expect(callCount).toBe(1);
        });
    });

    describe('createAsync()', () => {
        it('returns an async lazy wrapper', async () => {
            const factory = new LazyFactoryImpl((x: number) => x + 10);
            const asyncLazy = factory.createAsync(5);
            await expect(asyncLazy.force()).resolves.toBe(15);
        });
    });

    describe('cacheSize', () => {
        it('reflects current cache size', () => {
            const factory = new LazyFactoryImpl((x: number) => x);
            expect(factory.cacheSize).toBe(0);
            factory.getOrAdd(1);
            expect(factory.cacheSize).toBe(1);
            factory.getOrAdd(2);
            expect(factory.cacheSize).toBe(2);
        });
    });
});
