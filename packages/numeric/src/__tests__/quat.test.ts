import { Quat, QuatComparer, QuatEqualityComparer, QuatComparisonMode, IQuatLike } from '../quat';
import { IVec3Like } from '../vec3';

const TEST_PRECISION = {
    HIGH: 12,
    STANDARD: 8,
    LOW: 4,
    LOOSE: 2,
} as const;

const NUMERICAL_LIMITS = {
    EPSILON: 1e-15,
    LARGE_NUMBER: 1e12,
    SMALL_NUMBER: 1e-12,
    MAX_SAFE_INTEGER: Number.MAX_SAFE_INTEGER,
    MIN_SAFE_INTEGER: Number.MIN_SAFE_INTEGER,
} as const;

const COMMON_ANGLES = {
    ZERO: 0,
    QUARTER_PI: Math.PI / 4,
    HALF_PI: Math.PI / 2,
    PI: Math.PI,
    THREE_QUARTER_PI: (3 * Math.PI) / 4,
    TWO_PI: 2 * Math.PI,
    NEGATIVE_PI: -Math.PI,
} as const;

class QuaternionTestUtils {
    static expectQuaternionEquals(
        actual: IQuatLike,
        expected: IQuatLike,
        precision: number = TEST_PRECISION.STANDARD,
        context?: string
    ): void {
        const contextStr = context ? ` (${context})` : '';

        try {
            expect(actual.x).toBeCloseTo(expected.x, precision);
            expect(actual.y).toBeCloseTo(expected.y, precision);
            expect(actual.z).toBeCloseTo(expected.z, precision);
            expect(actual.w).toBeCloseTo(expected.w, precision);
        } catch (error) {
            const actualMag = Math.sqrt(
                actual.x ** 2 + actual.y ** 2 + actual.z ** 2 + actual.w ** 2
            );
            const expectedMag = Math.sqrt(
                expected.x ** 2 + expected.y ** 2 + expected.z ** 2 + expected.w ** 2
            );

            throw new Error(
                `Quaternion assertion failed${contextStr}:\n` +
                    `  Expected: (${expected.x}, ${expected.y}, ${expected.z}, ${expected.w}) |${expectedMag}|\n` +
                    `  Actual:   (${actual.x}, ${actual.y}, ${actual.z}, ${actual.w}) |${actualMag}|\n` +
                    `  Diff:     (${actual.x - expected.x}, ${actual.y - expected.y}, ${actual.z - expected.z}, ${actual.w - expected.w})\n` +
                    `  Original error: ${error instanceof Error ? error.message : String(error)}`
            );
        }
    }

    static expectVector3Equals(
        actual: IVec3Like,
        expected: IVec3Like,
        precision: number = TEST_PRECISION.STANDARD,
        context?: string
    ): void {
        const contextStr = context ? ` (${context})` : '';

        try {
            expect(actual.x).toBeCloseTo(expected.x, precision);
            expect(actual.y).toBeCloseTo(expected.y, precision);
            expect(actual.z).toBeCloseTo(expected.z, precision);
        } catch (error) {
            throw new Error(
                `Vector3 assertion failed${contextStr}:\n` +
                    `  Expected: (${expected.x}, ${expected.y}, ${expected.z})\n` +
                    `  Actual:   (${actual.x}, ${actual.y}, ${actual.z})\n` +
                    `  Original error: ${error instanceof Error ? error.message : String(error)}`
            );
        }
    }

    static expectNormalized(q: IQuatLike, precision: number = TEST_PRECISION.HIGH): void {
        const magnitude = Math.sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
        expect(magnitude).toBeCloseTo(1.0, precision);
    }

    static expectValidQuaternion(q: IQuatLike): void {
        expect(Number.isFinite(q.x)).toBe(true);
        expect(Number.isFinite(q.y)).toBe(true);
        expect(Number.isFinite(q.z)).toBe(true);
        expect(Number.isFinite(q.w)).toBe(true);
        expect(Number.isNaN(q.x)).toBe(false);
        expect(Number.isNaN(q.y)).toBe(false);
        expect(Number.isNaN(q.z)).toBe(false);
        expect(Number.isNaN(q.w)).toBe(false);
    }

