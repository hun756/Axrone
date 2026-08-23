import { ShaderInstance } from './instance';
import type { ICompiledShader, IShaderVariant, ShaderUniformValue } from './interfaces';
import type { IGLContext } from '../context';
import { getOrCreateGLContext, isGLContext } from '../context';

export type ContextSource = IGLContext | WebGL2RenderingContext;

const resolveContext = (source: ContextSource): IGLContext =>
    isGLContext(source) ? source : getOrCreateGLContext(source as WebGL2RenderingContext);

const resolveContextNullable = (source: ContextSource | null | undefined): IGLContext | null => {
    if (!source) return null;
    if (isGLContext(source)) return source;
    try {
        return getOrCreateGLContext(source as WebGL2RenderingContext);
    } catch {
        return null;
    }
};

const resolveRawGL = (source: ContextSource | null): WebGL2RenderingContext | null => {
    if (!source) return null;
    if (isGLContext(source)) return source.gl;
    return source as WebGL2RenderingContext;
};

export interface ShaderInstancePoolOptions {
    readonly initialCapacity: number;
    readonly maxCapacity: number;
    readonly growthFactor: number;
    readonly shrinkThreshold: number;
}

export interface ShaderInstancePoolStats {
    readonly created: number;
    readonly acquired: number;
    readonly released: number;
    readonly reused: number;
    readonly inUse: number;
    readonly idle: number;
    readonly total: number;
    readonly hitRate: number;
}

interface PoolKey {
    readonly shader: string;
    readonly variant: string;
}

const keyFrom = (shader: ICompiledShader, variant: IShaderVariant): PoolKey => ({
    shader: shader.name,
    variant: variant.hash,
});

const DEFAULT_POOL_OPTIONS: ShaderInstancePoolOptions = {
    initialCapacity: 16,
    maxCapacity: 1024,
    growthFactor: 1.5,
    shrinkThreshold: 0.25,
};

class PoolBucket {
    private readonly idle: ShaderInstance[] = [];
    private readonly inUse: WeakSet<ShaderInstance> = new WeakSet();
    private created = 0;
    private acquired = 0;
    private released = 0;
    private reused = 0;
    private readonly _ctx: IGLContext | null;
    private readonly gl: WebGL2RenderingContext | null;

    constructor(
        public readonly shader: ICompiledShader,
        public readonly variant: IShaderVariant,
        private readonly maxCapacity: number,
        source: ContextSource | null
    ) {
        const ctx = resolveContextNullable(source);
        this._ctx = ctx;
        this.gl = ctx?.gl ?? resolveRawGL(source);
    }

    acquire(): ShaderInstance {
        let instance: ShaderInstance | null = null;
        while (this.idle.length > 0) {
            const candidate = this.idle.pop();
            if (candidate && !candidate.isDisposed()) {
                instance = candidate;
                this.reused++;
                break;
            }
        }
        if (!instance) {
            instance = new ShaderInstance(this.shader, this.variant, this._ctx ?? this.gl);
            this.created++;
        }
        this.acquired++;
        this.inUse.add(instance);
        return instance;
    }

    release(instance: ShaderInstance): void {
        if (!this.inUse.has(instance)) return;
        this.inUse.delete(instance);
        instance.dispose();
        this.released++;
        if (this.idle.length < this.maxCapacity) {
            this.idle.push(instance);
        }
    }

    releaseAll(): void {
        for (const instance of this.idle) {
            instance.dispose();
        }
        this.idle.length = 0;
    }

    stats(inUseCount: number): {
        created: number;
        acquired: number;
        released: number;
        reused: number;
        inUse: number;
        idle: number;
    } {
        return {
            created: this.created,
            acquired: this.acquired,
            released: this.released,
            reused: this.reused,
            inUse: inUseCount,
            idle: this.idle.length,
        };
    }
}

export class ShaderInstancePool {
    private readonly buckets: Map<string, PoolBucket> = new Map();
    private readonly inUseCount: Map<string, number> = new Map();
    private readonly options: ShaderInstancePoolOptions;
    private readonly _ctx: IGLContext | null;
    private readonly gl: WebGL2RenderingContext | null;

    constructor(source: ContextSource | null, options?: Partial<ShaderInstancePoolOptions>) {
        const ctx = resolveContextNullable(source);
        this._ctx = ctx;
        this.gl = ctx?.gl ?? resolveRawGL(source);
        this.options = { ...DEFAULT_POOL_OPTIONS, ...options };
    }

    public get context(): IGLContext | null {
        return this._ctx;
    }

    acquire(shader: ICompiledShader, variant: IShaderVariant): ShaderInstance {
        const key = keyFrom(shader, variant);
        const keyStr = `${key.shader}::${key.variant}`;
        let bucket = this.buckets.get(keyStr);
        if (!bucket) {
            bucket = new PoolBucket(shader, variant, this.options.maxCapacity, this._ctx ?? this.gl);
            this.buckets.set(keyStr, bucket);
            this.inUseCount.set(keyStr, 0);
        }
        const instance = bucket.acquire();
        this.inUseCount.set(keyStr, (this.inUseCount.get(keyStr) ?? 0) + 1);
        return instance;
    }

    acquireWith(
        shader: ICompiledShader,
        variant: IShaderVariant,
        uniforms: Readonly<Record<string, ShaderUniformValue>>
    ): ShaderInstance {
        const instance = this.acquire(shader, variant);
        for (const [name, value] of Object.entries(uniforms)) {
            instance.setUniform(name, value);
        }
        return instance;
    }

    release(instance: ShaderInstance): void {
        const keyStr = `${instance.shader.name}::${instance.variant.hash}`;
        const bucket = this.buckets.get(keyStr);
        if (!bucket) return;
        bucket.release(instance);
        this.inUseCount.set(keyStr, Math.max(0, (this.inUseCount.get(keyStr) ?? 0) - 1));
        this.maybeShrink(keyStr, bucket);
    }

    releaseAll(): void {
        for (const bucket of this.buckets.values()) {
            bucket.releaseAll();
        }
        for (const key of this.inUseCount.keys()) {
            this.inUseCount.set(key, 0);
        }
    }

    getStats(): Readonly<ShaderInstancePoolStats> {
        let created = 0;
        let acquired = 0;
        let released = 0;
        let reused = 0;
        let inUse = 0;
        let idle = 0;
        for (const [key, bucket] of this.buckets) {
            const s = bucket.stats(this.inUseCount.get(key) ?? 0);
            created += s.created;
            acquired += s.acquired;
            released += s.released;
            reused += s.reused;
            inUse += s.inUse;
            idle += s.idle;
        }
        return {
            created,
            acquired,
            released,
            reused,
            inUse,
            idle,
            total: inUse + idle,
            hitRate: acquired === 0 ? 0 : reused / acquired,
        };
    }

    resize(newMaxCapacity: number): void {
        (this.options as { maxCapacity: number }).maxCapacity = newMaxCapacity;
    }

    private maybeShrink(key: string, bucket: PoolBucket): void {
        const idleCount = bucket.stats(this.inUseCount.get(key) ?? 0).idle;
        const totalCount = idleCount + (this.inUseCount.get(key) ?? 0);
        if (totalCount === 0) return;
        const idleRatio = idleCount / totalCount;
        if (idleRatio > 1 - this.options.shrinkThreshold) {
            bucket.releaseAll();
        }
    }
}
