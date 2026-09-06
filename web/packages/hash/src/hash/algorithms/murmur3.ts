import type { BytesLike } from '../../../types';
import { rotl32, writeU32LE, encodeBase64 } from '../bits';
import { fmix32, murmur3Scramble } from '../mixers';
import { asHash32, asSeed32, type Hash32, type Seed32, type HashAlgorithmMetadata } from '../types';
import type { IHasher } from '../interfaces';
import { HasherBase } from '../base';

const MURMUR3_METADATA: HashAlgorithmMetadata = {
    name: 'murmur3-32',
    family: 'fast',
    category: 'non-crypto',
    outputSize: 32,
    blockSize: 4,
    seedable: true,
    keyed: false,
    cryptographicallySecure: false,
    async: false,
    description: 'MurmurHash3 32-bit (Austin Appleby)',
};

export class Murmur3_32 extends HasherBase<Hash32> {
    readonly algorithm: string = MURMUR3_METADATA.name;
    readonly metadata: Readonly<HashAlgorithmMetadata> = MURMUR3_METADATA;
    private _h1: number;
    private _totalLen: number = 0;
    private _tail: number = 0;
    private _tailLen: number = 0;
    private _initialSeed: number;

    constructor(seed: Seed32 = asSeed32(0)) {
        super();
        this._initialSeed = (seed as number) >>> 0;
        this._h1 = this._initialSeed;
    }

    get seed(): Seed32 {
        return asSeed32(this._initialSeed);
    }

    get byteLength(): number {
        return this._totalLen;
    }

    private _checkFinalized(): void {
        if (this._finalized) throw new Error(`Murmur3_32: cannot update after digest() (algorithm=${this.algorithm})`);
    }

    private _accumulate(byte: number): void {
        this._tail = (this._tail | (byte << (this._tailLen * 8))) >>> 0;
        this._tailLen++;
        this._totalLen++;
        if (this._tailLen === 4) {
            const k1 = murmur3Scramble(this._tail);
            this._h1 = (this._h1 ^ k1) >>> 0;
            this._h1 = rotl32(this._h1, 13);
            this._h1 = (Math.imul(this._h1, 5) + 0xe6546b64) >>> 0;
            this._tail = 0;
            this._tailLen = 0;
        }
    }

    updateBytes(bytes: BytesLike, offset: number = 0, length?: number): this {
        this._checkFinalized();
        const end = length === undefined ? bytes.length : offset + length;
        for (let i = offset; i < end; i++) {
            this._accumulate(bytes[i]! & 0xff);
        }
        return this;
    }

    updateString(input: string): this {
        this._checkFinalized();
        const bytes = new TextEncoder().encode(input);
        return this.updateBytes(bytes);
    }

    updateBoolean(value: boolean): this {
        this._checkFinalized();
        this._accumulate(value ? 1 : 0);
        return this;
    }

    updateI64(value: bigint): this {
        this._checkFinalized();
        let v = value;
        for (let i = 0; i < 8; i++) {
            this._accumulate(Number(v & 0xffn));
            v >>= 8n;
        }
        return this;
    }

    updateU32(value: number): this {
        this._checkFinalized();
        const v = value >>> 0;
        this._accumulate(v & 0xff);
        this._accumulate((v >>> 8) & 0xff);
        this._accumulate((v >>> 16) & 0xff);
        this._accumulate((v >>> 24) & 0xff);
        return this;
    }

    updateHash(value: Hash32 | bigint): this {
        this._checkFinalized();
        if (typeof value === 'number') return this.updateU32(value);
        let v = value;
        for (let i = 0; i < 8; i++) {
            this._accumulate(Number(v & 0xffn));
            v >>= 8n;
        }
        return this;
    }

    updateHashable<H2 extends import('../types').HashValue>(value: { hashInto(hasher: IHasher<H2>): void }): this {
        value.hashInto(this as unknown as IHasher<H2>);
        return this;
    }

    updateAny(value: unknown): this {
        if (value === null || value === undefined) {
            this._accumulate(0);
            return this;
        }
        if (typeof value === 'number') {
            if (Number.isInteger(value)) return this.updateI32(value);
            return this.updateF64(value);
        }
        if (typeof value === 'bigint') return this.updateI64(value);
        if (typeof value === 'string') return this.updateString(value);
        if (typeof value === 'boolean') return this.updateBoolean(value);
        if (value instanceof ArrayBuffer || ArrayBuffer.isView(value)) {
            return this.updateBytes(value as unknown as ArrayLike<number>);
        }
        return this;
    }

    digest(): Hash32 {
        this._finalized = true;
        let k1 = 0;
        switch (this._tailLen) {
            case 3:
                k1 ^= (this._tail & 0xff0000) >>> 0;
            case 2:
                k1 ^= (this._tail & 0xff00) >>> 0;
            case 1:
                k1 ^= (this._tail & 0xff) >>> 0;
                k1 = murmur3Scramble(k1);
                this._h1 = (this._h1 ^ k1) >>> 0;
        }
        this._h1 ^= this._totalLen;
        this._h1 = fmix32(this._h1);
        return asHash32(this._h1);
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
        return encodeBase64(this.digestBytes());
    }

    digestBigInt<H2 extends bigint = bigint>(): H2 {
        return BigInt(this.digest() as number) as H2;
    }

    reset(seed: Seed32 = asSeed32(0)): this {
        this._initialSeed = (seed as number) >>> 0;
        this._h1 = this._initialSeed;
        this._totalLen = 0;
        this._tail = 0;
        this._tailLen = 0;
        this._finalized = false;
        return this;
    }

