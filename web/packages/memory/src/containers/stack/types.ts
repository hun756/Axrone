import type { Nominal, NonEmptyReadonlyArray } from '@axrone/utility';

export type { Nominal };

// Variance is a phantom type marker used for documentation and compile-time checks.
export type Variance<T, V extends 'in' | 'out' | 'invariant'> = T & { readonly __variance?: V };
export type Phantom<T, P> = T & { readonly __phantom?: P };

/** @stable */
export type StackCapacity = Nominal<number, 'StackCapacity'>;
/** @stable */
export type StackSize = Nominal<number, 'StackSize'>;
/** @stable */
export type NodeId = Nominal<number, 'NodeId'>;
/** @stable */
export type MemoryAddress = Nominal<number, 'MemoryAddress'>;
/** @stable */
export type AllocatorId = Nominal<number, 'AllocatorId'>;
/** @stable */
export type PoolIndex = Nominal<number, 'PoolIndex'>;

/** @stable */
export type NonEmptyArray<T> = NonEmptyReadonlyArray<T>;
/** @stable */
export type EmptyArray = readonly [];
/** @stable */
export type ArrayWithLength<T, N extends number> = readonly T[] & { readonly length: N };

/** @stable */
export interface StackNode<T> extends Variance<{}, 'out'> {
    readonly id: NodeId;
    readonly value: T;
    readonly next: StackNode<T> | null;
    readonly refs: number;
    readonly generation: number;
    readonly memAddr: MemoryAddress;
}

/** @stable */
export interface AlignedStackNode<T> extends StackNode<T> {
    readonly padding: readonly number[];
    readonly checksum: number;
}

/** @stable */
export type StackResult<T, E = never> =
    | { readonly tag: 'success'; readonly value: T; readonly cost: number }
    | { readonly tag: 'failure'; readonly error: E; readonly recovery?: () => void };

/** @stable */
export type ExtractSuccess<T> = T extends StackResult<infer U, any> ? U : never;
/** @stable */
export type ExtractError<T> = T extends StackResult<any, infer E> ? E : never;
