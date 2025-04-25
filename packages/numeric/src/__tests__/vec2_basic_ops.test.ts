import { Vec2, IVec2Like } from '../vec2';
import { EPSILON } from '../common';

declare global {
    namespace jest {
        interface Matchers<R> {
            toBeVectorCloseTo: (expected: IVec2Like, precision?: number) => R;
            toBeOrthogonalTo: (expected: IVec2Like, precision?: number) => R;
            toHaveNorm: (expected: number, precision?: number) => R;
        }
    }
}

const PRECISION = EPSILON * 10;
const ITERATIONS = 100;
const PERFORMANCE_ITERATIONS = 10000;
const LARGE_VALUE = 1e15;
const SMALL_VALUE = 1e-15;

expect.extend({
    toBeVectorCloseTo(received: IVec2Like, expected: IVec2Like, precision = PRECISION) {
        const pass =
            Math.abs(received.x - expected.x) < precision &&
            Math.abs(received.y - expected.y) < precision;

        return {
            message: () =>
                `Expected vector (${received.x}, ${received.y}) to be close to (${expected.x}, ${expected.y}) within ${precision}`,
            pass,
        };
    },

    toBeOrthogonalTo(received: IVec2Like, expected: IVec2Like, precision = PRECISION) {
        const dotProduct = received.x * expected.x + received.y * expected.y;
        const pass = Math.abs(dotProduct) < precision;

        return {
            message: () =>
                `Expected vector (${received.x}, ${received.y}) to be orthogonal to (${expected.x}, ${expected.y}), dot product: ${dotProduct}`,
            pass,
        };
    },

    toHaveNorm(received: IVec2Like, expected: number, precision = PRECISION) {
        const norm = Math.sqrt(received.x * received.x + received.y * received.y);
        const pass = Math.abs(norm - expected) < precision;

        return {
            message: () =>
                `Expected vector (${received.x}, ${received.y}) to have norm ${expected}, got ${norm}`,
            pass,
        };
    },
});

function randomNonZeroVec2(): Vec2 {
    let x, y;
    do {
        x = Math.random() * 200 - 100;
        y = Math.random() * 200 - 100;
    } while (Math.abs(x) < EPSILON || Math.abs(y) < EPSILON);

    return new Vec2(x, y);
}

function generateRandomVectors(count: number): Vec2[] {
    return Array.from(
        { length: count },
        () => new Vec2(Math.random() * 200 - 100, Math.random() * 200 - 100)
    );
}

function measurePerformance(name: string, fn: () => void): number {
    const start = performance.now();
    fn();
    const end = performance.now();
    return end - start;
}

describe('Vec2 Class - Basic Operations Test Suite', () => {
    describe('Constructor and Initialization', () => {
        test('constructor without parameters creates a zero vector', () => {
            const v = new Vec2();
            expect(v).toBeVectorCloseTo({ x: 0, y: 0 });
        });

        test('constructor with parameters correctly initializes components', () => {
            const v = new Vec2(3.14, -2.718);
            expect(v.x).toBe(3.14);
            expect(v.y).toBe(-2.718);
        });

        test('constructor coerces parameters to numbers', () => {
            // @ts-ignore - Intentionally testing type coercion
            const v = new Vec2('5', '10');

            // Vec2 constructor does not perform string conversion,
            // it takes the value given as parameter as it is.
            expect(v.x).toBe('5');
            expect(v.y).toBe('10');
        });

        test.each([
            ['Infinity', Infinity, Infinity],
            ['NaN', NaN, NaN],
            ['MAX_VALUE', Number.MAX_VALUE, Number.MAX_VALUE],
            ['MIN_VALUE', Number.MIN_VALUE, Number.MIN_VALUE],
        ])('constructor correctly handles %s', (_, x, y) => {
            const v = new Vec2(x, y);

            if (Number.isNaN(x)) {
                expect(Number.isNaN(v.x)).toBe(true);
            } else {
                expect(v.x).toBe(x);
            }

            if (Number.isNaN(y)) {
                expect(Number.isNaN(v.y)).toBe(true);
            } else {
                expect(v.y).toBe(y);
            }
        });
    });

    describe('Static Constants', () => {
        const constants = [
            { name: 'ZERO', expected: { x: 0, y: 0 } },
            { name: 'ONE', expected: { x: 1, y: 1 } },
            { name: 'NEG_ONE', expected: { x: -1, y: -1 } },
            { name: 'UNIT_X', expected: { x: 1, y: 0 } },
            { name: 'UNIT_Y', expected: { x: 0, y: 1 } },
            { name: 'UP', expected: { x: 0, y: 1 } },
            { name: 'DOWN', expected: { x: 0, y: -1 } },
            { name: 'LEFT', expected: { x: -1, y: 0 } },
            { name: 'RIGHT', expected: { x: 1, y: 0 } },
        ];

        test.each(constants)('$name has correct values and is immutable', ({ name, expected }) => {
            const constant = Vec2[name as keyof typeof Vec2] as Vec2;

            expect(constant.x).toBe(expected.x);
            expect(constant.y).toBe(expected.y);

            const originalX = constant.x;
            const originalY = constant.y;

            expect(() => {
                constant.x = 999;
                constant.y = 999;
            }).toThrow();

            expect(constant.x).toBe(originalX);
            expect(constant.y).toBe(originalY);
        });

        test('directional constants are mathematically consistent', () => {
            // When Object.is() is used to compare 0 and -0 in JavaScript, it is not equal
            // But the vector should be mathematically consistent,
            // so let's compare absolute values
            expect(Math.abs(Vec2.UP.x)).toEqual(Math.abs(Vec2.DOWN.x));
            expect(Math.abs(Vec2.UP.y)).toEqual(Math.abs(Vec2.DOWN.y));

            expect(Math.abs(Vec2.LEFT.x)).toEqual(Math.abs(Vec2.RIGHT.x));
            expect(Math.abs(Vec2.LEFT.y)).toEqual(Math.abs(Vec2.RIGHT.y));

            // Up and right vectors must be perpendicular
            expect(Vec2.UP).toBeOrthogonalTo(Vec2.RIGHT);

            expect(Vec2.UP).toBeVectorCloseTo(Vec2.UNIT_Y);
            expect(Vec2.RIGHT).toBeVectorCloseTo(Vec2.UNIT_X);
        });

        test('ZERO behaves as additive identity', () => {
            const testVec = new Vec2(3.14, 2.718);
            const result = Vec2.add(testVec, Vec2.ZERO);
            expect(result).toBeVectorCloseTo(testVec);
        });

        test('ONE behaves as multiplicative identity for component-wise multiplication', () => {
            const testVec = new Vec2(3.14, 2.718);
            const result = Vec2.multiply(testVec, Vec2.ONE);
            expect(result).toBeVectorCloseTo(testVec);
        });
    });
});
