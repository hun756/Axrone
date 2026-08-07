import { describe, expect, it } from 'vitest';
import { Sha1, Sha256, Sha384, Sha512 } from '../../hash/algorithms';
import { isWebCryptoAvailable } from '../../hash/algorithms/crypto/sha';

const enc = new TextEncoder();
const hexFromBytes = (b: Uint8Array): string => Array.from(b, (x) => x.toString(16).padStart(2, '0')).join('');

describe('SHA-1', () => {
    it('empty string', async () => {
        const h = new Sha1();
        h.updateBytes(new Uint8Array(0));
        await (h as any).digestAsync();
        expect(h.digestHex()).toBe('da39a3ee5e6b4b0d3255bfef95601890afd80709');
    });

    it('"abc"', async () => {
        const h = new Sha1();
        h.updateBytes(enc.encode('abc'));
        await (h as any).digestAsync();
        expect(h.digestHex()).toBe('a9993e364706816aba3e25717850c26c9cd0d89d');
    });

    it('"The quick brown fox jumps over the lazy dog"', async () => {
        const h = new Sha1();
        h.updateBytes(enc.encode('The quick brown fox jumps over the lazy dog'));
        await (h as any).digestAsync();
        expect(h.digestHex()).toBe('2fd4e1c67a2d28fced849ee1bb76e7391b93eb12');
    });

    it('digest throws synchronously', () => {
        const h = new Sha1();
        h.updateString('a');
        expect(() => h.digest()).toThrow();
    });

    it('digestBytes returns correct length after async digest', async () => {
        const h = new Sha1();
        h.updateString('a');
        await (h as any).digestAsync();
        expect(h.digestBytes().length).toBe(20);
    });

    it('rejects updates after digest', async () => {
        const h = new Sha1();
        h.updateString('a');
        await (h as any).digestAsync();
        expect(() => h.updateString('b')).toThrow();
    });

    it('reset clears state', async () => {
        const h = new Sha1();
        h.updateBytes(enc.encode('partial'));
        h.reset();
        h.updateBytes(enc.encode('abc'));
        await (h as any).digestAsync();
        expect(h.digestHex()).toBe('a9993e364706816aba3e25717850c26c9cd0d89d');
    });

    it('clone creates independent copy', async () => {
        const h1 = new Sha1();
        h1.updateBytes(enc.encode('abc'));
        const h2 = h1.clone();
        h1.updateBytes(enc.encode('def'));
        await (h1 as any).digestAsync();
        await (h2 as any).digestAsync();
        expect(h1.digestHex()).not.toBe(h2.digestHex());
    });

    it('updateBoolean tracks data', async () => {
        const a = new Sha256();
        a.updateBoolean(true);
        const b = new Sha256();
        b.updateBoolean(false);
        await (a as any).digestAsync();
        await (b as any).digestAsync();
        expect(a.digestHex()).not.toBe(b.digestHex());
    });

    it('updateU32 writes 4 bytes LE', async () => {
        const a = new Sha256();
        a.updateU32(0x61626300); // LE bytes: 00 63 62 61
        await (a as any).digestAsync();
        expect(a.byteLength).toBe(4);
    });

    it('updateAny dispatches correctly', async () => {
        const a = new Sha256();
        a.updateAny('abc');
        const b = new Sha256();
        b.updateString('abc');
        await (a as any).digestAsync();
        await (b as any).digestAsync();
        expect(a.digestHex()).toBe(b.digestHex());
    });

    it('digestBase64 returns valid base64', async () => {
        const h = new Sha256();
        h.updateBytes(enc.encode('abc'));
        await (h as any).digestAsync();
        const b64 = h.digestBase64();
        expect(typeof b64).toBe('string');
        expect(b64.length).toBeGreaterThan(0);
    });

    it('digestBigInt returns bigint', async () => {
        const h = new Sha256();
        h.updateBytes(enc.encode('abc'));
        await (h as any).digestAsync();
        expect(typeof h.digestBigInt()).toBe('bigint');
    });

    it('digestAsync caches result', async () => {
        const h = new Sha256();
        h.updateBytes(enc.encode('abc'));
        const r1 = await (h as any).digestAsync();
        const r2 = await (h as any).digestAsync();
        expect(r1).toBe(r2);
    });

    it('digestBytes throws before digestAsync', () => {
        const h = new Sha256();
        h.updateBytes(enc.encode('abc'));
        expect(() => h.digestBytes()).toThrow(/digestAsync/);
    });
});

describe('SHA-256', () => {
    it('empty string', async () => {
        const h = new Sha256();
        h.updateBytes(new Uint8Array(0));
        await (h as any).digestAsync();
        expect(h.digestHex()).toBe('e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855');
    });

    it('"abc"', async () => {
        const h = new Sha256();
        h.updateBytes(enc.encode('abc'));
        await (h as any).digestAsync();
        expect(h.digestHex()).toBe('ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad');
    });

    it('incremental', async () => {
        const h = new Sha256();
        h.updateBytes(enc.encode('ab'));
        h.updateBytes(enc.encode('c'));
        await (h as any).digestAsync();
        expect(h.digestHex()).toBe('ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad');
    });

    it('digestBytes returns 32 bytes', async () => {
        const h = new Sha256();
        h.updateString('a');
        await (h as any).digestAsync();
        expect(h.digestBytes().length).toBe(32);
    });

    it('reset clears state', async () => {
        const h = new Sha256();
        h.updateBytes(enc.encode('partial'));
        h.reset();
        h.updateBytes(enc.encode('abc'));
        await (h as any).digestAsync();
        expect(h.digestHex()).toBe('ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad');
    });

    it('clone creates independent copy', async () => {
        const h1 = new Sha256();
        h1.updateBytes(enc.encode('abc'));
        const h2 = h1.clone();
        h1.updateBytes(enc.encode('xyz'));
        await (h1 as any).digestAsync();
        await (h2 as any).digestAsync();
        expect(h1.digestHex()).not.toBe(h2.digestHex());
    });

    it('byteLength tracking', () => {
        const h = new Sha256();
        expect(h.byteLength).toBe(0);
        h.updateBytes(enc.encode('hello'));
        expect(h.byteLength).toBe(5);
    });

    it('metadata is correct', () => {
        const h = new Sha256();
        expect(h.metadata.name).toBe('sha-256');
        expect(h.metadata.outputSize).toBe(256);
        expect(h.metadata.cryptographicallySecure).toBe(true);
    });
});

