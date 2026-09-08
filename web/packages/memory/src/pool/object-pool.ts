import { MemoryPool, MemoryPoolOptions, PoolableObject } from './mempool';
import { POOL_OPTION_DEFAULTS } from './pool-support';

/**
 * Wrapper interface that adapts a plain object to the PoolableObject lifecycle contract.
 * @stable
 */
export interface PoolableWrapper<T> extends PoolableObject {
    readonly value: T;
    readonly isWrapped: true;
}

/**
 * Configuration options for ObjectPool.
 * @stable
 */
export interface ObjectPoolOptions<T>
    extends Omit<MemoryPoolOptions<PoolableWrapper<T>>, 'factory' | 'asyncFactory'> {
    readonly factory: () => T;
    readonly asyncFactory?: () => Promise<T>;
    readonly resetHandler?: (obj: T) => void;
    readonly validateHandler?: (obj: T) => boolean;
    readonly onAcquireHandler?: (obj: T) => void;
    readonly onReleaseHandler?: (obj: T) => void;
    readonly onEvictHandler?: (obj: T) => void;
}

type ObjectPoolState = 'active' | 'draining' | 'disposed';

const enum ObjectPoolInternalError {
    INVALID_STATE = 'INVALID_STATE',
    WRAPPER_CORRUPTION = 'WRAPPER_CORRUPTION',
    FACTORY_ERROR = 'FACTORY_ERROR',
}

class ObjectPoolError extends Error {
    readonly code: ObjectPoolInternalError;
    readonly timestamp: number;

    constructor(message: string, code: ObjectPoolInternalError) {
        super(message);
        this.name = 'ObjectPoolError';
        this.code = code;
        this.timestamp = performance.now();
        Object.setPrototypeOf(this, ObjectPoolError.prototype);
    }
}

interface MaybeResettable {
    reset?(): void;
}

const createWrapper = <T>(value: T, resetHandler?: (obj: T) => void): PoolableWrapper<T> => {
    const wrapper: PoolableWrapper<T> = {
        value,
        isWrapped: true as const,
        reset(): void {
            if (resetHandler) {
                resetHandler(value);
            } else if (typeof (value as MaybeResettable).reset === 'function') {
                (value as MaybeResettable).reset!();
            }
        },
    };

    return wrapper;
};

/**
 * Object pool that wraps plain objects in a PoolableObject-compatible wrapper.
 *
 * Delegates to an internal {@link MemoryPool} while hiding the wrapper indirection from callers.
 * Acquired objects are the raw values produced by the factory; the pool internally wraps them
 * to track lifecycle metadata. Uses WeakMap/WeakSet to map between raw objects and their wrappers.
 *
 * This is the recommended pool for general-purpose objects that don't already implement
 * {@link PoolableObject}. For typed arrays, use {@link TypedArrayPool} instead.
 *
 * @example
 * const pool = new ObjectPool({
 *   factory: () => ({ x: 0, y: 0, reset() { this.x = 0; this.y = 0; } }),
 *   maxCapacity: 512,
 * });
 * const obj = pool.acquire();  // { x: 0, y: 0 }
 * obj.x = 10;
 * pool.release(obj);           // reset() called automatically
 *
 * @stable
 */
export class ObjectPool<T extends {}> implements Disposable {
    private readonly _pool: MemoryPool<PoolableWrapper<T>>;
    private readonly _options: Required<
        Omit<
            ObjectPoolOptions<T>,
            | 'asyncFactory'
            | 'resetHandler'
            | 'validateHandler'
            | 'onAcquireHandler'
            | 'onReleaseHandler'
            | 'onEvictHandler'
            | 'estimatedObjectSize'
        >
    > &
        Pick<
            ObjectPoolOptions<T>,
            | 'asyncFactory'
            | 'resetHandler'
            | 'validateHandler'
            | 'onAcquireHandler'
            | 'onReleaseHandler'
            | 'onEvictHandler'
            | 'estimatedObjectSize'
        >;
    private _state: ObjectPoolState = 'active';
    private readonly _activeObjects = new WeakSet<T>();
    private readonly _objToWrapper = new WeakMap<T, PoolableWrapper<T>>();

