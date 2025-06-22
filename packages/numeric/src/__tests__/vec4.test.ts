// src/vec4.spec.ts
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
});
