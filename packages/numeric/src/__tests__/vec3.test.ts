import { Vec3, Vec3ComparisonMode, Vec3Comparer, Vec3EqualityComparer, IVec3Like } from '../vec3';

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
        const pass =
            Math.abs(received.x - expected.x) < precision &&
            Math.abs(received.y - expected.y) < precision &&
            Math.abs(received.z - expected.z) < precision;

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
});
