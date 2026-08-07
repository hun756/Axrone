import { describe, expect, it } from 'vitest';
import {
    createRandom,
    RandomEngineType,
} from '@axrone/random';
import type { IRandomAPI, IRandomState, IDistribution, RandomResult } from '@axrone/random';

// ─── floatBetween ───────────────────────────────────────────────────────────

describe('Random.floatBetween', () => {
    const r = createRandom(42);

    it('returns value in [min, max)', () => {
        for (let i = 0; i < 100; i++) {
            const v = r.floatBetween(5, 10);
            expect(v).toBeGreaterThanOrEqual(5);
            expect(v).toBeLessThan(10);
        }
    });

    it('throws on NaN bounds', () => {
        expect(() => r.floatBetween(NaN, 10)).toThrow(RangeError);
        expect(() => r.floatBetween(5, NaN)).toThrow(RangeError);
    });

    it('throws on Infinity bounds', () => {
        expect(() => r.floatBetween(-Infinity, 10)).toThrow(RangeError);
        expect(() => r.floatBetween(5, Infinity)).toThrow(RangeError);
    });

    it('throws on min >= max', () => {
        expect(() => r.floatBetween(10, 5)).toThrow(RangeError);
        expect(() => r.floatBetween(5, 5)).toThrow(RangeError);
    });
});

// ─── int ────────────────────────────────────────────────────────────────────

describe('Random.int', () => {
    const r = createRandom(42);

    it('min === max returns min', () => {
        expect(r.int(7, 7)).toBe(7);
    });

    it('throws on non-integer args', () => {
        expect(() => r.int(1.5, 5)).toThrow(TypeError);
        expect(() => r.int(1, 5.5)).toThrow(TypeError);
    });

    it('throws on min > max', () => {
        expect(() => r.int(10, 5)).toThrow(RangeError);
    });

    it('returns integer in range', () => {
        for (let i = 0; i < 100; i++) {
            const v = r.int(1, 6);
            expect(Number.isInteger(v)).toBe(true);
            expect(v).toBeGreaterThanOrEqual(1);
            expect(v).toBeLessThanOrEqual(6);
        }
    });
});

// ─── boolean ────────────────────────────────────────────────────────────────

describe('Random.boolean', () => {
    it('throws on invalid probability', () => {
        const r = createRandom(42);
        expect(() => r.boolean(-0.1)).toThrow(RangeError);
        expect(() => r.boolean(1.1)).toThrow(RangeError);
    });

    it('boolean(0) always returns false', () => {
        const r = createRandom(42);
        for (let i = 0; i < 20; i++) {
            expect(r.boolean(0)).toBe(false);
        }
    });

    it('boolean(1) always returns true', () => {
        const r = createRandom(42);
        for (let i = 0; i < 20; i++) {
            expect(r.boolean(1)).toBe(true);
        }
    });
});

// ─── pick error paths ───────────────────────────────────────────────────────

describe('Random.pick', () => {
    it('throws on empty array', () => {
        const r = createRandom(42);
        expect(() => r.pick([])).toThrow('Cannot pick from an empty array');
    });

    it('single-element array always returns that element', () => {
        const r = createRandom(42);
        for (let i = 0; i < 10; i++) {
            expect(r.pick([42])).toBe(42);
        }
    });
});

// ─── weighted error paths ───────────────────────────────────────────────────

describe('Random.weighted', () => {
    it('throws on empty array', () => {
        const r = createRandom(42);
        expect(() => r.weighted([])).toThrow('Cannot pick from an empty array');
    });

    it('throws on negative weight', () => {
        const r = createRandom(42);
        expect(() => r.weighted([['a', -1]])).toThrow(RangeError);
    });

    it('throws on all-zero weights', () => {
        const r = createRandom(42);
        expect(() => r.weighted([['a', 0], ['b', 0]])).toThrow(RangeError);
    });
});

// ─── shuffle edge cases ─────────────────────────────────────────────────────

describe('Random.shuffle', () => {
    it('empty array returns empty', () => {
        const r = createRandom(42);
        expect(r.shuffle([])).toEqual([]);
    });

    it('single-element array returns same element', () => {
        const r = createRandom(42);
        expect(r.shuffle([1])).toEqual([1]);
    });

    it('does not mutate original array', () => {
        const r = createRandom(42);
        const original = [1, 2, 3, 4, 5];
        const copy = [...original];
        r.shuffle(original);
        expect(original).toEqual(copy);
    });
});

