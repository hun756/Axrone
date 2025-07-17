declare const __LAZY_BRAND: unique symbol;
declare const __THUNK_BRAND: unique symbol;
declare const __EVALUATED_BRAND: unique symbol;

type LazyBrand = typeof __LAZY_BRAND;
type ThunkBrand = typeof __THUNK_BRAND;
type EvaluatedBrand = typeof __EVALUATED_BRAND;

type LazyValue<T> = T extends Lazy<infer U> ? U : never;

type UnwrapLazy<T> = T extends Lazy<infer U> ? (U extends Lazy<any> ? UnwrapLazy<U> : U) : T;

type DeepUnwrapLazy<T> =
    T extends Lazy<infer U>
        ? DeepUnwrapLazy<U>
        : T extends readonly (infer U)[]
          ? readonly DeepUnwrapLazy<U>[]
          : T extends Record<PropertyKey, any>
            ? { readonly [K in keyof T]: DeepUnwrapLazy<T[K]> }
            : T;

type IsLazy<T> = T extends Lazy<any> ? true : false;

type FilterLazy<T extends readonly unknown[]> = {
    readonly [K in keyof T]: IsLazy<T[K]> extends true ? T[K] : never;
}[number];

type AllLazy<T extends readonly unknown[]> = T extends readonly Lazy<any>[] ? T : never;

interface ThunkDescriptor<T> {
    readonly [__THUNK_BRAND]: typeof __THUNK_BRAND;
    readonly computation: () => T;
    readonly state: ThunkState<T>;
}

interface EvaluatedDescriptor<T> {
    readonly [__EVALUATED_BRAND]: typeof __EVALUATED_BRAND;
    readonly value: T;
}

type ThunkState<T> =
    | { readonly status: 'pending' }
    | { readonly status: 'evaluating' }
    | { readonly status: 'resolved'; readonly value: T }
    | { readonly status: 'rejected'; readonly error: unknown };

interface Lazy<T> {
    readonly [__LAZY_BRAND]: LazyBrand;
    readonly _descriptor: ThunkDescriptor<T> | EvaluatedDescriptor<T>;
}

type LazyConstructor = {
    readonly<T>(computation: () => T): Lazy<T>;
    readonly of: <T>(value: T) => Lazy<T>;
    readonly defer: <T>(computation: () => T) => Lazy<T>;
    readonly pure: <T>(value: T) => Lazy<T>;
    readonly fromPromise: <T>(promise: Promise<T>) => Lazy<T>;
    readonly sequence: <T extends readonly Lazy<any>[]>(
        ...lazies: T
    ) => Lazy<{ readonly [K in keyof T]: LazyValue<T[K]> }>;
    readonly parallel: <T extends readonly Lazy<any>[]>(
        ...lazies: T
    ) => Lazy<{ readonly [K in keyof T]: LazyValue<T[K]> }>;
    readonly race: <T extends readonly Lazy<any>[]>(...lazies: T) => Lazy<LazyValue<T[number]>>;
    readonly merge: <T extends Record<PropertyKey, Lazy<any>>>(
        obj: T
    ) => Lazy<{ readonly [K in keyof T]: LazyValue<T[K]> }>;
};

const EMPTY_OBJECT = Object.freeze({}) as Record<never, never>;
const EMPTY_ARRAY = Object.freeze([]) as readonly never[];

const createThunkState = <T>(): {
    state: ThunkState<T>;
    setState: (newState: ThunkState<T>) => void;
} => {
    let currentState: ThunkState<T> = { status: 'pending' };
    return {
        get state() {
            return currentState;
        },
        setState: (newState: ThunkState<T>) => {
            currentState = newState;
        },
    };
};

const createThunkDescriptor = <T>(computation: () => T): ThunkDescriptor<T> => {
    const { state, setState } = createThunkState<T>();

    return Object.freeze({
        [__THUNK_BRAND]: __THUNK_BRAND,
        computation,
        get state() {
            return state;
        },
        [Symbol.toStringTag]: 'ThunkDescriptor',
    } as ThunkDescriptor<T>);
};

