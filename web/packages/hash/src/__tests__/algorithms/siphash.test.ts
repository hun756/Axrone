import { describe, expect, it } from 'vitest';
import { SipHash2_4 } from '../../hash/algorithms';

const enc = new TextEncoder();

describe('SipHash-2-4', () => {
    const key0to15 = (): Uint8Array => {
        const k = new Uint8Array(16);
        for (let i = 0; i < 16; i++) k[i] = i;
        return k;
    };

    it('empty input with 0..15 key', () => {
        const h = new SipHash2_4(key0to15());
        h.updateBytes(new Uint8Array(0));
        expect((h.digest() as unknown as bigint)).toBe(0x726fdb47dd0e0e31n);
    });

    it('"a" with 0..15 key', () => {
        const h = new SipHash2_4(key0to15());
        h.updateBytes(enc.encode('a'));
        expect((h.digest() as unknown as bigint)).toBe(0x2ba3e8e9a71148can);
    });

    it('"abc" with 0..15 key', () => {
        const h = new SipHash2_4(key0to15());
        h.updateBytes(enc.encode('abc'));
        expect((h.digest() as unknown as bigint)).toBe(0x5dbcfa53aa2007a5n);
    });

    it('"Hello, world!" with 0..15 key', () => {
        const h = new SipHash2_4(key0to15());
        h.updateBytes(enc.encode('Hello, world!'));
        expect((h.digest() as unknown as bigint)).toBe(0xa5e0f6923596b3a1n);
    });

    it('accepts short keys (zero-padded)', () => {
        const h = new SipHash2_4(new Uint8Array(8));
        h.updateBytes(enc.encode('a'));
        const v = h.digest() as unknown as bigint;
        expect(typeof v).toBe('bigint');
    });

    it('different keys produce different output', () => {
        const k1 = new Uint8Array(16);
        const k2 = new Uint8Array(16);
        for (let i = 0; i < 16; i++) {
            k1[i] = i;
            k2[i] = i + 1;
        }
        const a = new SipHash2_4(k1);
        a.updateBytes(enc.encode('test'));
        const b = new SipHash2_4(k2);
        b.updateBytes(enc.encode('test'));
        expect((a.digest() as unknown as bigint)).not.toBe((b.digest() as unknown as bigint));
    });

    it('determinism', () => {
        const a = new SipHash2_4(key0to15());
        a.updateBytes(enc.encode('test'));
        const b = new SipHash2_4(key0to15());
        b.updateBytes(enc.encode('test'));
        expect((a.digest() as unknown as bigint)).toBe((b.digest() as unknown as bigint));
    });

    it('digestBytes returns 8 bytes', () => {
        const h = new SipHash2_4(key0to15());
        h.updateString('test');
        expect(h.digestBytes().length).toBe(8);
    });

    it('digestHex returns 16-char hex', () => {
        const h = new SipHash2_4(key0to15());
        h.updateString('test');
        expect(h.digestHex()).toMatch(/^[0-9a-f]{16}$/);
    });

    it('rejects updates after digest', () => {
        const h = new SipHash2_4(key0to15());
        h.updateString('a');
        h.digest();
        expect(() => h.updateString('b')).toThrow();
    });
});