// ─── sample edge cases ──────────────────────────────────────────────────────

describe('Random.sample', () => {
    it('count=0 returns empty', () => {
        const r = createRandom(42);
        expect(r.sample([1, 2, 3], 0)).toEqual([]);
    });

    it('empty array returns empty', () => {
        const r = createRandom(42);
        expect(r.sample([], 5)).toEqual([]);
    });

    it('throws on negative count', () => {
        const r = createRandom(42);
        expect(() => r.sample([1, 2, 3], -1)).toThrow(RangeError);
    });

    it('throws on non-integer count', () => {
        const r = createRandom(42);
        expect(() => r.sample([1, 2, 3], 1.5)).toThrow(TypeError);
    });

    it('small sample ratio uses set-based path', () => {
        const r = createRandom(42);
        const arr = Array.from({ length: 100 }, (_, i) => i);
        // count < 15% of 100 => uses Set-based path
        const result = r.sample(arr, 5);
        expect(result).toHaveLength(5);
        const unique = new Set(result);
        expect(unique.size).toBe(5);
    });

    it('large sample ratio uses shuffle-based path', () => {
        const r = createRandom(42);
        const arr = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        // count >= 15% of 10 => uses partial shuffle path
        const result = r.sample(arr, 5);
        expect(result).toHaveLength(5);
    });
});

// ─── string edge cases ──────────────────────────────────────────────────────

describe('Random.string', () => {
    it('throws on empty charset', () => {
        const r = createRandom(42);
        expect(() => r.string(10, '')).toThrow('Charset must not be empty');
    });

    it('power-of-2 charset uses optimized path', () => {
        const r = createRandom(42);
        // charset length = 4 (power of 2)
        const s = r.string(20, 'ABCD');
        expect(s).toHaveLength(20);
        for (const c of s) {
            expect('ABCD').toContain(c);
        }
    });

    it('non-power-of-2 charset uses standard path', () => {
        const r = createRandom(42);
        // charset length = 3 (not power of 2)
        const s = r.string(20, 'ABC');
        expect(s).toHaveLength(20);
        for (const c of s) {
            expect('ABC').toContain(c);
        }
    });

    it('default charset produces valid output', () => {
        const r = createRandom(42);
        const s = r.string(50);
        expect(s).toHaveLength(50);
        const DEFAULT_CHARSET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';
        for (const c of s) {
            expect(DEFAULT_CHARSET).toContain(c);
        }
    });

    it('throws on negative length', () => {
        const r = createRandom(42);
        expect(() => r.string(-1)).toThrow(RangeError);
    });

    it('throws on non-integer length', () => {
        const r = createRandom(42);
        expect(() => r.string(1.5)).toThrow(TypeError);
    });
});

// ─── bytes edge cases ───────────────────────────────────────────────────────

describe('Random.bytes', () => {
    it('length=0 returns empty Uint8Array', () => {
        const r = createRandom(42);
        const b = r.bytes(0);
        expect(b).toHaveLength(0);
        expect(b).toBeInstanceOf(Uint8Array);
    });

    it('throws on negative length', () => {
        const r = createRandom(42);
        expect(() => r.bytes(-1)).toThrow(RangeError);
    });

    it('throws on non-integer length', () => {
        const r = createRandom(42);
        expect(() => r.bytes(1.5)).toThrow(TypeError);
    });
});

// ─── Distribution convenience methods ───────────────────────────────────────

