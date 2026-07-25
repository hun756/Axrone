import type { BytesLike } from '../../../../types';
import { asHash256, asSeed32, type Hash256, type Hash512, type Hash128, type Seed32, type HashAlgorithmMetadata } from '../../types';
import type { IHasher } from '../../interfaces';
import { HashCryptoUnavailableError, HashCryptoOperationError } from '../../errors';

const _HEX = '0123456789abcdef';

function toHex(bytes: Uint8Array, uppercase: boolean = false): string {
    let s = '';
    for (let i = 0; i < bytes.length; i++) {
        const b = bytes[i]!;
        s += _HEX[(b >>> 4) & 0xf] + _HEX[b & 0xf];
    }
    return uppercase ? s.toUpperCase() : s;
}

function bytesToBigInt(bytes: Uint8Array): bigint {
    let v = 0n;
    for (let i = 0; i < bytes.length; i++) v = (v << 8n) | BigInt(bytes[i]!);
    return v;
}

export function isWebCryptoAvailable(): boolean {
    return typeof globalThis !== 'undefined' &&
        typeof (globalThis as { crypto?: Crypto }).crypto !== 'undefined' &&
        typeof (globalThis as { crypto?: Crypto }).crypto?.subtle !== 'undefined';
}

async function subtleDigest(algorithm: string, data: Uint8Array): Promise<Uint8Array> {
    if (!isWebCryptoAvailable()) {
        throw new HashCryptoUnavailableError(`WebCrypto is not available in this environment`);
    }
    try {
        const subtle = (globalThis as { crypto: Crypto }).crypto.subtle;
        const buf = await subtle.digest(algorithm, data as unknown as ArrayBuffer);
        return new Uint8Array(buf);
    } catch (e) {
        throw new HashCryptoOperationError(`WebCrypto ${algorithm} failed: ${(e as Error).message}`);
    }
}

const SHA1_METADATA: HashAlgorithmMetadata = {
    name: 'sha-1',
    family: 'cryptographic',
    category: 'crypto',
    outputSize: 160,
    blockSize: 64,
    seedable: false,
    keyed: false,
    cryptographicallySecure: false,
    description: 'SHA-1 (deprecated, collision attacks known; use for compatibility only)',
};

const SHA256_METADATA: HashAlgorithmMetadata = {
    name: 'sha-256',
    family: 'cryptographic',
    category: 'crypto',
    outputSize: 256,
    blockSize: 64,
    seedable: false,
    keyed: false,
    cryptographicallySecure: true,
    description: 'SHA-256 (FIPS 180-4)',
};

const SHA384_METADATA: HashAlgorithmMetadata = {
    name: 'sha-384',
    family: 'cryptographic',
    category: 'crypto',
    outputSize: 384,
    blockSize: 128,
    seedable: false,
    keyed: false,
    cryptographicallySecure: true,
    description: 'SHA-384 (FIPS 180-4)',
};

const SHA512_METADATA: HashAlgorithmMetadata = {
    name: 'sha-512',
    family: 'cryptographic',
    category: 'crypto',
    outputSize: 512,
    blockSize: 128,
    seedable: false,
    keyed: false,
    cryptographicallySecure: true,
    description: 'SHA-512 (FIPS 180-4)',
};

abstract class WebCryptoHasher<H extends Hash256 | Hash512 | Hash128> implements IHasher<H> {
    abstract readonly algorithm: string;
    abstract readonly metadata: Readonly<HashAlgorithmMetadata>;
    protected _chunks: Uint8Array[] = [];
    protected _totalLen: number = 0;
    protected _finalized: boolean = false;
    protected _cachedResult: Uint8Array | undefined;

    get seed(): Seed32 | undefined {
        return undefined;
    }

    get byteLength(): number {
        return this._totalLen;
    }

    get finalized(): boolean {
        return this._finalized;
    }

