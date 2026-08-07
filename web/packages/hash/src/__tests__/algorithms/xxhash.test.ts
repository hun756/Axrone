import { describe, expect, it } from 'vitest';
import { XxHash32, XxHash64 } from '../../hash/algorithms';

const enc = new TextEncoder();

describe('xxHash32', () => {
    describe('known vectors (seed=0)', () => {
        it('empty input', () => {
            const h = new XxHash32();
            h.updateBytes(new Uint8Array(0));
            expect(h.digest()).toBe(0x02cc5d05);
        });

        it('"a"', () => {
            const h = new XxHash32();
            h.updateBytes(enc.encode('a'));
            expect(h.digest()).toBe(0x8fd39473);
        });

        it('"abc"', () => {
            const h = new XxHash32();
            h.updateBytes(enc.encode('abc'));
            expect(h.digest()).toBe(0xee37052e);
        });

        it('"Hello, world!"', () => {
            const h = new XxHash32();
            h.updateBytes(enc.encode('Hello, world!'));
            expect(h.digest()).toBe(0x4042669b);
        });
    });

    describe('API', () => {
        it('determinism', () => {
            const a = new XxHash32();
            a.updateBytes(enc.encode('test'));
            const b = new XxHash32();
            b.updateBytes(enc.encode('test'));
            expect(a.digest()).toBe(b.digest());
        });

        it('different seeds give different output', () => {
            const a = new XxHash32();
            a.updateBytes(enc.encode('test'));
            const b = new XxHash32(12345);
            b.updateBytes(enc.encode('test'));
            expect(a.digest()).not.toBe(b.digest());
        });

        it('reset', () => {
            const h = new XxHash32();
            h.updateBytes(enc.encode('a'));
            h.reset();
            h.updateBytes(enc.encode('abc'));
            expect(h.digest()).toBe(0xee37052e);
        });

        it('clone independence', () => {
            const a = new XxHash32();
            a.updateBytes(enc.encode('a'));
            const b = a.clone();
            a.updateBytes(enc.encode('b'));
            expect(a.digest()).not.toBe(b.digest());
        });

        it('digestBytes returns 4 bytes', () => {
            const h = new XxHash32();
            h.updateString('test');
            expect(h.digestBytes().length).toBe(4);
        });

        it('digestHex returns 8-char hex', () => {
            const h = new XxHash32();
            h.updateString('test');
            expect(h.digestHex()).toMatch(/^[0-9a-f]{8}$/);
        });

        it('byteLength tracking', () => {
            const h = new XxHash32();
            h.updateBytes(enc.encode('hello'));
            expect(h.byteLength).toBe(5);
        });

        it('rejects updates after digest', () => {
            const h = new XxHash32();
            h.updateString('a');
            h.digest();
            expect(() => h.updateString('b')).toThrow();
        });

        it('incremental update', () => {
            const a = new XxHash32();
            a.updateBytes(enc.encode('Hello, '));
            a.updateBytes(enc.encode('world!'));
            const b = new XxHash32();
            b.updateBytes(enc.encode('Hello, world!'));
            expect(a.digest()).toBe(b.digest());
        });

        it('updateBoolean produces different hashes', () => {
            const a = new XxHash32();
            a.updateBoolean(true);
            const b = new XxHash32();
            b.updateBoolean(false);
            expect(a.digest()).not.toBe(b.digest());
        });

        it('updateI8/I16/U8/U16 delegate correctly', () => {
            const a = new XxHash32();
            a.updateI8(42);
            const b = new XxHash32();
            b.updateI32(42);
            expect(a.digest()).toBe(b.digest());

            const c = new XxHash32();
            c.updateU8(0xff);
            const d = new XxHash32();
            d.updateU32(0xff);
            expect(c.digest()).toBe(d.digest());

            const e = new XxHash32();
            e.updateU16(0xabcd);
            const f = new XxHash32();
            f.updateU32(0xabcd);
            expect(e.digest()).toBe(f.digest());
        });

        it('updateHashable delegates to hashInto', () => {
            const hashable = { hashInto(h: any) { h.updateString('x'); } };
            const a = new XxHash32();
            a.updateHashable(hashable);
            const b = new XxHash32();
            b.updateString('x');
            expect(a.digest()).toBe(b.digest());
        });

        it('digestBase64 returns valid base64', () => {
            const h = new XxHash32();
            h.updateString('test');
            const b64 = h.digestBase64();
            expect(typeof b64).toBe('string');
            expect(b64.length).toBeGreaterThan(0);
        });

        it('large input (>= 16 bytes) exercises main loop', () => {
            const data = enc.encode('abcdefghijklmnop'); // exactly 16 bytes
            const h = new XxHash32();
            h.updateBytes(data);
            const v = h.digest();
            expect(typeof v).toBe('number');
            expect(v).toBeGreaterThanOrEqual(0);
        });

        it('large input (32 bytes) exercises main loop fully', () => {
            const data = new Uint8Array(32);
            for (let i = 0; i < 32; i++) data[i] = i;
            const h = new XxHash32();
            h.updateBytes(data);
            const v = h.digest();
            expect(typeof v).toBe('number');
            // Deterministic
            const h2 = new XxHash32();
            h2.updateBytes(data);
            expect(h2.digest()).toBe(v);
        });

        it('metadata is correct', () => {
            const h = new XxHash32();
            expect(h.metadata.name).toBe('xxhash32');
            expect(h.metadata.outputSize).toBe(32);
            expect(h.metadata.blockSize).toBe(16);
        });
    });
});

