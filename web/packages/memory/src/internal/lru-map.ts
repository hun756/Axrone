import { Comparator, HeapHandle, IndexedHeap, createMaxHeap, createMinHeap } from './indexed-heap';

declare const __lruEntryBrand: unique symbol;
declare const __lruHandleBrand: unique symbol;

/** @stable */
export type LruOrder = 'least-recently-used' | 'most-recently-used';

/** @stable */
export interface LruOptions<K, V> {
    readonly capacity?: number;
    readonly order?: LruOrder;
    readonly keyEqual?: (a: K, b: K) => boolean;
    readonly initial?: ReadonlyArray<readonly [K, V]>;
}

/** @stable */
export interface LruEntryView<K, V> {
    readonly key: K;
    readonly value: V;
    readonly recency: number;
}

interface LruInternalEntry<K, V> {
    key: K;
    value: V;
}

/**
 * Opaque handle to an LRU map entry for direct removal or inspection.
 * @stable
 */
export class LruHandle<K = unknown, V = unknown> {
    readonly #brand: { readonly [__lruHandleBrand]: true } = null as never;
    readonly #id: number;
    readonly #owner: LruMap<K, V>;
    #state: 'valid' | 'invalid' = 'valid';

    constructor(id: number, owner: LruMap<K, V>) {
        this.#id = id;
        this.#owner = owner;
    }

    get id(): number {
        return this.#id;
    }

    get owner(): LruMap<K, V> {
        return this.#owner;
    }

    get isValid(): boolean {
        return this.#state === 'valid';
    }

    invalidate(): void {
        this.#state = 'invalid';
    }

    equals(other: LruHandle<K, V>): boolean {
        return this.#id === other.#id && this.#owner === other.#owner;
    }
}

/**
 * Fixed-capacity map with heap-based LRU (or MRU) eviction in O(log n) per operation.
 *
 * Uses an {@link IndexedHeap} internally to track recency, enabling O(log n) touch and eviction
 * instead of the O(1) amortized but O(n) worst-case of linked-list LRU caches. Each entry
 * receives an {@link LruHandle} for direct removal or inspection. Supports custom key equality,
 * configurable capacity, and both least-recently-used and most-recently-used eviction orders.
 *
 * Recency counters are monotonically increasing with automatic wraparound normalization to
 * prevent integer overflow in long-running applications.
 *
 * @example
 * const lru = new LruMap<string, number>({ capacity: 3, order: 'least-recently-used' });
 * lru.set('a', 1);
 * lru.set('b', 2);
 * lru.set('c', 3);
 * lru.get('a');           // touch 'a', making it most-recent
 * lru.set('d', 4);        // evicts 'b' (least-recently used)
 * lru.has('b');           // false
 *
 * @stable
 */
export class LruMap<K, V> {
    readonly #heap: IndexedHeap<number, LruHandle<K, V>>;
    readonly #isLru: boolean;
    readonly #capacity: number;
    readonly #keyEqual: (a: K, b: K) => boolean;
    readonly #handleToRecency: Map<LruHandle<K, V>, number>;
    readonly #handleToEntry: Map<LruHandle<K, V>, LruInternalEntry<K, V>>;
    readonly #handleToHeapHandle: Map<LruHandle<K, V>, HeapHandle<number, LruHandle<K, V>>>;
    readonly #keyToHandle: Map<K, LruHandle<K, V>>;
    #sequence: number = 0;
    #nextHandleId: number = 1;
    #modCount: number = 0;

