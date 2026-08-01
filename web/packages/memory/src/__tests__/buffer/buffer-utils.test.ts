import { describe, expect, it } from 'vitest';
import { BufferUtils } from '../../buffering/utils';
import { ByteOrder } from '../../buffering/types';

describe('BufferUtils', () => {
    describe('nativeEndianness()', () => {
        it('returns a valid ByteOrder', () => {
            const endianness = BufferUtils.nativeEndianness();
            expect([ByteOrder.Little, ByteOrder.Big]).toContain(endianness);
        });
    });

    describe('encodeString() / decodeString()', () => {
        it('utf8 roundtrip', () => {
            const str = 'Hello, World!';
            const encoded = BufferUtils.encodeString(str, 'utf8');
            const decoded = BufferUtils.decodeString(encoded, 'utf8');
            expect(decoded).toBe(str);
        });

        it('utf8 handles unicode', () => {
            const str = 'こんにちは世界 🌍';
            const encoded = BufferUtils.encodeString(str, 'utf8');
            const decoded = BufferUtils.decodeString(encoded, 'utf8');
            expect(decoded).toBe(str);
        });

        it('utf16 roundtrip', () => {
            const str = 'Hello, World!';
            const encoded = BufferUtils.encodeString(str, 'utf16');
            const decoded = BufferUtils.decodeString(encoded, 'utf16');
            expect(decoded).toBe(str);
        });

        it('utf16 produces 2 bytes per char', () => {
            const str = 'ABC';
            const encoded = BufferUtils.encodeString(str, 'utf16');
            expect(encoded.byteLength).toBe(6);
        });
    });

    describe('calculateHash()', () => {
        it('fnv1a produces deterministic result', () => {
            const data = new Uint8Array([1, 2, 3, 4]);
            const h1 = BufferUtils.calculateHash(data, 'fnv1a');
            const h2 = BufferUtils.calculateHash(data, 'fnv1a');
            expect(h1).toBe(h2);
        });

        it('djb2 produces deterministic result', () => {
            const data = new Uint8Array([1, 2, 3, 4]);
            const h1 = BufferUtils.calculateHash(data, 'djb2');
            const h2 = BufferUtils.calculateHash(data, 'djb2');
            expect(h1).toBe(h2);
        });

        it('different algorithms produce different results', () => {
            const data = new Uint8Array([1, 2, 3, 4]);
            const fnv = BufferUtils.calculateHash(data, 'fnv1a');
            const djb = BufferUtils.calculateHash(data, 'djb2');
            expect(fnv).not.toBe(djb);
        });

        it('default algorithm is fnv1a', () => {
            const data = new Uint8Array([5, 6, 7]);
            expect(BufferUtils.calculateHash(data)).toBe(BufferUtils.calculateHash(data, 'fnv1a'));
        });
    });

    describe('calculateCrc32()', () => {
        it('known test vector: "123456789"', () => {
            const data = new TextEncoder().encode('123456789');
            expect(BufferUtils.calculateCrc32(data)).toBe(0xcbf43926);
        });

        it('empty data returns 0', () => {
            expect(BufferUtils.calculateCrc32(new Uint8Array(0))).toBe(0);
        });

        it('is deterministic', () => {
            const data = new Uint8Array([0xde, 0xad, 0xbe, 0xef]);
            expect(BufferUtils.calculateCrc32(data)).toBe(BufferUtils.calculateCrc32(data));
        });
    });

    describe('getCrc32Table()', () => {
        it('returns 256-entry table', () => {
            const table = BufferUtils.getCrc32Table();
            expect(table.length).toBe(256);
            expect(table).toBeInstanceOf(Uint32Array);
        });

        it('returns same instance on subsequent calls', () => {
            expect(BufferUtils.getCrc32Table()).toBe(BufferUtils.getCrc32Table());
        });
    });

    describe('isPowerOfTwo()', () => {
        it('0 is not power of two', () => {
            expect(BufferUtils.isPowerOfTwo(0)).toBe(false);
        });

        it('1 is power of two', () => {
            expect(BufferUtils.isPowerOfTwo(1)).toBe(true);
        });

        it('2 is power of two', () => {
            expect(BufferUtils.isPowerOfTwo(2)).toBe(true);
        });

        it('3 is not power of two', () => {
            expect(BufferUtils.isPowerOfTwo(3)).toBe(false);
        });

        it('4 is power of two', () => {
            expect(BufferUtils.isPowerOfTwo(4)).toBe(true);
        });

        it('1024 is power of two', () => {
            expect(BufferUtils.isPowerOfTwo(1024)).toBe(true);
        });

        it('negative numbers are not power of two', () => {
            expect(BufferUtils.isPowerOfTwo(-4)).toBe(false);
        });
    });

    describe('nextPowerOfTwo()', () => {
        it('0 returns 1', () => {
            expect(BufferUtils.nextPowerOfTwo(0)).toBe(1);
        });

        it('1 returns 1', () => {
            expect(BufferUtils.nextPowerOfTwo(1)).toBe(1);
        });

        it('exact power returns itself', () => {
            expect(BufferUtils.nextPowerOfTwo(4)).toBe(4);
            expect(BufferUtils.nextPowerOfTwo(16)).toBe(16);
        });

        it('non-power rounds up', () => {
            expect(BufferUtils.nextPowerOfTwo(3)).toBe(4);
            expect(BufferUtils.nextPowerOfTwo(5)).toBe(8);
            expect(BufferUtils.nextPowerOfTwo(100)).toBe(128);
        });
    });

    describe('alignTo()', () => {
        it('aligns value to alignment boundary', () => {
            expect(BufferUtils.alignTo(5, 4)).toBe(8);
            expect(BufferUtils.alignTo(8, 4)).toBe(8);
            expect(BufferUtils.alignTo(0, 8)).toBe(0);
        });

        it('throws for non-power-of-2 alignment', () => {
            expect(() => BufferUtils.alignTo(5, 3)).toThrow('Alignment must be a power of 2');
        });
    });

    describe('compareBytes()', () => {
        it('equal arrays return 0', () => {
            const a = new Uint8Array([1, 2, 3]);
            const b = new Uint8Array([1, 2, 3]);
            expect(BufferUtils.compareBytes(a, b)).toBe(0);
        });

        it('less returns negative', () => {
            const a = new Uint8Array([1, 2, 3]);
            const b = new Uint8Array([1, 3, 3]);
            expect(BufferUtils.compareBytes(a, b)).toBeLessThan(0);
        });

        it('greater returns positive', () => {
            const a = new Uint8Array([2, 2, 3]);
            const b = new Uint8Array([1, 2, 3]);
            expect(BufferUtils.compareBytes(a, b)).toBeGreaterThan(0);
        });

        it('different lengths: shorter is less', () => {
            const a = new Uint8Array([1, 2]);
            const b = new Uint8Array([1, 2, 3]);
            expect(BufferUtils.compareBytes(a, b)).toBeLessThan(0);
        });
    });

    describe('equalBytes()', () => {
        it('equal arrays return true', () => {
            const a = new Uint8Array([1, 2, 3]);
            const b = new Uint8Array([1, 2, 3]);
            expect(BufferUtils.equalBytes(a, b)).toBe(true);
        });

        it('different arrays return false', () => {
            const a = new Uint8Array([1, 2, 3]);
            const b = new Uint8Array([1, 2, 4]);
            expect(BufferUtils.equalBytes(a, b)).toBe(false);
        });

        it('different lengths return false', () => {
            const a = new Uint8Array([1, 2]);
            const b = new Uint8Array([1, 2, 3]);
            expect(BufferUtils.equalBytes(a, b)).toBe(false);
        });
    });

    describe('copyBytes()', () => {
        it('copies full source', () => {
            const src = new Uint8Array([1, 2, 3]);
            const dst = new Uint8Array(3);
            BufferUtils.copyBytes(src, dst);
            expect(Array.from(dst)).toEqual([1, 2, 3]);
        });

        it('copies with offsets', () => {
            const src = new Uint8Array([1, 2, 3, 4, 5]);
            const dst = new Uint8Array(5);
            BufferUtils.copyBytes(src, dst, 1, 2, 3);
            expect(Array.from(dst)).toEqual([0, 0, 2, 3, 4]);
        });
    });

    describe('fillBytes()', () => {
        it('fills entire array', () => {
            const target = new Uint8Array(4);
            BufferUtils.fillBytes(target, 0xff);
            expect(Array.from(target)).toEqual([0xff, 0xff, 0xff, 0xff]);
        });

        it('fills with offset and length', () => {
            const target = new Uint8Array(6);
            BufferUtils.fillBytes(target, 0xab, 1, 3);
            expect(Array.from(target)).toEqual([0, 0xab, 0xab, 0xab, 0, 0]);
        });
    });
});
