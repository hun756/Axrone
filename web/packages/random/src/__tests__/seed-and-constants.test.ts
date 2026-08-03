import { describe, expect, it } from 'vitest';
import {
    hashSeedToState,
    createSeedFromTime,
    validateProbability,
    validatePositive,
    validateNonNegative,
    validateInteger,
    factorial,
    hex,
    UINT32_MAX,
    UINT64_MAX,
    INV_UINT32_MAX,
    PI,
    TWO_PI,
    LN2,
    E,
    SQRT_2PI,
    RandomEngineType,
} from '@axrone/random';

// ─── hashSeedToState ────────────────────────────────────────────────────────

describe('hashSeedToState', () => {
    it('number seed produces deterministic state', () => {
        const a = hashSeedToState(42);
        const b = hashSeedToState(42);
        expect(a.vector).toEqual(b.vector);
        expect(a.counter).toBe(b.counter);
    });

    it('number seed produces non-zero vector', () => {
        const state = hashSeedToState(42);
        const allZero = state.vector.every((v) => v === 0n);
        expect(allZero).toBe(false);
    });

    it('different number seeds produce different states', () => {
        const a = hashSeedToState(1);
        const b = hashSeedToState(2);
        expect(a.vector).not.toEqual(b.vector);
    });

    it('string seed produces deterministic state', () => {
        const a = hashSeedToState('hello');
        const b = hashSeedToState('hello');
        expect(a.vector).toEqual(b.vector);
    });

    it('different strings produce different states', () => {
        const a = hashSeedToState('hello');
        const b = hashSeedToState('world');
        expect(a.vector).not.toEqual(b.vector);
    });

    it('long string seed (>32 bytes) produces valid state', () => {
        const longStr = 'a'.repeat(100);
        const state = hashSeedToState(longStr);
        expect(state.vector).toHaveLength(4);
        expect(state.counter).toBe(0n);
    });

    it('Uint8Array seed produces valid state', () => {
        const arr = new Uint8Array([1, 2, 3, 4, 5, 6, 7, 8]);
        const state = hashSeedToState(arr);
        expect(state.vector).toHaveLength(4);
        expect(state.counter).toBe(0n);
        const allZero = state.vector.every((v) => v === 0n);
        expect(allZero).toBe(false);
    });

    it('Uint8Array seed is deterministic', () => {
        const arr = new Uint8Array([10, 20, 30]);
        const a = hashSeedToState(arr);
        const b = hashSeedToState(new Uint8Array([10, 20, 30]));
        expect(a.vector).toEqual(b.vector);
    });

    it('Int32Array seed produces valid state', () => {
        const arr = new Int32Array([100, -200, 300]);
        const state = hashSeedToState(arr);
        expect(state.vector).toHaveLength(4);
        expect(state.counter).toBe(0n);
        const allZero = state.vector.every((v) => v === 0n);
        expect(allZero).toBe(false);
    });

    it('Int32Array seed is deterministic', () => {
        const a = hashSeedToState(new Int32Array([1, 2, 3, 4, 5, 6, 7, 8, 9]));
        const b = hashSeedToState(new Int32Array([1, 2, 3, 4, 5, 6, 7, 8, 9]));
        expect(a.vector).toEqual(b.vector);
    });

    it('BigInt64Array seed produces valid state', () => {
        const arr = new BigInt64Array([1n, 2n, 3n, 4n]);
        const state = hashSeedToState(arr);
        expect(state.vector).toHaveLength(4);
        expect(state.counter).toBe(0n);
    });

    it('BigInt64Array seed is deterministic', () => {
        const a = hashSeedToState(new BigInt64Array([10n, 20n]));
        const b = hashSeedToState(new BigInt64Array([10n, 20n]));
        expect(a.vector).toEqual(b.vector);
    });

    it('null seed produces time-based state (non-deterministic)', () => {
        // null seed uses Date.now() + crypto randomness, so two calls should differ
        const a = hashSeedToState(null);
        const b = hashSeedToState(null);
        // Both should be valid states
        expect(a.vector).toHaveLength(4);
        expect(b.vector).toHaveLength(4);
        expect(a.counter).toBe(0n);
        expect(b.counter).toBe(0n);
    });

    it('state shape is always correct', () => {
        const seeds = [42, 'test', new Uint8Array([1]), new Int32Array([1]), new BigInt64Array([1n])];
        for (const seed of seeds) {
            const state = hashSeedToState(seed);
            expect(state.vector).toHaveLength(4);
            expect(typeof state.counter).toBe('bigint');
            expect(state.counter).toBe(0n);
            expect(state.engine).toBe(RandomEngineType.XOROSHIRO128_PLUS_PLUS);
        }
    });

    it('all-zero guard: vector is never all zeros', () => {
        // Try many seeds to ensure the guard works
        for (let i = 0; i < 20; i++) {
            const state = hashSeedToState(i);
            const allZero = state.vector.every((v) => v === 0n);
            expect(allZero).toBe(false);
        }
    });
});

