import type { BytesLike } from '../../../types';
import { float32ToBits, float64ToBitsPair, rotl32, rotl64 } from '../bits';
import { asHash64, asSeed32, type Hash64, type Seed32, type HashAlgorithmMetadata, type HashValue } from '../types';
import type { IHasher } from '../interfaces';

const SIPHASH_METADATA: HashAlgorithmMetadata = {
    name: 'siphash-2-4',
    family: 'keyed',
    category: 'non-crypto',
    outputSize: 64,
    blockSize: 8,
    seedable: true,
    keyed: true,
    cryptographicallySecure: false,
    description: 'SipHash-2-4 (Jean-Philippe Aumasson, Daniel J. Bernstein) - keyed hash for hash-flooding DoS protection',
};

const SIP_ROUND_CONST_0 = 0x736f6d6570736575n;
const SIP_ROUND_CONST_1 = 0x646f72616e646f6dn;
const SIP_ROUND_CONST_2 = 0x6c7967656e657261n;
const SIP_ROUND_CONST_3 = 0x7465646279746573n;
const SIP_ROUND_KEY_SIZE = 16;

export class SipHash2_4 implements IHasher<Hash64> {
    readonly algorithm: string = SIPHASH_METADATA.name;
    readonly metadata: Readonly<HashAlgorithmMetadata> = SIPHASH_METADATA;
    private _k0: bigint = 0n;
    private _k1: bigint = 0n;
    private _v0: bigint = 0n;
    private _v1: bigint = 0n;
    private _v2: bigint = 0n;
    private _v3: bigint = 0n;
    private _totalLen: number = 0;
    private _tail: bigint = 0n;
    private _tailLen: number = 0;
    private _finalized: boolean = false;
    private _keyBytes: Uint8Array;
    private _byteLength: number = 0;

    constructor(key: BytesLike = new Uint8Array(SIP_ROUND_KEY_SIZE), seed?: Seed32) {
        const k = new Uint8Array(SIP_ROUND_KEY_SIZE);
        const inKey = key instanceof Uint8Array ? key : Uint8Array.from(key as ArrayLike<number>);
        for (let i = 0; i < Math.min(SIP_ROUND_KEY_SIZE, inKey.length); i++) k[i] = inKey[i]!;
        if (seed !== undefined) {
            const seedBytes = new Uint8Array(8);
            const sv = (seed as number) >>> 0;
            seedBytes[0] = sv & 0xff;
            seedBytes[1] = (sv >>> 8) & 0xff;
            seedBytes[2] = (sv >>> 16) & 0xff;
            seedBytes[3] = (sv >>> 24) & 0xff;
            for (let i = 0; i < 8; i++) k[i] ^= seedBytes[i]!;
        }
        this._keyBytes = k;
        this._k0 = BigInt(k[0]!) | (BigInt(k[1]!) << 8n) | (BigInt(k[2]!) << 16n) | (BigInt(k[3]!) << 24n) | (BigInt(k[4]!) << 32n) | (BigInt(k[5]!) << 40n) | (BigInt(k[6]!) << 48n) | (BigInt(k[7]!) << 56n);
        this._k1 = BigInt(k[8]!) | (BigInt(k[9]!) << 8n) | (BigInt(k[10]!) << 16n) | (BigInt(k[11]!) << 24n) | (BigInt(k[12]!) << 32n) | (BigInt(k[13]!) << 40n) | (BigInt(k[14]!) << 48n) | (BigInt(k[15]!) << 56n);
        this.reset();
    }

    get seed(): Seed32 {
        return asSeed32(0);
    }

    get byteLength(): number {
        return this._byteLength;
    }

    get finalized(): boolean {
        return this._finalized;
    }

    private _checkFinalized(): void {
        if (this._finalized) throw new Error(`SipHash2_4: cannot update after digest() (algorithm=${this.algorithm})`);
    }

    private _sipround(): void {
        this._v0 = (this._v0 + this._v1) & 0xffffffffffffffffn;
        this._v1 = rotl64(this._v1, 13n);
        this._v1 = (this._v1 ^ this._v0) & 0xffffffffffffffffn;
        this._v0 = rotl64(this._v0, 32n);
        this._v2 = (this._v2 + this._v3) & 0xffffffffffffffffn;
        this._v3 = rotl64(this._v3, 16n);
        this._v3 = (this._v3 ^ this._v2) & 0xffffffffffffffffn;
        this._v0 = (this._v0 + this._v3) & 0xffffffffffffffffn;
        this._v3 = rotl64(this._v3, 21n);
        this._v3 = (this._v3 ^ this._v0) & 0xffffffffffffffffn;
        this._v2 = (this._v2 + this._v1) & 0xffffffffffffffffn;
        this._v1 = rotl64(this._v1, 17n);
        this._v1 = (this._v1 ^ this._v2) & 0xffffffffffffffffn;
        this._v2 = rotl64(this._v2, 32n);
    }

