import { describe, expect, it } from 'vitest';
import { Murmur3_32, Murmur2_64 } from '../../hash/algorithms';

const enc = new TextEncoder();

describe('Murmur3 32-bit', () => {
    describe('known SMHasher vectors (seed=0, no key)', () => {
        it('empty input → 0', () => {
            const h = new Murmur3_32();
            h.updateBytes(new Uint8Array(0));
            expect(h.digest()).toBe(0);
        });

        it('"a"', () => {
            const h = new Murmur3_32();
            h.updateBytes(enc.encode('a'));
            expect(h.digest()).toBe(0x3c2569b2);
        });

        it('"abc"', () => {
            const h = new Murmur3_32();
            h.updateBytes(enc.encode('abc'));
            expect(h.digest()).toBe(0xcfb8023e);
        });

        it('"abcd"', () => {
            const h = new Murmur3_32();
            h.updateBytes(enc.encode('abcd'));
            expect(h.digest()).toBe(0x43ed676a);
        });

        it('"Hello, world!"', () => {
            const h = new Murmur3_32();
            h.updateBytes(enc.encode('Hello, world!'));
            expect(h.digest()).toBe(0xc0363e43);
        });
    });

    describe('seeded variants', () => {
        it('seed=0 vs seed=1 give different results', () => {
            const a = new Murmur3_32();
            a.updateBytes(enc.encode('test'));
            const b = new Murmur3_32(1);
            b.updateBytes(enc.encode('test'));
            expect(a.digest()).not.toBe(b.digest());
        });

        it('same seed produces same output', () => {
            const a = new Murmur3_32(12345);
            a.updateBytes(enc.encode('test'));
            const b = new Murmur3_32(12345);
            b.updateBytes(enc.encode('test'));
            expect(a.digest()).toBe(b.digest());
        });
    });

    describe('API', () => {
        it('determinism', () => {
            const a = new Murmur3_32();
            a.updateBytes(enc.encode('test'));
            const b = new Murmur3_32();
            b.updateBytes(enc.encode('test'));
            expect(a.digest()).toBe(b.digest());
        });

        it('reset', () => {
            const h = new Murmur3_32();
            h.updateBytes(enc.encode('a'));
            h.reset();
            h.updateBytes(enc.encode('abc'));
            expect(h.digest()).toBe(0xcfb8023e);
        });

        it('clone', () => {
            const a = new Murmur3_32();
            a.updateBytes(enc.encode('a'));
            const b = a.clone();
            a.updateBytes(enc.encode('b'));
            expect(a.digest()).not.toBe(b.digest());
        });

        it('byteLength tracking', () => {
            const h = new Murmur3_32();
            h.updateBytes(enc.encode('hello'));
            expect(h.byteLength).toBe(5);
        });

        it('rejects updates after digest', () => {
            const h = new Murmur3_32();
            h.updateString('a');
            h.digest();
            expect(() => h.updateString('b')).toThrow();
        });

        it('incremental update', () => {
            const a = new Murmur3_32();
            a.updateBytes(enc.encode('Hello, '));
            a.updateBytes(enc.encode('world!'));
            const b = new Murmur3_32();
            b.updateBytes(enc.encode('Hello, world!'));
            expect(a.digest()).toBe(b.digest());
        });

        it('digest variants', () => {
            const h = new Murmur3_32();
            h.updateString('test');
            expect(h.digestBytes().length).toBe(4);
            expect(h.digestHex()).toMatch(/^[0-9a-f]{8}$/);
            expect(typeof h.digestBigInt()).toBe('bigint');
        });

        it('digestBase64 returns valid base64', () => {
            const h = new Murmur3_32();
            h.updateString('test');
            const b64 = h.digestBase64();
            expect(typeof b64).toBe('string');
            expect(b64.length).toBeGreaterThan(0);
        });

        it('updateBoolean produces different hashes for true vs false', () => {
            const a = new Murmur3_32();
            a.updateBoolean(true);
            const b = new Murmur3_32();
            b.updateBoolean(false);
            expect(a.digest()).not.toBe(b.digest());
        });

        it('updateI8/I16/U8/U16 delegate correctly', () => {
            const a = new Murmur3_32();
            a.updateI8(42);
            const b = new Murmur3_32();
            b.updateI32(42);
            expect(a.digest()).toBe(b.digest());

            const c = new Murmur3_32();
            c.updateI16(1000);
            const d = new Murmur3_32();
            d.updateI32(1000);
            expect(c.digest()).toBe(d.digest());

            const e = new Murmur3_32();
            e.updateU8(0xff);
            const f = new Murmur3_32();
            f.updateU32(0xff);
            expect(e.digest()).toBe(f.digest());

            const g = new Murmur3_32();
            g.updateU16(0xabcd);
            const hh = new Murmur3_32();
            hh.updateU32(0xabcd);
            expect(g.digest()).toBe(hh.digest());
        });

        it('updateHashable delegates to hashInto', () => {
            const hashable = { hashInto(h: any) { h.updateString('custom'); } };
            const a = new Murmur3_32();
            a.updateHashable(hashable);
            const b = new Murmur3_32();
            b.updateString('custom');
            expect(a.digest()).toBe(b.digest());
        });

        it('non-zero tail (1 byte remainder)', () => {
            // "a" is 1 byte, which means 1 byte remainder
            const h = new Murmur3_32();
            h.updateBytes(enc.encode('a'));
            expect(h.digest()).toBe(0x3c2569b2);
        });

        it('non-zero tail (2 byte remainder)', () => {
            // "ab" is 2 bytes
            const h = new Murmur3_32();
            h.updateBytes(enc.encode('ab'));
            const v = h.digest();
            expect(v).not.toBe(0);
        });

        it('non-zero tail (3 byte remainder)', () => {
            // "abc" is 3 bytes
            const h = new Murmur3_32();
            h.updateBytes(enc.encode('abc'));
            expect(h.digest()).toBe(0xcfb8023e);
        });

        it('metadata is correct', () => {
            const h = new Murmur3_32();
            expect(h.metadata.name).toBe('murmur3-32');
            expect(h.metadata.outputSize).toBe(32);
            expect(h.metadata.blockSize).toBe(4);
            expect(h.metadata.seedable).toBe(true);
        });
    });
});

