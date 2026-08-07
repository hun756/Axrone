import { describe, expect, test } from 'vitest';
import { Vec2, Vec3, Mat3, Mat4, Quat, Color } from '../index';

describe('P1 Performance Refactor Regression Tests', () => {
    describe('Float bit pattern hash quality', () => {
        test('distinct close floats produce distinct hashes', () => {
            const a = new Vec2(0.0009, 0);
            const b = new Vec2(0.001, 0);
            const c = new Vec2(0.0011, 0);
            const ha = a.getHashCode();
            const hb = b.getHashCode();
            const hc = c.getHashCode();
            expect(ha).not.toBe(hb);
            expect(hb).not.toBe(hc);
            expect(ha).not.toBe(hc);
        });

        test('hash of zero vector is deterministic', () => {
            const a = new Vec3(0, 0, 0);
            const b = new Vec3(0, 0, 0);
            expect(a.getHashCode()).toBe(b.getHashCode());
        });

        test('hash of negative-zero and positive-zero differ (IEEE 754 distinct bit patterns)', () => {
            const a = new Vec2(0, 0);
            const b = new Vec2(-0, 0);
            expect(a.getHashCode()).not.toBe(b.getHashCode());
        });

        test('hash is not the legacy FNV-1a with floor(x*1000) quantization (case where floor collisions occurred)', () => {
            const a = new Vec3(1.234, 5.678, 9.012);
            const b = new Vec3(1.235, 5.678, 9.012);
            const ha = a.getHashCode();
            const hb = b.getHashCode();
            expect(ha).not.toBe(hb);
        });

        test('hash respects float32 precision bound (large magnitude sub-ULP differences may collide)', () => {
            const a = new Vec2(1e6, 1e6);
            const b = new Vec2(1e6 + 1e-3, 1e6);
            const aBits = new Float32Array([1e6])[0];
            const bBits = new Float32Array([1e6 + 1e-3])[0];
            if (aBits === bBits) {
                expect(a.getHashCode()).toBe(b.getHashCode());
            } else {
                expect(a.getHashCode()).not.toBe(b.getHashCode());
            }
        });
    });

    describe('Mat3.computeNormalMatrix + transformNormalWithIT', () => {
        test('computeNormalMatrix returns inverse-transpose of diagonal 3x3', () => {
            const m = new Mat3([2, 0, 0, 0, 3, 0, 0, 0, 4]);
            const it = Mat3.computeNormalMatrix(m);
            expect(it.data[0]).toBeCloseTo(0.5, 5);
            expect(it.data[4]).toBeCloseTo(1 / 3, 5);
            expect(it.data[8]).toBeCloseTo(0.25, 5);
        });

        test('transformNormalWithIT is mathematically equivalent to transformNormal', () => {
            const m = new Mat3([1, 0, 5, 2, 1, 6, 3, 4, 0]);
            const n = new Vec3(1, 2, 3);
            const it = Mat3.computeNormalMatrix(m);
            const resultWithIT = Mat3.transformNormalWithIT(n, it, new Vec3());
            const resultRegular = Mat3.transformNormal(n, m, new Vec3());
            expect(resultWithIT.x).toBeCloseTo(resultRegular.x, 4);
            expect(resultWithIT.y).toBeCloseTo(resultRegular.y, 4);
            expect(resultWithIT.z).toBeCloseTo(resultRegular.z, 4);
        });
    });

    describe('Mat4.fromTRS inlined output', () => {
        test('fromTRS produces T*R*S matrix equivalent to multiply chain', () => {
            const t = new Vec3(10, 20, 30);
            const r = Quat.fromEuler(0, Math.PI / 2, 0);
            const s = new Vec3(2, 3, 4);
            const result = Mat4.fromTRS(t, r, s, new Mat4());

            const sMat = Mat4.scale(s, new Mat4());
            const rMat = Mat4.fromQuaternion(r, new Mat4());
            const tMat = Mat4.translate(t, new Mat4());
            const m = Mat4.multiply(Mat4.multiply(tMat, rMat, new Mat4()), sMat, new Mat4());

            for (let i = 0; i < 16; i++) {
                expect(result.data[i]).toBeCloseTo(m.data[i], 5);
            }
        });

        test('fromTRS translation column matches input', () => {
            const t = new Vec3(5, 6, 7);
            const r = Quat.IDENTITY;
            const s = new Vec3(1, 1, 1);
            const result = Mat4.fromTRS(t, r, s, new Mat4());
            expect(result.data[3]).toBe(5);
            expect(result.data[7]).toBe(6);
            expect(result.data[11]).toBe(7);
            expect(result.data[15]).toBe(1);
        });

        test('fromTRS with identity rotation and scale matches translation matrix', () => {
            const t = new Vec3(1, 2, 3);
            const result = Mat4.fromTRS(t, Quat.IDENTITY, new Vec3(1, 1, 1), new Mat4());
            expect(result.data[0]).toBe(1);
            expect(result.data[5]).toBe(1);
            expect(result.data[10]).toBe(1);
            expect(result.data[1]).toBe(0);
            expect(result.data[2]).toBe(0);
        });
    });

    describe('Hash determinism for canonical values', () => {
        test('Color hash is deterministic and stable', () => {
            const a = Color.fromHSL(0.5, 0.5, 0.5, new Color());
            const b = Color.fromHSL(0.5, 0.5, 0.5, new Color());
            expect(a.getHashCode()).toBe(b.getHashCode());
        });

        test('Quat hash is deterministic', () => {
            const a = Quat.fromEuler(0.1, 0.2, 0.3, new Quat());
            const b = Quat.fromEuler(0.1, 0.2, 0.3, new Quat());
            expect(a.getHashCode()).toBe(b.getHashCode());
        });

        test('Mat4 hash is deterministic', () => {
            const a = Mat4.translate(new Vec3(1, 2, 3), new Mat4());
            const b = Mat4.translate(new Vec3(1, 2, 3), new Mat4());
            expect(a.getHashCode()).toBe(b.getHashCode());
        });
    });
});
