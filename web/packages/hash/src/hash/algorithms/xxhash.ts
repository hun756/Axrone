import type { BytesLike } from '../../../types';
import { float32ToBits, float64ToBitsPair, readU32LE, rotl32, writeU32LE } from '../bits';
import { fmix32Alt } from '../mixers';
import { asHash32, asSeed32, asHash64, type Hash32, type Hash64, type Seed32, type HashAlgorithmMetadata, type HashValue } from '../types';
import type { IHasher } from '../interfaces';

const XXH32_METADATA: HashAlgorithmMetadata = {
    name: 'xxhash32',
    family: 'fast',
    category: 'non-crypto',
    outputSize: 32,
    blockSize: 16,
    seedable: true,
    keyed: false,
    cryptographicallySecure: false,
    description: 'xxHash32 - extremely fast non-cryptographic hash (Yann Collet)',
};

const XXH_P1 = 0x9e3779b1;
const XXH_P2 = 0x85ebca77;
const XXH_P3 = 0xc2b2ae3d;
const XXH_P4 = 0x27d4eb2f;
const XXH_P5 = 0x165667b1;

export class XxHash32 implements IHasher<Hash32> {
    readonly algorithm: string = XXH32_METADATA.name;
    readonly metadata: Readonly<HashAlgorithmMetadata> = XXH32_METADATA;
    private _v1: number;
    private _v2: number;
    private _v3: number;
    private _v4: number;
    private _totalLen: number = 0;
    private _mem: Uint32Array;
    private _memSize: number = 0;
    private _seed: number;
    private _finalized: boolean = false;

    constructor(seed: Seed32 = asSeed32(0)) {
        this._seed = (seed as number) >>> 0;
        this._v1 = (this._seed + XXH_P1 + XXH_P2) >>> 0;
        this._v2 = (this._seed + XXH_P2) >>> 0;
        this._v3 = this._seed;
        this._v4 = (this._seed - XXH_P1) >>> 0;
        this._mem = new Uint32Array(4);
    }

    get seed(): Seed32 {
        return asSeed32(this._seed);
    }

    get byteLength(): number {
        return this._totalLen;
    }

    get finalized(): boolean {
        return this._finalized;
    }

    private _checkFinalized(): void {
        if (this._finalized) throw new Error(`XxHash32: cannot update after digest() (algorithm=${this.algorithm})`);
    }

    private _round(acc: number, input: number): number {
        acc = (acc + Math.imul(input, XXH_P2)) >>> 0;
        acc = rotl32(acc, 13);
        acc = Math.imul(acc, XXH_P1) >>> 0;
        return acc;
    }

    private _consume(input: number): void {
        switch (this._memSize) {
            case 0:
                this._mem[0] = input;
                break;
            case 1:
                this._mem[1] = input;
                break;
            case 2:
                this._mem[2] = input;
                break;
            case 3:
                this._mem[3] = input;
                this._v1 = this._round(this._v1, this._mem[0]!);
                this._v2 = this._round(this._v2, this._mem[1]!);
                this._v3 = this._round(this._v3, this._mem[2]!);
                this._v4 = this._round(this._v4, this._mem[3]!);
                break;
        }
        this._memSize = (this._memSize + 1) & 3;
    }

    updateBytes(bytes: BytesLike, offset: number = 0, length?: number): this {
        this._checkFinalized();
        const end = length === undefined ? bytes.length : offset + length;
        for (let i = offset; i < end; i++) {
            this._consume(bytes[i]! & 0xff);
        }
        this._totalLen += end - offset;
        return this;
    }

    updateString(input: string): this {
        this._checkFinalized();
        for (let i = 0; i < input.length; i++) {
            const c = input.charCodeAt(i);
            this._consume(c & 0xff);
            this._consume((c >>> 8) & 0xff);
        }
        this._totalLen += input.length * 2;
        return this;
    }

    updateBoolean(value: boolean): this {
        this._checkFinalized();
        this._consume(value ? 1 : 0);
        this._totalLen += 1;
        return this;
    }

