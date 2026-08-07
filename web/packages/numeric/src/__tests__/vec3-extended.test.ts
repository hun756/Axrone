import { describe, expect, test } from 'vitest';
import { Vec3, IVec3Like } from '../vec3';
import { Fnv1a32 } from '@axrone/hash';

const EPSILON = 1e-10;

const expectVecClose = (actual: IVec3Like, expected: IVec3Like, eps = EPSILON) => {
    expect(Math.abs(actual.x - expected.x)).toBeLessThan(eps);
    expect(Math.abs(actual.y - expected.y)).toBeLessThan(eps);
    expect(Math.abs(actual.z - expected.z)).toBeLessThan(eps);
};

describe('Vec3 - Additional Coverage', () => {
    // ─── Instance Method Variants (Rotations) ─────────────────────────────
    describe('Instance Rotation Variants', () => {
        test('instance rotateX matches static', () => {
            const v = new Vec3(0, 1, 0);
            const staticResult = Vec3.rotateX(new Vec3(0, 1, 0), Math.PI / 2);
            v.rotateX(Math.PI / 2);
            expectVecClose(v, staticResult, 1e-10);
        });

        test('instance rotateY matches static', () => {
            const v = new Vec3(1, 0, 0);
            const staticResult = Vec3.rotateY(new Vec3(1, 0, 0), Math.PI / 2);
            v.rotateY(Math.PI / 2);
            expectVecClose(v, staticResult, 1e-10);
        });

        test('instance rotateZ matches static', () => {
            const v = new Vec3(1, 0, 0);
            const staticResult = Vec3.rotateZ(new Vec3(1, 0, 0), Math.PI / 2);
            v.rotateZ(Math.PI / 2);
            expectVecClose(v, staticResult, 1e-10);
        });

        test('instance rotateAxis matches static', () => {
            const v = new Vec3(1, 0, 0);
            const axis = new Vec3(0, 0, 1);
            const staticResult = Vec3.rotateAxis(new Vec3(1, 0, 0), axis, Math.PI / 2);
            v.rotateAxis(axis, Math.PI / 2);
            expectVecClose(v, staticResult, 1e-10);
        });

        test('instance rotateX preserves length', () => {
            const v = new Vec3(1, 2, 3);
            const originalLen = v.length();
            v.rotateX(1.23);
            expect(Math.abs(v.length() - originalLen)).toBeLessThan(1e-10);
        });

        test('instance rotateY preserves length', () => {
            const v = new Vec3(1, 2, 3);
            const originalLen = v.length();
            v.rotateY(0.77);
            expect(Math.abs(v.length() - originalLen)).toBeLessThan(1e-10);
        });

        test('instance rotateZ preserves length', () => {
            const v = new Vec3(1, 2, 3);
            const originalLen = v.length();
            v.rotateZ(2.34);
            expect(Math.abs(v.length() - originalLen)).toBeLessThan(1e-10);
        });

        test('instance rotateAxis preserves length', () => {
            const v = new Vec3(3, 4, 5);
            const axis = Vec3.normalize(new Vec3(1, 1, 1));
            const originalLen = v.length();
            v.rotateAxis(axis, 1.5);
            expect(Math.abs(v.length() - originalLen)).toBeLessThan(1e-10);
        });

        test('instance rotateX returns this (mutation)', () => {
            const v = new Vec3(0, 1, 0);
            const result = v.rotateX(Math.PI / 4);
            expect(result).toBe(v);
        });

        test('instance rotateY returns this (mutation)', () => {
            const v = new Vec3(1, 0, 0);
            const result = v.rotateY(Math.PI / 4);
            expect(result).toBe(v);
        });

        test('instance rotateZ returns this (mutation)', () => {
            const v = new Vec3(1, 0, 0);
            const result = v.rotateZ(Math.PI / 4);
            expect(result).toBe(v);
        });

        test('instance rotateAxis returns this (mutation)', () => {
            const v = new Vec3(1, 0, 0);
            const result = v.rotateAxis(new Vec3(0, 0, 1), Math.PI / 4);
            expect(result).toBe(v);
        });

        test('full rotation (2π) returns to original via instance methods', () => {
            const original = new Vec3(1, 2, 3);
            const v = original.clone();
            v.rotateX(2 * Math.PI);
            expectVecClose(v, original, 1e-10);

            v.rotateY(2 * Math.PI);
            expectVecClose(v, original, 1e-10);

            v.rotateZ(2 * Math.PI);
            expectVecClose(v, original, 1e-10);
        });
    });

    // ─── Instance Inverse / InverseSafe ───────────────────────────────────
    describe('Instance Inverse / InverseSafe', () => {
        test('instance inverse matches static', () => {
            const v = new Vec3(2, 4, 5);
            const staticResult = Vec3.inverse(new Vec3(2, 4, 5));
            v.inverse();
            expectVecClose(v, staticResult, 1e-10);
        });

        test('instance inverse returns this', () => {
            const v = new Vec3(2, 4, 5);
            const result = v.inverse();
            expect(result).toBe(v);
        });

        test('instance inverseSafe matches static', () => {
            const v = new Vec3(2, 4, 5);
            const staticResult = Vec3.inverseSafe(new Vec3(2, 4, 5));
            v.inverseSafe();
            expectVecClose(v, staticResult, 1e-10);
        });

        test('instance inverseSafe returns this', () => {
            const v = new Vec3(2, 4, 5);
            const result = v.inverseSafe();
            expect(result).toBe(v);
        });

        test('instance inverseSafe uses default for zero components', () => {
            const v = new Vec3(0, 4, 5);
            // Instance inverseSafe does NOT throw; it uses defaultValue for zero components
            v.inverseSafe(99);
            expect(Math.abs(v.x - 99)).toBeLessThan(EPSILON);
            expect(Math.abs(v.y - 0.25)).toBeLessThan(EPSILON);
            expect(Math.abs(v.z - 0.2)).toBeLessThan(EPSILON);
        });

        test('static inverseSafe throws on any zero component', () => {
            // Static inverseSafe throws if ANY component is zero
            expect(() => Vec3.inverseSafe({ x: 0, y: 4, z: 5 })).toThrow('zero or near-zero');
        });
    });

    // ─── hashInto Integration ─────────────────────────────────────────────
    describe('hashInto Integration', () => {
        test('hashInto integrates with Fnv1a32', () => {
            const hasher = new Fnv1a32();
            const v = new Vec3(1, 2, 3);
            v.hashInto(hasher);
            const hash = hasher.digest();
            expect(typeof hash).toBe('number');
            expect(Number.isFinite(hash)).toBe(true);
        });

        test('hashInto produces different hashes for different vectors', () => {
            const h1 = new Fnv1a32();
            new Vec3(1, 2, 3).hashInto(h1);
            const hash1 = h1.digest();

            const h2 = new Fnv1a32();
            new Vec3(4, 5, 6).hashInto(h2);
            const hash2 = h2.digest();

            expect(hash1).not.toBe(hash2);
        });

        test('hashInto is consistent with getHashCode', () => {
            const v = new Vec3(1.5, 2.5, 3.5);

            // hashInto using Fnv1a32 should produce same result as getHashCode
            // since getHashCode uses the same approach internally
            const hasher = new Fnv1a32();
            v.hashInto(hasher);
            const hashFromInto = hasher.digest();

            const hashFromMethod = v.getHashCode();

            expect(hashFromInto).toBe(hashFromMethod);
        });

        test('hashInto with equal vectors produces same hash', () => {
            const h1 = new Fnv1a32();
            new Vec3(7, 8, 9).hashInto(h1);
            const hash1 = h1.digest();

            const h2 = new Fnv1a32();
            new Vec3(7, 8, 9).hashInto(h2);
            const hash2 = h2.digest();

            expect(hash1).toBe(hash2);
        });
    });

    // ─── Additional Instance Method Variants ──────────────────────────────
    describe('Additional Instance Method Parity', () => {
        test('instance normalize matches static', () => {
            const v = new Vec3(3, 4, 0);
            const staticResult = Vec3.normalize(new Vec3(3, 4, 0));
            v.normalize();
            expectVecClose(v, staticResult, 1e-10);
        });

        test('instance distance matches static', () => {
            const a = new Vec3(1, 2, 3);
            const b = new Vec3(4, 5, 6);
            const staticDist = Vec3.distance(a, b);
            expect(Math.abs(a.distance(b) - staticDist)).toBeLessThan(EPSILON);
        });

        test('instance dot matches static', () => {
            const a = new Vec3(1, 2, 3);
            const b = new Vec3(4, 5, 6);
            expect(a.dot(b)).toBe(Vec3.dot(a, b));
        });

        test('instance cross matches static', () => {
            const a = new Vec3(1, 0, 0);
            const b = new Vec3(0, 1, 0);
            const staticResult = Vec3.cross(a, b);
            const instanceResult = a.cross(b);
            expectVecClose(instanceResult, staticResult);
        });

        test('instance angleBetween matches static', () => {
            const a = new Vec3(1, 0, 0);
            const b = new Vec3(0, 1, 0);
            expect(Math.abs(a.angleBetween(b) - Vec3.angleBetween(a, b))).toBeLessThan(EPSILON);
        });
    });
});
