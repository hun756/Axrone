import { describe, expect, it } from 'vitest';
import {
    asHash32,
    asHash64,
    asHash128,
    asHash256,
    asHash512,
    asSeed32,
    asSeed64,
    asDigest32,
    asDigest64,
    asDigest128,
    asHashBytes,
    isHash32,
    isHash64,
    isHash128,
    isHash256,
    isSeed32,
    isSeed64,
} from '../hash/types';

describe('hash/types — branded types and type guards', () => {
    describe('asHash32', () => {
        it('accepts finite numbers', () => {
            expect(asHash32(0)).toBe(0);
            expect(asHash32(0xffffffff)).toBe(0xffffffff);
            expect(asHash32(12345)).toBe(12345);
        });

        it('truncates to 32-bit unsigned', () => {
            expect(asHash32(0x100000000)).toBe(0);
        });

        it('handles -1', () => {
            expect(asHash32(-1)).toBe(0xffffffff);
        });

        it('handles 0xffffffffffffffff', () => {
            expect(asHash32(0xffffffffffffffff)).toBe(0xffffffff);
        });

        it('rejects non-finite values', () => {
            expect(() => asHash32(NaN)).toThrow(TypeError);
            expect(() => asHash32(Infinity)).toThrow(TypeError);
            expect(() => asHash32(-Infinity)).toThrow(TypeError);
        });
    });

    describe('asHash64', () => {
        it('accepts bigint values', () => {
            expect(asHash64(0n)).toBe(0n);
            expect(asHash64(0xffffffffffffffffn)).toBe(0xffffffffffffffffn);
            expect(asHash64(0x1234567890abcdefn)).toBe(0x1234567890abcdefn);
        });

        it('accepts finite number values', () => {
            expect(asHash64(0)).toBe(0n);
            expect(asHash64(42)).toBe(42n);
        });

        it('masks to 64-bit', () => {
            expect(asHash64(0x1ffffffffffffffffn)).toBe(0xffffffffffffffffn);
        });

        it('rejects negative bigint', () => {
            expect(() => asHash64(-1n)).toThrow(TypeError);
        });

        it('rejects non-finite numbers', () => {
            expect(() => asHash64(NaN)).toThrow(TypeError);
            expect(() => asHash64(Infinity)).toThrow(TypeError);
        });
    });

    describe('asHash128/256/512', () => {
        it('asHash128 masks to 128-bit', () => {
            expect(asHash128(0xffffffffffffffffffffffffffffffffn)).toBe(0xffffffffffffffffffffffffffffffffn);
            expect(asHash128(0x1ffffffffffffffffffffffffffffffffn)).toBe(0xffffffffffffffffffffffffffffffffn);
        });

        it('asHash256 masks to 256-bit', () => {
            const max256 = (1n << 256n) - 1n;
            expect(asHash256(max256)).toBe(max256);
            expect(asHash256((1n << 257n) - 1n)).toBe(max256);
        });

        it('asHash512 passes through bigint', () => {
            expect(asHash512(0n)).toBe(0n);
            expect(asHash512(0xdeadbeefcafebaben)).toBe(0xdeadbeefcafebaben);
        });

        it('rejects negative', () => {
            expect(() => asHash128(-1n)).toThrow(TypeError);
            expect(() => asHash256(-1n)).toThrow(TypeError);
        });
    });

    describe('asSeed* — branded seed types', () => {
        it('asSeed32 accepts number', () => {
            expect(asSeed32(0)).toBe(0);
            expect(asSeed32(0xffffffff)).toBe(0xffffffff);
        });

        it('asSeed64 accepts bigint and number', () => {
            expect(asSeed64(0n)).toBe(0n);
            expect(asSeed64(42)).toBe(42n);
        });

        it('rejects invalid', () => {
            expect(() => asSeed32(NaN)).toThrow(TypeError);
            expect(() => asSeed64(-1n)).toThrow(TypeError);
        });
    });

    describe('asDigest*', () => {
        it('asDigest32 from number', () => {
            expect(asDigest32(0xdeadbeef)).toBe(0xdeadbeef);
        });

        it('asDigest64 from bigint and number', () => {
            expect(asDigest64(0xcafebaben)).toBe(0xcafebaben);
            expect(asDigest64(0)).toBe(0n);
        });

        it('asDigest128 from bigint', () => {
            expect(asDigest128(0x1234567890abcdefn)).toBe(0x1234567890abcdefn);
        });
    });

    describe('asHashBytes', () => {
        it('accepts BytesLike', () => {
            const u = new Uint8Array([1, 2, 3]);
            const result = asHashBytes(u);
            expect(result).toBe(u);
        });

        it('accepts plain arrays', () => {
            const arr = [1, 2, 3];
            const result = asHashBytes(arr);
            expect(result).toBe(arr);
        });
    });

    describe('isHash* — type guards', () => {
        it('isHash32', () => {
            expect(isHash32(0)).toBe(true);
            expect(isHash32(0xffffffff)).toBe(true);
            expect(isHash32(0x100000000)).toBe(false);
            expect(isHash32(-1)).toBe(false);
            expect(isHash32(NaN)).toBe(false);
            expect(isHash32(Infinity)).toBe(false);
            expect(isHash32('123')).toBe(false);
            expect(isHash32(null)).toBe(false);
            expect(isHash32(undefined)).toBe(false);
            expect(isHash32({})).toBe(false);
        });

        it('isHash64', () => {
            expect(isHash64(0n)).toBe(true);
            expect(isHash64(0xffffffffffffffffn)).toBe(true);
            expect(isHash64(-1n)).toBe(false);
            expect(isHash64(0x1ffffffffffffffffn)).toBe(false);
            expect(isHash64(123)).toBe(false);
            expect(isHash64('abc')).toBe(false);
            expect(isHash64(null)).toBe(false);
        });

        it('isHash128', () => {
            expect(isHash128(0n)).toBe(true);
            expect(isHash128((1n << 128n) - 1n)).toBe(true);
            expect(isHash128(1n << 128n)).toBe(false);
            expect(isHash128(-1n)).toBe(false);
            expect(isHash128(123)).toBe(false);
        });

        it('isHash256', () => {
            expect(isHash256(0n)).toBe(true);
            expect(isHash256((1n << 256n) - 1n)).toBe(true);
            expect(isHash256(1n << 256n)).toBe(false);
        });

        it('isSeed32', () => {
            expect(isSeed32(0)).toBe(true);
            expect(isSeed32(0xffffffff)).toBe(true);
            expect(isSeed32(0x100000000)).toBe(false);
            expect(isSeed32(NaN)).toBe(false);
        });

        it('isSeed64', () => {
            expect(isSeed64(0n)).toBe(true);
            expect(isSeed64(0xffffffffffffffffn)).toBe(true);
            expect(isSeed64(-1n)).toBe(false);
        });
    });
});
