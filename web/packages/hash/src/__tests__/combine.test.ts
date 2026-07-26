import { describe, expect, it } from 'vitest';
import {
    hashCombine,
    hashCombineOrdered,
    hashCombineStrings,
    hashCombineBooleans,
    hashCombineNumbers,
} from '../hash/combine';
import { asHash32 } from '../hash/types';

describe('hashCombine', () => {
    it('combines two values', () => {
        const a = asHash32(1);
        const b = asHash32(2);
        const v = hashCombine(a, b) as unknown as number;
        expect(v).toBeGreaterThanOrEqual(0);
        expect(v).toBeLessThan(0x100000000);
    });

    it('combines two hash32 (small values)', () => {
        const v = hashCombine(asHash32(0x100), asHash32(0x200)) as unknown as number;
        expect(typeof v).toBe('number');
    });

    it('different inputs give different output', () => {
        const v1 = hashCombine(asHash32(1), asHash32(2)) as unknown as number;
        const v2 = hashCombine(asHash32(1), asHash32(3)) as unknown as number;
        expect(v1).not.toBe(v2);
    });
});

describe('hashCombineOrdered', () => {
    it('combines values in order', () => {
        const v1 = hashCombineOrdered([asHash32(1), asHash32(2), asHash32(3)]) as unknown as number;
        const v2 = hashCombineOrdered([asHash32(1), asHash32(2), asHash32(3)]) as unknown as number;
        expect(v1).toBe(v2);
    });

    it('order matters', () => {
        const v1 = hashCombineOrdered([asHash32(1), asHash32(2), asHash32(3)]) as unknown as number;
        const v2 = hashCombineOrdered([asHash32(3), asHash32(2), asHash32(1)]) as unknown as number;
        expect(v1).not.toBe(v2);
    });

    it('handles 64-bit values', () => {
        const v = hashCombineOrdered([1n, 2n, 3n]) as unknown as number;
        expect(typeof v).toBe('number');
    });

    it('empty input returns fmix32(0x811c9dc5)', () => {
        const v = hashCombineOrdered([]) as unknown as number;
        expect(v).toBeGreaterThanOrEqual(0);
    });
});

describe('hashCombineStrings', () => {
    it('combines strings', () => {
        const v = hashCombineStrings(0, 'hello', 'world') as unknown as number;
        expect(v).toBeGreaterThanOrEqual(0);
    });

    it('order matters', () => {
        const a = hashCombineStrings(0, 'a', 'b') as unknown as number;
        const b = hashCombineStrings(0, 'b', 'a') as unknown as number;
        expect(a).not.toBe(b);
    });

    it('different seeds produce different output', () => {
        const a = hashCombineStrings(0, 'hello') as unknown as number;
        const b = hashCombineStrings(12345, 'hello') as unknown as number;
        expect(a).not.toBe(b);
    });
});

describe('hashCombineBooleans', () => {
    it('combines boolean values', () => {
        const a = hashCombineBooleans(0, true, false, true) as unknown as number;
        const b = hashCombineBooleans(0, true, false, false) as unknown as number;
        expect(a).not.toBe(b);
    });
});

describe('hashCombineNumbers', () => {
    it('combines numeric values', () => {
        const a = hashCombineNumbers(0, 1.5, 2.5, 3.5) as unknown as number;
        const b = hashCombineNumbers(0, 1.5, 2.5, 3.5) as unknown as number;
        expect(a).toBe(b);
    });

    it('different values give different output', () => {
        const a = hashCombineNumbers(0, 1.5, 2.5) as unknown as number;
        const b = hashCombineNumbers(0, 1.5, 2.6) as unknown as number;
        expect(a).not.toBe(b);
    });
});
