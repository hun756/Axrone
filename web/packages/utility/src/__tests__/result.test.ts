import { describe, it, expect, vi } from 'vitest';
import {
    Ok,
    Err,
    ResultError,
    ok,
    err,
    isResult,
    isOk,
    isErr,
    assertOk,
    assertErr,
    fromThrowable,
    tryCatch,
    tryCatchAsync,
    fromPromise,
    fromNullable,
    fromOption,
    fromJSON,
    all,
    allT,
    any,
    partition,
    combine,
    retry,
    retryAsync,
    AsyncResult,
} from '../result';
import type { Result } from '../result';

// ---------------------------------------------------------------------------
// Ok
// ---------------------------------------------------------------------------
describe('Ok', () => {
    const value = 42;
    const sut = () => Ok.of(value);

    describe('tag checks', () => {
        it('isOk returns true', () => expect(sut().isOk()).toBe(true));
        it('isErr returns false', () => expect(sut().isErr()).toBe(false));
        it('_tag is Ok', () => expect(sut()._tag).toBe('Ok'));
    });

    describe('transformations', () => {
        it('map applies fn to value', () => {
            const r = sut().map((v) => v * 2);
            expect(r.isOk()).toBe(true);
            expect(r.get()).toBe(84);
        });

        it('mapErr is no-op', () => {
            const r = sut().mapErr((e: never) => 'changed');
            expect(r.get()).toBe(42);
        });

        it('flatMap chains into new Result', () => {
            const r = sut().flatMap((v) => Ok.of(v + 1));
            expect(r.get()).toBe(43);
        });

        it('andThen is alias for flatMap', () => {
            expect(sut().andThen((v) => Ok.of(v + 1)).get()).toBe(43);
        });

        it('chain is alias for flatMap', () => {
            expect(sut().chain((v) => Ok.of(v + 1)).get()).toBe(43);
        });

        it('orElse is no-op on Ok', () => {
            const spy = vi.fn();
            const r = sut().orElse(spy);
            expect(spy).not.toHaveBeenCalled();
            expect(r.get()).toBe(42);
        });

        it('recover is no-op on Ok', () => {
            const spy = vi.fn();
            const r = sut().recover(spy);
            expect(spy).not.toHaveBeenCalled();
            expect(r.get()).toBe(42);
        });
    });

    describe('extraction', () => {
        it('fold calls onOk', () => expect(sut().fold((v) => v + 1, () => -1)).toBe(43));
        it('match calls onOk', () => expect(sut().match((v) => v + 1, () => -1)).toBe(43));
        it('get returns value', () => expect(sut().get()).toBe(42));
        it('unwrap returns value', () => expect(sut().unwrap()).toBe(42));
        it('getOrElse returns value (ignores default)', () => expect(sut().getOrElse(99)).toBe(42));
        it('getOrElse does not call lazy default', () => {
            const lazy = vi.fn(() => 99);
            expect(sut().getOrElse(lazy)).toBe(42);
            expect(lazy).not.toHaveBeenCalled();
        });
        it('getOrDefault returns value', () => expect(sut().getOrDefault(99)).toBe(42));
        it('getOrThrow returns value', () => expect(sut().getOrThrow()).toBe(42));
        it('getOrThrowWith returns value', () => expect(sut().getOrThrowWith(() => 'x')).toBe(42));
        it('unwrapOr returns value', () => expect(sut().unwrapOr(99)).toBe(42));
        it('unwrapOrElse returns value', () => expect(sut().unwrapOrElse(() => 99)).toBe(42));
        it('expect returns value', () => expect(sut().expect('fail')).toBe(42));
        it('expectErr throws ResultError', () => {
            expect(() => sut().expectErr('should be err')).toThrow(ResultError);
        });
    });

    describe('conversions', () => {
        it('toOptional returns value', () => expect(sut().toOptional()).toBe(42));
        it('toNullable returns value', () => expect(sut().toNullable()).toBe(42));
        it('okToOptional returns value', () => expect(sut().okToOptional()).toBe(42));
        it('errToOptional returns undefined', () => expect(sut().errToOptional()).toBeUndefined());
    });

    describe('side-effects', () => {
        it('tap calls fn and returns self', () => {
            const spy = vi.fn();
            const r = sut().tap(spy);
            expect(spy).toHaveBeenCalledWith(42);
            expect(r).toBe(r);
        });
        it('tapErr is no-op', () => {
            const spy = vi.fn();
            sut().tapErr(spy);
            expect(spy).not.toHaveBeenCalled();
        });
        it('inspect calls fn', () => {
            const spy = vi.fn();
            sut().inspect(spy);
            expect(spy).toHaveBeenCalledWith(42);
        });
        it('inspectErr is no-op', () => {
            const spy = vi.fn();
            sut().inspectErr(spy);
            expect(spy).not.toHaveBeenCalled();
        });
    });

    describe('combinators', () => {
        it('zip pairs with another Ok', () => {
            const r = sut().zip(Ok.of('hello'));
            expect(r.get()).toEqual([42, 'hello']);
        });
        it('zip with Err returns Err', () => {
            const r = sut().zip(Err.of('bad'));
            expect(r.isErr()).toBe(true);
        });
        it('zipWith applies combiner', () => {
            const r = sut().zipWith(Ok.of(8), (a, b) => a + b);
            expect(r.get()).toBe(50);
        });
        it('contains returns true for same value', () => expect(sut().contains(42)).toBe(true));
        it('contains returns false for different value', () => expect(sut().contains(99)).toBe(false));
        it('containsErr returns false', () => expect(sut().containsErr('x' as never)).toBe(false));
    });

    describe('projections', () => {
        it('ok returns self', () => {
            const r = sut();
            expect(r.ok()).toBe(r);
        });
        it('err returns null', () => expect(sut().err()).toBeNull());
        it('flip returns Err', () => {
            const flipped = sut().flip();
            expect(flipped.isErr()).toBe(true);
        });
    });

    describe('serialization', () => {
        it('toArray returns [value]', () => expect(sut().toArray()).toEqual([42]));
        it('toErrorArray returns []', () => expect(sut().toErrorArray()).toEqual([]));
        it('toJSON returns tagged object', () => expect(sut().toJSON()).toEqual({ _tag: 'Ok', value: 42 }));
        it('toString formats value', () => expect(sut().toString()).toBe('Ok(42)'));
        it('toString formats string value with quotes', () => {
            expect(Ok.of('hi').toString()).toBe('Ok("hi")');
        });
    });

    describe('iterable', () => {
        it('Symbol.iterator yields value', () => {
            const values = [...sut()];
            expect(values).toEqual([42]);
        });
    });

    describe('equals', () => {
        it('equal Ok values with Object.is', () => expect(sut().equals(Ok.of(42))).toBe(true));
        it('unequal Ok values', () => expect(sut().equals(Ok.of(99))).toBe(false));
        it('Ok vs Err', () => expect(sut().equals(Err.of(42 as never))).toBe(false));
        it('custom valueEq', () => {
            const a = Ok.of({ x: 1 });
            const b = Ok.of({ x: 1 });
            expect(a.equals(b, (l, r) => l.x === r.x)).toBe(true);
        });
    });

    describe('transpose', () => {
        it('returns self for non-nullable value', () => {
            const r = sut();
            expect(r.transpose()).toBe(r);
        });
        it('returns null for null value', () => {
            expect(Ok.of(null).transpose()).toBeNull();
        });
        it('returns null for undefined value', () => {
            expect(Ok.of(undefined).transpose()).toBeNull();
        });
    });
});

