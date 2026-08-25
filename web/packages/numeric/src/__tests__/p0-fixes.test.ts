import { describe, expect, it } from 'vitest';
import { Color, floatEquals, Mat2, Mat4, Vec2 } from '../../src';

describe('P0 regression: Mat4.lookAt', () => {
    it('places eye at the origin of camera space', () => {
        const eye = { x: 0, y: 0, z: 5 };
        const center = { x: 0, y: 0, z: 0 };
        const up = { x: 0, y: 1, z: 0 };
        const view = Mat4.lookAt(eye, center, up);

        const origin = Mat4.transformVec3({ x: 0, y: 0, z: 0 }, view);
        const eyeInCam = Mat4.transformVec3(eye, view);
        expect(eyeInCam.x).toBeCloseTo(0, 6);
        expect(eyeInCam.y).toBeCloseTo(0, 6);
        expect(eyeInCam.z).toBeCloseTo(0, 6);
        expect(origin.x).toBeCloseTo(0, 6);
        expect(origin.y).toBeCloseTo(0, 6);
        expect(origin.z).toBeCloseTo(-5, 6);
    });

    it('produces the canonical lookAt for a side view', () => {
        const view = Mat4.lookAt({ x: 5, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, { x: 0, y: 1, z: 0 });
        const d = view.data;
        expect(d[0]).toBeCloseTo(0);
        expect(d[1]).toBeCloseTo(0);
        expect(d[2]).toBeCloseTo(-1);
        expect(d[3]).toBeCloseTo(0);
        expect(d[4]).toBeCloseTo(0);
        expect(d[5]).toBeCloseTo(1);
        expect(d[6]).toBeCloseTo(0);
        expect(d[7]).toBeCloseTo(0);
        expect(d[8]).toBeCloseTo(1);
        expect(d[9]).toBeCloseTo(0);
        expect(d[10]).toBeCloseTo(0);
        expect(d[11]).toBeCloseTo(-5);
        expect(d[12]).toBeCloseTo(0);
        expect(d[13]).toBeCloseTo(0);
        expect(d[14]).toBeCloseTo(0);
        expect(d[15]).toBeCloseTo(1);
    });

    it('returns the identity matrix when eye equals center', () => {
        const eye = { x: 1, y: 2, z: 3 };
        const view = Mat4.lookAt(eye, eye, { x: 0, y: 1, z: 0 });
        for (let i = 0; i < 16; i++) {
            expect(view.data[i]).toBeCloseTo(i % 5 === 0 ? 1 : 0);
        }
    });

    it('produces an orthonormal rotation (rows are unit length & orthogonal)', () => {
        const view = Mat4.lookAt({ x: 2, y: 3, z: 4 }, { x: 0, y: 0, z: 0 }, { x: 0, y: 1, z: 0 });
        const d = view.data;
        const rowLens = [
            Math.hypot(d[0], d[1], d[2]),
            Math.hypot(d[4], d[5], d[6]),
            Math.hypot(d[8], d[9], d[10]),
        ];
        for (const l of rowLens) expect(l).toBeCloseTo(1, 6);

        const dot = (a: number[], b: number[]) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        const r0 = [d[0], d[1], d[2]];
        const r1 = [d[4], d[5], d[6]];
        const r2 = [d[8], d[9], d[10]];
        expect(dot(r0, r1)).toBeCloseTo(0, 6);
        expect(dot(r1, r2)).toBeCloseTo(0, 6);
        expect(dot(r0, r2)).toBeCloseTo(0, 6);
    });

    it('writes to `out` consistently with the no-out branch', () => {
        const eye = { x: 1, y: 2, z: 3 };
        const center = { x: 0, y: 0, z: 0 };
        const up = { x: 0, y: 1, z: 0 };
        const fresh = Mat4.lookAt(eye, center, up);
        const out = new Mat4();
        Mat4.lookAt(eye, center, up, out);
        for (let i = 0; i < 16; i++) {
            expect(out.data[i]).toBeCloseTo(fresh.data[i], 6);
        }
    });
});

describe('P0 regression: Vec2.slerp', () => {
    it('returns a at t=0 and b at t=1', () => {
        const a: Vec2 = { x: 1, y: 0 };
        const b: Vec2 = { x: 0, y: 1 };
        const r0 = Vec2.slerp(a, b, 0);
        const r1 = Vec2.slerp(a, b, 1);
        expect(floatEquals(r0.x, 1)).toBe(true);
        expect(floatEquals(r0.y, 0)).toBe(true);
        expect(floatEquals(r1.x, 0)).toBe(true);
        expect(floatEquals(r1.y, 1)).toBe(true);
    });

    it('interpolates along the quarter arc at t=0.5 (radius preserved, angle = 45deg)', () => {
        const a: Vec2 = { x: 2, y: 0 };
        const b: Vec2 = { x: 0, y: 2 };
        const r = Vec2.slerp(a, b, 0.5);
        const expected = Math.SQRT2;
        expect(floatEquals(r.x, expected)).toBe(true);
        expect(floatEquals(r.y, expected)).toBe(true);
    });

    it('takes the shortest path across the wrap-around at 180 degrees', () => {
        const a: Vec2 = { x: 1, y: 0 };
        const b: Vec2 = { x: -1, y: 0 };
        const r = Vec2.slerp(a, b, 0.5);
        expect(floatEquals(r.x, 0)).toBe(true);
        expect(floatEquals(Math.abs(r.y), 1)).toBe(true);
    });

    it('interpolates radius linearly when angles are equal', () => {
        const a: Vec2 = { x: 1, y: 0 };
        const b: Vec2 = { x: 3, y: 0 };
        const r = Vec2.slerp(a, b, 0.5);
        expect(floatEquals(r.x, 2)).toBe(true);
        expect(floatEquals(r.y, 0)).toBe(true);
    });

    it('falls back to lerpUnClamped for zero-length inputs', () => {
        const a: Vec2 = { x: 0, y: 0 };
        const b: Vec2 = { x: 2, y: 0 };
        const r = Vec2.slerp(a, b, 0.5);
        expect(floatEquals(r.x, 1)).toBe(true);
        expect(floatEquals(r.y, 0)).toBe(true);
    });

    it('writes to `out` correctly', () => {
        const a: Vec2 = { x: 1, y: 0 };
        const b: Vec2 = { x: 0, y: 1 };
        const out: Vec2 = { x: 0, y: 0 };
        const r = Vec2.slerp(a, b, 0.5, out);
        expect(r).toBe(out);
        expect(floatEquals(out.x, Math.SQRT2 / 2)).toBe(true);
    });
});

describe('P0 regression: Mat2.translate', () => {
    it('throws because 2x2 cannot represent 2D affine translation', () => {
        expect(() => Mat2.translate({ x: 1, y: 2 })).toThrowError(/Mat2\.translate is not supported/);
    });

    it('throws even when an `out` matrix is provided', () => {
        const out = new Mat2();
        expect(() => Mat2.translate({ x: 1, y: 0 }, out)).toThrowError(/Mat2\.translate is not supported/);
    });
});

describe('P0 regression: Color.SILVER', () => {
    it('equals #C0C0C0 (192/255 in linear sRGB)', () => {
        const silver = Color.SILVER;
        expect(floatEquals(silver.r, 192 / 255, 1e-9)).toBe(true);
        expect(floatEquals(silver.g, 192 / 255, 1e-9)).toBe(true);
        expect(floatEquals(silver.b, 192 / 255, 1e-9)).toBe(true);
        expect(floatEquals(silver.a, 1, 1e-9)).toBe(true);
    });

    it('differs from LIGHT_GRAY (0.75) to expose the previous off-by-tile bug', () => {
        const silver = Color.SILVER;
        const lightGray = Color.LIGHT_GRAY;
        const same = floatEquals(silver.r, lightGray.r) && floatEquals(silver.g, lightGray.g) && floatEquals(silver.b, lightGray.b);
        expect(same).toBe(false);
    });
});
