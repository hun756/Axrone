import type { BytesLike } from '../../../types';
import { readU32LE, readU64LE, rotl32, writeU32LE, encodeBase64 } from '../bits';
import { u32ToHex, bigIntToHex } from '../hex';
import { asHash32, asSeed32, asHash64, type Hash32, type Hash64, type Seed32, type HashAlgorithmMetadata } from '../types';
import type { IHasher } from '../interfaces';
import { HasherBase } from '../base';
import { HashAlreadyFinalizedError } from '../errors';

const XXH32_METADATA: HashAlgorithmMetadata = {
    name: 'xxhash32',
    family: 'fast',
    category: 'non-crypto',
    outputSize: 32,
    blockSize: 16,
    seedable: true,
    keyed: false,
    cryptographicallySecure: false,
    async: false,
    description: 'xxHash32 - extremely fast non-cryptographic hash (Yann Collet)',
};

const XXH_P1 = 0x9e3779b1;
const XXH_P2 = 0x85ebca77;
const XXH_P3 = 0xc2b2ae3d;
const XXH_P4 = 0x27d4eb2f;
const XXH_P5 = 0x165667b1;

export class XxHash32 extends HasherBase<Hash32> {
    readonly algorithm: string = XXH32_METADATA.name;
    readonly metadata: Readonly<HashAlgorithmMetadata> = XXH32_METADATA;
    private _v1: number;
    private _v2: number;
    private _v3: number;
    private _v4: number;
    private _totalLen: number = 0;
    private _mem: Uint8Array;
    private _memSize: number = 0;
    private _seed: number;

    constructor(seed: Seed32 = asSeed32(0)) {
        super();
        this._seed = (seed as number) >>> 0;
        this._v1 = (this._seed + XXH_P1 + XXH_P2) >>> 0;
        this._v2 = (this._seed + XXH_P2) >>> 0;
        this._v3 = this._seed;
        this._v4 = (this._seed - XXH_P1) >>> 0;
        this._mem = new Uint8Array(16);
    }

    get seed(): Seed32 {
        return asSeed32(this._seed);
    }

    get byteLength(): number {
        return this._totalLen;
    }

    private _checkFinalized(): void {
        if (this._finalized) throw new HashAlreadyFinalizedError(`XxHash32: cannot update after digest() (algorithm=${this.algorithm})`);
    }

    private _round(acc: number, input: number): number {
        acc = (acc + Math.imul(input, XXH_P2)) >>> 0;
        acc = rotl32(acc, 13);
        acc = Math.imul(acc, XXH_P1) >>> 0;
        return acc;
    }

