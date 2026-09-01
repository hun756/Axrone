import { describe, expect, it, vi } from 'vitest';
import { create, createAsync } from '../lazy';
import {
    lazyToSingleton,
    lazyAsyncToAsyncSingleton,
    singletonToLazy,
    asyncSingletonToLazyAsync,
} from '../lazy/lazy-singleton-bridge';

// ---------------------------------------------------------------------------
// lazyToSingleton
// ---------------------------------------------------------------------------

describe('lazyToSingleton', () => {
    it('should wrap an ILazy as an ISingleton and delegate value computation', () => {
        let calls = 0;
        const lazy = create(() => {
            calls++;
            return 42;
        });

        const singleton = lazyToSingleton(lazy);

        // Value not yet computed (lazy: true is passed through)
        expect(calls).toBe(0);
        expect(singleton.isCreated).toBe(false);

        const value = singleton.getInstance();
        expect(value).toBe(42);
        expect(calls).toBe(1);
        expect(singleton.isCreated).toBe(true);
    });

    it('should cache the value across multiple getInstance calls', () => {
        let calls = 0;
        const lazy = create(() => {
            calls++;
            return 'cached';
        });
        const singleton = lazyToSingleton(lazy);

        expect(singleton.getInstance()).toBe('cached');
        expect(singleton.getInstance()).toBe('cached');
        expect(singleton.getInstance()).toBe('cached');
        // Lazy caches internally, so factory called once
        expect(calls).toBe(1);
        // Singleton tracks access count
        expect(singleton.accessCount).toBe(3);
    });

    it('should propagate errors from the lazy factory', () => {
        const lazy = create<number>(() => {
            throw new Error('lazy fail');
        });
        const singleton = lazyToSingleton(lazy);

        expect(() => singleton.getInstance()).toThrow('lazy fail');
        expect(singleton.isFaulted).toBe(true);
    });

    it('should pass options through to the singleton', () => {
        const lazy = create(() => 10);
        const disposer = vi.fn();
        const singleton = lazyToSingleton(lazy, {
            lifecycle: 'scoped',
            disposer,
        });

        expect(singleton.getInstance()).toBe(10);
        singleton.dispose();
        expect(disposer).toHaveBeenCalledWith(10);
    });

    it('should generate a unique key for the singleton', () => {
        const lazy1 = create(() => 1);
        const lazy2 = create(() => 2);
        const s1 = lazyToSingleton(lazy1);
        const s2 = lazyToSingleton(lazy2);

        expect(s1.key).not.toBe(s2.key);
    });

    it('should support tryGetInstance', () => {
        const lazy = create(() => 'hello');
        const singleton = lazyToSingleton(lazy);

        expect(singleton.tryGetInstance()).toBe('hello');
    });

    it('should return null from tryGetInstance when factory throws', () => {
        const lazy = create<number>(() => {
            throw new Error('fail');
        });
        const singleton = lazyToSingleton(lazy);

        expect(singleton.tryGetInstance()).toBeNull();
    });
});

// ---------------------------------------------------------------------------
// lazyAsyncToAsyncSingleton
// ---------------------------------------------------------------------------

