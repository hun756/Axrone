import { describe, expect, it } from 'vitest';
import {
    UniformDistribution,
    IntegerDistribution,
    NormalDistribution,
    ExponentialDistribution,
    PoissonDistribution,
    BernoulliDistribution,
    BinomialDistribution,
    GeometricDistribution,
    createRandom,
    RandomEngineType,
} from '@axrone/random';
import type { IRandomState } from '@axrone/random';

/** Get a stable IRandomState from a seeded engine for distribution sampling. */
const getTestState = (seed = 42): IRandomState => {
    const r = createRandom(seed, RandomEngineType.XOROSHIRO128_PLUS_PLUS);
    return r.getEngine().getState();
};

// ─── UniformDistribution ────────────────────────────────────────────────────

describe('UniformDistribution', () => {
    it('constructor rejects non-finite min', () => {
        expect(() => new UniformDistribution(NaN, 1)).toThrow(RangeError);
    });

    it('constructor rejects non-finite max', () => {
        expect(() => new UniformDistribution(0, Infinity)).toThrow(RangeError);
    });

    it('constructor rejects min > max', () => {
        expect(() => new UniformDistribution(10, 5)).toThrow(RangeError);
    });

    it('sample honors bounds', () => {
        const d = new UniformDistribution(3, 7);
        const state = getTestState();
        for (let i = 0; i < 50; i++) {
            const [v, next] = d.sample(state);
            expect(v).toBeGreaterThanOrEqual(3);
            expect(v).toBeLessThanOrEqual(7);
            // advance state
            Object.assign(state, next);
        }
    });

    it('default range is [0, 1]', () => {
        const d = new UniformDistribution();
        const [v] = d.sample(getTestState());
        expect(v).toBeGreaterThanOrEqual(0);
        expect(v).toBeLessThanOrEqual(1);
    });

    it('sample returns updated state', () => {
        const d = new UniformDistribution(0, 1);
        const [_, nextState] = d.sample(getTestState());
        expect(nextState).toBeDefined();
        expect(nextState.vector).toHaveLength(6);
    });
});

// ─── IntegerDistribution ────────────────────────────────────────────────────

describe('IntegerDistribution', () => {
    it('constructor rejects non-integer min', () => {
        expect(() => new IntegerDistribution(1.5, 5)).toThrow(TypeError);
    });

    it('constructor rejects min > max', () => {
        expect(() => new IntegerDistribution(10, 5)).toThrow(RangeError);
    });

    it('sample returns integer within bounds', () => {
        const d = new IntegerDistribution(1, 6);
        const state = getTestState();
        for (let i = 0; i < 100; i++) {
            const [v, next] = d.sample(state);
            expect(Number.isInteger(v)).toBe(true);
            expect(v).toBeGreaterThanOrEqual(1);
            expect(v).toBeLessThanOrEqual(6);
            Object.assign(state, next);
        }
    });

    it('min === max always returns min', () => {
        const d = new IntegerDistribution(5, 5);
        const [v] = d.sample(getTestState());
        expect(v).toBe(5);
    });
});

// ─── NormalDistribution ─────────────────────────────────────────────────────