    updateI8(v: number): this { return this.updateI32(v | 0); }
    updateI16(v: number): this { return this.updateI32(v | 0); }
    updateI32(value: number): this { return this.updateU32(value | 0); }
    updateI64(value: bigint): this {
        this._checkFinalized();
        let v = value;
        for (let i = 0; i < 8; i++) {
            this._consume(Number(v & 0xffn));
            v >>= 8n;
        }
        this._totalLen += 8;
        return this;
    }
    updateU8(v: number): this { return this.updateU32(v & 0xff); }
    updateU16(v: number): this { return this.updateU32(v & 0xffff); }
    updateU32(value: number): this {
        this._checkFinalized();
        const v = value >>> 0;
        for (let i = 0; i < 4; i++) this._consume((v >>> (i * 8)) & 0xff);
        this._totalLen += 4;
        return this;
    }
    updateU64(value: bigint): this { return this.updateI64(value); }
    updateF32(value: number): this { return this.updateU32(float32ToBits(value)); }
    updateF64(value: number): this {
        const [lo, hi] = float64ToBitsPair(value);
        return this.updateU32(lo).updateU32(hi);
    }
    updateHash(value: Hash32 | bigint): this {
        this._checkFinalized();
        if (typeof value === 'number') return this.updateU32(value);
        let v = value;
        for (let i = 0; i < 8; i++) {
            this._consume(Number(v & 0xffn));
            v >>= 8n;
        }
        this._totalLen += 8;
        return this;
    }
    updateHashable<H2 extends HashValue>(value: { hashInto(hasher: IHasher<H2>): void }): this {
        value.hashInto(this as unknown as IHasher<H2>);
        return this;
    }
    updateAny(value: unknown): this {
        if (value === null || value === undefined) { this._consume(0); this._totalLen++; return this; }
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

    private _avalanche(h: number): number {
        h = (h ^ (h >>> 15)) >>> 0;
        h = Math.imul(h, XXH_P2) >>> 0;
        h = (h ^ (h >>> 13)) >>> 0;
        h = Math.imul(h, XXH_P3) >>> 0;
        h = (h ^ (h >>> 16)) >>> 0;
        return h >>> 0;
    }

    private _finalize(): number {
        let h32: number;
        if (this._totalLen >= 16) {
            h32 = (rotl32(this._v1, 1) + rotl32(this._v2, 7) + rotl32(this._v3, 12) + rotl32(this._v4, 18)) >>> 0;
        } else {
            h32 = (this._seed + XXH_P5) >>> 0;
        }
        h32 = (h32 + this._totalLen) >>> 0;

        if (this._memSize >= 1) h32 = (h32 + Math.imul(this._mem[0]!, XXH_P3)) >>> 0;
        if (this._memSize >= 2) h32 = ((rotl32(h32, 17) * XXH_P4) ^ Math.imul(this._mem[1]!, XXH_P3)) >>> 0;
        if (this._memSize >= 3) h32 = ((rotl32(h32, 15) * XXH_P2) ^ Math.imul(this._mem[2]!, XXH_P4)) >>> 0;

        return this._avalanche(h32);
    }

    digest(): Hash32 {
        this._finalized = true;
        return asHash32(this._finalize());
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
        const bytes = this.digestBytes();
        let binary = '';
        for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]!);
        return typeof btoa !== 'undefined' ? btoa(binary) : Buffer.from(binary, 'binary').toString('base64');
    }

    digestBigInt<H2 extends bigint = bigint>(): H2 {
        return BigInt(this.digest() as number) as H2;
    }

    reset(seed: Seed32 = asSeed32(0)): this {
        this._seed = (seed as number) >>> 0;
        this._v1 = (this._seed + XXH_P1 + XXH_P2) >>> 0;
        this._v2 = (this._seed + XXH_P2) >>> 0;
        this._v3 = this._seed;
        this._v4 = (this._seed - XXH_P1) >>> 0;
        this._totalLen = 0;
        this._memSize = 0;
        this._mem = new Uint32Array(4);
        this._finalized = false;
        return this;
    }

    clone(): IHasher<Hash32> {
        const c = new XxHash32(this.seed);
        c._v1 = this._v1;
        c._v2 = this._v2;
        c._v3 = this._v3;
        c._v4 = this._v4;
        c._totalLen = this._totalLen;
        c._memSize = this._memSize;
        c._mem = new Uint32Array(this._mem);
        c._finalized = this._finalized;
        return c;
    }


}

const XXH64_METADATA: HashAlgorithmMetadata = {
    name: 'xxhash64',
    family: 'fast',
    category: 'non-crypto',
    outputSize: 64,
    blockSize: 32,
    seedable: true,
    keyed: false,
    cryptographicallySecure: false,
    description: 'xxHash64 - 64-bit extremely fast non-cryptographic hash',
};

const XXH64_P1 = 0x9e3779b97f4a7c15n;
const XXH64_P2 = 0xc2b2ae3d27d4eb4fn;
const XXH64_P3 = 0x165667b19e3779f9n;
const XXH64_P4 = 0x85ebca6c2b72e835n;
const XXH64_P5 = 0x27d4eb2f165667c5n;