// ─── createSeedFromTime ─────────────────────────────────────────────────────

describe('createSeedFromTime', () => {
    it('produces valid state shape', () => {
        const state = createSeedFromTime();
        expect(state.vector).toHaveLength(4);
        expect(state.counter).toBe(0n);
        expect(state.engine).toBe(RandomEngineType.XOROSHIRO128_PLUS_PLUS);
    });

    it('produces non-zero vector', () => {
        const state = createSeedFromTime();
        const allZero = state.vector.every((v) => v === 0n);
        expect(allZero).toBe(false);
    });
});

// ─── validateProbability ────────────────────────────────────────────────────

describe('validateProbability', () => {
    it('accepts 0', () => {
        expect(() => validateProbability(0)).not.toThrow();
    });

    it('accepts 0.5', () => {
        expect(() => validateProbability(0.5)).not.toThrow();
    });

    it('accepts 1', () => {
        expect(() => validateProbability(1)).not.toThrow();
    });

    it('rejects -0.1', () => {
        expect(() => validateProbability(-0.1)).toThrow(RangeError);
    });

    it('rejects 1.1', () => {
        expect(() => validateProbability(1.1)).toThrow(RangeError);
    });

    it('rejects NaN', () => {
        expect(() => validateProbability(NaN)).toThrow(RangeError);
    });

    it('rejects Infinity', () => {
        expect(() => validateProbability(Infinity)).toThrow(RangeError);
    });

    it('rejects -Infinity', () => {
        expect(() => validateProbability(-Infinity)).toThrow(RangeError);
    });

    it('uses custom name in error message', () => {
        expect(() => validateProbability(2, 'myParam')).toThrow('myParam');
    });
});

// ─── validatePositive ───────────────────────────────────────────────────────

describe('validatePositive', () => {
    it('accepts 0.1', () => {
        expect(() => validatePositive(0.1)).not.toThrow();
    });

    it('accepts 1', () => {
        expect(() => validatePositive(1)).not.toThrow();
    });

    it('accepts 100', () => {
        expect(() => validatePositive(100)).not.toThrow();
    });

    it('rejects 0', () => {
        expect(() => validatePositive(0)).toThrow(RangeError);
    });

    it('rejects -1', () => {
        expect(() => validatePositive(-1)).toThrow(RangeError);
    });

    it('rejects NaN', () => {
        expect(() => validatePositive(NaN)).toThrow(RangeError);
    });

    it('rejects Infinity', () => {
        expect(() => validatePositive(Infinity)).toThrow(RangeError);
    });
});

// ─── validateNonNegative ────────────────────────────────────────────────────