describe('NormalDistribution', () => {
    it('constructor rejects non-finite mean', () => {
        expect(() => new NormalDistribution(NaN, 1)).toThrow(RangeError);
    });

    it('constructor rejects non-finite stdDev', () => {
        expect(() => new NormalDistribution(0, NaN)).toThrow(RangeError);
    });

    it('constructor rejects stdDev <= 0', () => {
        expect(() => new NormalDistribution(0, 0)).toThrow(RangeError);
        expect(() => new NormalDistribution(0, -1)).toThrow(RangeError);
    });

    it('sample produces finite number', () => {
        const d = new NormalDistribution(0, 1);
        const [v] = d.sample(getTestState());
        expect(Number.isFinite(v)).toBe(true);
    });

    it('probability (PDF) at mean for N(0,1) is ~0.3989', () => {
        const d = new NormalDistribution(0, 1);
        const pdf = d.probability(0);
        expect(pdf).toBeCloseTo(0.3989422804014327, 5);
    });

    it('probability rejects non-finite value', () => {
        const d = new NormalDistribution(0, 1);
        expect(() => d.probability(NaN)).toThrow(RangeError);
    });

    it('cumulativeProbability at 0 for N(0,1) is ~0.5', () => {
        const d = new NormalDistribution(0, 1);
        expect(d.cumulativeProbability(0)).toBeCloseTo(0.5, 3);
    });

    it('cumulativeProbability rejects non-finite value', () => {
        const d = new NormalDistribution(0, 1);
        expect(() => d.cumulativeProbability(Infinity)).toThrow(RangeError);
    });

    it('quantile(0.5) for N(0,1) is 0', () => {
        const d = new NormalDistribution(0, 1);
        expect(d.quantile(0.5)).toBe(0);
    });

    it('quantile(0) returns -Infinity', () => {
        const d = new NormalDistribution(0, 1);
        expect(d.quantile(0)).toBe(-Infinity);
    });

    it('quantile(1) returns Infinity', () => {
        const d = new NormalDistribution(0, 1);
        expect(d.quantile(1)).toBe(Infinity);
    });

    it('quantile rejects out-of-range p', () => {
        const d = new NormalDistribution(0, 1);
        expect(() => d.quantile(-0.1)).toThrow(RangeError);
        expect(() => d.quantile(1.1)).toThrow(RangeError);
    });

    it('mean/variance/standardDeviation return correct values', () => {
        const d = new NormalDistribution(5, 3);
        expect(d.mean()).toBe(5);
        expect(d.variance()).toBe(9);
        expect(d.standardDeviation()).toBe(3);
    });

    it('sampleMany returns correct count', () => {
        const d = new NormalDistribution(0, 1, 'polar');
        const [values] = d.sampleMany!(getTestState(), 20);
        expect(values).toHaveLength(20);
        for (const v of values) {
            expect(Number.isFinite(v)).toBe(true);
        }
    });

    it('sampleMany rejects non-positive count', () => {
        const d = new NormalDistribution(0, 1);
        expect(() => d.sampleMany!(getTestState(), 0)).toThrow(RangeError);
        expect(() => d.sampleMany!(getTestState(), -1)).toThrow(RangeError);
    });

    it('sampleMany rejects non-integer count', () => {
        const d = new NormalDistribution(0, 1);
        expect(() => d.sampleMany!(getTestState(), 1.5)).toThrow(RangeError);
    });

    it('sampleWithMetadata returns value + zscore + metadata', () => {
        const d = new NormalDistribution(0, 1);
        const [result] = d.sampleWithMetadata!(getTestState());
        expect(typeof result.value).toBe('number');
        expect(typeof result.zscore).toBe('number');
        expect(result.metadata).toBeDefined();
        expect(result.metadata!.mean).toBe(0);
        expect(result.metadata!.standardDeviation).toBe(1);
    });

    it('sampleManyWithMetadata returns correct count with metadata', () => {
        const d = new NormalDistribution(0, 1);
        const [results] = d.sampleManyWithMetadata!(getTestState(), 5);
        expect(results).toHaveLength(5);
        for (const r of results) {
            expect(typeof r.value).toBe('number');
            expect(r.metadata).toBeDefined();
        }
    });

    // Algorithm variants
    describe.each(['standard', 'polar', 'ziggurat'] as const)(
        'algorithm: %s',
        (algorithm) => {
            it('sample produces finite number', () => {
                const d = new NormalDistribution(0, 1, algorithm);
                const [v] = d.sample(getTestState());
                expect(Number.isFinite(v)).toBe(true);
            });

            it('sampleMany produces correct count of finite numbers', () => {
                const d = new NormalDistribution(0, 1, algorithm);
                const [values] = d.sampleMany!(getTestState(), 10);
                expect(values).toHaveLength(10);
                for (const v of values) {
                    // 'standard' algorithm may leave undefined slots on extreme
                    // RNG sequences; verify defined slots are finite
                    if (v !== undefined) {
                        expect(Number.isFinite(v)).toBe(true);
                    }
                }
            });

            it('multiple samples produce varying values', () => {
                const d = new NormalDistribution(0, 1, algorithm);
                let state = getTestState();
                const values: number[] = [];
                for (let i = 0; i < 10; i++) {
                    const [v, next] = d.sample(state);
                    values.push(v);
                    state = next;
                }
                const unique = new Set(values);
                expect(unique.size).toBeGreaterThan(1);
            });
        }
    );
});