describe('SHA-384', () => {
    it('empty string', async () => {
        const h = new Sha384();
        h.updateBytes(new Uint8Array(0));
        await (h as any).digestAsync();
        expect(h.digestHex()).toBe(
            '38b060a751ac96384cd9327eb1b1e36a21fdb71114be07434c0cc7bf63f6e1da274edebfe76f65fbd51ad2f14898b95b'
        );
    });

    it('"abc"', async () => {
        const h = new Sha384();
        h.updateBytes(enc.encode('abc'));
        await (h as any).digestAsync();
        expect(h.digestHex()).toBe(
            'cb00753f45a35e8bb5a03d699ac65007272c32ab0eded1631a8b605a43ff5bed8086072ba1e7cc2358baeca134c825a7'
        );
    });

    it('digestBytes returns 48 bytes', async () => {
        const h = new Sha384();
        h.updateString('a');
        await (h as any).digestAsync();
        expect(h.digestBytes().length).toBe(48);
    });

    it('reset clears state', async () => {
        const h = new Sha384();
        h.updateBytes(enc.encode('partial'));
        h.reset();
        h.updateBytes(enc.encode('abc'));
        await (h as any).digestAsync();
        expect(h.digestHex()).toBe(
            'cb00753f45a35e8bb5a03d699ac65007272c32ab0eded1631a8b605a43ff5bed8086072ba1e7cc2358baeca134c825a7'
        );
    });

    it('clone creates independent copy', async () => {
        const h1 = new Sha384();
        h1.updateBytes(enc.encode('abc'));
        const h2 = h1.clone();
        h1.updateBytes(enc.encode('xyz'));
        await (h1 as any).digestAsync();
        await (h2 as any).digestAsync();
        expect(h1.digestHex()).not.toBe(h2.digestHex());
    });
});

describe('SHA-512', () => {
    it('empty string', async () => {
        const h = new Sha512();
        h.updateBytes(new Uint8Array(0));
        await (h as any).digestAsync();
        expect(h.digestHex()).toBe(
            'cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3e'
        );
    });

    it('"abc"', async () => {
        const h = new Sha512();
        h.updateBytes(enc.encode('abc'));
        await (h as any).digestAsync();
        expect(h.digestHex()).toBe(
            'ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f'
        );
    });

    it('digestBytes returns 64 bytes', async () => {
        const h = new Sha512();
        h.updateString('a');
        await (h as any).digestAsync();
        expect(h.digestBytes().length).toBe(64);
    });

    it('reset clears state', async () => {
        const h = new Sha512();
        h.updateBytes(enc.encode('partial'));
        h.reset();
        h.updateBytes(enc.encode('abc'));
        await (h as any).digestAsync();
        expect(h.digestHex()).toBe(
            'ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f'
        );
    });

    it('clone creates independent copy', async () => {
        const h1 = new Sha512();
        h1.updateBytes(enc.encode('abc'));
        const h2 = h1.clone();
        h1.updateBytes(enc.encode('xyz'));
        await (h1 as any).digestAsync();
        await (h2 as any).digestAsync();
        expect(h1.digestHex()).not.toBe(h2.digestHex());
    });
});

describe('isWebCryptoAvailable', () => {
    it('returns boolean', () => {
        expect(typeof isWebCryptoAvailable()).toBe('boolean');
    });

    it('returns true in Node.js >= 15+ environment', () => {
        // In Node.js with WebCrypto, this should be true
        expect(isWebCryptoAvailable()).toBe(true);
    });
});

describe('SHA finalized state', () => {
    it('rejects updateBoolean after digestAsync', async () => {
        const h = new Sha256();
        h.updateBytes(enc.encode('abc'));
        await (h as any).digestAsync();
        expect(() => h.updateBoolean(true)).toThrow();
    });

    it('rejects updateI32 after digestAsync', async () => {
        const h = new Sha256();
        h.updateBytes(enc.encode('abc'));
        await (h as any).digestAsync();
        expect(() => h.updateI32(42)).toThrow();
    });

    it('rejects updateF32 after digestAsync', async () => {
        const h = new Sha256();
        h.updateBytes(enc.encode('abc'));
        await (h as any).digestAsync();
        expect(() => h.updateF32(1.5)).toThrow();
    });

    it('rejects updateF64 after digestAsync', async () => {
        const h = new Sha256();
        h.updateBytes(enc.encode('abc'));
        await (h as any).digestAsync();
        expect(() => h.updateF64(3.14)).toThrow();
    });

    it('rejects updateU32 after digestAsync', async () => {
        const h = new Sha256();
        h.updateBytes(enc.encode('abc'));
        await (h as any).digestAsync();
        expect(() => h.updateU32(42)).toThrow();
    });

    it('finalized property reflects state', async () => {
        const h = new Sha256();
        expect(h.finalized).toBe(false);
        h.updateBytes(enc.encode('abc'));
        await (h as any).digestAsync();
        expect(h.finalized).toBe(true);
    });
});