describe('Random distribution convenience methods', () => {
    const r = createRandom(42);

    it('normal() produces finite number', () => {
        expect(Number.isFinite(r.normal())).toBe(true);
    });

    it('exponential() produces non-negative number', () => {
        expect(r.exponential()).toBeGreaterThanOrEqual(0);
    });

    it('poisson() produces non-negative integer', () => {
        const v = r.poisson(5);
        expect(Number.isInteger(v)).toBe(true);
        expect(v).toBeGreaterThanOrEqual(0);
    });

    it('bernoulli() produces boolean', () => {
        expect(typeof r.bernoulli()).toBe('boolean');
    });

    it('binomial() produces integer in [0, n]', () => {
        const v = r.binomial(10, 0.5);
        expect(Number.isInteger(v)).toBe(true);
        expect(v).toBeGreaterThanOrEqual(0);
        expect(v).toBeLessThanOrEqual(10);
    });

    it('geometric() produces non-negative integer', () => {
        const v = r.geometric(0.3);
        expect(Number.isInteger(v)).toBe(true);
        expect(v).toBeGreaterThanOrEqual(0);
    });

    it('distribution() with custom IDistribution', () => {
        const custom: IDistribution<number> = {
            sample: (_state: IRandomState): RandomResult<number> => {
                return [42, _state];
            },
        };
        expect(r.distribution(custom)).toBe(42);
    });
});

// ─── *WithMetadata methods ──────────────────────────────────────────────────

describe('Random *WithMetadata methods', () => {
    const r = createRandom(42);

    it('normalWithMetadata returns value + zscore + metadata', () => {
        const result = r.normalWithMetadata();
        expect(typeof result.value).toBe('number');
        expect(typeof result.zscore).toBe('number');
        expect(result.metadata).toBeDefined();
        expect(result.metadata!.mean).toBe(0);
    });

    it('normalMany returns correct count', () => {
        const values = r.normalMany(10);
        expect(values).toHaveLength(10);
        for (const v of values) {
            expect(Number.isFinite(v)).toBe(true);
        }
    });

    it('normalManyWithMetadata returns correct count with metadata', () => {
        const results = r.normalManyWithMetadata(5);
        expect(results).toHaveLength(5);
        for (const res of results) {
            expect(typeof res.value).toBe('number');
            expect(res.metadata).toBeDefined();
        }
    });

    it('exponentialWithMetadata returns metadata', () => {
        const result = r.exponentialWithMetadata();
        expect(typeof result.value).toBe('number');
        expect(result.metadata).toBeDefined();
        expect(result.metadata!.lambda).toBe(1);
    });

    it('exponentialMany returns correct count', () => {
        const values = r.exponentialMany(8);
        expect(values).toHaveLength(8);
    });

    it('exponentialManyWithMetadata returns correct count', () => {
        const results = r.exponentialManyWithMetadata(6);
        expect(results).toHaveLength(6);
        for (const res of results) {
            expect(res.metadata).toBeDefined();
        }
    });

    it('poissonWithMetadata returns metadata', () => {
        const result = r.poissonWithMetadata(3);
        expect(Number.isInteger(result.value)).toBe(true);
        expect(result.metadata!.lambda).toBe(3);
    });

    it('poissonMany returns correct count', () => {
        const values = r.poissonMany(7, 4);
        expect(values).toHaveLength(7);
    });

    it('poissonManyWithMetadata returns correct count', () => {
        const results = r.poissonManyWithMetadata(5, 3);
        expect(results).toHaveLength(5);
        for (const res of results) {
            expect(res.metadata).toBeDefined();
        }
    });

    it('bernoulliWithMetadata returns metadata', () => {
        const result = r.bernoulliWithMetadata(0.7);
        expect(typeof result.value).toBe('boolean');
        expect(result.metadata!.p).toBe(0.7);
    });

    it('bernoulliMany returns correct count', () => {
        const values = r.bernoulliMany(12);
        expect(values).toHaveLength(12);
        for (const v of values) {
            expect(typeof v).toBe('boolean');
        }
    });

    it('bernoulliManyWithMetadata returns correct count', () => {
        const results = r.bernoulliManyWithMetadata(8);
        expect(results).toHaveLength(8);
    });

    it('binomialWithMetadata returns metadata', () => {
        const result = r.binomialWithMetadata(10, 0.5);
        expect(Number.isInteger(result.value)).toBe(true);
        expect(result.metadata!.n).toBe(10);
        expect(result.metadata!.p).toBe(0.5);
    });

    it('binomialMany returns correct count', () => {
        const values = r.binomialMany(6, 10, 0.3);
        expect(values).toHaveLength(6);
    });

    it('binomialManyWithMetadata returns correct count', () => {
        const results = r.binomialManyWithMetadata(4, 10, 0.5);
        expect(results).toHaveLength(4);
    });

    it('geometricWithMetadata returns metadata', () => {
        const result = r.geometricWithMetadata(0.4);
        expect(Number.isInteger(result.value)).toBe(true);
        expect(result.metadata!.p).toBe(0.4);
    });

    it('geometricMany returns correct count', () => {
        const values = r.geometricMany(9, 0.3);
        expect(values).toHaveLength(9);
    });

    it('geometricManyWithMetadata returns correct count', () => {
        const results = r.geometricManyWithMetadata(7, 0.2);
        expect(results).toHaveLength(7);
    });
});

