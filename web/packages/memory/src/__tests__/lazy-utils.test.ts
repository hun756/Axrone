import { describe, expect, it } from 'vitest';
import {
    create,
    createAsync,
    fromValue,
    fromPromise,
    createFactory,
    isLazy,
    isLazyAsync,
    isLazyFactory,
    combine,
    combineAsync,
    sequence,
    sequenceAsync,
    traverseSync,
    traverseAsync,
    race,
    all,
    allSettled,
    when,
    unless,
    tryLazy,
    tryAsync,
    memoize,
    memoizeAsync,
    delay,
    delayAsync,
    withTimeout,
    withRetry,
    empty,
    emptyAsync,
    never,
} from '../lazy';
import { LazyFactoryImpl } from '../lazy/lazy-factory';

describe('lazy-utils', () => {
    describe('create() / fromValue()', () => {
        it('create evaluates factory on demand', () => {
            let called = 0;
            const lazy = create(() => { called++; return 42; });
            expect(called).toBe(0);
            expect(lazy.force()).toBe(42);
            expect(called).toBe(1);
        });

        it('fromValue wraps a static value', () => {
            const lazy = fromValue('hello');
            expect(lazy.force()).toBe('hello');
        });
    });

    describe('createAsync() / fromPromise()', () => {
        it('createAsync wraps async factory', async () => {
            const lazy = createAsync(() => Promise.resolve(42));
            await expect(lazy.force()).resolves.toBe(42);
        });

        it('fromPromise wraps a promise', async () => {
            const lazy = fromPromise(Promise.resolve('test'));
            await expect(lazy.force()).resolves.toBe('test');
        });
    });

    describe('createFactory()', () => {
        it('returns a LazyFactoryImpl', () => {
            const factory = createFactory((x: number) => x * 2);
            expect(factory).toBeInstanceOf(LazyFactoryImpl);
            expect(factory.getOrAdd(5)).toBe(10);
        });
    });

    describe('isLazy() / isLazyAsync() / isLazyFactory()', () => {
        it('isLazy returns true for lazy values', () => {
            expect(isLazy(fromValue(1))).toBe(true);
            expect(isLazy(create(() => 1))).toBe(true);
            expect(isLazy(null)).toBe(false);
            expect(isLazy(42)).toBe(false);
            expect(isLazy({})).toBe(false);
        });

        it('isLazyAsync returns true for async lazy values', () => {
            expect(isLazyAsync(createAsync(() => Promise.resolve(1)))).toBe(true);
            expect(isLazyAsync(fromPromise(Promise.resolve(1)))).toBe(true);
            expect(isLazyAsync(fromValue(1))).toBe(false);
            expect(isLazyAsync(null)).toBe(false);
        });

        it('isLazyFactory returns true for factory values', () => {
            expect(isLazyFactory(createFactory((x: number) => x))).toBe(true);
            expect(isLazyFactory(fromValue(1))).toBe(false);
            expect(isLazyFactory(null)).toBe(false);
        });
    });

    describe('combine() / combineAsync()', () => {
        it('combine merges multiple lazies into a tuple', () => {
            const a = fromValue(1);
            const b = fromValue('hello');
            const combined = combine(a, b);
            const result = combined.force();
            expect(result).toEqual([1, 'hello']);
        });

        it('combineAsync merges multiple async lazies', async () => {
            const a = createAsync(() => Promise.resolve(10));
            const b = createAsync(() => Promise.resolve(20));
            const combined = combineAsync(a, b);
            const result = await combined.force();
            expect(result).toEqual([10, 20]);
        });
    });

    describe('sequence() / sequenceAsync()', () => {
        it('sequence converts array of lazies to lazy of array', () => {
            const lazies = [fromValue(1), fromValue(2), fromValue(3)] as const;
            const seq = sequence(lazies);
            expect(seq.force()).toEqual([1, 2, 3]);
        });

        it('sequenceAsync converts array of async lazies', async () => {
            const lazies = [
                createAsync(() => Promise.resolve('a')),
                createAsync(() => Promise.resolve('b')),
            ] as const;
            const seq = sequenceAsync(lazies);
            await expect(seq.force()).resolves.toEqual(['a', 'b']);
        });
    });

    describe('traverseSync() / traverseAsync()', () => {
        it('traverseSync maps items to lazies and collects', () => {
            const result = traverseSync([1, 2, 3], (x) => fromValue(x * 10));
            expect(result.force()).toEqual([10, 20, 30]);
        });

        it('traverseAsync maps items to async lazies', async () => {
            const result = traverseAsync([1, 2, 3], (x) =>
                createAsync(() => Promise.resolve(x * 10))
            );
            await expect(result.force()).resolves.toEqual([10, 20, 30]);
        });
    });

    describe('race()', () => {
        it('resolves with first completed lazy', async () => {
            const fast = createAsync(() => Promise.resolve('fast'));
            const slow = createAsync(
                () => new Promise((resolve) => setTimeout(() => resolve('slow'), 1000))
            );
            const result = race(fast, slow);
            await expect(result.force()).resolves.toBe('fast');
        });
    });

    describe('all() / allSettled()', () => {
        it('all resolves when all resolve', async () => {
            const lazies = [
                createAsync(() => Promise.resolve(1)),
                createAsync(() => Promise.resolve(2)),
            ] as const;
            const result = all(lazies);
            await expect(result.force()).resolves.toEqual([1, 2]);
        });

        it('allSettled returns settled results', async () => {
            const lazies = [
                createAsync(() => Promise.resolve('ok')),
                createAsync(() => Promise.reject(new Error('fail'))),
            ] as const;
            const result = allSettled(lazies);
            const settled = await result.force();
            expect(settled[0]).toEqual({ status: 'fulfilled', value: 'ok' });
            expect((settled[1] as PromiseRejectedResult).reason).toBeInstanceOf(Error);
        });
    });

    describe('when() / unless()', () => {
        it('when returns value if condition is true', () => {
            const result = when(true, fromValue(42));
            expect(result.force()).toBe(42);
        });

        it('when returns undefined if condition is false', () => {
            const result = when(false, fromValue(42));
            expect(result.force()).toBeUndefined();
        });

        it('unless returns value if condition is false', () => {
            const result = unless(false, fromValue(42));
            expect(result.force()).toBe(42);
        });

        it('unless returns undefined if condition is true', () => {
            const result = unless(true, fromValue(42));
            expect(result.force()).toBeUndefined();
        });
    });

    describe('tryAsync()', () => {
        it('wraps successful async result', async () => {
            const result = tryAsync(() => Promise.resolve(42));
            await expect(result.force()).resolves.toBe(42);
        });

        it('wraps async error as Error', async () => {
            const result = tryAsync(() => Promise.reject(new Error('fail')));
            const val = await result.force();
            expect(val).toBeInstanceOf(Error);
            expect((val as Error).message).toBe('fail');
        });
    });

    describe('memoize() / memoizeAsync()', () => {
        it('memoize caches function results', () => {
            let calls = 0;
            const fn = memoize((x: number) => {
                calls++;
                return x * 2;
            });
            expect(fn(5)).toBe(10);
            expect(fn(5)).toBe(10);
            expect(calls).toBe(1);
            expect(fn(3)).toBe(6);
            expect(calls).toBe(2);
        });

        it('memoizeAsync caches async function results', async () => {
            let calls = 0;
            const fn = memoizeAsync(async (x: number) => {
                calls++;
                return x * 3;
            });
            expect(await fn(5)).toBe(15);
            expect(await fn(5)).toBe(15);
            expect(calls).toBe(1);
        });
    });

    describe('delay() / delayAsync()', () => {
        it('delay defers evaluation', async () => {
            const lazy = fromValue(42);
            const delayed = delay(lazy, 10);
            await expect(delayed.force()).resolves.toBe(42);
        });

        it('delayAsync defers async evaluation', async () => {
            const lazy = createAsync(() => Promise.resolve(99));
            const delayed = delayAsync(lazy, 10);
            await expect(delayed.force()).resolves.toBe(99);
        });
    });

    describe('withTimeout() / withRetry()', () => {
        it('withTimeout delegates to timeout()', async () => {
            const lazy = createAsync(() => Promise.resolve(42));
            const result = withTimeout(lazy, 1000);
            await expect(result.force()).resolves.toBe(42);
        });

        it('withRetry delegates to retry()', async () => {
            let attempt = 0;
            const lazy = createAsync(() => {
                attempt++;
                if (attempt < 2) return Promise.reject(new Error('fail'));
                return Promise.resolve('ok');
            });
            const result = withRetry(lazy, 3);
            await expect(result.force()).resolves.toBe('ok');
        });
    });

    describe('empty() / emptyAsync() / never()', () => {
        it('empty returns lazy of empty array', () => {
            const result = empty();
            expect(result.force()).toEqual([]);
        });

        it('emptyAsync returns async lazy of empty array', async () => {
            const result = emptyAsync();
            await expect(result.force()).resolves.toEqual([]);
        });

        it('never returns a promise that never resolves', () => {
            const result = never();
            const promise = result.force();
            expect(promise).toBeInstanceOf(Promise);
            // We can't test that it never resolves, but we can verify it's a promise
        });
    });
});
