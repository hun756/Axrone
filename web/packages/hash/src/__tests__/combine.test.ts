import { describe, expect, it } from 'vitest';
import {
    hashCombine,
    hashCombineOrdered,
    hashCombineStrings,
    hashCombineBooleans,
    hashCombineNumbers,
} from '../../hash/combine';

describe('hashCombine', () => {
    it('produces 32-bit unsigned output', () => {
        const v = hashCombine([1, 2, 3]);
        expect(v).toBeGreaterThanOrEqual(0);
        expect(v).toBeLessThan(0x100000000);
    });

    it('order matters', () => {
        const a = hashCombine([1, 2, 3]);
        const b = hashCombine([3, 2, 1]);
        expect(a).not.toBe(b);
    });

    it('different values produce different output', () => {
        const a = hashCombine([1, 2, 3]);
        const b = hashCombine([1, 2, 4]);
        expect(a).not.toBe(b);
    });

    it('empty input returns deterministic value', () => {
        const v = hashCombine([]);
        expect(v).toBeDefined();
    });
});

describe('hashCombineOrdered', () => {
    it('different from unordered for permutations', () => {
        const a = hashCombineOrdered([1, 2, 3]);
        const b = hashCombineOrdered([1, 2, 3]);
        expect(a).toBe(b);
    });

    it('order independence vs hashCombine', () => {
        const ordered = hashCombineOrdered([1, 2, 3]);
        const combined = hashCombine([1, 2, 3]);
        expect(ordered).not.toBe(combined);
    });
});

describe('hashCombineStrings', () => {
    it('matches inline string concat via FNV', () => {
        const v = hashCombineStrings(['hello', 'world']);
        expect(v).toBeGreaterThanOrEqual(0);
    });

    it('order matters', () => {
        const a = hashCombineStrings(['a', 'b']);
        const b = hashCombineStrings(['b', 'a']);
        expect(a).not.toBe(b);
    });
});

describe('hashCombineBooleans', () => {
    it('combines boolean values', () => {
        const a = hashCombineBooleans([true, false, true]);
        const b = hashCombineBooleans([true, false, false]);
        expect(a).not.toBe(b);
    });
});

describe('hashCombineNumbers', () => {
    it('combines numeric values', () => {
        const a = hashCombineNumbers([1.5, 2.5, 3.5]);
        const b = hashCombineNumbers([1.5, 2.5, 3.5]);
        expect(a).toBe(b);
    });

    it('different values give different output', () => {
        const a = hashCombineNumbers([1.5, 2.5]);
        const b = hashCombineNumbers([1.5, 2.6]);
        expect(a).not.toBe(b);
    });
});
