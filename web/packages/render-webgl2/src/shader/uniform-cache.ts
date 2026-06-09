import type { ShaderUniformValue, ShaderDataType } from './interfaces';

export interface UniformDescriptorLite {
    readonly name: string;
    readonly type: ShaderDataType;
    readonly arraySize: number;
    readonly location: WebGLUniformLocation | null;
}

export interface UniformValueDescriptor {
    readonly name: string;
    readonly type: ShaderDataType;
    readonly arraySize: number;
}

type UniformFloat = number | Float32Array;
type UniformInt = number | Int32Array;
type UniformUint = number | Uint32Array;

export type UniformScalarValue =
    | number
    | boolean
    | Float32Array
    | Int32Array
    | Uint32Array
    | ReadonlyArray<number>
    | null;

interface BucketEntry {
    readonly type: ShaderDataType;
    readonly arraySize: number;
    lastValueHash: number;
    lastValue: UniformScalarValue;
}

const fnv1a = (input: string): number => {
    let hash = 0x811c9dc5;
    for (let i = 0; i < input.length; i++) {
        hash ^= input.charCodeAt(i);
        hash = Math.imul(hash, 0x01000193) >>> 0;
    }
    return hash >>> 0;
};

const hashValue = (value: UniformScalarValue): number => {
    if (value === null) return 0;
    if (typeof value === 'number') {
        const bits = Math.fround(value);
        return fnv1a(`n:${bits}`);
    }
    if (typeof value === 'boolean') return fnv1a(`b:${value ? 1 : 0}`);
    if (value instanceof Float32Array) {
        let h = fnv1a('f32:');
        for (let i = 0; i < value.length; i++) {
            h = Math.imul(h ^ fnv1a(`n:${Math.fround(value[i] as number)}`), 0x01000193) >>> 0;
        }
        return h;
    }
    if (value instanceof Int32Array) {
        let h = fnv1a('i32:');
        for (let i = 0; i < value.length; i++) {
            h = Math.imul(h ^ fnv1a(`n:${value[i] as number}`), 0x01000193) >>> 0;
        }
        return h;
    }
    if (value instanceof Uint32Array) {
        let h = fnv1a('u32:');
        for (let i = 0; i < value.length; i++) {
            h = Math.imul(h ^ fnv1a(`n:${value[i] as number}`), 0x01000193) >>> 0;
        }
        return h;
    }
    if (Array.isArray(value)) {
        let h = fnv1a('a:');
        for (let i = 0; i < value.length; i++) {
            const v = value[i];
            h = Math.imul(h ^ fnv1a(`n:${Math.fround(v as number)}`), 0x01000193) >>> 0;
        }
        return h;
    }
    return 0;
};

export class UniformCache {
    private readonly entries: Map<string, BucketEntry> = new Map();
    private readonly maxEntries: number;
    private hits = 0;
    private misses = 0;

    constructor(maxEntries: number = 4096) {
        this.maxEntries = maxEntries;
    }

    record(name: string, type: ShaderDataType, arraySize: number, value: UniformScalarValue): boolean {
        const existing = this.entries.get(name);
        if (existing && existing.type === type && existing.arraySize === arraySize) {
            const next = hashValue(value);
            if (next === existing.lastValueHash) {
                this.hits++;
                return false;
            }
            this.entries.set(name, {
                type,
                arraySize,
                lastValueHash: next,
                lastValue: value,
            });
            this.misses++;
            return true;
        }
        if (this.entries.size >= this.maxEntries) {
            const oldest = this.entries.keys().next().value;
            if (oldest !== undefined) this.entries.delete(oldest);
        }
        this.entries.set(name, {
            type,
            arraySize,
            lastValueHash: hashValue(value),
            lastValue: value,
        });
        this.misses++;
        return true;
    }

    lookup(name: string): UniformScalarValue | undefined {
        return this.entries.get(name)?.lastValue;
    }

    has(name: string): boolean {
        return this.entries.has(name);
    }

    invalidate(name: string): void {
        this.entries.delete(name);
    }

    invalidateAll(): void {
        this.entries.clear();
    }

    size(): number {
        return this.entries.size;
    }

    getStats(): { hits: number; misses: number; hitRate: number; size: number } {
        const total = this.hits + this.misses;
        return {
            hits: this.hits,
            misses: this.misses,
            hitRate: total === 0 ? 0 : this.hits / total,
            size: this.entries.size,
        };
    }
}

export type { UniformFloat, UniformInt, UniformUint };
