import type { Comparator, HeapOrder } from '../containers/queue/binary-heap';
import { heapSiftUp, heapSiftDown } from './heap-sift';
import type { HeapComesBefore, HeapOnMove } from './heap-sift';

export type { Comparator, HeapOrder };

declare const __indexedHeapBrand: unique symbol;
declare const __indexedHeapVersion: unique symbol;
declare const __indexedHeapState: unique symbol;

export type IndexedHeapBrand = { readonly [__indexedHeapBrand]: true };
export type IndexedHeapVersion = { readonly [__indexedHeapVersion]: 1 };
export type IndexedHeapState = { readonly [__indexedHeapState]: 'open' | 'sealed' };

export interface IndexedHeapOptions<K, V> {
    readonly compare: Comparator<K>;
    readonly order?: HeapOrder;
    readonly initialCapacity?: number;
}

export interface IndexedHeapEntryView<K, V> {
    readonly key: K;
    readonly value: V;
}

interface HeapNode<K, V> {
    key: K;
    value: V;
    handle: HeapHandle<K, V>;
}

export class HeapHandle<K = unknown, V = unknown> {
    readonly #id: number;
    readonly #heap: IndexedHeap<K, V>;
    #state: 'valid' | 'removed' = 'valid';

    constructor(id: number, heap: IndexedHeap<K, V>) {
        this.#id = id;
        this.#heap = heap;
    }

    get id(): number {
        return this.#id;
    }

    get isValid(): boolean {
        return this.#state === 'valid';
    }

    get heap(): IndexedHeap<K, V> {
        return this.#heap;
    }

    invalidate(): void {
        this.#state = 'removed';
    }

    equals(other: HeapHandle<K, V>): boolean {
        return this.#id === other.#id && this.#heap === other.#heap;
    }
}

export class IndexedHeap<K, V> implements Iterable<IndexedHeapEntryView<K, V>> {
    readonly #brand: IndexedHeapBrand = null as never;
    readonly #version: IndexedHeapVersion = null as never;
    readonly #state: IndexedHeapState = null as never;

    readonly #compare: Comparator<K>;
    readonly #isMinHeap: boolean;
    #nodes: HeapNode<K, V>[];
    #handleToIndex: Map<HeapHandle<K, V>, number>;
    #size: number = 0;
    #nextHandleId: number = 1;
    #modCount: number = 0;
    readonly #comesBeforeFn: HeapComesBefore<HeapNode<K, V>>;
    readonly #onMoveFn: HeapOnMove<HeapNode<K, V>>;

    constructor(options: IndexedHeapOptions<K, V>) {
        const compare = options.compare;
        if (typeof compare !== 'function') {
            throw new TypeError('IndexedHeap: compare must be a function');
        }
        this.#compare = compare;
        this.#isMinHeap = (options.order ?? 'min') === 'min';

        const initialCapacity = Math.max(0, options.initialCapacity ?? 0);
        this.#nodes = new Array(initialCapacity);
        this.#handleToIndex = new Map();
        this.#comesBeforeFn = (a: HeapNode<K, V>, b: HeapNode<K, V>): boolean =>
            this.#comesBeforeByKey(a.key, b.key);
        this.#onMoveFn = (node: HeapNode<K, V>, newIndex: number): void =>
            this.#handleToIndex.set(node.handle, newIndex);
    }

    get size(): number {
        return this.#size;
    }

    get isEmpty(): boolean {
        return this.#size === 0;
    }

    get capacity(): number {
        return this.#nodes.length;
    }

    get modCount(): number {
        return this.#modCount;
    }

    clear(): void {
        if (this.#size > 0) {
            for (let i = 0; i < this.#size; i++) {
                const node = this.#nodes[i]!;
                node.handle.invalidate();
                this.#nodes[i] = undefined as unknown as HeapNode<K, V>;
            }
            this.#handleToIndex.clear();
            this.#size = 0;
        }
        this.#modCount++;
    }

    peek(): IndexedHeapEntryView<K, V> | undefined {
        return this.#size === 0 ? undefined : this.#viewAt(0);
    }

    peekHandle(): HeapHandle<K, V> | undefined {
        return this.#size === 0 ? undefined : this.#nodes[0]!.handle;
    }

