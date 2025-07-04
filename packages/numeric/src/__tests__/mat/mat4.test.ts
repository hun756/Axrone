import { EPSILON } from '../../common';
import { Mat4 } from '../../mat4';

describe('Mat4', () => {
    describe('Constructor', () => {
        it('should create identity matrix by default', () => {
            const m = new Mat4();
            expect(m.data).toEqual([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);
        });

        it('should create matrix from valid array', () => {
            const arr = Array.from({ length: 16 }, (_, i) => i + 1);
            const m = new Mat4(arr);
            expect(m.data).toEqual(arr);
        });

        it('should throw if array length < 16', () => {
            expect(() => new Mat4([1, 2, 3])).toThrow(
                'Matrix values array must have at least 16 elements'
            );
        });
    });

    describe('Static Constants', () => {
        it('IDENTITY should be correct and readonly', () => {
            expect(Mat4.IDENTITY.data).toEqual([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);
            expect(Object.isFrozen(Mat4.IDENTITY)).toBe(true);
        });

        it('ZERO should be correct and readonly', () => {
            expect(Mat4.ZERO.data).toEqual(Array(16).fill(0));
            expect(Object.isFrozen(Mat4.ZERO)).toBe(true);
        });
    });

    describe('Static Methods', () => {
        it('from should create a new Mat4 from IMat4Like', () => {
            const arr = Array.from({ length: 16 }, (_, i) => i);
            const mLike = { data: arr };
            const m = Mat4.from(mLike);
            expect(m.data).toEqual(arr);
            expect(m).not.toBe(mLike);
        });

        it('fromArray should create from array with default offset', () => {
            const arr = Array.from({ length: 20 }, (_, i) => i);
            const m = Mat4.fromArray(arr);
            expect(m.data).toEqual(arr.slice(0, 16));
        });

        it('fromArray should create from array with custom offset', () => {
            const arr = Array.from({ length: 20 }, (_, i) => i);
            const m = Mat4.fromArray(arr, 2);
            expect(m.data).toEqual(arr.slice(2, 18));
        });

        it('fromArray should throw for negative offset', () => {
            const arr = Array.from({ length: 16 }, (_, i) => i);
            expect(() => Mat4.fromArray(arr, -1)).toThrow('Offset cannot be negative');
        });

        it('fromArray should throw for insufficient array length', () => {
            const arr = Array.from({ length: 10 }, (_, i) => i);
            expect(() => Mat4.fromArray(arr)).toThrow('Array must have at least 16 elements');
        });

        it('fromArray should throw for insufficient array length with offset', () => {
            const arr = Array.from({ length: 16 }, (_, i) => i);
            expect(() => Mat4.fromArray(arr, 10)).toThrow(
                'Array must have at least 26 elements when using offset 10'
            );
        });

        it('fromArray should work with typed arrays', () => {
            const arr = new Float32Array(16);
            arr.set(Array.from({ length: 16 }, (_, i) => i * 2));
            const m = Mat4.fromArray(arr);
            expect(m.data).toEqual(Array.from(arr));
        });

        it('create should return identity by default', () => {
            const m = Mat4.create();
            expect(m.data).toEqual([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);
        });

        it('create should accept custom values', () => {
            const m = Mat4.create(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16);
            expect(m.data).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);
        });

        it('createFromElements should accept custom values', () => {
            const m = Mat4.createFromElements(
                16,
                15,
                14,
                13,
                12,
                11,
                10,
                9,
                8,
                7,
                6,
                5,
                4,
                3,
                2,
                1
            );
            expect(m.data).toEqual([16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1]);
        });
    });

    describe('Instance Methods', () => {
        it('clone should return a deep copy', () => {
            const m1 = new Mat4(Array.from({ length: 16 }, (_, i) => i));
            const m2 = m1.clone();
            expect(m2).not.toBe(m1);
            expect(m2.data).toEqual(m1.data);
            // Mutate clone and check original is unchanged
            m2.data[0] = 999;
            expect(m1.data[0]).not.toBe(999);
        });

        it('equals should return true for identical matrices', () => {
            const arr = Array.from({ length: 16 }, (_, i) => i * 0.1);
            const m1 = new Mat4(arr);
            const m2 = new Mat4(arr);
            expect(m1.equals(m2)).toBe(true);
        });

        it('equals should return false for different matrices', () => {
            const arr1 = Array.from({ length: 16 }, (_, i) => i);
            const arr2 = Array.from({ length: 16 }, (_, i) => i + 1);
            const m1 = new Mat4(arr1);
            const m2 = new Mat4(arr2);
            expect(m1.equals(m2)).toBe(false);
        });

        it('equals should handle epsilon tolerance', () => {
            const arr1 = Array.from({ length: 16 }, (_, i) => i);
            const arr2 = arr1.map((v, i) => v + (i === 5 ? EPSILON / 2 : 0));
            const m1 = new Mat4(arr1);
            const m2 = new Mat4(arr2);
            expect(m1.equals(m2)).toBe(true);
        });

        it('equals should return false for non-Mat4', () => {
            const m = new Mat4();
            expect(m.equals(null)).toBe(false);
            expect(m.equals(undefined)).toBe(false);
            expect(m.equals({ data: m.data })).toBe(false);
        });

        it('getHashCode should return same hash for equal matrices', () => {
            const arr = Array.from({ length: 16 }, (_, i) => i * 2);
            const m1 = new Mat4(arr);
            const m2 = new Mat4(arr);
            expect(m1.getHashCode()).toBe(m2.getHashCode());
        });

        it('getHashCode should return different hash for different matrices', () => {
            const arr1 = Array.from({ length: 16 }, (_, i) => i);
            const arr2 = Array.from({ length: 16 }, (_, i) => i + 1);
            const m1 = new Mat4(arr1);
            const m2 = new Mat4(arr2);
            expect(m1.getHashCode()).not.toBe(m2.getHashCode());
        });

        it('getHashCode should return a number', () => {
            const m = new Mat4();
            expect(typeof m.getHashCode()).toBe('number');
        });
    });

    describe('multiply', () => {
        it('should multiply two identity matrices and return identity', () => {
            const a = Mat4.IDENTITY;
            const b = Mat4.IDENTITY;
            const result = Mat4.multiply(a, b) as Mat4;
            expect(result.data).toEqual(Mat4.IDENTITY.data);
        });

        it('should multiply identity with arbitrary matrix and return the matrix', () => {
            const arr = Array.from({ length: 16 }, (_, i) => i + 1);
            const m = new Mat4(arr);
            const result1 = Mat4.multiply(Mat4.IDENTITY, m) as Mat4;
            const result2 = Mat4.multiply(m, Mat4.IDENTITY) as Mat4;
            expect(result1.data).toEqual(m.data);
            expect(result2.data).toEqual(m.data);
        });

        it('should multiply two arbitrary matrices correctly', () => {
            const a = new Mat4([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);
            const b = new Mat4([17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32]);
            const expected = [
                250, 260, 270, 280, 618, 644, 670, 696, 986, 1028, 1070, 1112, 1354, 1412, 1470,
                1528,
            ];
            const result = Mat4.multiply(a, b) as Mat4;
            expect(result.data).toEqual(expected);
        });

        it('should write result into provided out parameter', () => {
            const a = Mat4.IDENTITY;
            const b = new Mat4([2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17]);
            const out = new Mat4();
            const returned = Mat4.multiply(a, b, out);
            expect(out.data).toEqual(b.data);
            expect(returned).toBe(out);
        });

        it('should support IMat4Like objects as input and output', () => {
            const a = { data: [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1] };
            const b = { data: [2, 0, 0, 0, 0, 3, 0, 0, 0, 0, 4, 0, 0, 0, 0, 1] };
            const out = { data: Array(16).fill(0) };
            const result = Mat4.multiply(a, b, out);
            expect(result.data).toEqual([2, 0, 0, 0, 0, 3, 0, 0, 0, 0, 4, 0, 0, 0, 0, 1]);
            expect(result).toBe(out);
        });

        it('should not mutate input matrices', () => {
            const aArr = Array.from({ length: 16 }, (_, i) => i + 1);
            const bArr = Array.from({ length: 16 }, (_, i) => 16 - i);
            const a = new Mat4(aArr);
            const b = new Mat4(bArr);
            const aBefore = [...a.data];
            const bBefore = [...b.data];
            Mat4.multiply(a, b);
            expect(a.data).toEqual(aBefore);
            expect(b.data).toEqual(bBefore);
        });

        it('should return a plain object with data if out is not provided', () => {
            const a = Mat4.IDENTITY;
            const b = Mat4.IDENTITY;
            const result = Mat4.multiply(a, b);
            expect(result).toHaveProperty('data');
            expect(Array.isArray(result.data) || ArrayBuffer.isView(result.data)).toBe(true);
        });

        it('should handle multiply with zero matrix', () => {
            const a = Mat4.ZERO;
            const b = new Mat4([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);
            const result = Mat4.multiply(a, b) as Mat4;
            expect(result.data).toEqual(Array(16).fill(0));
        });

        it('should handle multiply where out is the same as one of the inputs', () => {
            const a = new Mat4([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);
            const b = Mat4.IDENTITY;
            // out === a
            const result = Mat4.multiply(a, b, a);
            expect(result.data).toEqual(a.data);
            expect(result).toBe(a);
        });
    });
});
