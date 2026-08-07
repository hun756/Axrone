import type {
    InternalPoolMetrics,
    PoolSlot,
    PoolableObject,
    ResolvedMemoryPoolOptions,
} from '../pool-support';

export type {
    PoolSlot,
    PoolableObject,
    ResolvedMemoryPoolOptions,
    InternalPoolMetrics,
} from '../pool-support';

export interface WaitQueueEntry<T extends PoolableObject> {
    resolve: (obj: T | null) => void;
    reject: (err: Error) => void;
    timer: ReturnType<typeof setTimeout> | null;
}

export interface PoolContext<T extends PoolableObject> {
    readonly name: string;
    readonly options: ResolvedMemoryPoolOptions<T>;
    readonly slots: PoolSlot<T>[];
    readonly freeList: Set<number>;
    readonly waitQueue: WaitQueueEntry<T>[];
    readonly lruFreeIds: { size: number; clear(): void } | null;
    readonly metrics: InternalPoolMetrics;
    readonly isDisposed: () => boolean;
}
