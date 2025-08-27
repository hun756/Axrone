import type { IMemoryManager } from './interfaces';
import { ParticleSystemException } from './error';

export class AlignedMemoryManager implements IMemoryManager {
    private readonly _allocations = new Map<ArrayBuffer, { size: number; alignment: number }>();
    private _totalAllocated = 0;
    private _allocationCount = 0;

    allocate(size: number, alignment: number = 16): ArrayBuffer | null {
        if (size <= 0) return null;

        const alignedSize = this._alignSize(size, alignment);

        try {
            const buffer = new ArrayBuffer(alignedSize);
            this._allocations.set(buffer, { size: alignedSize, alignment });
            this._totalAllocated += alignedSize;
            this._allocationCount++;
            return buffer;
        } catch {
            throw ParticleSystemException.memoryAllocationFailed(alignedSize);
        }
    }

    deallocate(buffer: ArrayBuffer): void {
        const allocation = this._allocations.get(buffer);
        if (!allocation) return;

        this._allocations.delete(buffer);
        this._totalAllocated -= allocation.size;
        this._allocationCount--;
    }

    reallocate(buffer: ArrayBuffer, newSize: number): ArrayBuffer | null {
        const allocation = this._allocations.get(buffer);
        if (!allocation) return null;

        const newBuffer = this.allocate(newSize, allocation.alignment);
        if (!newBuffer) return null;

        const copySize = Math.min(buffer.byteLength, newSize);
        new Uint8Array(newBuffer).set(new Uint8Array(buffer, 0, copySize));

        this.deallocate(buffer);
        return newBuffer;
    }

    getStats() {
        return {
            totalAllocated: this._totalAllocated,
            totalUsed: this._totalAllocated,
            allocationCount: this._allocationCount,
            fragmentationRatio: 0,
        } as const;
    }

    private _alignSize(size: number, alignment: number): number {
        return Math.ceil(size / alignment) * alignment;
    }
}

export class PooledMemoryManager implements IMemoryManager {
    private readonly _pools = new Map<number, ArrayBuffer[]>();
    private readonly _allocations = new Map<ArrayBuffer, number>();
    private readonly _poolSizes: readonly number[];
    private _totalAllocated = 0;
    private _totalUsed = 0;
    private _allocationCount = 0;

    constructor(poolSizes: readonly number[] = [64, 256, 1024, 4096, 16384, 65536]) {
        this._poolSizes = [...poolSizes].sort((a, b) => a - b);
        for (const size of this._poolSizes) {
            this._pools.set(size, []);
        }
    }

    allocate(size: number, alignment: number = 16): ArrayBuffer | null {
        if (size <= 0) return null;

        const alignedSize = this._alignSize(size, alignment);
        const poolSize = this._findPoolSize(alignedSize);

        if (poolSize) {
            const pool = this._pools.get(poolSize)!;
            let buffer = pool.pop();

            if (!buffer) {
                try {
                    buffer = new ArrayBuffer(poolSize);
                } catch {
                    throw ParticleSystemException.memoryAllocationFailed(poolSize);
                }
            }

            this._allocations.set(buffer, poolSize);
            this._totalUsed += alignedSize;
            this._allocationCount++;
            return buffer;
        }

        try {
            const buffer = new ArrayBuffer(alignedSize);
            this._allocations.set(buffer, alignedSize);
            this._totalAllocated += alignedSize;
            this._totalUsed += alignedSize;
            this._allocationCount++;
            return buffer;
        } catch {
            throw ParticleSystemException.memoryAllocationFailed(alignedSize);
        }
    }

    deallocate(buffer: ArrayBuffer): void {
        const size = this._allocations.get(buffer);
        if (!size) return;

        this._allocations.delete(buffer);
        this._totalUsed -= buffer.byteLength;
        this._allocationCount--;

        const pool = this._pools.get(size);
        if (pool && pool.length < 100) {
            pool.push(buffer);
        } else if (!pool) {
            this._totalAllocated -= size;
        }
    }

    reallocate(buffer: ArrayBuffer, newSize: number): ArrayBuffer | null {
        const oldSize = this._allocations.get(buffer);
        if (!oldSize) return null;

        const newBuffer = this.allocate(newSize);
        if (!newBuffer) return null;

        const copySize = Math.min(buffer.byteLength, newSize);
        new Uint8Array(newBuffer).set(new Uint8Array(buffer, 0, copySize));

        this.deallocate(buffer);
        return newBuffer;
    }

    getStats() {
        const totalPooled = Array.from(this._pools.entries()).reduce(
            (sum, [size, pool]) => sum + size * pool.length,
            0
        );

        return {
            totalAllocated: this._totalAllocated + totalPooled,
            totalUsed: this._totalUsed,
            allocationCount: this._allocationCount,
            fragmentationRatio: totalPooled / (this._totalAllocated + totalPooled),
        } as const;
    }

    private _alignSize(size: number, alignment: number): number {
        return Math.ceil(size / alignment) * alignment;
    }

    private _findPoolSize(size: number): number | null {
        for (const poolSize of this._poolSizes) {
            if (poolSize >= size) return poolSize;
        }
        return null;
    }
}

export class TypedArrayPool<
    T extends
        | Float32Array
        | Float64Array
        | Int8Array
        | Int16Array
        | Int32Array
        | Uint8Array
        | Uint16Array
        | Uint32Array,
> {
    private readonly _available: T[] = [];
    private readonly _inUse = new Set<T>();
    private readonly _constructor: new (length: number) => T;
    private readonly _defaultSize: number;

    constructor(
        arrayConstructor: new (length: number) => T,
        defaultSize: number = 1024,
        initialPoolSize: number = 8
    ) {
        this._constructor = arrayConstructor;
        this._defaultSize = defaultSize;

        for (let i = 0; i < initialPoolSize; i++) {
            this._available.push(new arrayConstructor(defaultSize));
        }
    }

    acquire(size?: number): T {
        const actualSize = size ?? this._defaultSize;
        let array = this._available.find((arr) => arr.length >= actualSize);

        if (!array) {
            array = new this._constructor(actualSize);
        } else {
            const index = this._available.indexOf(array);
            this._available.splice(index, 1);
        }

        this._inUse.add(array);
        return array;
    }

    release(array: T): void {
        if (!this._inUse.has(array)) return;

        this._inUse.delete(array);

        if (this._available.length < 32) {
            try {
                (array as any).fill(0);
            } catch {
                // Some typed arrays might not support fill
            }
            this._available.push(array);
        }
    }

    clear(): void {
        this._available.length = 0;
        this._inUse.clear();
    }

    get size(): number {
        return this._available.length;
    }

    get inUse(): number {
        return this._inUse.size;
    }
}

export const MemoryManager = {
    aligned: new AlignedMemoryManager(),
    pooled: new PooledMemoryManager(),

    float32Pool: new TypedArrayPool(Float32Array),
    float64Pool: new TypedArrayPool(Float64Array),
    uint32Pool: new TypedArrayPool(Uint32Array),
    uint16Pool: new TypedArrayPool(Uint16Array),
    uint8Pool: new TypedArrayPool(Uint8Array),
    int32Pool: new TypedArrayPool(Int32Array),
    int16Pool: new TypedArrayPool(Int16Array),
    int8Pool: new TypedArrayPool(Int8Array),
} as const;
