import { BinaryHeapOperations, HeapIndex, QueueSize, Capacity, Comparator } from './types';
import { DynamicArray } from './dynamic-array';
import { getParentIndex, getLeftChildIndex, getRightChildIndex, hasParent, hasLeftChild, hasRightChild } from './utils';
import { EmptyQueueError } from './errors';

export class BinaryMinHeap<T> implements BinaryHeapOperations<T> {
    private storage: DynamicArray<T>;
    private compare: Comparator<T>;

    constructor(comparator: Comparator<T>, initialCapacity?: Capacity) {
        this.compare = comparator;
        this.storage = new DynamicArray<T>(initialCapacity ?? undefined as any);
    }

    get size(): QueueSize {
        return this.storage.length as QueueSize;
    }

    get isEmpty(): boolean {
        return (this.size as unknown as number) === 0;
    }

    get capacity(): Capacity {
        return this.storage.capacity as Capacity;
    }

    insert(item: T): void {
        this.storage.push(item);
        this.heapifyUp(createHeapIndex(this.size as unknown as number - 1));
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
        for (let i = 0; i < (this.size as unknown as number); i++) {
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
        while (hasLeftChild(index, this.size as QueueSize)) {
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

        if (!hasRightChild(index, this.size as QueueSize)) {
            return leftChildIndex;
        }

        const rightChildIndex = getRightChildIndex(index);

        return this.shouldSwap(leftChildIndex, rightChildIndex) ? leftChildIndex : rightChildIndex;
    }

    private shouldSwap(childIndex: HeapIndex, parentIndex: HeapIndex): boolean {
        return this.compare(this.storage.get(childIndex), this.storage.get(parentIndex)) < 0;
    }
}

function createHeapIndex(value: number): HeapIndex {
    return value as HeapIndex;
}