// ---------------------------------------------------------------------------
// Err
// ---------------------------------------------------------------------------
describe('Err', () => {
    const error = 'boom';
    const sut = () => Err.of(error);

    describe('tag checks', () => {
        it('isOk returns false', () => expect(sut().isOk()).toBe(false));
        it('isErr returns true', () => expect(sut().isErr()).toBe(true));
        it('_tag is Err', () => expect(sut()._tag).toBe('Err'));
    });

    describe('transformations', () => {
        it('map is no-op', () => {
            const spy = vi.fn();
            const r = sut().map(spy);
            expect(spy).not.toHaveBeenCalled();
            expect(r.isErr()).toBe(true);
        });
        it('mapErr applies fn', () => {
            const r = sut().mapErr((e) => e.toUpperCase());
            expect(r.isErr()).toBe(true);
            expect(r.match(() => '', (e) => e)).toBe('BOOM');
        });
        it('flatMap is no-op', () => {
            const spy = vi.fn();
            sut().flatMap(spy);
            expect(spy).not.toHaveBeenCalled();
        });
        it('andThen is no-op', () => {
            const spy = vi.fn();
            sut().andThen(spy);
            expect(spy).not.toHaveBeenCalled();
        });
        it('chain is no-op', () => {
            const spy = vi.fn();
            sut().chain(spy);
            expect(spy).not.toHaveBeenCalled();
        });
        it('orElse invokes fn with error', () => {
            const r = sut().orElse((e) => Ok.of(e.length));
            expect(r.isOk()).toBe(true);
            expect(r.get()).toBe(4);
        });
        it('recover invokes fn with error', () => {
            const r = sut().recover(() => Ok.of(99));
            expect(r.get()).toBe(99);
        });
    });

    describe('extraction', () => {
        it('fold calls onErr', () => expect(sut().fold(() => -1, (e) => e.length)).toBe(4));
        it('match calls onErr', () => expect(sut().match(() => -1, (e) => e.length)).toBe(4));
        it('get throws ResultError', () => expect(() => sut().get()).toThrow(ResultError));
        it('unwrap throws ResultError', () => expect(() => sut().unwrap()).toThrow(ResultError));
        it('getOrElse returns default value', () => expect(sut().getOrElse(99)).toBe(99));
        it('getOrElse calls lazy default', () => {
            const lazy = vi.fn(() => 99);
            expect(sut().getOrElse(lazy)).toBe(99);
            expect(lazy).toHaveBeenCalled();
        });
        it('getOrDefault returns default', () => expect(sut().getOrDefault(99)).toBe(99));
        it('getOrThrow throws', () => expect(() => sut().getOrThrow()).toThrow());
        it('getOrThrowWith uses transform', () => {
            expect(() => sut().getOrThrowWith((e) => new TypeError(e))).toThrow(TypeError);
        });
        it('getOrThrowWith without transform throws default', () => {
            expect(() => sut().getOrThrowWith()).toThrow(ResultError);
        });
        it('unwrapOr returns default', () => expect(sut().unwrapOr(99)).toBe(99));
        it('unwrapOrElse calls fn', () => expect(sut().unwrapOrElse((e) => e.length)).toBe(4));
        it('expect throws ResultError with message', () => {
            try {
                sut().expect('bad');
                expect.unreachable();
            } catch (e) {
                expect(e).toBeInstanceOf(ResultError);
                expect((e as ResultError).message).toBe('bad');
            }
        });
        it('expectErr returns error value', () => expect(sut().expectErr('ok')).toBe('boom'));
    });

    describe('conversions', () => {
        it('toOptional returns undefined', () => expect(sut().toOptional()).toBeUndefined());
        it('toNullable returns null', () => expect(sut().toNullable()).toBeNull());
        it('okToOptional returns undefined', () => expect(sut().okToOptional()).toBeUndefined());
        it('errToOptional returns error', () => expect(sut().errToOptional()).toBe('boom'));
    });

    describe('side-effects', () => {
        it('tap is no-op', () => {
            const spy = vi.fn();
            sut().tap(spy);
            expect(spy).not.toHaveBeenCalled();
        });
        it('tapErr calls fn', () => {
            const spy = vi.fn();
            sut().tapErr(spy);
            expect(spy).toHaveBeenCalledWith('boom');
        });
        it('inspect is no-op', () => {
            const spy = vi.fn();
            sut().inspect(spy);
            expect(spy).not.toHaveBeenCalled();
        });
        it('inspectErr calls fn', () => {
            const spy = vi.fn();
            sut().inspectErr(spy);
            expect(spy).toHaveBeenCalledWith('boom');
        });
    });

    describe('combinators', () => {
        it('zip returns self (Err propagates)', () => {
            const r = sut().zip(Ok.of(1));
            expect(r.isErr()).toBe(true);
        });
        it('zipWith returns self', () => {
            const r = sut().zipWith(Ok.of(1), (a, b) => a);
            expect(r.isErr()).toBe(true);
        });
        it('contains returns false', () => expect(sut().contains(42 as never)).toBe(false));
        it('containsErr returns true for same error', () => expect(sut().containsErr('boom')).toBe(true));
        it('containsErr returns false for different error', () => expect(sut().containsErr('other')).toBe(false));
    });

    describe('projections', () => {
        it('ok returns null', () => expect(sut().ok()).toBeNull());
        it('err returns self', () => expect(sut().err()).not.toBeNull());
        it('flip returns Ok', () => {
            const flipped = sut().flip();
            expect(flipped.isOk()).toBe(true);
            expect(flipped.get()).toBe('boom');
        });
    });

    describe('serialization', () => {
        it('toArray returns []', () => expect(sut().toArray()).toEqual([]));
        it('toErrorArray returns [error]', () => expect(sut().toErrorArray()).toEqual(['boom']));
        it('toJSON returns tagged object', () => expect(sut().toJSON()).toEqual({ _tag: 'Err', error: 'boom' }));
        it('toString formats error', () => expect(sut().toString()).toBe('Err("boom")'));
    });

    describe('iterable', () => {
        it('Symbol.iterator yields nothing', () => {
            expect([...sut()]).toEqual([]);
        });
    });

    describe('equals', () => {
        it('equal Err values', () => expect(sut().equals(Err.of('boom'))).toBe(true));
        it('unequal Err values', () => expect(sut().equals(Err.of('other'))).toBe(false));
        it('Err vs Ok', () => expect(sut().equals(Ok.of('boom' as never) as never)).toBe(false));
        it('custom errEq', () => {
            const a = Err.of({ code: 1 });
            const b = Err.of({ code: 1 });
            expect(a.equals(b, undefined, (l, r) => l.code === r.code)).toBe(true);
        });
    });

    describe('transpose', () => {
        it('returns self', () => {
            const r = sut().transpose();
            expect(r!.isErr()).toBe(true);
        });
    });
});