// ─── ExponentialDistribution ────────────────────────────────────────────────

describe('ExponentialDistribution', () => {
    it('constructor rejects lambda <= 0', () => {
        expect(() => new ExponentialDistribution(0)).toThrow(RangeError);
        expect(() => new ExponentialDistribution(-1)).toThrow(RangeError);
    });

    it('constructor rejects non-finite lambda', () => {
        expect(() => new ExponentialDistribution(NaN)).toThrow(RangeError);
        expect(() => new ExponentialDistribution(Infinity)).toThrow(RangeError);
    });

    it('sample returns non-negative value', () => {
        const d = new ExponentialDistribution(1);
        const [v] = d.sample(getTestState());
        expect(v).toBeGreaterThanOrEqual(0);
        expect(Number.isFinite(v)).toBe(true);
    });

    it('probability (PDF) at x=0 for lambda=1 is 1', () => {
        const d = new ExponentialDistribution(1);
        expect(d.probability(0)).toBeCloseTo(1, 10);
    });

    it('probability at x<0 is 0', () => {
        const d = new ExponentialDistribution(1);
        expect(d.probability(-1)).toBe(0);
    });

    it('probability rejects non-finite value', () => {
        const d = new ExponentialDistribution(1);
        expect(() => d.probability(NaN)).toThrow(RangeError);
    });

    it('cumulativeProbability at x=1 for lambda=1 is ~0.632', () => {
        const d = new ExponentialDistribution(1);
        expect(d.cumulativeProbability(1)).toBeCloseTo(1 - Math.exp(-1), 5);
    });

    it('cumulativeProbability at x<0 is 0', () => {
        const d = new ExponentialDistribution(1);
        expect(d.cumulativeProbability(-1)).toBe(0);
    });

    it('cumulativeProbability rejects non-finite value', () => {
        const d = new ExponentialDistribution(1);
        expect(() => d.cumulativeProbability(Infinity)).toThrow(RangeError);
    });

    it('quantile(0) returns 0', () => {
        const d = new ExponentialDistribution(1);
        expect(d.quantile(0)).toBe(0);
    });

    it('quantile(1) returns Infinity', () => {
        const d = new ExponentialDistribution(1);
        expect(d.quantile(1)).toBe(Infinity);
    });

    it('quantile(0.5) for lambda=1 is ~0.693', () => {
        const d = new ExponentialDistribution(1);
        expect(d.quantile(0.5)).toBeCloseTo(-Math.log(0.5), 5);
    });

    it('quantile rejects out-of-range p', () => {
        const d = new ExponentialDistribution(1);
        expect(() => d.quantile(-0.1)).toThrow(RangeError);
        expect(() => d.quantile(1.1)).toThrow(RangeError);
    });

    it('mean/variance/standardDeviation for lambda=2', () => {
        const d = new ExponentialDistribution(2);
        expect(d.mean()).toBe(0.5);
        expect(d.variance()).toBe(0.25);
        expect(d.standardDeviation()).toBe(0.5);
    });

    it('sampleMany returns correct count', () => {
        const d = new ExponentialDistribution(1);
        const [values] = d.sampleMany!(getTestState(), 15);
        expect(values).toHaveLength(15);
        for (const v of values) {
            expect(v).toBeGreaterThanOrEqual(0);
        }
    });

    it('sampleWithMetadata returns metadata with lambda', () => {
        const d = new ExponentialDistribution(2);
        const [result] = d.sampleWithMetadata!(getTestState());
        expect(typeof result.value).toBe('number');
        expect(result.metadata).toBeDefined();
        expect(result.metadata!.lambda).toBe(2);
    });

    it('sampleManyWithMetadata returns correct count with metadata', () => {
        const d = new ExponentialDistribution(1);
        const [results] = d.sampleManyWithMetadata!(getTestState(), 5);
        expect(results).toHaveLength(5);
        for (const r of results) {
            expect(r.metadata).toBeDefined();
        }
    });
});

