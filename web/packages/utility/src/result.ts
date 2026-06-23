type Lazy<T> = () => T;
type AnyResult = Result<unknown, unknown>;
export type UnwrapOk<R> = R extends Result<infer T, unknown> ? T : never;
export type UnwrapErr<R> = R extends Result<unknown, infer E> ? E : never;
export type UnwrapOkTuple<R extends readonly AnyResult[]> = {
    -readonly [K in keyof R]: UnwrapOk<R[K]>;
};
export type UnwrapErrTuple<R extends readonly AnyResult[]> = UnwrapErr<R[number]>;

const EMPTY_ITERATOR: Iterator<never> = {
    next() {
        return { done: true, value: undefined as never };
    },
};

function formatValue(v: unknown): string {
    if (v === null) return 'null';
    if (v === undefined) return 'undefined';
    if (typeof v === 'string') return `"${v}"`;
    if (
        typeof v === 'number' ||
        typeof v === 'boolean' ||
        typeof v === 'bigint' ||
        typeof v === 'symbol'
    )
        return String(v);
    if (v instanceof Error) return `${v.name}: ${v.message}`;
    if (typeof v === 'object') {
        try {
            return JSON.stringify(v);
        } catch {
            return Object.prototype.toString.call(v);
        }
    }
    return String(v);
}

function asError(e: unknown): Error {
    return e instanceof Error
        ? e
        : new ResultError(typeof e === 'string' ? e : formatValue(e), { cause: e });
}

function extractErr<T, E>(r: Err<T, E>): E {
    return r.fold(
        (_) => undefined as never,
        (e) => e
    );
}

export class ResultError extends Error {
    override readonly name = 'ResultError';
    readonly code?: string | number;

    constructor(message: string, options?: { cause?: unknown; code?: string | number }) {
        super(message, options as ErrorOptions);
        Object.defineProperty(this, 'code', {
            value: options?.code,
            writable: false,
            enumerable: true,
            configurable: false,
        });
        Object.setPrototypeOf(this, ResultError.prototype);
        const capture = (
            Error as unknown as { captureStackTrace?: (target: unknown, ctor?: unknown) => void }
        ).captureStackTrace;
        if (typeof capture === 'function') capture(this, ResultError);
    }
}

export interface IResult<T, E> {
    readonly _tag: 'Ok' | 'Err';
    isOk(): this is Ok<T, E>;
    isErr(): this is Err<T, E>;
    map<U>(fn: (value: T) => U): Result<U, E>;
    mapErr<F>(fn: (error: E) => F): Result<T, F>;
    flatMap<U, F = E>(fn: (value: T) => Result<U, F>): Result<U, F>;
    andThen<U, F = E>(fn: (value: T) => Result<U, F>): Result<U, F>;
    chain<U, F = E>(fn: (value: T) => Result<U, F>): Result<U, F>;
    orElse<F>(fn: (error: E) => Result<T, F>): Result<T, F>;
    recover<F>(fn: (error: E) => Result<T, F>): Result<T, F>;
    fold<U>(onOk: (value: T) => U, onErr: (error: E) => U): U;
    match<U>(onOk: (value: T) => U, onErr: (error: E) => U): U;
    get(): T;
    getOrElse(defaultValue: T | Lazy<T>): T;
    getOrDefault(defaultValue: T): T;
    getOrThrow(): T;
    getOrThrowWith(transform?: (error: E) => unknown): T;
    unwrap(): T;
    unwrapOr(defaultValue: T): T;
    unwrapOrElse(fn: (error: E) => T): T;
    expect(message: string): T;
    expectErr(message: string): E;
    toOptional(): T | undefined;
    toNullable(): T | null;
    okToOptional(): T | undefined;
    errToOptional(): E | undefined;
    tap(fn: (value: T) => void): Result<T, E>;
    tapErr(fn: (error: E) => void): Result<T, E>;
    inspect(fn: (value: T) => void): Result<T, E>;
    inspectErr(fn: (error: E) => void): Result<T, E>;
    zip<U>(other: Result<U, E>): Result<[T, U], E>;
    zipWith<U, R>(other: Result<U, E>, fn: (a: T, b: U) => R): Result<R, E>;
    contains(value: T): boolean;
    containsErr(error: E): boolean;
    ok(): Ok<T, E> | null;
    err(): Err<T, E> | null;
    flip(): Result<E, T>;
    toArray(): T[];
    toErrorArray(): E[];
    toJSON(): unknown;
    toString(): string;
    [Symbol.iterator](): Iterator<T>;
    equals(
        other: Result<T, E>,
        valueEq?: (a: T, b: T) => boolean,
        errEq?: (a: E, b: E) => boolean
    ): boolean;
    transpose(): Result<NonNullable<T>, E> | null;
}

