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
});
