import type { IHasher, IHashable } from './interfaces';
import { createHasher } from './factory';
import type { HashValue, HashAlgorithmName } from './types';
import { fmix32 } from './mixers';

export class StructState {
    private _h: number;
    private _byteLength: number = 0;

    constructor(seed: number = 0x811c9dc5) {
        this._h = seed >>> 0;
    }

    get state(): number {
        return this._h;
    }

    get byteLength(): number {
        return this._byteLength;
    }

    mixIn(value: number): this {
        this._h = (Math.imul(this._h ^ (value & 0xff), 0x01000193)) >>> 0;
        this._h = (Math.imul(this._h ^ ((value >>> 8) & 0xff), 0x01000193)) >>> 0;
        this._h = (Math.imul(this._h ^ ((value >>> 16) & 0xff), 0x01000193)) >>> 0;
        this._h = (Math.imul(this._h ^ ((value >>> 24) & 0xff), 0x01000193)) >>> 0;
        this._byteLength += 4;
        return this;
    }

    mixString(value: string): this {
        for (let i = 0; i < value.length; i++) {
            const c = value.charCodeAt(i);
            this._h = Math.imul(this._h ^ (c & 0xff), 0x01000193) >>> 0;
            this._h = Math.imul(this._h ^ ((c >>> 8) & 0xff), 0x01000193) >>> 0;
        }
        this._byteLength += value.length * 2;
        return this;
    }

    mixBoolean(value: boolean): this {
        this._h = Math.imul(this._h ^ (value ? 1 : 0), 0x01000193) >>> 0;
        this._byteLength += 1;
        return this;
    }

    mixNumber(value: number): this {
        const buf = new Float64Array(1);
        const ibuf = new Int32Array(buf.buffer);
        buf[0] = value;
        this.mixIn(ibuf[0]!).mixIn(ibuf[1]!);
        return this;
    }

    mixInt(value: number): this {
        return this.mixIn(value | 0);
    }

    mixU32(value: number): this {
        return this.mixIn(value >>> 0);
    }

    mixF32(value: number): this {
        const buf = new Float32Array(1);
        const ibuf = new Int32Array(buf.buffer);
        buf[0] = value;
        return this.mixIn(ibuf[0]!);
    }

    mixF64(value: number): this {
        return this.mixNumber(value);
    }

    mixHash(value: HashValue): this {
        if (typeof value === 'number') {
            return this.mixIn(value >>> 0);
        }
        let v = value as bigint;
        for (let i = 0; i < 8; i++) {
            this._h = Math.imul(this._h ^ Number(v & 0xffn), 0x01000193) >>> 0;
            v >>= 8n;
        }
        this._byteLength += 8;
        return this;
    }

    mixStruct(value: unknown): this {
        if (value === null || value === undefined) {
            this._h = Math.imul(this._h, 0x01000193) >>> 0;
            return this;
        }
        if (typeof value === 'object' && value !== null && 'hashInto' in value) {
            const hashable = value as IHashable;
            const h = createHasher<any>('fnv1a-32');
            hashable.hashInto(h);
            return this.mixHash(h.digest());
        }
        return this;
    }

    digest(): number {
        return fmix32(this._h);
    }

    reset(seed: number = 0x811c9dc5): this {
        this._h = seed >>> 0;
        this._byteLength = 0;
        return this;
    }
}

export interface StructFieldWriter<T> {
    (state: StructState, value: T): void;
}

export interface StructFieldInfo<T = unknown> {
    readonly name: string;
    readonly writer: StructFieldWriter<T>;
}

export function structHash<T>(value: T): number {
    const state = new StructState();
    state.mixStruct(value);
    return state.digest();
}

export function fieldString(): StructFieldWriter<string> {
    return (s, v) => s.mixString(v);
}

export function fieldNumber(): StructFieldWriter<number> {
    return (s, v) => s.mixNumber(v);
}

export function fieldInt(): StructFieldWriter<number> {
    return (s, v) => s.mixInt(v);
}

export function fieldU32(): StructFieldWriter<number> {
    return (s, v) => s.mixU32(v);
}

export function fieldF32(): StructFieldWriter<number> {
    return (s, v) => s.mixF32(v);
}

export function fieldF64(): StructFieldWriter<number> {
    return (s, v) => s.mixF64(v);
}

export function fieldBoolean(): StructFieldWriter<boolean> {
    return (s, v) => s.mixBoolean(v);
}

export function fieldHash(): StructFieldWriter<HashValue> {
    return (s, v) => s.mixHash(v);
}

export function fieldStruct(): StructFieldWriter<unknown> {
    return (s, v) => s.mixStruct(v);
}
