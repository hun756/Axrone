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

    /**
     * Calculates the pool's new capacity after an expansion event.
     *
     * The chosen {@link PoolExpansionStrategy} determines the growth curve:
     *
     * - **fixed**: linear growth — adds `expansionRate` slots (default 32).
     *   Use when the workload has a predictable, bounded object count and you
     *   want to avoid over-allocating.
     *
     * - **multiplicative**: exponential growth — multiplies by `expansionFactor`
     *   (default 2). Best general-purpose choice; gives O(log N) total
     *   expansions and good amortised cost.
     *
     * - **fibonacci**: golden-ratio growth (~1.618×). Gentler than doubling,
     *   so the pool overshoots less. Useful for generative workloads (particle
     *   systems, spatial subdivision) where Fibonacci-sized batches feel
     *   natural.
     *
     * - **prime**: next prime above `current * expansionFactor`. Ensures
     *   hash-table-friendly sizes when slot indices are used as modular hash
     *   keys, and produces irregular step sizes that avoid resonance with
     *   periodic allocation patterns.
     *
     * @param current - Current pool capacity.
     * @param strategy - Expansion strategy to apply.
     * @param expansionFactor - Multiplier for multiplicative and prime strategies.
     * @param expansionRate - Fixed increment for the 'fixed' strategy.
     * @returns The new capacity (always > current).
     */
    computeExpandedCapacity(
        current: number,
        strategy: PoolExpansionStrategy,
        expansionFactor: number,
        expansionRate: number
    ): number {
        switch (strategy) {
            case 'fixed':
                // Linear growth: add a constant number of slots each expansion.
                return current + (expansionRate || 32);
            case 'multiplicative':
                // Exponential growth: multiply by expansionFactor (typically 2×).
                return Math.ceil(current * expansionFactor);
            case 'fibonacci':
                // Golden-ratio growth (~1.618×). Jumps to the next Fibonacci
                // number, producing sizes like 34, 55, 89, 144, 233, 377…
                return this.#nextFibonacciAbove(current);
            case 'prime':
                // Hash-table-friendly growth: scale by expansionFactor then
                // round up to the next prime. Prime capacities ensure uniform
                // distribution under modular hashing.
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

        freeList.clear();

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

    /**
     * Returns the smallest Fibonacci number strictly greater than `current`.
     *
     * Fibonacci growth uses the golden ratio (phi ≈ 1.618) as its expansion
     * factor, producing the sequence 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144…
     * This sits between fixed (linear) and multiplicative (2×) growth:
     *
     * - **Less overshoot** than doubling — the pool never more than ~62%
     *   exceeds the current capacity, reducing wasted memory.
     * - **Fewer expansions** than fixed growth — the super-linear curve still
     *   keeps expansion frequency logarithmic.
     * - **Natural sizing** — Fibonacci numbers appear in particle counts,
     *   spatial subdivision depths, and other generative workloads common in
     *   game engines.
     *
     * Uses an iterative loop rather than Binet's formula to avoid floating-
     * point precision issues at large capacities.
     */
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
