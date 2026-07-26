import { describe, expect, it } from 'vitest';
import { Sha1, Sha256, Sha384, Sha512 } from '../../hash/algorithms';

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
});