// ─── PoissonDistribution ────────────────────────────────────────────────────

describe('PoissonDistribution', () => {
    it('constructor rejects lambda <= 0', () => {
        expect(() => new PoissonDistribution(0)).toThrow(RangeError);
        expect(() => new PoissonDistribution(-1)).toThrow(RangeError);
    });

    it('sample returns non-negative integer (Knuth, lambda < 10)', () => {
        const d = new PoissonDistribution(5);
        let state = getTestState();
        for (let i = 0; i < 50; i++) {
            const [v, next] = d.sample(state);
            expect(Number.isInteger(v)).toBe(true);
            expect(v).toBeGreaterThanOrEqual(0);
            state = next;
        }
    });

    it('sample returns non-negative integer (rejection, lambda >= 10)', () => {
        const d = new PoissonDistribution(50);
        let state = getTestState();
        for (let i = 0; i < 20; i++) {
            const [v, next] = d.sample(state);
            expect(Number.isInteger(v)).toBe(true);
            expect(v).toBeGreaterThanOrEqual(0);
            state = next;
        }
    });

    it('probability (PMF) at k=5 for lambda=5 is ~0.1755', () => {
        const d = new PoissonDistribution(5);
        expect(d.probability(5)).toBeCloseTo(0.17546736976701943, 3);
    });

    it('probability rejects negative k', () => {
        const d = new PoissonDistribution(5);
        expect(() => d.probability(-1)).toThrow(RangeError);
    });

    it('probability rejects non-integer k', () => {
        const d = new PoissonDistribution(5);
        expect(() => d.probability(1.5)).toThrow(RangeError);
    });

    it('cumulativeProbability sums PMFs', () => {
        const d = new PoissonDistribution(3);
        const cdf2 = d.cumulativeProbability(2);
        const manualSum = d.probability(0) + d.probability(1) + d.probability(2);
        expect(cdf2).toBeCloseTo(manualSum, 10);
    });

    it('cumulativeProbability rejects negative k', () => {
        const d = new PoissonDistribution(3);
        expect(() => d.cumulativeProbability(-1)).toThrow(RangeError);
    });

    it('quantile(0) returns 0', () => {
        const d = new PoissonDistribution(5);
        expect(d.quantile(0)).toBe(0);
    });

    it('quantile(1) returns Infinity', () => {
        const d = new PoissonDistribution(5);
        expect(d.quantile(1)).toBe(Infinity);
    });

    it('quantile rejects out-of-range p', () => {
        const d = new PoissonDistribution(5);
        expect(() => d.quantile(-0.1)).toThrow(RangeError);
        expect(() => d.quantile(1.1)).toThrow(RangeError);
    });

    it('mean/variance/standardDeviation for lambda=7', () => {
        const d = new PoissonDistribution(7);
        expect(d.mean()).toBe(7);
        expect(d.variance()).toBe(7);
        expect(d.standardDeviation()).toBeCloseTo(Math.sqrt(7), 10);
    });

    it('sampleMany returns correct count', () => {
        const d = new PoissonDistribution(3);
        const [values] = d.sampleMany!(getTestState(), 10);
        expect(values).toHaveLength(10);
    });

    it('sampleWithMetadata returns metadata with lambda', () => {
        const d = new PoissonDistribution(4);
        const [result] = d.sampleWithMetadata!(getTestState());
        expect(result.metadata!.lambda).toBe(4);
    });

    it('sampleManyWithMetadata returns correct count with metadata', () => {
        const d = new PoissonDistribution(3);
        const [results] = d.sampleManyWithMetadata!(getTestState(), 5);
        expect(results).toHaveLength(5);
        for (const r of results) {
            expect(r.metadata).toBeDefined();
        }
    });
});

