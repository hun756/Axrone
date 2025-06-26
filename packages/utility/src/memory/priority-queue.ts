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

abstract class QueueError extends Error {
    abstract readonly code: string;

    constructor(message: string) {
        super(message);
        this.name = this.constructor.name;
        Error.captureStackTrace?.(this, this.constructor);
    }
}

class EmptyQueueError extends QueueError {
    readonly code = 'EMPTY_QUEUE' as const;

    constructor() {
        super('Queue is empty');
    }
}

class InvalidCapacityError extends QueueError {
    readonly code = 'INVALID_CAPACITY' as const;

    constructor(capacity: number) {
        super(`Invalid capacity: ${capacity}`);
    }
}

const createHeapIndex = (value: number): HeapIndex => value as HeapIndex;

const createQueueSize = (value: number): QueueSize => value as QueueSize;

const createCapacity = (value: number): Capacity => value as Capacity;

const getParentIndex = (index: HeapIndex): HeapIndex => createHeapIndex((index - 1) >>> 1);

const getLeftChildIndex = (index: HeapIndex): HeapIndex => createHeapIndex((index << 1) | 1);

const getRightChildIndex = (index: HeapIndex): HeapIndex => createHeapIndex((index + 1) << 1);

const hasParent = (index: HeapIndex): boolean => index > 0;

const hasLeftChild = (index: HeapIndex, size: QueueSize): boolean =>
    getLeftChildIndex(index) < size;

const hasRightChild = (index: HeapIndex, size: QueueSize): boolean =>
    getRightChildIndex(index) < size;

const defaultComparator = <T>(a: T, b: T): number => {
    return a < b ? -1 : a > b ? 1 : 0;
};