const createEvaluatedDescriptor = <T>(value: T): EvaluatedDescriptor<T> =>
    Object.freeze({
        [__EVALUATED_BRAND]: __EVALUATED_BRAND,
        value,
        [Symbol.toStringTag]: 'EvaluatedDescriptor',
    });

const createLazy = <T>(descriptor: ThunkDescriptor<T> | EvaluatedDescriptor<T>): Lazy<T> =>
    Object.freeze({
        [__LAZY_BRAND]: __LAZY_BRAND,
        _descriptor: descriptor,
        [Symbol.toStringTag]: 'Lazy',
    }) as Lazy<T>;

const isThunkDescriptor = <T>(
    descriptor: ThunkDescriptor<T> | EvaluatedDescriptor<T>
): descriptor is ThunkDescriptor<T> => __THUNK_BRAND in descriptor;

const isEvaluatedDescriptor = <T>(
    descriptor: ThunkDescriptor<T> | EvaluatedDescriptor<T>
): descriptor is EvaluatedDescriptor<T> => __EVALUATED_BRAND in descriptor;

const evaluateThunk = <T>(thunk: ThunkDescriptor<T>): T => {
    const currentState = thunk.state;

    if (currentState.status === 'resolved') {
        return currentState.value;
    }

    if (currentState.status === 'rejected') {
        throw currentState.error;
    }

    if (currentState.status === 'evaluating') {
        throw new Error('Circular dependency detected');
    }

    if (typeof thunk.state === 'object' && 'setState' in thunk) {
        (thunk as any).setState({ status: 'evaluating' });
    }
    try {
        const result = thunk.computation();
        if (typeof thunk.state === 'object' && 'setState' in thunk) {
            (thunk as any).setState({ status: 'resolved', value: result });
        }
        return result;
    } catch (error) {
        if (typeof thunk.state === 'object' && 'setState' in thunk) {
            (thunk as any).setState({ status: 'rejected', error });
        }
        throw error;
    }
};

export const force = <T>(lazy: Lazy<T>): T => {
    const descriptor = lazy._descriptor;

    if (isEvaluatedDescriptor(descriptor)) {
        return descriptor.value;
    }

    return evaluateThunk(descriptor);
};

export const isEvaluated = <T>(lazy: Lazy<T>): boolean => {
    const descriptor = lazy._descriptor;
    return isEvaluatedDescriptor(descriptor) || descriptor.state.status === 'resolved';
};

export const tryForce = <T>(lazy: Lazy<T>): T | Error => {
    try {
        return force(lazy);
    } catch (error) {
        return error instanceof Error ? error : new Error(String(error));
    }
};

const lazy = Object.assign(
    function lazy<T>(computation: () => T): Lazy<T> {
        return createLazy(createThunkDescriptor(computation));
    },
    {
        of: <T>(value: T): Lazy<T> => createLazy(createEvaluatedDescriptor(value)),
        defer: <T>(computation: () => T): Lazy<T> => createLazy(createThunkDescriptor(computation)),
        pure: <T>(value: T): Lazy<T> => createLazy(createEvaluatedDescriptor(value)),
        fromPromise: <T>(promise: Promise<T>): Lazy<T> =>
            createLazy(
                createThunkDescriptor(() => {
                    throw new Error('Use lazy.fromPromiseAsync for async');
                })
            ),
        sequence: <T extends readonly Lazy<any>[]>(
            ...lazies: T
        ): Lazy<{ readonly [K in keyof T]: LazyValue<T[K]> }> =>
            createLazy(
                createThunkDescriptor(
                    () => lazies.map(force) as { readonly [K in keyof T]: LazyValue<T[K]> }
                )
            ),
        parallel: <T extends readonly Lazy<any>[]>(
            ...lazies: T
        ): Lazy<{ readonly [K in keyof T]: LazyValue<T[K]> }> =>
            createLazy(
                createThunkDescriptor(
                    () => lazies.map(force) as { readonly [K in keyof T]: LazyValue<T[K]> }
                )
            ),
        race: <T extends readonly Lazy<any>[]>(...lazies: T): Lazy<LazyValue<T[number]>> =>
            createLazy(
                createThunkDescriptor(() => {
                    const errors: unknown[] = [];
                    for (const lazyValue of lazies) {
                        try {
                            return force(lazyValue);
                        } catch (error) {
                            errors.push(error);
                        }
                    }
                    throw new Error(
                        'All lazy values failed: ' + errors.map((e) => String(e)).join(', ')
                    );
                })
            ),
        merge: <T extends Record<PropertyKey, Lazy<any>>>(
            obj: T
        ): Lazy<{ readonly [K in keyof T]: LazyValue<T[K]> }> =>
            createLazy(
                createThunkDescriptor(() => {
                    const result = {} as { [K in keyof T]: LazyValue<T[K]> };
                    for (const key in obj) {
                        if (Object.prototype.hasOwnProperty.call(obj, key)) {
                            result[key] = force(obj[key]);
                        }
                    }
                    return result;
                })
            ),
    }
) as unknown as LazyConstructor;

