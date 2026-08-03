import { describe, it, expect, afterEach } from 'vitest';
import {
    create,
    createAsync,
    createScoped,
    createLazy,
    createLazyAsync,
    fromValue,
    fromPromise,
    createRegistered,
    createRegisteredAsync,
    isSingleton,
    isAsyncSingleton,
    isScopedSingleton,
    isAnySingleton,
    resolve,
    tryResolve,
    map,
    mapAsync,
    combine,
    combineAsync,
    SingletonImpl,
    AsyncSingletonImpl,
    ScopedSingletonImpl,
    createRootScope,
    resetGlobalRegistryAsync,
    getGlobalRegistry,
} from '../../singleton';

afterEach(async () => {
    await resetGlobalRegistryAsync();
});

// ---------------------------------------------------------------------------
// Factory functions
// ---------------------------------------------------------------------------
describe('factory functions', () => {
    describe('create', () => {
        it('returns ISingleton with eager init', () => {
            const s = create(() => 42);
            expect(isSingleton(s)).toBe(true);
            expect(s.isCreated).toBe(true);
            expect(s.getInstance()).toBe(42);
            s.dispose();
        });
    });

    describe('createAsync', () => {
        it('returns IAsyncSingleton', async () => {
            const s = createAsync(async () => 42, { lazy: true });
            expect(isAsyncSingleton(s)).toBe(true);
            expect(await s.getInstance()).toBe(42);
            await s.disposeAsync();
        });
    });

    describe('createScoped', () => {
        it('returns IScopedSingleton', () => {
            const scope = createRootScope('test');
            const s = createScoped(() => 42);
            expect(isScopedSingleton(s)).toBe(true);
            expect(s.getInstance(scope)).toBe(42);
            scope.disposeAsync();
        });
    });

    describe('createLazy', () => {
        it('defers initialization', () => {
            const s = createLazy(() => 42);
            expect(isSingleton(s)).toBe(true);
            expect(s.isCreated).toBe(false);
            expect(s.getInstance()).toBe(42);
            expect(s.isCreated).toBe(true);
            s.dispose();
        });
    });

    describe('createLazyAsync', () => {
        it('defers async initialization', async () => {
            const s = createLazyAsync(async () => 42);
            expect(isAsyncSingleton(s)).toBe(true);
            expect(s.isCreated).toBe(false);
            expect(await s.getInstance()).toBe(42);
            await s.disposeAsync();
        });
    });

    describe('fromValue', () => {
        it('wraps a value directly', () => {
            const s = fromValue(99);
            expect(isSingleton(s)).toBe(true);
            expect(s.getInstance()).toBe(99);
            s.dispose();
        });
    });

    describe('fromPromise', () => {
        it('wraps a promise', async () => {
            const s = fromPromise(Promise.resolve(99));
            expect(isAsyncSingleton(s)).toBe(true);
            expect(await s.getInstance()).toBe(99);
            await s.disposeAsync();
        });
    });

    describe('createRegistered', () => {
        it('registers in global registry', () => {
            const s = createRegistered('my-key', () => 42);
            expect(getGlobalRegistry().has('my-key')).toBe(true);
            expect(getGlobalRegistry().get('my-key')).toBe(s);
            s.dispose();
        });
    });

    describe('createRegisteredAsync', () => {
        it('registers async singleton in global registry', async () => {
            const s = createRegisteredAsync('async-key', async () => 42);
            expect(getGlobalRegistry().has('async-key')).toBe(true);
            expect(getGlobalRegistry().get('async-key')).toBe(s);
            await s.disposeAsync();
        });
    });
});