    clone(): IHasher<Hash32> {
        const c = new Murmur3_32(this.seed);
        c._h1 = this._h1;
        c._totalLen = this._totalLen;
        c._tail = this._tail;
        c._tailLen = this._tailLen;
        c._finalized = this._finalized;
        return c;
    }
}

const MURMUR2_METADATA: HashAlgorithmMetadata = {
    name: 'murmur2-64',
    family: 'fast',
    category: 'non-crypto',
    outputSize: 64,
    blockSize: 8,
    seedable: true,
    keyed: false,
    cryptographicallySecure: false,
    async: false,
    description: 'MurmurHash2 64-bit',
};

export class Murmur2_64 extends HasherBase<import('../types').Hash64> {
    readonly algorithm: string = MURMUR2_METADATA.name;
    readonly metadata: Readonly<HashAlgorithmMetadata> = MURMUR2_METADATA;
    private _h: bigint;
    private _totalLen: number = 0;
    private _tail: bigint = 0n;
    private _tailLen: number = 0;
    private _initialSeed: bigint;

    constructor(seed: import('../types').Seed32 = asSeed32(0)) {
        super();
        this._initialSeed = BigInt((seed as number) >>> 0);
        this._h = this._initialSeed & 0xffffffffffffffffn;
    }

    get seed(): import('../types').Seed32 {
        return asSeed32(Number(this._initialSeed));
    }

    get byteLength(): number {
        return this._totalLen;
    }

    private _checkFinalized(): void {
        if (this._finalized) throw new Error(`Murmur2_64: cannot update after digest() (algorithm=${this.algorithm})`);
    }

    private _accumulate(byte: number): void {
        this._tail = (this._tail | (BigInt(byte & 0xff) << BigInt(this._tailLen * 8))) & 0xffffffffffffffffn;
        this._tailLen++;
        this._totalLen++;
        if (this._tailLen === 8) {
            let k = this._tail;
            k = (k * 0xc6a4a7935bd1e995n) & 0xffffffffffffffffn;
            k ^= k >> 47n;
            k = (k * 0xc6a4a7935bd1e995n) & 0xffffffffffffffffn;
            this._h ^= k;
            this._h = (this._h * 0xc6a4a7935bd1e995n) & 0xffffffffffffffffn;
            this._tail = 0n;
            this._tailLen = 0;
        }
    }

    updateBytes(bytes: BytesLike, offset: number = 0, length?: number): this {
        this._checkFinalized();
        const end = length === undefined ? bytes.length : offset + length;
        for (let i = offset; i < end; i++) {
            this._accumulate(bytes[i]! & 0xff);
        }
        return this;
    }

    updateString(input: string): this {
        this._checkFinalized();
        const bytes = new TextEncoder().encode(input);
        return this.updateBytes(bytes);
    }

    updateBoolean(value: boolean): this {
        this._checkFinalized();
        this._accumulate(value ? 1 : 0);
        return this;
    }

    updateI64(value: bigint): this {
        this._checkFinalized();
        let v = value;
        for (let i = 0; i < 8; i++) {
            this._accumulate(Number(v & 0xffn));
            v >>= 8n;
        }
        return this;
    }

    updateU32(value: number): this {
        this._checkFinalized();
        const v = value >>> 0;
        for (let i = 0; i < 4; i++) this._accumulate((v >>> (i * 8)) & 0xff);
        return this;
    }

    updateHash(value: import('../types').Hash32 | bigint): this {
        this._checkFinalized();
        if (typeof value === 'number') return this.updateU32(value);
        let v = value;
        for (let i = 0; i < 8; i++) {
            this._accumulate(Number(v & 0xffn));
            v >>= 8n;
        }
        return this;
    }

    updateHashable<H2 extends import('../types').HashValue>(value: { hashInto(hasher: IHasher<H2>): void }): this {
        value.hashInto(this as unknown as IHasher<H2>);
        return this;
    }

    updateAny(value: unknown): this {
        if (value === null || value === undefined) { this._accumulate(0); return this; }
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

    digest(): import('../types').Hash64 {
        this._finalized = true;
        let h = this._h;

        if (this._tailLen > 0) {
            h ^= this._tail;
            h = (h * 0xc6a4a7935bd1e995n) & 0xffffffffffffffffn;
        }

        h ^= BigInt(this._totalLen);
        h ^= h >> 47n;
        h = (h * 0xc6a4a7935bd1e995n) & 0xffffffffffffffffn;
        h ^= h >> 47n;
        return asHash64(h);
    }

    digestBytes(): Uint8Array {
        const h = this.digest() as bigint;
        const out = new Uint8Array(8);
        for (let i = 0; i < 8; i++) out[i] = Number((h >> BigInt(i * 8)) & 0xffn);
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
        return encodeBase64(this.digestBytes());
    }

    digestBigInt<H2 extends bigint = bigint>(): H2 {
        return this.digest() as unknown as H2;
    }

    reset(seed: import('../types').Seed32 = asSeed32(0)): this {
        this._initialSeed = BigInt((seed as number) >>> 0);
        this._h = this._initialSeed & 0xffffffffffffffffn;
        this._totalLen = 0;
        this._tail = 0n;
        this._tailLen = 0;
        this._finalized = false;
        return this;
    }

    clone(): IHasher<import('../types').Hash64> {
        const c = new Murmur2_64(this.seed);
        c._h = this._h;
        c._totalLen = this._totalLen;
        c._tail = this._tail;
        c._tailLen = this._tailLen;
        c._finalized = this._finalized;
        return c;
    }
}