export class XxHash64 implements IHasher<Hash64> {
    readonly algorithm: string = XXH64_METADATA.name;
    readonly metadata: Readonly<HashAlgorithmMetadata> = XXH64_METADATA;
    private _v1: bigint;
    private _v2: bigint;
    private _v3: bigint;
    private _v4: bigint;
    private _totalLen: number = 0;
    private _mem: BigInt64Array;
    private _memSize: number = 0;
    private _seed: bigint;
    private _finalized: boolean = false;

    constructor(seed: Seed32 = asSeed32(0)) {
        this._seed = BigInt((seed as number) >>> 0);
        this._v1 = (this._seed + XXH64_P1 + XXH64_P2) & 0xffffffffffffffffn;
        this._v2 = (this._seed + XXH64_P2) & 0xffffffffffffffffn;
        this._v3 = (this._seed + 0n) & 0xffffffffffffffffn;
        this._v4 = (this._seed - XXH64_P1) & 0xffffffffffffffffn;
        this._mem = new BigInt64Array(4);
    }

    get seed(): Seed32 {
        return asSeed32(Number(this._seed));
    }

    get byteLength(): number {
        return this._totalLen;
    }

    get finalized(): boolean {
        return this._finalized;
    }

    private _checkFinalized(): void {
        if (this._finalized) throw new Error(`XxHash64: cannot update after digest() (algorithm=${this.algorithm})`);
    }

    private _round(acc: bigint, input: bigint): bigint {
        acc = (acc + (input * XXH64_P2)) & 0xffffffffffffffffn;
        acc = ((acc << 31n) | (acc >> 33n)) & 0xffffffffffffffffn;
        acc = (acc * XXH64_P1) & 0xffffffffffffffffn;
        return acc;
    }

    private _mergeRound(acc: bigint, val: bigint): bigint {
        val = this._round(0n, val);
        acc = (acc ^ val) & 0xffffffffffffffffn;
        acc = ((acc << 27n) | (acc >> 37n)) * XXH64_P1 + XXH64_P4 & 0xffffffffffffffffn;
        return acc & 0xffffffffffffffffn;
    }

    private _consume(input: bigint): void {
        switch (this._memSize) {
            case 0:
                this._mem[0] = input;
                break;
            case 1:
                this._mem[1] = input;
                break;
            case 2:
                this._mem[2] = input;
                break;
            case 3:
                this._mem[3] = input;
                this._v1 = this._round(this._v1, this._mem[0]!);
                this._v2 = this._round(this._v2, this._mem[1]!);
                this._v3 = this._round(this._v3, this._mem[2]!);
                this._v4 = this._round(this._v4, this._mem[3]!);
                break;
        }
        this._memSize = (this._memSize + 1) & 3;
    }

    updateBytes(bytes: BytesLike, offset: number = 0, length?: number): this {
        this._checkFinalized();
        const end = length === undefined ? bytes.length : offset + length;
        for (let i = offset; i < end; i++) {
            this._consume(BigInt(bytes[i]! & 0xff));
        }
        this._totalLen += end - offset;
        return this;
    }

    updateString(input: string): this {
        this._checkFinalized();
        for (let i = 0; i < input.length; i++) {
            const c = BigInt(input.charCodeAt(i));
            this._consume(c & 0xffn);
            this._consume(c >> 8n);
        }
        this._totalLen += input.length * 2;
        return this;
    }

    updateBoolean(value: boolean): this {
        this._checkFinalized();
        this._consume(value ? 1n : 0n);
        this._totalLen += 1;
        return this;
    }