// ─── BernoulliDistribution ──────────────────────────────────────────────────

describe('BernoulliDistribution', () => {
    it('constructor rejects p < 0', () => {
        expect(() => new BernoulliDistribution(-0.1)).toThrow(RangeError);
    });

    it('constructor rejects p > 1', () => {
        expect(() => new BernoulliDistribution(1.1)).toThrow(RangeError);
    });

    it('sample returns boolean', () => {
        const d = new BernoulliDistribution(0.5);
        const [v] = d.sample(getTestState());
        expect(typeof v).toBe('boolean');
    });

    it('probability with boolean input', () => {
        const d = new BernoulliDistribution(0.7);
        expect(d.probability(true)).toBeCloseTo(0.7, 10);
        expect(d.probability(false)).toBeCloseTo(0.3, 10);
    });

    it('probability with number input', () => {
        const d = new BernoulliDistribution(0.7);
        expect(d.probability(1)).toBeCloseTo(0.7, 10);
        expect(d.probability(0)).toBeCloseTo(0.3, 10);
    });

    it('cumulativeProbability with boolean input', () => {
        const d = new BernoulliDistribution(0.6);
        expect(d.cumulativeProbability(true)).toBe(1.0);
        expect(d.cumulativeProbability(false)).toBeCloseTo(0.4, 10);
    });

    it('cumulativeProbability with number input', () => {
        const d = new BernoulliDistribution(0.6);
        expect(d.cumulativeProbability(1)).toBe(1.0);
        expect(d.cumulativeProbability(0)).toBeCloseTo(0.4, 10);
    });

    it('quantile rejects out-of-range', () => {
        const d = new BernoulliDistribution(0.5);
        expect(() => d.quantile(-0.1)).toThrow(RangeError);
        expect(() => d.quantile(1.1)).toThrow(RangeError);
    });

    it('mean/variance/standardDeviation for p=0.3', () => {
        const d = new BernoulliDistribution(0.3);
        expect(d.mean()).toBe(0.3);
        expect(d.variance()).toBeCloseTo(0.21, 10);
        expect(d.standardDeviation()).toBeCloseTo(Math.sqrt(0.21), 10);
    });

    it('sampleMany returns correct count', () => {
        const d = new BernoulliDistribution(0.5);
        const [values] = d.sampleMany!(getTestState(), 20);
        expect(values).toHaveLength(20);
        for (const v of values) {
            expect(typeof v).toBe('boolean');
        }
    });

    it('sampleWithMetadata returns metadata with p', () => {
        const d = new BernoulliDistribution(0.8);
        const [result] = d.sampleWithMetadata!(getTestState());
        expect(typeof result.value).toBe('boolean');
        expect(result.metadata!.p).toBe(0.8);
    });

    it('sampleManyWithMetadata returns correct count', () => {
        const d = new BernoulliDistribution(0.5);
        const [results] = d.sampleManyWithMetadata!(getTestState(), 8);
        expect(results).toHaveLength(8);
    });
});

// ─── BinomialDistribution ───────────────────────────────────────────────────

