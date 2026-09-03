/**
 * Shared helper functions and types for heap and priority queue implementations.
 * @internal
 */

import type { Comparator } from '../containers/queue/types';
import { HeapComparatorError, HeapSerializationError } from '../containers/queue/heap-errors';

/** @stable */
export type HeapOrder = 'min' | 'max';

/** @stable */
export type Equality<T> = (left: T, right: T) => boolean;

/** @stable */
export type HeapPrimitive = number | bigint | string | Date;

/** @stable */
export type HeapSerialized<T> = Readonly<{
    readonly kind: 'BinaryHeap';
    readonly version: 1;
    readonly order: HeapOrder;
    readonly items: readonly T[];
}>;

/** @stable */
export type HeapLike<T> = Iterable<T> | ArrayLike<T>;

const InternalOrder = {
    Min: 1,
    Max: -1,
} as const;

export type InternalOrder = (typeof InternalOrder)[keyof typeof InternalOrder];

export const isFunction = (value: unknown): value is (...args: readonly unknown[]) => unknown =>
    typeof value === 'function';

export const isObject = (value: unknown): value is Record<PropertyKey, unknown> =>
    value !== null && typeof value === 'object';

export const defaultPrimitiveComparator = <T extends HeapPrimitive>(left: T, right: T): number => {
    if (left === right) return 0;

    if (typeof left === 'number' && typeof right === 'number') {
        if (Number.isNaN(left)) return Number.isNaN(right) ? 0 : 1;
        if (Number.isNaN(right)) return -1;
        return left < right ? -1 : 1;
    }

    const leftValue = left instanceof Date ? left.getTime() : left;
    const rightValue = right instanceof Date ? right.getTime() : right;

    return leftValue < rightValue ? -1 : 1;
};

export const defaultEquality = <T>(left: T, right: T): boolean => Object.is(left, right);

export const ensureComparator = <T>(comparator: unknown): Comparator<T> => {
    if (!isFunction(comparator)) {
        throw new HeapComparatorError();
    }

    return comparator as Comparator<T>;
};

export const ensureEquality = <T>(equality: Equality<T> | undefined): Equality<T> =>
    equality ?? defaultEquality;

export const normalizeOrder = (order: HeapOrder | undefined): HeapOrder => (order === 'max' ? 'max' : 'min');

export const internalOrderOf = (order: HeapOrder): InternalOrder =>
    order === 'max' ? InternalOrder.Max : InternalOrder.Min;

export const collectToArray = <T>(source: HeapLike<T> | undefined): T[] => {
    if (source === undefined) {
        return [];
    }

    if (Array.isArray(source)) {
        return source.slice();
    }

    const length = (source as ArrayLike<T>).length;

    if (typeof length === 'number') {
        const result = new Array<T>(length);

        for (let index = 0; index < length; index++) {
            result[index] = (source as ArrayLike<T>)[index]!;
        }

        return result;
    }

    const result: T[] = [];

    for (const value of source as Iterable<T>) {
        result.push(value);
    }

    return result;
};

export const compareByOrder = <T>(
    order: InternalOrder,
    comparator: Comparator<T>,
    left: T,
    right: T
): boolean => comparator(left, right) * order < 0;

export const ensureSerializable = <T>(value: unknown): HeapSerialized<T> => {
    if (!isObject(value)) {
        throw new HeapSerializationError();
    }

    if (value.kind !== 'BinaryHeap' || value.version !== 1) {
        throw new HeapSerializationError();
    }

    if (value.order !== 'min' && value.order !== 'max') {
        throw new HeapSerializationError();
    }

    if (!Array.isArray(value.items)) {
        throw new HeapSerializationError();
    }

    return value as HeapSerialized<T>;
};

export const siftUpThreshold = (baseLength: number, incoming: number): number => {
    if (incoming <= 0) return 0;
    const denom = Math.log2(baseLength + incoming + 1);
    if (denom <= 0) return baseLength;
    return Math.ceil(baseLength / denom);
};
