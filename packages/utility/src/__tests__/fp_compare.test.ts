import { FpCompare } from '../comparer/fp_compare';

describe('FpCompare Class - Test Suite', () => {
    const DEFAULT_EPSILON = Number.EPSILON;
    const DEFAULT_ABS_THRESHOLD = Math.min(Math.abs(Number.MIN_VALUE), DEFAULT_EPSILON);
    const CUSTOM_EPSILON = 1e-6;
    const CUSTOM_ABS_THRESHOLD = 1e-10;

    const ITERATION_COUNT = 1000;

    function benchmark(fn: () => void, iterations: number = ITERATION_COUNT): number {
        const start = performance.now();
        for (let i = 0; i < iterations; i++) {
            fn();
        }
        return (performance.now() - start) / iterations;
    }

    describe('Constructor and Initialization', () => {
        test('creates instance with default parameters', () => {
            const comparer = new FpCompare();
            expect(comparer.getEpsilon()).toBe(DEFAULT_EPSILON);
            expect(comparer.getAbsThreshold()).toBe(DEFAULT_ABS_THRESHOLD);
        });

        test('creates instance with custom parameters', () => {
            const comparer = new FpCompare(CUSTOM_EPSILON, CUSTOM_ABS_THRESHOLD);
            expect(comparer.getEpsilon()).toBe(CUSTOM_EPSILON);
            expect(comparer.getAbsThreshold()).toBe(CUSTOM_ABS_THRESHOLD);
        });

        test('creates instance with only epsilon parameter', () => {
            const comparer = new FpCompare(CUSTOM_EPSILON);
            expect(comparer.getEpsilon()).toBe(CUSTOM_EPSILON);
            expect(comparer.getAbsThreshold()).toBe(
                Math.min(Math.abs(Number.MIN_VALUE), CUSTOM_EPSILON)
            );
        });

        test('throws RangeError when epsilon is 0', () => {
            expect(() => new FpCompare(0)).toThrow(RangeError);
            expect(() => new FpCompare(0)).toThrow('Epsilon must be between 0 and 1 (exclusive)');
        });

        test('throws RangeError when epsilon is negative', () => {
            expect(() => new FpCompare(-0.1)).toThrow(RangeError);
            expect(() => new FpCompare(-0.1)).toThrow(
                'Epsilon must be between 0 and 1 (exclusive)'
            );
        });

        test('throws RangeError when epsilon is 1 or greater', () => {
            expect(() => new FpCompare(1)).toThrow(RangeError);
            expect(() => new FpCompare(1.5)).toThrow(RangeError);
            expect(() => new FpCompare(1)).toThrow('Epsilon must be between 0 and 1 (exclusive)');
        });

        test('throws RangeError when absThreshold is 0', () => {
            expect(() => new FpCompare(DEFAULT_EPSILON, 0)).toThrow(RangeError);
            expect(() => new FpCompare(DEFAULT_EPSILON, 0)).toThrow(
                'absThreshold must be positive'
            );
        });

        test('throws RangeError when absThreshold is negative', () => {
            expect(() => new FpCompare(DEFAULT_EPSILON, -1e-10)).toThrow(RangeError);
            expect(() => new FpCompare(DEFAULT_EPSILON, -1e-10)).toThrow(
                'absThreshold must be positive'
            );
        });
    });

    describe('nearlyEqual Method', () => {
        test('returns true for identical values', () => {
            const comparer = new FpCompare();
            expect(comparer.nearlyEqual(0, 0)).toBe(true);
            expect(comparer.nearlyEqual(1, 1)).toBe(true);
            expect(comparer.nearlyEqual(-1, -1)).toBe(true);
            expect(comparer.nearlyEqual(123.456, 123.456)).toBe(true);
        });

        test('returns true for nearly equal values within epsilon', () => {
            const comparer = new FpCompare(1e-5);
            expect(comparer.nearlyEqual(1.0, 1.0 + 0.5e-5)).toBe(true);
            expect(comparer.nearlyEqual(1000.0, 1000.0 + 1e-5 * 1000.0 * 0.9)).toBe(true);
            expect(comparer.nearlyEqual(-1.0, -1.0 - 0.5e-5)).toBe(true);
        });
    });

    // ...
});
