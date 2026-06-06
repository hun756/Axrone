import type { PoolSlot, PoolableObject, PoolAllocationStrategy } from '../pool-support';
import type { LruSlotIndex } from './lru-slot-index';

export class AllocationSelector {
    #roundRobinIndex = 0;

    readonly #slots: PoolSlot<PoolableObject>[];
    readonly #freeList: Set<number>;
    readonly #lruIndex: LruSlotIndex | null;

    constructor(
        slots: PoolSlot<PoolableObject>[],
        freeList: Set<number>,
        lruIndex: LruSlotIndex | null
    ) {
        this.#slots = slots;
        this.#freeList = freeList;
        this.#lruIndex = lruIndex;
    }

    pickNextFreeId(strategy: PoolAllocationStrategy): number {
        if (this.#freeList.size === 0) {
            return -1;
        }

        switch (strategy) {
            case 'least-recently-used':
            case 'most-recently-used': {
                if (this.#lruIndex) {
                    const id = this.#lruIndex.pickAndRemove();
                    if (id !== null) return id;
                }
                return this.#firstAvailable();
            }
            case 'round-robin': {
                const freeIds = Array.from(this.#freeList);
                const id = freeIds[this.#roundRobinIndex % freeIds.length];
                this.#roundRobinIndex = (this.#roundRobinIndex + 1) % freeIds.length;
                return id;
            }
            case 'first-available':
            default:
                return this.#firstAvailable();
        }
    }

    #firstAvailable(): number {
        const iter = this.#freeList.values().next();
        return iter.done ? -1 : iter.value;
    }

    reset(): void {
        this.#roundRobinIndex = 0;
    }
}
