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
            const b = new XxHash64(12345n);
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
    });
});
