import type { Opaque } from '@axrone/utility';

type Nominal<T, K extends PropertyKey> = Opaque<T, K>;
export type { Opaque as Nominal };

/** @stable */
export type Comparator<T> = (a: T, b: T) => number;

/** @stable */
export type HeapIndex = Nominal<number, 'HeapIndex'>;
/** @stable */
export type QueueSize = Nominal<number, 'QueueSize'>;
/** @stable */
export type Capacity = Nominal<number, 'Capacity'>;

/** @stable */
export interface BinaryHeapOperations<T> {
    insert(item: T): void;
    extract(): T;

    peek(): T;
    readonly size: QueueSize;
    readonly isEmpty: boolean;

    clear(): void;
}