export class Ok<T, E = never> implements IResult<T, E>, Iterable<T> {
    readonly _tag = 'Ok' as const;

    private constructor(private readonly _value: T) {}

    static of<T>(value: T): Ok<T, never> {
        return new Ok(value);
    }

    isOk(): this is Ok<T, E> {
        return true;
    }
    isErr(): this is Err<T, E> {
        return false;
    }

    map<U>(fn: (value: T) => U): Result<U, E> {
        return new Ok(fn(this._value));
    }

    mapErr<F>(_fn: (error: E) => F): Result<T, F> {
        return this as unknown as Result<T, F>;
    }

    flatMap<U, F = E>(fn: (value: T) => Result<U, F>): Result<U, F> {
        return fn(this._value);
    }

    andThen<U, F = E>(fn: (value: T) => Result<U, F>): Result<U, F> {
        return fn(this._value);
    }

    chain<U, F = E>(fn: (value: T) => Result<U, F>): Result<U, F> {
        return fn(this._value);
    }

    orElse<F>(_fn: (error: E) => Result<T, F>): Result<T, F> {
        return this as unknown as Result<T, F>;
    }

    recover<F>(_fn: (error: E) => Result<T, F>): Result<T, F> {
        return this as unknown as Result<T, F>;
    }

    fold<U>(onOk: (value: T) => U, _onErr: (error: E) => U): U {
        return onOk(this._value);
    }

    match<U>(onOk: (value: T) => U, _onErr: (error: E) => U): U {
        return onOk(this._value);
    }

    get(): T {
        return this._value;
    }
    unwrap(): T {
        return this._value;
    }
    getOrElse(_defaultValue: T | Lazy<T>): T {
        return this._value;
    }
    getOrDefault(_defaultValue: T): T {
        return this._value;
    }
    getOrThrow(): T {
        return this._value;
    }
    getOrThrowWith(_transform?: (error: E) => unknown): T {
        return this._value;
    }
    unwrapOr(_defaultValue: T): T {
        return this._value;
    }
    unwrapOrElse(_fn: (error: E) => T): T {
        return this._value;
    }
    expect(_message: string): T {
        return this._value;
    }

    expectErr(message: string): E {
        throw new ResultError(message, { cause: this._value });
    }

    toOptional(): T | undefined {
        return this._value;
    }
    toNullable(): T | null {
        return this._value;
    }
    okToOptional(): T | undefined {
        return this._value;
    }
    errToOptional(): E | undefined {
        return undefined;
    }

    tap(fn: (value: T) => void): Result<T, E> {
        fn(this._value);
        return this;
    }

    tapErr(_fn: (error: E) => void): Result<T, E> {
        return this;
    }

    inspect(fn: (value: T) => void): Result<T, E> {
        fn(this._value);
        return this;
    }

    inspectErr(_fn: (error: E) => void): Result<T, E> {
        return this;
    }

    zip<U>(other: Result<U, E>): Result<[T, U], E> {
        return other.map((u) => [this._value, u] as [T, U]);
    }

    zipWith<U, R>(other: Result<U, E>, fn: (a: T, b: U) => R): Result<R, E> {
        return other.map((u) => fn(this._value, u));
    }

    contains(value: T): boolean {
        return Object.is(this._value, value);
    }

    containsErr(_error: E): boolean {
        return false;
    }

    ok(): Ok<T, E> | null {
        return this;
    }
    err(): Err<T, E> | null {
        return null;
    }

    flip(): Result<E, T> {
        return Err.of<T>(this._value) as unknown as Result<E, T>;
    }

    toArray(): T[] {
        return [this._value];
    }
    toErrorArray(): E[] {
        return [];
    }

    toJSON(): { _tag: 'Ok'; value: T } {
        return { _tag: 'Ok', value: this._value };
    }

    toString(): string {
        return `Ok(${formatValue(this._value)})`;
    }

