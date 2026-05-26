export const Ok: unique symbol = Symbol('Ok');
export const Err: unique symbol = Symbol('Err');

export type Result<T, E = Error> =
    | { readonly _tag: typeof Ok; readonly value: T }
    | { readonly _tag: typeof Err; readonly error: E };

export const ok = <T>(value: T): Result<T, never> =>
    ({ _tag: Ok, value }) as Result<T, never>;

export const err = <E>(error: E): Result<never, E> =>
    ({ _tag: Err, error }) as Result<never, E>;

export const isOk = <T, E>(result: Result<T, E>): result is { _tag: typeof Ok; value: T } =>
    result._tag === Ok;

export const isErr = <T, E>(result: Result<T, E>): result is { _tag: typeof Err; error: E } =>
    result._tag === Err;

export const unwrap = <T, E>(result: Result<T, E>): T => {
    if (isOk(result)) return result.value;
    throw result.error;
};

export const unwrapOr = <T, E>(result: Result<T, E>, defaultValue: T): T =>
    isOk(result) ? result.value : defaultValue;

export const map = <T, U, E>(result: Result<T, E>, fn: (value: T) => U): Result<U, E> =>
    isOk(result) ? ok(fn(result.value)) : result;

export const flatMap = <T, U, E>(result: Result<T, E>, fn: (value: T) => Result<U, E>): Result<U, E> =>
    isOk(result) ? fn(result.value) : result;

export const mapErr = <T, E, F>(result: Result<T, E>, fn: (error: E) => F): Result<T, F> =>
    isErr(result) ? err(fn(result.error)) : result;

export const Result = { ok, err, isOk, isErr, unwrap, unwrapOr, map, flatMap, mapErr } as const;
Object.freeze(Result);
