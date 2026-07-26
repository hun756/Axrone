import type { PoolSlot, PoolableObject, PoolEvictionPolicy } from '../pool-support';

export interface EvictionContext<T extends PoolableObject> {
    readonly slots: ReadonlyArray<PoolSlot<T>>;
    readonly now: number;
}

export interface IEvictionStrategy {
    readonly policy: PoolEvictionPolicy;
    findEvictableSlot<T extends PoolableObject>(ctx: EvictionContext<T>): number | null;
}

export class NoneEvictionStrategy implements IEvictionStrategy {
    readonly policy = 'none' as const;
    findEvictableSlot<T extends PoolableObject>(_ctx: EvictionContext<T>): number | null {
        return null;
    }
}

export class LruEvictionStrategy implements IEvictionStrategy {
    readonly policy = 'lru' as const;
    findEvictableSlot<T extends PoolableObject>(ctx: EvictionContext<T>): number | null {
        let leastRecentId: number | null = null;
        let leastRecentTime = Number.POSITIVE_INFINITY;

        for (let i = 0; i < ctx.slots.length; i++) {
            const slot = ctx.slots[i];
            if (slot.status === 'allocated' && slot.lastAccessed < leastRecentTime) {
                leastRecentTime = slot.lastAccessed;
                leastRecentId = i;
            }
        }
        return leastRecentId;
    }
}

export class TtlEvictionStrategy implements IEvictionStrategy {
    readonly policy = 'ttl' as const;
    readonly #ttl: number;

    constructor(ttl: number) {
        this.#ttl = ttl;
    }

    findEvictableSlot<T extends PoolableObject>(ctx: EvictionContext<T>): number | null {
        if (this.#ttl <= 0) return null;
        for (let i = 0; i < ctx.slots.length; i++) {
            const slot = ctx.slots[i];
            if (slot.status === 'allocated' && ctx.now - slot.lastAccessed > this.#ttl) {
                return i;
            }
        }
        return null;
    }
}

export class FifoEvictionStrategy implements IEvictionStrategy {
    readonly policy = 'fifo' as const;
    findEvictableSlot<T extends PoolableObject>(ctx: EvictionContext<T>): number | null {
        let oldestId: number | null = null;
        let oldestTime = Number.POSITIVE_INFINITY;

        for (let i = 0; i < ctx.slots.length; i++) {
            const slot = ctx.slots[i];
            if (slot.status === 'allocated' && slot.createdAt < oldestTime) {
                oldestTime = slot.createdAt;
                oldestId = i;
            }
        }
        return oldestId;
    }
}

export function createEvictionStrategy(
    policy: PoolEvictionPolicy,
    ttl: number
): IEvictionStrategy {
    switch (policy) {
        case 'lru':
            return new LruEvictionStrategy();
        case 'ttl':
            return new TtlEvictionStrategy(ttl);
        case 'fifo':
            return new FifoEvictionStrategy();
        case 'none':
        default:
            return new NoneEvictionStrategy();
    }
}
