import { describe, expect, it } from 'vitest';
import {
    createRandom,
    ExponentialDistribution,
    PoissonDistribution,
    BernoulliDistribution,
    BinomialDistribution,
    GeometricDistribution,
    NormalDistribution,
} from '@axrone/random';
import type { IRandomState } from '@axrone/random';

const getTestState = (seed = 42): IRandomState => {
    const r = createRandom(seed);
    return r.getEngine().getState();
};

// ─── RandomSequence validation ──────────────────────────────────────────────

describe('RandomSequence validation', () => {
    it('take(0) returns empty array', () => {
        const r = createRandom(42);
        const seq = r.sequence(() => r.int(1, 10));
        expect(seq.take(0)).toEqual([]);
    });

    it('take(-1) throws', () => {
        const r = createRandom(42);
        const seq = r.sequence(() => r.int(1, 10));
        expect(() => seq.take(-1)).toThrow(RangeError);
    });

    it('take(1.5) throws', () => {
        const r = createRandom(42);
        const seq = r.sequence(() => r.int(1, 10));
        expect(() => seq.take(1.5)).toThrow(TypeError);
    });

    it('skip(0) is a no-op', () => {
        const r = createRandom(42);
        const seq = r.sequence(() => r.int(1, 10));
        // Should not throw
        seq.skip(0);
        // Should still produce values
        const v = seq.next();
        expect(typeof v).toBe('number');
    });

    it('skip(-1) throws', () => {
        const r = createRandom(42);
        const seq = r.sequence(() => r.int(1, 10));
        expect(() => seq.skip(-1)).toThrow(RangeError);
    });

    it('skip(1.5) throws', () => {
        const r = createRandom(42);
        const seq = r.sequence(() => r.int(1, 10));
        expect(() => seq.skip(1.5)).toThrow(TypeError);
    });

    it('filter with impossible predicate throws after maxAttempts', () => {
        const r = createRandom(42);
        const seq = r.sequence(() => r.int(1, 5));
        // Predicate that can never be satisfied (values are 1-5, we want >100)
        const filtered = seq.filter((x) => x > 100, 10);
        expect(() => filtered.take(1)).toThrow(/No value matched the predicate/);
    });

    it('filter default maxAttempts is 100', () => {
        const r = createRandom(42);
        const seq = r.sequence(() => r.int(1, 5));
        // Predicate that can never be satisfied
        const filtered = seq.filter((x) => x > 100);
        expect(() => filtered.take(1)).toThrow(/No value matched the predicate after 100 attempts/);
    });
});

// ─── RandomSequence chaining ────────────────────────────────────────────────

describe('RandomSequence chaining', () => {
    it('map transforms values', () => {
        const r = createRandom(42);
        const seq = r.sequence(() => r.int(1, 3));
        const doubled = seq.map((x) => x * 2);
        const values = doubled.take(5);
        expect(values).toHaveLength(5);
        for (const v of values) {
            expect(v).toBeGreaterThanOrEqual(2);
            expect(v).toBeLessThanOrEqual(6);
            expect(Number.isInteger(v)).toBe(true);
        }
    });

    it('map chains correctly', () => {
        const r = createRandom(42);
        const seq = r.sequence(() => 5);
        const result = seq
            .map((x) => x + 1)
            .map((x) => x * 2)
            .take(3);
        expect(result).toEqual([12, 12, 12]);
    });

    it('filter then take works', () => {
        const r = createRandom(42);
        const seq = r.sequence(() => r.int(1, 10));
        const evens = seq.filter((x) => x % 2 === 0);
        const values = evens.take(5);
        expect(values).toHaveLength(5);
        for (const v of values) {
            expect(v % 2).toBe(0);
        }
    });

    it('map().filter().take() chain works', () => {
        const r = createRandom(42);
        const seq = r.sequence(() => r.int(1, 5));
        const result = seq
            .map((x) => x * 10)
            .filter((x) => x >= 30)
            .take(3);
        expect(result).toHaveLength(3);
        for (const v of result) {
            expect(v).toBeGreaterThanOrEqual(30);
        }
    });

    it('next() returns single value', () => {
        const r = createRandom(42);
        const seq = r.sequence(() => r.int(1, 100));
        const v = seq.next();
        expect(typeof v).toBe('number');
        expect(v).toBeGreaterThanOrEqual(1);
        expect(v).toBeLessThanOrEqual(100);
    });

    it('skip advances the generator', () => {
        const r = createRandom(42);
        let counter = 0;
        const seq = r.sequence(() => ++counter);

        seq.skip(3);
        expect(seq.next()).toBe(4);
    });
});

