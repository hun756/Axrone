import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { LazyAsyncImpl, LazyImpl } from '../lazy/lazy-impl';
import { delay, delayAsync } from '../lazy/lazy-utils';

describe('LazyAsyncImpl', () => {
    describe('basic async evaluation and caching', () => {
        it('evaluates factory lazily and caches result', async () => {
            let called = 0;
            const lazy = new LazyAsyncImpl(() => {
                called++;
                return Promise.resolve(42);
            });
            expect(called).toBe(0);
            expect(lazy.isValueCreated).toBe(false);

            const result = await lazy.force();
            expect(result).toBe(42);
            expect(called).toBe(1);

            const result2 = await lazy.force();
            expect(result2).toBe(42);
            expect(called).toBe(1);
            expect(lazy.isValueCreated).toBe(true);
        });

        it('returns same promise on repeated calls', async () => {
            const lazy = new LazyAsyncImpl(() => Promise.resolve(10));
            const p1 = lazy.force();
            const p2 = lazy.force();
            expect(p1).toBe(p2);
            await expect(p1).resolves.toBe(10);
        });
    });

    describe('error/fault handling', () => {
        it('caches exception on rejection', async () => {
            const error = new Error('async fail');
            const lazy = new LazyAsyncImpl(() => Promise.reject(error));

            await expect(lazy.force()).rejects.toThrow('async fail');
            expect(lazy.isValueFaulted).toBe(true);
            expect(lazy.exception).toBe(error);

            await expect(lazy.force()).rejects.toThrow('async fail');
        });
    });

    describe('delay helpers', () => {
        beforeEach(() => { vi.useFakeTimers(); });
        afterEach(() => { vi.useRealTimers(); });

        it('delayAsync propagates force rejection', async () => {
            const failing = new LazyAsyncImpl(() => Promise.reject(new Error('boom')));
            const delayed = delayAsync(failing, 1);
            const promise = delayed.force();
            vi.advanceTimersByTime(10);
            await expect(promise).rejects.toThrow('boom');
        });

        it('delay propagates sync force throw', async () => {
            const failing = new LazyImpl(() => {
                throw new Error('sync boom');
            });
            const delayed = delay(failing, 1);
            const promise = delayed.force();
            vi.advanceTimersByTime(10);
            await expect(promise).rejects.toThrow('sync boom');
        });
    });

    describe('map()', () => {
        it('transforms resolved value', async () => {
            const lazy = new LazyAsyncImpl(() => Promise.resolve(5));
            const mapped = lazy.map((x) => x * 2);
            await expect(mapped.force()).resolves.toBe(10);
        });
    });

    describe('mapAsync()', () => {
        it('transforms with async selector', async () => {
            const lazy = new LazyAsyncImpl(() => Promise.resolve(3));
            const mapped = lazy.mapAsync((x) => Promise.resolve(x + 7));
            await expect(mapped.force()).resolves.toBe(10);
        });
    });

    describe('flatMap()', () => {
        it('chains async lazies', async () => {
            const lazy = new LazyAsyncImpl(() => Promise.resolve(5));
            const flatMapped = lazy.flatMap((x) =>
                new LazyAsyncImpl(() => Promise.resolve(x + 10))
            );
            await expect(flatMapped.force()).resolves.toBe(15);
        });
    });

    describe('filter()', () => {
        it('passes when predicate is true', async () => {
            const lazy = new LazyAsyncImpl(() => Promise.resolve(10));
            const filtered = lazy.filter((x) => x > 5);
            await expect(filtered.force()).resolves.toBe(10);
        });

        it('rejects when predicate is false', async () => {
            const lazy = new LazyAsyncImpl(() => Promise.resolve(3));
            const filtered = lazy.filter((x) => x > 5);
            await expect(filtered.force()).rejects.toThrow(/Predicate failed/);
        });
    });

    describe('orElse()', () => {
        it('returns original on success', async () => {
            const lazy = new LazyAsyncImpl(() => Promise.resolve(42));
            const withFallback = lazy.orElse(() => Promise.resolve(0));
            await expect(withFallback.force()).resolves.toBe(42);
        });

        it('returns fallback on failure', async () => {
            const lazy = new LazyAsyncImpl(() => Promise.reject(new Error('fail')));
            const withFallback = lazy.orElse(() => Promise.resolve(99));
            await expect(withFallback.force()).resolves.toBe(99);
        });
    });

    describe('catch()', () => {
        it('recovers from error with sync handler', async () => {
            const lazy = new LazyAsyncImpl(() => Promise.reject(new Error('oops')));
            const caught = lazy.catch((err) => err.message);
            await expect(caught.force()).resolves.toBe('oops');
        });
    });

    describe('catchAsync()', () => {
        it('recovers from error with async handler', async () => {
            const lazy = new LazyAsyncImpl(() => Promise.reject(new Error('async-oops')));
            const caught = lazy.catchAsync((err) => Promise.resolve(`recovered: ${err.message}`));
            await expect(caught.force()).resolves.toBe('recovered: async-oops');
        });
    });

    describe('tap()', () => {
        it('executes side effect without changing value', async () => {
            let sideEffect = 0;
            const lazy = new LazyAsyncImpl(() => Promise.resolve(7));
            const tapped = lazy.tap((x) => {
                sideEffect = x;
            });
            await expect(tapped.force()).resolves.toBe(7);
            expect(sideEffect).toBe(7);
        });
    });

    describe('tapAsync()', () => {
        it('executes async side effect without changing value', async () => {
            let sideEffect = 0;
            const lazy = new LazyAsyncImpl(() => Promise.resolve(11));
            const tapped = lazy.tapAsync(async (x) => {
                sideEffect = x;
            });
            await expect(tapped.force()).resolves.toBe(11);
            expect(sideEffect).toBe(11);
        });
    });

    describe('timeout()', () => {
        beforeEach(() => { vi.useFakeTimers(); });
        afterEach(() => { vi.useRealTimers(); });

        it('resolves when factory completes before timeout', async () => {
            const lazy = new LazyAsyncImpl(() => Promise.resolve(42));
            const withTimeout = lazy.timeout(1000);
            const promise = withTimeout.force();
            await vi.advanceTimersByTimeAsync(0);
            await expect(promise).resolves.toBe(42);
        });

        it('rejects when factory exceeds timeout', async () => {
            const lazy = new LazyAsyncImpl(
                () => new Promise((resolve) => setTimeout(() => resolve(42), 5000))
            );
            const withTimeout = lazy.timeout(10);
            const promise = withTimeout.force();
            vi.advanceTimersByTime(10);
            await expect(promise).rejects.toThrow(/timed out/);
        });
    });

    describe('retry()', () => {
        it('succeeds after failures within attempts', async () => {
            let attempt = 0;
            const lazy = new LazyAsyncImpl(() => {
                attempt++;
                if (attempt < 3) return Promise.reject(new Error(`fail ${attempt}`));
                return Promise.resolve('success');
            });
            const retried = lazy.retry(5);
            await expect(retried.force()).resolves.toBe('success');
            expect(attempt).toBe(3);
        });

        it('exhausts all attempts and throws last error', async () => {
            const lazy = new LazyAsyncImpl(() => Promise.reject(new Error('always fails')));
            const retried = lazy.retry(2);
            await expect(retried.force()).rejects.toThrow('always fails');
        });
    });

    describe('force() / reset() / toLazy()', () => {
        it('force returns the promise', async () => {
            const lazy = new LazyAsyncImpl(() => Promise.resolve(1));
            await expect(lazy.force()).resolves.toBe(1);
        });

        it('reset creates a fresh instance', async () => {
            let count = 0;
            const lazy = new LazyAsyncImpl(() => {
                count++;
                return Promise.resolve(count);
            });
            await expect(lazy.force()).resolves.toBe(1);
            const reset = lazy.reset();
            await expect(reset.force()).resolves.toBe(2);
        });

        it('toLazy wraps as sync lazy returning promise', async () => {
            const lazy = new LazyAsyncImpl(() => Promise.resolve(99));
            const syncLazy = lazy.toLazy();
            const promise = syncLazy.force();
            await expect(promise).resolves.toBe(99);
        });
    });

    describe('Value / IsValueCreated / IsValueFaulted', () => {
        it('exposes metadata properties', async () => {
            const lazy = new LazyAsyncImpl(() => Promise.resolve(5));
            expect(lazy.IsValueCreated).toBe(false);
            expect(lazy.IsValueFaulted).toBe(false);
            await lazy.Value;
            expect(lazy.IsValueCreated).toBe(true);
        });
    });
});
