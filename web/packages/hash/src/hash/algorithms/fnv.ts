import type { BytesLike } from '../../../types';
import { float32ToBits, float64ToBitsPair, writeU32LE } from '../bits';
import { asHash32, asHash64, asSeed32, type Hash32, type Hash64, type HashValue, type Seed32, type HashAlgorithmMetadata } from '../types';
import type { IHasher } from '../interfaces';
import { encode } from '@axrone/utility';

const FNV_METADATA: HashAlgorithmMetadata = {
    name: 'fnv1a-32',
    family: 'fast',
    category: 'non-crypto',
    outputSize: 32,
    blockSize: 1,
    seedable: true,
    keyed: false,
    cryptographicallySecure: false,
    description: 'FNV-1a 32-bit non-cryptographic hash (Fowler-Noll-Voy)',
};

abstract class Fnv1a32Base implements IHasher<Hash32> {
    abstract readonly algorithm: string;
    abstract readonly metadata: Readonly<HashAlgorithmMetadata>;
    protected _h: number = 0;
    protected _byteLength: number = 0;
    protected _finalized: boolean = false;
    protected _initialSeed: number = 0;
    protected _checkFinalized(): void {}

    get seed(): Seed32 { return asSeed32(this._initialSeed); }
    get byteLength(): number { return this._byteLength; }
    get finalized(): boolean { return this._finalized; }

    abstract updateBytes(bytes: BytesLike, offset?: number, length?: number): this;

    updateString(input: string): this {
        this._checkFinalized();
        for (let i = 0; i < input.length; i++) {
            const c = input.charCodeAt(i);
            this._h = Math.imul(this._h ^ (c & 0xff), 0x01000193) >>> 0;
            this._h = Math.imul(this._h ^ ((c >>> 8) & 0xff), 0x01000193) >>> 0;
        }
        this._byteLength += input.length * 2;
        return this;
    }

    updateBoolean(value: boolean): this {
        this._checkFinalized();
        this._h = Math.imul(this._h ^ (value ? 1 : 0), 0x01000193) >>> 0;
        this._byteLength += 1;
        return this;
    }

    updateI8(v: number): this { return this.updateI32(v | 0); }
    updateI16(v: number): this { return this.updateI32(v | 0); }
    updateI32(value: number): this { return this.updateU32(value | 0); }

    updateI64(value: bigint): this {
        this._checkFinalized();
        let v = value;
        for (let i = 0; i < 8; i++) {
            this._h = Math.imul(this._h ^ Number(v & 0xffn), 0x01000193) >>> 0;
            v >>= 8n;
        }
        this._byteLength += 8;
        return this;
    }

    updateU8(v: number): this { return this.updateU32(v & 0xff); }
    updateU16(v: number): this { return this.updateU32(v & 0xffff); }

    updateU32(value: number): this {
        this._checkFinalized();
        this._h = Math.imul(this._h ^ (value & 0xff), 0x01000193) >>> 0;
        this._h = Math.imul(this._h ^ ((value >>> 8) & 0xff), 0x01000193) >>> 0;
        this._h = Math.imul(this._h ^ ((value >>> 16) & 0xff), 0x01000193) >>> 0;
        this._h = Math.imul(this._h ^ ((value >>> 24) & 0xff), 0x01000193) >>> 0;
        this._byteLength += 4;
        return this;
    }

    updateU64(value: bigint): this { return this.updateI64(value); }
    updateF32(value: number): this { return this.updateU32(float32ToBits(value)); }

    updateF64(value: number): this {
        const [lo, hi] = float64ToBitsPair(value);
        return this.updateU32(lo).updateU32(hi);
    }

    updateHash(value: Hash32 | Hash64 | bigint): this {
        this._checkFinalized();
        if (typeof value === 'number') return this.updateU32(value);
        let v = value as bigint;
        for (let i = 0; i < 8; i++) {
            this._h = Math.imul(this._h ^ Number(v & 0xffn), 0x01000193) >>> 0;
            v >>= 8n;
        }
        this._byteLength += 8;
        return this;
    }

    updateHashable<H2 extends HashValue>(value: { hashInto(hasher: IHasher<H2>): void }): this {
        value.hashInto(this as unknown as IHasher<H2>);
        return this;
    }

    updateAny(value: unknown): this {
        if (value === null || value === undefined) { this._h = Math.imul(this._h, 0x01000193) >>> 0; return this; }
        if (typeof value === 'number') {
            if (Number.isInteger(value)) return this.updateI32(value);
            return this.updateF64(value);
        }
        if (typeof value === 'bigint') return this.updateI64(value);
        if (typeof value === 'string') return this.updateString(value);
        if (typeof value === 'boolean') return this.updateBoolean(value);
        if (value instanceof ArrayBuffer || ArrayBuffer.isView(value)) return this.updateBytes(value as unknown as ArrayLike<number>);
        return this;
    }

    digest(): Hash32 {
        this._finalized = true;
        return asHash32(this._h);
    }

    digestBytes(): Uint8Array {
        const h = this.digest();
        const out = new Uint8Array(4);
        writeU32LE(h as number, out, 0);
        return out;
    }

