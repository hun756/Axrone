import { afterEach, beforeEach, describe, expect, test } from 'vitest';
import { EPSILON, PI_2 } from '../../common';
import { Mat2, Mat2ComparisonMode, Mat2Comparer } from '../../mat2';
import { Fnv1a32 } from '@axrone/hash';

const expectMatrixClose = (actual: Mat2, expected: number[], epsilon = EPSILON) => {
    expect(actual.data.length).toBe(4);
    for (let i = 0; i < 4; i++) {
        expect(Math.abs(actual.data[i] - expected[i])).toBeLessThan(epsilon);
    }
};

describe('Mat2', () => {
    // ─── Constructor & Creation ──────────────────────────────────────────
    describe('Constructor', () => {
        test('should create identity matrix when no values provided', () => {
            const m = new Mat2();
            expect(m.data).toEqual([1, 0, 0, 1]);
        });

        test('should create matrix with provided values', () => {
            const m = new Mat2([1, 2, 3, 4]);
            expect(m.data).toEqual([1, 2, 3, 4]);
        });

        test('should truncate values longer than 4 elements', () => {
            const m = new Mat2([1, 2, 3, 4, 5, 6]);
            expect(m.data).toEqual([1, 2, 3, 4]);
        });

        test('should throw RangeError when values array has less than 4 elements', () => {
            expect(() => new Mat2([1, 2])).toThrow(RangeError);
            expect(() => new Mat2([1, 2])).toThrow('Matrix values array must have at least 4 elements');
        });

        test('should throw RangeError when values array is empty', () => {
            expect(() => new Mat2([])).toThrow(RangeError);
        });

        test('should handle Float32Array input', () => {
            const m = new Mat2(new Float32Array([1, 2, 3, 4]));
            expect(m.data).toEqual([1, 2, 3, 4]);
        });
    });

    describe('Static Constants', () => {
        test('IDENTITY should be a 2x2 identity matrix', () => {
            expect(Mat2.IDENTITY.data).toEqual([1, 0, 0, 1]);
        });

        test('ZERO should be a 2x2 zero matrix', () => {
            expect(Mat2.ZERO.data).toEqual([0, 0, 0, 0]);
        });

        test('static constants should be frozen', () => {
            expect(Object.isFrozen(Mat2.IDENTITY)).toBe(true);
            expect(Object.isFrozen(Mat2.ZERO)).toBe(true);
        });

        test('static constants should return same instance on multiple accesses', () => {
            expect(Mat2.IDENTITY).toBe(Mat2.IDENTITY);
            expect(Mat2.ZERO).toBe(Mat2.ZERO);
        });
    });

    describe('from', () => {
        test('should create new Mat2 from IMat2Like', () => {
            const source = { data: [1, 2, 3, 4] };
            const m = Mat2.from(source);
            expect(m.data).toEqual([1, 2, 3, 4]);
            expect(m).toBeInstanceOf(Mat2);
        });

        test('should create independent copy', () => {
            const source = new Mat2([1, 2, 3, 4]);
            const m = Mat2.from(source);
            expect(m).not.toBe(source);
            expect(m.data).toEqual(source.data);
        });
    });

    describe('fromArray', () => {
        test('should create from array with default offset', () => {
            const m = Mat2.fromArray([1, 2, 3, 4, 5, 6]);
            expect(m.data).toEqual([1, 2, 3, 4]);
        });

        test('should create from array with custom offset', () => {
            const m = Mat2.fromArray([0, 0, 1, 2, 3, 4], 2);
            expect(m.data).toEqual([1, 2, 3, 4]);
        });

        test('should handle Float32Array input', () => {
            const m = Mat2.fromArray(new Float32Array([5, 6, 7, 8]));
            expect(m.data).toEqual([5, 6, 7, 8]);
        });

        describe('Development mode validations', () => {
            const originalEnv = process.env.NODE_ENV;

            beforeEach(() => {
                process.env.NODE_ENV = 'development';
            });

            afterEach(() => {
                process.env.NODE_ENV = originalEnv;
            });

            test('should throw RangeError for negative offset', () => {
                expect(() => Mat2.fromArray([1, 2, 3, 4], -1)).toThrow(RangeError);
                expect(() => Mat2.fromArray([1, 2, 3, 4], -1)).toThrow('Offset cannot be negative');
            });

            test('should throw RangeError when array is too short for offset', () => {
                expect(() => Mat2.fromArray([1, 2], 0)).toThrow(RangeError);
                expect(() => Mat2.fromArray([1, 2], 0)).toThrow(
                    'Array must have at least 4 elements when using offset 0'
                );
            });
        });
    });

    describe('create / createFromElements', () => {
        test('create() with defaults produces identity', () => {
            expect(Mat2.create().data).toEqual([1, 0, 0, 1]);
        });

        test('create() with all parameters', () => {
            expect(Mat2.create(2, 3, 4, 5).data).toEqual([2, 3, 4, 5]);
        });

        test('createFromElements()', () => {
            expect(Mat2.createFromElements(10, 20, 30, 40).data).toEqual([10, 20, 30, 40]);
        });
    });

    // ─── Clone, Equals, Hash ─────────────────────────────────────────────
    describe('clone', () => {
        test('should create exact copy', () => {
            const original = new Mat2([1, 2, 3, 4]);
            const cloned = original.clone();
            expect(cloned.data).toEqual(original.data);
            expect(cloned).toBeInstanceOf(Mat2);
            expect(cloned).not.toBe(original);
        });

        test('mutation of clone does not affect original', () => {
            const original = new Mat2([1, 2, 3, 4]);
            const cloned = original.clone();
            cloned.multiply(Mat2.ZERO);
            expect(original.data).toEqual([1, 2, 3, 4]);
        });
    });

    describe('equals', () => {
        test('identical matrices are equal', () => {
            const a = new Mat2([1, 2, 3, 4]);
            const b = new Mat2([1, 2, 3, 4]);
            expect(a.equals(b)).toBe(true);
            expect(b.equals(a)).toBe(true);
        });

        test('epsilon tolerance', () => {
            const a = new Mat2([1, 2, 3, 4]);
            const b = new Mat2([1 + EPSILON / 2, 2, 3, 4]);
            expect(a.equals(b)).toBe(true);
        });

        test('outside epsilon returns false', () => {
            const a = new Mat2([1, 2, 3, 4]);
            const b = new Mat2([1 + EPSILON * 2, 2, 3, 4]);
            expect(a.equals(b)).toBe(false);
        });

        test('non-Mat2 returns false', () => {
            expect(new Mat2().equals({ data: [1, 0, 0, 1] })).toBe(false);
            expect(new Mat2().equals(null)).toBe(false);
            expect(new Mat2().equals(42)).toBe(false);
        });

        test('reflexive', () => {
            const m = new Mat2([1, 2, 3, 4]);
            expect(m.equals(m)).toBe(true);
        });
    });

    describe('getHashCode', () => {
        test('identical matrices have same hash', () => {
            const a = new Mat2([1, 2, 3, 4]);
            const b = new Mat2([1, 2, 3, 4]);
            expect(a.getHashCode()).toBe(b.getHashCode());
        });

        test('different matrices have different hashes', () => {
            const a = new Mat2([1, 2, 3, 4]);
            const b = new Mat2([4, 3, 2, 1]);
            expect(a.getHashCode()).not.toBe(b.getHashCode());
        });

        test('returns unsigned 32-bit integer', () => {
            const hash = new Mat2([1, 2, 3, 4]).getHashCode();
            expect(hash).toBeGreaterThanOrEqual(0);
            expect(hash).toBeLessThanOrEqual(0xffffffff);
            expect(Number.isInteger(hash)).toBe(true);
        });

        test('consistent across calls', () => {
            const m = new Mat2([1, 2, 3, 4]);
            expect(m.getHashCode()).toBe(m.getHashCode());
        });
    });

    describe('hashInto', () => {
        test('integrates with Fnv1a32 hasher', () => {
            const m = new Mat2([1, 2, 3, 4]);
            const hasher = new Fnv1a32();
            m.hashInto(hasher);
            const hash = hasher.digest();
            expect(typeof hash).toBe('number');
            expect(Number.isInteger(hash)).toBe(true);
        });
    });

    // ─── Core Operations ─────────────────────────────────────────────────
    describe('multiply', () => {
        test('identity * M = M', () => {
            const m = new Mat2([2, 3, 4, 5]);
            expectMatrixClose(Mat2.multiply(Mat2.IDENTITY, m), [2, 3, 4, 5]);
        });

        test('M * identity = M', () => {
            const m = new Mat2([2, 3, 4, 5]);
            expectMatrixClose(Mat2.multiply(m, Mat2.IDENTITY), [2, 3, 4, 5]);
        });

        test('known 2x2 product', () => {
            // [1,2] * [5,6] = [1*5+2*7, 1*6+2*8] = [19,22]
            // [3,4]   [7,8]   [3*5+4*7, 3*6+4*8]   [43,50]
            const a = new Mat2([1, 2, 3, 4]);
            const b = new Mat2([5, 6, 7, 8]);
            expectMatrixClose(Mat2.multiply(a, b), [19, 22, 43, 50]);
        });

        test('out parameter writes in-place', () => {
            const a = new Mat2([1, 2, 3, 4]);
            const b = new Mat2([5, 6, 7, 8]);
            const out = new Mat2([0, 0, 0, 0]);
            const result = Mat2.multiply(a, b, out);
            expect(result).toBe(out);
            expectMatrixClose(out, [19, 22, 43, 50]);
        });

        test('instance multiply', () => {
            const a = new Mat2([1, 2, 3, 4]);
            const b = new Mat2([5, 6, 7, 8]);
            const result = a.multiply(b);
            expect(result).toBe(a);
            expectMatrixClose(a, [19, 22, 43, 50]);
        });
    });

    describe('transpose', () => {
        test('swaps off-diagonal elements', () => {
            const m = new Mat2([1, 2, 3, 4]);
            expectMatrixClose(Mat2.transpose(m), [1, 3, 2, 4]);
        });

        test('double transpose = original', () => {
            const m = new Mat2([1, 2, 3, 4]);
            expectMatrixClose(Mat2.transpose(Mat2.transpose(m)), [1, 2, 3, 4]);
        });

        test('identity transpose = identity', () => {
            expectMatrixClose(Mat2.transpose(Mat2.IDENTITY), [1, 0, 0, 1]);
        });

        test('out parameter', () => {
            const m = new Mat2([1, 2, 3, 4]);
            const out = new Mat2();
            const result = Mat2.transpose(m, out);
            expect(result).toBe(out);
            expectMatrixClose(out, [1, 3, 2, 4]);
        });

        test('instance transpose', () => {
            const m = new Mat2([1, 2, 3, 4]);
            const result = m.transpose();
            expect(result).toBe(m);
        });
    });

    describe('determinant', () => {
        test('identity determinant = 1', () => {
            expect(Mat2.determinant(Mat2.IDENTITY)).toBeCloseTo(1, 10);
        });

        test('zero matrix determinant = 0', () => {
            expect(Mat2.determinant(Mat2.ZERO)).toBe(0);
        });

        test('known determinant: ad - bc', () => {
            // [1,2] => 1*4 - 2*3 = -2
            // [3,4]
            expect(Mat2.determinant(new Mat2([1, 2, 3, 4]))).toBeCloseTo(-2, 10);
        });

        test('instance determinant', () => {
            const m = new Mat2([1, 2, 3, 4]);
            expect(m.determinant()).toBeCloseTo(-2, 10);
        });
    });

    describe('invert', () => {
        test('identity inverse = identity', () => {
            expectMatrixClose(Mat2.invert(Mat2.IDENTITY), [1, 0, 0, 1]);
        });

        test('M * M^-1 = identity', () => {
            const m = new Mat2([1, 2, 3, 5]);
            const inv = Mat2.invert(m);
            const product = Mat2.multiply(m, inv);
            expectMatrixClose(product, [1, 0, 0, 1], 1e-10);
        });

        test('singular matrix throws', () => {
            const singular = new Mat2([1, 2, 2, 4]); // det = 4 - 4 = 0
            expect(() => Mat2.invert(singular)).toThrow('Matrix is not invertible');
        });

        test('out parameter', () => {
            const m = new Mat2([1, 2, 3, 5]);
            const out = new Mat2();
            const result = Mat2.invert(m, out);
            expect(result).toBe(out);
            const product = Mat2.multiply(m, out);
            expectMatrixClose(product, [1, 0, 0, 1], 1e-10);
        });

        test('instance invert', () => {
            const m = new Mat2([1, 2, 3, 5]);
            const result = m.invert();
            expect(result).toBe(m);
            // m is now the inverse of original
            // original * inverse should be identity
            const original = new Mat2([1, 2, 3, 5]);
            const inv = new Mat2([1, 2, 3, 5]).invert();
            expectMatrixClose(Mat2.multiply(original, inv), [1, 0, 0, 1], 1e-10);
        });
    });

    // ─── Transformation Matrices ─────────────────────────────────────────
    describe('scale', () => {
        test('creates diagonal scale matrix', () => {
            const s = Mat2.scale({ x: 2, y: 3 });
            expectMatrixClose(s, [2, 0, 0, 3]);
        });

        test('out parameter', () => {
            const out = new Mat2();
            const result = Mat2.scale({ x: 5, y: 7 }, out);
            expect(result).toBe(out);
            expectMatrixClose(out, [5, 0, 0, 7]);
        });
    });

    describe('scaleUniform', () => {
        test('creates uniform scale matrix', () => {
            expectMatrixClose(Mat2.scaleUniform(5), [5, 0, 0, 5]);
        });

        test('out parameter', () => {
            const out = new Mat2();
            Mat2.scaleUniform(3, out);
            expectMatrixClose(out, [3, 0, 0, 3]);
        });
    });

    describe('rotate', () => {
        test('rotate by PI/2 produces known matrix', () => {
            const r = Mat2.rotate(Math.PI / 2);
            // [cos90, -sin90] = [0, -1]
            // [sin90,  cos90]   [1,  0]
            expectMatrixClose(r, [0, -1, 1, 0], 1e-10);
        });

        test('rotate by 0 = identity', () => {
            expectMatrixClose(Mat2.rotate(0), [1, 0, 0, 1], 1e-10);
        });

        test('rotate by 2PI = identity', () => {
            expectMatrixClose(Mat2.rotate(PI_2), [1, 0, 0, 1], 1e-8);
        });

        test('rotation matrix is orthogonal (det = 1)', () => {
            const r = Mat2.rotate(1.23);
            expect(Mat2.determinant(r)).toBeCloseTo(1, 10);
        });

        test('out parameter', () => {
            const out = new Mat2();
            Mat2.rotate(Math.PI / 4, out);
            expect(Mat2.determinant(out)).toBeCloseTo(1, 10);
        });
    });

    describe('shear', () => {
        test('creates shear matrix', () => {
            expectMatrixClose(Mat2.shear(0.5, 0.3), [1, 0.5, 0.3, 1]);
        });

        test('out parameter', () => {
            const out = new Mat2();
            Mat2.shear(1, 2, out);
            expectMatrixClose(out, [1, 1, 2, 1]);
        });
    });

    describe('translate', () => {
        test('throws Error (not supported for 2x2)', () => {
            expect(() => Mat2.translate({ x: 1, y: 2 })).toThrow('Mat2.translate is not supported');
        });
    });

    // ─── Vector Transformation ───────────────────────────────────────────
    describe('transformVec2', () => {
        test('identity transform preserves vector', () => {
            const v = { x: 3, y: 4 };
            const result = Mat2.transformVec2(v, Mat2.IDENTITY);
            expect(Math.abs(result.x - 3)).toBeLessThan(EPSILON);
            expect(Math.abs(result.y - 4)).toBeLessThan(EPSILON);
        });

        test('scale transform scales vector', () => {
            const scale = Mat2.scale({ x: 2, y: 3 });
            const result = Mat2.transformVec2({ x: 5, y: 7 }, scale);
            expect(Math.abs(result.x - 10)).toBeLessThan(EPSILON);
            expect(Math.abs(result.y - 21)).toBeLessThan(EPSILON);
        });

        test('rotate transform rotates vector', () => {
            const r = Mat2.rotate(Math.PI / 2);
            const result = Mat2.transformVec2({ x: 1, y: 0 }, r);
            expect(Math.abs(result.x - 0)).toBeLessThan(1e-10);
            expect(Math.abs(result.y - 1)).toBeLessThan(1e-10);
        });

        test('out parameter', () => {
            const out = { x: 0, y: 0 };
            const result = Mat2.transformVec2({ x: 1, y: 2 }, Mat2.IDENTITY, out);
            expect(result).toBe(out);
            expect(Math.abs(out.x - 1)).toBeLessThan(EPSILON);
            expect(Math.abs(out.y - 2)).toBeLessThan(EPSILON);
        });

        test('instance transformVec2', () => {
            const scale = Mat2.scale({ x: 2, y: 3 });
            const result = scale.transformVec2({ x: 5, y: 7 });
            expect(Math.abs(result.x - 10)).toBeLessThan(EPSILON);
            expect(Math.abs(result.y - 21)).toBeLessThan(EPSILON);
        });
    });

    // ─── Interpolation ───────────────────────────────────────────────────
    describe('lerp', () => {
        const a = new Mat2([1, 2, 3, 4]);
        const b = new Mat2([5, 6, 7, 8]);

        test('t=0 returns a', () => {
            expectMatrixClose(Mat2.lerp(a, b, 0), [1, 2, 3, 4]);
        });

        test('t=1 returns b', () => {
            expectMatrixClose(Mat2.lerp(a, b, 1), [5, 6, 7, 8]);
        });

        test('t=0.5 returns midpoint', () => {
            expectMatrixClose(Mat2.lerp(a, b, 0.5), [3, 4, 5, 6]);
        });

        test('clamps t > 1 to b', () => {
            expectMatrixClose(Mat2.lerp(a, b, 2), [5, 6, 7, 8]);
        });

        test('clamps t < 0 to a', () => {
            expectMatrixClose(Mat2.lerp(a, b, -1), [1, 2, 3, 4]);
        });

        test('out parameter', () => {
            const out = new Mat2();
            const result = Mat2.lerp(a, b, 0.5, out);
            expect(result).toBe(out);
            expectMatrixClose(out, [3, 4, 5, 6]);
        });
    });

    describe('lerpUnClamped', () => {
        const a = new Mat2([1, 2, 3, 4]);
        const b = new Mat2([5, 6, 7, 8]);

        test('t=-0.5 extrapolates', () => {
            expectMatrixClose(Mat2.lerpUnClamped(a, b, -0.5), [-1, 0, 1, 2]);
        });

        test('t=1.5 extrapolates', () => {
            expectMatrixClose(Mat2.lerpUnClamped(a, b, 1.5), [7, 8, 9, 10]);
        });

        test('out parameter', () => {
            const out = new Mat2();
            Mat2.lerpUnClamped(a, b, 2, out);
            expectMatrixClose(out, [9, 10, 11, 12]);
        });
    });

    // ─── Serialization ───────────────────────────────────────────────────
    describe('toArray / toString', () => {
        test('toArray returns copy of data', () => {
            const m = new Mat2([1, 2, 3, 4]);
            const arr = m.toArray();
            expect(arr).toEqual([1, 2, 3, 4]);
            arr[0] = 999;
            expect(m.data[0]).toBe(1); // original unchanged
        });

        test('toString returns formatted string', () => {
            const m = new Mat2([1, 2, 3, 4]);
            const str = m.toString();
            expect(str).toContain('Mat2');
            expect(str).toContain('1.000');
            expect(str).toContain('2.000');
            expect(str).toContain('3.000');
            expect(str).toContain('4.000');
        });
    });

    // ─── Mat2Comparer ────────────────────────────────────────────────────
    describe('Mat2Comparer', () => {
        describe('FROBENIUS_NORM mode', () => {
            test('equal norms return 0', () => {
                // Both have same Frobenius norm: sqrt(1+4+9+16) = sqrt(30)
                const a = new Mat2([1, 2, 3, 4]);
                const b = new Mat2([4, 3, 2, 1]); // sqrt(16+9+4+1) = sqrt(30)
                const comparer = new Mat2Comparer(Mat2ComparisonMode.FROBENIUS_NORM);
                expect(comparer.compare(a, b)).toBe(0);
            });

            test('larger norm returns 1', () => {
                const a = new Mat2([10, 0, 0, 0]);
                const b = new Mat2([1, 0, 0, 1]);
                const comparer = new Mat2Comparer(Mat2ComparisonMode.FROBENIUS_NORM);
                expect(comparer.compare(a, b)).toBe(1);
            });

            test('smaller norm returns -1', () => {
                const a = new Mat2([1, 0, 0, 1]);
                const b = new Mat2([10, 0, 0, 0]);
                const comparer = new Mat2Comparer(Mat2ComparisonMode.FROBENIUS_NORM);
                expect(comparer.compare(a, b)).toBe(-1);
            });
        });

        describe('DETERMINANT mode', () => {
            test('equal determinants return 0', () => {
                const a = new Mat2([1, 0, 0, 1]); // det = 1
                const b = new Mat2([2, 1, 1, 1]); // det = 2-1 = 1
                const comparer = new Mat2Comparer(Mat2ComparisonMode.DETERMINANT);
                expect(comparer.compare(a, b)).toBe(0);
            });

            test('larger determinant returns 1', () => {
                const a = new Mat2([5, 0, 0, 5]); // det = 25
                const b = new Mat2([1, 0, 0, 1]); // det = 1
                const comparer = new Mat2Comparer(Mat2ComparisonMode.DETERMINANT);
                expect(comparer.compare(a, b)).toBe(1);
            });
        });

        describe('TRACE mode', () => {
            test('equal traces return 0', () => {
                const a = new Mat2([3, 1, 0, 2]); // trace = 5
                const b = new Mat2([2, 0, 1, 3]); // trace = 5
                const comparer = new Mat2Comparer(Mat2ComparisonMode.TRACE);
                expect(comparer.compare(a, b)).toBe(0);
            });

            test('larger trace returns 1', () => {
                const a = new Mat2([10, 0, 0, 0]); // trace = 10
                const b = new Mat2([1, 0, 0, 1]); // trace = 2
                const comparer = new Mat2Comparer(Mat2ComparisonMode.TRACE);
                expect(comparer.compare(a, b)).toBe(1);
            });
        });

        describe('CONDITION_NUMBER mode', () => {
            test('equal condition numbers return 0', () => {
                // cond = max/min of abs elements: both have max/min = 2/1 = 2
                const a = new Mat2([1, 2, 2, 1]); // cond = 2
                const b = new Mat2([3, 6, 6, 3]); // cond = 2
                const comparer = new Mat2Comparer(Mat2ComparisonMode.CONDITION_NUMBER);
                expect(comparer.compare(a, b)).toBe(0);
            });

            test('lower condition number returns -1', () => {
                const a = new Mat2([1, 2, 2, 1]); // cond = 2/1 = 2
                const b = new Mat2([1, 4, 4, 1]); // cond = 4/1 = 4
                const comparer = new Mat2Comparer(Mat2ComparisonMode.CONDITION_NUMBER);
                expect(comparer.compare(a, b)).toBe(-1);
            });
        });

        describe('default mode', () => {
            test('defaults to FROBENIUS_NORM', () => {
                const comparer = new Mat2Comparer();
                const a = new Mat2([1, 0, 0, 0]);
                const b = new Mat2([0, 0, 0, 1]);
                expect(comparer.compare(a, b)).toBe(0); // same Frobenius norm
            });
        });
    });

    // ─── Integration Tests ───────────────────────────────────────────────
    describe('Integration', () => {
        test('rotate then inverse-rotate = identity', () => {
            const angle = 0.7;
            const r = Mat2.rotate(angle);
            const rInv = Mat2.invert(r);
            const product = Mat2.multiply(r, rInv);
            expectMatrixClose(product, [1, 0, 0, 1], 1e-10);
        });

        test('scale composed with inverse-scale = identity', () => {
            const s = Mat2.scale({ x: 3, y: 7 });
            const sInv = Mat2.invert(s);
            expectMatrixClose(Mat2.multiply(s, sInv), [1, 0, 0, 1], 1e-10);
        });

        test('rotation preserves vector length', () => {
            const r = Mat2.rotate(1.23);
            const v = { x: 3, y: 4 };
            const transformed = Mat2.transformVec2(v, r);
            const originalLen = Math.sqrt(3 * 3 + 4 * 4);
            const transformedLen = Math.sqrt(transformed.x * transformed.x + transformed.y * transformed.y);
            expect(Math.abs(originalLen - transformedLen)).toBeLessThan(1e-10);
        });

        test('lerp at boundaries matches inputs', () => {
            const a = Mat2.rotate(0.5);
            const b = Mat2.rotate(1.5);
            expectMatrixClose(Mat2.lerp(a, b, 0), a.data as unknown as number[], 1e-10);
            expectMatrixClose(Mat2.lerp(a, b, 1), b.data as unknown as number[], 1e-10);
        });
    });

    // ─── Performance Smoke Test ──────────────────────────────────────────
    describe('Performance', () => {
        test('multiply 100k times completes quickly', () => {
            const a = new Mat2([1, 2, 3, 4]);
            const b = new Mat2([5, 6, 7, 8]);
            const start = performance.now();
            for (let i = 0; i < 100000; i++) {
                Mat2.multiply(a, b);
            }
            expect(performance.now() - start).toBeLessThan(5000);
        });
    });
});