    updateBytes(bytes: BytesLike, offset: number = 0, length?: number): this {
        this._checkFinalized();
        const end = length === undefined ? bytes.length : offset + length;
        const inputLen = end - offset;
        this._totalLen += inputLen;

        if (this._memSize > 0) {
            const remaining = 16 - this._memSize;
            const toCopy = Math.min(inputLen, remaining);
            for (let i = 0; i < toCopy; i++) {
                this._mem[this._memSize + i] = bytes[offset + i]! & 0xff;
            }
            this._memSize += toCopy;
            offset += toCopy;

            if (this._memSize === 16) {
                const v1 = this._round(this._v1, readU32LE(this._mem, 0));
                const v2 = this._round(this._v2, readU32LE(this._mem, 4));
                const v3 = this._round(this._v3, readU32LE(this._mem, 8));
                const v4 = this._round(this._v4, readU32LE(this._mem, 12));
                this._v1 = v1;
                this._v2 = v2;
                this._v3 = v3;
                this._v4 = v4;
                this._memSize = 0;
            }
        }

        const limit = end - offset;
        if (limit >= 16) {
            let v1 = this._v1;
            let v2 = this._v2;
            let v3 = this._v3;
            let v4 = this._v4;
            let pos = offset;
            const blockEnd = offset + (limit - (limit % 16));
            while (pos < blockEnd) {
                v1 = this._round(v1, readU32LE(bytes, pos));
                v2 = this._round(v2, readU32LE(bytes, pos + 4));
                v3 = this._round(v3, readU32LE(bytes, pos + 8));
                v4 = this._round(v4, readU32LE(bytes, pos + 12));
                pos += 16;
            }
            this._v1 = v1;
            this._v2 = v2;
            this._v3 = v3;
            this._v4 = v4;
            offset = pos;
        }

        const leftover = end - offset;
        if (leftover > 0) {
            for (let i = 0; i < leftover; i++) {
                this._mem[i] = bytes[offset + i]! & 0xff;
            }
            this._memSize = leftover;
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
        const b = new Uint8Array(1);
        b[0] = value ? 1 : 0;
        return this.updateBytes(b);
    }

    updateI64(value: bigint): this {
        this._checkFinalized();
        const buf = new Uint8Array(8);
        let v = value;
        for (let i = 0; i < 8; i++) {
            buf[i] = Number(v & 0xffn);
            v >>= 8n;
        }
        return this.updateBytes(buf);
    }

    updateU32(value: number): this {
        this._checkFinalized();
        const buf = new Uint8Array(4);
        writeU32LE(value >>> 0, buf, 0);
        return this.updateBytes(buf);
    }

    updateHash(value: Hash32 | bigint): this {
        this._checkFinalized();
        if (typeof value === 'number') return this.updateU32(value);
        return this.updateI64(value);
    }

    updateHashable<H2 extends import('../types').HashValue>(value: { hashInto(hasher: IHasher<H2>): void }): this {
        value.hashInto(this as unknown as IHasher<H2>);
        return this;
    }

    updateAny(value: unknown): this {
        if (value === null || value === undefined) {
            const b = new Uint8Array(1);
            b[0] = 0;
            return this.updateBytes(b);
        }
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

        let pos = 0;
        while (pos + 4 <= this._memSize) {
            const lane = readU32LE(this._mem, pos);
            h32 = (h32 + Math.imul(lane, XXH_P3)) >>> 0;
            h32 = Math.imul(rotl32(h32, 17), XXH_P4) >>> 0;
            pos += 4;
        }

        while (pos < this._memSize) {
            h32 = (h32 + Math.imul(this._mem[pos]! & 0xff, XXH_P5)) >>> 0;
            h32 = Math.imul(rotl32(h32, 11), XXH_P1) >>> 0;
            pos++;
        }

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
        return u32ToHex(this.digest() as number, uppercase);
    }

    digestBase64(): string {
        return encodeBase64(this.digestBytes());
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
        this._mem = new Uint8Array(16);
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
        c._mem = new Uint8Array(this._mem);
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
    async: false,
    description: 'xxHash64 - 64-bit extremely fast non-cryptographic hash',
};

const MASK64 = 0xffffffffffffffffn;
const XXH64_P1 = 0x9e3779b97f4a7c15n;
const XXH64_P2 = 0xc2b2ae3d27d4eb4fn;
const XXH64_P3 = 0x165667b19e3779f9n;
const XXH64_P4 = 0x85ebca6c2b72e835n;
const XXH64_P5 = 0x27d4eb2f165667c5n;

export class XxHash64 extends HasherBase<Hash64> {
    readonly algorithm: string = XXH64_METADATA.name;
    readonly metadata: Readonly<HashAlgorithmMetadata> = XXH64_METADATA;
    private _v1: bigint;
    private _v2: bigint;
    private _v3: bigint;
    private _v4: bigint;
    private _totalLen: number = 0;
    private _mem: Uint8Array;
    private _memSize: number = 0;
    private _seed: bigint;

    constructor(seed: Seed32 = asSeed32(0)) {
        super();
        this._seed = BigInt((seed as number) >>> 0);
        this._v1 = (this._seed + XXH64_P1 + XXH64_P2) & MASK64;
        this._v2 = (this._seed + XXH64_P2) & MASK64;
        this._v3 = (this._seed + 0n) & MASK64;
        this._v4 = (this._seed - XXH64_P1) & MASK64;
        this._mem = new Uint8Array(32);
    }

    get seed(): Seed32 {
        return asSeed32(Number(this._seed));
    }

    get byteLength(): number {
        return this._totalLen;
    }

    private _checkFinalized(): void {
        if (this._finalized) throw new HashAlreadyFinalizedError(`XxHash64: cannot update after digest() (algorithm=${this.algorithm})`);
    }

    private _round(acc: bigint, input: bigint): bigint {
        acc = (acc + (input * XXH64_P2)) & MASK64;
        acc = ((acc << 31n) | (acc >> 33n)) & MASK64;
        acc = (acc * XXH64_P1) & MASK64;
        return acc;
    }

    private _mergeRound(acc: bigint, val: bigint): bigint {
        val = this._round(0n, val);
        acc = (acc ^ val) & MASK64;
        acc = ((((acc << 27n) | (acc >> 37n)) & MASK64) * XXH64_P1 + XXH64_P4) & MASK64;
        return acc;
    }

    updateBytes(bytes: BytesLike, offset: number = 0, length?: number): this {
        this._checkFinalized();
        const end = length === undefined ? bytes.length : offset + length;
        const inputLen = end - offset;
        this._totalLen += inputLen;

        if (this._memSize > 0) {
            const remaining = 32 - this._memSize;
            const toCopy = Math.min(inputLen, remaining);
            for (let i = 0; i < toCopy; i++) {
                this._mem[this._memSize + i] = bytes[offset + i]! & 0xff;
            }
            this._memSize += toCopy;
            offset += toCopy;

            if (this._memSize === 32) {
                const v1 = this._round(this._v1, readU64LE(this._mem, 0));
                const v2 = this._round(this._v2, readU64LE(this._mem, 8));
                const v3 = this._round(this._v3, readU64LE(this._mem, 16));
                const v4 = this._round(this._v4, readU64LE(this._mem, 24));
                this._v1 = v1;
                this._v2 = v2;
                this._v3 = v3;
                this._v4 = v4;
                this._memSize = 0;
            }
        }

        const limit = end - offset;
        if (limit >= 32) {
            let v1 = this._v1;
            let v2 = this._v2;
            let v3 = this._v3;
            let v4 = this._v4;
            let pos = offset;
            const blockEnd = offset + (limit - (limit % 32));
            while (pos < blockEnd) {
                v1 = this._round(v1, readU64LE(bytes, pos));
                v2 = this._round(v2, readU64LE(bytes, pos + 8));
                v3 = this._round(v3, readU64LE(bytes, pos + 16));
                v4 = this._round(v4, readU64LE(bytes, pos + 24));
                pos += 32;
            }
            this._v1 = v1;
            this._v2 = v2;
            this._v3 = v3;
            this._v4 = v4;
            offset = pos;
        }

        const leftover = end - offset;
        if (leftover > 0) {
            for (let i = 0; i < leftover; i++) {
                this._mem[i] = bytes[offset + i]! & 0xff;
            }
            this._memSize = leftover;
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
        const b = new Uint8Array(1);
        b[0] = value ? 1 : 0;
        return this.updateBytes(b);
    }

    updateI64(value: bigint): this {
        this._checkFinalized();
        const buf = new Uint8Array(8);
        let v = value & MASK64;
        for (let i = 0; i < 8; i++) {
            buf[i] = Number(v & 0xffn);
            v >>= 8n;
        }
        return this.updateBytes(buf);
    }

    updateU32(value: number): this {
        this._checkFinalized();
        const buf = new Uint8Array(4);
        writeU32LE(value >>> 0, buf, 0);
        return this.updateBytes(buf);
    }

    updateHash(value: Hash32 | bigint): this {
        this._checkFinalized();
        if (typeof value === 'number') return this.updateU32(value);
        return this.updateI64(value);
    }

    updateHashable<H2 extends import('../types').HashValue>(value: { hashInto(hasher: IHasher<H2>): void }): this {
        value.hashInto(this as unknown as IHasher<H2>);
        return this;
    }

    updateAny(value: unknown): this {
        if (value === null || value === undefined) {
            const b = new Uint8Array(1);
            b[0] = 0;
            return this.updateBytes(b);
        }
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
        h = (h ^ (h >> 37n)) & MASK64;
        h = (h * XXH64_P4) & MASK64;
        h = (h ^ (h >> 32n)) & MASK64;
        h = (h * XXH64_P3) & MASK64;
        h = (h ^ (h >> 27n)) & MASK64;
        h = (h * XXH64_P5) & MASK64;
        h = (h ^ (h >> 31n)) & MASK64;
        return h;
    }

    private _finalize(): bigint {
        let h64: bigint;
        if (this._totalLen >= 32) {
            h64 = ((this._v1 << 1n) | (this._v1 >> 63n)) & MASK64;
            h64 = (h64 + ((this._v2 << 7n) | (this._v2 >> 57n))) & MASK64;
            h64 = (h64 + ((this._v3 << 12n) | (this._v3 >> 52n))) & MASK64;
            h64 = (h64 + ((this._v4 << 18n) | (this._v4 >> 46n))) & MASK64;
            h64 = this._mergeRound(h64, this._v1);
            h64 = this._mergeRound(h64, this._v2);
            h64 = this._mergeRound(h64, this._v3);
            h64 = this._mergeRound(h64, this._v4);
        } else {
            h64 = (this._seed + XXH64_P5) & MASK64;
        }
        h64 = (h64 + BigInt(this._totalLen)) & MASK64;

        let pos = 0;
        while (pos + 8 <= this._memSize) {
            const lane = readU64LE(this._mem, pos);
            h64 = (h64 ^ this._round(0n, lane)) & MASK64;
            h64 = ((((h64 << 27n) | (h64 >> 37n)) & MASK64) * XXH64_P1 + XXH64_P4) & MASK64;
            pos += 8;
        }

        while (pos < this._memSize) {
            h64 = (h64 ^ (BigInt(this._mem[pos]! & 0xff) * XXH64_P5)) & MASK64;
            h64 = ((((h64 << 11n) | (h64 >> 53n)) & MASK64) * XXH64_P1) & MASK64;
            pos++;
        }

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
        return bigIntToHex(this.digest() as bigint, 16, uppercase);
    }

    digestBase64(): string {
        return encodeBase64(this.digestBytes());
    }

    digestBigInt<H2 extends bigint = bigint>(): H2 {
        return this.digest() as unknown as H2;
    }

    reset(seed: Seed32 = asSeed32(0)): this {
        this._seed = BigInt((seed as number) >>> 0);
        this._v1 = (this._seed + XXH64_P1 + XXH64_P2) & MASK64;
        this._v2 = (this._seed + XXH64_P2) & MASK64;
        this._v3 = (this._seed + 0n) & MASK64;
        this._v4 = (this._seed - XXH64_P1) & MASK64;
        this._totalLen = 0;
        this._memSize = 0;
        this._mem = new Uint8Array(32);
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
        c._mem = new Uint8Array(this._mem);
        c._finalized = this._finalized;
        return c;
    }
}
