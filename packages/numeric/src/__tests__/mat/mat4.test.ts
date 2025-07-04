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
});