describe('Murmur2 64-bit', () => {
    it('produces known output for "abc"', () => {
        const h = new Murmur2_64();
        h.updateBytes(enc.encode('abc'));
        const v = h.digest() as unknown as bigint;
        expect(typeof v).toBe('bigint');
        expect(v).not.toBe(0n);
    });

    it('digestBytes returns 8 bytes', () => {
        const h = new Murmur2_64();
        h.updateString('test');
        expect(h.digestBytes().length).toBe(8);
    });

    it('digestHex returns 16-char hex', () => {
        const h = new Murmur2_64();
        h.updateString('test');
        expect(h.digestHex()).toMatch(/^[0-9a-f]{16}$/);
    });

    it('determinism', () => {
        const a = new Murmur2_64();
        a.updateBytes(enc.encode('test'));
        const b = new Murmur2_64();
        b.updateBytes(enc.encode('test'));
        expect((a.digest() as unknown as bigint)).toBe((b.digest() as unknown as bigint));
    });

    it('different seeds give different output', () => {
        const a = new Murmur2_64();
        a.updateBytes(enc.encode('test'));
        const b = new Murmur2_64(12345 as any);
        b.updateBytes(enc.encode('test'));
        expect((a.digest() as unknown as bigint)).not.toBe((b.digest() as unknown as bigint));
    });

    it('reset', () => {
        const h = new Murmur2_64();
        h.updateBytes(enc.encode('abc'));
        const v1 = h.digest() as unknown as bigint;
        h.reset();
        h.updateBytes(enc.encode('abc'));
        expect(h.digest() as unknown as bigint).toBe(v1);
    });

    it('rejects updates after digest', () => {
        const h = new Murmur2_64();
        h.updateString('a');
        h.digest();
        expect(() => h.updateString('b')).toThrow();
    });

    it('clone creates independent copy', () => {
        const h1 = new Murmur2_64();
        h1.updateBytes(enc.encode('hello'));
        const h2 = h1.clone();
        h1.updateBytes(enc.encode('world'));
        expect((h1.digest() as unknown as bigint)).not.toBe((h2.digest() as unknown as bigint));
    });

    it('byteLength tracking', () => {
        const h = new Murmur2_64();
        expect(h.byteLength).toBe(0);
        h.updateBytes(enc.encode('hello'));
        expect(h.byteLength).toBe(5);
    });

    it('digestBase64 returns valid base64', () => {
        const h = new Murmur2_64();
        h.updateString('test');
        const b64 = h.digestBase64();
        expect(typeof b64).toBe('string');
        expect(b64.length).toBeGreaterThan(0);
    });

    it('digestBigInt returns bigint', () => {
        const h = new Murmur2_64();
        h.updateString('test');
        expect(typeof h.digestBigInt()).toBe('bigint');
    });

    it('finalized guard for all update methods', () => {
        const h = new Murmur2_64();
        h.digest();
        expect(() => h.updateBytes(enc.encode('x'))).toThrow();
        expect(() => h.updateBoolean(true)).toThrow();
        expect(() => h.updateI32(42)).toThrow();
        expect(() => h.updateI64(1n)).toThrow();
        expect(() => h.updateF32(1.0)).toThrow();
        expect(() => h.updateF64(1.0)).toThrow();
    });

    it('metadata is correct', () => {
        const h = new Murmur2_64();
        expect(h.metadata.name).toBe('murmur2-64');
        expect(h.metadata.outputSize).toBe(64);
        expect(h.metadata.blockSize).toBe(8);
    });
});
