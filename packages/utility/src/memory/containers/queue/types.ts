declare const __nominal: unique symbol;

export type Nominal<T, K> = T & { readonly [__nominal]: K };

export type Comparator<T> = (a: T, b: T) => number;

export type HeapIndex = Nominal<number, 'HeapIndex'>;

export type QueueSize = Nominal<number, 'QueueSize'>;

export type Capacity = Nominal<number, 'Capacity'>;

export interface ReadonlyQueueNode<TElement, TPriority> {
    readonly element: TElement;
    readonly priority: TPriority;
}

export interface QueueNode<TElement, TPriority> extends ReadonlyQueueNode<TElement, TPriority> {
    element: TElement;
    priority: TPriority;
}

export interface HeapStorage<T> {
    readonly length: QueueSize;
    readonly capacity: Capacity;
    get(index: HeapIndex): T;
    set(index: HeapIndex, value: T): void;
    resize(newCapacity: Capacity): void;
    clear(): void;
}

export interface BinaryHeapOperations<T> {
    insert(item: T): void;
    extract(): T;
    peek(): T;
    clear(): void;
    readonly size: QueueSize;
    readonly isEmpty: boolean;
}

export interface PriorityQueueCore<TElement, TPriority> {
    enqueue(element: TElement, priority: TPriority): void;
    dequeue(): TElement;
    peek(): TElement;
    clear(): void;
    readonly size: QueueSize;
    readonly isEmpty: boolean;
}

export interface OptionalOperations<TElement, TPriority> {
    tryDequeue(): TElement | undefined;
    tryPeek(): TElement | undefined;
    dequeueAll(): TElement[];
    enqueueRange(items: ReadonlyArray<ReadonlyQueueNode<TElement, TPriority>>): void;
}

export interface QueryOperations<TElement> {
    contains(element: TElement): boolean;
    toArray(): ReadonlyArray<TElement>;
}

export interface CapacityOperations {
    ensureCapacity(capacity: Capacity): void;
    trimExcess(): void;
    readonly capacity: Capacity;
}

export type PriorityQueueOptions<TPriority> = {
    readonly comparator?: Comparator<TPriority>;
    readonly initialCapacity?: Capacity;
    readonly autoTrim?: boolean;
};