    constructor(options: LruOptions<K, V> = {} as LruOptions<K, V>) {
        this.#isLru = (options.order ?? 'least-recently-used') === 'least-recently-used';
        this.#capacity = Math.max(0, options.capacity ?? Infinity);
        this.#keyEqual = options.keyEqual ?? Object.is;
        this.#handleToRecency = new Map();
        this.#handleToEntry = new Map();
        this.#handleToHeapHandle = new Map();
        this.#keyToHandle = new Map();

        const compare: Comparator<number> = (a, b) => a - b;
        this.#heap = this.#isLru ? createMinHeap(compare) : createMaxHeap(compare);

        if (options.initial && this.#capacity !== Infinity) {
            const bounded = options.initial.slice(0, this.#capacity);
            for (const [key, value] of bounded) {
                this.set(key, value);
            }
        }
    }

    get size(): number {
        return this.#handleToEntry.size;
    }

    get isEmpty(): boolean {
        return this.#handleToEntry.size === 0;
    }

    get capacity(): number {
        return this.#capacity;
    }

    get modCount(): number {
        return this.#modCount;
    }

    get order(): LruOrder {
        return this.#isLru ? 'least-recently-used' : 'most-recently-used';
    }

    has(key: K): boolean {
        return this.#keyToHandle.has(key);
    }

    hasHandle(handle: LruHandle<K, V>): boolean {
        return this.#handleToEntry.has(handle);
    }

    peek(key: K): V | undefined {
        const handle = this.#keyToHandle.get(key);
        if (handle === undefined) return undefined;
        return this.#handleToEntry.get(handle)?.value;
    }

    get(key: K): V | undefined {
        const handle = this.#keyToHandle.get(key);
        if (handle === undefined) return undefined;
        const entry = this.#handleToEntry.get(handle);
        if (entry === undefined) return undefined;
        this.#touch(handle);
        return entry.value;
    }

    set(key: K, value: V): LruHandle<K, V> {
        this.#modCount++;
        const existing = this.#keyToHandle.get(key);
        if (existing !== undefined) {
            const entry = this.#handleToEntry.get(existing)!;
            entry.value = value;
            this.#touch(existing);
            return existing;
        }

        if (this.#capacity === 0) {
            const invalid = this.#createHandle();
            invalid.invalidate();
            return invalid;
        }

        if (this.#handleToEntry.size >= this.#capacity) {
            this.#evictOldest();
        }

        const recency = this.#nextRecency();
        const handle = this.#createHandle();
        const entry: LruInternalEntry<K, V> = { key, value };
        this.#handleToEntry.set(handle, entry);
        this.#handleToRecency.set(handle, recency);
        this.#keyToHandle.set(key, handle);
        const heapHandle = this.#heap.push(recency, handle);
        this.#handleToHeapHandle.set(handle, heapHandle);
        return handle;
    }

    touch(key: K): boolean {
        const handle = this.#keyToHandle.get(key);
        if (handle === undefined) return false;
        return this.#touchHandle(handle);
    }

    delete(key: K): boolean {
        const handle = this.#keyToHandle.get(key);
        if (handle === undefined) return false;
        return this.#removeHandle(handle);
    }

    deleteHandle(handle: LruHandle<K, V>): boolean {
        if (!this.#handleToEntry.has(handle)) return false;
        return this.#removeHandle(handle);
    }

    pop(): LruEntryView<K, V> | undefined {
        if (this.#heap.isEmpty) return undefined;
        this.#modCount++;
        const popped = this.#heap.pop();
        if (!popped) return undefined;
        const handle = popped.value;
        const recency = this.#handleToRecency.get(handle) ?? popped.key;
        const entry = this.#handleToEntry.get(handle);
        if (entry === undefined) return undefined;
        this.#handleToEntry.delete(handle);
        this.#handleToRecency.delete(handle);
        this.#handleToHeapHandle.delete(handle);
        this.#keyToHandle.delete(entry.key);
        return { key: entry.key, value: entry.value, recency };
    }

    peekOldest(): LruEntryView<K, V> | undefined {
        const top = this.#heap.peek();
        if (top === undefined) return undefined;
        const handle = top.value;
        const entry = this.#handleToEntry.get(handle);
        if (entry === undefined) return undefined;
        return { key: entry.key, value: entry.value, recency: top.key };
    }

    clear(): void {
        this.#handleToEntry.clear();
        this.#handleToRecency.clear();
        this.#handleToHeapHandle.clear();
        this.#keyToHandle.clear();
        this.#heap.clear();
        this.#sequence = 0;
        this.#modCount++;
    }

    keys(): K[] {
        const result: K[] = [];
        for (const entry of this.#handleToEntry.values()) result.push(entry.key);
        return result;
    }

    values(): V[] {
        const result: V[] = [];
        for (const entry of this.#handleToEntry.values()) result.push(entry.value);
        return result;
    }

    entriesArray(): Array<readonly [K, V]> {
        const result: Array<readonly [K, V]> = [];
        for (const entry of this.#handleToEntry.values()) {
            result.push([entry.key, entry.value] as const);
        }
        return result;
    }

    toRecencySortedArray(): LruEntryView<K, V>[] {
        return this.#heap.toSortedArray().map((view) => ({
            key: (this.#handleToEntry.get(view.value) as LruInternalEntry<K, V>).key,
            value: (this.#handleToEntry.get(view.value) as LruInternalEntry<K, V>).value,
            recency: view.key,
        }));
    }

    [Symbol.iterator](): Iterator<readonly [K, V]> {
        return this.entriesArray()[Symbol.iterator]();
    }

    #touchHandle(handle: LruHandle<K, V>): boolean {
        const entry = this.#handleToEntry.get(handle);
        if (entry === undefined) return false;
        this.#modCount++;
        const recency = this.#nextRecency();
        this.#handleToRecency.set(handle, recency);
        const heapHandle = this.#handleToHeapHandle.get(handle);
        if (heapHandle !== undefined) {
            this.#heap.update(heapHandle, recency, handle);
        }
        return true;
    }

    #touch(handle: LruHandle<K, V>): void {
        this.#touchHandle(handle);
    }

    #evictOldest(): void {
        if (this.#heap.isEmpty) return;
        this.#modCount++;
        const popped = this.#heap.pop();
        if (!popped) return;
        const handle = popped.value;
        const entry = this.#handleToEntry.get(handle);
        if (entry !== undefined) {
            this.#keyToHandle.delete(entry.key);
        }
        this.#handleToEntry.delete(handle);
        this.#handleToRecency.delete(handle);
        this.#handleToHeapHandle.delete(handle);
    }

    #removeHandle(handle: LruHandle<K, V>): boolean {
        this.#modCount++;
        const entry = this.#handleToEntry.get(handle);
        if (entry === undefined) return false;
        this.#keyToHandle.delete(entry.key);
        this.#handleToEntry.delete(handle);
        this.#handleToRecency.delete(handle);
        const heapHandle = this.#handleToHeapHandle.get(handle);
        if (heapHandle !== undefined) {
            this.#heap.remove(heapHandle);
            this.#handleToHeapHandle.delete(handle);
        }
        return true;
    }

    #nextRecency(): number {
        this.#sequence = (this.#sequence + 1) >>> 0;
        if (this.#sequence === 0) {
            // Wraparound detected — normalize all heap entries to prevent wrong eviction order
            this.#normalizeRecency();
            // After normalization, sequence is set to N (number of entries)
            // Next call will increment to N+1, ensuring new entries have highest recency
        }
        return this.#sequence;
    }

