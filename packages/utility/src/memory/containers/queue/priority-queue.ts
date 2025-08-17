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

class PriorityQueueNode<TElement, TPriority> implements QueueNode<TElement, TPriority> {
    constructor(
        public element: TElement,
        public priority: TPriority
    ) {}
}

type PriorityQueueOptions<TPriority> = {
    readonly comparator?: Comparator<TPriority>;
    readonly initialCapacity?: Capacity;
    readonly autoTrim?: boolean;
};

export class PriorityQueue<TElement, TPriority = number>
    implements
        PriorityQueueCore<TElement, TPriority>,
        OptionalOperations<TElement, TPriority>,
        QueryOperations<TElement>,
        CapacityOperations,
        Iterable<TElement>
{
    private heap: BinaryMinHeap<PriorityQueueNode<TElement, TPriority>>;
    private comparator: Comparator<TPriority>;
    private autoTrimEnabled: boolean;

    constructor(options?: PriorityQueueOptions<TPriority>) {
        this.comparator = options?.comparator ?? (defaultComparator as Comparator<TPriority>);
        this.autoTrimEnabled = options?.autoTrim ?? false;

        const nodeComparator: Comparator<PriorityQueueNode<TElement, TPriority>> = (a, b) =>
            this.comparator(a.priority, b.priority);

        this.heap = new BinaryMinHeap(nodeComparator, options?.initialCapacity);
    }

    get size(): QueueSize {
        return this.heap.size;
    }

    get isEmpty(): boolean {
        return this.heap.isEmpty;
    }

    get capacity(): Capacity {
        return this.heap.capacity;
    }

    enqueue(element: TElement, priority: TPriority): void {
        const node = new PriorityQueueNode(element, priority);
        this.heap.insert(node);
    }

    dequeue(): TElement {
        const node = this.heap.extract();

        if (this.autoTrimEnabled && this.shouldAutoTrim()) {
            this.trimExcess();
        }

        return node.element;
    }

    peek(): TElement {
        const node = this.heap.peek();
        return node.element;
    }

    tryDequeue(): TElement | undefined {
        if (this.isEmpty) {
            return undefined;
        }
        return this.dequeue();
    }

    tryPeek(): TElement | undefined {
        if (this.isEmpty) {
            return undefined;
        }
        return this.peek();
    }

    dequeueAll(): TElement[] {
        const result: TElement[] = [];
        while (!this.isEmpty) {
            result.push(this.dequeue());
        }
        return result;
    }

    enqueueRange(items: ReadonlyArray<ReadonlyQueueNode<TElement, TPriority>>): void {
        if (items.length === 0) return;

        this.ensureCapacity(createCapacity(this.size + items.length));

        for (const item of items) {
            this.enqueue(item.element, item.priority);
        }
    }

    contains(element: TElement): boolean {
        return this.heap.toArray().some((node) => node.element === element);
    }

    clear(): void {
        this.heap.clear();
    }

    ensureCapacity(capacity: Capacity): void {
        this.heap.ensureCapacity(capacity);
    }

    trimExcess(): void {
        this.heap.trimExcess();
    }

    toArray(): ReadonlyArray<TElement> {
        return Object.freeze(this.heap.toArray().map((node) => node.element));
    }

    clone(): PriorityQueue<TElement, TPriority> {
        const cloned = new PriorityQueue<TElement, TPriority>({
            comparator: this.comparator,
            initialCapacity: this.capacity,
            autoTrim: this.autoTrimEnabled,
        });

        const nodes = this.heap.toArray();
        const nodeItems = nodes.map((node) => ({
            element: node.element,
            priority: node.priority,
        }));

        cloned.enqueueRange(nodeItems);
        return cloned;
    }

    *[Symbol.iterator](): Iterator<TElement> {
        const clone = this.clone();

        while (!clone.isEmpty) {
            yield clone.dequeue();
        }
    }

    static from<T, P = number>(
        items: Iterable<ReadonlyQueueNode<T, P>>,
        options?: PriorityQueueOptions<P>
    ): PriorityQueue<T, P> {
        const queue = new PriorityQueue<T, P>(options);
        const itemArray = Array.isArray(items) ? items : Array.from(items);
        queue.enqueueRange(itemArray);
        return queue;
    }

    static withComparator<T, P>(
        comparator: Comparator<P>,
        initialCapacity?: Capacity
    ): PriorityQueue<T, P> {
        return new PriorityQueue<T, P>({
            comparator,
            initialCapacity,
        });
    }

    static minQueue<T, P = number>(initialCapacity?: Capacity): PriorityQueue<T, P> {
        return new PriorityQueue<T, P>({
            comparator: defaultComparator as Comparator<P>,
            initialCapacity,
        });
    }

    static maxQueue<T, P = number>(initialCapacity?: Capacity): PriorityQueue<T, P> {
        const maxComparator: Comparator<P> = (a, b) => (defaultComparator as Comparator<P>)(b, a);

        return new PriorityQueue<T, P>({
            comparator: maxComparator,
            initialCapacity,
        });
    }

    private shouldAutoTrim(): boolean {
        return this.capacity > 32 && this.size < this.capacity / 4;
    }
}

export type {
    Comparator,
    HeapIndex,
    QueueSize,
    Capacity,
    ReadonlyQueueNode,
    QueueNode,
    PriorityQueueOptions,
    PriorityQueueCore,
    OptionalOperations,
    QueryOperations,
    CapacityOperations,
};

export {
    QueueError,
    EmptyQueueError,
    InvalidCapacityError,
    PriorityQueueNode,
    createHeapIndex,
    createQueueSize,
    createCapacity,
    defaultComparator,
};
