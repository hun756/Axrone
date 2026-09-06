import type { IHasher, HashFactoryOptions } from './interfaces';
import { createHasher } from './factory';
import type { HashValue, HashAlgorithmName, Seed32, Seed64, Hash32 } from './types';

const SHARED_ENCODER = new TextEncoder();

export class HashBuilder<H extends HashValue = Hash32> {
    private _hasher: IHasher<H>;
    private _algorithm: HashAlgorithmName;
    private _seed?: Seed32 | Seed64;
    private _key?: Uint8Array;
    private _domain?: string;

    constructor(algorithm: HashAlgorithmName = 'fnv1a-32') {
        this._algorithm = algorithm;
        this._hasher = createHasher<H>(algorithm);
    }

    use(algorithm: HashAlgorithmName): this {
        this._algorithm = algorithm;
        const opts: HashFactoryOptions = {};
        if (this._seed !== undefined) opts.seed = this._seed;
        if (this._key !== undefined) opts.key = this._key;
        if (this._domain !== undefined) opts.domain = this._domain;
        this._hasher = createHasher<H>(algorithm, opts);
        if (this._domain) {
            const tagBytes = SHARED_ENCODER.encode(this._domain);
            this._hasher.updateBytes(tagBytes);
        }
        return this;
    }

    private _applyConfig(): void {
        if (this._domain) {
            this._hasher.updateBytes(SHARED_ENCODER.encode(this._domain));
        }
        if (this._key) {
            this._hasher.updateBytes(this._key);
        }
        if (this._seed !== undefined) {
            this._hasher.updateHash(this._seed as unknown as HashValue);
        }
    }

    withSeed(seed: Seed32 | Seed64): this {
        this._seed = seed;
        this._hasher.reset();
        this._applyConfig();
        return this;
    }

    withKey(key: Uint8Array): this {
        this._key = key;
        this._hasher.reset();
        this._applyConfig();
        return this;
    }

    withDomain(tag: string): this {
        this._domain = tag;
        this._hasher.reset();
        this._applyConfig();
        return this;
    }

    update(data: Uint8Array | string | number | bigint | boolean): this {
        if (typeof data === 'string') this._hasher.updateString(data);
        else if (typeof data === 'number') this._hasher.updateF64(data);
        else if (typeof data === 'bigint') this._hasher.updateI64(data);
        else if (typeof data === 'boolean') this._hasher.updateBoolean(data);
        else this._hasher.updateBytes(data);
        return this;
    }

    updateMany(values: readonly (Uint8Array | string | number | bigint | boolean)[]): this {
        for (const v of values) this.update(v);
        return this;
    }

    digest(): H {
        return this._hasher.digest();
    }

    digestHex(): string {
        return this._hasher.digestHex();
    }

    digestBase64(): string {
        return this._hasher.digestBase64();
    }

    digestBytes(): Uint8Array {
        return this._hasher.digestBytes();
    }

    buildHasher(): IHasher<H> {
        return this._hasher;
    }

    reset(): this {
        this._hasher.reset();
        this._applyConfig();
        return this;
    }
}

export function builder<H extends HashValue = Hash32>(algorithm: HashAlgorithmName = 'fnv1a-32'): HashBuilder<H> {
    return new HashBuilder<H>(algorithm);
}

export const HashBuilderFactory = {
    create: builder,
    fnv1a32: () => new HashBuilder('fnv1a-32'),
    xxhash32: () => new HashBuilder('xxhash32'),
    xxhash64: () => new HashBuilder('xxhash64'),
    sha256: () => new HashBuilder('sha-256'),
};
