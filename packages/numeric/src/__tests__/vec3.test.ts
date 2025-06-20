import { Vec3, Vec3ComparisonMode, Vec3Comparer, Vec3EqualityComparer, IVec3Like } from '../vec3';

declare global {
    namespace jest {
        interface Matchers<R> {
            toBeCloseToVec3(expected: Vec3, precision?: number): R;
            toBeNormalizedVec3(precision?: number): R;
            toBePerpendicularTo(other: Vec3, precision?: number): R;
        }
    }
}

const EPSILON = 1e-10;
const FLOAT_PRECISION = 1e-6;
const PERFORMANCE_ITERATIONS = 100000;

class Vec3TestDataBuilder {
    static createZero(): Vec3 {
        return new Vec3(0, 0, 0);
    }

    static createUnit(): Vec3 {
        return new Vec3(1, 1, 1);
    }

    static createRandom(scale: number = 1): Vec3 {
        return new Vec3(
            (Math.random() - 0.5) * 2 * scale,
            (Math.random() - 0.5) * 2 * scale,
            (Math.random() - 0.5) * 2 * scale
        );
    }

    static createNormalized(): Vec3 {
        const v = Vec3TestDataBuilder.createRandom(10);
        return v.normalize();
    }

    static createLarge(): Vec3 {
        return new Vec3(Number.MAX_SAFE_INTEGER, Number.MAX_SAFE_INTEGER, Number.MAX_SAFE_INTEGER);
    }

    static createSmall(): Vec3 {
        return new Vec3(Number.MIN_VALUE, Number.MIN_VALUE, Number.MIN_VALUE);
    }

    static createNearZero(): Vec3 {
        return new Vec3(EPSILON / 2, EPSILON / 2, EPSILON / 2);
    }

    static createBatch(count: number): Vec3[] {
        return Array.from({ length: count }, () => Vec3TestDataBuilder.createRandom());
    }
}

expect.extend({
    toBeCloseToVec3(received: Vec3, expected: Vec3, precision = FLOAT_PRECISION) {
        const normalizeZero = (val: number) => (val === 0 ? 0 : val);

        const rxNorm = normalizeZero(received.x);
        const ryNorm = normalizeZero(received.y);
        const rzNorm = normalizeZero(received.z);
        const exNorm = normalizeZero(expected.x);
        const eyNorm = normalizeZero(expected.y);
        const ezNorm = normalizeZero(expected.z);

        const pass =
            Math.abs(rxNorm - exNorm) < precision &&
            Math.abs(ryNorm - eyNorm) < precision &&
            Math.abs(rzNorm - ezNorm) < precision;

        return {
            message: () =>
                `expected Vec3(${received.x}, ${received.y}, ${received.z}) to be close to Vec3(${expected.x}, ${expected.y}, ${expected.z})`,
            pass,
        };
    },

    toBeNormalizedVec3(received: Vec3, precision = FLOAT_PRECISION) {
        const length = received.length();
        const pass = Math.abs(length - 1) < precision;

        return {
            message: () => `expected Vec3 to be normalized (length = 1), but length was ${length}`,
            pass,
        };
    },

    toBePerpendicularTo(received: Vec3, other: Vec3, precision = FLOAT_PRECISION) {
        const dotProduct = received.dot(other);
        const pass = Math.abs(dotProduct) < precision;

        return {
            message: () =>
                `expected vectors to be perpendicular (dot product = 0), but dot product was ${dotProduct}`,
            pass,
        };
    },
});