    protected abstract getSubtleName(): string;
    protected abstract getOutputSize(): number;
    protected abstract convertResult(bytes: Uint8Array): H;

    private _checkFinalized(): void {
        if (this._finalized) throw new Error(`${this.algorithm}: cannot update after digest() (algorithm=${this.algorithm})`);
    }

    updateBytes(bytes: BytesLike, offset: number = 0, length?: number): this {
        this._checkFinalized();
        const end = length === undefined ? bytes.length : offset + length;
        if (offset === 0 && length === undefined && (bytes instanceof Uint8Array)) {
            this._chunks.push(bytes as Uint8Array);
        } else if (bytes instanceof Uint8Array) {
            this._chunks.push((bytes as Uint8Array).slice(offset, end));
        } else {
            this._chunks.push(Uint8Array.from(Array.from(bytes as ArrayLike<number>).slice(offset, end)));
        }
        this._totalLen += end - offset;
        return this;
    }

    updateString(input: string): this {
        this._checkFinalized();
        const enc = new TextEncoder();
        this._chunks.push(enc.encode(input));
        this._totalLen += input.length * 2;
        return this;
    }

    updateBoolean(value: boolean): this {
        this._checkFinalized();
        this._chunks.push(new Uint8Array([value ? 1 : 0]));
        this._totalLen += 1;
        return this;
    }

    updateI8(v: number): this { return this.updateI32(v | 0); }
    updateI16(v: number): this { return this.updateI32(v | 0); }
    updateI32(value: number): this { return this.updateU32(value | 0); }
    updateI64(value: bigint): this {
        this._checkFinalized();
        const arr = new Uint8Array(8);
        const view = new DataView(arr.buffer);
        view.setBigInt64(0, value, true);
        this._chunks.push(arr);
        this._totalLen += 8;
        return this;
    }
    updateU8(v: number): this { return this.updateU32(v & 0xff); }
    updateU16(v: number): this { return this.updateU32(v & 0xffff); }
    updateU32(value: number): this {
        this._checkFinalized();
        const arr = new Uint8Array(4);
        const view = new DataView(arr.buffer);
        view.setUint32(0, value >>> 0, true);
        this._chunks.push(arr);
        this._totalLen += 4;
        return this;
    }
    updateU64(value: bigint): this {
        this._checkFinalized();
        const arr = new Uint8Array(8);
        const view = new DataView(arr.buffer);
        view.setBigUint64(0, value, true);
        this._chunks.push(arr);
        this._totalLen += 8;
        return this;
    }
    updateF32(value: number): this {
        this._checkFinalized();
        const arr = new Uint8Array(4);
        new DataView(arr.buffer).setFloat32(0, value, true);
        this._chunks.push(arr);
        this._totalLen += 4;
        return this;
    }
    updateF64(value: number): this {
        this._checkFinalized();
        const arr = new Uint8Array(8);
        new DataView(arr.buffer).setFloat64(0, value, true);
        this._chunks.push(arr);
        this._totalLen += 8;
        return this;
    }
    updateHash(value: import('../../types').Hash32 | bigint): this {
        this._checkFinalized();
        if (typeof value === 'number') return this.updateU32(value);
        let v = value;
        for (let i = 0; i < 8; i++) {
            this._chunks.push(new Uint8Array([Number(v & 0xffn)]));
            v >>= 8n;
        }
        this._totalLen += 8;
        return this;
    }
    updateHashable<H2 extends import('../../types').HashValue>(value: { hashInto(hasher: IHasher<H2>): void }): this {
        value.hashInto(this as unknown as IHasher<H2>);
        return this;
    }
    updateAny(value: unknown): this {
        if (value === null || value === undefined) { this._chunks.push(new Uint8Array([0])); this._totalLen++; return this; }
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

    async digestAsync(): Promise<H> {
        if (this._cachedResult) return this.convertResult(this._cachedResult);
        this._finalized = true;
        const total = this._chunks.reduce((s, c) => s + c.length, 0);
        const merged = new Uint8Array(total);
        let p = 0;
        for (const c of this._chunks) {
            merged.set(c, p);
            p += c.length;
        }
        this._cachedResult = await subtleDigest(this.getSubtleName(), merged);
        return this.convertResult(this._cachedResult);
    }

    digest(): H {
        throw new Error(`${this.algorithm}: WebCrypto digests are asynchronous. Use digestAsync() instead.`);
    }

    digestBytes(): Uint8Array {
        if (!this._cachedResult) {
            throw new Error(`${this.algorithm}: digestAsync() must be awaited before digestBytes()`);
        }
        return this._cachedResult;
    }

    digestHex(uppercase: boolean = false): string {
        return toHex(this.digestBytes(), uppercase);
    }

    digestBase64(): string {
        const bytes = this.digestBytes();
        let binary = '';
        for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]!);
        return typeof btoa !== 'undefined' ? btoa(binary) : Buffer.from(binary, 'binary').toString('base64');
    }

    digestBigInt<H2 extends bigint = bigint>(): H2 {
        return bytesToBigInt(this.digestBytes()) as H2;
    }

    reset(seed?: Seed32): this {
        this._chunks = [];
        this._totalLen = 0;
        this._finalized = false;
        this._cachedResult = undefined;
        return this;
    }

    clone(): IHasher<H> {
        const Ctor = (this as any).constructor;
        const c = new Ctor();
        for (const chunk of this._chunks) c._chunks.push(new Uint8Array(chunk));
        c._totalLen = this._totalLen;
        c._finalized = this._finalized;
        if (this._cachedResult) c._cachedResult = new Uint8Array(this._cachedResult);
        return c;
    }


}

