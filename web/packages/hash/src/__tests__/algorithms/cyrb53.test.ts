import { describe, expect, it } from 'vitest';
import { Cyrb53 } from '../../hash/algorithms';

const enc = new TextEncoder();

describe('Cyrb53', () => {
    it('empty string returns specific value', () => {
        const h = new Cyrb53();
        h.updateString('');
        expect((h.digest() as unknown as bigint)).toBe(0xc14d96711aee8d83n);
    });

    it('"a"', () => {
        const h = new Cyrb53();
        h.updateString('a');
        expect((h.digest() as unknown as bigint)).toBe(0x5c5652937e108e72n);
    });

    it('"abc"', () => {
        const h = new Cyrb53();
        h.updateString('abc');
        expect((h.digest() as unknown as bigint)).toBe(0xec4f695ea335eacdn);
    });

    it('"Hello, world!"', () => {
        const h = new Cyrb53();
        h.updateString('Hello, world!');
        expect((h.digest() as unknown as bigint)).toBe(0x7858572c49676fcdn);
    });

    it('determinism', () => {
        const a = new Cyrb53();
        a.updateString('test');
        const b = new Cyrb53();
        b.updateString('test');
        expect((a.digest() as unknown as bigint)).toBe((b.digest() as unknown as bigint));
    });

    it('different seeds give different output', () => {
        const a = new Cyrb53();
        a.updateString('test');
        const b = new Cyrb53(12345);
        b.updateString('test');
        expect((a.digest() as unknown as bigint)).not.toBe((b.digest() as unknown as bigint));
    });

    it('reset', () => {
        const h = new Cyrb53();
        h.updateString('partial');
        h.reset();
        h.updateString('a');
        expect((h.digest() as unknown as bigint)).toBe(0x5c5652937e108e72n);
    });

    it('digestBytes returns 8 bytes', () => {
        const h = new Cyrb53();
        h.updateString('test');
        expect(h.digestBytes().length).toBe(8);
    });

    it('digestHex returns 16-char hex', () => {
        const h = new Cyrb53();
        h.updateString('test');
        expect(h.digestHex()).toMatch(/^[0-9a-f]{16}$/);
    });

    it('rejects updates after digest', () => {
        const h = new Cyrb53();
        h.updateString('a');
        h.digest();
        expect(() => h.updateString('b')).toThrow();
    });
});
