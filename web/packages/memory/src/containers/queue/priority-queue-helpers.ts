/**
 * Internal helper functions and types for the PriorityQueue implementation.
 * @internal
 */

import type {
    PriorityOrder,
    PriorityQueueEntry,
    PriorityQueueHandle,
    PriorityQueueSerialized,
    QueueLike,
} from './priority-queue';
import {
    PriorityQueueComparatorError,
    PriorityQueueSerializationError,
} from './priority-queue-errors';
import type { Comparator, Equality } from './binary-heap';
import { defaultPrimitiveComparator } from './binary-heap';

const InternalOrder = {
    Min: 1,
    Max: -1,
} as const;

export type InternalOrder = (typeof InternalOrder)[keyof typeof InternalOrder];
export { InternalOrder };

export type Node<T, P> = {
    value: T;
    priority: P;
    handle: PriorityQueueHandle;
    sequence: number;
};

export const isFunction = (value: unknown): value is (...args: readonly unknown[]) => unknown =>
    typeof value === 'function';

export const isObject = (value: unknown): value is Record<PropertyKey, unknown> =>
    value !== null && typeof value === 'object';

export const isEntryLike = <T, P>(value: unknown): value is PriorityQueueEntry<T, P> =>
    isObject(value) && 'value' in value && 'priority' in value;

export const defaultEquality = <T>(left: T, right: T): boolean => Object.is(left, right);

export const ensureComparator = <P>(comparator: unknown): Comparator<P> => {
    if (!isFunction(comparator)) {
        throw new PriorityQueueComparatorError();
    }

    return comparator as Comparator<P>;
};

export const normalizeOrder = (order: PriorityOrder | undefined): PriorityOrder =>
    order === 'min' ? 'min' : 'max';

export const internalOrderOf = (order: PriorityOrder): InternalOrder =>
    order === 'max' ? InternalOrder.Max : InternalOrder.Min;

export const toHandle = (value: number): PriorityQueueHandle => value as PriorityQueueHandle;

export const collectEntries = <T, P>(
    source: QueueLike<PriorityQueueEntry<T, P>> | undefined
): PriorityQueueEntry<T, P>[] => {
    if (source === undefined) {
        return [];
    }

    if (Array.isArray(source)) {
        return source.slice();
    }

    const length = (source as ArrayLike<PriorityQueueEntry<T, P>>).length;

    if (typeof length === 'number') {
        const result = new Array<PriorityQueueEntry<T, P>>(length);

        for (let index = 0; index < length; index++) {
            result[index] = (source as ArrayLike<PriorityQueueEntry<T, P>>)[index]!;
        }

        return result;
    }

    const result: PriorityQueueEntry<T, P>[] = [];

    for (const entry of source as Iterable<PriorityQueueEntry<T, P>>) {
        result.push(entry);
    }

    return result;
};

export const collectValues = <T>(source: QueueLike<T>): T[] => {
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

export const ensureSerialized = <T, P>(value: unknown): PriorityQueueSerialized<T, P> => {
    if (!isObject(value)) {
        throw new PriorityQueueSerializationError();
    }

    if (value.kind !== 'PriorityQueue' || value.version !== 1) {
        throw new PriorityQueueSerializationError();
    }

    if (value.order !== 'min' && value.order !== 'max') {
        throw new PriorityQueueSerializationError();
    }

    if (!Array.isArray(value.items)) {
        throw new PriorityQueueSerializationError();
    }

    for (let index = 0; index < value.items.length; index++) {
        if (!isEntryLike<T, P>(value.items[index])) {
            throw new PriorityQueueSerializationError();
        }
    }

    return value as PriorityQueueSerialized<T, P>;
};

export const siftUpThreshold = (baseLength: number, incoming: number): number =>
    Math.ceil(baseLength / Math.log2(baseLength + incoming + 1));

export class NodeIterator<T, P, R> implements IterableIterator<R> {
    readonly #store: Node<T, P>[];
    readonly #select: (node: Node<T, P>) => R;
    #index = 0;

    constructor(store: Node<T, P>[], select: (node: Node<T, P>) => R) {
        this.#store = store;
        this.#select = select;
    }

    next(): IteratorResult<R> {
        const index = this.#index;

        if (index >= this.#store.length) {
            return { value: undefined as unknown as R, done: true };
        }

        this.#index = index + 1;
        return { value: this.#select(this.#store[index]!), done: false };
    }

    [Symbol.iterator](): this {
        return this;
    }
}

/** Re-export defaultPrimitiveComparator for convenience. */
export { defaultPrimitiveComparator };