describe('validateNonNegative', () => {
    it('accepts 0', () => {
        expect(() => validateNonNegative(0)).not.toThrow();
    });

    it('accepts 1', () => {
        expect(() => validateNonNegative(1)).not.toThrow();
    });

    it('accepts 100', () => {
        expect(() => validateNonNegative(100)).not.toThrow();
    });

    it('rejects -1', () => {
        expect(() => validateNonNegative(-1)).toThrow(RangeError);
    });

    it('rejects NaN', () => {
        expect(() => validateNonNegative(NaN)).toThrow(RangeError);
    });

    it('rejects Infinity', () => {
        expect(() => validateNonNegative(Infinity)).toThrow(RangeError);
    });
});

// ─── validateInteger ────────────────────────────────────────────────────────

describe('validateInteger', () => {
    it('accepts 0', () => {
        expect(() => validateInteger(0)).not.toThrow();
    });

    it('accepts 1', () => {
        expect(() => validateInteger(1)).not.toThrow();
    });

    it('accepts -5', () => {
        expect(() => validateInteger(-5)).not.toThrow();
    });

    it('rejects 1.5', () => {
        expect(() => validateInteger(1.5)).toThrow(TypeError);
    });

    it('rejects NaN', () => {
        expect(() => validateInteger(NaN)).toThrow(TypeError);
    });

    it('rejects Infinity', () => {
        expect(() => validateInteger(Infinity)).toThrow(TypeError);
    });
});

// ─── factorial ──────────────────────────────────────────────────────────────

describe('factorial', () => {
    it('0! = 1', () => {
        expect(factorial(0)).toBe(1);
    });

    it('1! = 1', () => {
        expect(factorial(1)).toBe(1);
    });

    it('5! = 120', () => {
        expect(factorial(5)).toBe(120);
    });

    it('10! = 3628800', () => {
        expect(factorial(10)).toBe(3628800);
    });

    it('170! is finite', () => {
        expect(Number.isFinite(factorial(170))).toBe(true);
    });

    it('171! returns Infinity', () => {
        expect(factorial(171)).toBe(Infinity);
    });

    it('negative input throws', () => {
        expect(() => factorial(-1)).toThrow(RangeError);
    });

    it('caching works — repeated calls return same result', () => {
        const a = factorial(12);
        const b = factorial(12);
        expect(a).toBe(b);
        expect(a).toBe(479001600);
    });
});

// ─── hex lookup table ───────────────────────────────────────────────────────

describe('hex lookup table', () => {
    it('has 256 entries', () => {
        expect(hex).toHaveLength(256);
    });

    it('hex[0] is "00"', () => {
        expect(hex[0]).toBe('00');
    });

    it('hex[255] is "ff"', () => {
        expect(hex[255]).toBe('ff');
    });

    it('hex[16] is "10"', () => {
        expect(hex[16]).toBe('10');
    });

    it('all entries are 2 characters', () => {
        for (let i = 0; i < 256; i++) {
            expect(hex[i]).toHaveLength(2);
        }
    });
});

// ─── numeric constants ──────────────────────────────────────────────────────

describe('numeric constants', () => {
    it('UINT32_MAX is 2^32 - 1', () => {
        expect(UINT32_MAX).toBe(0xffffffff);
    });

    it('UINT64_MAX is 2^64 - 1', () => {
        expect(UINT64_MAX).toBe(0xffffffffffffffffn);
    });

    it('INV_UINT32_MAX is 1 / (UINT32_MAX + 1)', () => {
        expect(INV_UINT32_MAX).toBeCloseTo(1 / 0x100000000, 20);
    });

    it('PI matches Math.PI', () => {
        expect(PI).toBe(Math.PI);
    });

    it('TWO_PI is 2 * PI', () => {
        expect(TWO_PI).toBeCloseTo(2 * Math.PI, 15);
    });

    it('LN2 matches Math.LN2', () => {
        expect(LN2).toBe(Math.LN2);
    });

    it('E matches Math.E', () => {
        expect(E).toBe(Math.E);
    });

    it('SQRT_2PI is sqrt(2*PI)', () => {
        expect(SQRT_2PI).toBeCloseTo(Math.sqrt(2 * Math.PI), 15);
    });
});
