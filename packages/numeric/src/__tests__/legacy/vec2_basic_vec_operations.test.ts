import * as vec2 from '../../vec2_legacy';
import { Vec2, ReadonlyVec2 } from '../../vec2_legacy';

describe('Basic Vector Operations (inplace)', () => {
    let out: Vec2;
    const v1: ReadonlyVec2 = Object.freeze({ x: 1, y: 2 });
    const v2: ReadonlyVec2 = Object.freeze({ x: 3, y: 4 });
    const v3: Vec2 = { x: -1, y: 0.5 };

    beforeEach(() => {
        out = vec2.create(0, 0);
    });

    test('set should modify vector components and return it', () => {
        const result = vec2.set(out, 5, -6);
        expect(out).toEqual({ x: 5, y: -6 });
        expect(result).toBe(out);
    });

    test('copy should copy components from Vec2Like and return out', () => {
        const result1 = vec2.copy(out, v1);
        expect(out).toEqual({ x: 1, y: 2 });
        expect(result1).toBe(out);

        const result2 = vec2.copy(out, v2);
        expect(out).toEqual({ x: 3, y: 4 });
        expect(result2).toBe(out);

        const result3 = vec2.copy(out, v3);
        expect(out).toEqual({ x: -1, y: 0.5 });
        expect(result3).toBe(out);
    });

    test('add should add components of two Vec2Likes', () => {
        vec2.add(out, v1, v2); // {1,2} + [3,4]
        expect(out).toEqual({ x: 4, y: 6 });

        vec2.add(out, out, v3); // {4,6} + {-1, 0.5}
        expect(out).toEqual({ x: 3, y: 6.5 });
    });

    test('addScalar should add scalar to each component', () => {
        vec2.addScalar(out, v1, 10); // {1,2} + 10
        expect(out).toEqual({ x: 11, y: 12 });
    });

    test('subtract should subtract components of two Vec2Likes', () => {
        vec2.subtract(out, v1, v2); // {1,2} - [3,4]
        expect(out).toEqual({ x: -2, y: -2 });

        vec2.subtract(out, v3, out); // {-1, 0.5} - {-2, -2}
        expect(out).toEqual({ x: 1, y: 2.5 });
    });

    test('subtractScalar should subtract scalar from each component', () => {
        vec2.subtractScalar(out, v2, 1); // [3,4] - 1
        expect(out).toEqual({ x: 2, y: 3 });
    });

    test('multiply should multiply components of two Vec2Likes', () => {
        vec2.multiply(out, v1, v2); // {1,2} * [3,4]
        expect(out).toEqual({ x: 3, y: 8 });
    });

    test('multiplyScalar should multiply components by scalar', () => {
        vec2.multiplyScalar(out, v3, -2); // {-1, 0.5} * -2
        expect(out).toEqual({ x: 2, y: -1 });
    });

    test('divide should divide components of two Vec2Likes', () => {
        const vA = vec2.create(6, -8);
        const vB = vec2.create(3, 2);
        vec2.divide(out, vA, vB); // {6,-8} / [3,2]
        expect(out).toEqual({ x: 2, y: -4 });
    });

    test('divide should throw on division by zero or near-zero', () => {
        const vA = vec2.create(1, 1);
        expect(() => vec2.divide(out, vA, { x: 0, y: 1 })).toThrow();
        expect(() => vec2.divide(out, vA, { x: 1, y: 0 })).toThrow();
        expect(() => vec2.divide(out, vA, { x: 1e-12, y: 1 })).toThrow();
        expect(() => vec2.divide(out, vA, { x: 1, y: 1e-12 })).toThrow();
    });

    test('divideScalar should divide components by scalar', () => {
        vec2.divideScalar(out, v1, 2); // {1,2} / 2
        expect(out).toEqual({ x: 0.5, y: 1 });
    });

    test('divideScalar should throw on division by zero or near-zero', () => {
        expect(() => vec2.divideScalar(out, v1, 0)).toThrow();
        expect(() => vec2.divideScalar(out, v1, 1e-14)).toThrow();
    });

    test('negate should negate each component', () => {
        vec2.negate(out, v1); // negate {1,2}
        expect(out).toEqual({ x: -1, y: -2 });
        vec2.negate(out, out); // negate {-1, -2}
        expect(out).toEqual({ x: 1, y: 2 });
    });

    test('inverse should invert each non-zero component', () => {
        const vA = vec2.create(2, -4);
        vec2.inverse(out, vA); // inverse {2, -4}
        expect(out).toEqual({ x: 0.5, y: -0.25 });
    });

    test('inverse should throw on inversion of zero or near-zero', () => {
        expect(() => vec2.inverse(out, { x: 0, y: 1 })).toThrow();
        expect(() => vec2.inverse(out, { x: 1, y: 0 })).toThrow();
        expect(() => vec2.inverse(out, { x: 1e-12, y: 1 })).toThrow();
    });

    test('inverseSafe should invert non-zero components and use default for zero', () => {
        vec2.inverseSafe(out, { x: 2, y: 0 });
        expect(out).toEqual({ x: 0.5, y: 0 });

        vec2.inverseSafe(out, { x: 0, y: -4 });
        expect(out).toEqual({ x: 0, y: -0.25 });

        vec2.inverseSafe(out, { x: 1e-12, y: 5 }, 999);
        expect(out.x).toBe(999);
        expect(out.y).toBeCloseTo(1 / 5);
    });

    test('operations should return the modified out vector (chaining)', () => {
        vec2.add(out, v1, v2); // out = {1,2} + [3,4] = {4,6}
        vec2.multiplyScalar(out, out, 2); // out = {4,6} * 2 = {8,12}
        const result = vec2.addScalar(out, out, 1); // out = {8,12} + 1 = {9,13}

        expect(result).toBe(out);
        expect(out).toEqual({ x: 9, y: 13 });
    });

    test('input vectors should remain unchanged', () => {
        const v1Before = { ...v1 };
        const v2Before = { ...v2 };
        const v3Before = { ...v3 };

        vec2.add(out, v1, v2);
        vec2.subtract(out, v3, v1);
        vec2.multiplyScalar(out, v2, 5);
        vec2.divideScalar(out, v3, 2);

        expect(v1).toEqual(v1Before);
        expect(v2).toEqual(v2Before);
        expect(v3).toEqual(v3Before);
    });
});
