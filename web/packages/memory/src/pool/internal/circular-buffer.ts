import { BufferOverflowError } from '../../buffering/errors';
import { InvalidCapacityError } from '../../containers/queue/errors';

const SERIALIZED_TAG = 'CircularBuffer@1' as const;

declare const __capacityBrand: unique symbol;
type Capacity = number & { readonly [__capacityBrand]: never };

type OverflowPolicy = 'overwrite' | 'reject';

type CircularBufferOptions<T> = Readonly<{
    overflowPolicy?: OverflowPolicy;
    onOverflow?: (item: T) => void;
}>;

type SerializedCircularBuffer<T> = Readonly<{
    $tag: typeof SERIALIZED_TAG;
    capacity: number;
    items: readonly T[];
}>;

function isValidCapacity(value: unknown): value is number {
    return typeof value === 'number' && Number.isSafeInteger(value) && value > 0;
}

function assertCapacity(value: number): asserts value is Capacity {
    if (!isValidCapacity(value)) {
        throw new InvalidCapacityError(value);
    }
}

function resolveIndex(index: number, size: number): number {
    return index < 0 ? size + index : index;
}

class CircularBufferError extends Error {
    readonly code: string;
    constructor(message: string, code: string) {
        super(message);
        this.name = new.target.name;
        this.code = code;
    }
}

class BufferEmptyError extends CircularBufferError {
    constructor() {
        super('Buffer is empty', 'ERR_CB_EMPTY');
    }
}

interface ReadonlyCircularBuffer<T> extends Iterable<T> {
    readonly capacity: number;
    readonly size: number;
    readonly isEmpty: boolean;
    readonly isFull: boolean;

    at(index: number): T | undefined;
    first(): T | undefined;
    last(): T | undefined;

    includes(searchElement: T, fromIndex?: number): boolean;
    indexOf(searchElement: T, fromIndex?: number): number;
    lastIndexOf(searchElement: T, fromIndex?: number): number;

    find(predicate: (value: T, index: number) => boolean): T | undefined;
    findIndex(predicate: (value: T, index: number) => boolean): number;
    some(predicate: (value: T, index: number) => boolean): boolean;
    every(predicate: (value: T, index: number) => boolean): boolean;

    forEach(callback: (value: T, index: number) => void): void;
    map<U>(fn: (value: T, index: number) => U): U[];
    filter<S extends T>(predicate: (value: T, index: number) => value is S): S[];
    filter(predicate: (value: T, index: number) => boolean): T[];
    reduce<U>(fn: (acc: U, value: T, index: number) => U, initial: U): U;
    reduce(fn: (acc: T, value: T, index: number) => T): T;

    toArray(): T[];
    slice(start?: number, end?: number): T[];
    entries(): IterableIterator<[number, T]>;
    keys(): IterableIterator<number>;
    values(): IterableIterator<T>;
    reverse(): IterableIterator<T>;

    clone(): CircularBuffer<T>;
    toJSON(): SerializedCircularBuffer<T>;
    asReadonly(): ReadonlyCircularBuffer<T>;
}

class CircularBuffer<T> implements ReadonlyCircularBuffer<T> {
    readonly #buf: (T | undefined)[];
    readonly #cap: number;
    #head: number;
    #size: number;
    readonly #policy: OverflowPolicy;
    readonly #onOverflow: ((item: T) => void) | undefined;

    constructor(capacity: number, options?: CircularBufferOptions<T>) {
        assertCapacity(capacity);
        this.#cap = capacity;
        this.#buf = new Array<T | undefined>(capacity);
        this.#head = 0;
        this.#size = 0;
        this.#policy = options?.overflowPolicy ?? 'overwrite';
        this.#onOverflow = options?.onOverflow;
    }

    static from<T>(
        iterable: Iterable<T>,
        options: CircularBufferOptions<T> & { capacity: number }
    ): CircularBuffer<T> {
        assertCapacity(options.capacity);
        const buffer = new CircularBuffer<T>(options.capacity, options);
        for (const item of iterable) {
            buffer.#pushBack(item);
        }
        return buffer;
    }

