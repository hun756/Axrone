import { describe, expect, it } from 'vitest';
import { HashBuilder, builder, HashBuilderFactory } from '../hash/builder';
import { createHasher } from '../hash/factory';
import { asSeed32 } from '../hash/types';

const enc = new TextEncoder();

describe('HashBuilder gaps — withSeed / withKey / withDomain', () => {
    it('withSeed changes the hash output', () => {
        const a = new HashBuilder('fnv1a-32').withSeed(asSeed32(0)).update('hello').digest();
        const b = new HashBuilder('fnv1a-32').withSeed(asSeed32(42)).update('hello').digest();
        expect(a).not.toBe(b);
    });

    it('withKey injects key bytes', () => {
        const key = new Uint8Array([1, 2, 3, 4]);
        const a = new HashBuilder('fnv1a-32').withKey(key).update('hello').digest();
        const b = new HashBuilder('fnv1a-32').update('hello').digest();
        expect(a).not.toBe(b);
    });

    it('withDomain prefixes domain tag', () => {
        const a = new HashBuilder('fnv1a-32').withDomain('ns-alpha').update('data').digest();
        const b = new HashBuilder('fnv1a-32').withDomain('ns-beta').update('data').digest();
        expect(a).not.toBe(b);
    });

    it('withDomain matches manual domain prefix', () => {
        const tag = 'my-domain';
        const b = new HashBuilder('fnv1a-32').withDomain(tag).update('data').digest();
        const h = createHasher('fnv1a-32');
        h.updateBytes(enc.encode(tag));
        h.updateString('data');
        expect(b).toBe(h.digest());
    });
});

describe('HashBuilder gaps — update() type dispatch', () => {
    it('update(string) matches updateString', () => {
        const b = new HashBuilder('fnv1a-32').update('hello').digest();
        const h = createHasher('fnv1a-32');
        h.updateString('hello');
        expect(b).toBe(h.digest());
    });

    it('update(number) dispatches to updateF64', () => {
        const b = new HashBuilder('fnv1a-32').update(3.14).digest();
        const h = createHasher('fnv1a-32');
        h.updateF64(3.14);
        expect(b).toBe(h.digest());
    });

    it('update(bigint) dispatches to updateI64', () => {
        const b = new HashBuilder('fnv1a-32').update(42n).digest();
        const h = createHasher('fnv1a-32');
        h.updateI64(42n);
        expect(b).toBe(h.digest());
    });

    it('update(boolean) dispatches to updateBoolean', () => {
        const bt = new HashBuilder('fnv1a-32').update(true).digest();
        const bf = new HashBuilder('fnv1a-32').update(false).digest();
        expect(bt).not.toBe(bf);
        const h = createHasher('fnv1a-32');
        h.updateBoolean(true);
        expect(bt).toBe(h.digest());
    });

    it('update(Uint8Array) dispatches to updateBytes', () => {
        const data = enc.encode('hello');
        const b = new HashBuilder('fnv1a-32').update(data).digest();
        const h = createHasher('fnv1a-32');
        h.updateBytes(data);
        expect(b).toBe(h.digest());
    });
});

describe('HashBuilder gaps — updateMany with mixed types', () => {
    it('handles mixed type array', () => {
        const b = new HashBuilder('fnv1a-32')
            .updateMany(['hello', 42, true, 1n, new Uint8Array([1, 2])])
            .digest();
        expect(typeof b).toBe('number');

        // Verify it matches sequential update() calls
        const seq = new HashBuilder('fnv1a-32')
            .update('hello')
            .update(42)
            .update(true)
            .update(1n)
            .update(new Uint8Array([1, 2]))
            .digest();
        expect(b).toBe(seq);
    });
});

describe('HashBuilder gaps — reset with domain/key/seed', () => {
    it('reset re-applies domain tag', () => {
        const b = new HashBuilder('fnv1a-32').withDomain('ns');
        b.update('first');
        b.reset(); // should re-apply domain
        // After reset, hasher is clean + domain prefix
        const afterReset = b.digest(); // digest without additional update
        // Compare with fresh builder that has domain but no data
        const fresh = new HashBuilder('fnv1a-32').withDomain('ns').digest();
        expect(afterReset).toBe(fresh);
    });

    it('reset re-applies key', () => {
        const key = new Uint8Array([10, 20, 30]);
        const b = new HashBuilder('fnv1a-32').withKey(key).update('data');
        b.reset();
        // After reset, should have key re-applied
        const afterReset = b.digest();
        const fresh = new HashBuilder('fnv1a-32').withKey(key).digest();
        expect(afterReset).toBe(fresh);
    });
});

describe('HashBuilder gaps — use() with domain re-application', () => {
    it('use() preserves domain when switching algorithms', () => {
        const b = new HashBuilder('fnv1a-32').withDomain('ns');
        b.update('data');
        b.use('fnv1a-64'); // switch algorithm, domain should be re-applied
        const v = b.digest();
        expect(typeof v).toBe('bigint');
    });
});

describe('HashBuilderFactory', () => {
    it('.create() returns a HashBuilder', () => {
        const b = HashBuilderFactory.create('fnv1a-32');
        expect(b).toBeInstanceOf(HashBuilder);
    });

    it('.fnv1a32() creates fnv1a-32 builder', () => {
        const b = HashBuilderFactory.fnv1a32().update('test');
        const hex = b.digestHex();
        expect(hex).toMatch(/^[0-9a-f]{8}$/);
    });

    it('.xxhash32() creates xxhash32 builder', () => {
        const b = HashBuilderFactory.xxhash32().update('test');
        const hex = b.digestHex();
        expect(hex).toMatch(/^[0-9a-f]{8}$/);
    });

    it('.xxhash64() creates xxhash64 builder', () => {
        const b = HashBuilderFactory.xxhash64().update('test');
        const v = b.digest();
        expect(typeof v).toBe('bigint');
    });

    it('.sha256() creates sha-256 builder', () => {
        const b = HashBuilderFactory.sha256().update('test');
        // SHA uses WebCrypto (async), so we verify the builder was created
        // and the algorithm is set correctly
        const hasher = b.buildHasher();
        expect(hasher.algorithm).toBe('sha-256');
    });

    it('all factory methods produce different outputs for same input', () => {
        const results = new Set<string>();
        results.add(HashBuilderFactory.fnv1a32().update('x').digestHex());
        results.add(HashBuilderFactory.xxhash32().update('x').digestHex());
        // fnv1a32 and xxhash32 should produce different hashes
        expect(results.size).toBe(2);
    });
});

describe('builder() convenience function', () => {
    it('returns a HashBuilder with default algorithm', () => {
        const b = builder();
        expect(b).toBeInstanceOf(HashBuilder);
        const hex = b.update('test').digestHex();
        expect(hex).toMatch(/^[0-9a-f]{8}$/);
    });

    it('accepts algorithm parameter', () => {
        const b = builder('xxhash64');
        const v = b.update('test').digest();
        expect(typeof v).toBe('bigint');
    });
});
