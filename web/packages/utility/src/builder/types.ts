import type { Primitive, DeepReadonly, DeepPartial } from '../types';

export type { Primitive, DeepReadonly, DeepPartial };

export const $brand: unique symbol = Symbol.for('axrone.builder.brand');
export const $state: unique symbol = Symbol.for('axrone.builder.state');
export const $node: unique symbol = Symbol.for('axrone.builder.node');

export type DepthCounter = [never, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

export type NestedKey<T> = T extends readonly (infer U)[]
    ? `${number}`
    : T extends object
      ? Extract<keyof T, string | number>
      : never;

export type Path<T, D extends number = 6> = [D] extends [never]
    ? never
    : T extends Primitive
      ? never
      : T extends readonly (infer U)[]
        ? `${number}` | (Path<U, DepthCounter[D]> extends never ? never : `${number}.${Path<U, DepthCounter[D]>}`)
        : T extends Map<unknown, unknown> | Set<unknown>
          ? never
          : T extends object
            ? {
                    [K in Extract<keyof T, string | number>]:
                        | `${K}`
                        | (Path<T[K], DepthCounter[D]> extends never ? never : `${K}.${Path<T[K], DepthCounter[D]>}`);
                }[Extract<keyof T, string | number>]
            : never;

export type PathValue<T, P extends string> = P extends `${infer K}.${infer Rest}`
    ? K extends keyof T
        ? PathValue<T[K], Rest>
        : T extends readonly (infer U)[]
          ? K extends `${number}`
              ? PathValue<U, Rest>
              : never
          : never
    : P extends keyof T
      ? T[P]
      : T extends readonly (infer U)[]
        ? P extends `${number}`
            ? U
            : never
        : never;

export type RequiredKeys<T> = {
    [K in keyof T]-?: {} extends Pick<T, K> ? never : K;
}[keyof T];

export type OptionalKeys<T> = {
    [K in keyof T]-?: {} extends Pick<T, K> ? K : never;
}[keyof T];

export type MissingKeys<T, TSupplied extends keyof T> = Exclude<RequiredKeys<T>, TSupplied>;

export type IsBuildable<T, TSupplied extends keyof T> = [MissingKeys<T, TSupplied>] extends [never] ? true : false;

export type ValueUpdater<TValue> = (previous: TValue | undefined) => TValue;
export type AsyncValueResolver<TValue> = () => Promise<TValue> | TValue;

export interface ValidationIssue {
    readonly path: string;
    readonly message: string;
    readonly code: string;
    readonly received?: unknown;
}

export type ValidationResult<T> =
    | { readonly ok: true; readonly value: T }
    | { readonly ok: false; readonly issues: readonly ValidationIssue[] };

export type SyncValidator<T> = (value: T) => ValidationResult<T>;
export type AsyncValidator<T> = (value: T) => Promise<ValidationResult<T>>;
export type AnyValidator<T> = SyncValidator<T> | AsyncValidator<T>;

export type SyncHook<T> = (data: T) => T | void;
export type AsyncHook<T> = (data: T) => Promise<T | void> | T | void;

export interface IBuilder<TTarget> {
    build(): DeepReadonly<TTarget>;
    buildUnsafe(): DeepReadonly<TTarget>;
}

export interface IAsyncBuilder<TTarget> {
    build(): Promise<DeepReadonly<TTarget>>;
    buildUnsafe(): Promise<DeepReadonly<TTarget>>;
}

export interface ILens<TRoot, TSub> {
    get(root: TRoot): TSub;
    set(sub: TSub, root: TRoot): TRoot;
}

export type FactoryProvider<TTarget extends object, TArgs extends readonly unknown[]> =
    | Partial<TTarget>
    | ((...args: TArgs) => Partial<TTarget>);

export interface IFactory<TTarget extends object, TArgs extends readonly unknown[] = []> {
    (...args: TArgs): import('./sync-builder').Builder<TTarget, never>;
    create(...args: TArgs): import('./sync-builder').Builder<TTarget, never>;
    createProxy(...args: TArgs): import('./proxy').DynamicProxyBuilder<TTarget, never>;
    createAsync(...args: TArgs): import('./async-builder').AsyncBuilder<TTarget, never>;
    build(...args: TArgs): DeepReadonly<TTarget>;
    batch(count: number, ...args: TArgs): readonly DeepReadonly<TTarget>[];
}
