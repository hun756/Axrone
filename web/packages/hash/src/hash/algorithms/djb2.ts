import type { BytesLike } from '../../../types';
import { float32ToBits, float64ToBitsPair, writeU32LE } from '../bits';
import { asHash32, asSeed32, type Hash32, type Seed32, type HashAlgorithmMetadata } from '../types';
import { Fnv1a32 } from './fnv';
import type { IHasher } from '../interfaces';

const DJB2_METADATA: HashAlgorithmMetadata = {
    name: 'djb2',
    family: 'fast',
    category: 'non-crypto',
    outputSize: 32,
    blockSize: 1,
    seedable: false,
    keyed: false,
    cryptographicallySecure: false,
    description: 'DJB2 hash by Daniel J. Bernstein (hash * 33 + c)',
};

export class Djb2 extends Fnv1a32 {
    constructor() {
        super(asSeed32(0));
        (this as any).algorithm = DJB2_METADATA.name;
        (this as any).metadata = DJB2_METADATA;
        (this as any)._h = 5381;
    }

    override updateBytes(bytes: BytesLike, offset: number = 0, length?: number): this {
        this._checkFinalized();
        const end = length === undefined ? bytes.length : offset + length;
        for (let i = offset; i < end; i++) {
            (this as any)._h = (((this as any)._h as number) * 33 + (bytes[i]! & 0xff)) >>> 0;
        }
        (this as any)._byteLength += end - offset;
        return this;
    }

    override reset(seed: Seed32 = asSeed32(0)): this {
        (this as any)._h = 5381;
        (this as any)._byteLength = 0;
        (this as any)._finalized = false;
        return this;
    }
}

const DJB2A_METADATA: HashAlgorithmMetadata = {
    ...DJB2_METADATA,
    name: 'djb2a',
    description: 'DJB2a variant (xor then multiply)',
};

export class Djb2a extends Fnv1a32 {
    constructor() {
        super(asSeed32(0));
        (this as any).algorithm = DJB2A_METADATA.name;
        (this as any).metadata = DJB2A_METADATA;
        (this as any)._h = 5381;
    }

    override updateBytes(bytes: BytesLike, offset: number = 0, length?: number): this {
        this._checkFinalized();
        const end = length === undefined ? bytes.length : offset + length;
        for (let i = offset; i < end; i++) {
            (this as any)._h = ((((this as any)._h as number) ^ (bytes[i]! & 0xff)) * 33) >>> 0;
        }
        (this as any)._byteLength += end - offset;
        return this;
    }

    override reset(seed: Seed32 = asSeed32(0)): this {
        (this as any)._h = 5381;
        (this as any)._byteLength = 0;
        (this as any)._finalized = false;
        return this;
    }
}

const SDBM_METADATA: HashAlgorithmMetadata = {
    name: 'sdbm',
    family: 'fast',
    category: 'non-crypto',
    outputSize: 32,
    blockSize: 1,
    seedable: false,
    keyed: false,
    cryptographicallySecure: false,
    description: 'SDBM hash used by SDBM database library',
};

export class Sdbm extends Fnv1a32 {
    constructor() {
        super(asSeed32(0));
        (this as any).algorithm = SDBM_METADATA.name;
        (this as any).metadata = SDBM_METADATA;
        (this as any)._h = 0;
    }

    override updateBytes(bytes: BytesLike, offset: number = 0, length?: number): this {
        this._checkFinalized();
        const end = length === undefined ? bytes.length : offset + length;
        for (let i = offset; i < end; i++) {
            const b = bytes[i]! & 0xff;
            (this as any)._h = ((b) + (((this as any)._h as number) << 6) + (((this as any)._h as number) << 16) - (this as any)._h) >>> 0;
        }
        (this as any)._byteLength += end - offset;
        return this;
    }

    override reset(seed: Seed32 = asSeed32(0)): this {
        (this as any)._h = 0;
        (this as any)._byteLength = 0;
        (this as any)._finalized = false;
        return this;
    }
}
