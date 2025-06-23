import { Vec4, Vec4ComparisonMode, Vec4Comparer, Vec4EqualityComparer, IVec4Like } from '../vec4';
import { EPSILON } from '../common';

const CUSTOM_EPSILON = 1e-10;
const LARGE_NUMBER = 1e6;
const SMALL_NUMBER = 1e-6;

const expectVectorClose = (actual: IVec4Like, expected: IVec4Like, epsilon = EPSILON) => {
    expect(Math.abs(actual.x - expected.x)).toBeLessThan(epsilon);
    expect(Math.abs(actual.y - expected.y)).toBeLessThan(epsilon);
    expect(Math.abs(actual.z - expected.z)).toBeLessThan(epsilon);
    expect(Math.abs(actual.w - expected.w)).toBeLessThan(epsilon);
};

const expectNumberClose = (actual: number, expected: number, epsilon = EPSILON) => {
    expect(Math.abs(actual - expected)).toBeLessThan(epsilon);
};

const createRandomVec4 = (scale = 100): Vec4 => {
    return new Vec4(
        (Math.random() - 0.5) * scale,
        (Math.random() - 0.5) * scale,
        (Math.random() - 0.5) * scale,
        (Math.random() - 0.5) * scale
    );
};

describe('Vec4 Professional Unit Tests', () => {
    describe('Constructor and Creation', () => {
        test('should create zero vector by default', () => {
            const vec = new Vec4();
            expectVectorClose(vec, { x: 0, y: 0, z: 0, w: 0 });
        });

        test('should create vector with specified components', () => {
            const vec = new Vec4(1, 2, 3, 4);
            expectVectorClose(vec, { x: 1, y: 2, z: 3, w: 4 });
        });

        test('should create vector with partial components', () => {
            const vec1 = new Vec4(5);
            expectVectorClose(vec1, { x: 5, y: 0, z: 0, w: 0 });

            const vec2 = new Vec4(5, 10);
            expectVectorClose(vec2, { x: 5, y: 10, z: 0, w: 0 });

            const vec3 = new Vec4(5, 10, 15);
            expectVectorClose(vec3, { x: 5, y: 10, z: 15, w: 0 });
        });

        test('should create from IVec4Like object', () => {
            const source = { x: 1.5, y: 2.5, z: 3.5, w: 4.5 };
            const vec = Vec4.from(source);
            expectVectorClose(vec, source);
        });

        test('should create from array with default offset', () => {
            const arr = [1, 2, 3, 4, 5, 6];
            const vec = Vec4.fromArray(arr);
            expectVectorClose(vec, { x: 1, y: 2, z: 3, w: 4 });
        });

        test('should create from array with custom offset', () => {
            const arr = [0, 0, 1, 2, 3, 4, 5];
            const vec = Vec4.fromArray(arr, 2);
            expectVectorClose(vec, { x: 1, y: 2, z: 3, w: 4 });
        });

        test('should throw error for negative offset', () => {
            const arr = [1, 2, 3, 4];
            expect(() => Vec4.fromArray(arr, -1)).toThrow('Offset cannot be negative');
        });

        test('should throw error for insufficient array length', () => {
            const arr = [1, 2];
            expect(() => Vec4.fromArray(arr)).toThrow('Array must have at least 4 elements');
            expect(() => Vec4.fromArray(arr, 1)).toThrow(
                'Array must have at least 5 elements when using offset 1'
            );
        });

        test('should create using static create method', () => {
            const vec = Vec4.create(1, 2, 3, 4);
            expectVectorClose(vec, { x: 1, y: 2, z: 3, w: 4 });
        });
    });

    describe('Static Constants', () => {
        test('should have correct static constant values', () => {
            expectVectorClose(Vec4.ZERO, { x: 0, y: 0, z: 0, w: 0 });
            expectVectorClose(Vec4.ONE, { x: 1, y: 1, z: 1, w: 1 });
            expectVectorClose(Vec4.NEG_ONE, { x: -1, y: -1, z: -1, w: -1 });
            expectVectorClose(Vec4.UNIT_X, { x: 1, y: 0, z: 0, w: 0 });
            expectVectorClose(Vec4.UNIT_Y, { x: 0, y: 1, z: 0, w: 0 });
            expectVectorClose(Vec4.UNIT_Z, { x: 0, y: 0, z: 1, w: 0 });
            expectVectorClose(Vec4.UNIT_W, { x: 0, y: 0, z: 0, w: 1 });
        });

        test('should have immutable static constants', () => {
            expect(Object.isFrozen(Vec4.ZERO)).toBe(true);
            expect(Object.isFrozen(Vec4.ONE)).toBe(true);
            expect(Object.isFrozen(Vec4.UNIT_X)).toBe(true);
        });
    });

    // CLONE AND EQUALITY TESTS
    describe('Clone and Equality', () => {
        test('should clone vector correctly', () => {
            const original = new Vec4(1, 2, 3, 4);
            const cloned = original.clone();

            expectVectorClose(cloned, original);
            expect(cloned).not.toBe(original);
        });

        test('should check equality correctly', () => {
            const vec1 = new Vec4(1, 2, 3, 4);
            const vec2 = new Vec4(1, 2, 3, 4);
            const vec3 = new Vec4(1, 2, 3, 4.1);

            expect(vec1.equals(vec2)).toBe(true);
            expect(vec1.equals(vec3)).toBe(false);
            expect(vec1.equals(null)).toBe(false);
            expect(vec1.equals('not a vector')).toBe(false);
        });

        test('should handle floating point precision in equality', () => {
            const vec1 = new Vec4(1, 2, 3, 4);
            const vec2 = new Vec4(
                1 + EPSILON * 0.5,
                2 + EPSILON * 0.5,
                3 + EPSILON * 0.5,
                4 + EPSILON * 0.5
            );

            expect(vec1.equals(vec2)).toBe(true);
        });

        test('should generate consistent hash codes', () => {
            const vec1 = new Vec4(1, 2, 3, 4);
            const vec2 = new Vec4(1, 2, 3, 4);
            const vec3 = new Vec4(5, 6, 7, 8);

            expect(vec1.getHashCode()).toBe(vec2.getHashCode());
            expect(vec1.getHashCode()).not.toBe(vec3.getHashCode());
        });
    });

    // BASIC ARITHMETIC OPERATIONS TESTS
    describe('Basic Arithmetic Operations', () => {
        describe('Addition', () => {
            test('should add vectors correctly (static)', () => {
                const a = new Vec4(1, 2, 3, 4);
                const b = new Vec4(5, 6, 7, 8);
                const result = Vec4.add(a, b);

                expectVectorClose(result, { x: 6, y: 8, z: 10, w: 12 });
            });

            test('should add vectors with output parameter', () => {
                const a = new Vec4(1, 2, 3, 4);
                const b = new Vec4(5, 6, 7, 8);
                const out = new Vec4();
                const result = Vec4.add(a, b, out);

                expect(result).toBe(out);
                expectVectorClose(out, { x: 6, y: 8, z: 10, w: 12 });
            });

            test('should add scalar correctly', () => {
                const a = new Vec4(1, 2, 3, 4);
                const result = Vec4.addScalar(a, 5);

                expectVectorClose(result, { x: 6, y: 7, z: 8, w: 9 });
            });

            test('should add vectors correctly (instance)', () => {
                const a = new Vec4(1, 2, 3, 4);
                const b = new Vec4(5, 6, 7, 8);
                const result = a.add(b);

                expect(result).toBe(a);
                expectVectorClose(a, { x: 6, y: 8, z: 10, w: 12 });
            });

            test('should add scalar correctly (instance)', () => {
                const a = new Vec4(1, 2, 3, 4);
                const result = a.addScalar(5);

                expect(result).toBe(a);
                expectVectorClose(a, { x: 6, y: 7, z: 8, w: 9 });
            });
        });

        describe('Subtraction', () => {
            test('should subtract vectors correctly (static)', () => {
                const a = new Vec4(5, 6, 7, 8);
                const b = new Vec4(1, 2, 3, 4);
                const result = Vec4.subtract(a, b);

                expectVectorClose(result, { x: 4, y: 4, z: 4, w: 4 });
            });

            test('should subtract scalar correctly', () => {
                const a = new Vec4(5, 6, 7, 8);
                const result = Vec4.subtractScalar(a, 2);

                expectVectorClose(result, { x: 3, y: 4, z: 5, w: 6 });
            });

            test('should subtract vectors correctly (instance)', () => {
                const a = new Vec4(5, 6, 7, 8);
                const b = new Vec4(1, 2, 3, 4);
                const result = a.subtract(b);

                expect(result).toBe(a);
                expectVectorClose(a, { x: 4, y: 4, z: 4, w: 4 });
            });
        });

        describe('Multiplication', () => {
            test('should multiply vectors correctly (static)', () => {
                const a = new Vec4(2, 3, 4, 5);
                const b = new Vec4(1, 2, 3, 4);
                const result = Vec4.multiply(a, b);

                expectVectorClose(result, { x: 2, y: 6, z: 12, w: 20 });
            });

            test('should multiply by scalar correctly', () => {
                const a = new Vec4(1, 2, 3, 4);
                const result = Vec4.multiplyScalar(a, 3);

                expectVectorClose(result, { x: 3, y: 6, z: 9, w: 12 });
            });

            test('should handle zero multiplication', () => {
                const a = new Vec4(1, 2, 3, 4);
                const result = Vec4.multiplyScalar(a, 0);

                expectVectorClose(result, { x: 0, y: 0, z: 0, w: 0 });
            });

            test('should handle negative scalar multiplication', () => {
                const a = new Vec4(1, 2, 3, 4);
                const result = Vec4.multiplyScalar(a, -2);

                expectVectorClose(result, { x: -2, y: -4, z: -6, w: -8 });
            });
        });

        describe('Division', () => {
            test('should divide vectors correctly (static)', () => {
                const a = new Vec4(6, 8, 12, 16);
                const b = new Vec4(2, 4, 3, 4);
                const result = Vec4.divide(a, b);

                expectVectorClose(result, { x: 3, y: 2, z: 4, w: 4 });
            });

            test('should divide by scalar correctly', () => {
                const a = new Vec4(6, 8, 12, 16);
                const result = Vec4.divideScalar(a, 2);

                expectVectorClose(result, { x: 3, y: 4, z: 6, w: 8 });
            });

            test('should throw error for division by zero vector', () => {
                const a = new Vec4(1, 2, 3, 4);
                const b = new Vec4(0, 1, 2, 3);

                expect(() => Vec4.divide(a, b)).toThrow(
                    'Division by zero or near-zero value is not allowed'
                );
            });

            test('should throw error for division by zero scalar', () => {
                const a = new Vec4(1, 2, 3, 4);

                expect(() => Vec4.divideScalar(a, 0)).toThrow(
                    'Division by zero or near-zero value is not allowed'
                );
            });

            test('should throw error for division by near-zero values', () => {
                const a = new Vec4(1, 2, 3, 4);
                const b = new Vec4(EPSILON * 0.5, 1, 2, 3);

                expect(() => Vec4.divide(a, b)).toThrow(
                    'Division by zero or near-zero value is not allowed'
                );
            });
        });

        describe('Negation', () => {
            test('should negate vector correctly', () => {
                const a = new Vec4(1, -2, 3, -4);
                const result = Vec4.negate(a);

                expectVectorClose(result, { x: -1, y: 2, z: -3, w: 4 });
            });

            test('should handle zero negation correctly', () => {
                const a = new Vec4(0, 1, 0, -1);
                const result = Vec4.negate(a);

                expectVectorClose(result, { x: 0, y: -1, z: 0, w: 1 });
            });
        });

        describe('Inverse', () => {
            test('should calculate inverse correctly', () => {
                const a = new Vec4(2, 4, 0.5, 0.25);
                const result = Vec4.inverse(a);

                expectVectorClose(result, { x: 0.5, y: 0.25, z: 2, w: 4 });
            });

            test('should calculate safe inverse correctly', () => {
                const a = new Vec4(2, 1, 0.5, 0.25);
                const result = Vec4.inverseSafe(a, undefined, 999);

                expectVectorClose(result, { x: 0.5, y: 1, z: 2, w: 4 });
            });

            test('should throw error for inverse of zero', () => {
                const a = new Vec4(2, 0, 0.5, 1);

                expect(() => Vec4.inverseSafe(a)).toThrow('Inversion of zero or near-zero value');
            });
        });
    });

    // VECTOR MATHEMATICS TESTS
    describe('Vector Mathematics', () => {
        describe('Dot Product', () => {
            test('should calculate dot product correctly', () => {
                const a = new Vec4(1, 2, 3, 4);
                const b = new Vec4(5, 6, 7, 8);

                expect(Vec4.dot(a, b)).toBe(70);
                expect(a.dot(b)).toBe(70);
            });

            test('should calculate dot product with self (length squared)', () => {
                const a = new Vec4(1, 2, 3, 4);

                expect(Vec4.dot(a, a)).toBe(30);
                expect(a.lengthSquared()).toBe(30);
            });

            test('should return zero for orthogonal vectors', () => {
                const a = new Vec4(1, 0, 0, 0);
                const b = new Vec4(0, 1, 0, 0);

                expect(Vec4.dot(a, b)).toBe(0);
            });
        });

        describe('Cross Product (3D)', () => {
            test('should calculate 3D cross product correctly', () => {
                const a = new Vec4(1, 0, 0, 5);
                const b = new Vec4(0, 1, 0, 10);
                const result = Vec4.cross3D(a, b);

                expectVectorClose(result, { x: 0, y: 0, z: 1, w: 0 });
            });

            test('should calculate cross product with standard basis vectors', () => {
                const x = new Vec4(1, 0, 0, 0);
                const y = new Vec4(0, 1, 0, 0);
                const z = new Vec4(0, 0, 1, 0);

                expectVectorClose(Vec4.cross3D(x, y), { x: 0, y: 0, z: 1, w: 0 });
                expectVectorClose(Vec4.cross3D(y, z), { x: 1, y: 0, z: 0, w: 0 });
                expectVectorClose(Vec4.cross3D(z, x), { x: 0, y: 1, z: 0, w: 0 });
            });

            test('should return zero for parallel vectors', () => {
                const a = new Vec4(1, 2, 3, 0);
                const b = new Vec4(2, 4, 6, 0);
                const result = Vec4.cross3D(a, b);

                expectVectorClose(result, { x: 0, y: 0, z: 0, w: 0 });
            });
        });

        describe('Length and Normalization', () => {
            test('should calculate length correctly', () => {
                const a = new Vec4(3, 4, 0, 0);
                expect(Vec4.len(a)).toBe(5);
                expect(a.length()).toBe(5);
            });

            test('should calculate length squared correctly', () => {
                const a = new Vec4(1, 2, 3, 4);
                expect(Vec4.lengthSquared(a)).toBe(30);
                expect(a.lengthSquared()).toBe(30);
            });

            test('should calculate fast length approximation', () => {
                const a = new Vec4(3, 4, 0, 0);
                const fastLen = Vec4.fastLength(a);
                const realLen = Vec4.len(a);

                expect(Math.abs(fastLen - realLen) / realLen).toBeLessThan(0.3);
            });

            test('should normalize vector correctly', () => {
                const a = new Vec4(3, 4, 0, 0);
                const normalized = Vec4.normalize(a);

                expectNumberClose(Vec4.len(normalized), 1);
                expectVectorClose(normalized, { x: 0.6, y: 0.8, z: 0, w: 0 });
            });

            test('should normalize vector in place', () => {
                const a = new Vec4(3, 4, 0, 0);
                const result = a.normalize();

                expect(result).toBe(a);
                expectNumberClose(a.length(), 1);
            });

            test('should throw error when normalizing zero vector', () => {
                const zero = new Vec4(0, 0, 0, 0);

                expect(() => Vec4.normalize(zero)).toThrow('Cannot normalize a zero-length vector');
                expect(() => zero.normalize()).toThrow('Cannot normalize a zero-length vector');
            });

            test('should use Quake fast inverse square root', () => {
                const a = new Vec4(3, 4, 0, 0);
                const normalized = Vec4.normalizeQuake(a);

                // No 1e-3
                expectNumberClose(Vec4.len(normalized), 1, 0.002);
            });
        });
    });
});