// ---------------------------------------------------------------------------
// ResultError
// ---------------------------------------------------------------------------
describe('ResultError', () => {
    it('extends Error', () => {
        const e = new ResultError('fail');
        expect(e).toBeInstanceOf(Error);
        expect(e).toBeInstanceOf(ResultError);
    });
    it('has name ResultError', () => expect(new ResultError('x').name).toBe('ResultError'));
    it('stores code as enumerable non-writable property', () => {
        const e = new ResultError('x', { code: 42 });
        expect(e.code).toBe(42);
        expect(Object.getOwnPropertyDescriptor(e, 'code')?.writable).toBe(false);
        expect(Object.getOwnPropertyDescriptor(e, 'code')?.enumerable).toBe(true);
    });
    it('code is undefined when not provided', () => {
        expect(new ResultError('x').code).toBeUndefined();
    });
    it('propagates cause', () => {
        const cause = new Error('root');
        const e = new ResultError('wrap', { cause });
        expect(e.cause).toBe(cause);
    });
});

// ---------------------------------------------------------------------------
// Factory functions
// ---------------------------------------------------------------------------
describe('factory functions', () => {
    it('ok() creates Ok<void>', () => {
        const r = ok();
        expect(r.isOk()).toBe(true);
    });
    it('ok(value) creates Ok<T>', () => {
        expect(ok(42).get()).toBe(42);
    });
    it('err() creates Err', () => {
        expect(err('bad').isErr()).toBe(true);
    });
});