    static createTestQuaternions() {
        return {
            identity: { x: 0, y: 0, z: 0, w: 1 },
            zero: { x: 0, y: 0, z: 0, w: 0 },
            unitX: { x: 1, y: 0, z: 0, w: 0 },
            unitY: { x: 0, y: 1, z: 0, w: 0 },
            unitZ: { x: 0, y: 0, z: 1, w: 0 },
            normalized: { x: 0.5, y: 0.5, z: 0.5, w: 0.5 },
            arbitrary: { x: 1, y: 2, z: 3, w: 4 },
            rotationY90: { x: 0, y: Math.sin(Math.PI / 4), z: 0, w: Math.cos(Math.PI / 4) },
            rotationX90: { x: Math.sin(Math.PI / 4), y: 0, z: 0, w: Math.cos(Math.PI / 4) },
            rotationZ90: { x: 0, y: 0, z: Math.sin(Math.PI / 4), w: Math.cos(Math.PI / 4) },
            large: { x: 1e6, y: 1e6, z: 1e6, w: 1e6 },
            small: { x: 1e-6, y: 1e-6, z: 1e-6, w: 1e-6 },
            negative: { x: -1, y: -2, z: -3, w: -4 },
        };
    }

    static generateRandomQuaternion(normalize: boolean = false): IQuatLike {
        const q = {
            x: (Math.random() - 0.5) * 2,
            y: (Math.random() - 0.5) * 2,
            z: (Math.random() - 0.5) * 2,
            w: (Math.random() - 0.5) * 2,
        };

        if (normalize) {
            const length = Math.sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (length > NUMERICAL_LIMITS.EPSILON) {
                q.x /= length;
                q.y /= length;
                q.z /= length;
                q.w /= length;
            }
        }

        return q;
    }

    static benchmark<T>(name: string, operation: () => T, iterations: number = 10000): T {
        const start = performance.now();
        let result: T;

        for (let i = 0; i < iterations; i++) {
            result = operation();
        }

        const end = performance.now();
        const duration = end - start;
        const opsPerSecond = (iterations / duration) * 1000;

        console.log(
            `Benchmark [${name}]: ${duration.toFixed(2)}ms total, ${opsPerSecond.toFixed(0)} ops/sec`
        );
        return result!;
    }
}


