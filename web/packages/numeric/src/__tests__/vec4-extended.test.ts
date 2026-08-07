import { describe, expect, test } from 'vitest';
import { Vec4, IVec4Like } from '../vec4';
import { Fnv1a32 } from '@axrone/hash';
import { EPSILON } from '../common';

const expectVecClose = (actual: IVec4Like, expected: IVec4Like, eps = EPSILON) => {
    expect(Math.abs(actual.x - expected.x)).toBeLessThan(eps);
    expect(Math.abs(actual.y - expected.y)).toBeLessThan(eps);
    expect(Math.abs(actual.z - expected.z)).toBeLessThan(eps);
    expect(Math.abs(actual.w - expected.w)).toBeLessThan(eps);
};

const expectNumClose = (actual: number, expected: number, eps = EPSILON) => {
    expect(Math.abs(actual - expected)).toBeLessThan(eps);
};

describe('Vec4 - Additional Coverage', () => {
    // ─── Instance Method Variants (6 Rotation Planes) ─────────────────────
    describe('Instance Rotation Variants (6 planes)', () => {
        test('instance rotateXY matches static', () => {
            const v = new Vec4(1, 0, 5, 10);
            const staticResult = Vec4.rotateXY(new Vec4(1, 0, 5, 10), Math.PI / 2);
            v.rotateXY(Math.PI / 2);
            expectVecClose(v, staticResult, 1e-10);
        });

        test('instance rotateXZ matches static', () => {
            const v = new Vec4(1, 5, 0, 10);
            const staticResult = Vec4.rotateXZ(new Vec4(1, 5, 0, 10), Math.PI / 2);
            v.rotateXZ(Math.PI / 2);
            expectVecClose(v, staticResult, 1e-10);
        });

        test('instance rotateXW matches static', () => {
            const v = new Vec4(1, 5, 10, 0);
            const staticResult = Vec4.rotateXW(new Vec4(1, 5, 10, 0), Math.PI / 2);
            v.rotateXW(Math.PI / 2);
            expectVecClose(v, staticResult, 1e-10);
        });

        test('instance rotateYZ matches static', () => {
            const v = new Vec4(5, 1, 0, 10);
            const staticResult = Vec4.rotateYZ(new Vec4(5, 1, 0, 10), Math.PI / 2);
            v.rotateYZ(Math.PI / 2);
            expectVecClose(v, staticResult, 1e-10);
        });

        test('instance rotateYW matches static', () => {
            const v = new Vec4(5, 1, 10, 0);
            const staticResult = Vec4.rotateYW(new Vec4(5, 1, 10, 0), Math.PI / 2);
            v.rotateYW(Math.PI / 2);
            expectVecClose(v, staticResult, 1e-10);
        });

        test('instance rotateZW matches static', () => {
            const v = new Vec4(5, 10, 1, 0);
            const staticResult = Vec4.rotateZW(new Vec4(5, 10, 1, 0), Math.PI / 2);
            v.rotateZW(Math.PI / 2);
            expectVecClose(v, staticResult, 1e-10);
        });

        test('all instance rotations return this (mutation)', () => {
            const v1 = new Vec4(1, 0, 0, 0);
            expect(v1.rotateXY(0.5)).toBe(v1);

            const v2 = new Vec4(1, 0, 0, 0);
            expect(v2.rotateXZ(0.5)).toBe(v2);

            const v3 = new Vec4(1, 0, 0, 0);
            expect(v3.rotateXW(0.5)).toBe(v3);

            const v4 = new Vec4(0, 1, 0, 0);
            expect(v4.rotateYZ(0.5)).toBe(v4);

            const v5 = new Vec4(0, 1, 0, 0);
            expect(v5.rotateYW(0.5)).toBe(v5);

            const v6 = new Vec4(0, 0, 1, 0);
            expect(v6.rotateZW(0.5)).toBe(v6);
        });

        test('all instance rotations preserve length', () => {
            const original = new Vec4(3, 4, 5, 6);
            const len = original.length();

            const planes = ['rotateXY', 'rotateXZ', 'rotateXW', 'rotateYZ', 'rotateYW', 'rotateZW'] as const;
            for (const plane of planes) {
                const v = original.clone();
                (v as any)[plane](1.23);
                expectNumClose(v.length(), len, 1e-10);
            }
        });

        test('full rotation (2π) returns to original for all planes', () => {
            const original = new Vec4(1, 2, 3, 4);
            const planes = ['rotateXY', 'rotateXZ', 'rotateXW', 'rotateYZ', 'rotateYW', 'rotateZW'] as const;

            for (const plane of planes) {
                const v = original.clone();
                (v as any)[plane](2 * Math.PI);
                expectVecClose(v, original, 1e-10);
            }
        });
    });

    // ─── Instance multiply / divide Mutating Methods ──────────────────────
    describe('Instance multiply / divide', () => {
        test('instance multiply mutates in place', () => {
            const a = new Vec4(2, 3, 4, 5);
            const b = new Vec4(1, 2, 3, 4);
            const result = a.multiply(b);
            expect(result).toBe(a);
            expectVecClose(a, { x: 2, y: 6, z: 12, w: 20 });
        });

        test('instance multiply matches static', () => {
            const a = new Vec4(2, 3, 4, 5);
            const b = new Vec4(1, 2, 3, 4);
            const staticResult = Vec4.multiply(new Vec4(2, 3, 4, 5), b);
            a.multiply(b);
            expectVecClose(a, staticResult);
        });

        test('instance divide mutates in place', () => {
            const a = new Vec4(6, 8, 12, 16);
            const b = new Vec4(2, 4, 3, 4);
            const result = a.divide(b);
            expect(result).toBe(a);
            expectVecClose(a, { x: 3, y: 2, z: 4, w: 4 });
        });

        test('instance divide matches static', () => {
            const a = new Vec4(6, 8, 12, 16);
            const b = new Vec4(2, 4, 3, 4);
            const staticResult = Vec4.divide(new Vec4(6, 8, 12, 16), b);
            a.divide(b);
            expectVecClose(a, staticResult);
        });

        test('instance multiplyScalar mutates', () => {
            const v = new Vec4(1, 2, 3, 4);
            const result = v.multiplyScalar(3);
            expect(result).toBe(v);
            expectVecClose(v, { x: 3, y: 6, z: 9, w: 12 });
        });

        test('instance multiplyScalar mutates and returns this', () => {
            const v = new Vec4(1, 2, 3, 4);
            const result = v.multiplyScalar(0.5);
            expect(result).toBe(v);
            expectVecClose(v, { x: 0.5, y: 1, z: 1.5, w: 2 });
        });
    });

    // ─── hashInto Integration ─────────────────────────────────────────────
    describe('hashInto Integration', () => {
        test('hashInto integrates with Fnv1a32', () => {
            const hasher = new Fnv1a32();
            const v = new Vec4(1, 2, 3, 4);
            v.hashInto(hasher);
            const hash = hasher.digest();
            expect(typeof hash).toBe('number');
            expect(Number.isFinite(hash)).toBe(true);
        });

        test('hashInto produces different hashes for different vectors', () => {
            const h1 = new Fnv1a32();
            new Vec4(1, 2, 3, 4).hashInto(h1);
            const hash1 = h1.digest();

            const h2 = new Fnv1a32();
            new Vec4(5, 6, 7, 8).hashInto(h2);
            const hash2 = h2.digest();

            expect(hash1).not.toBe(hash2);
        });

        test('hashInto is consistent with getHashCode', () => {
            const v = new Vec4(1.5, 2.5, 3.5, 4.5);

            const hasher = new Fnv1a32();
            v.hashInto(hasher);
            const hashFromInto = hasher.digest();

            const hashFromMethod = v.getHashCode();

            expect(hashFromInto).toBe(hashFromMethod);
        });

        test('hashInto with equal vectors produces same hash', () => {
            const h1 = new Fnv1a32();
            new Vec4(7, 8, 9, 10).hashInto(h1);
            const hash1 = h1.digest();

            const h2 = new Fnv1a32();
            new Vec4(7, 8, 9, 10).hashInto(h2);
            const hash2 = h2.digest();

            expect(hash1).toBe(hash2);
        });
    });

    // ─── Additional Instance Method Parity ────────────────────────────────
    describe('Additional Instance Method Parity', () => {
        test('instance normalize matches static', () => {
            const v = new Vec4(3, 4, 0, 0);
            const staticResult = Vec4.normalize(new Vec4(3, 4, 0, 0));
            v.normalize();
            expectVecClose(v, staticResult, 1e-10);
        });

        test('instance inverse matches static', () => {
            const v = new Vec4(2, 4, 0.5, 0.25);
            const staticResult = Vec4.inverse(new Vec4(2, 4, 0.5, 0.25));
            v.inverse();
            expectVecClose(v, staticResult, 1e-10);
        });

        test('instance inverseSafe matches static', () => {
            const v = new Vec4(2, 1, 0.5, 0.25);
            const staticResult = Vec4.inverseSafe(new Vec4(2, 1, 0.5, 0.25));
            v.inverseSafe();
            expectVecClose(v, staticResult, 1e-10);
        });

        test('instance addScalar matches static', () => {
            const v = new Vec4(1, 2, 3, 4);
            const staticResult = Vec4.addScalar(new Vec4(1, 2, 3, 4), 5);
            v.addScalar(5);
            expectVecClose(v, staticResult);
        });

        test('instance subtractScalar matches static', () => {
            const v = new Vec4(5, 6, 7, 8);
            const staticResult = Vec4.subtractScalar(new Vec4(5, 6, 7, 8), 2);
            v.subtractScalar(2);
            expectVecClose(v, staticResult);
        });

        test('instance divideScalar matches static', () => {
            const v = new Vec4(6, 8, 12, 16);
            const staticResult = Vec4.divideScalar(new Vec4(6, 8, 12, 16), 2);
            v.divideScalar(2);
            expectVecClose(v, staticResult);
        });

        test('instance project matches static', () => {
            const v = new Vec4(3, 4, 0, 0);
            const onto = new Vec4(1, 0, 0, 0);
            const staticResult = Vec4.project(new Vec4(3, 4, 0, 0), onto);
            v.project(onto);
            expectVecClose(v, staticResult);
        });

        test('instance reject matches static', () => {
            const v = new Vec4(3, 4, 0, 0);
            const onto = new Vec4(1, 0, 0, 0);
            const staticResult = Vec4.reject(new Vec4(3, 4, 0, 0), onto);
            v.reject(onto);
            expectVecClose(v, staticResult);
        });

        test('instance reflect matches static', () => {
            const v = new Vec4(1, 1, 0, 0);
            const normal = new Vec4(0, 1, 0, 0);
            const staticResult = Vec4.reflect(new Vec4(1, 1, 0, 0), normal);
            v.reflect(normal);
            expectVecClose(v, staticResult);
        });
    });
});