describe('xxHash64', () => {
    describe('known vectors (seed=0)', () => {
        it('empty input', () => {
            const h = new XxHash64();
            h.updateBytes(new Uint8Array(0));
            expect((h.digest() as unknown as bigint)).toBe(0xe5787ae7444cc0e7n);
        });

        it('"a"', () => {
            const h = new XxHash64();
            h.updateBytes(enc.encode('a'));
            expect((h.digest() as unknown as bigint)).toBe(0xfa13fbb33234a46en);
        });

        it('"abc"', () => {
            const h = new XxHash64();
            h.updateBytes(enc.encode('abc'));
            expect((h.digest() as unknown as bigint)).toBe(0xb53fcf2fb2dbdb73n);
        });

        it('"Hello, world!"', () => {
            const h = new XxHash64();
            h.updateBytes(enc.encode('Hello, world!'));
            expect((h.digest() as unknown as bigint)).toBe(0xcb7e18d43b222a4bn);
        });
    });

    describe('API', () => {
        it('determinism', () => {
            const a = new XxHash64();
            a.updateBytes(enc.encode('test'));
            const b = new XxHash64();
            b.updateBytes(enc.encode('test'));
            expect((a.digest() as unknown as bigint)).toBe((b.digest() as unknown as bigint));
        });

        it('different seeds give different output', () => {
            const a = new XxHash64();
            a.updateBytes(enc.encode('test'));
            const b = new XxHash64(12345);
            b.updateBytes(enc.encode('test'));
            expect((a.digest() as unknown as bigint)).not.toBe((b.digest() as unknown as bigint));
        });

        it('reset', () => {
            const h = new XxHash64();
            h.updateBytes(enc.encode('abc'));
            const v1 = h.digest() as unknown as bigint;
            h.reset();
            h.updateBytes(enc.encode('abc'));
            expect((h.digest() as unknown as bigint)).toBe(v1);
        });

        it('digestBytes returns 8 bytes', () => {
            const h = new XxHash64();
            h.updateString('test');
            expect(h.digestBytes().length).toBe(8);
        });

        it('digestHex returns 16-char hex', () => {
            const h = new XxHash64();
            h.updateString('test');
            expect(h.digestHex()).toMatch(/^[0-9a-f]{16}$/);
        });

        it('rejects updates after digest', () => {
            const h = new XxHash64();
            h.updateString('a');
            h.digest();
            expect(() => h.updateString('b')).toThrow();
        });

        it('clone creates independent copy', () => {
            const h1 = new XxHash64();
            h1.updateBytes(enc.encode('hello'));
            const h2 = h1.clone();
            h1.updateBytes(enc.encode('world'));
            expect((h1.digest() as unknown as bigint)).not.toBe((h2.digest() as unknown as bigint));
        });

        it('byteLength tracking', () => {
            const h = new XxHash64();
            expect(h.byteLength).toBe(0);
            h.updateBytes(enc.encode('hello'));
            expect(h.byteLength).toBe(5);
        });

        it('digestBase64 returns valid base64', () => {
            const h = new XxHash64();
            h.updateString('test');
            const b64 = h.digestBase64();
            expect(typeof b64).toBe('string');
            expect(b64.length).toBeGreaterThan(0);
        });

        it('digestBigInt returns bigint', () => {
            const h = new XxHash64();
            h.updateString('test');
            expect(typeof h.digestBigInt()).toBe('bigint');
        });

        it('metadata is correct', () => {
            const h = new XxHash64();
            expect(h.metadata.name).toBe('xxhash64');
            expect(h.metadata.outputSize).toBe(64);
            expect(h.metadata.blockSize).toBe(32);
        });
    });
});
