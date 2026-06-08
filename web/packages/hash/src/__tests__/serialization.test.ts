import { describe, expect, it } from 'vitest';
import {
    hash32ToHex,
    hash64ToHex,
    bytesToBase64,
    base64ToBytes,
    toBase64Url,
    fromBase64Url,
    bytesToHash32,
    bytesToHash64,
    hash32ToBase64,
    hash64ToBase64,
    base64ToHash32,
    base64ToHash64,
} from '../../hash/serialization';
import { asHash32, asHash64 } from '../../hash/types';

describe('hex serialization', () => {
    it('hash32ToHex', () => {
        expect(hash32ToHex(0xdeadbeef)).toBe('deadbeef');
        expect(hash32ToHex(0)).toBe('00000000');
        expect(hash32ToHex(0x01020304)).toBe('01020304');
    });

    it('hash32ToHex uppercase', () => {
        expect(hash32ToHex(0xabcdef01, true)).toBe('ABCDEF01');
    });

    it('hash64ToHex', () => {
        const h = 0x0123456789abcdefn;
        expect(hash64ToHex(h)).toBe('0123456789abcdef');
    });

    it('hash64ToHex uppercase', () => {
        expect(hash64ToHex(0xabcdef0123456789n, true)).toBe('ABCDEF0123456789');
    });
});

describe('base64', () => {
    it('bytesToBase64 / base64ToBytes roundtrip', () => {
        const bytes = new Uint8Array([0xde, 0xad, 0xbe, 0xef]);
        const b64 = bytesToBase64(bytes);
        expect(b64).toBe('3q2+7w==');
        const back = base64ToBytes(b64);
        expect(Array.from(back)).toEqual(Array.from(bytes));
    });

    it('handles empty', () => {
        expect(bytesToBase64(new Uint8Array(0))).toBe('');
        expect(Array.from(base64ToBytes(''))).toEqual([]);
    });

    it('handles arbitrary bytes', () => {
        const bytes = new Uint8Array(256);
        for (let i = 0; i < 256; i++) bytes[i] = i;
        const back = base64ToBytes(bytesToBase64(bytes));
        expect(Array.from(back)).toEqual(Array.from(bytes));
    });
});

describe('base64url', () => {
    it('toBase64Url replaces + and /', () => {
        const bytes = new Uint8Array([0xfb, 0xff, 0xfe]);
        const s = toBase64Url(bytes);
        expect(s).not.toContain('+');
        expect(s).not.toContain('/');
    });

    it('fromBase64Url is inverse of toBase64Url', () => {
        const bytes = new Uint8Array(64);
        for (let i = 0; i < 64; i++) bytes[i] = i;
        const s = toBase64Url(bytes);
        const back = fromBase64Url(s);
        expect(Array.from(back)).toEqual(Array.from(bytes));
    });
});

describe('hash<->bytes', () => {
    it('bytesToHash32 / bytesToHash64', () => {
        const b = new Uint8Array([0xde, 0xad, 0xbe, 0xef]);
        const h = bytesToHash32(b);
        expect(h).toBe(0xdeadbeef);
    });

    it('bytesToHash64', () => {
        const b = new Uint8Array([0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef]);
        const h = bytesToHash64(b);
        expect(h).toBe(0x0123456789abcdefn);
    });

    it('bytesToHash32 throws for wrong length', () => {
        expect(() => bytesToHash32(new Uint8Array(3))).toThrow();
    });

    it('bytesToHash64 throws for wrong length', () => {
        expect(() => bytesToHash64(new Uint8Array(7))).toThrow();
    });
});

describe('hash<->base64', () => {
    it('hash32ToBase64 / base64ToHash32 roundtrip', () => {
        const h = 0xdeadbeef;
        const b64 = hash32ToBase64(h);
        const back = base64ToHash32(b64);
        expect(back).toBe(h);
    });

    it('hash64ToBase64 / base64ToHash64 roundtrip', () => {
        const h = 0x0123456789abcdefn;
        const b64 = hash64ToBase64(h);
        const back = base64ToHash64(b64);
        expect(back).toBe(h);
    });
});