describe('lazyAsyncToAsyncSingleton', () => {
    it('should wrap an ILazyAsync as an IAsyncSingleton and delegate value computation', async () => {
        let calls = 0;
        const lazy = createAsync(async () => {
            calls++;
            return 99;
        });

        const singleton = lazyAsyncToAsyncSingleton(lazy);

        expect(calls).toBe(0);
        expect(singleton.isCreated).toBe(false);

        const value = await singleton.getInstance();
        expect(value).toBe(99);
        expect(calls).toBe(1);
        expect(singleton.isCreated).toBe(true);
    });

    it('should cache the value across multiple getInstance calls', async () => {
        let calls = 0;
        const lazy = createAsync(async () => {
            calls++;
            return 'async-cached';
        });
        const singleton = lazyAsyncToAsyncSingleton(lazy);

        expect(await singleton.getInstance()).toBe('async-cached');
        expect(await singleton.getInstance()).toBe('async-cached');
        expect(calls).toBe(1);
        expect(singleton.accessCount).toBe(2);
    });

    it('should propagate errors from the lazy async factory', async () => {
        const lazy = createAsync<number>(async () => {
            throw new Error('async lazy fail');
        });
        const singleton = lazyAsyncToAsyncSingleton(lazy);

        await expect(singleton.getInstance()).rejects.toThrow('async lazy fail');
        expect(singleton.isFaulted).toBe(true);
    });

    it('should pass options through to the async singleton', async () => {
        const lazy = createAsync(async () => 10);
        const disposer = vi.fn();
        const singleton = lazyAsyncToAsyncSingleton(lazy, {
            lifecycle: 'scoped',
            disposer,
        });

        expect(await singleton.getInstance()).toBe(10);
        singleton.dispose();
        expect(disposer).toHaveBeenCalledWith(10);
    });

    it('should generate a unique key for the async singleton', () => {
        const lazy1 = createAsync(async () => 1);
        const lazy2 = createAsync(async () => 2);
        const s1 = lazyAsyncToAsyncSingleton(lazy1);
        const s2 = lazyAsyncToAsyncSingleton(lazy2);

        expect(s1.key).not.toBe(s2.key);
    });

    it('should support tryGetInstance', async () => {
        const lazy = createAsync(async () => 'world');
        const singleton = lazyAsyncToAsyncSingleton(lazy);

        expect(await singleton.tryGetInstance()).toBe('world');
    });

    it('should return null from tryGetInstance when factory throws', async () => {
        const lazy = createAsync<number>(async () => {
            throw new Error('fail');
        });
        const singleton = lazyAsyncToAsyncSingleton(lazy);

        expect(await singleton.tryGetInstance()).toBeNull();
    });
});

// ---------------------------------------------------------------------------
// singletonToLazy
// ---------------------------------------------------------------------------

describe('singletonToLazy', () => {
    it('should wrap an ISingleton as an ILazy and delegate value computation', () => {
        // Create a real singleton from @axrone/utility via the bridge round-trip
        const lazy = create(() => 42);
        const singleton = lazyToSingleton(lazy);
        const reLazy = singletonToLazy(singleton);

        expect(reLazy.Value).toBe(42);
        expect(reLazy.IsValueCreated).toBe(true);
    });

    it('should reflect the singleton state (value not created until accessed)', () => {
        let calls = 0;
        const lazy = create(() => {
            calls++;
            return 'deferred';
        });
        const singleton = lazyToSingleton(lazy);
        const reLazy = singletonToLazy(singleton);

        // Nothing computed yet
        expect(calls).toBe(0);
        expect(reLazy.IsValueCreated).toBe(false);

        expect(reLazy.Value).toBe('deferred');
        expect(calls).toBe(1);
        expect(reLazy.IsValueCreated).toBe(true);
    });

    it('should cache the value through the singleton', () => {
        let calls = 0;
        const lazy = create(() => {
            calls++;
            return 'once';
        });
        const singleton = lazyToSingleton(lazy);
        const reLazy = singletonToLazy(singleton);

        expect(reLazy.Value).toBe('once');
        expect(reLazy.Value).toBe('once');
        // Singleton caches, so factory called once
        expect(calls).toBe(1);
    });

    it('should propagate errors from the singleton', () => {
        const lazy = create<number>(() => {
            throw new Error('singleton error');
        });
        const singleton = lazyToSingleton(lazy);
        const reLazy = singletonToLazy(singleton);

        expect(() => reLazy.Value).toThrow('singleton error');
        expect(reLazy.IsValueFaulted).toBe(true);
    });

    it('should support force()', () => {
        const lazy = create(() => 'forced');
        const singleton = lazyToSingleton(lazy);
        const reLazy = singletonToLazy(singleton);

        expect(reLazy.force()).toBe('forced');
    });

    it('should support map combinator', () => {
        const lazy = create(() => 10);
        const singleton = lazyToSingleton(lazy);
        const reLazy = singletonToLazy(singleton);
        const mapped = reLazy.map((x) => x * 3);

        expect(mapped.Value).toBe(30);
    });
});