export class Sha1 extends WebCryptoHasher<Hash256> {
    readonly algorithm: string = SHA1_METADATA.name;
    readonly metadata: Readonly<HashAlgorithmMetadata> = SHA1_METADATA;
    protected getSubtleName(): string { return 'SHA-1'; }
    protected getOutputSize(): number { return 20; }
    protected convertResult(bytes: Uint8Array): Hash256 {
        const padded = new Uint8Array(32);
        padded.set(bytes);
        return asHash256(bytesToBigInt(padded));
    }
}

export class Sha256 extends WebCryptoHasher<Hash256> {
    readonly algorithm: string = SHA256_METADATA.name;
    readonly metadata: Readonly<HashAlgorithmMetadata> = SHA256_METADATA;
    protected getSubtleName(): string { return 'SHA-256'; }
    protected getOutputSize(): number { return 32; }
    protected convertResult(bytes: Uint8Array): Hash256 {
        return asHash256(bytesToBigInt(bytes));
    }
}

export class Sha384 extends WebCryptoHasher<Hash512> {
    readonly algorithm: string = SHA384_METADATA.name;
    readonly metadata: Readonly<HashAlgorithmMetadata> = SHA384_METADATA;
    protected getSubtleName(): string { return 'SHA-384'; }
    protected getOutputSize(): number { return 48; }
    protected convertResult(bytes: Uint8Array): Hash512 {
        let v = 0n;
        for (let i = 0; i < bytes.length; i++) v = (v << 8n) | BigInt(bytes[i]!);
        return v as unknown as Hash512;
    }
}

export class Sha512 extends WebCryptoHasher<Hash512> {
    readonly algorithm: string = SHA512_METADATA.name;
    readonly metadata: Readonly<HashAlgorithmMetadata> = SHA512_METADATA;
    protected getSubtleName(): string { return 'SHA-512'; }
    protected getOutputSize(): number { return 64; }
    protected convertResult(bytes: Uint8Array): Hash512 {
        let v = 0n;
        for (let i = 0; i < bytes.length; i++) v = (v << 8n) | BigInt(bytes[i]!);
        return v as unknown as Hash512;
    }
}