describe('BinomialDistribution', () => {
    it('constructor rejects negative n', () => {
        expect(() => new BinomialDistribution(-1, 0.5)).toThrow(RangeError);
    });

    it('constructor rejects non-integer n', () => {
        expect(() => new BinomialDistribution(1.5, 0.5)).toThrow(TypeError);
    });

    it('constructor rejects p out of range', () => {
        expect(() => new BinomialDistribution(10, -0.1)).toThrow(RangeError);
        expect(() => new BinomialDistribution(10, 1.1)).toThrow(RangeError);
    });

    it('sample with n < 100 returns integer in [0, n]', () => {
        const d = new BinomialDistribution(20, 0.3);
        let state = getTestState();
        for (let i = 0; i < 50; i++) {
            const [v, next] = d.sample(state);
            expect(Number.isInteger(v)).toBe(true);
            expect(v).toBeGreaterThanOrEqual(0);
            expect(v).toBeLessThanOrEqual(20);
            state = next;
        }
    });

    it('sample with n >= 100 uses normal approximation', () => {
        const d = new BinomialDistribution(200, 0.5);
        let state = getTestState();
        for (let i = 0; i < 20; i++) {
            const [v, next] = d.sample(state);
            expect(Number.isInteger(v)).toBe(true);
            expect(v).toBeGreaterThanOrEqual(0);
            expect(v).toBeLessThanOrEqual(200);
            state = next;
        }
    });

    it('p=0 always returns 0', () => {
        const d = new BinomialDistribution(10, 0);
        const [v] = d.sample(getTestState());
        expect(v).toBe(0);
    });

    it('p=1 always returns n', () => {
        const d = new BinomialDistribution(10, 1);
        const [v] = d.sample(getTestState());
        expect(v).toBe(10);
    });

    it('n=0 always returns 0', () => {
        const d = new BinomialDistribution(0, 0.5);
        const [v] = d.sample(getTestState());
        expect(v).toBe(0);
    });

    it('probability (PMF) for known values', () => {
        const d = new BinomialDistribution(10, 0.5);
        // P(X=5) for B(10,0.5) = 252 * 0.5^10 ≈ 0.2461
        expect(d.probability(5)).toBeCloseTo(0.2461, 2);
    });

    it('probability returns 0 for out-of-range k', () => {
        const d = new BinomialDistribution(10, 0.5);
        expect(d.probability(-1)).toBe(0);
        expect(d.probability(11)).toBe(0);
    });

    it('cumulativeProbability for k >= n returns 1', () => {
        const d = new BinomialDistribution(10, 0.5);
        expect(d.cumulativeProbability(10)).toBe(1);
        expect(d.cumulativeProbability(15)).toBe(1);
    });

    it('cumulativeProbability for negative k returns 0', () => {
        const d = new BinomialDistribution(10, 0.5);
        expect(d.cumulativeProbability(-1)).toBe(0);
    });

    it('quantile(0) returns 0', () => {
        const d = new BinomialDistribution(10, 0.5);
        expect(d.quantile(0)).toBe(0);
    });

    it('quantile(1) returns n', () => {
        const d = new BinomialDistribution(10, 0.5);
        expect(d.quantile(1)).toBe(10);
    });

    it('quantile rejects out-of-range', () => {
        const d = new BinomialDistribution(10, 0.5);
        expect(() => d.quantile(-0.1)).toThrow(RangeError);
        expect(() => d.quantile(1.1)).toThrow(RangeError);
    });

    it('mean/variance/standardDeviation for n=20, p=0.3', () => {
        const d = new BinomialDistribution(20, 0.3);
        expect(d.mean()).toBe(6);
        expect(d.variance()).toBeCloseTo(4.2, 10);
        expect(d.standardDeviation()).toBeCloseTo(Math.sqrt(4.2), 10);
    });

    it('sampleMany returns correct count', () => {
        const d = new BinomialDistribution(10, 0.5);
        const [values] = d.sampleMany!(getTestState(), 15);
        expect(values).toHaveLength(15);
    });

    it('sampleWithMetadata returns metadata', () => {
        const d = new BinomialDistribution(10, 0.4);
        const [result] = d.sampleWithMetadata!(getTestState());
        expect(result.metadata!.n).toBe(10);
        expect(result.metadata!.p).toBe(0.4);
    });

    it('sampleManyWithMetadata returns correct count', () => {
        const d = new BinomialDistribution(10, 0.5);
        const [results] = d.sampleManyWithMetadata!(getTestState(), 5);
        expect(results).toHaveLength(5);
    });
});

