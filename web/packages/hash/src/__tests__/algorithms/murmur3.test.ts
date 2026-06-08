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
        const b = new Murmur2_64(12345n as any);
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
});