    *[Symbol.iterator](): Iterator<T> {
        yield this._value;
    }

    equals(
        other: Result<T, E>,
        valueEq?: (a: T, b: T) => boolean,
        _errEq?: (a: E, b: E) => boolean
    ): boolean {
        return other.fold(
            (o) => (valueEq ? valueEq(o, this._value) : Object.is(o, this._value)),
            () => false
        );
    }

    transpose(): Result<NonNullable<T>, E> | null {
        if (this._value === null || this._value === undefined) return null;
        return this as unknown as Result<NonNullable<T>, E>;
    }
}

export class Err<T, E = Error> implements IResult<T, E>, Iterable<never> {
    readonly _tag = 'Err' as const;

    private constructor(private readonly _error: E) {}

    static of<E>(error: E): Err<never, E> {
        return new Err(error);
    }

    isOk(): this is Ok<T, E> {
        return false;
    }
    isErr(): this is Err<T, E> {
        return true;
    }

    map<U>(_fn: (value: T) => U): Result<U, E> {
        return this as unknown as Result<U, E>;
    }

    mapErr<F>(fn: (error: E) => F): Result<T, F> {
        return new Err(fn(this._error));
    }

    flatMap<U, F = E>(_fn: (value: T) => Result<U, F>): Result<U, F> {
        return this as unknown as Result<U, F>;
    }

    andThen<U, F = E>(_fn: (value: T) => Result<U, F>): Result<U, F> {
        return this as unknown as Result<U, F>;
    }

    chain<U, F = E>(_fn: (value: T) => Result<U, F>): Result<U, F> {
        return this as unknown as Result<U, F>;
    }

    orElse<F>(fn: (error: E) => Result<T, F>): Result<T, F> {
        return fn(this._error);
    }

    recover<F>(fn: (error: E) => Result<T, F>): Result<T, F> {
        return fn(this._error);
    }

    fold<U>(_onOk: (value: T) => U, onErr: (error: E) => U): U {
        return onErr(this._error);
    }

    match<U>(_onOk: (value: T) => U, onErr: (error: E) => U): U {
        return onErr(this._error);
    }

    get(): never {
        throw asError(this._error);
    }

    unwrap(): never {
        throw asError(this._error);
    }

    getOrElse(defaultValue: T | Lazy<T>): T {
        return typeof defaultValue === 'function' ? (defaultValue as Lazy<T>)() : defaultValue;
    }

    getOrDefault(defaultValue: T): T {
        return defaultValue;
    }

    getOrThrow(): never {
        throw asError(this._error);
    }

    getOrThrowWith(transform?: (error: E) => unknown): never {
        if (transform) throw transform(this._error);
        throw asError(this._error);
    }

    unwrapOr(defaultValue: T): T {
        return defaultValue;
    }
    unwrapOrElse(fn: (error: E) => T): T {
        return fn(this._error);
    }

    expect(message: string): never {
        throw new ResultError(message, { cause: this._error });
    }

    expectErr(_message: string): E {
        return this._error;
    }

    toOptional(): T | undefined {
        return undefined;
    }
    toNullable(): T | null {
        return null;
    }
    okToOptional(): T | undefined {
        return undefined;
    }
    errToOptional(): E | undefined {
        return this._error;
    }

    tap(_fn: (value: T) => void): Result<T, E> {
        return this;
    }

    tapErr(fn: (error: E) => void): Result<T, E> {
        fn(this._error);
        return this;
    }

    inspect(_fn: (value: T) => void): Result<T, E> {
        return this;
    }

    inspectErr(fn: (error: E) => void): Result<T, E> {
        fn(this._error);
        return this;
    }

    zip<U>(_other: Result<U, E>): Result<[T, U], E> {
        return this as unknown as Result<[T, U], E>;
    }

    zipWith<U, R>(_other: Result<U, E>, _fn: (a: T, b: U) => R): Result<R, E> {
        return this as unknown as Result<R, E>;
    }

    contains(_value: T): boolean {
        return false;
    }

    containsErr(error: E): boolean {
        return Object.is(this._error, error);
    }

    ok(): Ok<T, E> | null {
        return null;
    }
    err(): Err<T, E> | null {
        return this;
    }

    flip(): Result<E, T> {
        return Ok.of<E>(this._error) as unknown as Result<E, T>;
    }

