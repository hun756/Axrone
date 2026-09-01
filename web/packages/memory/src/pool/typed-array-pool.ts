import type { TypedArray, NumericTypedArray } from '@axrone/utility';
import { MemoryPool, MemoryPoolOptions, PoolableObject } from './mempool';
import { POOL_OPTION_DEFAULTS } from './pool-support';
import { CircularBuffer } from './internal/circular-buffer';

export type { TypedArray, TypedArray as TypedArrayType };

/**
 * Constructor signature for typed array types.
 * @stable
 */
export type TypedArrayConstructor<T extends TypedArray> = {
    new (length: number): T;
    new (buffer: ArrayBuffer, byteOffset?: number, length?: number): T;
    BYTES_PER_ELEMENT: number;
};

/**
 * A typed array wrapper with pool lifecycle metadata.
 * @stable
 */
export interface PoolableTypedArray<T extends TypedArray> extends PoolableObject {
    readonly array: T;
    readonly byteLength: number;
    readonly length: number;
    readonly bytesPerElement: number;
    readonly isAligned: boolean;
    readonly alignment: number;
    readonly buffer: ArrayBuffer;

    zero(): void;

    fill(value: number, start?: number, end?: number): this;

    resize(newLength: number): boolean;

    copyFrom(
        source: ArrayLike<number>,
        sourceOffset?: number,
        targetOffset?: number,
        length?: number
    ): void;

    subarray(start?: number, end?: number): T;
}

/**
 * Configuration options for TypedArrayPool.
 * @stable
 */
export interface TypedArrayPoolOptions<T extends TypedArray>
    extends Omit<MemoryPoolOptions<PoolableTypedArray<T>>, 'factory'> {
    readonly arrayConstructor: TypedArrayConstructor<T>;

    readonly defaultLength?: number;

    readonly alignment?: number;

    readonly sizeBuckets?: readonly number[];

    readonly maxPoolableLength?: number;

    readonly zeroOnRelease?: boolean;

    readonly validateIntegrity?: boolean;

    readonly initializeArray?: (array: T) => void;

    readonly growthStrategy?: 'exact' | 'bucket' | 'exponential';

    readonly growthFactor?: number;
}

/**
 * Statistics for a TypedArrayPool instance.
 * @stable
 */
export interface TypedArrayPoolStats {
    readonly poolMetrics: ReturnType<MemoryPool<any>['getMetrics']>;
    readonly arrayStats: {
        readonly totalArrays: number;
        readonly totalMemory: number;
        readonly averageLength: number;
        readonly lengthDistribution: Map<number, number>;
        readonly wasteRatio: number;
    };
    readonly performanceStats: {
        readonly allocationTime: {
            readonly min: number;
            readonly max: number;
            readonly avg: number;
            readonly samples: number;
        };
        readonly zeroingTime: {
            readonly min: number;
            readonly max: number;
            readonly avg: number;
            readonly samples: number;
        };
        readonly copyTime: {
            readonly min: number;
            readonly max: number;
            readonly avg: number;
            readonly samples: number;
        };
    };
}

/**
 * Pool for typed arrays, organized into size buckets for efficient reuse.
 *
 * Maintains a separate {@link MemoryPool} per bucket size (default: powers of two from 64 to
 * maxPoolableLength). Acquired arrays can be resized down (but not up) within their bucket
 * capacity. Supports optional memory alignment (e.g., 16-byte for SIMD), zero-on-release
 * for safety, and integrity validation.
 *
 * Requests larger than maxPoolableLength bypass pooling and allocate directly. The
 * {@link TypedArrayPools} export provides pre-configured instances for common typed array types.
 *
 * @example
 * const pool = new TypedArrayPool({
 *   arrayConstructor: Float32Array,
 *   defaultLength: 1024,
 *   sizeBuckets: [64, 256, 1024, 4096],
 *   alignment: 16,
 * });
 * const arr = pool.acquire(512);   // from 1024-bucket, resized to 512
 * arr.array[0] = 3.14;
 * pool.release(arr);               // zeroed and returned to bucket
 *
 * @stable
 */
export class TypedArrayPool<T extends TypedArray> {
    private readonly _pools: Map<number, MemoryPool<PoolableTypedArray<T>>>;
    private readonly _poolByObject = new WeakMap<
        PoolableTypedArray<T>,
        MemoryPool<PoolableTypedArray<T>>
    >();
    private readonly _options: Partial<TypedArrayPoolOptions<T>> & {
        arrayConstructor: TypedArrayConstructor<T>;
        defaultLength: number;
        alignment: number;
        maxPoolableLength: number;
        zeroOnRelease: boolean;
        growthStrategy: 'exact' | 'bucket' | 'exponential';
        growthFactor: number;
    };
    private readonly _buckets: readonly number[];
    private _performanceTracker: {
        allocationTimes: CircularBuffer<number>;
        zeroingTimes: CircularBuffer<number>;
        copyTimes: CircularBuffer<number>;
    };
    private readonly _stats = {
        totalAllocations: 0,
        totalReleases: 0,
        totalMemoryAllocated: 0,
        bucketsHit: new Map<number, number>(),
        oversizedRequests: 0,
    };