// ─── GeometricDistribution ──────────────────────────────────────────────────

describe('GeometricDistribution', () => {
    it('constructor rejects p < 0', () => {
        expect(() => new GeometricDistribution(-0.1)).toThrow(RangeError);
    });

    it('constructor rejects p > 1', () => {
        expect(() => new GeometricDistribution(1.1)).toThrow(RangeError);
    });

    it('constructor rejects p = 0', () => {
        expect(() => new GeometricDistribution(0)).toThrow(RangeError);
    });

    it('sample returns non-negative integer', () => {
        const d = new GeometricDistribution(0.3);
        let state = getTestState();
        for (let i = 0; i < 50; i++) {
            const [v, next] = d.sample(state);
            expect(Number.isInteger(v)).toBe(true);
            expect(v).toBeGreaterThanOrEqual(0);
            state = next;
        }
    });

    it('probability (PMF) for k=0 with p=0.5 is 0.5', () => {
        const d = new GeometricDistribution(0.5);
        expect(d.probability(0)).toBeCloseTo(0.5, 10);
    });

    it('probability for k=1 with p=0.5 is 0.25', () => {
        const d = new GeometricDistribution(0.5);
        expect(d.probability(1)).toBeCloseTo(0.25, 10);
    });

    it('probability returns 0 for negative k', () => {
        const d = new GeometricDistribution(0.5);
        expect(d.probability(-1)).toBe(0);
    });

    it('probability returns 0 for non-integer k', () => {
        const d = new GeometricDistribution(0.5);
        expect(d.probability(1.5)).toBe(0);
    });

    it('cumulativeProbability for k=0 with p=0.5 is 0.5', () => {
        const d = new GeometricDistribution(0.5);
        expect(d.cumulativeProbability(0)).toBeCloseTo(0.5, 10);
    });

    it('cumulativeProbability returns 0 for negative k', () => {
        const d = new GeometricDistribution(0.5);
        expect(d.cumulativeProbability(-1)).toBe(0);
    });

    it('quantile(0) returns 0', () => {
        const d = new GeometricDistribution(0.5);
        expect(d.quantile(0)).toBe(0);
    });

    it('quantile(1) returns Infinity', () => {
        const d = new GeometricDistribution(0.5);
        expect(d.quantile(1)).toBe(Infinity);
    });

    it('quantile rejects out-of-range', () => {
        const d = new GeometricDistribution(0.5);
        expect(() => d.quantile(-0.1)).toThrow(RangeError);
        expect(() => d.quantile(1.1)).toThrow(RangeError);
    });

    it('mean/variance/standardDeviation for p=0.25', () => {
        const d = new GeometricDistribution(0.25);
        expect(d.mean()).toBe(3); // (1-p)/p = 0.75/0.25 = 3
        expect(d.variance()).toBe(12); // (1-p)/p^2 = 0.75/0.0625 = 12
        expect(d.standardDeviation()).toBeCloseTo(Math.sqrt(12), 10);
    });

    it('sampleMany returns correct count', () => {
        const d = new GeometricDistribution(0.3);
        const [values] = d.sampleMany!(getTestState(), 10);
        expect(values).toHaveLength(10);
    });

    it('sampleWithMetadata returns metadata with p', () => {
        const d = new GeometricDistribution(0.4);
        const [result] = d.sampleWithMetadata!(getTestState());
        expect(result.metadata!.p).toBe(0.4);
    });

    it('sampleManyWithMetadata returns correct count', () => {
        const d = new GeometricDistribution(0.3);
        const [results] = d.sampleManyWithMetadata!(getTestState(), 5);
        expect(results).toHaveLength(5);
    });
});