// ─── analyzeSequence ────────────────────────────────────────────────────────

describe('Random.analyzeSequence', () => {
    it('computes correct statistics for [1,2,3,4,5]', () => {
        const r = createRandom(42);
        const stats = r.analyzeSequence([1, 2, 3, 4, 5]);
        expect(stats.mean).toBe(3);
        expect(stats.variance).toBe(2);
        expect(stats.standardDeviation).toBeCloseTo(Math.sqrt(2), 10);
        expect(stats.min).toBe(1);
        expect(stats.max).toBe(5);
        expect(stats.count).toBe(5);
    });

    it('single-element array', () => {
        const r = createRandom(42);
        const stats = r.analyzeSequence([42]);
        expect(stats.mean).toBe(42);
        expect(stats.variance).toBe(0);
        expect(stats.standardDeviation).toBe(0);
        expect(stats.min).toBe(42);
        expect(stats.max).toBe(42);
        expect(stats.count).toBe(1);
    });

    it('throws on empty array', () => {
        const r = createRandom(42);
        expect(() => r.analyzeSequence([])).toThrow('Cannot analyze empty sequence');
    });
});

// ─── setNormalAlgorithm ─────────────────────────────────────────────────────

describe('Random.setNormalAlgorithm', () => {
    it.each(['standard', 'polar', 'ziggurat'] as const)(
        'algorithm "%s" produces finite numbers',
        (algo) => {
            const r = createRandom(42);
            r.setNormalAlgorithm(algo);
            for (let i = 0; i < 20; i++) {
                expect(Number.isFinite(r.normal())).toBe(true);
            }
        }
    );
});

// ─── setState with different engine ─────────────────────────────────────────

describe('Random.setState engine switching', () => {
    it('setState from different engine type switches engine', () => {
        const r = createRandom(42, RandomEngineType.XOROSHIRO128_PLUS_PLUS);
        expect(r.getState().engine).toBe(RandomEngineType.XOROSHIRO128_PLUS_PLUS);

        // Get state from a PCG engine
        const pcgRand = createRandom(42, RandomEngineType.PCG_XSH_RR);
        const pcgState = pcgRand.getState();

        r.setState(pcgState);
        expect(r.getState().engine).toBe(RandomEngineType.PCG_XSH_RR);
    });
});

// ─── fork preserves engine type ─────────────────────────────────────────────

describe('Random.fork', () => {
    it('forked instance uses same engine type', () => {
        const parent = createRandom(42, RandomEngineType.PCG_XSH_RR);
        const forked = parent.fork();
        expect(forked.getState().engine).toBe(RandomEngineType.PCG_XSH_RR);
    });

    it('forked instance is independent', () => {
        const parent = createRandom(42);
        const forked = parent.fork();
        // Advance parent
        parent.float();
        parent.float();
        // Forked should still produce its own sequence
        const v = forked.float();
        expect(Number.isFinite(v)).toBe(true);
    });
});

// ─── setEngine preserves continuity ─────────────────────────────────────────

describe('Random.setEngine', () => {
    it('switches engine type', () => {
        const r = createRandom(42, RandomEngineType.XOROSHIRO128_PLUS_PLUS);
        r.setEngine(RandomEngineType.CHACHA20);
        expect(r.getState().engine).toBe(RandomEngineType.CHACHA20);
    });

    it('subsequent values are finite after engine switch', () => {
        const r = createRandom(42);
        r.setEngine(RandomEngineType.SPLITMIX64);
        for (let i = 0; i < 20; i++) {
            expect(Number.isFinite(r.float())).toBe(true);
        }
    });
});
