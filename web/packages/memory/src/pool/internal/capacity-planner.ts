import {
    MemoryPoolError,
    MemoryPoolErrorCode,
    type PoolExpansionStrategy,
    type PoolSlot,
    type PoolableObject,
} from '../pool-support';
import { nextPrime } from './prime-calculator';
import type { LruSlotIndex } from './lru-slot-index';

export interface ContractionDecision {
    readonly shouldResize: boolean;
    readonly shouldCompact: boolean;
    readonly targetCapacity: number;
}

export interface ContractionOptions {
    readonly initialCapacity: number;
    readonly compactionThreshold: number;
    readonly lowWatermarkRatio: number;
    readonly minFree: number;
    readonly compactionTriggerRatio: number;
}

export type SlotCreator<T extends PoolableObject> = (
    id: number,
    withObject: boolean
) => PoolSlot<T>;

export class CapacityPlanner {
    readonly #poolName: string;
    readonly #lruIndex: LruSlotIndex | null;

    constructor(poolName: string, lruIndex: LruSlotIndex | null) {
        this.#poolName = poolName;
        this.#lruIndex = lruIndex;
    }

    computeExpandedCapacity(
        current: number,
        strategy: PoolExpansionStrategy,
        expansionFactor: number,
        expansionRate: number
    ): number {
        switch (strategy) {
            case 'fixed':
                return current + (expansionRate || 32);
            case 'multiplicative':
                return Math.ceil(current * expansionFactor);
            case 'fibonacci':
                return this.#nextFibonacciAbove(current);
            case 'prime':
                return nextPrime(current * expansionFactor);
            default:
                return Math.ceil(current * expansionFactor);
        }
    }

    grow<T extends PoolableObject>(
        slots: PoolSlot<T>[],
        freeList: Set<number>,
        newCapacity: number,
        preallocate: boolean,
        createSlot: SlotCreator<T>
    ): void {
        const current = slots.length;
        for (let i = current; i < newCapacity; i++) {
            slots[i] = createSlot(i, preallocate);
            freeList.add(i);
        }
        if (this.#lruIndex) this.#lruIndex.rebuild(slots);
    }

    shrink<T extends PoolableObject>(
        slots: PoolSlot<T>[],
        freeList: Set<number>,
        newCapacity: number
    ): void {
        const current = slots.length;
        if (newCapacity >= current) return;

        const allocatedCount = current - freeList.size;
        if (newCapacity < allocatedCount) {
            throw new MemoryPoolError(
                'Cannot shrink pool below allocated count',
                MemoryPoolErrorCode.IN_USE_DURING_OPERATION,
                this.#poolName,
                { allocatedCount, requestedCapacity: newCapacity }
            );
        }

        const freeIds = Array.from(freeList);
        freeIds.sort((a, b) => slots[a]!.lastAccessed - slots[b]!.lastAccessed);

        const removeCount = current - newCapacity;
        for (let i = 0; i < Math.min(removeCount, freeIds.length); i++) {
            const id = freeIds[i];
            slots[id] = undefined as unknown as PoolSlot<T>;
            freeList.delete(id);
        }

        this.compact(slots, freeList);
    }

    compact<T extends PoolableObject>(
        slots: PoolSlot<T>[],
        freeList: Set<number>
    ): void {
        const compacted: PoolSlot<T>[] = [];
        for (let i = 0; i < slots.length; i++) {
            const slot = slots[i];
            if (slot !== undefined) compacted.push(slot);
        }

        for (let i = 0; i < compacted.length; i++) {
            const slot = compacted[i];
            if (slot.obj) slot.obj.__poolId = i;
            if (slot.status === 'free') freeList.add(i);
        }

        slots.length = 0;
        for (const s of compacted) slots.push(s);

        if (this.#lruIndex) this.#lruIndex.rebuild(slots);
    }

    shouldContract<T extends PoolableObject>(
        slots: PoolSlot<T>[],
        freeList: Set<number>,
        options: ContractionOptions,
        fragmentationRatio: number
    ): ContractionDecision {
        const totalCapacity = slots.length;
        if (totalCapacity <= options.initialCapacity || totalCapacity <= options.compactionThreshold) {
            return { shouldResize: false, shouldCompact: false, targetCapacity: totalCapacity };
        }

        const allocatedCount = totalCapacity - freeList.size;
        const utilization = allocatedCount / totalCapacity;

        let shouldResize = false;
        let targetCapacity = totalCapacity;

        if (utilization < options.lowWatermarkRatio) {
            targetCapacity = Math.max(
                options.initialCapacity,
                allocatedCount + options.minFree,
                Math.ceil(allocatedCount / options.lowWatermarkRatio)
            );
            if (targetCapacity <= totalCapacity * 0.75) {
                shouldResize = true;
            }
        }

        const shouldCompact =
            fragmentationRatio > options.compactionTriggerRatio &&
            totalCapacity > options.compactionThreshold;

        return { shouldResize, shouldCompact, targetCapacity };
    }

    #nextFibonacciAbove(current: number): number {
        let a = 1;
        let b = 1;
        while (b <= current) {
            const next = a + b;
            a = b;
            b = next;
        }
        return b;
    }
}
