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

class DynamicArray<T> implements HeapStorage<T> {
    private buffer: Array<T>;
    private _length: QueueSize;
    private _capacity: Capacity;

    constructor(initialCapacity: Capacity = createCapacity(16)) {
        this._capacity = initialCapacity;
        this._length = createQueueSize(0);
        this.buffer = new Array(initialCapacity);
    }

    get length(): QueueSize {
        return this._length;
    }

    get capacity(): Capacity {
        return this._capacity;
    }

    get(index: HeapIndex): T {
        return this.buffer[index];
    }

    set(index: HeapIndex, value: T): void {
        this.buffer[index] = value;
    }

    push(value: T): void {
        this.ensureCapacity(createCapacity(this._length + 1));
        this.buffer[this._length] = value;
        this._length = createQueueSize(this._length + 1);
    }

    pop(): T {
        if (this._length === 0) {
            throw new EmptyQueueError();
        }
        this._length = createQueueSize(this._length - 1);
        return this.buffer[this._length];
    }

    swap(i: HeapIndex, j: HeapIndex): void {
        const temp = this.buffer[i];
        this.buffer[i] = this.buffer[j];
        this.buffer[j] = temp;
    }

    resize(newCapacity: Capacity): void {
        if (newCapacity < this._length) {
            throw new InvalidCapacityError(newCapacity);
        }

        const newBuffer = new Array<T>(newCapacity);
        for (let i = 0; i < this._length; i++) {
            newBuffer[i] = this.buffer[i];
        }

        this.buffer = newBuffer;
        this._capacity = newCapacity;
    }

    ensureCapacity(minCapacity: Capacity): void {
        if (minCapacity > this._capacity) {
            const newCapacity = createCapacity(Math.max(minCapacity, this._capacity * 2));
            this.resize(newCapacity);
        }
    }

    trimToSize(): void {
        if (this._length < this._capacity) {
            this.resize(createCapacity(Math.max(1, this._length)));
        }
    }

    clear(): void {
        this._length = createQueueSize(0);
    }

    slice(): T[] {
        return this.buffer.slice(0, this._length);
    }
}

class BinaryMinHeap<T> implements BinaryHeapOperations<T> {
    private storage: DynamicArray<T>;
    private compare: Comparator<T>;

    constructor(comparator: Comparator<T>, initialCapacity?: Capacity) {
        this.compare = comparator;
        this.storage = new DynamicArray<T>(initialCapacity);
    }

    get size(): QueueSize {
        return this.storage.length;
    }

    get isEmpty(): boolean {
        return this.size === 0;
    }

    get capacity(): Capacity {
        return this.storage.capacity;
    }

    insert(item: T): void {
        this.storage.push(item);
        this.heapifyUp(createHeapIndex(this.size - 1));
    }

    extract(): T {
        if (this.isEmpty) {
            throw new EmptyQueueError();
        }

        const root = this.storage.get(createHeapIndex(0));
        const lastItem = this.storage.pop();

        if (!this.isEmpty) {
            this.storage.set(createHeapIndex(0), lastItem);
            this.heapifyDown(createHeapIndex(0));
        }

        return root;
    }

    peek(): T {
        if (this.isEmpty) {
            throw new EmptyQueueError();
        }
        return this.storage.get(createHeapIndex(0));
    }

    clear(): void {
        this.storage.clear();
    }

    ensureCapacity(capacity: Capacity): void {
        this.storage.ensureCapacity(capacity);
    }

    trimExcess(): void {
        this.storage.trimToSize();
    }

    contains(item: T): boolean {
        for (let i = 0; i < this.size; i++) {
            if (this.storage.get(createHeapIndex(i)) === item) {
                return true;
            }
        }
        return false;
    }

    toArray(): T[] {
        return this.storage.slice();
    }

    private heapifyUp(index: HeapIndex): void {
        while (hasParent(index)) {
            const parentIndex = getParentIndex(index);

            if (this.shouldSwap(index, parentIndex)) {
                this.storage.swap(index, parentIndex);
                index = parentIndex;
            } else {
                break;
            }
        }
    }

    private heapifyDown(index: HeapIndex): void {
        while (hasLeftChild(index, this.size)) {
            const smallestChildIndex = this.getSmallestChildIndex(index);

            if (this.shouldSwap(smallestChildIndex, index)) {
                this.storage.swap(index, smallestChildIndex);
                index = smallestChildIndex;
            } else {
                break;
            }
        }
    }

    private getSmallestChildIndex(index: HeapIndex): HeapIndex {
        const leftChildIndex = getLeftChildIndex(index);

        if (!hasRightChild(index, this.size)) {
            return leftChildIndex;
        }

        const rightChildIndex = getRightChildIndex(index);

        return this.shouldSwap(leftChildIndex, rightChildIndex) ? leftChildIndex : rightChildIndex;
    }

    private shouldSwap(childIndex: HeapIndex, parentIndex: HeapIndex): boolean {
        return this.compare(this.storage.get(childIndex), this.storage.get(parentIndex)) < 0;
    }
}
