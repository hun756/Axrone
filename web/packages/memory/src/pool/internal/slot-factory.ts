import type { PoolSlot, PoolableObject } from '../pool-support';
import type { PoolMetricsCollector } from './pool-metrics';

export class SlotFactory<T extends PoolableObject> {
    readonly #factory: () => T;
    readonly #metrics: PoolMetricsCollector;

    constructor(factory: () => T, metrics: PoolMetricsCollector) {
        this.#factory = factory;
        this.#metrics = metrics;
    }

    create(id: number, withObject: boolean): PoolSlot<T> {
        const now = Date.now();
        let obj: T | undefined = undefined;

        if (withObject) {
            const timer = this.#metrics.newTimer();
            obj = this.#factory();
            if (timer) this.#metrics.recordCreation(timer.stop());
            obj.__poolId = id;
            obj.__poolStatus = 'free';
            obj.__lastAccessed = now;
            obj.__allocCount = 0;
        }

        return {
            obj,
            status: 'free',
            lastAccessed: now,
            allocCount: 0,
            createdAt: now,
        };
    }

    preallocate(slots: PoolSlot<T>[], freeList: Set<number>, capacity: number): number {
        for (let i = 0; i < capacity; i++) {
            slots[i] = this.create(i, true);
            freeList.add(i);
        }
        return capacity;
    }

    reserve(slots: PoolSlot<T>[], freeList: Set<number>, capacity: number): number {
        const start = slots.length;
        for (let i = start; i < capacity; i++) {
            slots[i] = this.create(i, false);
            freeList.add(i);
        }
        return capacity;
    }
}
