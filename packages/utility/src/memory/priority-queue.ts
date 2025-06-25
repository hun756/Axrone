declare const __nominal: unique symbol;

type Nominal<T, K> = T & { readonly [__nominal]: K };

type Comparator<T> = (a: T, b: T) => number;

type HeapIndex = Nominal<number, 'HeapIndex'>;

type QueueSize = Nominal<number, 'QueueSize'>;

type Capacity = Nominal<number, 'Capacity'>;

interface ReadonlyQueueNode<TElement, TPriority> {
    readonly element: TElement;
    readonly priority: TPriority;
}

interface QueueNode<TElement, TPriority> extends ReadonlyQueueNode<TElement, TPriority> {
    element: TElement;
    priority: TPriority;
}

interface HeapStorage<T> {
    readonly length: QueueSize;
    readonly capacity: Capacity;
    get(index: HeapIndex): T;
    set(index: HeapIndex, value: T): void;
    resize(newCapacity: Capacity): void;
    clear(): void;
}

interface BinaryHeapOperations<T> {
    insert(item: T): void;
    extract(): T;
    peek(): T;
    clear(): void;
    readonly size: QueueSize;
    readonly isEmpty: boolean;
}

interface PriorityQueueCore<TElement, TPriority> {
    enqueue(element: TElement, priority: TPriority): void;
    dequeue(): TElement;
    peek(): TElement;
    clear(): void;
    readonly size: QueueSize;
    readonly isEmpty: boolean;
}

interface OptionalOperations<TElement, TPriority> {
    tryDequeue(): TElement | undefined;
    tryPeek(): TElement | undefined;
    dequeueAll(): TElement[];
    enqueueRange(items: ReadonlyArray<ReadonlyQueueNode<TElement, TPriority>>): void;
}

interface QueryOperations<TElement> {
    contains(element: TElement): boolean;
    toArray(): ReadonlyArray<TElement>;
}

interface CapacityOperations {
    ensureCapacity(capacity: Capacity): void;
    trimExcess(): void;
    readonly capacity: Capacity;
}
