import { Variance, StackSize, StackCapacity } from './types';

/** @stable */
export interface StackConfiguration<T> {
    readonly capacity?: number;
    readonly enableIntegrityChecks?: boolean;
    readonly compareFn?: (a: T, b: T) => boolean;
    readonly hashFn?: (value: T) => number;
    readonly serializeFn?: (value: T) => ArrayBuffer;
    readonly deserializeFn?: (data: ArrayBuffer) => T;
    readonly validateFn?: (value: T) => boolean;
    readonly transformFn?: (value: T) => T;
}

/** @stable */
export interface ReadonlyStackInterface<out T> extends Iterable<T>, Variance<{}, 'out'> {
    readonly size: StackSize;
    readonly capacity: StackCapacity | null;
    readonly isEmpty: boolean;
    readonly isFull: boolean;
    readonly generation: number;
    readonly checksum: number;

    peek(): T | undefined;
    peekUnsafe(): T | undefined;
    peekMany(count: number): readonly T[];
    contains(value: T): boolean;
    indexOf(value: T): number;
    toArray(): readonly T[];
    toReversedArray(): readonly T[];
    slice(start?: number, end?: number): readonly T[];
    serialize(): ArrayBuffer;
    equals(other: ReadonlyStackInterface<T>): boolean;
    hash(): number;
    validate(): boolean;
}

/** @stable */
export interface MutableStackInterface<T> extends ReadonlyStackInterface<T> {
    push(value: T): this;
    pushUnsafe(value: T): this;
    pushMany(values: readonly T[]): this;
    pop(): T | undefined;
    popUnsafe(): T | undefined;
    popMany(count: number): readonly T[];
    swap(): this;
    duplicate(): this;
    clear(): this;
    compact(): this;
    defragment(): Promise<this>;
    dispose(): Promise<void>;
}

/** @stable */
export interface ImmutableStackInterface<out T> extends ReadonlyStackInterface<T> {
    push<U extends T>(value: U): ImmutableStackInterface<T | U>;
    pushMany<U extends readonly T[]>(values: U): ImmutableStackInterface<T>;
    pop(): readonly [T | undefined, ImmutableStackInterface<T>];
    popMany(count: number): readonly [readonly T[], ImmutableStackInterface<T>];
    concat<U>(other: ReadonlyStackInterface<U>): ImmutableStackInterface<T | U>;
    filter<U extends T>(predicate: (value: T) => value is U): ImmutableStackInterface<U>;
    map<U>(fn: (value: T) => U): ImmutableStackInterface<U>;
}
