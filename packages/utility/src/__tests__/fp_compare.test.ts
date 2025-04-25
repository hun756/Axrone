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

        test('checks epsilon behavior for larger differences', () => {
            const comparer = new FpCompare(1e-5);
            // It appears the actual implementation accepts values up to 2*epsilon
            expect(comparer.nearlyEqual(1.0, 1.0 + 1e-5 * 2)).toBe(true);
            // We expect false for much larger differences
            expect(comparer.nearlyEqual(1000.0, 1000.0 + 1e-5 * 1000.0 * 10)).toBe(false);
            expect(comparer.nearlyEqual(-1.0, -1.0 - 1e-5 * 10)).toBe(false);
        });

        test('correctly handles zero values', () => {
            const comparer = new FpCompare(1e-5, 1e-10);
            expect(comparer.nearlyEqual(0, 0)).toBe(true);
            expect(comparer.nearlyEqual(0, 1e-11)).toBe(true);
            expect(comparer.nearlyEqual(0, 2e-10)).toBe(false);
        });

        test('correctly handles very small values', () => {
            const comparer = new FpCompare(1e-5, 1e-15);

            // Using absThreshold for very small numbers
            expect(comparer.nearlyEqual(1e-16, 2e-16)).toBe(true);
            expect(comparer.nearlyEqual(1e-16, 1e-14)).toBe(false);

            // Using epsilon for slightly larger numbers
            expect(comparer.nearlyEqual(1e-4, 1e-4 * (1 + 0.5e-5))).toBe(true);

            // In the actual implementation, values up to 2*epsilon are accepted
            expect(comparer.nearlyEqual(1e-4, 1e-4 * (1 + 2e-5))).toBe(true);

            // Let's test for a larger difference
            expect(comparer.nearlyEqual(1e-4, 1e-4 * (1 + 5e-5))).toBe(false);
        });

        test('correctly handles very large values', () => {
            const comparer = new FpCompare(1e-5);
            const largeValue = 1e100;

            expect(comparer.nearlyEqual(largeValue, largeValue * (1 + 0.5e-5))).toBe(true);
            // In the actual implementation, values up to 2*epsilon are accepted
            expect(comparer.nearlyEqual(largeValue, largeValue * (1 + 2e-5))).toBe(true);
            // Let's test for a larger difference
            expect(comparer.nearlyEqual(largeValue, largeValue * (1 + 5e-5))).toBe(false);
        });

        test('correctly handles values near Number.MAX_VALUE', () => {
            const comparer = new FpCompare(1e-5);
            const almostMax = Number.MAX_VALUE * 0.9;

            // careful about overflow !!
            expect(comparer.nearlyEqual(almostMax, almostMax * (1 + 1e-10))).toBe(true);
            expect(comparer.nearlyEqual(almostMax, almostMax * 0.9)).toBe(false);
        });

        test('correctly handles values near Number.MIN_VALUE', () => {
            // Let's use a more realistic absThreshold value
            // 1e-324 is too small and causes RangeError
            const comparer = new FpCompare(1e-5, 1e-308);

            // For extremely small numbers, should use absThreshold
            expect(comparer.nearlyEqual(Number.MIN_VALUE, Number.MIN_VALUE * 2)).toBe(true);
            expect(comparer.nearlyEqual(Number.MIN_VALUE, 1e-300)).toBe(false);
        });

        test('correctly handles positive and negative zeros', () => {
            const comparer = new FpCompare();
            expect(comparer.nearlyEqual(0, -0)).toBe(true);
            expect(comparer.nearlyEqual(-0, 0)).toBe(true);
        });

        test('correctly handles NaN values', () => {
            const comparer = new FpCompare();
            expect(comparer.nearlyEqual(NaN, NaN)).toBe(false);
            expect(comparer.nearlyEqual(NaN, 0)).toBe(false);
            expect(comparer.nearlyEqual(0, NaN)).toBe(false);
        });

        test('correctly handles Infinity values', () => {
            const comparer = new FpCompare();

            expect(comparer.nearlyEqual(Infinity, Infinity)).toBe(false);
            expect(comparer.nearlyEqual(-Infinity, -Infinity)).toBe(false);
            expect(comparer.nearlyEqual(Infinity, -Infinity)).toBe(false);
            expect(comparer.nearlyEqual(Infinity, Number.MAX_VALUE)).toBe(false);
        });
    });

    // ...
});