    constructor(options: ObjectPoolOptions<T>) {
        this._options = {
            initialCapacity: options.initialCapacity ?? 16,
            maxCapacity: options.maxCapacity ?? 1024,
            minFree: options.minFree ?? POOL_OPTION_DEFAULTS.minFree,
            highWatermarkRatio: options.highWatermarkRatio ?? 0.85,
            lowWatermarkRatio: options.lowWatermarkRatio ?? 0.15,
            expansionStrategy:
                options.expansionStrategy ?? POOL_OPTION_DEFAULTS.expansionStrategy,
            expansionFactor: options.expansionFactor ?? 1.5,
            expansionRate: options.expansionRate ?? POOL_OPTION_DEFAULTS.expansionRate,
            allocationStrategy:
                options.allocationStrategy ?? POOL_OPTION_DEFAULTS.allocationStrategy,
            evictionPolicy: options.evictionPolicy ?? 'lru',
            ttl: options.ttl ?? POOL_OPTION_DEFAULTS.ttl,
            factory: options.factory,
            resetOnRecycle: options.resetOnRecycle ?? POOL_OPTION_DEFAULTS.resetOnRecycle,
            preallocate: options.preallocate ?? POOL_OPTION_DEFAULTS.preallocate,
            autoExpand: options.autoExpand ?? POOL_OPTION_DEFAULTS.autoExpand,
            compactionThreshold: options.compactionThreshold ?? 64,
            compactionTriggerRatio: options.compactionTriggerRatio ?? 0.3,
            enableMetrics: options.enableMetrics ?? POOL_OPTION_DEFAULTS.enableMetrics,
            enableInstrumentation:
                options.enableInstrumentation ?? POOL_OPTION_DEFAULTS.enableInstrumentation,
            name: options.name ?? `ObjectPool-${Date.now()}`,
            asyncFactory: options.asyncFactory,
            resetHandler: options.resetHandler,
            validateHandler: options.validateHandler,
            onAcquireHandler: options.onAcquireHandler,
            onReleaseHandler: options.onReleaseHandler,
            onEvictHandler: options.onEvictHandler,
            estimatedObjectSize: options.estimatedObjectSize,

            validator: () => true,
            onAcquire: () => {},
            onRelease: () => {},
            onEvict: () => {},
            onOutOfMemory: () => {},
        };

        const poolOptions: MemoryPoolOptions<PoolableWrapper<T>> = {
            ...this._options,
            factory: this._createWrapperFactory(),
            asyncFactory: this._options.asyncFactory
                ? this._createAsyncWrapperFactory()
                : undefined,
            validator: this._createValidator(),
            onAcquire: this._createOnAcquireHandler(),
            onRelease: this._createOnReleaseHandler(),
            onEvict: this._createOnEvictHandler(),
        };

        this._pool = new MemoryPool(poolOptions);
    }

    public acquire(): T {
        this._assertActive();

        const wrapper = this._pool.acquire();
        const obj = wrapper.value;

        this._activeObjects.add(obj);
        this._objToWrapper.set(obj, wrapper);

        return obj;
    }

    public release(obj: T): void {
        if (this._state === 'disposed') {
            return;
        }

        if (!this._activeObjects.has(obj)) {
            throw new ObjectPoolError(
                'Object not acquired from this pool',
                ObjectPoolInternalError.INVALID_STATE
            );
        }

        const wrapper = this._objToWrapper.get(obj);
        if (!wrapper) {
            throw new ObjectPoolError(
                'Wrapper not found for object',
                ObjectPoolInternalError.WRAPPER_CORRUPTION
            );
        }

        this._activeObjects.delete(obj);
        this._objToWrapper.delete(obj);
        this._pool.release(wrapper);
    }

    public tryAcquire(): T | null {
        if (this._state !== 'active') {
            return null;
        }

        const wrapper = this._pool.tryAcquire();
        if (!wrapper) {
            return null;
        }

        const obj = wrapper.value;
        this._activeObjects.add(obj);
        this._objToWrapper.set(obj, wrapper);

        return obj;
    }

    public async acquireAsync(): Promise<T> {
        this._assertActive();

        const wrapper = await this._pool.acquireAsync();
        const obj = wrapper.value;

        this._activeObjects.add(obj);
        this._objToWrapper.set(obj, wrapper);

        return obj;
    }

    public async releaseAsync(obj: T): Promise<void> {
        if (this._state === 'disposed') {
            return;
        }

        if (!this._activeObjects.has(obj)) {
            throw new ObjectPoolError(
                'Object not acquired from this pool',
                ObjectPoolInternalError.INVALID_STATE
            );
        }

        const wrapper = this._objToWrapper.get(obj);
        if (!wrapper) {
            throw new ObjectPoolError(
                'Wrapper not found for object',
                ObjectPoolInternalError.WRAPPER_CORRUPTION
            );
        }

        this._activeObjects.delete(obj);
        this._objToWrapper.delete(obj);
        await this._pool.releaseAsync(wrapper);
    }

    public async tryAcquireAsync(timeoutMs?: number): Promise<T | null> {
        if (this._state !== 'active') {
            return null;
        }

        const wrapper = await this._pool.tryAcquireAsync(timeoutMs);
        if (!wrapper) {
            return null;
        }

        const obj = wrapper.value;
        this._activeObjects.add(obj);
        this._objToWrapper.set(obj, wrapper);

        return obj;
    }