    constructor(options: TypedArrayPoolOptions<T>) {
        this._options = {
            arrayConstructor: options.arrayConstructor,
            defaultLength: options.defaultLength ?? 1024,
            alignment: options.alignment ?? 0,
            maxPoolableLength: options.maxPoolableLength ?? 1024 * 1024,
            zeroOnRelease: options.zeroOnRelease ?? true,
            growthStrategy: options.growthStrategy ?? 'bucket',
            growthFactor: options.growthFactor ?? 1.5,
            initialCapacity: options.initialCapacity ?? 16,
            maxCapacity: options.maxCapacity ?? 256,
            minFree: options.minFree ?? 4,
            highWatermarkRatio: options.highWatermarkRatio ?? 0.85,
            lowWatermarkRatio: options.lowWatermarkRatio ?? 0.25,
            expansionStrategy:
                options.expansionStrategy ?? POOL_OPTION_DEFAULTS.expansionStrategy,
            expansionFactor: options.expansionFactor ?? 1.5,
            expansionRate: options.expansionRate ?? POOL_OPTION_DEFAULTS.expansionRate,
            allocationStrategy: options.allocationStrategy ?? 'least-recently-used',
            evictionPolicy: options.evictionPolicy ?? 'lru',
            ttl: options.ttl ?? POOL_OPTION_DEFAULTS.ttl,
            resetOnRecycle: options.resetOnRecycle ?? POOL_OPTION_DEFAULTS.resetOnRecycle,
            preallocate: options.preallocate ?? true,
            autoExpand: options.autoExpand ?? POOL_OPTION_DEFAULTS.autoExpand,
            compactionThreshold: options.compactionThreshold ?? 32,
            compactionTriggerRatio: options.compactionTriggerRatio ?? 0.3,
            enableMetrics: options.enableMetrics ?? POOL_OPTION_DEFAULTS.enableMetrics,
            enableInstrumentation:
                options.enableInstrumentation ?? POOL_OPTION_DEFAULTS.enableInstrumentation,
            name: options.name ?? `TypedArrayPool<${options.arrayConstructor.name}>`,
            maxObjectAge: options.maxObjectAge ?? 300000,
            threadSafe: options.threadSafe ?? POOL_OPTION_DEFAULTS.threadSafe,
            sizeBuckets: options.sizeBuckets,
            initializeArray: options.initializeArray,
            validateIntegrity: options.validateIntegrity,
        };

        this._buckets = this._options.sizeBuckets ?? this._generateDefaultBuckets();

        this._pools = new Map();
        this._performanceTracker = {
            allocationTimes: new CircularBuffer<number>(1000),
            zeroingTimes: new CircularBuffer<number>(1000),
            copyTimes: new CircularBuffer<number>(1000),
        };

        this._initializePools();
    }

    acquire(length: number = this._options.defaultLength): PoolableTypedArray<T> {
        const startTime = performance.now();

        const bucketSize = this._findBestBucket(length);

        this._stats.totalAllocations++;
        this._stats.bucketsHit.set(bucketSize, (this._stats.bucketsHit.get(bucketSize) ?? 0) + 1);

        if (length > this._options.maxPoolableLength) {
            this._stats.oversizedRequests++;
            return this._createOversizedArray(length);
        }

        let pool = this._pools.get(bucketSize);
        if (!pool) {
            pool = this._createBucketPool(bucketSize);
        }

        const pooled = pool.acquire();
        this._poolByObject.set(pooled, pool);

        if (pooled.length !== length && !pooled.resize(length)) {
            throw new Error(
                `Requested typed array length ${length} exceeds pooled capacity ${pooled.length}`
            );
        }

        const allocationTime = performance.now() - startTime;
        this._trackPerformance('allocation', allocationTime);

        return pooled;
    }

    release(pooledArray: PoolableTypedArray<T>): void {
        const startTime = performance.now();

        if (this._options.validateIntegrity && !this._validateArray(pooledArray)) {
            console.warn('TypedArrayPool: Invalid array detected during release');
            return;
        }

        if (this._options.zeroOnRelease) {
            const zeroStart = performance.now();
            pooledArray.zero();
            this._trackPerformance('zeroing', performance.now() - zeroStart);
        }

        this._stats.totalReleases++;

        const ownerPool = this._poolByObject.get(pooledArray);
        if (ownerPool && pooledArray.length <= this._options.maxPoolableLength) {
            ownerPool.release(pooledArray);
        }

        const releaseTime = performance.now() - startTime;
        this._trackPerformance('allocation', releaseTime); // Track as negative allocation time
    }