    static of<T>(...items: T[]): CircularBuffer<T> {
        if (items.length === 0) {
            throw new CircularBufferError(
                'CircularBuffer.of requires at least one element',
                'ERR_CB_INVALID_ARGS'
            );
        }
        const buffer = new CircularBuffer<T>(items.length);
        for (let i = 0; i < items.length; i++) {
            buffer.#buf[i] = items[i];
        }
        buffer.#size = items.length;
        return buffer;
    }

    static fromJSON<T>(data: unknown): CircularBuffer<T> {
        if (typeof data !== 'object' || data === null) {
            throw new CircularBufferError(
                'Invalid serialized data: expected object',
                'ERR_CB_DESERIALIZATION'
            );
        }
        const obj = data as Record<string, unknown>;
        if (obj.$tag !== SERIALIZED_TAG) {
            throw new CircularBufferError(
                `Invalid serialized data: expected tag "${SERIALIZED_TAG}"`,
                'ERR_CB_DESERIALIZATION'
            );
        }
        if (!isValidCapacity(obj.capacity)) {
            throw new CircularBufferError(
                'Invalid serialized data: invalid or missing capacity',
                'ERR_CB_DESERIALIZATION'
            );
        }
        if (!Array.isArray(obj.items)) {
            throw new CircularBufferError(
                'Invalid serialized data: invalid or missing items',
                'ERR_CB_DESERIALIZATION'
            );
        }
        const buffer = new CircularBuffer<T>(obj.capacity as number);
        for (let i = 0; i < obj.items.length; i++) {
            buffer.#pushBack(obj.items[i] as T);
        }
        return buffer;
    }

    get capacity(): number {
        return this.#cap;
    }

    get size(): number {
        return this.#size;
    }

    get isEmpty(): boolean {
        return this.#size === 0;
    }

    get isFull(): boolean {
        return this.#size === this.#cap;
    }