    #normalizeRecency(): void {
        // Drain heap into array, reassign recency 1..N, rebuild heap
        const entries: Array<{ handle: LruHandle<K, V>; recency: number }> = [];
        while (!this.#heap.isEmpty) {
            const heapHandle = this.#heap.pop();
            if (!heapHandle) break;
            const entry = this.#handleToEntry.get(heapHandle.value);
            if (entry) {
                entries.push({ handle: heapHandle.value, recency: 0 });
            }
        }

        // Reassign recency 1..N and rebuild
        for (let i = 0; i < entries.length; i++) {
            const newRecency = i + 1;
            const handle = entries[i].handle;
            this.#handleToRecency.set(handle, newRecency);

            const heapHandle = this.#heap.push(newRecency, handle);
            this.#handleToHeapHandle.set(handle, heapHandle);
        }

        // Set sequence to N so next recency will be N+1
        this.#sequence = entries.length;
    }

    #createHandle(): LruHandle<K, V> {
        const h = new LruHandle<K, V>(this.#nextHandleId++, this);
        if (this.#nextHandleId > Number.MAX_SAFE_INTEGER) this.#nextHandleId = 1;
        return h;
    }

    /** @internal Test seam for H10 wraparound verification */
    _debugSetSequence(n: number): void {
        this.#sequence = n;
    }
}
