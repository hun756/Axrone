import { describe, expect, it } from 'vitest';
import { Mat4, Quat, Vec3 } from '@axrone/numeric';
import {
    ANIMATION_EPSILON,
    composeMatrix,
    mat4Invert,
    mat4Multiply,
    quatApplyToVec3,
    quatDot,
    quatFromTo,
    quatIdentity,
    quatInvert,
    quatMultiply,
    quatNormalize,
    quatSlerp,
    vec3Length,
    vec3LengthSquared,
    vec3Normalize,
} from '../math';

const createRandom = (seed: number): (() => number) => {
    let state = seed >>> 0;
    return () => {
        state = (state * 1664525 + 1013904223) >>> 0;
        return state / 0xffffffff;
    };
};

const random = createRandom(0x5eed);
const randomValue = (): number => random() * 4 - 2;

const randomQuat = (): Float32Array => {
    const value = new Float32Array([randomValue(), randomValue(), randomValue(), randomValue() + 0.5]);
    quatNormalize(value, 0, value, 0);
    return value;
};

const randomVec3 = (): Float32Array => new Float32Array([randomValue(), randomValue(), randomValue()]);

const expectClose = (actual: ArrayLike<number>, expected: ArrayLike<number>, digits = 5): void => {
    for (let index = 0; index < expected.length; index += 1) {
        expect(actual[index]).toBeCloseTo(expected[index]!, digits);
    }
};

