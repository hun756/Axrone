import { describe, expect, it } from 'vitest';
import { Crc32, Crc32c } from '../../hash/algorithms';

const enc = new TextEncoder();

describe('CRC32', () => {
    describe('known RFC 3720 test vectors', () => {
        it('"123456789" → 0xCBF43926', () => {
            const h = new Crc32();
            h.updateBytes(enc.encode('123456789'));
            expect(h.digest()).toBe(0xcbf43926);
        });

        it('empty input → 0', () => {
            const h = new Crc32();
            h.updateBytes(new Uint8Array(0));
            expect(h.digest()).toBe(0);
        });

        it('"a" → 0xE8B7BE43', () => {
            const h = new Crc32();
            h.updateBytes(enc.encode('a'));
            expect(h.digest()).toBe(0xe8b7be43);
        });

        it('"abc" → 0x352441C2', () => {
            const h = new Crc32();
            h.updateBytes(enc.encode('abc'));
            expect(h.digest()).toBe(0x352441c2);
        });

        it('"The quick brown fox jumps over the lazy dog" → 0x414FA339', () => {
            const h = new Crc32();
            h.updateBytes(enc.encode('The quick brown fox jumps over the lazy dog'));
            expect(h.digest()).toBe(0x414fa339);
        });
    });

    describe('API', () => {
        it('digest returns unsigned 32-bit', () => {
            const h = new Crc32();
            h.updateBytes(enc.encode('123456789'));
            const v = h.digest();
            expect(v).toBeGreaterThanOrEqual(0);
            expect(v).toBeLessThan(0x100000000);
        });

        it('digestBytes returns 4 bytes', () => {
            const h = new Crc32();
            h.updateString('test');
            expect(h.digestBytes().length).toBe(4);
        });

        it('digestHex returns 8-char hex', () => {
            const h = new Crc32();
            h.updateString('test');
            expect(h.digestHex()).toMatch(/^[0-9a-f]{8}$/);
        });

        it('digestBigInt returns bigint', () => {
            const h = new Crc32();
            h.updateString('test');
            expect(typeof h.digestBigInt()).toBe('bigint');
        });

        it('determinism', () => {
            const a = new Crc32();
            a.updateBytes(enc.encode('test'));
            const b = new Crc32();
            b.updateBytes(enc.encode('test'));
            expect(a.digest()).toBe(b.digest());
        });

        it('reset clears state', () => {
            const h = new Crc32();
            h.updateBytes(enc.encode('12345'));
            h.reset();
            h.updateBytes(enc.encode('123456789'));
            expect(h.digest()).toBe(0xcbf43926);
        });

        it('clone independence', () => {
            const a = new Crc32();
            a.updateBytes(enc.encode('a'));
            const b = a.clone();
            a.updateBytes(enc.encode('b'));
            expect(a.digest()).not.toBe(b.digest());
        });

        it('byteLength tracking', () => {
            const h = new Crc32();
            h.updateBytes(enc.encode('hello'));
            expect(h.byteLength).toBe(5);
        });

        it('rejects updates after digest', () => {
            const h = new Crc32();
            h.updateString('a');
            h.digest();
            expect(() => h.updateString('b')).toThrow();
        });

        it('supports incremental update', () => {
            const a = new Crc32();
            a.updateBytes(enc.encode('12345'));
            a.updateBytes(enc.encode('6789'));
            const b = new Crc32();
            b.updateBytes(enc.encode('123456789'));
            expect(a.digest()).toBe(b.digest());
        });
    });
});

describe('CRC32C', () => {
    describe('known RFC 3720 test vectors', () => {
        it('"123456789" → 0xE3069283', () => {
            const h = new Crc32c();
            h.updateBytes(enc.encode('123456789'));
            expect(h.digest()).toBe(0xe3069283);
        });

        it('empty input → 0', () => {
            const h = new Crc32c();
            h.updateBytes(new Uint8Array(0));
            expect(h.digest()).toBe(0);
        });

        it('"a" → 0xC1D04330', () => {
            const h = new Crc32c();
            h.updateBytes(enc.encode('a'));
            expect(h.digest()).toBe(0xc1d04330);
        });
    });

    describe('API', () => {
        it('digestBytes returns 4 bytes', () => {
            const h = new Crc32c();
            h.updateString('test');
            expect(h.digestBytes().length).toBe(4);
        });

        it('reset', () => {
            const h = new Crc32c();
            h.updateBytes(enc.encode('12345'));
            h.reset();
            h.updateBytes(enc.encode('123456789'));
            expect(h.digest()).toBe(0xe3069283);
        });

        it('different from CRC32', () => {
            const c = new Crc32c();
            c.updateBytes(enc.encode('123456789'));
            const k = new Crc32();
            k.updateBytes(enc.encode('123456789'));
            expect(c.digest()).not.toBe(k.digest());
        });
    });
});