// Main Test Suite
describe('Quaternion Mathematics Library', () => {
    let testQuats: ReturnType<typeof QuaternionTestUtils.createTestQuaternions>;

    beforeEach(() => {
        testQuats = QuaternionTestUtils.createTestQuaternions();
    });

    // Core Construction & Properties
    describe('Construction and Basic Properties', () => {
        describe('Constructor Behavior', () => {
            test('default constructor creates identity quaternion', () => {
                const q = new Quat();
                QuaternionTestUtils.expectQuaternionEquals(q, testQuats.identity, TEST_PRECISION.HIGH);
            });

            test('parameterized constructor sets values correctly', () => {
                const q = new Quat(1, 2, 3, 4);
                QuaternionTestUtils.expectQuaternionEquals(q, { x: 1, y: 2, z: 3, w: 4 });
            });

            test('constructor handles edge values', () => {
                const cases = [
                    [0, 0, 0, 0],
                    [Infinity, 0, 0, 0],
                    [-Infinity, 0, 0, 0],
                    [Number.MAX_VALUE, 0, 0, 0],
                    [Number.MIN_VALUE, 0, 0, 0]
                ];

                cases.forEach(([x, y, z, w]) => {
                    const q = new Quat(x, y, z, w);
                    expect(q.x).toBe(x);
                    expect(q.y).toBe(y);
                    expect(q.z).toBe(z);
                    expect(q.w).toBe(w);
                });
            });
        });

        describe('Static Constants', () => {
            test('constants have correct mathematical properties', () => {
                QuaternionTestUtils.expectNormalized(Quat.IDENTITY);
                expect(Quat.len(Quat.IDENTITY)).toBeCloseTo(1.0, TEST_PRECISION.HIGH);

                QuaternionTestUtils.expectNormalized(Quat.UNIT_X);
                QuaternionTestUtils.expectNormalized(Quat.UNIT_Y);
                QuaternionTestUtils.expectNormalized(Quat.UNIT_Z);
                QuaternionTestUtils.expectNormalized(Quat.UNIT_W);

                expect(Quat.len(Quat.ZERO)).toBe(0);
            });

            test('constants are immutable', () => {
                const constants = [Quat.ZERO, Quat.IDENTITY, Quat.UNIT_X, Quat.UNIT_Y, Quat.UNIT_Z, Quat.UNIT_W];
                
                constants.forEach(constant => {
                    expect(Object.isFrozen(constant)).toBe(true);
                    
                    const originalX = constant.x;
                    try {
                        (constant as any).x = 999;
                    } catch (e) {
                    }
                    expect(constant.x).toBe(originalX);
                });
            });
        });

        describe('Factory Methods', () => {
            test('from() creates independent copy', () => {
                const source = testQuats.arbitrary;
                const copy = Quat.from(source);
                
                QuaternionTestUtils.expectQuaternionEquals(copy, source);
                expect(copy).not.toBe(source);
                
                source.x = 999;
                expect(copy.x).not.toBe(999);
            });

            test('fromArray() with various configurations', () => {
                const testCases = [
                    { array: [1, 2, 3, 4], offset: 0, expected: { x: 1, y: 2, z: 3, w: 4 } },
                    { array: [0, 1, 2, 3, 4, 5], offset: 1, expected: { x: 1, y: 2, z: 3, w: 4 } },
                    { array: [1.5, 2.7, 3.9, 4.1], offset: 0, expected: { x: 1.5, y: 2.7, z: 3.9, w: 4.1 } }
                ];

                testCases.forEach(({ array, offset, expected }, index) => {
                    const result = Quat.fromArray(array, offset);
                    QuaternionTestUtils.expectQuaternionEquals(result, expected, TEST_PRECISION.HIGH, `case ${index}`);
                });
            });

            test('fromArray() error handling', () => {
                const validArray = [1, 2, 3, 4];
                
                expect(() => Quat.fromArray(validArray, -1))
                    .toThrow('Offset cannot be negative');
                
                expect(() => Quat.fromArray([1, 2], 0))
                    .toThrow('Array must have at least 4 elements');
                
                expect(() => Quat.fromArray([1, 2, 3, 4, 5], 2))
                    .toThrow('Array must have at least 6 elements when using offset 2');
            });

            test('create() factory method', () => {
                const q1 = Quat.create();
                const q2 = Quat.create(1, 2, 3, 4);
                
                QuaternionTestUtils.expectQuaternionEquals(q1, testQuats.identity);
                QuaternionTestUtils.expectQuaternionEquals(q2, { x: 1, y: 2, z: 3, w: 4 });
            });
        });

        describe('Equality and Hashing', () => {
            test('equals() with epsilon tolerance', () => {
                const q1 = new Quat(1, 2, 3, 4);
                const q2 = new Quat(1 + NUMERICAL_LIMITS.EPSILON/2, 2, 3, 4);
                const q3 = new Quat(1 + 0.1, 2, 3, 4);
                
                expect(q1.equals(q2)).toBe(true);
                expect(q1.equals(q3)).toBe(false);
            });

            test('equals() type safety', () => {
                const q = new Quat(1, 2, 3, 4);
                
                expect(q.equals(null)).toBe(false);
                expect(q.equals(undefined)).toBe(false);
                expect(q.equals("quaternion")).toBe(false);
                expect(q.equals({ x: 1, y: 2, z: 3, w: 4 })).toBe(false); // Not a Quat instance
            });

            test('getHashCode() consistency and distribution', () => {
                const q1 = new Quat(1, 2, 3, 4);
                const q2 = new Quat(1, 2, 3, 4);
                const q3 = new Quat(4, 3, 2, 1);
                
                expect(q1.getHashCode()).toBe(q1.getHashCode());
                expect(q1.getHashCode()).toBe(q2.getHashCode());
                
                expect(q1.getHashCode()).not.toBe(q3.getHashCode());
                
                expect(Number.isInteger(q1.getHashCode())).toBe(true);
                expect(q1.getHashCode()).toBeGreaterThanOrEqual(0);
            });
        });

        describe('Cloning', () => {
            test('clone() creates exact independent copy', () => {
                const original = new Quat(1.123456789, 2.987654321, 3.555555555, 4.777777777);
                const cloned = original.clone();
                
                QuaternionTestUtils.expectQuaternionEquals(cloned, original, TEST_PRECISION.HIGH);
                expect(cloned).not.toBe(original);
                
                original.x = 999;
                expect(cloned.x).toBeCloseTo(1.123456789, TEST_PRECISION.HIGH);
            });
        });
    });

});