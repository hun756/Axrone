import { ShaderInstance } from './instance';
import { ShaderInstancePool } from './pool';
import { UniformCache } from './uniform-cache';
import {
    ShaderInstanceError,
    ShaderInstanceValidationError,
    ShaderInstanceLifecycleError,
    ShaderInstanceBackendError,
    isShaderInstanceError,
} from './errors';
import type {
    ICompiledShader,
    IShaderVariant,
    ShaderDataType,
    ShaderUniformValue,
} from './interfaces';

export interface ShaderInstanceManagerOptions {
    readonly gl: WebGL2RenderingContext;
    readonly pool?: {
        readonly initialCapacity?: number;
        readonly maxCapacity?: number;
        readonly growthFactor?: number;
        readonly shrinkThreshold?: number;
    };
    readonly cache?: {
        readonly maxEntries?: number;
    };
}

export interface ShaderInstanceManagerStats {
    readonly pool: ReturnType<ShaderInstancePool['getStats']>;
    readonly cache: ReturnType<UniformCache['getStats']>;
    readonly instanceCount: number;
    readonly disposedInstances: number;
}

export interface BorrowedShaderInstance {
    readonly instance: ShaderInstance;
    release(): void;
}

interface TrackedInstance {
    instance: ShaderInstance;
    acquireTime: number;
    borrowCount: number;
}

const DEFAULT_POOL_OPTIONS = {
    initialCapacity: 32,
    maxCapacity: 1024,
    growthFactor: 1.5,
    shrinkThreshold: 0.25,
} as const;

const DEFAULT_CACHE_OPTIONS = {
    maxEntries: 8192,
} as const;

export class ShaderInstanceManager {
    private readonly gl: WebGL2RenderingContext;
    private readonly pool: ShaderInstancePool;
    private readonly cache: UniformCache;
    private readonly tracked: WeakSet<ShaderInstance> = new WeakSet();
    private readonly trackedRegistry: Map<ShaderInstance, TrackedInstance> = new Map();
    private instanceCount = 0;
    private disposedCount = 0;
    private disposed = false;

    constructor(options: ShaderInstanceManagerOptions) {
        this.gl = options.gl;
        this.pool = new ShaderInstancePool(this.gl, {
            ...DEFAULT_POOL_OPTIONS,
            ...(options.pool ?? {}),
        });
        this.cache = new UniformCache(options.cache?.maxEntries ?? DEFAULT_CACHE_OPTIONS.maxEntries);
    }

    borrow(shader: ICompiledShader, variant: IShaderVariant): BorrowedShaderInstance {
        if (this.disposed) {
            throw new ShaderInstanceLifecycleError('PROGRAM_DISPOSED', 'en', {
                shader: shader.name,
            });
        }
        const instance = this.pool.acquire(shader, variant);
        this.tracked.add(instance);
        this.trackedRegistry.set(instance, {
            instance,
            acquireTime: performance.now(),
            borrowCount: 0,
        });
        this.instanceCount++;
        return {
            instance,
            release: () => this.release(instance),
        };
    }

    borrowWithUniforms(
        shader: ICompiledShader,
        variant: IShaderVariant,
        uniforms: Readonly<Record<string, ShaderUniformValue>>
    ): BorrowedShaderInstance {
        const borrowed = this.borrow(shader, variant);
        for (const [name, value] of Object.entries(uniforms)) {
            borrowed.instance.setUniform(name, value);
        }
        const tracked = this.trackedRegistry.get(borrowed.instance);
        if (tracked) tracked.borrowCount++;
        return borrowed;
    }

    setUniformCached(
        instance: ShaderInstance,
        name: string,
        type: ShaderDataType,
        arraySize: number,
        value: ShaderUniformValue
    ): boolean {
        const scalar = toScalar(value);
        const shouldUpload = this.cache.record(name, type, arraySize, scalar);
        if (shouldUpload) {
            instance.setUniform(name, value);
        }
        return shouldUpload;
    }

    release(instance: ShaderInstance): void {
        if (!this.tracked.has(instance)) return;
        this.tracked.delete(instance);
        this.trackedRegistry.delete(instance);
        this.instanceCount = Math.max(0, this.instanceCount - 1);
        if (instance.isDisposed()) {
            this.disposedCount++;
            return;
        }
        this.pool.release(instance);
    }

    releaseAll(): void {
        this.pool.releaseAll();
        for (const tracked of this.trackedRegistry.values()) {
            tracked.instance.dispose();
        }
        this.trackedRegistry.clear();
        this.instanceCount = 0;
    }

    getStats(): Readonly<ShaderInstanceManagerStats> {
        return {
            pool: this.pool.getStats(),
            cache: this.cache.getStats(),
            instanceCount: this.instanceCount,
            disposedInstances: this.disposedCount,
        };
    }

    invalidateCache(name?: string): void {
        if (name === undefined) {
            this.cache.invalidateAll();
        } else {
            this.cache.invalidate(name);
        }
    }

    resizePool(newMaxCapacity: number): void {
        this.pool.resize(newMaxCapacity);
    }

    dispose(): void {
        if (this.disposed) return;
        this.disposed = true;
        this.releaseAll();
        this.cache.invalidateAll();
    }

    isDisposed(): boolean {
        return this.disposed;
    }
}

const toScalar = (value: ShaderUniformValue): number | boolean | Float32Array | Int32Array | Uint32Array | ReadonlyArray<number> | null => {
    if (value === null) return null;
    if (typeof value === 'number' || typeof value === 'boolean') return value;
    if (value instanceof Float32Array) return value;
    if (value instanceof Int32Array) return value;
    if (value instanceof Uint32Array) return value;
    if (Array.isArray(value)) return value as ReadonlyArray<number>;
    return null;
};

export {
    ShaderInstance,
    ShaderInstancePool,
    UniformCache,
    ShaderInstanceError,
    ShaderInstanceValidationError,
    ShaderInstanceLifecycleError,
    ShaderInstanceBackendError,
    isShaderInstanceError,
};
