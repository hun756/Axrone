import { describe, it, expect } from 'vitest';
import { RayPrimitiveIntersector2D, RayPrimitiveIntersector3D } from '@axrone/physics';
import type { IVec2Like, IVec3Like } from '@axrone/numeric';

const v2 = (x: number, y: number): IVec2Like => ({ x, y });
const v3 = (x: number, y: number, z: number): IVec3Like => ({ x, y, z });

describe('RayPrimitiveIntersector2D', () => {
    describe('intersectCircle', () => {
        it('hits a circle and reports the correct distance', () => {
            const r = RayPrimitiveIntersector2D.intersectCircle(v2(-5, 0), v2(1, 0), v2(5, 0), 1, 100);
            expect(r.hit).toBe(true);
            expect(r.distance).toBeCloseTo(9, 5);
            expect(r.fraction).toBeCloseTo(9 / 100, 5);
        });

        it('misses a circle that is off the ray line', () => {
            const r = RayPrimitiveIntersector2D.intersectCircle(v2(-5, 5), v2(1, 0), v2(5, 0), 1, 100);
            expect(r.hit).toBe(false);
        });

        it('respects max distance', () => {
            const r = RayPrimitiveIntersector2D.intersectCircle(v2(-5, 0), v2(1, 0), v2(50, 0), 1, 10);
            expect(r.hit).toBe(false);
        });
    });

    describe('intersectBox', () => {
        it('hits an axis-aligned box on its near face', () => {
            const r = RayPrimitiveIntersector2D.intersectBox(v2(-5, 0), v2(1, 0), v2(5, 0), v2(1, 1), 0, 100);
            expect(r.hit).toBe(true);
            expect(r.distance).toBeCloseTo(9, 5);
        });

        it('misses an axis-aligned box that is off the ray line', () => {
            const r = RayPrimitiveIntersector2D.intersectBox(v2(-5, 5), v2(1, 0), v2(5, 0), v2(1, 1), 0, 100);
            expect(r.hit).toBe(false);
        });

        it('hits a rotated box and returns a positive finite distance', () => {
            const r = RayPrimitiveIntersector2D.intersectBox(
                v2(-5, -5),
                v2(0.894427, 0.447214),
                v2(5, 0),
                v2(1, 1),
                Math.PI / 4,
                100
            );
            expect(r.hit).toBe(true);
            expect(r.distance).toBeGreaterThan(0);
            expect(r.distance).toBeLessThanOrEqual(100);
        });
    });

    describe('intersectSegment', () => {
        it('hits a segment crossing the ray', () => {
            const r = RayPrimitiveIntersector2D.intersectSegment(v2(-5, 0), v2(1, 0), v2(4, -1), v2(4, 1), 100);
            expect(r.hit).toBe(true);
            expect(r.distance).toBeCloseTo(9, 4);
        });

        it('misses a segment that is off the ray line', () => {
            const r = RayPrimitiveIntersector2D.intersectSegment(v2(-5, 5), v2(1, 0), v2(4, -1), v2(4, 1), 100);
            expect(r.hit).toBe(false);
        });
    });

    describe('intersectCapsule', () => {
        it('hits a capsule (swept segment + radius)', () => {
            const r = RayPrimitiveIntersector2D.intersectCapsule(v2(-5, 0), v2(1, 0), v2(4, -1), v2(4, 1), 1, 100);
            expect(r.hit).toBe(true);
            expect(r.distance).toBeCloseTo(8, 3);
        });

        it('misses a capsule that is off the ray line', () => {
            const r = RayPrimitiveIntersector2D.intersectCapsule(v2(-5, 5), v2(1, 0), v2(4, -1), v2(4, 1), 1, 100);
            expect(r.hit).toBe(false);
        });
    });

    describe('intersectPolygon', () => {
        const square: IVec2Like[] = [v2(4, -1), v2(6, -1), v2(6, 1), v2(4, 1)];

        it('hits a polygon', () => {
            const r = RayPrimitiveIntersector2D.intersectPolygon(v2(-5, 0), v2(1, 0), square, 100);
            expect(r.hit).toBe(true);
            expect(r.distance).toBeCloseTo(9, 4);
        });

        it('misses a polygon that is off the ray line', () => {
            const r = RayPrimitiveIntersector2D.intersectPolygon(v2(-5, 5), v2(1, 0), square, 100);
            expect(r.hit).toBe(false);
        });
    });

    describe('intersectAABB', () => {
        it('detects AABB overlap and computes the entry t', () => {
            const out = { tMin: 0, tMax: 0 };
            const aabb = { min: v2(4, 0), max: v2(6, 2) };
            const hit = RayPrimitiveIntersector2D.intersectAABB(v2(-5, 0), v2(1, 0), aabb, 100, out);
            expect(hit).toBe(true);
            expect(out.tMin).toBeCloseTo(9, 5);
        });

        it('misses a non-overlapping AABB', () => {
            const out = { tMin: 0, tMax: 0 };
            const aabb = { min: v2(4, 5), max: v2(6, 7) };
            const hit = RayPrimitiveIntersector2D.intersectAABB(v2(-5, 0), v2(1, 0), aabb, 100, out);
            expect(hit).toBe(false);
        });
    });
});

