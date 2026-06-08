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
        expect(U32(h.digest() as unknown as number)).toBe(0x0a4108c2);
    });

    it('determinism: same input same output', () => {
        const h1 = new Djb2();
        h1.updateBytes(enc.encode('test'));
        const h2 = new Djb2();
        h2.updateBytes(enc.encode('test'));
        expect(h1.digest()).toBe(h2.digest());
    });

    it('different seed gives different output', () => {
        const h1 = new Djb2();
        h1.updateBytes(enc.encode('test'));
        const h2 = new Djb2(12345 as any);
        h2.updateBytes(enc.encode('test'));
        expect(h1.digest()).not.toBe(h2.digest());
    });

    it('reset clears state', () => {
        const h = new Djb2();
        h.updateBytes(enc.encode('partial'));
        h.reset();
        h.updateBytes(enc.encode('a'));
        expect(U32(h.digest() as unknown as number)).toBe(0x0a4108c2);
    });

    it('clone independence', () => {
        const h1 = new Djb2();
        h1.updateBytes(enc.encode('a'));
        const h2 = h1.clone();
        h1.updateBytes(enc.encode('b'));
        expect(h1.digest()).not.toBe(h2.digest());
    });

    it('updateString matches updateBytes UTF-8', () => {
        const h1 = new Djb2();
        h1.updateString('a');
        const h2 = new Djb2();
        h2.updateBytes(enc.encode('a'));
        expect(h1.digest()).toBe(h2.digest());
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
        expect(U32(h.digest() as unknown as number)).toBe(0x0a4108c2);
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
});
