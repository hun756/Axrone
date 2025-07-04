import { EPSILON } from '../../common';
import { Mat4 } from '../../mat4';

describe('Mat4', () => {
    describe('Constructor', () => {
        test('should create identity matrix when no values provided', () => {
            const matrix = new Mat4();
            expect(matrix.data).toEqual([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);
        });

        test('should create matrix with provided values', () => {
            const values = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
            const matrix = new Mat4(values);
            expect(matrix.data).toEqual(values);
        });

        test('should create matrix with values array longer than 16 elements', () => {
            const values = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20];
            const matrix = new Mat4(values);
            expect(matrix.data).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);
        });

        test('should throw RangeError when values array has less than 16 elements', () => {
            const values = [1, 2, 3, 4, 5];

            expect(() => new Mat4(values)).toThrow(RangeError);
            expect(() => new Mat4(values)).toThrow(
                'Matrix values array must have at least 16 elements'
            );
        });

        test('should throw RangeError when values array is empty', () => {
            const values: number[] = [];

            expect(() => new Mat4(values)).toThrow(RangeError);
        });

        test('should handle Float32Array input', () => {
            const values = new Float32Array([
                1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
            ]);

            const matrix = new Mat4(values);

            expect(matrix.data).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);
        });
    });

    describe('Static Constants', () => {
        test('IDENTITY should be a 4x4 identity matrix', () => {
            expect(Mat4.IDENTITY.data).toEqual([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);
        });

        test('ZERO should be a 4x4 zero matrix', () => {
            expect(Mat4.ZERO.data).toEqual([0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
        });

        test('static constants should be frozen (immutable)', () => {
            expect(Object.isFrozen(Mat4.IDENTITY)).toBe(true);
            expect(Object.isFrozen(Mat4.ZERO)).toBe(true);
        });

        test('static constants should return same instance on multiple accesses', () => {
            const identity1 = Mat4.IDENTITY;
            const identity2 = Mat4.IDENTITY;
            const zero1 = Mat4.ZERO;
            const zero2 = Mat4.ZERO;

            expect(identity1).toBe(identity2);
            expect(zero1).toBe(zero2);
        });
    });

    describe('Static from method', () => {
        test('should create new Mat4 from existing matrix-like object', () => {
            const sourceMatrix = { data: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16] };

            const matrix = Mat4.from(sourceMatrix);

            expect(matrix.data).toEqual(sourceMatrix.data);
            expect(matrix).toBeInstanceOf(Mat4);
        });

        test('should create independent copy from source matrix', () => {
            const sourceMatrix = new Mat4([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);

            const matrix = Mat4.from(sourceMatrix);

            expect(matrix).not.toBe(sourceMatrix);
            expect(matrix.data).toEqual(sourceMatrix.data);
        });
    });

    describe('Static fromArray method', () => {
        test('should create matrix from array with default offset', () => {
            const arr = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

            const matrix = Mat4.fromArray(arr);

            expect(matrix.data).toEqual(arr);
        });

        test('should create matrix from array with custom offset', () => {
            const arr = [0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

            const matrix = Mat4.fromArray(arr, 2);

            expect(matrix.data).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);
        });

        test('should handle Float32Array input', () => {
            const arr = new Float32Array([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);

            const matrix = Mat4.fromArray(arr);

            expect(matrix.data).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);
        });

        describe('Development mode validations', () => {
            const originalEnv = process.env.NODE_ENV;

            beforeEach(() => {
                process.env.NODE_ENV = 'development';
            });

            afterEach(() => {
                process.env.NODE_ENV = originalEnv;
            });

            test('should throw RangeError for negative offset in development', () => {
                const arr = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

                expect(() => Mat4.fromArray(arr, -1)).toThrow(RangeError);
                expect(() => Mat4.fromArray(arr, -1)).toThrow('Offset cannot be negative');
            });

            test('should throw RangeError when array is too short for offset in development', () => {
                const arr = [1, 2, 3, 4, 5];

                expect(() => Mat4.fromArray(arr, 0)).toThrow(RangeError);
                expect(() => Mat4.fromArray(arr, 0)).toThrow(
                    'Array must have at least 16 elements when using offset 0'
                );
            });

            test('should throw RangeError when offset makes array too short in development', () => {
                const arr = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17];

                expect(() => Mat4.fromArray(arr, 2)).toThrow(RangeError);
                expect(() => Mat4.fromArray(arr, 2)).toThrow(
                    'Array must have at least 18 elements when using offset 2'
                );
            });
        });
    });

    describe('Static create method', () => {
        test('should create identity matrix with default parameters', () => {
            const matrix = Mat4.create();

            expect(matrix.data).toEqual([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);
        });

        test('should create matrix with all parameters provided', () => {
            const matrix = Mat4.create(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16);

            expect(matrix.data).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);
        });

        test('should create matrix with partial parameters (rest defaulted)', () => {
            const matrix = Mat4.create(2, 0, 0, 0, 0, 3);

            expect(matrix.data).toEqual([2, 0, 0, 0, 0, 3, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);
        });
    });

    describe('Static createFromElements method', () => {
        test('should create matrix with all elements provided', () => {
            const matrix = Mat4.createFromElements(
                1,
                2,
                3,
                4,
                5,
                6,
                7,
                8,
                9,
                10,
                11,
                12,
                13,
                14,
                15,
                16
            );

            expect(matrix.data).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);
        });

        test('should handle negative values', () => {
            const matrix = Mat4.createFromElements(
                -1,
                -2,
                -3,
                -4,
                -5,
                -6,
                -7,
                -8,
                -9,
                -10,
                -11,
                -12,
                -13,
                -14,
                -15,
                -16
            );

            expect(matrix.data).toEqual([
                -1, -2, -3, -4, -5, -6, -7, -8, -9, -10, -11, -12, -13, -14, -15, -16,
            ]);
        });

        test('should handle decimal values', () => {
            const matrix = Mat4.createFromElements(
                1.1,
                2.2,
                3.3,
                4.4,
                5.5,
                6.6,
                7.7,
                8.8,
                9.9,
                10.1,
                11.11,
                12.12,
                13.13,
                14.14,
                15.15,
                16.16
            );

            expect(matrix.data).toEqual([
                1.1, 2.2, 3.3, 4.4, 5.5, 6.6, 7.7, 8.8, 9.9, 10.1, 11.11, 12.12, 13.13, 14.14,
                15.15, 16.16,
            ]);
        });
    });

    describe('clone method', () => {
        test('should create exact copy of matrix', () => {
            const original = new Mat4([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);

            const cloned = original.clone();

            expect(cloned.data).toEqual(original.data);
            expect(cloned).toBeInstanceOf(Mat4);
        });

        test('should create independent copy', () => {
            const original = new Mat4([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);

            const cloned = original.clone();

            expect(cloned).not.toBe(original);
            expect(cloned.data).not.toBe(original.data);
        });

        test('should clone identity matrix correctly', () => {
            const identity = new Mat4();

            const cloned = identity.clone();

            expect(cloned.data).toEqual([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);
        });
    });

    describe('equals method', () => {
        test('should return true for identical matrices', () => {
            const matrix1 = new Mat4([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);
            const matrix2 = new Mat4([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);

            expect(matrix1.equals(matrix2)).toBe(true);
            expect(matrix2.equals(matrix1)).toBe(true);
        });

        test('should return true for matrices within epsilon tolerance', () => {
            const matrix1 = new Mat4([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);
            const matrix2 = new Mat4([
                1 + EPSILON / 2,
                2 + EPSILON / 2,
                3 + EPSILON / 2,
                4 + EPSILON / 2,
                5 + EPSILON / 2,
                6 + EPSILON / 2,
                7 + EPSILON / 2,
                8 + EPSILON / 2,
                9 + EPSILON / 2,
                10 + EPSILON / 2,
                11 + EPSILON / 2,
                12 + EPSILON / 2,
                13 + EPSILON / 2,
                14 + EPSILON / 2,
                15 + EPSILON / 2,
                16 + EPSILON / 2,
            ]);

            expect(matrix1.equals(matrix2)).toBe(true);
        });

        test('should return false for matrices outside epsilon tolerance', () => {
            const matrix1 = new Mat4([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);
            const matrix2 = new Mat4([
                1 + EPSILON * 2,
                2,
                3,
                4,
                5,
                6,
                7,
                8,
                9,
                10,
                11,
                12,
                13,
                14,
                15,
                16,
            ]);

            expect(matrix1.equals(matrix2)).toBe(false);
        });

        test('should return false for different matrices', () => {
            const matrix1 = new Mat4([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);
            const matrix2 = new Mat4([16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1]);

            expect(matrix1.equals(matrix2)).toBe(false);
        });

        test('should return false for non-Mat4 objects', () => {
            const matrix = new Mat4();
            const notMatrix = { data: [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1] };

            expect(matrix.equals(notMatrix)).toBe(false);
        });

        test('should return false for null and undefined', () => {
            const matrix = new Mat4();

            expect(matrix.equals(null)).toBe(false);
            expect(matrix.equals(undefined)).toBe(false);
        });

        test('should return false for primitive values', () => {
            const matrix = new Mat4();

            expect(matrix.equals(42)).toBe(false);
            expect(matrix.equals('matrix')).toBe(false);
            expect(matrix.equals(true)).toBe(false);
        });

        test('should be reflexive (matrix equals itself)', () => {
            const matrix = new Mat4([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);

            expect(matrix.equals(matrix)).toBe(true);
        });

        test('should work with identity matrices', () => {
            const identity1 = new Mat4();
            const identity2 = Mat4.IDENTITY;

            expect(identity1.equals(identity2)).toBe(true);
        });
    });
});
