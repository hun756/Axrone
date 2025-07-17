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
