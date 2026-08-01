import { describe, expect, it } from 'vitest';
import {
    hash128ToHex,
    hash256ToHex,
    hash512ToHex,
    hexToHash32,
    hexToHash64,
    hexToHash128,
    hashToBytes,
    bytesToHash32,
    bytesToHash64,
    hash32ToHex,
    hash64ToHex,
} from '../hash/serialization';
import { asHash32, asHash64, asHash128, asHash256 } from '../hash/types';
import { HashDeserializationError } from '../hash/errors';

describe('serialization gaps — hash128/256/512 ToHex', () => {
    it('hash128ToHex produces 32-char lowercase hex', () => {
        const v = asHash128(0x0123456789abcdef0123456789abcdefn);
        expect(hash128ToHex(v)).toBe('0123456789abcdef0123456789abcdef');
    });

    it('hash128ToHex uppercase', () => {
        const v = asHash128(0xabcdef0123456789abcdef0123456789n);
        expect(hash128ToHex(v, true)).toBe('ABCDEF0123456789ABCDEF0123456789');
    });

    it('hash128ToHex zero', () => {
        expect(hash128ToHex(asHash128(0n))).toBe('00000000000000000000000000000000');
    });

    it('hash256ToHex produces 64-char lowercase hex', () => {
        const v = asHash256(0x0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdefn);
        expect(hash256ToHex(v)).toBe('0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef');
    });

    it('hash256ToHex uppercase', () => {
        const v = asHash256(0xabcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789n);
        expect(hash256ToHex(v, true)).toBe('ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789');
    });

    it('hash256ToHex zero', () => {
        expect(hash256ToHex(asHash256(0n))).toBe('0'.repeat(64));
    });

    it('hash512ToHex produces 128-char lowercase hex', () => {
        const val = (1n << 512n) - 1n; // all 1s for 512 bits
        const hex = hash512ToHex(val as any);
        expect(hex.length).toBe(128);
        expect(hex).toBe('f'.repeat(128));
    });

    it('hash512ToHex zero', () => {
        expect(hash512ToHex(0n as any)).toBe('0'.repeat(128));
    });

    it('hash512ToHex uppercase', () => {
        const val = (1n << 512n) - 1n;
        const hex = hash512ToHex(val as any, true);
        expect(hex).toBe('F'.repeat(128));
    });
});

describe('serialization gaps — hexToHash32', () => {
    it('round-trips with hash32ToHex', () => {
        const original = asHash32(0xdeadbeef);
        const hex = hash32ToHex(original);
        const back = hexToHash32(hex);
        expect(back).toBe(original);
    });

    it('parses known hex string', () => {
        expect(hexToHash32('01020304') as unknown as number).toBe(0x01020304);
    });

    it('parses uppercase hex', () => {
        expect(hexToHash32('ABCDEF01') as unknown as number).toBe(0xabcdef01);
    });

    it('throws on wrong length', () => {
        expect(() => hexToHash32('abc')).toThrow(HashDeserializationError);
        expect(() => hexToHash32('abc')).toThrow(/8 hex chars/);
    });

    it('throws on invalid hex char', () => {
        expect(() => hexToHash32('0102030g')).toThrow(HashDeserializationError);
        expect(() => hexToHash32('0102030g')).toThrow(/invalid hex char/);
    });

    it('throws on too-long input', () => {
        expect(() => hexToHash32('0102030405')).toThrow(HashDeserializationError);
    });
});

describe('serialization gaps — hexToHash64', () => {
    it('round-trips with hash64ToHex', () => {
        const original = asHash64(0x0123456789abcdefn);
        const hex = hash64ToHex(original);
        const back = hexToHash64(hex);
        expect(back).toBe(original);
    });

    it('parses known hex string', () => {
        expect(hexToHash64('0123456789abcdef')).toBe(0x0123456789abcdefn);
    });

    it('throws on wrong length', () => {
        expect(() => hexToHash64('abc')).toThrow(HashDeserializationError);
        expect(() => hexToHash64('abc')).toThrow(/16 hex chars/);
    });

    it('throws on invalid hex char', () => {
        expect(() => hexToHash64('0123456789abcdeg')).toThrow(HashDeserializationError);
    });
});

describe('serialization gaps — hexToHash128', () => {
    it('round-trips with hash128ToHex', () => {
        const original = asHash128(0x0123456789abcdef0123456789abcdefn);
        const hex = hash128ToHex(original);
        const back = hexToHash128(hex);
        expect(back).toBe(original);
    });

    it('throws on wrong length', () => {
        expect(() => hexToHash128('abc')).toThrow(HashDeserializationError);
        expect(() => hexToHash128('abc')).toThrow(/32 hex chars/);
    });

    it('throws on invalid hex char', () => {
        expect(() => hexToHash128('0123456789abcdef0123456789abcdeX')).toThrow(HashDeserializationError);
    });
});

describe('serialization gaps — hashToBytes', () => {
    it('converts Hash32 (number) to 4 bytes LE', () => {
        const bytes = hashToBytes(asHash32(0xdeadbeef));
        expect(bytes).toBeInstanceOf(Uint8Array);
        expect(bytes.length).toBe(4);
        expect(Array.from(bytes)).toEqual([0xef, 0xbe, 0xad, 0xde]);
    });

    it('converts Hash64 (bigint) to 8 bytes LE', () => {
        const bytes = hashToBytes(asHash64(0x0102030405060708n));
        expect(bytes).toBeInstanceOf(Uint8Array);
        expect(bytes.length).toBe(8);
        expect(Array.from(bytes)).toEqual([0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01]);
    });

    it('zero Hash32 gives all-zero bytes', () => {
        const bytes = hashToBytes(asHash32(0));
        expect(Array.from(bytes)).toEqual([0, 0, 0, 0]);
    });
});

describe('serialization gaps — bytesToHash32/64 with offset', () => {
    it('bytesToHash32 reads at offset', () => {
        const bytes = new Uint8Array([0xff, 0xff, 0xef, 0xbe, 0xad, 0xde]);
        const h = bytesToHash32(bytes, 2);
        expect(h as unknown as number).toBe(0xdeadbeef);
    });

    it('bytesToHash32 throws when offset leaves insufficient bytes', () => {
        const bytes = new Uint8Array([0xef, 0xbe, 0xad]);
        expect(() => bytesToHash32(bytes, 1)).toThrow(HashDeserializationError);
    });

    it('bytesToHash64 reads at offset', () => {
        const bytes = new Uint8Array([0xff, 0xff, 0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01]);
        const h = bytesToHash64(bytes, 2);
        expect(h).toBe(0x0102030405060708n);
    });

    it('bytesToHash64 throws when offset leaves insufficient bytes', () => {
        const bytes = new Uint8Array(10);
        expect(() => bytesToHash64(bytes, 5)).toThrow(HashDeserializationError);
    });
});