    toArray(): T[] {
        return [];
    }
    toErrorArray(): E[] {
        return [this._error];
    }

    toJSON(): { _tag: 'Err'; error: E } {
        return { _tag: 'Err', error: this._error };
    }

    toString(): string {
        return `Err(${formatValue(this._error)})`;
    }

    [Symbol.iterator](): Iterator<never> {
        return EMPTY_ITERATOR;
    }

    equals(
        other: Result<T, E>,
        _valueEq?: (a: T, b: T) => boolean,
        errEq?: (a: E, b: E) => boolean
    ): boolean {
        return other.fold(
            () => false,
            (oe) => (errEq ? errEq(oe, this._error) : Object.is(oe, this._error))
        );
    }

    transpose(): Result<NonNullable<T>, E> | null {
        return this as unknown as Result<NonNullable<T>, E>;
    }
}

export type Result<T, E = Error> = Ok<T, E> | Err<T, E>;

export function ok<T>(value: T): Ok<T, never>;
export function ok(): Ok<void, never>;
export function ok<T>(value?: T): Ok<T, never> {
    return Ok.of(value as T);
}

export function err<E>(error: E): Err<never, E> {
    return Err.of(error);
}

export function isResult<T, E>(value: unknown): value is Result<T, E> {
    return value instanceof Ok || value instanceof Err;
}

export function isOk<T, E>(value: Result<T, E>): value is Ok<T, E>;
export function isOk<T, E>(value: unknown): value is Ok<T, E>;
export function isOk<T, E>(value: unknown): value is Ok<T, E> {
    return value instanceof Ok;
}

export function isErr<T, E>(value: Result<T, E>): value is Err<T, E>;
export function isErr<T, E>(value: unknown): value is Err<T, E>;
export function isErr<T, E>(value: unknown): value is Err<T, E> {
    return value instanceof Err;
}

export function assertOk<T, E>(result: Result<T, E>): asserts result is Ok<T, E> {
    if (result.isErr()) throw asError(extractErr(result));
}

export function assertErr<T, E>(result: Result<T, E>): asserts result is Err<T, E> {
    if (result.isOk()) throw new ResultError('Expected Err, got Ok', { cause: result.get() });
}

export function fromThrowable<T, A extends readonly unknown[]>(
    fn: (...args: A) => T,
    onError?: (error: unknown) => Error
): (...args: A) => Result<T, Error> {
    return (...args: A) => {
        try {
            return Ok.of(fn(...args));
        } catch (e) {
            return Err.of(onError ? onError(e) : asError(e));
        }
    };
}

export function tryCatch<T, E = Error>(fn: () => T, onError?: (error: unknown) => E): Result<T, E> {
    try {
        return Ok.of(fn());
    } catch (e) {
        return Err.of(onError ? onError(e) : (asError(e) as unknown as E));
    }
}

export async function tryCatchAsync<T, E = Error>(
    fn: () => Promise<T>,
    onError?: (error: unknown) => E
): Promise<Result<T, E>> {
    try {
        return Ok.of(await fn());
    } catch (e) {
        return Err.of(onError ? onError(e) : (asError(e) as unknown as E));
    }
}

export function fromPromise<T, E = Error>(
    promise: Promise<T>,
    onError?: (error: unknown) => E
): Promise<Result<T, E>> {
    return promise.then(
        (value) => Ok.of(value),
        (error) => Err.of(onError ? onError(error) : (asError(error) as unknown as E))
    );
}

export function fromNullable<T, E>(value: T | null | undefined, onError: Lazy<E>): Result<T, E> {
    return value === null || value === undefined ? Err.of(onError()) : Ok.of(value);
}

export function fromOption<T, E>(value: T | undefined | null, onNone: Lazy<E>): Result<T, E> {
    return value === undefined || value === null ? Err.of(onNone()) : Ok.of(value);
}

export function all<T, E>(results: Iterable<Result<T, E>>): Result<T[], E> {
    const out: T[] = [];
    for (const r of results) {
        if (r.isErr()) return r as unknown as Result<T[], E>;
        out.push(r.get());
    }
    return Ok.of(out);
}

