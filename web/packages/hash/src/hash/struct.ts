import type { IHasher, IHashable } from './interfaces';
import { createHasher } from './factory';
import type { HashValue, HashAlgorithmName } from './types';
import { fmix32 } from './mixers';
import { fnv1aMix32, fnv1aMixU32, FNV_PRIME_32 } from './mixing';

const STRUCT_F64_BUF = new Float64Array(1);
const STRUCT_I32_BUF = new Int32Array(STRUCT_F64_BUF.buffer);
const STRUCT_F32_BUF = new Float32Array(1);
const STRUCT_F32_I32_BUF = new Int32Array(STRUCT_F32_BUF.buffer);

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
        this._h = fnv1aMixU32(this._h, value);
        this._byteLength += 4;
        return this;
    }

    mixString(value: string): this {
        for (let i = 0; i < value.length; i++) {
            const c = value.charCodeAt(i);
            this._h = fnv1aMix32(this._h, c & 0xff);
            this._h = fnv1aMix32(this._h, (c >>> 8) & 0xff);
        }
        this._byteLength += value.length * 2;
        return this;
    }

    mixBoolean(value: boolean): this {
        this._h = fnv1aMix32(this._h, value ? 1 : 0);
        this._byteLength += 1;
        return this;
    }

    mixNumber(value: number): this {
        STRUCT_F64_BUF[0] = value;
        this.mixIn(STRUCT_I32_BUF[0]!).mixIn(STRUCT_I32_BUF[1]!);
        return this;
    }

    mixInt(value: number): this {
        return this.mixIn(value | 0);
    }

    mixU32(value: number): this {
        return this.mixIn(value >>> 0);
    }

    mixF32(value: number): this {
        STRUCT_F32_BUF[0] = value;
        return this.mixIn(STRUCT_F32_I32_BUF[0]!);
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
            this._h = fnv1aMix32(this._h, Number(v & 0xffn));
            v >>= 8n;
        }
        this._byteLength += 8;
        return this;
    }

    mixStruct(value: unknown): this {
        if (value === null || value === undefined) {
            this._h = fnv1aMix32(this._h, 0);
            return this;
        }
        if (typeof value === 'object' && value !== null && 'hashInto' in value) {
            const hashable = value as IHashable;
            const h = createHasher<any>('fnv1a-32');
            hashable.hashInto(h);
            return this.mixHash(h.digest());
        }
        throw new Error(`structHash: cannot hash plain object of type ${typeof value}. Implement IHashable interface or use mixIn/mixString/mixNumber.`);
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
