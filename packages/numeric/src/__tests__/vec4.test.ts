// src/vec4.spec.ts
import { EPSILON } from '../common';
import { IVec4Like, Vec4 } from '../vec4';

describe('Vec4 Class', () => {
    describe('Constructor and Properties', () => {
        it('should create a zero vector when called with no arguments', () => {
            const v = new Vec4();
            expect(v.x).toBe(0);
            expect(v.y).toBe(0);
            expect(v.z).toBe(0);
            expect(v.w).toBe(0);
        });

        it('should correctly assign values from constructor arguments', () => {
            const v = new Vec4(1, 2, 3, 4);
            expect(v.x).toBe(1);
            expect(v.y).toBe(2);
            expect(v.z).toBe(3);
            expect(v.w).toBe(4);
        });
    });

    describe('Static Readonly Properties', () => {
        it('Vec4.ZERO should be a zero vector', () => {
            expect(Vec4.ZERO).toEqual({ x: 0, y: 0, z: 0, w: 0 });
        });

        it('Vec4.ONE should be a vector of ones', () => {
            expect(Vec4.ONE).toEqual({ x: 1, y: 1, z: 1, w: 1 });
        });

        it('Vec4.UNIT_Z should be the unit vector in the Z direction', () => {
            expect(Vec4.UNIT_Z).toEqual({ x: 0, y: 0, z: 1, w: 0 });
        });

        it('static properties should be immutable (frozen)', () => {
            expect(Object.isFrozen(Vec4.ZERO)).toBe(true);
            expect(Object.isFrozen(Vec4.ONE)).toBe(true);
        });
    });

    describe('Static Factory Methods', () => {
        it('Vec4.create should create a new vector with given values', () => {
            const v = Vec4.create(5, 6, 7, 8);
            expect(v).toBeInstanceOf(Vec4);
            expect(v).toEqual({ x: 5, y: 6, z: 7, w: 8 });
        });

        it('Vec4.from should create a vector from an IVec4Like object', () => {
            const source: IVec4Like = { x: 10, y: 20, z: 30, w: 40 };
            const v = Vec4.from(source);
            expect(v).toBeInstanceOf(Vec4);
            expect(v).toEqual(source);
        });

        describe('Vec4.fromArray', () => {
            const arr = [10, 20, 30, 40, 50, 60];

            it('should create a vector from the beginning of an array', () => {
                const v = Vec4.fromArray(arr);
                expect(v).toEqual({ x: 10, y: 20, z: 30, w: 40 });
            });

            it('should create a vector from an array with a given offset', () => {
                const v = Vec4.fromArray(arr, 2);
                expect(v).toEqual({ x: 30, y: 40, z: 50, w: 60 });
            });

            it('should throw RangeError if offset is negative', () => {
                expect(() => Vec4.fromArray(arr, -1)).toThrow(RangeError);
                expect(() => Vec4.fromArray(arr, -1)).toThrow('Offset cannot be negative');
            });

            it('should throw RangeError if array is too short for the offset', () => {
                expect(() => Vec4.fromArray([1, 2, 3], 1)).toThrow(RangeError);
                expect(() => Vec4.fromArray([1, 2, 3], 0)).toThrow(
                    'Array must have at least 4 elements when using offset 0'
                );
            });
        });
    });

    describe('Instance Methods: clone, equals, getHashCode', () => {
        describe('clone()', () => {
            it('should create a new instance with the same values', () => {
                const v1 = new Vec4(1, 2, 3, 4);
                const v2 = v1.clone();
                expect(v2).toBeInstanceOf(Vec4);
                expect(v2.x).toBe(v1.x);
                expect(v2.y).toBe(v1.y);
                expect(v2.z).toBe(v1.z);
                expect(v2.w).toBe(v1.w);
            });

            it('should not be the same instance as the original', () => {
                const v1 = new Vec4(1, 2, 3, 4);
                const v2 = v1.clone();
                expect(v2).not.toBe(v1);
            });
        });

        describe('equals()', () => {
            const v = new Vec4(1, 2, 3, 4);

            it('should return true for an identical vector', () => {
                const identical = new Vec4(1, 2, 3, 4);
                expect(v.equals(identical)).toBe(true);
            });

            it('should return true for vectors with nearly identical floating point values', () => {
                const nearlyIdentical = new Vec4(1 + EPSILON / 2, 2 - EPSILON / 2, 3, 4);
                expect(v.equals(nearlyIdentical)).toBe(true);
            });

            it('should return false for different vectors', () => {
                const different = new Vec4(4, 3, 2, 1);
                expect(v.equals(different)).toBe(false);
            });

            it('should return false when a component is outside the EPSILON tolerance', () => {
                const outsideTolerance = new Vec4(1 + EPSILON * 2, 2, 3, 4);
                expect(v.equals(outsideTolerance)).toBe(false);
            });

            it('should return false for non-Vec4 objects', () => {
                expect(v.equals(null)).toBe(false);
                expect(v.equals(undefined)).toBe(false);
                expect(v.equals({ x: 1, y: 2, z: 3, w: 4 })).toBe(false);
                expect(v.equals('a vector')).toBe(false);
            });
        });

        describe('getHashCode()', () => {
            it('should return the same hash code for identical vectors', () => {
                const v1 = new Vec4(1.23, 4.56, 7.89, 0.12);
                const v2 = new Vec4(1.23, 4.56, 7.89, 0.12);
                expect(v1.getHashCode()).toBe(v2.getHashCode());
            });

            it('should return a different hash code for different vectors', () => {
                const v1 = new Vec4(1, 2, 3, 4);
                const v2 = new Vec4(4, 3, 2, 1);
                expect(v1.getHashCode()).not.toBe(v2.getHashCode());
            });

            it('should return an unsigned 32-bit integer', () => {
                const v = new Vec4(-10, 50.555, -1000.1, 99);
                const hash = v.getHashCode();
                expect(hash).toBeGreaterThanOrEqual(0);
                expect(hash).toBeLessThanOrEqual(4294967295); // 2^32 - 1
            });
        });
    });

     describe('Static Arithmetic Methods', () => {
        const v1 = new Vec4(1, 2, 3, 4);
        const v2 = new Vec4(5, 6, 7, 8);
        const out = new Vec4();

        beforeEach(() => {
            out.x = 0; out.y = 0; out.z = 0; out.w = 0;
        });

        it('add should return a new vector with the sum of two vectors', () => {
            const result = Vec4.add(v1, v2);
            expect(result).toEqual({ x: 6, y: 8, z: 10, w: 12 });
            expect(result).not.toBeInstanceOf(Vec4);
        });

        it('add should write the result to the out parameter if provided', () => {
            const result = Vec4.add(v1, v2, out);
            expect(result).toBe(out);
            expect(out).toEqual({ x: 6, y: 8, z: 10, w: 12 });
        });

        it('subtract should return the difference of two vectors', () => {
            const result = Vec4.subtract(v1, v2);
            expect(result).toEqual({ x: -4, y: -4, z: -4, w: -4 });
        });

        it('multiply should return the component-wise product of two vectors', () => {
            const result = Vec4.multiply(v1, v2);
            expect(result).toEqual({ x: 5, y: 12, z: 21, w: 32 });
        });
        
        it('divide should return the component-wise division of two vectors', () => {
            const vA = new Vec4(10, 20, 30, 40);
            const vB = new Vec4(2, 4, 5, 8);
            const result = Vec4.divide(vA, vB);
            expect(result.x).toBeCloseTo(5);
            expect(result.y).toBeCloseTo(5);
            expect(result.z).toBeCloseTo(6);
            expect(result.w).toBeCloseTo(5);
        });

        it('divide should throw an error when dividing by zero', () => {
            const zeroVec = new Vec4(1, 0, 1, 1);
            expect(() => Vec4.divide(v1, zeroVec)).toThrow('Division by zero or near-zero value is not allowed');
        });

        it('addScalar should add a number to each component', () => {
            const result = Vec4.addScalar(v1, 10, out);
            expect(result).toEqual({ x: 11, y: 12, z: 13, w: 14 });
        });

        it('subtractScalar should subtract a number from each component', () => {
            const result = Vec4.subtractScalar(v1, 1);
            expect(result).toEqual({ x: 0, y: 1, z: 2, w: 3 });
        });

        it('multiplyScalar should multiply each component by a number', () => {
            const result = Vec4.multiplyScalar(v1, 3);
            expect(result).toEqual({ x: 3, y: 6, z: 9, w: 12 });
        });

        it('divideScalar should divide each component by a number', () => {
            const result = Vec4.divideScalar(v1, 2);
            expect(result).toEqual({ x: 0.5, y: 1, z: 1.5, w: 2 });
        });

        it('divideScalar should throw an error when dividing by zero', () => {
            expect(() => Vec4.divideScalar(v1, 0)).toThrow('Division by zero or near-zero value is not allowed');
        });
    });
});
