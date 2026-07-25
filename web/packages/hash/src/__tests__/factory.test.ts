import { describe, expect, it, afterEach } from 'vitest';
import {
    createHasher,
    getFactory,
    hasFactory,
    listAlgorithms,
    listAlgorithmsByCategory,
    hash,
    hashBytes,
    hashString,
    registerCustomAlgorithm,
    unregisterCustomAlgorithm,
} from '../hash/factory';
import { Fnv1a32 } from '../hash/algorithms';
import type { IHasher } from '../hash/interfaces';
import type { Hash32 } from '../hash/types';

describe('factory', () => {
    it('listAlgorithms returns all built-in', () => {
        const algs = listAlgorithms();
        expect(algs).toContain('fnv1a-32');
        expect(algs).toContain('fnv1a-64');
        expect(algs).toContain('djb2');
        expect(algs).toContain('crc32');
        expect(algs).toContain('crc32c');
        expect(algs).toContain('murmur3-32');
        expect(algs).toContain('murmur2-64');
        expect(algs).toContain('xxhash32');
        expect(algs).toContain('xxhash64');
        expect(algs).toContain('cyrb53');
        expect(algs).toContain('siphash-2-4');
        expect(algs).toContain('sha-1');
        expect(algs).toContain('sha-256');
        expect(algs).toContain('sha-384');
        expect(algs).toContain('sha-512');
    });

    it('listAlgorithmsByCategory splits correctly', () => {
        const crypto = listAlgorithmsByCategory('crypto');
        const nonCrypto = listAlgorithmsByCategory('non-crypto');
        expect(crypto).toContain('sha-256');
        expect(nonCrypto).toContain('fnv1a-32');
    });

    it('hasFactory returns true for known, false for unknown', () => {
        expect(hasFactory('fnv1a-32')).toBe(true);
        expect(hasFactory('not-a-real-algo')).toBe(false);
    });

    it('getFactory returns factory for known algorithm', () => {
        const f = getFactory('fnv1a-32');
        expect(f).toBeDefined();
        expect(f.metadata.name).toBe('fnv1a-32');
        expect(f.metadata.outputSize).toBe(32);
    });

    it('getFactory throws for unknown', () => {
        expect(() => getFactory('nope' as any)).toThrow();
    });

    it('createHasher returns working hasher for known algorithm', () => {
        const h = createHasher('fnv1a-32');
        h.updateString('test');
        expect(typeof h.digest()).toBe('number');
    });

    it('createHasher returns working hasher for sha-256', () => {
        const h = createHasher('sha-256');
        expect(h.algorithm).toBe('sha-256');
    });

    describe('custom algorithm registration', () => {
        const customName = 'test-custom-32' as any;

        afterEach(() => {
            unregisterCustomAlgorithm(customName);
        });

        it('registers and uses a custom algorithm', () => {
            registerCustomAlgorithm<Hash32>(
                customName,
                Fnv1a32 as new (seed?: any) => IHasher<Hash32>,
                {
                    name: customName,
                    family: 'fast',
                    category: 'non-crypto',
                    outputSize: 32,
                    blockSize: 1,
                    seedable: false,
                    keyed: false,
                    cryptographicallySecure: false,
                    description: 'Test custom',
                }
            );
            expect(hasFactory(customName)).toBe(true);
            const h = createHasher(customName);
            h.updateString('a');
            expect(h.digest()).toBeDefined();
        });

        it('unregisterCustomAlgorithm removes it', () => {
            registerCustomAlgorithm<Hash32>(
                customName,
                Fnv1a32 as new (seed?: any) => IHasher<Hash32>,
                {
                    name: customName,
                    family: 'fast',
                    category: 'non-crypto',
                    outputSize: 32,
                    blockSize: 1,
                    seedable: false,
                    keyed: false,
                    cryptographicallySecure: false,
                    description: 'Test custom',
                }
            );
            unregisterCustomAlgorithm(customName);
            expect(hasFactory(customName)).toBe(false);
        });
    });

    describe('convenience hash() functions', () => {
        it('hash() returns number for fnv1a-32', () => {
            const h = hash('fnv1a-32', 'hello');
            expect(typeof h).toBe('number');
        });

        it('hashBytes() returns number', () => {
            const enc = new TextEncoder();
            const r = hashBytes('fnv1a-32', enc.encode('hello'));
            expect(typeof r).toBe('number');
        });

        it('hashString() returns number', () => {
            const h = hashString('fnv1a-32', 'hello');
            expect(typeof h).toBe('number');
        });
    });
});