    digestHex(uppercase: boolean = false): string {
        const h = this.digest() as number;
        const s = h.toString(16).padStart(8, '0');
        return uppercase ? s.toUpperCase() : s;
    }

    digestBase64(): string {
        return encode(this.digestBytes());
    }

    digestBigInt<H2 extends bigint = bigint>(): H2 {
        return BigInt(this.digest() as number) as H2;
    }

    abstract reset(seed?: Seed32): this;
    abstract clone(): IHasher<Hash32>;
}

export class Fnv1a32 extends Fnv1a32Base {
    readonly algorithm: string = FNV_METADATA.name;
    readonly metadata: Readonly<HashAlgorithmMetadata> = FNV_METADATA;

    constructor(seed: Seed32 = asSeed32(0)) {
        super();
        this._initialSeed = (seed as number) >>> 0;
        this._h = (this._initialSeed ^ 0x811c9dc5) >>> 0;
    }

    protected override _checkFinalized(): void {
        if (this._finalized) throw new Error(`Fnv1a32: cannot update after digest()`);
    }

    updateBytes(bytes: BytesLike, offset: number = 0, length?: number): this {
        this._checkFinalized();
        const end = length === undefined ? bytes.length : offset + length;
        for (let i = offset; i < end; i++) {
            this._h = Math.imul(this._h ^ (bytes[i]! & 0xff), 0x01000193) >>> 0;
        }
        this._byteLength += end - offset;
        return this;
    }

    reset(seed: Seed32 = asSeed32(0)): this {
        this._initialSeed = (seed as number) >>> 0;
        this._h = (this._initialSeed ^ 0x811c9dc5) >>> 0;
        this._byteLength = 0;
        this._finalized = false;
        return this;
    }

    clone(): IHasher<Hash32> {
        const c = new Fnv1a32(this.seed);
        c._h = this._h;
        c._byteLength = this._byteLength;
        c._finalized = this._finalized;
        return c;
    }


}

const FNV1_METADATA: HashAlgorithmMetadata = {
    ...FNV_METADATA,
    name: 'fnv1-32',
    description: 'FNV-1 32-bit (multiply first, then XOR)',
};

export class Fnv1_32 extends Fnv1a32Base {
    readonly algorithm: string = FNV1_METADATA.name;
    readonly metadata: Readonly<HashAlgorithmMetadata> = FNV1_METADATA;

    constructor(seed: Seed32 = asSeed32(0)) {
        super();
        this._initialSeed = (seed as number) >>> 0;
        this._h = (this._initialSeed ^ 0x811c9dc5) >>> 0;
    }

    protected override _checkFinalized(): void {
        if (this._finalized) throw new Error(`Fnv1_32: cannot update after digest()`);
    }

    updateBytes(bytes: BytesLike, offset: number = 0, length?: number): this {
        this._checkFinalized();
        const end = length === undefined ? bytes.length : offset + length;
        for (let i = offset; i < end; i++) {
            this._h = (Math.imul(this._h, 0x01000193) ^ (bytes[i]! & 0xff)) >>> 0;
        }
        this._byteLength += end - offset;
        return this;
    }

    reset(seed: Seed32 = asSeed32(0)): this {
        this._initialSeed = (seed as number) >>> 0;
        this._h = (this._initialSeed ^ 0x811c9dc5) >>> 0;
        this._byteLength = 0;
        this._finalized = false;
        return this;
    }

    clone(): IHasher<Hash32> {
        const c = new Fnv1_32(this.seed);
        c._h = this._h;
        c._byteLength = this._byteLength;
        c._finalized = this._finalized;
        return c;
    }


}

const FNV64_METADATA: HashAlgorithmMetadata = {
    name: 'fnv1a-64',
    family: 'fast',
    category: 'non-crypto',
    outputSize: 64,
    blockSize: 1,
    seedable: true,
    keyed: false,
    cryptographicallySecure: false,
    description: 'FNV-1a 64-bit non-cryptographic hash (bigint)',
};

export class Fnv1a64 implements IHasher<Hash64> {
    readonly algorithm: string = FNV64_METADATA.name;
    readonly metadata: Readonly<HashAlgorithmMetadata> = FNV64_METADATA;
    private _h: bigint = 0n;
    private _byteLength: number = 0;
    private _finalized: boolean = false;
    private _initialSeed: bigint = 0n;

    constructor(seed: Seed32 = asSeed32(0)) {
        this._initialSeed = BigInt((seed as number) >>> 0);
        this._h = (this._initialSeed ^ 0xcbf29ce484222325n) & 0xffffffffffffffffn;
    }

    get seed(): Seed32 { return asSeed32(Number(this._initialSeed)); }
    get byteLength(): number { return this._byteLength; }
    get finalized(): boolean { return this._finalized; }

    private _checkFinalized(): void {
        if (this._finalized) throw new Error(`Fnv1a64: cannot update after digest()`);
    }