    acquireWithData(source: ArrayLike<number>): PoolableTypedArray<T> {
        const startTime = performance.now();

        const pooled = this.acquire(source.length);
        pooled.copyFrom(source);

        this._trackPerformance('copy', performance.now() - startTime);
        return pooled;
    }

    getStats(): TypedArrayPoolStats {
        const poolMetrics = Array.from(this._pools.values()).map((pool) => pool.getMetrics());
        const totalArrays = poolMetrics.reduce((sum, m) => sum + m.allocated, 0);
        const totalMemory = poolMetrics.reduce((sum, m) => sum + m.allocated * m.capacity, 0);

        return {
            poolMetrics: poolMetrics[0], // Primary pool metrics
            arrayStats: {
                totalArrays,
                totalMemory: this._stats.totalMemoryAllocated,
                averageLength: totalArrays > 0 ? totalMemory / totalArrays : 0,
                lengthDistribution: new Map(this._stats.bucketsHit),
                wasteRatio: this._calculateWasteRatio(),
            },
            performanceStats: {
                allocationTime: this._getPerformanceStats('allocation'),
                zeroingTime: this._getPerformanceStats('zeroing'),
                copyTime: this._getPerformanceStats('copy'),
            },
        };
    }

    clear(): void {
        for (const pool of this._pools.values()) {
            pool.clear();
        }

        this._stats.totalAllocations = 0;
        this._stats.totalReleases = 0;
        this._stats.totalMemoryAllocated = 0;
        this._stats.bucketsHit.clear();
        this._stats.oversizedRequests = 0;

        this._performanceTracker.allocationTimes = new CircularBuffer<number>(1000);
        this._performanceTracker.zeroingTimes = new CircularBuffer<number>(1000);
        this._performanceTracker.copyTimes = new CircularBuffer<number>(1000);
    }

    dispose(): void {
        for (const pool of this._pools.values()) {
            pool.clear();
        }
        this._pools.clear();
        this.clear();
    }

    private _initializePools(): void {
        for (const bucketSize of this._buckets) {
            this._createBucketPool(bucketSize);
        }
    }

    private _createBucketPool(bucketSize: number): MemoryPool<PoolableTypedArray<T>> {
        const poolOptions: MemoryPoolOptions<PoolableTypedArray<T>> = {
            ...this._options,
            factory: () => this._createPoolableArray(bucketSize),
            name: `${this._options.name}[${bucketSize}]`,
        };

        const pool = new MemoryPool(poolOptions);
        this._pools.set(bucketSize, pool);
        return pool;
    }

    private _createPoolableArray(length: number): PoolableTypedArray<T> {
        const buffer =
            this._options.alignment > 0
                ? this._createAlignedBuffer(length)
                : new ArrayBuffer(length * this._options.arrayConstructor.BYTES_PER_ELEMENT);

        const array = new this._options.arrayConstructor(buffer) as T;

        if (this._options.initializeArray) {
            this._options.initializeArray(array);
        }

        this._stats.totalMemoryAllocated += buffer.byteLength;

        const poolable: PoolableTypedArray<T> = {
            array,
            byteLength: buffer.byteLength,
            length: array.length,
            bytesPerElement: this._options.arrayConstructor.BYTES_PER_ELEMENT,
            isAligned: this._options.alignment > 0,
            alignment: this._options.alignment,
            buffer,

            zero(): void {
                (array as { fill(value: number): unknown }).fill(0);
            },

            fill(value: number, start?: number, end?: number): PoolableTypedArray<T> {
                (array as { fill(value: number, start?: number, end?: number): unknown }).fill(
                    value,
                    start,
                    end
                );
                return this;
            },

            resize(newLength: number): boolean {
                if (newLength === array.length) return true;
                if (newLength > array.length) return false; // Can't expand existing buffer

                const mutableThis = this as {
                    array: T;
                    length: number;
                };
                mutableThis.array = new (array.constructor as new (
                    buffer: ArrayBuffer,
                    byteOffset?: number,
                    length?: number
                ) => T)(buffer, 0, newLength);
                mutableThis.length = newLength;
                return true;
            },

            copyFrom(
                source: ArrayLike<number>,
                sourceOffset: number = 0,
                targetOffset: number = 0,
                length?: number
            ): void {
                const copyLength =
                    length ?? Math.min(source.length - sourceOffset, array.length - targetOffset);

                if (source instanceof array.constructor) {
                    (array as NumericTypedArray).set(
                        (source as NumericTypedArray).subarray(sourceOffset, sourceOffset + copyLength),
                        targetOffset
                    );
                } else {
                    for (let i = 0; i < copyLength; i++) {
                        array[targetOffset + i] = source[sourceOffset + i];
                    }
                }
            },

            subarray(start?: number, end?: number): T {
                return array.subarray(start, end) as T;
            },

            reset(): void {},
        };

        return poolable;
    }

