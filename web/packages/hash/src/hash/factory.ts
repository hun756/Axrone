import type { IHasher, IHashFactory, HashFactoryOptions } from './interfaces';
import {
    type Hash32, type Hash64, type Hash256, type Hash512,
    type HashValue, type Seed32, type HashAlgorithmName, type HashAlgorithmMetadata
} from './types';
import { Fnv1a32, Fnv1_32, Fnv1a64, Djb2, Djb2a, Sdbm, Crc32, Crc32c, Murmur3_32, Murmur2_64, XxHash32, XxHash64, Sha1, Sha256, Sha384, Sha512 } from './algorithms';

type AnyCtor<H extends HashValue> = new (seed?: Seed32, ...rest: any[]) => IHasher<H>;

function createFactory<H extends HashValue>(
    name: HashAlgorithmName,
    ctor: AnyCtor<H>,
    outputSize: 32 | 64 | 128 | 160 | 256 | 384 | 512,
    blockSize: number,
    seedable: boolean,
    keyed: boolean,
    crypto: boolean,
    family: 'fast' | 'cryptographic' | 'keyed' | 'checksum' | 'universal',
    category: 'non-crypto' | 'crypto' | 'checksum' | 'universal',
    description: string
): IHashFactory<H> {
    const metadata: HashAlgorithmMetadata = {
        name, family, category, outputSize, blockSize, seedable, keyed,
        cryptographicallySecure: crypto, description,
    };
    return {
        metadata,
        create(options?: HashFactoryOptions) {
            const seed = options?.seed;
            const key = options?.key;
            if (key && (ctor as any).length > 1) {
                return new ctor(seed as unknown as Seed32, key);
            }
            return seed !== undefined ? new ctor(seed as unknown as Seed32) : new ctor();
        },
        hash(input: Uint8Array | string, options?: HashFactoryOptions) {
            const h = this.create(options);
            if (typeof input === 'string') h.updateString(input);
            else h.updateBytes(input);
            return h.digest();
        },
        hashBytes(input: Uint8Array, options?: HashFactoryOptions) {
            const h = this.create(options);
            h.updateBytes(input);
            return h.digest();
        },
        hashString(input: string, options?: HashFactoryOptions) {
            const h = this.create(options);
            h.updateString(input);
            return h.digest();
        },
    };
}

export const FACTORIES: ReadonlyMap<HashAlgorithmName, IHashFactory<any>> = (() => {
    const m = new Map<HashAlgorithmName, IHashFactory<any>>();

    m.set('fnv1a-32', createFactory<Hash32>('fnv1a-32', Fnv1a32 as AnyCtor<Hash32>, 32, 1, true, false, false, 'fast', 'non-crypto', 'FNV-1a 32-bit'));
    m.set('fnv1-32', createFactory<Hash32>('fnv1-32', Fnv1_32 as AnyCtor<Hash32>, 32, 1, true, false, false, 'fast', 'non-crypto', 'FNV-1 32-bit'));
    m.set('fnv1a-64', createFactory<Hash64>('fnv1a-64', Fnv1a64 as AnyCtor<Hash64>, 64, 1, true, false, false, 'fast', 'non-crypto', 'FNV-1a 64-bit'));
    m.set('djb2', createFactory<Hash32>('djb2', Djb2 as AnyCtor<Hash32>, 32, 1, false, false, false, 'fast', 'non-crypto', 'DJB2'));
    m.set('djb2a', createFactory<Hash32>('djb2a', Djb2a as AnyCtor<Hash32>, 32, 1, false, false, false, 'fast', 'non-crypto', 'DJB2a'));
    m.set('sdbm', createFactory<Hash32>('sdbm', Sdbm as AnyCtor<Hash32>, 32, 1, false, false, false, 'fast', 'non-crypto', 'SDBM'));
    m.set('crc32', createFactory<Hash32>('crc32', Crc32 as AnyCtor<Hash32>, 32, 1, true, false, false, 'checksum', 'checksum', 'CRC-32 IEEE'));
    m.set('crc32c', createFactory<Hash32>('crc32c', Crc32c as AnyCtor<Hash32>, 32, 1, true, false, false, 'checksum', 'checksum', 'CRC-32C Castagnoli'));
    m.set('murmur3-32', createFactory<Hash32>('murmur3-32', Murmur3_32 as AnyCtor<Hash32>, 32, 4, true, false, false, 'fast', 'non-crypto', 'MurmurHash3 32-bit'));
    m.set('murmur2-64', createFactory<Hash64>('murmur2-64', Murmur2_64 as AnyCtor<Hash64>, 64, 8, true, false, false, 'fast', 'non-crypto', 'MurmurHash2 64-bit'));
    m.set('xxhash32', createFactory<Hash32>('xxhash32', XxHash32 as AnyCtor<Hash32>, 32, 16, true, false, false, 'fast', 'non-crypto', 'xxHash32'));
    m.set('xxhash64', createFactory<Hash64>('xxhash64', XxHash64 as AnyCtor<Hash64>, 64, 32, true, false, false, 'fast', 'non-crypto', 'xxHash64'));
    m.set('sha-1', createFactory<Hash256>('sha-1', Sha1 as AnyCtor<Hash256>, 160, 64, false, false, false, 'cryptographic', 'crypto', 'SHA-1 (insecure)'));
    m.set('sha-256', createFactory<Hash256>('sha-256', Sha256 as AnyCtor<Hash256>, 256, 64, false, false, true, 'cryptographic', 'crypto', 'SHA-256'));
    m.set('sha-384', createFactory<Hash512>('sha-384', Sha384 as AnyCtor<Hash512>, 384, 128, false, false, true, 'cryptographic', 'crypto', 'SHA-384'));
    m.set('sha-512', createFactory<Hash512>('sha-512', Sha512 as AnyCtor<Hash512>, 512, 128, false, false, true, 'cryptographic', 'crypto', 'SHA-512'));

    return m;
})();