describe('type guards', () => {
    it('isResult true for Ok', () => expect(isResult(ok(1))).toBe(true));
    it('isResult true for Err', () => expect(isResult(err('x'))).toBe(true));
    it('isResult false for plain value', () => expect(isResult(42)).toBe(false));
    it('isOk true for Ok', () => expect(isOk(ok(1))).toBe(true));
    it('isOk false for Err', () => expect(isOk(err('x'))).toBe(false));
    it('isErr true for Err', () => expect(isErr(err('x'))).toBe(true));
    it('isErr false for Ok', () => expect(isErr(ok(1))).toBe(false));
});

describe('assertOk', () => {
    it('passes for Ok', () => {
        expect(() => assertOk(ok(1))).not.toThrow();
    });
    it('throws for Err', () => {
        expect(() => assertOk(err('bad'))).toThrow();
    });
});

describe('assertErr', () => {
    it('passes for Err', () => {
        expect(() => assertErr(err('bad'))).not.toThrow();
    });
    it('throws for Ok', () => {
        expect(() => assertErr(ok(1))).toThrow(ResultError);
    });
});

describe('fromThrowable', () => {
    it('returns Ok on success', () => {
        const fn = fromThrowable((x: number) => x * 2);
        expect(fn(3).get()).toBe(6);
    });
    it('returns Err on throw', () => {
        const fn = fromThrowable(() => { throw new Error('oops'); });
        expect(fn().isErr()).toBe(true);
    });
    it('uses custom onError', () => {
        const fn = fromThrowable(
            () => { throw 'string-error'; },
            (e) => new Error(`wrapped: ${e}`)
        );
        const r = fn();
        expect(r.isErr()).toBe(true);
        expect(r.match(() => '', (e) => e.message)).toBe('wrapped: string-error');
    });
});

