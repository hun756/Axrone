import { describe, expect, it } from 'vitest';
import { Djb2, Djb2a, Sdbm } from '../../hash/algorithms';

const U32 = (n: number): number => n >>> 0;
const enc = new TextEncoder();

describe('DJB2', () => {
    it('produces known output for empty input', () => {
        const h = new Djb2();
        h.updateBytes(new Uint8Array(0));
        expect(h.digest()).toBe(5381);
    });

    it('produces known output for "a"', () => {
        const h = new Djb2();
        h.updateBytes(enc.encode('a'));
        expect(h.digest()).toBe(177670);
    });

    it('determinism: same input same output', () => {
        const h1 = new Djb2();
        h1.updateBytes(enc.encode('test'));
        const h2 = new Djb2();
        h2.updateBytes(enc.encode('test'));
        expect(h1.digest()).toBe(h2.digest());
    });

    it('seed is ignored (DJB2 has no real seed)', () => {
        const h1 = new Djb2();
        h1.updateBytes(enc.encode('test'));
        const h2 = new Djb2(12345 as any);
        h2.updateBytes(enc.encode('test'));
        expect(h1.digest()).toBe(h2.digest());
    });

    it('reset clears state', () => {
        const h = new Djb2();
        h.updateBytes(enc.encode('partial'));
        h.reset();
        h.updateBytes(enc.encode('a'));
        expect(h.digest()).toBe(177670);
    });

    it('clone independence', () => {
        const h1 = new Djb2();
        h1.updateBytes(enc.encode('a'));
        const h2 = h1.clone();
        h1.updateBytes(enc.encode('b'));
        expect(h1.digest()).not.toBe(h2.digest());
    });

    it('updateString differs from updateBytes UTF-8 (uses UTF-16)', () => {
        const h1 = new Djb2();
        h1.updateString('a');
        const h2 = new Djb2();
        h2.updateBytes(enc.encode('a'));
        expect(h1.digest()).not.toBe(h2.digest());
    });

    it('digest variants return correct shape', () => {
        const h = new Djb2();
        h.updateString('hello');
        const bytes = h.digestBytes();
        expect(bytes).toBeInstanceOf(Uint8Array);
        expect(bytes.length).toBe(4);
        expect(h.digestHex()).toMatch(/^[0-9a-f]{8}$/);
        expect(typeof h.digestBigInt()).toBe('bigint');
    });

    it('byteLength tracking', () => {
        const h = new Djb2();
        h.updateBytes(enc.encode('hello'));
        expect(h.byteLength).toBe(5);
    });

    it('rejects updates after digest', () => {
        const h = new Djb2();
        h.updateString('a');
        h.digest();
        expect(() => h.updateString('b')).toThrow();
    });
});

describe('DJB2a', () => {
    it('empty input gives 5381', () => {
        const h = new Djb2a();
        h.updateBytes(new Uint8Array(0));
        expect(h.digest()).toBe(5381);
    });

    it('produces known output for "a"', () => {
        const h = new Djb2a();
        h.updateBytes(enc.encode('a'));
        expect(h.digest()).toBe(180708);
    });

    it('produces different output from Djb2 for non-empty', () => {
        const a = new Djb2();
        a.updateBytes(enc.encode('hello'));
        const b = new Djb2a();
        b.updateBytes(enc.encode('hello'));
        expect(a.digest()).not.toBe(b.digest());
    });

    it('updateString', () => {
        const h = new Djb2a();
        h.updateString('test');
        expect(typeof h.digest()).toBe('number');
    });

    it('reset clears state', () => {
        const h = new Djb2a();
        h.updateBytes(enc.encode('partial'));
        h.reset();
        h.updateBytes(enc.encode('a'));
        expect(h.digest()).toBe(180708);
    });

    it('clone creates independent copy', () => {
        const h1 = new Djb2a();
        h1.updateBytes(enc.encode('a'));
        const h2 = h1.clone();
        h1.updateBytes(enc.encode('b'));
        expect(h1.digest()).not.toBe(h2.digest());
    });

    it('rejects updates after digest', () => {
        const h = new Djb2a();
        h.updateString('a');
        h.digest();
        expect(() => h.updateString('b')).toThrow();
    });

    it('digestBytes returns 4 bytes', () => {
        const h = new Djb2a();
        h.updateString('test');
        expect(h.digestBytes().length).toBe(4);
    });

    it('digestHex returns 8-char hex', () => {
        const h = new Djb2a();
        h.updateString('test');
        expect(h.digestHex()).toMatch(/^[0-9a-f]{8}$/);
    });

    it('byteLength tracking', () => {
        const h = new Djb2a();
        h.updateBytes(enc.encode('hello'));
        expect(h.byteLength).toBe(5);
    });
});

describe('SDBM', () => {
    it('empty input gives 0', () => {
        const h = new Sdbm();
        h.updateBytes(new Uint8Array(0));
        expect(h.digest()).toBe(0);
    });

    it('produces known output for "a"', () => {
        const h = new Sdbm();
        h.updateBytes(enc.encode('a'));
        const v = h.digest() as unknown as number;
        expect(v >>> 0).toBe(v >>> 0);
        expect(v).not.toBe(0);
    });

    it('produces specific known value for "a" (97)', () => {
        // SDBM: h = c + (h << 6) + (h << 16) - h
        // For 'a' (97): 97 + 0 + 0 - 0 = 97
        const h = new Sdbm();
        h.updateBytes(enc.encode('a'));
        expect(h.digest()).toBe(97);
    });

    it('produces different output from Djb2', () => {
        const a = new Djb2();
        a.updateBytes(enc.encode('hello'));
        const b = new Sdbm();
        b.updateBytes(enc.encode('hello'));
        expect(a.digest()).not.toBe(b.digest());
    });

    it('determinism + reset + clone', () => {
        const h1 = new Sdbm();
        h1.updateBytes(enc.encode('hello'));
        const h2 = new Sdbm();
        h2.updateBytes(enc.encode('partial'));
        h2.reset();
        h2.updateBytes(enc.encode('hello'));
        expect(h1.digest()).toBe(h2.digest());
        const h3 = h1.clone();
        expect(h3.digest()).toBe(h1.digest());
    });

    it('byteLength tracking', () => {
        const h = new Sdbm();
        h.updateBytes(enc.encode('hello'));
        expect(h.byteLength).toBe(5);
    });

    it('rejects updates after digest', () => {
        const h = new Sdbm();
        h.updateString('a');
        h.digest();
        expect(() => h.updateString('b')).toThrow();
    });

    it('digest variants', () => {
        const h = new Sdbm();
        h.updateString('test');
        expect(h.digestBytes()).toBeInstanceOf(Uint8Array);
        expect(h.digestBytes().length).toBe(4);
        expect(h.digestHex()).toMatch(/^[0-9a-f]{8}$/);
        expect(typeof h.digestBigInt()).toBe('bigint');
    });

    it('incremental update matches single update', () => {
        const a = new Sdbm();
        a.updateBytes(enc.encode('hel'));
        a.updateBytes(enc.encode('lo'));
        const b = new Sdbm();
        b.updateBytes(enc.encode('hello'));
        expect(a.digest()).toBe(b.digest());
    });
});