    private _accumulate(byte: number): void {
        this._tail = (this._tail | (BigInt(byte & 0xff) << BigInt(this._tailLen * 8))) & 0xffffffffffffffffn;
        this._tailLen++;
        this._byteLength++;
        if (this._tailLen === 8) {
            this._v3 ^= this._tail;
            this._sipround();
            this._sipround();
            this._v0 ^= this._tail;
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
        for (let i = 0; i < input.length; i++) {
            const c = input.charCodeAt(i);
            this._accumulate(c & 0xff);
            this._accumulate((c >>> 8) & 0xff);
        }
        return this;
    }

    updateBoolean(value: boolean): this {
        this._checkFinalized();
        this._accumulate(value ? 1 : 0);
        return this;
    }

    updateI8(v: number): this { return this.updateI32(v | 0); }
    updateI16(v: number): this { return this.updateI32(v | 0); }
    updateI32(value: number): this { return this.updateU32(value | 0); }
    updateI64(value: bigint): this {
        this._checkFinalized();
        let v = value & 0xffffffffffffffffn;
        for (let i = 0; i < 8; i++) {
            this._accumulate(Number(v & 0xffn));
            v >>= 8n;
        }
        return this;
    }
    updateU8(v: number): this { return this.updateU32(v & 0xff); }
    updateU16(v: number): this { return this.updateU32(v & 0xffff); }
    updateU32(value: number): this {
        this._checkFinalized();
        const v = value >>> 0;
        for (let i = 0; i < 4; i++) this._accumulate((v >>> (i * 8)) & 0xff);
        return this;
    }
    updateU64(value: bigint): this { return this.updateI64(value); }
    updateF32(value: number): this { return this.updateU32(float32ToBits(value)); }
    updateF64(value: number): this {
        const [lo, hi] = float64ToBitsPair(value);
        return this.updateU32(lo).updateU32(hi);
    }
    updateHash(value: import('../types').Hash32 | bigint): this {
        this._checkFinalized();
        if (typeof value === 'number') return this.updateU32(value);
        let v = value & 0xffffffffffffffffn;
        for (let i = 0; i < 8; i++) {
            this._accumulate(Number(v & 0xffn));
            v >>= 8n;
        }
        return this;
    }
    updateHashable<H2 extends HashValue>(value: { hashInto(hasher: IHasher<H2>): void }): this {
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

    digest(): Hash64 {
        this._checkFinalized();
        this._finalized = true;
        const b = ((BigInt(this._tailLen) & 0xffn) << 56n) | this._tail;
        this._v3 ^= b;
        this._sipround();
        this._sipround();
        this._v0 ^= b;

        this._v2 ^= 0xffn;
        this._sipround();
        this._sipround();
        this._sipround();
        this._sipround();

        return asHash64((this._v0 ^ this._v1 ^ this._v2 ^ this._v3) & 0xffffffffffffffffn);
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

    reset(seed?: Seed32): this {
        if (seed !== undefined) {
            const seedBytes = new Uint8Array(8);
            const sv = (seed as number) >>> 0;
            seedBytes[0] = sv & 0xff;
            seedBytes[1] = (sv >>> 8) & 0xff;
            seedBytes[2] = (sv >>> 16) & 0xff;
            seedBytes[3] = (sv >>> 24) & 0xff;
            const k = new Uint8Array(SIP_ROUND_KEY_SIZE);
            for (let i = 0; i < SIP_ROUND_KEY_SIZE; i++) k[i] = this._keyBytes[i]! ^ (i < 8 ? seedBytes[i]! : 0);
            this._k0 = BigInt(k[0]!) | (BigInt(k[1]!) << 8n) | (BigInt(k[2]!) << 16n) | (BigInt(k[3]!) << 24n) | (BigInt(k[4]!) << 32n) | (BigInt(k[5]!) << 40n) | (BigInt(k[6]!) << 48n) | (BigInt(k[7]!) << 56n);
            this._k1 = BigInt(k[8]!) | (BigInt(k[9]!) << 8n) | (BigInt(k[10]!) << 16n) | (BigInt(k[11]!) << 24n) | (BigInt(k[12]!) << 32n) | (BigInt(k[13]!) << 40n) | (BigInt(k[14]!) << 48n) | (BigInt(k[15]!) << 56n);
        }
        this._v0 = (this._k0 ^ SIP_ROUND_CONST_0) & 0xffffffffffffffffn;
        this._v1 = (this._k1 ^ SIP_ROUND_CONST_1) & 0xffffffffffffffffn;
        this._v2 = (this._k0 ^ SIP_ROUND_CONST_2) & 0xffffffffffffffffn;
        this._v3 = (this._k1 ^ SIP_ROUND_CONST_3) & 0xffffffffffffffffn;
        this._totalLen = 0;
        this._tail = 0n;
        this._tailLen = 0;
        this._byteLength = 0;
        this._finalized = false;
        return this;
    }

    clone(): IHasher<Hash64> {
        const c = new SipHash2_4(this._keyBytes);
        c._k0 = this._k0;
        c._k1 = this._k1;
        c._v0 = this._v0;
        c._v1 = this._v1;
        c._v2 = this._v2;
        c._v3 = this._v3;
        c._totalLen = this._totalLen;
        c._tail = this._tail;
        c._tailLen = this._tailLen;
        c._byteLength = this._byteLength;
        c._finalized = this._finalized;
        return c;
    }


}
