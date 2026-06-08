import { describe, expect, it, beforeEach, afterEach } from 'vitest';
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
    FACTORIES,
} from '../../hash/factory';
import { asHash32, asHash64, type Hash32, type Hash64 } from '../../hash/types';
import type { IHasher } from '../../hash/interfaces';
import { Fnv1a32 } from '../../hash/algorithms';

describe('factory', () => {
    it('listAlgorithms returns all built-in', () => {
        const algs = listAlgorithms();
        expect(algs).toContain('fnv-1a-32');
        expect(algs).toContain('fnv-1a-64');
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
        const grouped = listAlgorithmsByCategory();
        expect(grouped.crypto).toBeDefined();
        expect(grouped.crypto).toContain('sha-256');
        expect(grouped['non-crypto']).toBeDefined();
        expect(grouped['non-crypto']).toContain('fnv-1a-32');
    });

    it('hasFactory returns true for known, false for unknown', () => {
        expect(hasFactory('fnv-1a-32')).toBe(true);
        expect(hasFactory('not-a-real-algo')).toBe(false);
    });

    it('getFactory returns metadata for known algorithm', () => {
        const m = getFactory('fnv-1a-32');
        expect(m).toBeDefined();
        expect(m!.name).toBe('fnv-1a-32');
        expect(m!.outputSize).toBe(32);
    });

    it('getFactory returns undefined for unknown', () => {
        expect(getFactory('nope')).toBeUndefined();
    });

    it('createHasher returns working hasher for known algorithm', () => {
        const h = createHasher('fnv-1a-32');
        h.updateString('test');
        expect(typeof h.digest()).toBe('number');
    });

    it('createHasher returns working hasher for sha-256', () => {
        const h = createHasher('sha-256');
        h.updateString('test');
        expect(h.algorithm).toBe('sha-256');
    });

    describe('custom algorithm registration', () => {
        const customName = 'test-custom-32';

        afterEach(() => {
            unregisterCustomAlgorithm(customName);
        });

        it('registers and uses a custom algorithm', () => {
            registerCustomAlgorithm(customName, {
                name: customName,
                family: 'fast',
                category: 'non-crypto',
                outputSize: 32,
                blockSize: 1,
                seedable: false,
                keyed: false,
                cryptographicallySecure: false,
                description: 'Test custom',
            });
            expect(hasFactory(customName)).toBe(true);
            const h = createHasher(customName);
            h.updateString('a');
            expect(h.digest()).toBeDefined();
        });

        it('unregisterCustomAlgorithm removes it', () => {
            registerCustomAlgorithm(customName, {
                name: customName,
                family: 'fast',
                category: 'non-crypto',
                outputSize: 32,
                blockSize: 1,
                seedable: false,
                keyed: false,
                cryptographicallySecure: false,
                description: 'Test custom',
            });
            unregisterCustomAlgorithm(customName);
            expect(hasFactory(customName)).toBe(false);
        });

        it('does not allow overwriting built-in', () => {
            expect(() =>
                registerCustomAlgorithm('fnv-1a-32', {
                    name: 'fnv-1a-32',
                    family: 'fast',
                    category: 'non-crypto',
                    outputSize: 32,
                    blockSize: 1,
                    seedable: false,
                    keyed: false,
                    cryptographicallySecure: false,
                    description: 'Override',
                })
            ).toThrow();
        });
    });

    describe('convenience hash() functions', () => {
        it('hash() returns hex string', () => {
            const h = hash('hello', 'fnv-1a-32');
            expect(typeof h).toBe('string');
            expect(h).toMatch(/^[0-9a-f]+$/);
        });

        it('hashBytes() returns Uint8Array', () => {
            const enc = new TextEncoder();
            const r = hashBytes(enc.encode('hello'), 'fnv-1a-32');
            expect(r).toBeInstanceOf(Uint8Array);
        });

        it('hashString() returns hex', () => {
            const h = hashString('hello', 'fnv-1a-32');
            expect(typeof h).toBe('string');
        });
    });
});
