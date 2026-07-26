import type { BytesLike } from '../../../types';
import { float32ToBits, float64ToBitsPair, writeU32LE } from '../bits';
import { asHash32, asSeed32, type Hash32, type Seed32, type HashAlgorithmMetadata } from '../types';
import { Fnv1a32 } from './fnv';
import type { IHasher } from '../interfaces';

const CRC32_METADATA: HashAlgorithmMetadata = {
    name: 'crc32',
    family: 'checksum',
    category: 'checksum',
    outputSize: 32,
    blockSize: 1,
    seedable: true,
    keyed: false,
    cryptographicallySecure: false,
    description: 'CRC-32 (IEEE 802.3, used in zlib/PNG)',
};

const CRC32_TABLE: Uint32Array = (() => {
    const t = new Uint32Array(256);
    for (let i = 0; i < 256; i++) {
        let c = i;
        for (let k = 0; k < 8; k++) {
            c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
        }
        t[i] = c >>> 0;
    }
    return t;
})();

export class Crc32 extends Fnv1a32 {
    constructor(seed: Seed32 = asSeed32(0)) {
        super(seed);
        (this as any).algorithm = CRC32_METADATA.name;
        (this as any).metadata = CRC32_METADATA;
        (this as any)._h = ((seed as number) >>> 0) ^ 0xffffffff;
    }

    override updateBytes(bytes: BytesLike, offset: number = 0, length?: number): this {
        this._checkFinalized();
        const end = length === undefined ? bytes.length : offset + length;
        for (let i = offset; i < end; i++) {
            (this as any)._h = ((this as any)._h >>> 8) ^ CRC32_TABLE[(((this as any)._h as number) ^ (bytes[i]! & 0xff)) & 0xff]!;
        }
        (this as any)._byteLength += end - offset;
        return this;
    }

    override digest(): Hash32 {
        (this as any)._finalized = true;
        (this as any)._h = ((this as any)._h as number) ^ 0xffffffff;
        return asHash32((this as any)._h as number);
    }

    override reset(seed: Seed32 = asSeed32(0)): this {
        (this as any)._h = ((seed as number) >>> 0) ^ 0xffffffff;
        (this as any)._byteLength = 0;
        (this as any)._finalized = false;
        return this;
    }
}

const CRC32C_METADATA: HashAlgorithmMetadata = {
    ...CRC32_METADATA,
    name: 'crc32c',
    description: 'CRC-32C (Castagnoli polynomial, used in iSCSI, SCTP)',
};

const CRC32C_TABLE: Uint32Array = (() => {
    const t = new Uint32Array(256);
    for (let i = 0; i < 256; i++) {
        let c = i;
        for (let k = 0; k < 8; k++) {
            c = c & 1 ? 0x82f63b78 ^ (c >>> 1) : c >>> 1;
        }
        t[i] = c >>> 0;
    }
    return t;
})();

export class Crc32c extends Fnv1a32 {
    constructor(seed: Seed32 = asSeed32(0)) {
        super(seed);
        (this as any).algorithm = CRC32C_METADATA.name;
        (this as any).metadata = CRC32C_METADATA;
        (this as any)._h = ((seed as number) >>> 0) ^ 0xffffffff;
    }

    override updateBytes(bytes: BytesLike, offset: number = 0, length?: number): this {
        this._checkFinalized();
        const end = length === undefined ? bytes.length : offset + length;
        for (let i = offset; i < end; i++) {
            (this as any)._h = ((this as any)._h >>> 8) ^ CRC32C_TABLE[(((this as any)._h as number) ^ (bytes[i]! & 0xff)) & 0xff]!;
        }
        (this as any)._byteLength += end - offset;
        return this;
    }

    override digest(): Hash32 {
        (this as any)._finalized = true;
        (this as any)._h = ((this as any)._h as number) ^ 0xffffffff;
        return asHash32((this as any)._h as number);
    }

    override reset(seed: Seed32 = asSeed32(0)): this {
        (this as any)._h = ((seed as number) >>> 0) ^ 0xffffffff;
        (this as any)._byteLength = 0;
        (this as any)._finalized = false;
        return this;
    }
}