export function allT<R extends readonly AnyResult[]>(
    ...results: R
): Result<UnwrapOkTuple<R>, UnwrapErrTuple<R>> {
    const out: unknown[] = [];
    for (const r of results) {
        if (r.isErr()) return r as unknown as Result<UnwrapOkTuple<R>, UnwrapErrTuple<R>>;
        out.push(r.get());
    }
    return Ok.of(out) as unknown as Result<UnwrapOkTuple<R>, UnwrapErrTuple<R>>;
}

export function any<T, E>(results: Iterable<Result<T, E>>): Result<T, E[]> {
    const errors: E[] = [];
    for (const r of results) {
        if (r.isOk()) return r as Result<T, E[]>;
        errors.push(extractErr(r));
    }
    return Err.of(errors);
}

export function partition<T, E>(results: Iterable<Result<T, E>>): { ok: T[]; err: E[] } {
    const okValues: T[] = [];
    const errValues: E[] = [];
    for (const r of results) {
        r.fold(
            (v) => okValues.push(v),
            (e) => errValues.push(e)
        );
    }
    return { ok: okValues, err: errValues };
}

export function combine<T, E>(results: Iterable<Result<T, E>>): Result<T[], E[]> {
    const okValues: T[] = [];
    const errValues: E[] = [];
    for (const r of results) {
        r.fold(
            (v) => okValues.push(v),
            (e) => errValues.push(e)
        );
    }
    return errValues.length === 0 ? Ok.of(okValues) : Err.of(errValues);
}

export function retry<T, E>(
    fn: (attempt: number) => Result<T, E>,
    options: {
        maxAttempts: number;
        shouldRetry?: (error: E, attempt: number) => boolean;
    }
): Result<T, E> {
    const { maxAttempts, shouldRetry } = options;
    let attempt = 0;
    let last: Result<T, E> = Err.of(new ResultError('Not attempted') as unknown as E);
    while (attempt < maxAttempts) {
        last = fn(attempt);
        if (last.isOk()) return last;
        if (shouldRetry && !shouldRetry(extractErr(last as Err<T, E>), attempt)) return last;
        attempt++;
    }
    return last;
}

export async function retryAsync<T, E>(
    fn: (attempt: number) => Promise<Result<T, E>>,
    options: {
        maxAttempts: number;
        shouldRetry?: (error: E, attempt: number) => boolean;
        delay?: (attempt: number) => number;
    }
): Promise<Result<T, E>> {
    const { maxAttempts, shouldRetry, delay } = options;
    let attempt = 0;
    let last: Result<T, E> = Err.of(new ResultError('Not attempted') as unknown as E);
    while (attempt < maxAttempts) {
        last = await fn(attempt);
        if (last.isOk()) return last;
        if (shouldRetry && !shouldRetry(extractErr(last as Err<T, E>), attempt)) return last;
        if (delay && attempt + 1 < maxAttempts) {
            const d = delay(attempt);
            if (d > 0) await new Promise<void>((res) => setTimeout(res, d));
        }
        attempt++;
    }
    return last;
}

export function fromJSON<T, E>(json: unknown, reviver?: (value: unknown) => unknown): Result<T, E> {
    let obj: unknown = json;
    if (typeof json === 'string') {
        try {
            obj = JSON.parse(json);
        } catch (e) {
            return Err.of(asError(e) as unknown as E);
        }
    }
    if (typeof obj !== 'object' || obj === null || !('_tag' in obj)) {
        return Err.of(new ResultError('Invalid Result JSON structure') as unknown as E);
    }
    const tag = (obj as { _tag: string })._tag;
    const payload =
        (obj as { value?: unknown; error?: unknown }).value ?? (obj as { error?: unknown }).error;
    const revived = reviver ? reviver(payload) : payload;
    if (tag === 'Ok') return Ok.of(revived) as unknown as Result<T, E>;
    if (tag === 'Err') return Err.of(revived) as unknown as Result<T, E>;
    return Err.of(new ResultError(`Unknown _tag: ${tag}`) as unknown as E);
}

export class AsyncResult<T, E = Error> implements PromiseLike<Result<T, E>> {
    private constructor(private readonly _promise: Promise<Result<T, E>>) {}

    static from<T, E = Error>(promise: Promise<Result<T, E>>): AsyncResult<T, E> {
        return new AsyncResult(promise);
    }

    static fromPromise<T, E = Error>(
        promise: Promise<T>,
        onError?: (error: unknown) => E
    ): AsyncResult<T, E> {
        return new AsyncResult(
            promise.then(
                (v) => Ok.of(v),
                (e) => Err.of(onError ? onError(e) : (asError(e) as unknown as E))
            )
        );
    }