    updateBytes(bytes: BytesLike, offset: number = 0, length?: number): this {
        this._checkFinalized();
        const end = length === undefined ? bytes.length : offset + length;
        for (let i = offset; i < end; i++) {
            this._h = ((this._h ^ BigInt(bytes[i]! & 0xff)) * 0x100000001b3n) & 0xffffffffffffffffn;
        }
        this._byteLength += end - offset;
        return this;
    }

    updateString(input: string): this {
        this._checkFinalized();
        for (let i = 0; i < input.length; i++) {
            const c = BigInt(input.charCodeAt(i));
            this._h = ((this._h ^ (c & 0xffn)) * 0x100000001b3n) & 0xffffffffffffffffn;
            this._h = ((this._h ^ ((c >> 8n) & 0xffn)) * 0x100000001b3n) & 0xffffffffffffffffn;
        }
        this._byteLength += input.length * 2;
        return this;
    }

    updateBoolean(value: boolean): this {
        this._checkFinalized();
        this._h = (this._h * 0x100000001b3n) & 0xffffffffffffffffn;
        this._h = (this._h ^ BigInt(value ? 1 : 0)) & 0xffffffffffffffffn;
        this._byteLength += 1;
        return this;
    }

    updateI8(v: number): this { return this.updateI32(v | 0); }
    updateI16(v: number): this { return this.updateI32(v | 0); }
    updateI32(value: number): this { return this.updateU32(value | 0); }

    updateI64(value: bigint): this {
        this._checkFinalized();
        let v = value & 0xffffffffffffffffn;
        for (let i = 0; i < 8; i++) {
            this._h = ((this._h ^ (v & 0xffn)) * 0x100000001b3n) & 0xffffffffffffffffn;
            v >>= 8n;
        }
        this._byteLength += 8;
        return this;
    }

    updateU8(v: number): this { return this.updateU32(v & 0xff); }
    updateU16(v: number): this { return this.updateU32(v & 0xffff); }

    updateU32(value: number): this {
        this._checkFinalized();
        const v = BigInt(value >>> 0);
        for (let i = 0; i < 4; i++) {
            this._h = ((this._h ^ ((v >> BigInt(i * 8)) & 0xffn)) * 0x100000001b3n) & 0xffffffffffffffffn;
        }
        this._byteLength += 4;
        return this;
    }

    updateU64(value: bigint): this { return this.updateI64(value); }
    updateF32(value: number): this { return this.updateU32(float32ToBits(value)); }

    updateF64(value: number): this {
        const [lo, hi] = float64ToBitsPair(value);
        return this.updateU32(lo).updateU32(hi);
    }

    updateHash(value: Hash32 | Hash64 | bigint): this {
        this._checkFinalized();
        if (typeof value === 'number') return this.updateU32(value);
        let v = value as bigint;
        for (let i = 0; i < 8; i++) {
            this._h = ((this._h ^ (v & 0xffn)) * 0x100000001b3n) & 0xffffffffffffffffn;
            v >>= 8n;
        }
        this._byteLength += 8;
        return this;
    }

    updateHashable<H2 extends HashValue>(value: { hashInto(hasher: IHasher<H2>): void }): this {
        value.hashInto(this as unknown as IHasher<H2>);
        return this;
    }

    updateAny(value: unknown): this {
        if (value === null || value === undefined) { this._h = (this._h * 0x100000001b3n) & 0xffffffffffffffffn; return this; }
        if (typeof value === 'number') {
            if (Number.isInteger(value)) return this.updateI32(value);
            return this.updateF64(value);
        }
        if (typeof value === 'bigint') return this.updateI64(value);
        if (typeof value === 'string') return this.updateString(value);
        if (typeof value === 'boolean') return this.updateBoolean(value);
        if (value instanceof ArrayBuffer || ArrayBuffer.isView(value)) return this.updateBytes(value as unknown as ArrayLike<number>);
        return this;
    }

    digest(): Hash64 {
        this._finalized = true;
        return asHash64(this._h);
    }

    digestBytes(): Uint8Array {
        const h = this.digest() as bigint;
        const out = new Uint8Array(8);
        for (let i = 0; i < 8; i++) {
            out[i] = Number((h >> BigInt(i * 8)) & 0xffn);
        }
        return out;
    }

    digestHex(uppercase: boolean = false): string {
        let h = this.digest() as bigint;
        let s = '';
        for (let i = 0; i < 16; i++) {
            s = (h & 0xfn).toString(16) + s;
            h >>= 4n;
        }
        return uppercase ? s.toUpperCase() : s;
    }

    digestBase64(): string {
        return encode(this.digestBytes());
    }

    digestBigInt<H2 extends bigint = bigint>(): H2 {
        return this.digest() as unknown as H2;
    }

    reset(seed: Seed32 = asSeed32(0)): this {
        this._initialSeed = BigInt((seed as number) >>> 0);
        this._h = (this._initialSeed ^ 0xcbf29ce484222325n) & 0xffffffffffffffffn;
        this._byteLength = 0;
        this._finalized = false;
        return this;
    }

    clone(): IHasher<Hash64> {
        const c = new Fnv1a64(this.seed);
        c._h = this._h;
        c._byteLength = this._byteLength;
        c._finalized = this._finalized;
        return c;
    }


}