describe('RayPrimitiveIntersector3D', () => {
    describe('intersectSphere', () => {
        it('hits a sphere and reports the correct distance', () => {
            const r = RayPrimitiveIntersector3D.intersectSphere(v3(-5, 0, 0), v3(1, 0, 0), v3(5, 0, 0), 1, 100);
            expect(r.hit).toBe(true);
            expect(r.distance).toBeCloseTo(9, 5);
        });

        it('misses a sphere that is off the ray line', () => {
            const r = RayPrimitiveIntersector3D.intersectSphere(v3(-5, 5, 0), v3(1, 0, 0), v3(5, 0, 0), 1, 100);
            expect(r.hit).toBe(false);
        });
    });

    describe('intersectBox', () => {
        it('hits an axis-aligned 3D box on its near face', () => {
            const r = RayPrimitiveIntersector3D.intersectBox(v3(-5, 0, 0), v3(1, 0, 0), v3(5, 0, 0), v3(1, 1, 1), 100);
            expect(r.hit).toBe(true);
            expect(r.distance).toBeCloseTo(9, 5);
        });
    });

    describe('intersectCapsule', () => {
        it('hits a 3D capsule', () => {
            const r = RayPrimitiveIntersector3D.intersectCapsule(v3(-5, 0, 0), v3(1, 0, 0), v3(4, -1, 0), v3(4, 1, 0), 1, 100);
            expect(r.hit).toBe(true);
            expect(r.distance).toBeCloseTo(8, 3);
        });
    });

    describe('intersectTriangle', () => {
        it('hits a triangle (Moller-Trumbore)', () => {
            const r = RayPrimitiveIntersector3D.intersectTriangle(
                v3(-5, 0, 1),
                v3(1, 0, 0),
                v3(4, -1, 1),
                v3(6, -1, 1),
                v3(5, 1, 1),
                100,
                false
            );
            expect(r.hit).toBe(true);
            expect(r.distance).toBeCloseTo(9, 3);
        });
    });

    describe('intersectCylinder', () => {
        it('hits a cylinder along its lateral surface', () => {
            const r = RayPrimitiveIntersector3D.intersectCylinder(
                v3(-5, 0, 1),
                v3(1, 0, 0),
                v3(5, 0, 0),
                v3(0, 0, 1),
                1,
                2,
                100
            );
            expect(r.hit).toBe(true);
            expect(r.distance).toBeCloseTo(9, 3);
        });
    });

    describe('intersectPlane', () => {
        it('hits a plane', () => {
            const r = RayPrimitiveIntersector3D.intersectPlane(v3(0, -5, 0), v3(0, 1, 0), v3(0, 1, 0), -2, 100);
            expect(r.hit).toBe(true);
            expect(r.distance).toBeCloseTo(7, 5);
        });
    });

    describe('intersectAABB', () => {
        it('detects 3D AABB overlap and computes the entry t', () => {
            const out = { tMin: 0, tMax: 0 };
            const aabb = { min: v3(4, 0, 0), max: v3(6, 2, 2) };
            const hit = RayPrimitiveIntersector3D.intersectAABB(v3(-5, 0, 1), v3(1, 0, 0), aabb, 100, out);
            expect(hit).toBe(true);
            expect(out.tMin).toBeCloseTo(9, 5);
        });
    });
});