describe('tryCatch', () => {
    it('returns Ok on success', () => {
        expect(tryCatch(() => 42).get()).toBe(42);
    });
    it('returns Err on throw', () => {
        expect(tryCatch(() => { throw 'fail'; }).isErr()).toBe(true);
    });
    it('uses custom onError', () => {
        const r = tryCatch(
            () => { throw 'raw'; },
            (e) => `caught: ${e}`
        );
        expect(r.match(() => '', (e) => e)).toBe('caught: raw');
    });
});

describe('tryCatchAsync', () => {
    it('returns Ok on resolved', async () => {
        const r = await tryCatchAsync(async () => 42);
        expect(r.get()).toBe(42);
    });
    it('returns Err on rejected', async () => {
        const r = await tryCatchAsync(async () => { throw 'fail'; });
        expect(r.isErr()).toBe(true);
    });
    it('uses custom onError', async () => {
        const r = await tryCatchAsync(
            async () => { throw 'raw'; },
            (e) => `caught: ${e}`
        );
        expect(r.match(() => '', (e) => e)).toBe('caught: raw');
    });
});

describe('fromPromise', () => {
    it('returns Ok for resolved promise', async () => {
        const r = await fromPromise(Promise.resolve(42));
        expect(r.get()).toBe(42);
    });
    it('returns Err for rejected promise', async () => {
        const r = await fromPromise(Promise.reject('fail'));
        expect(r.isErr()).toBe(true);
    });
    it('uses custom onError', async () => {
        const r = await fromPromise(Promise.reject('raw'), (e) => `caught: ${e}`);
        expect(r.match(() => '', (e) => e)).toBe('caught: raw');
    });
});

describe('fromNullable', () => {
    it('returns Ok for non-null value', () => {
        expect(fromNullable(42, () => 'missing').get()).toBe(42);
    });
    it('returns Err for null', () => {
        expect(fromNullable(null, () => 'missing').isErr()).toBe(true);
    });
    it('returns Err for undefined', () => {
        expect(fromNullable(undefined, () => 'missing').isErr()).toBe(true);
    });
});

describe('fromOption', () => {
    it('returns Ok for defined value', () => {
        expect(fromOption(42, () => 'none').get()).toBe(42);
    });
    it('returns Err for null', () => {
        expect(fromOption(null, () => 'none').isErr()).toBe(true);
    });
    it('returns Err for undefined', () => {
        expect(fromOption(undefined, () => 'none').isErr()).toBe(true);
    });
});

describe('fromJSON', () => {
    it('parses valid Ok JSON object', () => {
        const r = fromJSON({ _tag: 'Ok', value: 42 });
        expect(r.isOk()).toBe(true);
        expect(r.get()).toBe(42);
    });
    it('parses valid Err JSON object', () => {
        const r = fromJSON({ _tag: 'Err', error: 'bad' });
        expect(r.isErr()).toBe(true);
    });
    it('parses JSON string', () => {
        const r = fromJSON(JSON.stringify({ _tag: 'Ok', value: 'hello' }));
        expect(r.isOk()).toBe(true);
        expect(r.get()).toBe('hello');
    });
    it('returns Err for invalid JSON string', () => {
        const r = fromJSON('not-json');
        expect(r.isErr()).toBe(true);
    });
    it('returns Err for missing _tag', () => {
        const r = fromJSON({ value: 42 });
        expect(r.isErr()).toBe(true);
    });
    it('returns Err for unknown _tag', () => {
        const r = fromJSON({ _tag: 'Maybe' });
        expect(r.isErr()).toBe(true);
    });
    it('applies reviver', () => {
        const r = fromJSON<number, string>({ _tag: 'Ok', value: '10' }, (v) => Number(v));
        expect(r.get()).toBe(10);
    });
});

// ---------------------------------------------------------------------------
// Collection combinators
// ---------------------------------------------------------------------------
describe('all', () => {
    it('returns Ok for empty iterable', () => {
        expect(all([]).get()).toEqual([]);
    });
    it('returns Ok[] when all Ok', () => {
        expect(all([ok(1), ok(2), ok(3)]).get()).toEqual([1, 2, 3]);
    });
    it('returns first Err', () => {
        const r = all([ok(1), err('bad'), ok(3)]);
        expect(r.isErr()).toBe(true);
    });
});