    at(index: number): T | undefined {
        const i = resolveIndex(index, this.#size);
        if (i < 0 || i >= this.#size) return undefined;
        return this.#buf[(this.#head + i) % this.#cap] as T;
    }

    first(): T | undefined {
        return this.#size > 0 ? (this.#buf[this.#head] as T) : undefined;
    }

    last(): T | undefined {
        return this.#size > 0
            ? (this.#buf[(this.#head + this.#size - 1) % this.#cap] as T)
            : undefined;
    }

    push(...items: T[]): number {
        for (let i = 0; i < items.length; i++) {
            this.#pushBack(items[i]);
        }
        return this.#size;
    }

    pop(): T {
        if (this.#size === 0) throw new BufferEmptyError();
        const idx = (this.#head + this.#size - 1) % this.#cap;
        const item = this.#buf[idx] as T;
        this.#buf[idx] = undefined;
        this.#size--;
        return item;
    }

    shift(): T {
        if (this.#size === 0) throw new BufferEmptyError();
        const item = this.#buf[this.#head] as T;
        this.#buf[this.#head] = undefined;
        this.#head = (this.#head + 1) % this.#cap;
        this.#size--;
        return item;
    }

    unshift(...items: T[]): number {
        for (let i = items.length - 1; i >= 0; i--) {
            this.#pushFront(items[i]);
        }
        return this.#size;
    }

    pushMany(items: Iterable<T>): number {
        if (Array.isArray(items)) {
            const n = items.length;
            if (n === 0) return this.#size;
            if (this.#policy === 'reject' && n > this.#cap - this.#size) {
                throw new BufferOverflowError(
                    `Buffer overflow: capacity ${this.#cap} reached`,
                    this.#cap
                );
            }
            if (n >= this.#cap && !this.#onOverflow) {
                const offset = n - this.#cap;
                for (let i = 0; i < this.#cap; i++) {
                    this.#buf[i] = items[offset + i];
                }
                this.#head = 0;
                this.#size = this.#cap;
                return this.#size;
            }
            for (let i = 0; i < n; i++) {
                this.#pushBack(items[i]);
            }
            return this.#size;
        }
        for (const item of items) {
            this.#pushBack(item);
        }
        return this.#size;
    }

    clear(): void {
        for (let i = 0; i < this.#cap; i++) {
            this.#buf[i] = undefined;
        }
        this.#head = 0;
        this.#size = 0;
    }

    drain(): T[] {
        const result = this.toArray();
        this.clear();
        return result;
    }

    includes(searchElement: T, fromIndex?: number): boolean {
        return this.indexOf(searchElement, fromIndex) !== -1;
    }

    indexOf(searchElement: T, fromIndex?: number): number {
        const start =
            fromIndex != null ? Math.max(0, resolveIndex(fromIndex, this.#size)) : 0;
        for (let i = start; i < this.#size; i++) {
            if (this.#buf[(this.#head + i) % this.#cap] === searchElement) return i;
        }
        return -1;
    }

    lastIndexOf(searchElement: T, fromIndex?: number): number {
        const end =
            fromIndex != null
                ? Math.min(this.#size - 1, resolveIndex(fromIndex, this.#size))
                : this.#size - 1;
        for (let i = end; i >= 0; i--) {
            if (this.#buf[(this.#head + i) % this.#cap] === searchElement) return i;
        }
        return -1;
    }

    find(predicate: (value: T, index: number) => boolean): T | undefined {
        for (let i = 0; i < this.#size; i++) {
            const value = this.#buf[(this.#head + i) % this.#cap] as T;
            if (predicate(value, i)) return value;
        }
        return undefined;
    }

    findIndex(predicate: (value: T, index: number) => boolean): number {
        for (let i = 0; i < this.#size; i++) {
            if (predicate(this.#buf[(this.#head + i) % this.#cap] as T, i)) return i;
        }
        return -1;
    }

    some(predicate: (value: T, index: number) => boolean): boolean {
        for (let i = 0; i < this.#size; i++) {
            if (predicate(this.#buf[(this.#head + i) % this.#cap] as T, i)) return true;
        }
        return false;
    }

    every(predicate: (value: T, index: number) => boolean): boolean {
        for (let i = 0; i < this.#size; i++) {
            if (!predicate(this.#buf[(this.#head + i) % this.#cap] as T, i)) return false;
        }
        return true;
    }

    forEach(callback: (value: T, index: number) => void): void {
        for (let i = 0; i < this.#size; i++) {
            callback(this.#buf[(this.#head + i) % this.#cap] as T, i);
        }
    }

    map<U>(fn: (value: T, index: number) => U): U[] {
        const out = new Array<U>(this.#size);
        for (let i = 0; i < this.#size; i++) {
            out[i] = fn(this.#buf[(this.#head + i) % this.#cap] as T, i);
        }
        return out;
    }

    filter<S extends T>(predicate: (value: T, index: number) => value is S): S[];
    filter(predicate: (value: T, index: number) => boolean): T[];
    filter(predicate: (value: T, index: number) => boolean): T[] {
        const out: T[] = [];
        for (let i = 0; i < this.#size; i++) {
            const v = this.#buf[(this.#head + i) % this.#cap] as T;
            if (predicate(v, i)) out.push(v);
        }
        return out;
    }

    reduce<U>(fn: (acc: U, value: T, index: number) => U, initial: U): U;
    reduce(fn: (acc: T, value: T, index: number) => T): T;
    reduce(fn: (acc: any, value: T, index: number) => any, initial?: any): any {
        if (this.#size === 0 && arguments.length < 2) {
            throw new TypeError('Reduce of empty buffer with no initial value');
        }
        let acc: any = arguments.length >= 2 ? initial : this.#buf[this.#head];
        const start = arguments.length >= 2 ? 0 : 1;
        for (let i = start; i < this.#size; i++) {
            acc = fn(acc, this.#buf[(this.#head + i) % this.#cap] as T, i);
        }
        return acc;
    }

    toArray(): T[] {
        const out = new Array<T>(this.#size);
        for (let i = 0; i < this.#size; i++) {
            out[i] = this.#buf[(this.#head + i) % this.#cap] as T;
        }
        return out;
    }

    slice(start?: number, end?: number): T[] {
        const s = start != null ? resolveIndex(start, this.#size) : 0;
        const e = end != null ? resolveIndex(end, this.#size) : this.#size;
        const from = Math.max(0, s);
        const to = Math.min(this.#size, e);
        if (from >= to) return [];
        const out = new Array<T>(to - from);
        for (let i = from; i < to; i++) {
            out[i - from] = this.#buf[(this.#head + i) % this.#cap] as T;
        }
        return out;
    }

    clone(): CircularBuffer<T> {
        const c = new CircularBuffer<T>(this.#cap, {
            overflowPolicy: this.#policy,
            onOverflow: this.#onOverflow,
        });
        for (let i = 0; i < this.#cap; i++) {
            c.#buf[i] = this.#buf[i];
        }
        c.#head = this.#head;
        c.#size = this.#size;
        return c;
    }

    [Symbol.iterator](): IterableIterator<T> {
        return this.values();
    }

    *entries(): IterableIterator<[number, T]> {
        for (let i = 0; i < this.#size; i++) {
            yield [i, this.#buf[(this.#head + i) % this.#cap] as T];
        }
    }

    *keys(): IterableIterator<number> {
        for (let i = 0; i < this.#size; i++) {
            yield i;
        }
    }

    *values(): IterableIterator<T> {
        for (let i = 0; i < this.#size; i++) {
            yield this.#buf[(this.#head + i) % this.#cap] as T;
        }
    }

    *reverse(): IterableIterator<T> {
        for (let i = this.#size - 1; i >= 0; i--) {
            yield this.#buf[(this.#head + i) % this.#cap] as T;
        }
    }

    toJSON(): SerializedCircularBuffer<T> {
        return {
            $tag: SERIALIZED_TAG,
            capacity: this.#cap,
            items: this.toArray(),
        };
    }

    toString(): string {
        return `CircularBuffer(${this.#size}/${this.#cap}) [${this.toArray().join(', ')}]`;
    }

    get [Symbol.toStringTag](): string {
        return 'CircularBuffer';
    }

    asReadonly(): ReadonlyCircularBuffer<T> {
        return this;
    }

    #pushBack(item: T): void {
        if (this.#size === this.#cap) {
            if (this.#policy === 'reject')
                throw new BufferOverflowError(
                    `Buffer overflow: capacity ${this.#cap} reached`,
                    this.#cap
                );
            const dropped = this.#buf[this.#head] as T;
            this.#buf[this.#head] = item;
            this.#head = (this.#head + 1) % this.#cap;
            this.#onOverflow?.(dropped);
        } else {
            this.#buf[(this.#head + this.#size) % this.#cap] = item;
            this.#size++;
        }
    }

    #pushFront(item: T): void {
        if (this.#size === this.#cap) {
            if (this.#policy === 'reject')
                throw new BufferOverflowError(
                    `Buffer overflow: capacity ${this.#cap} reached`,
                    this.#cap
                );
            this.#head = (this.#head - 1 + this.#cap) % this.#cap;
            const dropped = this.#buf[this.#head] as T;
            this.#onOverflow?.(dropped);
            this.#buf[this.#head] = item;
        } else {
            this.#head = (this.#head - 1 + this.#cap) % this.#cap;
            this.#buf[this.#head] = item;
            this.#size++;
        }
    }
}

function createCircularBuffer<T>(
    capacity: number,
    options?: CircularBufferOptions<T>
): CircularBuffer<T> {
    return new CircularBuffer<T>(capacity, options);
}

export {
    CircularBuffer,
    CircularBufferError,
    BufferEmptyError,
    createCircularBuffer,
    type CircularBufferOptions,
    type ReadonlyCircularBuffer,
    type SerializedCircularBuffer,
    type OverflowPolicy,
};
