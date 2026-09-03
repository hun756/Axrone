import { afterEach, beforeEach, describe, expect, test } from 'vitest';
import { EPSILON, HALF_PI, PI_2 } from '../../common';
import { Mat3, Mat3ComparisonMode, Mat3Comparer } from '../../mat3';
import { Fnv1a32 } from '@axrone/hash';

const expectMatrixClose = (actual: Mat3, expected: number[], epsilon = EPSILON) => {
    expect(actual.data.length).toBe(9);
    for (let i = 0; i < 9; i++) {
        expect(Math.abs(actual.data[i] - expected[i])).toBeLessThan(epsilon);
    }
};

const IDENTITY_3 = [1, 0, 0, 0, 1, 0, 0, 0, 1];

describe('Mat3', () => {
    // ─── Constructor & Creation ──────────────────────────────────────────
    describe('Constructor', () => {
        test('should create identity matrix when no values provided', () => {
            expect(new Mat3().data).toEqual(IDENTITY_3);
        });

        test('should create matrix with 9 values', () => {
            const m = new Mat3([1, 2, 3, 4, 5, 6, 7, 8, 9]);
            expect(m.data).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9]);
        });

        test('should throw RangeError for < 9 elements', () => {
            expect(() => new Mat3([1, 2, 3])).toThrow(RangeError);
            expect(() => new Mat3([1, 2, 3])).toThrow('Matrix values array must have at least 9 elements');
        });

        test('should throw RangeError for empty array', () => {
            expect(() => new Mat3([])).toThrow(RangeError);
        });

        test('should handle Float32Array input', () => {
            const m = new Mat3(new Float32Array([1, 2, 3, 4, 5, 6, 7, 8, 9]));
            expect(m.data).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9]);
        });

        test('should truncate values longer than 9 elements', () => {
            const m = new Mat3([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]);
            expect(m.data).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9]);
        });
    });

    describe('Static Constants', () => {
        test('IDENTITY is 3x3 identity', () => {
            expect(Mat3.IDENTITY.data).toEqual(IDENTITY_3);
        });

        test('ZERO is 3x3 zero', () => {
            expect(Mat3.ZERO.data).toEqual([0, 0, 0, 0, 0, 0, 0, 0, 0]);
        });

        test('constants are frozen', () => {
            expect(Object.isFrozen(Mat3.IDENTITY)).toBe(true);
            expect(Object.isFrozen(Mat3.ZERO)).toBe(true);
        });

        test('constants return same instance', () => {
            expect(Mat3.IDENTITY).toBe(Mat3.IDENTITY);
            expect(Mat3.ZERO).toBe(Mat3.ZERO);
        });
    });

    describe('from / fromArray / create / createFromElements', () => {
        test('from creates independent copy', () => {
            const src = new Mat3([1, 2, 3, 4, 5, 6, 7, 8, 9]);
            const m = Mat3.from(src);
            expect(m).not.toBe(src);
            expect(m.data).toEqual(src.data);
        });

        test('fromArray with default offset', () => {
            expect(Mat3.fromArray([1, 2, 3, 4, 5, 6, 7, 8, 9]).data).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9]);
        });

        test('fromArray with custom offset', () => {
            const m = Mat3.fromArray([0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9], 2);
            expect(m.data).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9]);
        });

        describe('Development mode validations', () => {
            const originalEnv = process.env.NODE_ENV;
            beforeEach(() => { process.env.NODE_ENV = 'development'; });
            afterEach(() => { process.env.NODE_ENV = originalEnv; });

            test('negative offset throws', () => {
                expect(() => Mat3.fromArray([1, 2, 3, 4, 5, 6, 7, 8, 9], -1)).toThrow('Offset cannot be negative');
            });

            test('insufficient array length throws', () => {
                expect(() => Mat3.fromArray([1, 2, 3], 0)).toThrow('Array must have at least 9 elements');
            });
        });

        test('create() with defaults produces identity', () => {
            expect(Mat3.create().data).toEqual(IDENTITY_3);
        });

        test('create() with all parameters', () => {
            expect(Mat3.create(1, 2, 3, 4, 5, 6, 7, 8, 9).data).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9]);
        });

        test('createFromElements', () => {
            expect(Mat3.createFromElements(9, 8, 7, 6, 5, 4, 3, 2, 1).data).toEqual([9, 8, 7, 6, 5, 4, 3, 2, 1]);
        });
    });

    // ─── Clone, Equals, Hash ─────────────────────────────────────────────
    describe('clone / equals / hash', () => {
        test('clone creates independent copy', () => {
            const m = new Mat3([1, 2, 3, 4, 5, 6, 7, 8, 9]);
            const c = m.clone();
            expect(c).not.toBe(m);
            expect(c.data).toEqual(m.data);
        });

        test('equals within epsilon', () => {
            const a = new Mat3([1, 2, 3, 4, 5, 6, 7, 8, 9]);
            const b = new Mat3([1 + EPSILON / 2, 2, 3, 4, 5, 6, 7, 8, 9]);
            expect(a.equals(b)).toBe(true);
        });

        test('equals outside epsilon', () => {
            const a = new Mat3([1, 2, 3, 4, 5, 6, 7, 8, 9]);
            const b = new Mat3([1 + EPSILON * 2, 2, 3, 4, 5, 6, 7, 8, 9]);
            expect(a.equals(b)).toBe(false);
        });

        test('non-Mat3 returns false', () => {
            expect(new Mat3().equals({ data: IDENTITY_3 })).toBe(false);
            expect(new Mat3().equals(null)).toBe(false);
            expect(new Mat3().equals(42)).toBe(false);
        });

        test('getHashCode deterministic', () => {
            const a = new Mat3([1, 2, 3, 4, 5, 6, 7, 8, 9]);
            const b = new Mat3([1, 2, 3, 4, 5, 6, 7, 8, 9]);
            expect(a.getHashCode()).toBe(b.getHashCode());
        });

        test('hashInto integrates with Fnv1a32', () => {
            const m = new Mat3([1, 2, 3, 4, 5, 6, 7, 8, 9]);
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
            const m = new Mat3([1, 2, 3, 4, 5, 6, 7, 8, 9]);
            expectMatrixClose(Mat3.multiply(Mat3.IDENTITY, m), [1, 2, 3, 4, 5, 6, 7, 8, 9]);
        });

        test('M * identity = M', () => {
            const m = new Mat3([1, 2, 3, 4, 5, 6, 7, 8, 9]);
            expectMatrixClose(Mat3.multiply(m, Mat3.IDENTITY), [1, 2, 3, 4, 5, 6, 7, 8, 9]);
        });

        test('associativity: (AB)C = A(BC)', () => {
            const a = new Mat3([1, 0, 1, 0, 2, 0, 0, 0, 3]);
            const b = new Mat3([2, 1, 0, 0, 1, 1, 1, 0, 2]);
            const c = new Mat3([1, 0, 0, 0, 3, 0, 0, 1, 1]);
            const ab_c = Mat3.multiply(Mat3.multiply(a, b), c);
            const a_bc = Mat3.multiply(a, Mat3.multiply(b, c));
            expectMatrixClose(ab_c, a_bc.data as unknown as number[], 1e-10);
        });

        test('out parameter', () => {
            const a = new Mat3([1, 2, 3, 4, 5, 6, 7, 8, 9]);
            const b = Mat3.IDENTITY;
            const out = new Mat3();
            const result = Mat3.multiply(a, b, out);
            expect(result).toBe(out);
        });

        test('instance multiply', () => {
            const a = new Mat3([1, 0, 0, 0, 2, 0, 0, 0, 3]);
            const b = new Mat3([4, 0, 0, 0, 5, 0, 0, 0, 6]);
            const result = a.multiply(b);
            expect(result).toBe(a);
            expectMatrixClose(a, [4, 0, 0, 0, 10, 0, 0, 0, 18]);
        });
    });

    describe('transpose', () => {
        test('double transpose = original', () => {
            const m = new Mat3([1, 2, 3, 4, 5, 6, 7, 8, 9]);
            expectMatrixClose(Mat3.transpose(Mat3.transpose(m)), [1, 2, 3, 4, 5, 6, 7, 8, 9]);
        });

        test('identity transpose = identity', () => {
            expectMatrixClose(Mat3.transpose(Mat3.IDENTITY), IDENTITY_3);
        });

        test('out parameter', () => {
            const m = new Mat3([1, 2, 3, 4, 5, 6, 7, 8, 9]);
            const out = new Mat3();
            const result = Mat3.transpose(m, out);
            expect(result).toBe(out);
            expectMatrixClose(out, [1, 4, 7, 2, 5, 8, 3, 6, 9]);
        });

        test('instance transpose', () => {
            const m = new Mat3([1, 2, 3, 4, 5, 6, 7, 8, 9]);
            const result = m.transpose();
            expect(result).toBe(m);
        });
    });

    describe('determinant', () => {
        test('identity det = 1', () => {
            expect(Mat3.determinant(Mat3.IDENTITY)).toBeCloseTo(1, 10);
        });

        test('zero matrix det = 0', () => {
            expect(Mat3.determinant(Mat3.ZERO)).toBe(0);
        });

        test('known determinant via cofactor expansion', () => {
            // [1,2,3]
            // [4,5,6] => det = 1(5*9-6*8) - 2(4*9-6*7) + 3(4*8-5*7) = -3+6-3 = 0
            // [7,8,9]
            const m = new Mat3([1, 2, 3, 4, 5, 6, 7, 8, 9]);
            expect(Mat3.determinant(m)).toBeCloseTo(0, 10);
        });

        test('non-singular matrix', () => {
            const m = new Mat3([1, 0, 0, 0, 2, 0, 0, 0, 3]);
            expect(Mat3.determinant(m)).toBeCloseTo(6, 10);
        });

        test('instance determinant', () => {
            const m = new Mat3([1, 0, 0, 0, 2, 0, 0, 0, 3]);
            expect(m.determinant()).toBeCloseTo(6, 10);
        });
    });

    describe('invert', () => {
        test('identity inverse = identity', () => {
            expectMatrixClose(Mat3.invert(Mat3.IDENTITY), IDENTITY_3, 1e-10);
        });

        test('M * M^-1 = identity', () => {
            const m = new Mat3([1, 0, 1, 0, 2, 0, 0, 0, 3]);
            const inv = Mat3.invert(m);
            expectMatrixClose(Mat3.multiply(m, inv), IDENTITY_3, 1e-10);
        });

        test('singular matrix throws', () => {
            const singular = new Mat3([1, 2, 3, 4, 5, 6, 7, 8, 9]); // det = 0
            expect(() => Mat3.invert(singular)).toThrow('Matrix is not invertible');
        });

        test('out parameter', () => {
            const m = new Mat3([1, 0, 0, 0, 2, 0, 0, 0, 4]);
            const out = new Mat3();
            const result = Mat3.invert(m, out);
            expect(result).toBe(out);
            expectMatrixClose(Mat3.multiply(m, out), IDENTITY_3, 1e-10);
        });

        test('instance invert', () => {
            const original = new Mat3([1, 0, 0, 0, 2, 0, 0, 0, 4]);
            const m = new Mat3([1, 0, 0, 0, 2, 0, 0, 0, 4]);
            m.invert();
            expectMatrixClose(Mat3.multiply(original, m), IDENTITY_3, 1e-10);
        });
    });

    // ─── 2D/3D Transformation Matrices ───────────────────────────────────
    describe('translate2D', () => {
        test('produces affine translation matrix', () => {
            const t = Mat3.translate2D({ x: 5, y: 7 });
            expectMatrixClose(t, [1, 0, 5, 0, 1, 7, 0, 0, 1]);
        });

        test('out parameter', () => {
            const out = new Mat3();
            Mat3.translate2D({ x: 3, y: 4 }, out);
            expectMatrixClose(out, [1, 0, 3, 0, 1, 4, 0, 0, 1]);
        });
    });

    describe('scale2D', () => {
        test('produces diagonal 2D scale matrix', () => {
            expectMatrixClose(Mat3.scale2D({ x: 2, y: 3 }), [2, 0, 0, 0, 3, 0, 0, 0, 1]);
        });

        test('out parameter', () => {
            const out = new Mat3();
            Mat3.scale2D({ x: 5, y: 7 }, out);
            expectMatrixClose(out, [5, 0, 0, 0, 7, 0, 0, 0, 1]);
        });
    });

    describe('scale3D', () => {
        test('produces 3D diagonal scale matrix', () => {
            expectMatrixClose(Mat3.scale3D({ x: 2, y: 3, z: 4 }), [2, 0, 0, 0, 3, 0, 0, 0, 4]);
        });
    });

    describe('rotateX / rotateY / rotateZ', () => {
        test('rotateX by PI/2', () => {
            const r = Mat3.rotateX(HALF_PI);
            // [1, 0,  0]
            // [0, c, -s] = [0, 0, -1]
            // [0, s,  c]   [0, 1,  0]
            expectMatrixClose(r, [1, 0, 0, 0, 0, -1, 0, 1, 0], 1e-10);
        });

        test('rotateY by PI/2', () => {
            const r = Mat3.rotateY(HALF_PI);
            // [ c, 0, s]   [0, 0, 1]
            // [ 0, 1, 0] = [0, 1, 0]
            // [-s, 0, c]   [-1,0, 0]
            expectMatrixClose(r, [0, 0, 1, 0, 1, 0, -1, 0, 0], 1e-10);
        });

        test('rotateZ by PI/2', () => {
            const r = Mat3.rotateZ(HALF_PI);
            expectMatrixClose(r, [0, -1, 0, 1, 0, 0, 0, 0, 1], 1e-10);
        });

        test('rotation preserves vector length', () => {
            const r = Mat3.rotateX(1.23);
            const v = { x: 1, y: 2, z: 3 };
            const tv = Mat3.transformVec3(v, r);
            const origLen = Math.sqrt(1 + 4 + 9);
            const newLen = Math.sqrt(tv.x * tv.x + tv.y * tv.y + tv.z * tv.z);
            expect(Math.abs(origLen - newLen)).toBeLessThan(1e-10);
        });
    });

    describe('rotateAxis', () => {
        test('rotateAxis(UNIT_X) === rotateX', () => {
            const angle = 0.7;
            const axis = Mat3.rotateAxis({ x: 1, y: 0, z: 0 }, angle);
            const rotX = Mat3.rotateX(angle);
            expectMatrixClose(axis, rotX.data as unknown as number[], 1e-10);
        });

        test('rotateAxis(UNIT_Y) === rotateY', () => {
            const angle = 1.2;
            const axis = Mat3.rotateAxis({ x: 0, y: 1, z: 0 }, angle);
            const rotY = Mat3.rotateY(angle);
            expectMatrixClose(axis, rotY.data as unknown as number[], 1e-10);
        });

        test('rotateAxis(UNIT_Z) === rotateZ', () => {
            const angle = 0.3;
            const axis = Mat3.rotateAxis({ x: 0, y: 0, z: 1 }, angle);
            const rotZ = Mat3.rotateZ(angle);
            expectMatrixClose(axis, rotZ.data as unknown as number[], 1e-10);
        });

        test('zero-length axis throws', () => {
            expect(() => Mat3.rotateAxis({ x: 0, y: 0, z: 0 }, 1)).toThrow('zero-length axis');
        });

        test('out parameter', () => {
            const out = new Mat3();
            const result = Mat3.rotateAxis({ x: 0, y: 0, z: 1 }, HALF_PI, out);
            expect(result).toBe(out);
        });
    });

    // ─── Vector Transformations ──────────────────────────────────────────
    describe('transformVec2', () => {
        test('identity preserves vec2', () => {
            const r = Mat3.transformVec2({ x: 3, y: 4 }, Mat3.IDENTITY);
            expect(Math.abs(r.x - 3)).toBeLessThan(EPSILON);
            expect(Math.abs(r.y - 4)).toBeLessThan(EPSILON);
        });

        test('translation matrix translates vec2', () => {
            const t = Mat3.translate2D({ x: 10, y: 20 });
            const r = Mat3.transformVec2({ x: 1, y: 2 }, t);
            expect(Math.abs(r.x - 11)).toBeLessThan(EPSILON);
            expect(Math.abs(r.y - 22)).toBeLessThan(EPSILON);
        });

        test('out parameter', () => {
            const out = { x: 0, y: 0 };
            const result = Mat3.transformVec2({ x: 1, y: 2 }, Mat3.IDENTITY, out);
            expect(result).toBe(out);
        });

        test('instance transformVec2', () => {
            const t = Mat3.translate2D({ x: 5, y: 10 });
            const r = t.transformVec2({ x: 1, y: 1 });
            expect(Math.abs(r.x - 6)).toBeLessThan(EPSILON);
            expect(Math.abs(r.y - 11)).toBeLessThan(EPSILON);
        });
    });

    describe('transformVec3', () => {
        test('identity preserves vec3', () => {
            const r = Mat3.transformVec3({ x: 1, y: 2, z: 3 }, Mat3.IDENTITY);
            expect(Math.abs(r.x - 1)).toBeLessThan(EPSILON);
            expect(Math.abs(r.y - 2)).toBeLessThan(EPSILON);
            expect(Math.abs(r.z - 3)).toBeLessThan(EPSILON);
        });

        test('scale3D transforms vec3', () => {
            const s = Mat3.scale3D({ x: 2, y: 3, z: 4 });
            const r = Mat3.transformVec3({ x: 1, y: 2, z: 3 }, s);
            expect(Math.abs(r.x - 2)).toBeLessThan(EPSILON);
            expect(Math.abs(r.y - 6)).toBeLessThan(EPSILON);
            expect(Math.abs(r.z - 12)).toBeLessThan(EPSILON);
        });

        test('out parameter', () => {
            const out = { x: 0, y: 0, z: 0 };
            const result = Mat3.transformVec3({ x: 1, y: 2, z: 3 }, Mat3.IDENTITY, out);
            expect(result).toBe(out);
        });

        test('instance transformVec3', () => {
            const s = Mat3.scale3D({ x: 2, y: 3, z: 4 });
            const r = s.transformVec3({ x: 1, y: 1, z: 1 });
            expect(Math.abs(r.x - 2)).toBeLessThan(EPSILON);
            expect(Math.abs(r.y - 3)).toBeLessThan(EPSILON);
            expect(Math.abs(r.z - 4)).toBeLessThan(EPSILON);
        });
    });

    describe('transformNormal / transformNormalWithIT / computeNormalMatrix', () => {
        test('transformNormal uses inverse-transpose', () => {
            const s = Mat3.scale3D({ x: 2, y: 3, z: 4 });
            const normal = { x: 1, y: 0, z: 0 };
            const result = Mat3.transformNormal(normal, s);
            // inverse-transpose of diagonal(2,3,4) = diagonal(1/2, 1/3, 1/4)
            // transform (1,0,0) => (1/2, 0, 0)
            expect(Math.abs(result.x - 0.5)).toBeLessThan(1e-10);
            expect(Math.abs(result.y)).toBeLessThan(1e-10);
            expect(Math.abs(result.z)).toBeLessThan(1e-10);
        });

        test('transformNormalWithIT accepts pre-computed IT', () => {
            const s = Mat3.scale3D({ x: 2, y: 3, z: 4 });
            const it = Mat3.transpose(Mat3.invert(s));
            const normal = { x: 0, y: 1, z: 0 };
            const result = Mat3.transformNormalWithIT(normal, it);
            expect(Math.abs(result.x)).toBeLessThan(1e-10);
            expect(Math.abs(result.y - 1 / 3)).toBeLessThan(1e-10);
            expect(Math.abs(result.z)).toBeLessThan(1e-10);
        });

        test('computeNormalMatrix equals transpose(invert(m))', () => {
            const m = new Mat3([1, 0, 1, 0, 2, 0, 0, 0, 3]);
            const nm = Mat3.computeNormalMatrix(m);
            const expected = Mat3.transpose(Mat3.invert(m));
            expectMatrixClose(nm, expected.data as unknown as number[], 1e-10);
        });

        test('instance transformNormal', () => {
            const s = Mat3.scale3D({ x: 2, y: 4, z: 6 });
            const r = s.transformNormal({ x: 1, y: 0, z: 0 });
            expect(Math.abs(r.x - 0.5)).toBeLessThan(1e-10);
        });
    });

    // ─── Interpolation ───────────────────────────────────────────────────
    describe('lerp / lerpUnClamped', () => {
        const a = new Mat3([1, 2, 3, 4, 5, 6, 7, 8, 9]);
        const b = new Mat3([9, 8, 7, 6, 5, 4, 3, 2, 1]);

        test('lerp t=0 returns a', () => {
            expectMatrixClose(Mat3.lerp(a, b, 0), [1, 2, 3, 4, 5, 6, 7, 8, 9]);
        });

        test('lerp t=1 returns b', () => {
            expectMatrixClose(Mat3.lerp(a, b, 1), [9, 8, 7, 6, 5, 4, 3, 2, 1]);
        });

        test('lerp t=0.5 returns midpoint', () => {
            expectMatrixClose(Mat3.lerp(a, b, 0.5), [5, 5, 5, 5, 5, 5, 5, 5, 5]);
        });

        test('lerp clamps t > 1', () => {
            expectMatrixClose(Mat3.lerp(a, b, 2), [9, 8, 7, 6, 5, 4, 3, 2, 1]);
        });

        test('lerpUnClamped extrapolates', () => {
            expectMatrixClose(Mat3.lerpUnClamped(a, b, 2), [17, 14, 11, 8, 5, 2, -1, -4, -7]);
        });

        test('lerp out parameter', () => {
            const out = new Mat3();
            const result = Mat3.lerp(a, b, 0.5, out);
            expect(result).toBe(out);
            expectMatrixClose(out, [5, 5, 5, 5, 5, 5, 5, 5, 5]);
        });
    });

    // ─── Serialization ───────────────────────────────────────────────────
    describe('toArray / toString', () => {
        test('toArray returns copy', () => {
            const m = new Mat3([1, 2, 3, 4, 5, 6, 7, 8, 9]);
            const arr = m.toArray();
            expect(arr).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9]);
            arr[0] = 999;
            expect(m.data[0]).toBe(1);
        });

        test('toString contains Mat3', () => {
            const str = new Mat3([1, 2, 3, 4, 5, 6, 7, 8, 9]).toString();
            expect(str).toContain('Mat3');
        });
    });

    // ─── Mat3Comparer ────────────────────────────────────────────────────
    describe('Mat3Comparer', () => {
        describe('FROBENIUS_NORM mode', () => {
            test('equal norms return 0', () => {
                const a = new Mat3([1, 2, 3, 4, 5, 6, 7, 8, 9]);
                const b = new Mat3([9, 8, 7, 6, 5, 4, 3, 2, 1]);
                const comparer = new Mat3Comparer(Mat3ComparisonMode.FROBENIUS_NORM);
                expect(comparer.compare(a, b)).toBe(0);
            });

            test('larger norm returns 1', () => {
                const a = new Mat3([10, 0, 0, 0, 0, 0, 0, 0, 0]);
                const b = new Mat3([1, 0, 0, 0, 1, 0, 0, 0, 1]);
                const comparer = new Mat3Comparer(Mat3ComparisonMode.FROBENIUS_NORM);
                expect(comparer.compare(a, b)).toBe(1);
            });
        });

        describe('DETERMINANT mode', () => {
            test('equal determinants return 0', () => {
                const a = new Mat3([1, 0, 0, 0, 1, 0, 0, 0, 1]); // det = 1
                const b = new Mat3([2, 1, 0, 0, 1, 0, 0, 0, 1]); // det = 2*1-1*0 = 2... actually let's compute
                // b: 2(1*1-0*0) - 1(0*1-0*0) + 0 = 2
                // a: 1
                const comparer = new Mat3Comparer(Mat3ComparisonMode.DETERMINANT);
                expect(comparer.compare(a, b)).toBe(-1);
            });
        });

        describe('TRACE mode', () => {
            test('equal traces return 0', () => {
                const a = new Mat3([3, 0, 0, 0, 1, 0, 0, 0, 1]); // trace = 5
                const b = new Mat3([1, 0, 0, 0, 2, 0, 0, 0, 2]); // trace = 5
                const comparer = new Mat3Comparer(Mat3ComparisonMode.TRACE);
                expect(comparer.compare(a, b)).toBe(0);
            });
        });

        describe('CONDITION_NUMBER mode', () => {
            test('equal condition numbers return 0', () => {
                // cond = max/min of abs elements: both have ratio 2/1 = 2
                const a = new Mat3([1, 2, 1, 1, 1, 2, 1, 1, 1]); // cond = 2
                const b = new Mat3([3, 6, 3, 3, 3, 6, 3, 3, 3]); // cond = 2
                const comparer = new Mat3Comparer(Mat3ComparisonMode.CONDITION_NUMBER);
                expect(comparer.compare(a, b)).toBe(0);
            });
        });

        test('default mode is FROBENIUS_NORM', () => {
            const comparer = new Mat3Comparer();
            const a = new Mat3([1, 0, 0, 0, 0, 0, 0, 0, 0]);
            const b = new Mat3([0, 0, 0, 0, 0, 0, 0, 0, 1]);
            expect(comparer.compare(a, b)).toBe(0);
        });
    });

    // ─── Integration Tests ───────────────────────────────────────────────
    describe('Integration', () => {
        test('rotate then inverse-rotate = identity', () => {
            const r = Mat3.rotateZ(0.7);
            const rInv = Mat3.invert(r);
            expectMatrixClose(Mat3.multiply(r, rInv), IDENTITY_3, 1e-10);
        });

        test('translate2D then transformVec2 matches', () => {
            const t = Mat3.translate2D({ x: 10, y: 20 });
            const v = { x: 5, y: 7 };
            const r = Mat3.transformVec2(v, t);
            expect(Math.abs(r.x - 15)).toBeLessThan(EPSILON);
            expect(Math.abs(r.y - 27)).toBeLessThan(EPSILON);
        });

        test('composed scale-rotate preserves structure', () => {
            const s = Mat3.scale2D({ x: 2, y: 2 });
            const r = Mat3.rotateZ(HALF_PI);
            const sr = Mat3.multiply(s, r);
            // det(sr) = det(s) * det(r) = 4 * 1 = 4
            expect(Mat3.determinant(sr)).toBeCloseTo(4, 10);
        });

        test('computeNormalMatrix is inverse-transpose', () => {
            const m = Mat3.scale3D({ x: 2, y: 3, z: 4 });
            const nm = Mat3.computeNormalMatrix(m);
            const expected = Mat3.transpose(Mat3.invert(m));
            expectMatrixClose(nm, expected.data as unknown as number[], 1e-10);
        });
    });

    // ─── Performance ─────────────────────────────────────────────────────
    describe('Performance', () => {
        test('multiply 100k times completes quickly', () => {
            const a = new Mat3([1, 2, 3, 4, 5, 6, 7, 8, 9]);
            const b = new Mat3([9, 8, 7, 6, 5, 4, 3, 2, 1]);
            const start = performance.now();
            for (let i = 0; i < 100000; i++) Mat3.multiply(a, b);
            expect(performance.now() - start).toBeLessThan(5000);
        });
    });
});