export function getFactory<H extends HashValue>(name: HashAlgorithmName): IHashFactory<H> {
    const f = FACTORIES.get(name);
    if (!f) {
        const err = new Error(`Hash algorithm '${name}' not found in registry`) as Error & { code?: string };
        err.code = 'HASH_ALGORITHM_NOT_FOUND';
        throw err;
    }
    return f as IHashFactory<H>;
}

export function hasFactory(name: string): boolean {
    return FACTORIES.has(name as HashAlgorithmName);
}

export function listAlgorithms(): readonly HashAlgorithmName[] {
    return Array.from(FACTORIES.keys());
}

export function listAlgorithmsByCategory(category: 'non-crypto' | 'crypto' | 'checksum' | 'universal'): readonly HashAlgorithmName[] {
    const result: HashAlgorithmName[] = [];
    for (const [name, f] of FACTORIES) {
        if (f.metadata.category === category) result.push(name);
    }
    return result;
}

export function createHasher<H extends HashValue>(name: HashAlgorithmName, options?: HashFactoryOptions): IHasher<H> {
    return getFactory<H>(name).create(options);
}

export function hash<H extends HashValue>(name: HashAlgorithmName, input: Uint8Array | string, options?: HashFactoryOptions): H {
    return getFactory<H>(name).hash(input, options) as H;
}

export function hashBytes<H extends HashValue>(name: HashAlgorithmName, bytes: Uint8Array, options?: HashFactoryOptions): H {
    return getFactory<H>(name).hashBytes(bytes, options) as H;
}

export function hashString<H extends HashValue>(name: HashAlgorithmName, input: string, options?: HashFactoryOptions): H {
    return getFactory<H>(name).hashString(input, options) as H;
}

export function registerCustomAlgorithm<H extends HashValue>(
    name: HashAlgorithmName,
    ctor: new (seed?: Seed32) => IHasher<H>,
    metadata: HashAlgorithmMetadata
): void {
    const factory = createFactory<H>(
        name,
        ctor as any,
        metadata.outputSize as 32,
        metadata.blockSize,
        metadata.seedable,
        metadata.keyed,
        metadata.cryptographicallySecure,
        metadata.family,
        metadata.category,
        metadata.description
    );
    (FACTORIES as Map<HashAlgorithmName, IHashFactory<any>>).set(name, factory);
}

export function unregisterCustomAlgorithm(name: HashAlgorithmName): boolean {
    return (FACTORIES as Map<HashAlgorithmName, IHashFactory<any>>).delete(name);
}