    private _createOversizedArray(length: number): PoolableTypedArray<T> {
        return this._createPoolableArray(length);
    }

    private _createAlignedBuffer(length: number): ArrayBuffer {
        const requiredBytes = length * this._options.arrayConstructor.BYTES_PER_ELEMENT;
        const alignment = this._options.alignment;
        const alignedBytes = alignment > 1
            ? Math.ceil(requiredBytes / alignment) * alignment
            : requiredBytes;

        return new ArrayBuffer(alignedBytes);
    }

    private _generateDefaultBuckets(): number[] {
        const buckets: number[] = [];
        const max = this._options.maxPoolableLength;

        for (let size = 64; size <= max; size *= 2) {
            buckets.push(size);
        }

        return buckets;
    }

    private _findBestBucket(length: number): number {
        for (const bucket of this._buckets) {
            if (bucket >= length) {
                return bucket;
            }
        }

        switch (this._options.growthStrategy) {
            case 'exact':
                return length;
            case 'exponential':
                return Math.ceil(length * this._options.growthFactor);
            case 'bucket':
            default:
                return this._buckets[this._buckets.length - 1];
        }
    }

    private _validateArray(pooledArray: PoolableTypedArray<T>): boolean {
        return (
            pooledArray.array instanceof this._options.arrayConstructor &&
            pooledArray.array.buffer === pooledArray.buffer &&
            pooledArray.array.byteLength <= pooledArray.buffer.byteLength
        );
    }

    private _trackPerformance(type: 'allocation' | 'zeroing' | 'copy', time: number): void {
        const buffer = this._performanceTracker[`${type}Times`];
        buffer.push(time);
    }

    private _getPerformanceStats(type: 'allocation' | 'zeroing' | 'copy') {
        const buffer = this._performanceTracker[`${type}Times`];

        if (buffer.size === 0) {
            return { min: 0, max: 0, avg: 0, samples: 0 };
        }

        const times = buffer.toArray();
        let min = Infinity;
        let max = -Infinity;
        for (const t of times) {
            if (t < min) min = t;
            if (t > max) max = t;
        }
        const avg = buffer.reduce((a, b) => a + b, 0) / buffer.size;

        return { min, max, avg, samples: buffer.size };
    }

    private _calculateWasteRatio(): number {
        let totalWaste = 0;
        let totalUsed = 0;

        for (const [bucketSize, hitCount] of this._stats.bucketsHit) {
            const avgUtilization = bucketSize * 0.75; // Assume 75% average utilization
            totalWaste += (bucketSize - avgUtilization) * hitCount;
            totalUsed += avgUtilization * hitCount;
        }

        return totalUsed > 0 ? totalWaste / (totalWaste + totalUsed) : 0;
    }
}

/**
 * Pre-configured TypedArrayPool instances for common typed array types.
 * @stable
 */
export const TypedArrayPools = {
    Float32: new TypedArrayPool({
        arrayConstructor: Float32Array,
        defaultLength: 1024,
        sizeBuckets: [64, 256, 1024, 4096, 16384, 65536],
        name: 'Float32Pool',
        alignment: 16, // SIMD alignment
        enableMetrics: true,
        preallocate: false,
    }),

    Float64: new TypedArrayPool({
        arrayConstructor: Float64Array,
        defaultLength: 512,
        sizeBuckets: [32, 128, 512, 2048, 8192, 32768],
        name: 'Float64Pool',
        alignment: 16,
        enableMetrics: true,
        preallocate: false,
    }),

    Uint32: new TypedArrayPool({
        arrayConstructor: Uint32Array,
        defaultLength: 1024,
        sizeBuckets: [64, 256, 1024, 4096, 16384, 65536],
        name: 'Uint32Pool',
        alignment: 16,
        enableMetrics: true,
        preallocate: false,
    }),

    Uint16: new TypedArrayPool({
        arrayConstructor: Uint16Array,
        defaultLength: 2048,
        sizeBuckets: [128, 512, 2048, 8192, 32768, 131072],
        name: 'Uint16Pool',
        enableMetrics: true,
        preallocate: false,
    }),

    Uint8: new TypedArrayPool({
        arrayConstructor: Uint8Array,
        defaultLength: 4096,
        sizeBuckets: [256, 1024, 4096, 16384, 65536, 262144],
        name: 'Uint8Pool',
        enableMetrics: true,
        preallocate: false,
    }),
} as const;
