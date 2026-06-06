import { LruMap, type LruOrder } from '../../internal/lru-map';
import type { PoolSlot, PoolableObject } from '../pool-support';

export class LruSlotIndex {
    readonly #map: LruMap<number, number>;
    readonly #order: LruOrder;

    constructor(capacity: number, order: LruOrder) {
        this.#map = new LruMap<number, number>({ capacity, order });
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
    }

    upsert(slotId: number, lastAccessed: number): void {
        this.#map.set(slotId, lastAccessed);
    }

    remove(slotId: number): void {
        this.#map.delete(slotId);
    }

    pickAndRemove(): number | null {
        const entry = this.#map.pop();
        return entry?.key ?? null;
    }

    rebuild<T extends PoolableObject>(slots: ReadonlyArray<PoolSlot<T> | undefined>): void {
        this.#map.clear();
        for (let i = 0; i < slots.length; i++) {
            const slot = slots[i];
            if (slot && slot.status === 'free') {
                this.#map.set(i, slot.lastAccessed);
            }
        }
    }
}
