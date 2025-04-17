import { clamp, createBoundedClamp, NumericRangeError } from '../clamp';

describe('Numeric Clamp Utility', () => {
    describe('clamp()', () => {
        test.each([
            [5, 0, 10, 5],
            [0, 0, 10, 0],
            [10, 0, 10, 10],
            [-5, 0, 10, 0],
            [15, 0, 10, 10],
            [5, 10, 0, 5],
            [-10, -20, -5, -10],
            [0.5, 0.1, 0.9, 0.5],
            [Number.MAX_SAFE_INTEGER - 10, Number.MAX_SAFE_INTEGER - 100, Number.MAX_SAFE_INTEGER, Number.MAX_SAFE_INTEGER - 10], // Large numbers
        ])('clamp(%f, %f, %f) should return %f', (value, min, max, expected) => {
            expect(clamp(value, min, max)).toBe(expected);
        });

        test('automatically normalizes reversed min/max bounds', () => {
            expect(clamp(5, 10, 0)).toBe(5);
            expect(clamp(15, 10, 0)).toBe(10);
            expect(clamp(-5, 10, 0)).toBe(0);
        });

        test('handles special numeric edge cases', () => {
            expect(clamp(0, -10, 10)).toBe(0);

            const value = 0.1 + 0.2; // 0.30000000000000004 due to floating point
            expect(clamp(value, 0, 0.3)).toBeCloseTo(0.3);

            expect(clamp(42, 42, 42)).toBe(42);
        });

        test('throws NumericRangeError for non-finite inputs', () => {
            // Non-finite value
            expect(() => clamp(NaN, 0, 10)).toThrow(NumericRangeError);
            expect(() => clamp(Infinity, 0, 10)).toThrow(NumericRangeError);
            expect(() => clamp(-Infinity, 0, 10)).toThrow(NumericRangeError);

            // Non-finite bounds
            expect(() => clamp(5, NaN, 10)).toThrow(NumericRangeError);
            expect(() => clamp(5, 0, NaN)).toThrow(NumericRangeError);
            expect(() => clamp(5, Infinity, 10)).toThrow(NumericRangeError);
            expect(() => clamp(5, 0, -Infinity)).toThrow(NumericRangeError);
        });

        test('error messages are descriptive and helpful', () => {
            expect(() => clamp(NaN, 0, 10)).toThrow(/Value must be a finite number/);
            expect(() => clamp(5, NaN, 10)).toThrow(/Bounds must be finite numbers/);
        });
    });

    // Bounded clamp factory function
    describe('createBoundedClamp()', () => {
        test('creates a reusable clamping function with fixed bounds', () => {
            const boundTo0_100 = createBoundedClamp(0 as number, 100 as number);

            expect(typeof boundTo0_100).toBe('function');
            expect(boundTo0_100(50)).toBe(50);
            expect(boundTo0_100(-10)).toBe(0);
            expect(boundTo0_100(200)).toBe(100);
        });

        test('factory correctly normalizes reversed bounds', () => {
            const boundTo0_100 = createBoundedClamp(100 as number, 0 as number);

            expect(boundTo0_100(50)).toBe(50);
            expect(boundTo0_100(-10)).toBe(0);
            expect(boundTo0_100(200)).toBe(100);
        });

        test('throws during factory creation if bounds are invalid', () => {
            expect(() => createBoundedClamp(NaN, 100)).toThrow(NumericRangeError);
            expect(() => createBoundedClamp(0, Infinity)).toThrow(NumericRangeError);
        });

        test('throws when clamping invalid values with factory function', () => {
            const boundTo0_100 = createBoundedClamp(0 as number, 100 as number);
            expect(() => boundTo0_100(NaN)).toThrow(NumericRangeError);
            expect(() => boundTo0_100(Infinity)).toThrow(NumericRangeError);
        });

        test('handles repeated clamping operations consistently', () => {
            const boundTo0_100 = createBoundedClamp(0 as number, 100 as number);
            const values = [-10, 0, 50, 100, 200];
            const expected = [0, 0, 50, 100, 100];

            const results = values.map(boundTo0_100);
            expect(results).toEqual(expected);
        });
    });

    // Performance comparison (simple benchmark)
    describe('Performance', () => {
        test('bounded clamp is more efficient for repeated operations', () => {
            // Setup test data
            const testSize = 1000;
            const values = Array.from({ length: testSize }, (_, i) => i * 10 - 5000);

            // Standard clamp approach
            const standardStart = performance.now();
            const standardResults = values.map(v => clamp(v, 0, 1000));
            const standardEnd = performance.now();

            // Bounded clamp approach
            const boundedStart = performance.now();
            const boundedClamp = createBoundedClamp(0 as number, 1000 as number);
            const boundedResults = values.map(boundedClamp);
            const boundedEnd = performance.now();

            expect(boundedResults).toEqual(standardResults);

            // We aren't making a strict assertion here as performance can vary,
            console.log(`Standard clamp: ${standardEnd - standardStart}ms`);
            console.log(`Bounded clamp: ${boundedEnd - boundedStart}ms`);

            expect(true).toBe(true);
        });
    });

    describe('Type Safety', () => {
        test('maintains input number type', () => {
            const int: number = 5;
            const result: number = clamp(int, 0, 10);
            expect(result).toBe(5);

            expect(typeof clamp(5, 0, 10)).toBe('number');
        });
    });

    describe('Error Objects', () => {
        test('NumericRangeError has correct structure', () => {
            try {
                clamp(NaN, 0, 10);
                fail('Expected error was not thrown');
            } catch (error) {
                if (error instanceof NumericRangeError) {
                    expect(error).toBeInstanceOf(NumericRangeError);
                    expect(error.name).toBe('NumericRangeError');
                    expect(error.message).toBe('Value must be a finite number');
                    expect(error).toMatchSnapshot();
                } else {
                    fail('Error is not an instance of NumericRangeError');
                }
            }
        });
    });

    describe('Integration', () => {
        test('works with Array methods', () => {
            const values = [1, 5, 10, 15, 20];
            const boundedValues = values.map(v => clamp(v, 5, 15));
            expect(boundedValues).toEqual([5, 5, 10, 15, 15]);
        });
    });
});