    static fromThrowable<T, A extends readonly unknown[], E = Error>(
        fn: (...args: A) => Promise<T>,
        onError?: (error: unknown) => E
    ): (...args: A) => AsyncResult<T, E> {
        return (...args: A) => AsyncResult.fromPromise(fn(...args), onError);
    }

    static ok<T>(value: T): AsyncResult<T, never> {
        return new AsyncResult(Promise.resolve(Ok.of(value)));
    }

    static err<E>(error: E): AsyncResult<never, E> {
        return new AsyncResult(Promise.resolve(Err.of(error)));
    }

    map<U>(fn: (value: T) => U | Promise<U>): AsyncResult<U, E> {
        return AsyncResult.from(
            this._promise.then(async (r) =>
                r.isOk() ? Ok.of(await fn(r.get())) : (r as unknown as Result<U, E>)
            )
        );
    }

    mapErr<F>(fn: (error: E) => F | Promise<F>): AsyncResult<T, F> {
        return AsyncResult.from(
            this._promise.then(async (r) =>
                r.isErr() ? Err.of(await fn(extractErr(r))) : (r as unknown as Result<T, F>)
            )
        );
    }

    flatMap<U, F = E>(
        fn: (value: T) => Result<U, F> | AsyncResult<U, F> | Promise<Result<U, F>>
    ): AsyncResult<U, F> {
        return AsyncResult.from(
            this._promise.then(async (r) => {
                if (r.isErr()) return r as unknown as Result<U, F>;
                const next = fn(r.get());
                if (next instanceof AsyncResult) return next.toPromise();
                return next;
            })
        );
    }

    andThen<U, F = E>(
        fn: (value: T) => Result<U, F> | AsyncResult<U, F> | Promise<Result<U, F>>
    ): AsyncResult<U, F> {
        return this.flatMap(fn);
    }

    orElse<F>(
        fn: (error: E) => Result<T, F> | AsyncResult<T, F> | Promise<Result<T, F>>
    ): AsyncResult<T, F> {
        return AsyncResult.from(
            this._promise.then(async (r) => {
                if (r.isOk()) return r as unknown as Result<T, F>;
                const next = fn(extractErr(r));
                if (next instanceof AsyncResult) return next.toPromise();
                return next;
            })
        );
    }

    recover<F>(
        fn: (error: E) => Result<T, F> | AsyncResult<T, F> | Promise<Result<T, F>>
    ): AsyncResult<T, F> {
        return this.orElse(fn);
    }

    fold<U>(onOk: (value: T) => U | Promise<U>, onErr: (error: E) => U | Promise<U>): Promise<U> {
        return this._promise.then(async (r) => (r.isOk() ? onOk(r.get()) : onErr(extractErr(r))));
    }

    match<U>(onOk: (value: T) => U | Promise<U>, onErr: (error: E) => U | Promise<U>): Promise<U> {
        return this.fold(onOk, onErr);
    }

    get(): Promise<T> {
        return this._promise.then((r) => r.get());
    }

    getOrElse(defaultValue: T | Lazy<T> | Promise<T>): Promise<T> {
        return this._promise.then((r) => {
            if (r.isOk()) return r.get();
            if (typeof defaultValue === 'function') return (defaultValue as Lazy<T>)();
            return defaultValue;
        });
    }

    getOrThrow(): Promise<T> {
        return this._promise.then((r) => r.getOrThrow());
    }

    toPromise(): Promise<Result<T, E>> {
        return this._promise;
    }

    then<A = Result<T, E>, B = never>(
        onfulfilled?: (value: Result<T, E>) => A | PromiseLike<A>,
        onrejected?: (reason: unknown) => B | PromiseLike<B>
    ): Promise<A | B> {
        return this._promise.then(onfulfilled, onrejected);
    }

    catch<B>(onrejected?: (reason: unknown) => B | PromiseLike<B>): Promise<Result<T, E> | B> {
        return this._promise.catch(onrejected);
    }

    finally(onFinally?: () => void | Promise<void>): Promise<Result<T, E>> {
        return this._promise.finally(onFinally);
    }
}

export const Result = {
    ok,
    err,
    Ok,
    Err,
    AsyncResult,
    ResultError,
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
} as const;
