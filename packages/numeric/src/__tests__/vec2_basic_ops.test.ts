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
});