describe('Vec3 Test Suite', () => {
    describe('Constructor and Factory Methods', () => {
        describe('constructor', () => {
            test('should create vector with default values (0,0,0)', () => {
                const v = new Vec3();
                expect(v.x).toBe(0);
                expect(v.y).toBe(0);
                expect(v.z).toBe(0);
            });

            test('should create vector with provided values', () => {
                const v = new Vec3(1, 2, 3);
                expect(v.x).toBe(1);
                expect(v.y).toBe(2);
                expect(v.z).toBe(3);
            });

            test('should handle negative values', () => {
                const v = new Vec3(-1, -2, -3);
                expect(v.x).toBe(-1);
                expect(v.y).toBe(-2);
                expect(v.z).toBe(-3);
            });

            test('should handle fractional values', () => {
                const v = new Vec3(1.5, 2.7, 3.14159);
                expect(v.x).toBe(1.5);
                expect(v.y).toBe(2.7);
                expect(v.z).toBe(3.14159);
            });

            test('should handle extreme values', () => {
                const v = new Vec3(Number.MAX_VALUE, Number.MIN_VALUE, Number.POSITIVE_INFINITY);
                expect(v.x).toBe(Number.MAX_VALUE);
                expect(v.y).toBe(Number.MIN_VALUE);
                expect(v.z).toBe(Number.POSITIVE_INFINITY);
            });
        });

        describe('static constants', () => {
            test('ZERO should be (0,0,0)', () => {
                expect(Vec3.ZERO.x).toBe(0);
                expect(Vec3.ZERO.y).toBe(0);
                expect(Vec3.ZERO.z).toBe(0);
            });

            test('ONE should be (1,1,1)', () => {
                expect(Vec3.ONE.x).toBe(1);
                expect(Vec3.ONE.y).toBe(1);
                expect(Vec3.ONE.z).toBe(1);
            });

            test('UNIT_X should be (1,0,0)', () => {
                expect(Vec3.UNIT_X.x).toBe(1);
                expect(Vec3.UNIT_X.y).toBe(0);
                expect(Vec3.UNIT_X.z).toBe(0);
            });

            test('UNIT_Y should be (0,1,0)', () => {
                expect(Vec3.UNIT_Y.x).toBe(0);
                expect(Vec3.UNIT_Y.y).toBe(1);
                expect(Vec3.UNIT_Y.z).toBe(0);
            });

            test('UNIT_Z should be (0,0,1)', () => {
                expect(Vec3.UNIT_Z.x).toBe(0);
                expect(Vec3.UNIT_Z.y).toBe(0);
                expect(Vec3.UNIT_Z.z).toBe(1);
            });

            test('UP should be (0,1,0)', () => {
                expect(Vec3.UP.x).toBe(0);
                expect(Vec3.UP.y).toBe(1);
                expect(Vec3.UP.z).toBe(0);
            });

            test('FORWARD should be (0,0,1)', () => {
                expect(Vec3.FORWARD.x).toBe(0);
                expect(Vec3.FORWARD.y).toBe(0);
                expect(Vec3.FORWARD.z).toBe(1);
            });

            test('constants should be readonly', () => {
                expect(() => {
                    (Vec3.ZERO as any).x = 1;
                }).toThrow();
            });
        });

        describe('from', () => {
            test('should create Vec3 from IVec3Like object', () => {
                const source = { x: 1, y: 2, z: 3 };
                const result = Vec3.from(source);
                expect(result).toEqual(new Vec3(1, 2, 3));
                expect(result).not.toBe(source);
            });

            test('should work with Vec3 instance', () => {
                const source = new Vec3(1, 2, 3);
                const result = Vec3.from(source);
                expect(result).toEqual(source);
                expect(result).not.toBe(source);
            });
        });

        describe('fromArray', () => {
            test('should create Vec3 from array with default offset', () => {
                const arr = [1, 2, 3, 4, 5];
                const result = Vec3.fromArray(arr);
                expect(result).toEqual(new Vec3(1, 2, 3));
            });

            test('should create Vec3 from array with custom offset', () => {
                const arr = [0, 1, 2, 3, 4, 5];
                const result = Vec3.fromArray(arr, 2);
                expect(result).toEqual(new Vec3(2, 3, 4));
            });

            test('should throw error for negative offset', () => {
                const arr = [1, 2, 3];
                expect(() => Vec3.fromArray(arr, -1)).toThrow('Offset cannot be negative');
            });

            test('should throw error for insufficient array length', () => {
                const arr = [1, 2];
                expect(() => Vec3.fromArray(arr)).toThrow('Array must have at least 3 elements');
            });

            test('should throw error for insufficient array length with offset', () => {
                const arr = [1, 2, 3, 4];
                expect(() => Vec3.fromArray(arr, 3)).toThrow(
                    'Array must have at least 6 elements when using offset 3'
                );
            });

            test('should handle typed arrays', () => {
                const arr = new Float32Array([1.5, 2.5, 3.5]);
                const result = Vec3.fromArray(arr);
                expect(result).toEqual(new Vec3(1.5, 2.5, 3.5));
            });
        });

        describe('create', () => {
            test('should create with default values', () => {
                const result = Vec3.create();
                expect(result).toEqual(new Vec3(0, 0, 0));
            });

            test('should create with provided values', () => {
                const result = Vec3.create(1, 2, 3);
                expect(result).toEqual(new Vec3(1, 2, 3));
            });
        });
    });

    // BASIC OBJECT METHODS
    describe('Basic Object Methods', () => {
        describe('clone', () => {
            test('should create identical copy', () => {
                const original = new Vec3(1, 2, 3);
                const clone = original.clone();
                expect(clone).toEqual(original);
                expect(clone).not.toBe(original);
            });

            test('should maintain independence after cloning', () => {
                const original = new Vec3(1, 2, 3);
                const clone = original.clone();
                clone.x = 999;
                expect(original.x).toBe(1);
            });
        });

        describe('equals', () => {
            test('should return true for identical vectors', () => {
                const v1 = new Vec3(1, 2, 3);
                const v2 = new Vec3(1, 2, 3);
                expect(v1.equals(v2)).toBe(true);
            });

            test('should return false for different vectors', () => {
                const v1 = new Vec3(1, 2, 3);
                const v2 = new Vec3(1, 2, 4);
                expect(v1.equals(v2)).toBe(false);
            });

            test('should handle epsilon tolerance', () => {
                const v1 = new Vec3(1, 2, 3);
                const v2 = new Vec3(1 + EPSILON / 2, 2 + EPSILON / 2, 3 + EPSILON / 2);
                expect(v1.equals(v2)).toBe(true);
            });

            test('should return false for non-Vec3 objects', () => {
                const v1 = new Vec3(1, 2, 3);
                expect(v1.equals({ x: 1, y: 2, z: 3 })).toBe(false);
                expect(v1.equals(null)).toBe(false);
                expect(v1.equals(undefined)).toBe(false);
            });
        });

        describe('getHashCode', () => {
            test('should return same hash for equal vectors', () => {
                const v1 = new Vec3(1, 2, 3);
                const v2 = new Vec3(1, 2, 3);
                expect(v1.getHashCode()).toBe(v2.getHashCode());
            });

            test('should return different hash for different vectors', () => {
                const v1 = new Vec3(1, 2, 3);
                const v2 = new Vec3(1, 2, 4);
                expect(v1.getHashCode()).not.toBe(v2.getHashCode());
            });

            test('should return number type', () => {
                const v = new Vec3(1, 2, 3);
                expect(typeof v.getHashCode()).toBe('number');
            });
        });
    });

    // ARITHMETIC OPERATIONS
    describe('Arithmetic Operations', () => {
        describe('static add', () => {
            test('should add two vectors correctly', () => {
                const a = new Vec3(1, 2, 3);
                const b = new Vec3(4, 5, 6);
                const result = Vec3.add(a, b);
                expect(result).toEqual(new Vec3(5, 7, 9));
            });

            test('should not mutate input vectors', () => {
                const a = new Vec3(1, 2, 3);
                const b = new Vec3(4, 5, 6);
                Vec3.add(a, b);
                expect(a).toEqual(new Vec3(1, 2, 3));
                expect(b).toEqual(new Vec3(4, 5, 6));
            });

            test('should use output parameter when provided', () => {
                const a = new Vec3(1, 2, 3);
                const b = new Vec3(4, 5, 6);
                const out = new Vec3();
                const result = Vec3.add(a, b, out);
                expect(result).toBe(out);
                expect(out).toEqual(new Vec3(5, 7, 9));
            });

            test('should handle negative values', () => {
                const a = new Vec3(-1, -2, -3);
                const b = new Vec3(1, 2, 3);
                const result = Vec3.add(a, b);
                expect(result).toEqual(new Vec3(0, 0, 0));
            });
        });

        describe('static addScalar', () => {
            test('should add scalar to all components', () => {
                const a = new Vec3(1, 2, 3);
                const result = Vec3.addScalar(a, 5);
                expect(result).toEqual(new Vec3(6, 7, 8));
            });

            test('should handle negative scalar', () => {
                const a = new Vec3(1, 2, 3);
                const result = Vec3.addScalar(a, -1);
                expect(result).toEqual(new Vec3(0, 1, 2));
            });
        });

        describe('static subtract', () => {
            test('should subtract vectors correctly', () => {
                const a = new Vec3(5, 7, 9);
                const b = new Vec3(1, 2, 3);
                const result = Vec3.subtract(a, b);
                expect(result).toEqual(new Vec3(4, 5, 6));
            });

            test('should handle zero result', () => {
                const a = new Vec3(1, 2, 3);
                const b = new Vec3(1, 2, 3);
                const result = Vec3.subtract(a, b);
                expect(result).toEqual(new Vec3(0, 0, 0));
            });
        });

        describe('static multiply', () => {
            test('should multiply vectors component-wise', () => {
                const a = new Vec3(2, 3, 4);
                const b = new Vec3(3, 4, 5);
                const result = Vec3.multiply(a, b);
                expect(result).toEqual(new Vec3(6, 12, 20));
            });

            test('should handle zero multiplication', () => {
                const a = new Vec3(1, 2, 3);
                const b = new Vec3(0, 0, 0);
                const result = Vec3.multiply(a, b);
                expect(result).toEqual(new Vec3(0, 0, 0));
            });
        });

        describe('static multiplyScalar', () => {
            test('should multiply by scalar correctly', () => {
                const a = new Vec3(1, 2, 3);
                const result = Vec3.multiplyScalar(a, 3);
                expect(result).toEqual(new Vec3(3, 6, 9));
            });

            test('should handle zero scalar', () => {
                const a = new Vec3(1, 2, 3);
                const result = Vec3.multiplyScalar(a, 0);
                expect(result).toEqual(new Vec3(0, 0, 0));
            });

            test('should handle negative scalar', () => {
                const a = new Vec3(1, 2, 3);
                const result = Vec3.multiplyScalar(a, -2);
                expect(result).toEqual(new Vec3(-2, -4, -6));
            });
        });

        describe('static divide', () => {
            test('should divide vectors component-wise', () => {
                const a = new Vec3(6, 8, 10);
                const b = new Vec3(2, 4, 5);
                const result = Vec3.divide(a, b);
                expect(result).toEqual(new Vec3(3, 2, 2));
            });

            test('should throw error for division by zero', () => {
                const a = new Vec3(1, 2, 3);
                const b = new Vec3(0, 1, 1);
                expect(() => Vec3.divide(a, b)).toThrow(
                    'Division by zero or near-zero value is not allowed'
                );
            });

            test('should throw error for division by near-zero', () => {
                const a = new Vec3(1, 2, 3);
                const b = new Vec3(EPSILON / 2, 1, 1);
                expect(() => Vec3.divide(a, b)).toThrow(
                    'Division by zero or near-zero value is not allowed'
                );
            });
        });

        describe('static divideScalar', () => {
            test('should divide by scalar correctly', () => {
                const a = new Vec3(6, 9, 12);
                const result = Vec3.divideScalar(a, 3);
                expect(result).toEqual(new Vec3(2, 3, 4));
            });

            test('should throw error for division by zero', () => {
                const a = new Vec3(1, 2, 3);
                expect(() => Vec3.divideScalar(a, 0)).toThrow(
                    'Division by zero or near-zero value is not allowed'
                );
            });
        });

        describe('static negate', () => {
            test('should negate all components', () => {
                const a = new Vec3(1, -2, 3);
                const result = Vec3.negate(a);
                expect(result).toEqual(new Vec3(-1, 2, -3));
            });

            test('should handle zero vector', () => {
                const a = new Vec3(0, 0, 0);
                const result = Vec3.negate(a);
                expect(result.x).toBe(0);
                expect(result.y).toBe(0);
                expect(result.z).toBe(0);
            });
        });

        describe('instance add', () => {
            test('should modify vector in place', () => {
                const a = new Vec3(1, 2, 3);
                const b = new Vec3(4, 5, 6);
                const result = a.add(b);
                expect(result).toBe(a);
                expect(a).toEqual(new Vec3(5, 7, 9));
            });
        });
    });

    // VECTOR OPERATIONS
    describe('Vector Operations', () => {
        describe('static dot', () => {
            test('should calculate dot product correctly', () => {
                const a = new Vec3(1, 2, 3);
                const b = new Vec3(4, 5, 6);
                const result = Vec3.dot(a, b);
                expect(result).toBe(32);
            });

            test('should return zero for perpendicular vectors', () => {
                const a = new Vec3(1, 0, 0);
                const b = new Vec3(0, 1, 0);
                const result = Vec3.dot(a, b);
                expect(result).toBe(0);
            });

            test('should return negative for obtuse angle', () => {
                const a = new Vec3(1, 0, 0);
                const b = new Vec3(-1, 0, 0);
                const result = Vec3.dot(a, b);
                expect(result).toBe(-1);
            });
        });

        describe('static cross', () => {
            test('should calculate cross product correctly', () => {
                const a = new Vec3(1, 0, 0);
                const b = new Vec3(0, 1, 0);
                const result = Vec3.cross(a, b);
                expect(result).toEqual(new Vec3(0, 0, 1));
            });

            test('should be anti-commutative', () => {
                const a = new Vec3(1, 2, 3);
                const b = new Vec3(4, 5, 6);
                const ab = Vec3.cross(a, b);
                const ba = Vec3.cross(b, a);
                expect(ab).toEqual(Vec3.negate(ba));
            });

            test('should return zero for parallel vectors', () => {
                const a = new Vec3(1, 2, 3);
                const b = new Vec3(2, 4, 6);
                const result = Vec3.cross(a, b, new Vec3());
                expect(result.length()).toBeCloseTo(0, 6);
            });

            test('should use output parameter when provided', () => {
                const a = new Vec3(1, 0, 0);
                const b = new Vec3(0, 1, 0);
                const out = new Vec3();
                const result = Vec3.cross(a, b, out);
                expect(result).toBe(out);
                expect(out).toEqual(new Vec3(0, 0, 1));
            });
        });

        describe('static len', () => {
            test('should calculate length correctly', () => {
                const v = new Vec3(3, 4, 0);
                const result = Vec3.len(v);
                expect(result).toBe(5);
            });

            test('should return zero for zero vector', () => {
                const v = new Vec3(0, 0, 0);
                const result = Vec3.len(v);
                expect(result).toBe(0);
            });

            test('should handle 3D Pythagorean triple', () => {
                const v = new Vec3(2, 3, 6);
                const result = Vec3.len(v);
                expect(result).toBe(7);
            });
        });

        describe('static lengthSquared', () => {
            test('should calculate squared length correctly', () => {
                const v = new Vec3(3, 4, 0);
                const result = Vec3.lengthSquared(v);
                expect(result).toBe(25);
            });

            test('should be more efficient than length calculation', () => {
                const v = new Vec3(1, 2, 3);
                const lengthSq = Vec3.lengthSquared(v);
                const length = Vec3.len(v);
                expect(lengthSq).toBe(length * length);
            });
        });

        describe('static fastLength', () => {
            test('should approximate length', () => {
                const testCases = [
                    new Vec3(3, 4, 0),
                    new Vec3(1, 1, 1),
                    new Vec3(5, 12, 0),
                    new Vec3(1, 2, 3),
                ];

                testCases.forEach((v) => {
                    const exactLength = Vec3.len(v);
                    const fastLength = Vec3.fastLength(v);
                    const error = Math.abs(exactLength - fastLength) / exactLength;
                    expect(error).toBeLessThan(0.15);
                });
            });
        });

        describe('static normalize', () => {
            test('should create unit vector', () => {
                const v = new Vec3(3, 4, 0);
                const result = Vec3.normalize(v, new Vec3());
                expect(result.length()).toBeCloseTo(1, 6);
            });

            test('should preserve direction', () => {
                const v = new Vec3(3, 4, 5);
                const normalized = Vec3.normalize(v);
                const original = Vec3.multiplyScalar(normalized, Vec3.len(v));
                expect(original).toBeCloseToVec3(v);
            });

            test('should throw error for zero vector', () => {
                const v = new Vec3(0, 0, 0);
                expect(() => Vec3.normalize(v)).toThrow('Cannot normalize a zero-length vector');
            });

            test('should throw error for near-zero vector', () => {
                const v = new Vec3(EPSILON / 2, EPSILON / 2, EPSILON / 2);
                expect(() => Vec3.normalize(v)).toThrow('Cannot normalize a zero-length vector');
            });
        });

        describe('static normalizeFast', () => {
            test('should approximate normalization', () => {
                const testCases = [new Vec3(3, 4, 0), new Vec3(1, 2, 3), new Vec3(5, 12, 13)];

                testCases.forEach((v) => {
                    const exactNorm = Vec3.normalize(v);
                    const fastNorm = Vec3.normalizeFast(v, new Vec3());
                    const lengthDiff = Math.abs(fastNorm.length() - 1);
                    expect(lengthDiff).toBeLessThan(0.1);
                });
            });
        });
    });
});