    updateI8(v: number): this { return this.updateI32(v | 0); }
    updateI16(v: number): this { return this.updateI32(v | 0); }
    updateI32(value: number): this { return this.updateU32(value | 0); }
    updateI64(value: bigint): this {
        this._checkFinalized();
        let v = value & 0xffffffffffffffffn;
        for (let i = 0; i < 8; i++) {
            this._consume(v & 0xffn);
            v >>= 8n;
        }
        this._totalLen += 8;
        return this;
    }
    updateU8(v: number): this { return this.updateU32(v & 0xff); }
    updateU16(v: number): this { return this.updateU32(v & 0xffff); }
    updateU32(value: number): this {
        this._checkFinalized();
        const v = BigInt(value >>> 0);
        for (let i = 0; i < 4; i++) this._consume((v >> BigInt(i * 8)) & 0xffn);
        this._totalLen += 4;
        return this;
    }
    updateU64(value: bigint): this { return this.updateI64(value); }
    updateF32(value: number): this { return this.updateU32(float32ToBits(value)); }
    updateF64(value: number): this {
        const [lo, hi] = float64ToBitsPair(value);
        return this.updateU32(lo).updateU32(hi);
    }
    updateHash(value: Hash32 | bigint): this {
        this._checkFinalized();
        if (typeof value === 'number') return this.updateU32(value);
        let v = value & 0xffffffffffffffffn;
        for (let i = 0; i < 8; i++) {
            this._consume(v & 0xffn);
            v >>= 8n;
        }
        this._totalLen += 8;
        return this;
    }
    updateHashable<H2 extends HashValue>(value: { hashInto(hasher: IHasher<H2>): void }): this {
        value.hashInto(this as unknown as IHasher<H2>);
        return this;
    }
    updateAny(value: unknown): this {
        if (value === null || value === undefined) { this._consume(0n); this._totalLen++; return this; }
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

    private _avalanche(h: bigint): bigint {
        h = (h ^ (h >> 37n)) & 0xffffffffffffffffn;
        h = (h * XXH64_P4) & 0xffffffffffffffffn;
        h = (h ^ (h >> 32n)) & 0xffffffffffffffffn;
        h = (h * XXH64_P3) & 0xffffffffffffffffn;
        h = (h ^ (h >> 27n)) & 0xffffffffffffffffn;
        h = (h * XXH64_P5) & 0xffffffffffffffffn;
        h = (h ^ (h >> 31n)) & 0xffffffffffffffffn;
        return h;
    }

    private _finalize(): bigint {
        let h64: bigint;
        if (this._totalLen >= 32) {
            h64 = ((this._v1 << 1n) | (this._v1 >> 63n)) & 0xffffffffffffffffn;
            h64 = (h64 + ((this._v2 << 7n) | (this._v2 >> 57n))) & 0xffffffffffffffffn;
            h64 = (h64 + ((this._v3 << 12n) | (this._v3 >> 52n))) & 0xffffffffffffffffn;
            h64 = (h64 + ((this._v4 << 18n) | (this._v4 >> 46n))) & 0xffffffffffffffffn;
            h64 = this._mergeRound(h64, this._v1);
            h64 = this._mergeRound(h64, this._v2);
            h64 = this._mergeRound(h64, this._v3);
            h64 = this._mergeRound(h64, this._v4);
        } else {
            h64 = (this._seed + XXH64_P5) & 0xffffffffffffffffn;
        }
        h64 = (h64 + BigInt(this._totalLen)) & 0xffffffffffffffffn;

        if (this._memSize >= 1) h64 = (h64 ^ ((this._mem[0]! & 0xffn) * XXH64_P5)) & 0xffffffffffffffffn;
        if (this._memSize >= 2) h64 = (h64 ^ (((this._mem[1]! & 0xffn) << 8n) * XXH64_P5)) & 0xffffffffffffffffn;
        if (this._memSize >= 3) h64 = (h64 ^ (((this._mem[2]! & 0xffn) << 16n) * XXH64_P5)) & 0xffffffffffffffffn;
        if (this._memSize >= 4) h64 = (h64 ^ (((this._mem[3]! & 0xffn) << 24n) * XXH64_P5)) & 0xffffffffffffffffn;

        return this._avalanche(h64);
    }

    digest(): Hash64 {
        this._finalized = true;
        return asHash64(this._finalize());
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
        const bytes = this.digestBytes();
        let binary = '';
        for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]!);
        return typeof btoa !== 'undefined' ? btoa(binary) : Buffer.from(binary, 'binary').toString('base64');
    }

    digestBigInt<H2 extends bigint = bigint>(): H2 {
        return this.digest() as unknown as H2;
    }

    reset(seed: Seed32 = asSeed32(0)): this {
        this._seed = BigInt((seed as number) >>> 0);
        this._v1 = (this._seed + XXH64_P1 + XXH64_P2) & 0xffffffffffffffffn;
        this._v2 = (this._seed + XXH64_P2) & 0xffffffffffffffffn;
        this._v3 = (this._seed + 0n) & 0xffffffffffffffffn;
        this._v4 = (this._seed - XXH64_P1) & 0xffffffffffffffffn;
        this._totalLen = 0;
        this._memSize = 0;
        this._mem = new BigInt64Array(4);
        this._finalized = false;
        return this;
    }

    clone(): IHasher<Hash64> {
        const c = new XxHash64(this.seed);
        c._v1 = this._v1;
        c._v2 = this._v2;
        c._v3 = this._v3;
        c._v4 = this._v4;
        c._totalLen = this._totalLen;
        c._memSize = this._memSize;
        c._mem = new BigInt64Array(this._mem);
        c._finalized = this._finalized;
        return c;
    }


}
