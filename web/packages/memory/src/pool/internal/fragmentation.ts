import type { PoolSlot, PoolableObject } from '../pool-support';

export interface MemoryUsageParams {
    readonly slotCount: number;
    readonly freeListSize: number;
    readonly lruIndexSize: number;
    readonly waitQueueLength: number;
    readonly estimatedObjectSize: number | undefined;
    readonly slotHasObjectCount: number;
}

const BASE_OVERHEAD_BYTES = 1024;
const POINTER_BYTES = 8;
const LRU_ENTRY_BYTES = 16;

export class FragmentationAnalyzer {
    static calculate<T extends PoolableObject>(
        slots: ReadonlyArray<PoolSlot<T> | undefined>
    ): number {
        if (slots.length === 0) return 0;

        let holes = 0;
        let lastWasAllocated = false;

        for (let i = 0; i < slots.length; i++) {
            const slot = slots[i];
            if (!slot) {
                holes++;
                lastWasAllocated = false;
            } else if (slot.status === 'free') {
                if (lastWasAllocated) holes++;
                lastWasAllocated = false;
            } else {
                lastWasAllocated = true;
            }
        }
        return holes / slots.length;
    }

    static estimateMemoryUsageBytes(params: MemoryUsageParams): number {
        const base =
            params.slotCount * POINTER_BYTES +
            params.freeListSize * POINTER_BYTES +
            (params.lruIndexSize > 0 ? params.lruIndexSize * LRU_ENTRY_BYTES : 0) +
            params.waitQueueLength * POINTER_BYTES +
            BASE_OVERHEAD_BYTES;

        const perObject =
            typeof params.estimatedObjectSize === 'number' && params.estimatedObjectSize > 0
                ? params.estimatedObjectSize
                : 0;

        return base + perObject * params.slotHasObjectCount;
    }
}