    push(key: K, value: V): HeapHandle<K, V> {
        const handle = new HeapHandle<K, V>(this.#nextHandleId++, this);
        if (this.#nextHandleId > Number.MAX_SAFE_INTEGER) {
            this.#nextHandleId = 1;
        }
        this.#insertAtEnd(key, value, handle);
        this.#modCount++;
        this.#siftUp(this.#size - 1);
        return handle;
    }

    pop(): IndexedHeapEntryView<K, V> | undefined {
        if (this.#size === 0) return undefined;
        this.#modCount++;

        const root = this.#nodes[0]!;
        const lastIndex = this.#size - 1;

        if (lastIndex > 0) {
            const last = this.#nodes[lastIndex]!;
            this.#nodes[0] = last;
            this.#handleToIndex.set(last.handle, 0);
            this.#size = lastIndex;
            this.#siftDown(0);
        } else {
            this.#size = 0;
        }

        this.#handleToIndex.delete(root.handle);
        root.handle.invalidate();
        return { key: root.key, value: root.value };
    }

    popHandle(): HeapHandle<K, V> | undefined {
        if (this.#size === 0) return undefined;
        this.#modCount++;

        const root = this.#nodes[0]!;
        const lastIndex = this.#size - 1;

        if (lastIndex > 0) {
            const last = this.#nodes[lastIndex]!;
            this.#nodes[0] = last;
            this.#handleToIndex.set(last.handle, 0);
            this.#size = lastIndex;
            this.#siftDown(0);
        } else {
            this.#size = 0;
        }

        this.#handleToIndex.delete(root.handle);
        root.handle.invalidate();
        return root.handle;
    }

    update(handle: HeapHandle<K, V>, newKey: K, newValue?: V): boolean {
        const index = this.#handleToIndex.get(handle);
        if (index === undefined || handle.heap !== this) return false;
        const node = this.#nodes[index]!;
        const oldKey = node.key;

        const cmp = this.#compare(newKey, oldKey);
        const valueChanged = newValue !== undefined && !Object.is(newValue, node.value);
        if (cmp === 0 && !valueChanged) return true;

        node.key = newKey;
        if (valueChanged) node.value = newValue as V;
        this.#modCount++;

        if (cmp < 0) {
            this.#siftUp(index);
        } else if (cmp > 0) {
            this.#siftDown(index);
        }
        return true;
    }

    remove(handle: HeapHandle<K, V>): IndexedHeapEntryView<K, V> | undefined {
        const index = this.#handleToIndex.get(handle);
        if (index === undefined || handle.heap !== this) return undefined;
        const node = this.#nodes[index]!;
        const view: IndexedHeapEntryView<K, V> = { key: node.key, value: node.value };

        this.#modCount++;
        const lastIndex = this.#size - 1;

        if (index === lastIndex) {
            this.#size = lastIndex;
        } else {
            const last = this.#nodes[lastIndex]!;
            this.#nodes[index] = last;
            this.#handleToIndex.set(last.handle, index);
            this.#size = lastIndex;

            if (index > 0) {
                const parentIndex = (index - 1) >>> 1;
                if (this.#comesBefore(index, parentIndex)) {
                    this.#siftUp(index);
                } else {
                    this.#siftDown(index);
                }
            } else {
                this.#siftDown(0);
            }
        }

        this.#handleToIndex.delete(handle);
        handle.invalidate();
        return view;
    }

    contains(handle: HeapHandle<K, V>): boolean {
        return this.#handleToIndex.has(handle) && handle.heap === this;
    }

    get(handle: HeapHandle<K, V>): IndexedHeapEntryView<K, V> | undefined {
        const index = this.#handleToIndex.get(handle);
        if (index === undefined || handle.heap !== this) return undefined;
        const node = this.#nodes[index]!;
        return { key: node.key, value: node.value };
    }

    toSortedArray(): IndexedHeapEntryView<K, V>[] {
        const clone = new IndexedHeap<K, V>({
            compare: this.#compare,
            order: this.#isMinHeap ? 'min' : 'max',
            initialCapacity: this.#size,
        });
        for (let i = 0; i < this.#size; i++) {
            const node = this.#nodes[i]!;
            clone.push(node.key, node.value);
        }
        const result: IndexedHeapEntryView<K, V>[] = new Array(this.#size);
        for (let i = 0; i < this.#size; i++) {
            result[i] = clone.pop()!;
        }
        return result;
    }

    *entries(): Generator<readonly [HeapHandle<K, V>, IndexedHeapEntryView<K, V>]> {
        for (let i = 0; i < this.#size; i++) {
            const node = this.#nodes[i]!;
            yield [node.handle, { key: node.key, value: node.value }] as const;
        }
    }

    *handles(): Generator<HeapHandle<K, V>> {
        for (let i = 0; i < this.#size; i++) {
            yield this.#nodes[i]!.handle;
        }
    }

    [Symbol.iterator](): Iterator<IndexedHeapEntryView<K, V>> {
        const snapshot = this.toSortedArray();
        let index = 0;
        return {
            next(): IteratorResult<IndexedHeapEntryView<K, V>> {
                if (index >= snapshot.length) {
                    return { value: undefined as unknown as IndexedHeapEntryView<K, V>, done: true };
                }
                return { value: snapshot[index++], done: false };
            },
        };
    }

    #viewAt(heapIndex: number): IndexedHeapEntryView<K, V> {
        const node = this.#nodes[heapIndex]!;
        return { key: node.key, value: node.value };
    }

    #insertAtEnd(key: K, value: V, handle: HeapHandle<K, V>): void {
        const heapIndex = this.#size;
        const node: HeapNode<K, V> = {
            key,
            value,
            handle,
        };

        if (heapIndex < this.#nodes.length) {
            this.#nodes[heapIndex] = node;
        } else {
            this.#nodes.push(node);
        }
        this.#handleToIndex.set(handle, heapIndex);
        this.#size = heapIndex + 1;
    }

    #siftUp(startIndex: number): void {
        heapSiftUp(this.#nodes, startIndex, this.#comesBeforeFn, this.#onMoveFn);
    }

    #siftDown(startIndex: number): void {
        heapSiftDown(this.#nodes, this.#size, startIndex, this.#comesBeforeFn, this.#onMoveFn);
    }

    #comesBefore(i: number, j: number): boolean {
        const a = this.#nodes[i]!.key;
        const b = this.#nodes[j]!.key;
        return this.#comesBeforeByKey(a, b);
    }

    #comesBeforeByKey(a: K, b: K): boolean {
        const c = this.#compare(a, b);
        if (c === 0) return false;
        return this.#isMinHeap ? c < 0 : c > 0;
    }
}

export const createMinHeap = <K, V>(compare: Comparator<K>): IndexedHeap<K, V> =>
    new IndexedHeap<K, V>({ compare, order: 'min' });

export const createMaxHeap = <K, V>(compare: Comparator<K>): IndexedHeap<K, V> =>
    new IndexedHeap<K, V>({ compare, order: 'max' });

export const numericCompare = (a: number, b: number): number => a - b;