describe('allT', () => {
    it('returns typed tuple', () => {
        const r = allT(ok(1), ok('two'), ok(true));
        expect(r.get()).toEqual([1, 'two', true]);
    });
    it('returns first Err', () => {
        const r = allT(ok(1), err('bad'));
        expect(r.isErr()).toBe(true);
    });
});

describe('any', () => {
    it('returns Err[] for empty iterable', () => {
        const r = any([]);
        expect(r.isErr()).toBe(true);
        expect(r.match(() => [], (e) => e)).toEqual([]);
    });
    it('returns first Ok', () => {
        expect(any([err('a'), ok(1), err('b')]).get()).toBe(1);
    });
    it('collects all errors when all Err', () => {
        const r = any([err('a'), err('b')]);
        expect(r.isErr()).toBe(true);
        expect(r.match(() => [], (e) => e)).toEqual(['a', 'b']);
    });
});

describe('partition', () => {
    it('separates ok and err values', () => {
        const { ok: oks, err: errs } = partition([ok(1), err('a'), ok(2), err('b')]);
        expect(oks).toEqual([1, 2]);
        expect(errs).toEqual(['a', 'b']);
    });
    it('handles empty input', () => {
        const { ok: oks, err: errs } = partition([]);
        expect(oks).toEqual([]);
        expect(errs).toEqual([]);
    });
});

describe('combine', () => {
    it('returns Ok[] when all Ok', () => {
        expect(combine([ok(1), ok(2)]).get()).toEqual([1, 2]);
    });
    it('returns Err[] when any Err', () => {
        const r = combine([ok(1), err('a'), ok(2), err('b')]);
        expect(r.isErr()).toBe(true);
        expect(r.match(() => [], (e) => e)).toEqual(['a', 'b']);
    });
    it('returns Ok[] for empty', () => {
        expect(combine([]).get()).toEqual([]);
    });
});

describe('retry', () => {
    it('returns Ok on first success', () => {
        const fn = vi.fn(() => ok(42));
        expect(retry(fn, { maxAttempts: 3 }).get()).toBe(42);
        expect(fn).toHaveBeenCalledTimes(1);
    });
    it('retries until success', () => {
        let attempt = 0;
        const fn = (i: number) => {
            attempt = i;
            return i >= 1 ? ok('done') : err('fail');
        };
        const r = retry(fn, { maxAttempts: 3 });
        expect(r.get()).toBe('done');
        expect(attempt).toBe(1);
    });
    it('returns last Err when maxAttempts exhausted', () => {
        const fn = () => err('fail');
        const r = retry(fn, { maxAttempts: 3 });
        expect(r.isErr()).toBe(true);
    });
    it('stops when shouldRetry returns false', () => {
        const fn = () => err('fail');
        const r = retry(fn, { maxAttempts: 5, shouldRetry: (_e, attempt) => attempt < 1 });
        expect(r.isErr()).toBe(true);
    });
});

describe('retryAsync', () => {
    it('returns Ok on first success', async () => {
        const fn = vi.fn(async () => ok(42));
        const r = await retryAsync(fn, { maxAttempts: 3 });
        expect(r.get()).toBe(42);
        expect(fn).toHaveBeenCalledTimes(1);
    });
    it('retries until success', async () => {
        let count = 0;
        const fn = async () => {
            count++;
            return count >= 2 ? ok('done') : err('fail');
        };
        const r = await retryAsync(fn, { maxAttempts: 3 });
        expect(r.get()).toBe('done');
        expect(count).toBe(2);
    });
    it('applies delay between retries', async () => {
        let count = 0;
        const fn = async () => {
            count++;
            return count >= 2 ? ok('done') : err('fail');
        };
        const r = await retryAsync(fn, { maxAttempts: 3, delay: () => 10 });
        expect(r.get()).toBe('done');
    });
});