describe('animation math kernels stay in parity with @axrone/numeric', () => {
    it('matches Quat.multiply', () => {
        for (let run = 0; run < 25; run += 1) {
            const a = randomQuat();
            const b = randomQuat();
            const out = new Float32Array(4);
            quatMultiply(out, 0, a, 0, b, 0);
            const expected = Quat.multiply(
                new Quat(a[0], a[1], a[2], a[3]),
                new Quat(b[0], b[1], b[2], b[3]),
                new Quat()
            );
            expectClose(out, [expected.x, expected.y, expected.z, expected.w]);
        }
    });

    it('matches Quat.normalize and Quat.dot', () => {
        for (let run = 0; run < 25; run += 1) {
            const raw = new Float32Array([randomValue(), randomValue(), randomValue(), randomValue() + 0.5]);
            const out = new Float32Array(4);
            quatNormalize(out, 0, raw, 0);
            const expected = Quat.normalize(new Quat(raw[0], raw[1], raw[2], raw[3]), new Quat());
            expectClose(out, [expected.x, expected.y, expected.z, expected.w]);

            const other = randomQuat();
            expect(quatDot(out, 0, other, 0)).toBeCloseTo(
                Quat.dot(new Quat(out[0], out[1], out[2], out[3]), new Quat(other[0], other[1], other[2], other[3])),
                5
            );
        }
    });

    it('matches Quat.inverse', () => {
        for (let run = 0; run < 25; run += 1) {
            const value = randomQuat();
            const out = new Float32Array(4);
            quatInvert(out, 0, value, 0);
            const expected = Quat.inverse(new Quat(value[0], value[1], value[2], value[3]), new Quat());
            expectClose(out, [expected.x, expected.y, expected.z, expected.w]);
        }
    });

    it('matches normalized Quat.slerp', () => {
        for (let run = 0; run < 25; run += 1) {
            const a = randomQuat();
            const b = randomQuat();
            const alpha = random();
            const out = new Float32Array(4);
            quatSlerp(out, 0, a, 0, b, 0, alpha);
            const expected = Quat.normalize(
                Quat.slerp(
                    new Quat(a[0], a[1], a[2], a[3]),
                    new Quat(b[0], b[1], b[2], b[3]),
                    alpha,
                    new Quat()
                ),
                new Quat()
            );
            expectClose(out, [expected.x, expected.y, expected.z, expected.w]);
        }
    });

    it('matches Quat.rotateVector', () => {
        for (let run = 0; run < 25; run += 1) {
            const rotation = randomQuat();
            const vector = randomVec3();
            const out = new Float32Array(3);
            quatApplyToVec3(out, 0, rotation, 0, vector, 0);
            const expected = Quat.rotateVector(
                new Quat(rotation[0], rotation[1], rotation[2], rotation[3]),
                new Vec3(vector[0], vector[1], vector[2]),
                new Vec3()
            );
            expectClose(out, [expected.x, expected.y, expected.z]);
        }
    });

    it('matches Vec3 length helpers and normalize', () => {
        for (let run = 0; run < 25; run += 1) {
            const vector = randomVec3();
            const asVec = new Vec3(vector[0], vector[1], vector[2]);
            expect(vec3LengthSquared(vector, 0)).toBeCloseTo(Vec3.lengthSquared(asVec), 5);
            expect(vec3Length(vector, 0)).toBeCloseTo(Vec3.len(asVec), 5);

            const out = new Float32Array(3);
            vec3Normalize(out, 0, vector, 0);
            if (Vec3.len(asVec) > ANIMATION_EPSILON) {
                const expected = Vec3.normalize(asVec, new Vec3());
                expectClose(out, [expected.x, expected.y, expected.z]);
            }
        }
    });

    it('falls back safely on zero-length inputs', () => {
        const zero = new Float32Array(4);
        const out = new Float32Array(4);
        quatNormalize(out, 0, zero, 0);
        expectClose(out, [0, 0, 0, 1]);
        quatInvert(out, 0, zero, 0);
        expectClose(out, [0, 0, 0, 1]);
        const vecOut = new Float32Array(3);
        vec3Normalize(vecOut, 0, zero, 0, 1, 0, 0);
        expectClose(vecOut, [1, 0, 0]);
    });

    it('produces rotations that map from onto to (quatFromTo), including antipodal pairs', () => {
        const scratch = new Float32Array(9);
        for (let run = 0; run < 25; run += 1) {
            const from = randomVec3();
            const to = randomVec3();
            const rotation = new Float32Array(4);
            quatFromTo(rotation, 0, from, 0, to, 0, scratch);

            const normalizedFrom = new Float32Array(3);
            const normalizedTo = new Float32Array(3);
            vec3Normalize(normalizedFrom, 0, from, 0);
            vec3Normalize(normalizedTo, 0, to, 0);
            const rotated = new Float32Array(3);
            quatApplyToVec3(rotated, 0, rotation, 0, normalizedFrom, 0);
            expectClose(rotated, normalizedTo, 4);
        }

        const antipodalFrom = new Float32Array([1, 0, 0]);
        const antipodalTo = new Float32Array([-1, 0, 0]);
        const rotation = new Float32Array(4);
        quatFromTo(rotation, 0, antipodalFrom, 0, antipodalTo, 0, scratch);
        const rotated = new Float32Array(3);
        quatApplyToVec3(rotated, 0, rotation, 0, antipodalFrom, 0);
        expectClose(rotated, antipodalTo, 4);
    });

    it('matches Mat4.fromTRS through composeMatrix', () => {
        for (let run = 0; run < 25; run += 1) {
            const translation = randomVec3();
            const rotation = randomQuat();
            const scale = new Float32Array([random() + 0.5, random() + 0.5, random() + 0.5]);
            const out = new Float32Array(16);
            composeMatrix(out, 0, translation, 0, rotation, 0, scale, 0);
            const expected = Mat4.fromTRS(
                new Vec3(translation[0], translation[1], translation[2]),
                new Quat(rotation[0], rotation[1], rotation[2], rotation[3]),
                new Vec3(scale[0], scale[1], scale[2]),
                new Mat4()
            );
            expectClose(out, expected.data, 4);
        }
    });

    it('matches Mat4.multiply and Mat4.invert round trips', () => {
        for (let run = 0; run < 10; run += 1) {
            const translation = randomVec3();
            const rotation = randomQuat();
            const scale = new Float32Array([random() + 0.5, random() + 0.5, random() + 0.5]);
            const matrix = new Float32Array(16);
            composeMatrix(matrix, 0, translation, 0, rotation, 0, scale, 0);

            const product = new Float32Array(16);
            mat4Multiply(product, 0, matrix, 0, matrix, 0);
            const expectedProduct = Mat4.multiply(
                new Mat4(Array.from(matrix)),
                new Mat4(Array.from(matrix)),
                new Mat4()
            );
            expectClose(product, expectedProduct.data, 3);

            const inverse = new Float32Array(16);
            expect(mat4Invert(inverse, 0, matrix, 0)).toBe(true);
            const identity = new Float32Array(16);
            mat4Multiply(identity, 0, matrix, 0, inverse, 0);
            const expectedIdentity = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];
            expectClose(identity, expectedIdentity, 3);
        }
    });

    it('reports singular matrices as non-invertible', () => {
        const singular = new Float32Array(16);
        const out = new Float32Array(16);
        expect(mat4Invert(out, 0, singular, 0)).toBe(false);
    });

    it('writes identity quaternions in place', () => {
        const out = new Float32Array([9, 9, 9, 9]);
        quatIdentity(out, 0);
        expectClose(out, [0, 0, 0, 1]);
    });
});
