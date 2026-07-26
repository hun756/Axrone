import type { BytesLike } from '../../../types';
import { float32ToBits, float64ToBitsPair } from '../bits';
import { asHash64, asSeed32, type Hash64, type Seed32, type HashAlgorithmMetadata, type HashValue } from '../types';
import type { IHasher } from '../interfaces';

const CYRB_METADATA: HashAlgorithmMetadata = {
    name: 'cyrb53',
    family: 'fast',
    category: 'non-crypto',
    outputSize: 64,
    blockSize: 1,
    seedable: true,
    keyed: false,
    cryptographicallySecure: false,
    description: 'cyrb53 - 53-bit hash by bryc, returns 64-bit pair',
};

export class Cyrb53 implements IHasher<Hash64> {
    readonly algorithm: string = CYRB_METADATA.name;
    readonly metadata: Readonly<HashAlgorithmMetadata> = CYRB_METADATA;
    private _h1: number;
    private _h2: number;
    private _byteLength: number = 0;
    private _finalized: boolean = false;
    private _initialSeed: number;
    private _initialSeed2: number;

    constructor(seed: Seed32 = asSeed32(0), seed2: number = 0) {
        this._initialSeed = (seed as number) >>> 0;
        this._initialSeed2 = seed2 >>> 0;
        this._h1 = (0xdeadbeef ^ this._initialSeed) >>> 0;
        this._h2 = (0x41c6ce57 ^ this._initialSeed2) >>> 0;
    }

    get seed(): Seed32 {
        return asSeed32(this._initialSeed);
    }

    get byteLength(): number {
        return this._byteLength;
    }

    get finalized(): boolean {
        return this._finalized;
    }

    private _checkFinalized(): void {
        if (this._finalized) throw new Error(`Cyrb53: cannot update after digest() (algorithm=${this.algorithm})`);
    }

    updateBytes(bytes: BytesLike, offset: number = 0, length?: number): this {
        this._checkFinalized();
        const end = length === undefined ? bytes.length : offset + length;
        for (let i = offset; i < end; i++) {
            const ch = bytes[i]! & 0xff;
            this._h1 = Math.imul(this._h1 ^ ch, 2654435761) >>> 0;
            this._h2 = Math.imul(this._h2 ^ ch, 1597334677) >>> 0;
        }
        this._byteLength += end - offset;
        return this;
    }

    updateString(input: string): this {
        this._checkFinalized();
        for (let i = 0; i < input.length; i++) {
            const c = input.charCodeAt(i);
            this._h1 = Math.imul(this._h1 ^ (c & 0xff), 2654435761) >>> 0;
            this._h2 = Math.imul(this._h2 ^ (c & 0xff), 1597334677) >>> 0;
            this._h1 = Math.imul(this._h1 ^ ((c >>> 8) & 0xff), 2654435761) >>> 0;
            this._h2 = Math.imul(this._h2 ^ ((c >>> 8) & 0xff), 1597334677) >>> 0;
        }
        this._byteLength += input.length * 2;
        return this;
    }

    updateBoolean(value: boolean): this {
        this._checkFinalized();
        this._h1 = Math.imul(this._h1 ^ (value ? 1 : 0), 2654435761) >>> 0;
        this._h2 = Math.imul(this._h2 ^ (value ? 1 : 0), 1597334677) >>> 0;
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
            const b = Number(v & 0xffn) & 0xff;
            this._h1 = Math.imul(this._h1 ^ b, 2654435761) >>> 0;
            this._h2 = Math.imul(this._h2 ^ b, 1597334677) >>> 0;
            v >>= 8n;
        }
        this._byteLength += 8;
        return this;
    }
    updateU8(v: number): this { return this.updateU32(v & 0xff); }
    updateU16(v: number): this { return this.updateU32(v & 0xffff); }
    updateU32(value: number): this {
        this._checkFinalized();
        const v = value >>> 0;
        for (let i = 0; i < 4; i++) {
            const b = (v >>> (i * 8)) & 0xff;
            this._h1 = Math.imul(this._h1 ^ b, 2654435761) >>> 0;
            this._h2 = Math.imul(this._h2 ^ b, 1597334677) >>> 0;
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
    updateHash(value: import('../types').Hash32 | bigint): this {
        this._checkFinalized();
        if (typeof value === 'number') return this.updateU32(value);
        let v = value;
        for (let i = 0; i < 8; i++) {
            const b = Number(v & 0xffn) & 0xff;
            this._h1 = Math.imul(this._h1 ^ b, 2654435761) >>> 0;
            this._h2 = Math.imul(this._h2 ^ b, 1597334677) >>> 0;
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
        if (value === null || value === undefined) {
            this._h1 = Math.imul(this._h1, 2654435761) >>> 0;
            this._h2 = Math.imul(this._h2, 1597334677) >>> 0;
            return this;
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

    digest(): Hash64 {
        this._finalized = true;
        const f1 =
            (Math.imul(this._h1 ^ (this._h1 >>> 16), 2246822507) ^
                Math.imul(this._h2 ^ (this._h2 >>> 13), 3266489909)) >>>
            0;
        const f2 =
            (Math.imul(this._h2 ^ (this._h2 >>> 16), 2246822507) ^
                Math.imul(this._h1 ^ (this._h1 >>> 13), 3266489909)) >>>
            0;
        return asHash64((BigInt(f2) << 32n) | BigInt(f1));
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
        this._initialSeed = (seed as number) >>> 0;
        this._h1 = (0xdeadbeef ^ this._initialSeed) >>> 0;
        this._h2 = (0x41c6ce57 ^ this._initialSeed2) >>> 0;
        this._byteLength = 0;
        this._finalized = false;
        return this;
    }

    clone(): IHasher<Hash64> {
        const c = new Cyrb53(this.seed, this._initialSeed2);
        c._h1 = this._h1;
        c._h2 = this._h2;
        c._byteLength = this._byteLength;
        c._finalized = this._finalized;
        return c;
    }


}