// ---------------------------------------------------------------------------
// Type guards
// ---------------------------------------------------------------------------
describe('type guards', () => {
    describe('isSingleton', () => {
        it('returns true for SingletonImpl', () => {
            const s = new SingletonImpl(() => 1);
            expect(isSingleton(s)).toBe(true);
            s.dispose();
        });
        it('returns false for plain object', () => expect(isSingleton({})).toBe(false));
        it('returns false for null', () => expect(isSingleton(null)).toBe(false));
        it('returns false for AsyncSingletonImpl', () => {
            const s = new AsyncSingletonImpl(async () => 1, { lazy: true });
            expect(isSingleton(s)).toBe(false);
            s.disposeAsync();
        });
    });

    describe('isAsyncSingleton', () => {
        it('returns true for AsyncSingletonImpl', () => {
            const s = new AsyncSingletonImpl(async () => 1, { lazy: true });
            expect(isAsyncSingleton(s)).toBe(true);
            s.disposeAsync();
        });
        it('returns false for sync singleton', () => {
            const s = new SingletonImpl(() => 1);
            expect(isAsyncSingleton(s)).toBe(false);
            s.dispose();
        });
        it('returns false for null', () => expect(isAsyncSingleton(null)).toBe(false));
    });

    describe('isScopedSingleton', () => {
        it('returns true for ScopedSingletonImpl', () => {
            const s = new ScopedSingletonImpl(() => 1);
            expect(isScopedSingleton(s)).toBe(true);
        });
        it('returns false for sync singleton', () => {
            const s = new SingletonImpl(() => 1);
            expect(isScopedSingleton(s)).toBe(false);
            s.dispose();
        });
        it('returns false for null', () => expect(isScopedSingleton(null)).toBe(false));
    });

    describe('isAnySingleton', () => {
        it('accepts sync singleton', () => {
            const s = new SingletonImpl(() => 1);
            expect(isAnySingleton(s)).toBe(true);
            s.dispose();
        });
        it('accepts async singleton', () => {
            const s = new AsyncSingletonImpl(async () => 1, { lazy: true });
            expect(isAnySingleton(s)).toBe(true);
            s.disposeAsync();
        });
        it('accepts scoped singleton', () => {
            const s = new ScopedSingletonImpl(() => 1);
            expect(isAnySingleton(s)).toBe(true);
        });
        it('rejects non-singletons', () => {
            expect(isAnySingleton(null)).toBe(false);
            expect(isAnySingleton(42)).toBe(false);
            expect(isAnySingleton({})).toBe(false);
        });
    });
});

// ---------------------------------------------------------------------------
// resolve / tryResolve
// ---------------------------------------------------------------------------
describe('resolve', () => {
    it('returns instance from sync singleton', () => {
        const s = create(() => 42);
        expect(resolve(s)).toBe(42);
        s.dispose();
    });

    it('returns Promise from async singleton', async () => {
        const s = createAsync(async () => 42, { lazy: true });
        const result = resolve(s);
        expect(result).toBeInstanceOf(Promise);
        expect(await result).toBe(42);
        await s.disposeAsync();
    });
});

describe('tryResolve', () => {
    it('returns instance from sync singleton', () => {
        const s = create(() => 42);
        expect(tryResolve(s)).toBe(42);
        s.dispose();
    });

    it('returns null for disposed sync singleton', () => {
        const s = create(() => 42);
        s.dispose();
        expect(tryResolve(s)).toBeNull();
    });

    it('returns Promise from async singleton', async () => {
        const s = createAsync(async () => 42, { lazy: true });
        const result = tryResolve(s);
        expect(result).toBeInstanceOf(Promise);
        expect(await result).toBe(42);
        await s.disposeAsync();
    });
});

// ---------------------------------------------------------------------------
// map / mapAsync
// ---------------------------------------------------------------------------
describe('map', () => {
    it('creates new singleton that transforms value', () => {
        const source = create(() => 10);
        const mapped = map(source, (v) => v * 2);
        expect(mapped.getInstance()).toBe(20);
        source.dispose();
        mapped.dispose();
    });
});

describe('mapAsync', () => {
    it('creates new async singleton with async mapper', async () => {
        const source = createAsync(async () => 10, { lazy: true });
        const mapped = mapAsync(source, async (v) => v * 3);
        expect(await mapped.getInstance()).toBe(30);
        await source.disposeAsync();
        await mapped.disposeAsync();
    });
});

// ---------------------------------------------------------------------------
// combine / combineAsync
// ---------------------------------------------------------------------------
describe('combine', () => {
    it('merges multiple singletons into tuple', () => {
        const a = create(() => 1);
        const b = create(() => 'two');
        const c = create(() => true);
        const combined = combine(a, b, c);
        expect(combined.getInstance()).toEqual([1, 'two', true]);
        a.dispose();
        b.dispose();
        c.dispose();
        combined.dispose();
    });
});

describe('combineAsync', () => {
    it('merges multiple async singletons into tuple', async () => {
        const a = createAsync(async () => 1, { lazy: true });
        const b = createAsync(async () => 'two', { lazy: true });
        const combined = combineAsync(a, b);
        expect(await combined.getInstance()).toEqual([1, 'two']);
        await a.disposeAsync();
        await b.disposeAsync();
        await combined.disposeAsync();
    });
});