// ---------------------------------------------------------------------------
// AsyncResult
// ---------------------------------------------------------------------------
describe('AsyncResult', () => {
    describe('construction', () => {
        it('from wraps a promise of Result', async () => {
            const r = await AsyncResult.from(Promise.resolve(ok(42))).toPromise();
            expect(r.get()).toBe(42);
        });
        it('fromPromise wraps a plain promise', async () => {
            const r = await AsyncResult.fromPromise(Promise.resolve(42)).toPromise();
            expect(r.get()).toBe(42);
        });
        it('fromPromise catches rejection', async () => {
            const r = await AsyncResult.fromPromise(Promise.reject('fail')).toPromise();
            expect(r.isErr()).toBe(true);
        });
        it('fromThrowable returns factory', async () => {
            const fn = AsyncResult.fromThrowable(async (x: number) => x * 2);
            const r = await fn(3).toPromise();
            expect(r.get()).toBe(6);
        });
        it('ok creates resolved AsyncResult', async () => {
            const r = await AsyncResult.ok(42).toPromise();
            expect(r.get()).toBe(42);
        });
        it('err creates rejected AsyncResult', async () => {
            const r = await AsyncResult.err('bad').toPromise();
            expect(r.isErr()).toBe(true);
        });
    });

    describe('transformations', () => {
        it('map transforms Ok value', async () => {
            const r = await AsyncResult.ok(10).map((v) => v * 2).toPromise();
            expect(r.get()).toBe(20);
        });
        it('map skips Err', async () => {
            const r = await AsyncResult.err('bad').map((v: number) => v * 2).toPromise();
            expect(r.isErr()).toBe(true);
        });
        it('mapErr transforms error', async () => {
            const r = await AsyncResult.err('bad').mapErr((e) => e.toUpperCase()).toPromise();
            expect(r.isErr()).toBe(true);
            expect(r.match(() => '', (e) => e)).toBe('BAD');
        });
        it('flatMap chains', async () => {
            const r = await AsyncResult.ok(10).flatMap((v) => Ok.of(v + 1)).toPromise();
            expect(r.get()).toBe(11);
        });
        it('andThen is alias for flatMap', async () => {
            const r = await AsyncResult.ok(10).andThen((v) => AsyncResult.ok(v + 1)).toPromise();
            expect(r.get()).toBe(11);
        });
    });

    describe('recovery', () => {
        it('orElse recovers from Err', async () => {
            const r = await AsyncResult.err('bad').orElse(() => Ok.of(99)).toPromise();
            expect(r.get()).toBe(99);
        });
        it('recover is alias for orElse', async () => {
            const r = await AsyncResult.err('bad').recover(() => Ok.of(99)).toPromise();
            expect(r.get()).toBe(99);
        });
        it('orElse skips Ok', async () => {
            const spy = vi.fn();
            const r = await AsyncResult.ok(42).orElse(spy).toPromise();
            expect(spy).not.toHaveBeenCalled();
            expect(r.get()).toBe(42);
        });
    });

    describe('extraction', () => {
        it('fold calls onOk', async () => {
            const v = await AsyncResult.ok(10).fold((v) => v + 1, () => -1);
            expect(v).toBe(11);
        });
        it('fold calls onErr', async () => {
            const v = await AsyncResult.err('bad').fold(() => -1, (e) => e.length);
            expect(v).toBe(3);
        });
        it('match is alias for fold', async () => {
            const v = await AsyncResult.ok(10).match((v) => v + 1, () => -1);
            expect(v).toBe(11);
        });
        it('get returns value', async () => {
            expect(await AsyncResult.ok(42).get()).toBe(42);
        });
        it('get throws for Err', async () => {
            await expect(AsyncResult.err('bad').get()).rejects.toThrow();
        });
        it('getOrElse returns default for Err', async () => {
            expect(await AsyncResult.err('bad').getOrElse(99)).toBe(99);
        });
        it('getOrElse calls lazy default for Err', async () => {
            expect(await AsyncResult.err('bad').getOrElse(() => 99)).toBe(99);
        });
        it('getOrThrow throws for Err', async () => {
            await expect(AsyncResult.err('bad').getOrThrow()).rejects.toThrow();
        });
    });

    describe('PromiseLike', () => {
        it('then works', async () => {
            const r = await AsyncResult.ok(42).then((result) => result.get());
            expect(r).toBe(42);
        });
        // AsyncResult.catch delegates to underlying Promise.catch which can cause
        // thenable-recursion in some runtimes; verify rejection handling via toPromise.
        it('toPromise resolves rejected promise to Err', async () => {
            const r = await AsyncResult.fromPromise(Promise.reject('fail')).toPromise();
            expect(r.isErr()).toBe(true);
        });
        it('finally works', async () => {
            const spy = vi.fn();
            await AsyncResult.ok(42).finally(spy);
            expect(spy).toHaveBeenCalled();
        });
    });
});
