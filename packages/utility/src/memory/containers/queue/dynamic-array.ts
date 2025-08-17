import { HeapIndex, QueueSize, Capacity, HeapStorage } from './types';
import { EmptyQueueError, InvalidCapacityError } from './errors';
import { createQueueSize, createCapacity } from './utils';

export class DynamicArray<T> implements HeapStorage<T> {
    private buffer: Array<T>;
    private _length: QueueSize;
    private _capacity: Capacity;

    constructor(initialCapacity: Capacity = createCapacity(16)) {
        this._capacity = initialCapacity;
        this._length = createQueueSize(0);
        this.buffer = new Array(initialCapacity as unknown as number);
    }

    get length(): QueueSize {
        return this._length;
    }

    get capacity(): Capacity {
        return this._capacity;
    }

    get(index: HeapIndex): T {
        return this.buffer[index as unknown as number];
    }

    set(index: HeapIndex, value: T): void {
        this.buffer[index as unknown as number] = value;
    }

    push(value: T): void {
        this.ensureCapacity(createCapacity((this._length as unknown as number) + 1));
        this.buffer[this._length as unknown as number] = value;
        this._length = createQueueSize((this._length as unknown as number) + 1);
    }

    pop(): T {
        if ((this._length as unknown as number) === 0) {
            throw new EmptyQueueError();
        }
        this._length = createQueueSize((this._length as unknown as number) - 1);
        return this.buffer[this._length as unknown as number];
    }

    swap(i: HeapIndex, j: HeapIndex): void {
        const ii = i as unknown as number;
        const jj = j as unknown as number;
        const temp = this.buffer[ii];
        this.buffer[ii] = this.buffer[jj];
        this.buffer[jj] = temp;
    }

    resize(newCapacity: Capacity): void {
        if ((newCapacity as unknown as number) < (this._length as unknown as number)) {
            throw new InvalidCapacityError(newCapacity as unknown as number);
        }

        const newBuffer = new Array<T>(newCapacity as unknown as number);
        for (let i = 0; i < (this._length as unknown as number); i++) {
            newBuffer[i] = this.buffer[i];
        }

        this.buffer = newBuffer;
        this._capacity = newCapacity;
    }

    ensureCapacity(minCapacity: Capacity): void {
        if ((minCapacity as unknown as number) > (this._capacity as unknown as number)) {
            const newCapacity = createCapacity(Math.max(minCapacity as unknown as number, (this._capacity as unknown as number) * 2));
            this.resize(newCapacity);
        }
    }

    trimToSize(): void {
        if ((this._length as unknown as number) < (this._capacity as unknown as number)) {
            this.resize(createCapacity(Math.max(1, this._length as unknown as number)));
        }
    }

    clear(): void {
        this._length = createQueueSize(0);
    }

    slice(): T[] {
        return this.buffer.slice(0, this._length as unknown as number);
    }
}