export { lazy };

type MapFn<T, U> = (value: T) => U;
type FilterFn<T, U extends T = T> = (value: T) => value is U;
type PredicateFn<T> = (value: T) => boolean;
type ReducerFn<T, U> = (acc: U, value: T) => U;
type FlatMapFn<T, U> = (value: T) => Lazy<U>;

export const map =
    <T, U>(fn: MapFn<T, U>) =>
    (lazy: Lazy<T>): Lazy<U> =>
        createLazy(createThunkDescriptor(() => fn(force(lazy))));

export const flatMap =
    <T, U>(fn: FlatMapFn<T, U>) =>
    (lazy: Lazy<T>): Lazy<U> =>
        createLazy(createThunkDescriptor(() => force(fn(force(lazy)))));

export const filter =
    <T, U extends T>(fn: FilterFn<T, U>) =>
    (lazy: Lazy<T>): Lazy<U> =>
        createLazy(
            createThunkDescriptor(() => {
                const value = force(lazy);
                if (fn(value)) return value;
                throw new Error('Filter predicate failed');
            })
        );

export const tap =
    <T>(fn: (value: T) => void) =>
    (lazy: Lazy<T>): Lazy<T> =>
        createLazy(
            createThunkDescriptor(() => {
                const value = force(lazy);
                fn(value);
                return value;
            })
        );

export const recover =
    <T, U>(fn: (error: unknown) => U) =>
    (lazy: Lazy<T>): Lazy<T | U> =>
        createLazy(
            createThunkDescriptor(() => {
                try {
                    return force(lazy);
                } catch (error) {
                    return fn(error);
                }
            })
        );

export const timeout =
    <T>(ms: number) =>
    (lazy: Lazy<T>): Lazy<T> =>
        createLazy(
            createThunkDescriptor(() => {
                const start = performance.now();
                const result = force(lazy);
                const elapsed = performance.now() - start;

                if (elapsed > ms) {
                    throw new Error(`Timeout exceeded: ${elapsed}ms > ${ms}ms`);
                }

                return result;
            })
        );

export const delay =
    <T>(ms: number) =>
    (lazy: Lazy<T>): Lazy<T> =>
        createLazy(
            createThunkDescriptor(() => {
                const start = performance.now();
                const result = force(lazy);
                const elapsed = performance.now() - start;
                const remaining = Math.max(0, ms - elapsed);

                if (remaining > 0) {
                    const end = performance.now() + remaining;
                    while (performance.now() < end) {
                        /* busy wait */
                    }
                }

                return result;
            })
        );

export const memoize = <T>(lazy: Lazy<T>): Lazy<T> => lazy;

export const zip = <T extends readonly [Lazy<any>, ...Lazy<any>[]]>(
    ...lazies: T
): Lazy<{ readonly [K in keyof T]: LazyValue<T[K]> }> =>
    createLazy(
        createThunkDescriptor(
            () => lazies.map(force) as { readonly [K in keyof T]: LazyValue<T[K]> }
        )
    );

export const zipWith = <T extends readonly [Lazy<any>, ...Lazy<any>[]], U>(
    fn: (...values: { readonly [K in keyof T]: LazyValue<T[K]> }) => U,
    ...lazies: T
): Lazy<U> => createLazy(createThunkDescriptor(() => fn(...force(lazy.sequence(...lazies)))));