    public releaseAll(): void {
        if (this._state === 'disposed') {
            return;
        }

        this._pool.releaseAll();
    }

    public async releaseAllAsync(): Promise<void> {
        if (this._state === 'disposed') {
            return;
        }

        await this._pool.releaseAllAsync();
    }

    public clear(): void {
        this._assertActive();
        this._pool.clear();
    }

    public async clearAsync(): Promise<void> {
        this._assertActive();
        await this._pool.clearAsync();
    }

    public drain(): void {
        if (this._state === 'disposed') {
            return;
        }

        this._state = 'draining';
        try {
            this._pool.drain();
        } finally {
            this._state = 'active';
        }
    }

    public async drainAsync(): Promise<void> {
        if (this._state === 'disposed') {
            return;
        }

        this._state = 'draining';
        try {
            await this._pool.drainAsync();
        } finally {
            this._state = 'active';
        }
    }

    public resize(newCapacity: number): void {
        this._assertActive();
        this._pool.resize(newCapacity);
    }

    public forceCompact(): void {
        this._assertActive();
        this._pool.forceCompact();
    }

    public isFromPool(obj: T): boolean {
        return this._activeObjects.has(obj);
    }

    public getAvailableCount(): number {
        return this._pool.getAvailableCount();
    }

    public getAllocatedCount(): number {
        return this._pool.getAllocatedCount();
    }

    public getTotalCount(): number {
        return this._pool.getTotalCount();
    }

    public getMetrics() {
        return this._pool.getMetrics();
    }

    public get state(): ObjectPoolState {
        return this._state;
    }

    public get name(): string {
        return this._options.name;
    }

    public [Symbol.dispose](): void {
        if (this._state === 'disposed') {
            return;
        }

        this._state = 'disposed';
        this._pool[Symbol.dispose]();
    }

    private _assertActive(): void {
        if (this._state !== 'active') {
            throw new ObjectPoolError(
                `Pool is ${this._state}`,
                ObjectPoolInternalError.INVALID_STATE
            );
        }
    }

    private _createWrapperFactory(): () => PoolableWrapper<T> {
        return () => {
            try {
                const obj = this._options.factory();
                return createWrapper(obj, this._options.resetHandler);
            } catch (error) {
                throw new ObjectPoolError(
                    `Factory failed: ${error}`,
                    ObjectPoolInternalError.FACTORY_ERROR
                );
            }
        };
    }

    private _createAsyncWrapperFactory(): () => Promise<PoolableWrapper<T>> {
        return async () => {
            try {
                const obj = await this._options.asyncFactory!();
                return createWrapper(obj, this._options.resetHandler);
            } catch (error) {
                throw new ObjectPoolError(
                    `Async factory failed: ${error}`,
                    ObjectPoolInternalError.FACTORY_ERROR
                );
            }
        };
    }

    private _createValidator(): (wrapper: PoolableWrapper<T>) => boolean {
        if (!this._options.validateHandler) {
            return () => true;
        }

        return (wrapper: PoolableWrapper<T>) => {
            try {
                return this._options.validateHandler!(wrapper.value);
            } catch (error) {
                if (this._options.enableInstrumentation) {
                    console.debug(
                        `validateHandler error in pool "${this._options.name}":`,
                        error
                    );
                }
                return false;
            }
        };
    }

    private _createOnAcquireHandler(): (wrapper: PoolableWrapper<T>) => void {
        if (!this._options.onAcquireHandler) {
            return () => {};
        }

        return (wrapper: PoolableWrapper<T>) => {
            try {
                this._options.onAcquireHandler!(wrapper.value);
            } catch (error) {
                if (this._options.enableInstrumentation) {
                    console.debug(`onAcquire handler error in pool "${this._options.name}":`, error);
                }
            }
        };
    }

    private _createOnReleaseHandler(): (wrapper: PoolableWrapper<T>) => void {
        if (!this._options.onReleaseHandler) {
            return () => {};
        }

        return (wrapper: PoolableWrapper<T>) => {
            try {
                this._options.onReleaseHandler!(wrapper.value);
            } catch (error) {
                if (this._options.enableInstrumentation) {
                    console.debug(`onRelease handler error in pool "${this._options.name}":`, error);
                }
            }
        };
    }

    private _createOnEvictHandler(): (wrapper: PoolableWrapper<T>) => void {
        if (!this._options.onEvictHandler) {
            return () => {};
        }

        return (wrapper: PoolableWrapper<T>) => {
            try {
                this._options.onEvictHandler!(wrapper.value);
            } catch (error) {
                if (this._options.enableInstrumentation) {
                    console.debug(`onEvict handler error in pool "${this._options.name}":`, error);
                }
            }
        };
    }
}

export type { PoolableObject, MemoryPoolOptions, PoolPerformanceMetrics } from './mempool';