// ─── distribution-sampling.ts internals (via distribution classes) ──────────

describe('distribution-sampling internals', () => {
    it('ExponentialDistribution.sampleMany with count=0 throws', () => {
        const d = new ExponentialDistribution(1);
        expect(() => d.sampleMany!(getTestState(), 0)).toThrow(RangeError);
    });

    it('ExponentialDistribution.sampleMany with count=-1 throws', () => {
        const d = new ExponentialDistribution(1);
        expect(() => d.sampleMany!(getTestState(), -1)).toThrow(RangeError);
    });

    it('ExponentialDistribution.sampleMany with count=1.5 throws', () => {
        const d = new ExponentialDistribution(1);
        expect(() => d.sampleMany!(getTestState(), 1.5)).toThrow(RangeError);
    });

    it('PoissonDistribution.sampleMany with count=0 throws', () => {
        const d = new PoissonDistribution(5);
        expect(() => d.sampleMany!(getTestState(), 0)).toThrow(RangeError);
    });

    it('BernoulliDistribution.sampleMany with count=-1 throws', () => {
        const d = new BernoulliDistribution(0.5);
        expect(() => d.sampleMany!(getTestState(), -1)).toThrow(RangeError);
    });

    it('BinomialDistribution.sampleMany with count=0 throws', () => {
        const d = new BinomialDistribution(10, 0.5);
        expect(() => d.sampleMany!(getTestState(), 0)).toThrow(RangeError);
    });

    it('GeometricDistribution.sampleMany with count=1.5 throws', () => {
        const d = new GeometricDistribution(0.3);
        expect(() => d.sampleMany!(getTestState(), 1.5)).toThrow(RangeError);
    });

    it('sampleWithDistributionMetadata returns proper structure', () => {
        const d = new ExponentialDistribution(2);
        const [result] = d.sampleWithMetadata!(getTestState());
        // Should have value, and metadata
        expect(result).toHaveProperty('value');
        expect(result).toHaveProperty('metadata');
        expect(typeof result.value).toBe('number');
        expect(result.metadata).toBeDefined();
        expect(result.metadata!.lambda).toBe(2);
        expect(result.metadata!.mean).toBeCloseTo(0.5, 10);
    });

    it('sampleManyWithDistributionMetadata returns array of metadata objects', () => {
        const d = new NormalDistribution(0, 1);
        const [results] = d.sampleManyWithMetadata!(getTestState(), 5);
        expect(results).toHaveLength(5);
        for (const r of results) {
            expect(r).toHaveProperty('value');
            expect(r).toHaveProperty('zscore');
            expect(r).toHaveProperty('metadata');
            expect(typeof r.value).toBe('number');
            expect(typeof r.zscore).toBe('number');
        }
    });

    it('sampleManyFromDistribution produces correct count (via Exponential)', () => {
        const d = new ExponentialDistribution(1);
        const [values] = d.sampleMany!(getTestState(), 25);
        expect(values).toHaveLength(25);
        for (const v of values) {
            expect(v).toBeGreaterThanOrEqual(0);
            expect(Number.isFinite(v)).toBe(true);
        }
    });

    it('sampleManyFromDistribution state advances (via Bernoulli)', () => {
        const d = new BernoulliDistribution(0.5);
        const stateBefore = getTestState();
        const [values, stateAfter] = d.sampleMany!(stateBefore, 10);
        expect(values).toHaveLength(10);
        // State should have advanced
        expect(stateAfter.counter).not.toBe(stateBefore.counter);
    });
});
