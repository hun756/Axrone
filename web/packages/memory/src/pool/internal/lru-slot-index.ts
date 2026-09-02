import type { LruOrder } from '../../internal/lru-map';
import type { PoolSlot, PoolableObject } from '../pool-support';

/**
 * Monotonic-counter LRU/MRU index for free-slot tracking.
 *
 * Performance: upsert is O(1) — a single counter increment + Map.set —
 * compared to the previous LruMap (IndexedHeap + 4 Maps) which was O(log n).
 * pickAndRemove is O(n) over the number of tracked free slots, which is
 * acceptable because eviction is infrequent relative to allocation churn.
 * rebuild is O(n) over total pool slots.
 */
export class LruSlotIndex {
    readonly #order: LruOrder;
    #counter: number = 0;
    #map: Map<number, number> = new Map();

    constructor(_capacity: number, order: LruOrder) {
        this.#order = order;
    }

    get size(): number {
        return this.#map.size;
    }

    get order(): LruOrder {
        return this.#order;
    }

    clear(): void {
        this.#map.clear();
        this.#counter = 0;
    }

    upsert(slotId: number, _lastAccessed?: number): void {
        this.#map.set(slotId, ++this.#counter);
    }

    remove(slotId: number): void {
        this.#map.delete(slotId);
    }

    pickAndRemove(): number | null {
        if (this.#map.size === 0) return null;

        const isLRU = this.#order === 'least-recently-used';
        let targetKey = -1;
        let targetRecency = isLRU ? Infinity : -Infinity;

        for (const [key, recency] of this.#map) {
            if (isLRU ? recency < targetRecency : recency > targetRecency) {
                targetRecency = recency;
                targetKey = key;
            }
        }

        this.#map.delete(targetKey);
        return targetKey;
    }

    rebuild<T extends PoolableObject>(slots: ReadonlyArray<PoolSlot<T> | undefined>): void {
        this.#map.clear();
        this.#counter = 0;
        for (let i = 0; i < slots.length; i++) {
            const slot = slots[i];
            if (slot && slot.status === 'free') {
                this.#map.set(i, ++this.#counter);
            }
        }
    }
}
