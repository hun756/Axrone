import { describe, expect, it } from 'vitest';
import {
    float32ToBits,
    bitsToFloat32,
    float64ToBitsPair,
    bitsPairToFloat64,
    rotl32,
    rotr32,
    rotl64,
    rotr64,
    readU32LE,
    readU32BE,
    writeU32LE,
    writeU32BE,
    readU64LE,
    readU64BE,
    writeU64LE,
    writeU64BE,
    readU16LE,
    readU16BE,
    writeU16LE,
    writeU16BE,
} from '../hash/bits';

describe('hash/bits — bit/byte operations', () => {
    describe('float32ToBits / bitsToFloat32', () => {
        it('round-trips finite numbers', () => {
            for (const v of [0, 1, -1, 0.5, -0.5, 1.5, -1.5, 3.14159, -3.14159, 1e10, -1e10, 1e-10, 100, -100]) {
                const bits = float32ToBits(v);
                const back = bitsToFloat32(bits);
                expect(back).toBe(v);
            }
        });

        it('handles special floats', () => {
            const posInf = float32ToBits(Infinity);
            const negInf = float32ToBits(-Infinity);
            expect(posInf).toBe(0x7f800000);
            expect(negInf).toBe(0xff800000);
            const nanBits = float32ToBits(NaN);
            expect(nanBits & 0x7f800000).toBe(0x7f800000);
            expect(nanBits & 0x007fffff).not.toBe(0);
        });

        it('handles zero and -zero', () => {
            expect(float32ToBits(0)).toBe(0);
            expect(float32ToBits(-0)).toBe(0x80000000);
        });

        it('handles small subnormal numbers', () => {
            const tiny = 1.4e-45;
            expect(bitsToFloat32(float32ToBits(tiny))).toBe(tiny);
        });

        it('produces known IEEE 754 representations', () => {
            expect(float32ToBits(1.0)).toBe(0x3f800000);
            expect(float32ToBits(2.0)).toBe(0x40000000);
            expect(float32ToBits(0.5)).toBe(0x3f000000);
            expect(float32ToBits(-0.0)).toBe(0x80000000);
        });
    });

    describe('float64ToBitsPair / bitsPairToFloat64', () => {
        it('round-trips finite numbers', () => {
            for (const v of [0, 1, -1, 0.5, -0.5, Math.PI, -Math.PI, Number.MAX_SAFE_INTEGER, 1e-300, 1e300]) {
                const [lo, hi] = float64ToBitsPair(v);
                const back = bitsPairToFloat64(lo, hi);
                expect(back).toBe(v);
            }
        });

        it('handles special values', () => {
            const [infLo, infHi] = float64ToBitsPair(Infinity);
            expect(infLo).toBe(0);
            expect(infHi).toBe(0x7ff00000);
            const [ninfLo, ninfHi] = float64ToBitsPair(-Infinity);
            expect(ninfLo).toBe(0);
            expect(ninfHi).toBe(0xfff00000);
        });

        it('produces known IEEE 754 representations', () => {
            const [lo, hi] = float64ToBitsPair(1.0);
            expect(lo).toBe(0);
            expect(hi).toBe(0x3ff00000);
        });
    });

    describe('rotl32 / rotr32', () => {
        it('rotates by 0 returns same', () => {
            expect(rotl32(0xdeadbeef, 0)).toBe(0xdeadbeef);
            expect(rotr32(0xdeadbeef, 0)).toBe(0xdeadbeef);
        });

        it('rotl32 by 8 = swap bytes', () => {
            expect(rotl32(0x12345678, 8)).toBe(0x34567812);
        });

        it('rotr32 by 8 = reverse byte swap', () => {
            expect(rotr32(0x12345678, 8)).toBe(0x78123456);
        });

        it('rotl32 by 16 = swap word pairs', () => {
            expect(rotl32(0x12345678, 16)).toBe(0x56781234);
        });

        it('rotl32/rotr32 are inverses', () => {
            for (const n of [0, 1, 0x7f, 0xff, 0x1234, 0xffff, 0xdeadbeef, 0xffffffff]) {
                for (let r = 0; r < 32; r++) {
                    expect(rotr32(rotl32(n, r), r)).toBe(n);
                }
            }
        });

        it('rotl32 by 32 = identity', () => {
            expect(rotl32(0x12345678, 32)).toBe(0x12345678);
        });
    });

    describe('rotl64 / rotr64', () => {
        it('rotl64 rotates correctly', () => {
            const v = 0x123456789abcdef0n;
            expect(rotl64(v, 0n)).toBe(v);
            expect(rotl64(v, 64n)).toBe(v);
        });

        it('rotl64/rotr64 are inverses', () => {
            const v = 0xdeadbeefcafebaben;
            for (let r = 0n; r < 64n; r += 8n) {
                expect(rotr64(rotl64(v, r), r)).toBe(v);
            }
        });

        it('rotl64 by 8', () => {
            const v = 0x0102030405060708n;
            expect(rotl64(v, 8n)).toBe(0x0203040506070801n);
        });
    });

    describe('readU32LE / readU32BE', () => {
        it('reads little-endian correctly', () => {
            const bytes = new Uint8Array([0x78, 0x56, 0x34, 0x12]);
            expect(readU32LE(bytes, 0)).toBe(0x12345678);
        });

        it('reads big-endian correctly', () => {
            const bytes = new Uint8Array([0x12, 0x34, 0x56, 0x78]);
            expect(readU32BE(bytes, 0)).toBe(0x12345678);
        });

        it('respects offset', () => {
            const bytes = new Uint8Array([0xff, 0xff, 0x78, 0x56, 0x34, 0x12]);
            expect(readU32LE(bytes, 2)).toBe(0x12345678);
            expect(readU32BE(bytes, 2)).toBe(0x78563412);
        });

        it('works with plain arrays', () => {
            const arr: number[] = [0x78, 0x56, 0x34, 0x12];
            expect(readU32LE(arr, 0)).toBe(0x12345678);
        });
    });

    describe('writeU32LE / writeU32BE', () => {
        it('writes little-endian correctly', () => {
            const out = new Uint8Array(4);
            writeU32LE(0x12345678, out, 0);
            expect(Array.from(out)).toEqual([0x78, 0x56, 0x34, 0x12]);
        });

        it('writes big-endian correctly', () => {
            const out = new Uint8Array(4);
            writeU32BE(0x12345678, out, 0);
            expect(Array.from(out)).toEqual([0x12, 0x34, 0x56, 0x78]);
        });

        it('respects offset', () => {
            const out = new Uint8Array(6);
            writeU32LE(0x12345678, out, 2);
            expect(Array.from(out)).toEqual([0, 0, 0x78, 0x56, 0x34, 0x12]);
        });

        it('round-trips with read', () => {
            const out = new Uint8Array(4);
            writeU32LE(0xdeadbeef, out, 0);
            expect(readU32LE(out, 0)).toBe(0xdeadbeef);
            const out2 = new Uint8Array(4);
            writeU32BE(0xcafebabe, out2, 0);
            expect(readU32BE(out2, 0)).toBe(0xcafebabe);
        });
    });

    describe('readU64LE / readU64BE', () => {
        it('reads little-endian 64-bit', () => {
            const bytes = new Uint8Array([0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08]);
            expect(readU64LE(bytes, 0)).toBe(0x0807060504030201n);
        });

        it('reads big-endian 64-bit', () => {
            const bytes = new Uint8Array([0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08]);
            expect(readU64BE(bytes, 0)).toBe(0x0102030405060708n);
        });

        it('handles offset', () => {
            const bytes = new Uint8Array([0xff, 0xff, 0xff, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08]);
            expect(readU64LE(bytes, 3)).toBe(0x0807060504030201n);
        });
    });

    describe('writeU64LE / writeU64BE', () => {
        it('writes little-endian 64-bit', () => {
            const out = new Uint8Array(8);
            writeU64LE(0x0102030405060708n, out, 0);
            expect(Array.from(out)).toEqual([0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01]);
        });

        it('writes big-endian 64-bit', () => {
            const out = new Uint8Array(8);
            writeU64BE(0x0102030405060708n, out, 0);
            expect(Array.from(out)).toEqual([0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08]);
        });

        it('round-trips', () => {
            const out = new Uint8Array(8);
            writeU64LE(0xdeadbeefcafebaben, out, 0);
            expect(readU64LE(out, 0)).toBe(0xdeadbeefcafebaben);
        });
    });

    describe('readU16LE / readU16BE', () => {
        it('reads little-endian 16-bit', () => {
            expect(readU16LE(new Uint8Array([0x34, 0x12]), 0)).toBe(0x1234);
        });

        it('reads big-endian 16-bit', () => {
            expect(readU16BE(new Uint8Array([0x12, 0x34]), 0)).toBe(0x1234);
        });
    });

    describe('writeU16LE / writeU16BE', () => {
        it('writes little-endian 16-bit', () => {
            const out = new Uint8Array(2);
            writeU16LE(0x1234, out, 0);
            expect(Array.from(out)).toEqual([0x34, 0x12]);
        });

        it('writes big-endian 16-bit', () => {
            const out = new Uint8Array(2);
            writeU16BE(0x1234, out, 0);
            expect(Array.from(out)).toEqual([0x12, 0x34]);
        });

        it('round-trips', () => {
            const out = new Uint8Array(2);
            writeU16LE(0xbeef, out, 0);
            expect(readU16LE(out, 0)).toBe(0xbeef);
        });
    });
});