// ---------------------------------------------------------------------------
// asyncSingletonToLazyAsync
// ---------------------------------------------------------------------------

describe('asyncSingletonToLazyAsync', () => {
    it('should wrap an IAsyncSingleton as an ILazyAsync and delegate value computation', async () => {
        const lazy = createAsync(async () => 77);
        const singleton = lazyAsyncToAsyncSingleton(lazy);
        const reLazy = asyncSingletonToLazyAsync(singleton);

        const value = await reLazy.force();
        expect(value).toBe(77);
        expect(reLazy.IsValueCreated).toBe(true);
    });

    it('should reflect the singleton state (value not created until accessed)', async () => {
        let calls = 0;
        const lazy = createAsync(async () => {
            calls++;
            return 'async-deferred';
        });
        const singleton = lazyAsyncToAsyncSingleton(lazy);
        const reLazy = asyncSingletonToLazyAsync(singleton);

        expect(calls).toBe(0);
        expect(reLazy.IsValueCreated).toBe(false);

        expect(await reLazy.force()).toBe('async-deferred');
        expect(calls).toBe(1);
        expect(reLazy.IsValueCreated).toBe(true);
    });

    it('should cache the value through the singleton', async () => {
        let calls = 0;
        const lazy = createAsync(async () => {
            calls++;
            return 'async-once';
        });
        const singleton = lazyAsyncToAsyncSingleton(lazy);
        const reLazy = asyncSingletonToLazyAsync(singleton);

        expect(await reLazy.force()).toBe('async-once');
        expect(await reLazy.force()).toBe('async-once');
        expect(calls).toBe(1);
    });

    it('should propagate errors from the async singleton', async () => {
        const lazy = createAsync<number>(async () => {
            throw new Error('async singleton error');
        });
        const singleton = lazyAsyncToAsyncSingleton(lazy);
        const reLazy = asyncSingletonToLazyAsync(singleton);

        await expect(reLazy.force()).rejects.toThrow('async singleton error');
        expect(reLazy.IsValueFaulted).toBe(true);
    });

    it('should support Value property (returns Promise)', async () => {
        const lazy = createAsync(async () => 'via-value');
        const singleton = lazyAsyncToAsyncSingleton(lazy);
        const reLazy = asyncSingletonToLazyAsync(singleton);

        await expect(reLazy.Value).resolves.toBe('via-value');
    });

    it('should support map combinator', async () => {
        const lazy = createAsync(async () => 5);
        const singleton = lazyAsyncToAsyncSingleton(lazy);
        const reLazy = asyncSingletonToLazyAsync(singleton);
        const mapped = reLazy.map((x) => x * 10);

        await expect(mapped.force()).resolves.toBe(50);
    });
});

// ---------------------------------------------------------------------------
// Round-trip conversions
// ---------------------------------------------------------------------------

describe('round-trip conversions', () => {
    it('lazy -> singleton -> lazy preserves value', () => {
        const original = create(() => 'round-trip');
        const singleton = lazyToSingleton(original);
        const backToLazy = singletonToLazy(singleton);

        expect(backToLazy.Value).toBe('round-trip');
    });

    it('lazyAsync -> asyncSingleton -> lazyAsync preserves value', async () => {
        const original = createAsync(async () => 'async-round-trip');
        const singleton = lazyAsyncToAsyncSingleton(original);
        const backToLazy = asyncSingletonToLazyAsync(singleton);

        await expect(backToLazy.force()).resolves.toBe('async-round-trip');
    });

    it('multiple round-trips maintain identity of computed value', () => {
        let calls = 0;
        const lazy = create(() => {
            calls++;
            return { id: calls };
        });

        // lazy -> singleton -> lazy -> singleton
        const s1 = lazyToSingleton(lazy);
        const l2 = singletonToLazy(s1);
        const s2 = lazyToSingleton(l2);

        const v1 = s1.getInstance();
        const v2 = s2.getInstance();

        // Both should resolve to the same value since the original lazy caches
        expect(v1).toEqual(v2);
        // Factory called once (original lazy caches)
        expect(calls).toBe(1);
    });
});
