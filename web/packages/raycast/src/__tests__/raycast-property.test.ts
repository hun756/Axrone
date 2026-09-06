import { describe, it, expect } from 'vitest';
import { RayPrimitiveIntersector2D, RayPrimitiveIntersector3D } from '../core/raycast-primitives';
import type { IVec2Like, IVec3Like } from '@axrone/numeric';

const v2 = (x: number, y: number): IVec2Like => ({ x, y });
const v3 = (x: number, y: number, z: number): IVec3Like => ({ x, y, z });

describe('RayPrimitiveIntersector2D — property tests', () => {
    it('intersectCircle returns non-negative distance on hit', () => {
        for (let i = 0; i < 100; i++) {
            const cx = (Math.random() - 0.5) * 20;
            const cy = (Math.random() - 0.5) * 20;
            const radius = Math.random() * 5 + 0.1;
            const origin = v2(cx - radius - 5, cy);
            const direction = v2(1, 0);
            const result = RayPrimitiveIntersector2D.intersectCircle(origin, direction, v2(cx, cy), radius, 100);
            if (result.hit) {
                expect(result.distance).toBeGreaterThanOrEqual(0);
                expect(result.fraction).toBeGreaterThanOrEqual(0);
                expect(result.fraction).toBeLessThanOrEqual(1);
            }
        }
    });

    it('intersectBox returns valid hit data', () => {
        for (let i = 0; i < 100; i++) {
            const cx = (Math.random() - 0.5) * 20;
            const cy = (Math.random() - 0.5) * 20;
            const hw = Math.random() * 3 + 0.1;
            const hh = Math.random() * 3 + 0.1;
            const origin = v2(cx - hw - 5, cy);
            const direction = v2(1, 0);
            const result = RayPrimitiveIntersector2D.intersectBox(origin, direction, v2(cx, cy), v2(hw, hh), 0, 100);
            if (result.hit) {
                expect(result.distance).toBeGreaterThanOrEqual(0);
                expect(result.fraction).toBeGreaterThanOrEqual(0);
                expect(result.fraction).toBeLessThanOrEqual(1);
            }
        }
    });
});

describe('RayPrimitiveIntersector3D — property tests', () => {
    it('intersectSphere returns non-negative distance on hit', () => {
        for (let i = 0; i < 100; i++) {
            const cx = (Math.random() - 0.5) * 20;
            const cy = (Math.random() - 0.5) * 20;
            const cz = (Math.random() - 0.5) * 20;
            const radius = Math.random() * 5 + 0.1;
            const origin = v3(cx - radius - 5, cy, cz);
            const direction = v3(1, 0, 0);
            const result = RayPrimitiveIntersector3D.intersectSphere(origin, direction, v3(cx, cy, cz), radius, 100);
            if (result.hit) {
                expect(result.distance).toBeGreaterThanOrEqual(0);
                expect(result.fraction).toBeGreaterThanOrEqual(0);
                expect(result.fraction).toBeLessThanOrEqual(1);
            }
        }
    });

    it('intersectTriangle barycentric coords are valid', () => {
        const v0 = v3(0, 0, 0);
        const v1 = v3(1, 0, 0);
        const v2 = v3(0, 1, 0);
        const origin = v3(0.25, 0.25, -5);
        const direction = v3(0, 0, 1);
        const bary = { u: 0, v: 0 };
        const result = RayPrimitiveIntersector3D.intersectTriangle(origin, direction, v0, v1, v2, 100, false, bary);
        if (result.hit) {
            expect(bary.u).toBeGreaterThanOrEqual(0);
            expect(bary.v).toBeGreaterThanOrEqual(0);
            expect(bary.u + bary.v).toBeLessThanOrEqual(1);
        }
    });
});

describe('RayPrimitiveIntersector2D — negative paths', () => {
    it('returns no hit for ray going away from circle', () => {
        const result = RayPrimitiveIntersector2D.intersectCircle(v2(-5, 0), v2(-1, 0), v2(0, 0), 1, 100);
        expect(result.hit).toBe(false);
    });

    it('returns no hit for zero-radius circle offset from ray', () => {
        const result = RayPrimitiveIntersector2D.intersectCircle(v2(-5, 0), v2(1, 0), v2(0, 5), 0, 100);
        expect(result.hit).toBe(false);
    });

    it('returns no hit for parallel ray missing box', () => {
        const result = RayPrimitiveIntersector2D.intersectBox(v2(0, 10), v2(1, 0), v2(0, 0), v2(1, 1), 0, 100);
        expect(result.hit).toBe(false);
    });
});

describe('RayPrimitiveIntersector3D — negative paths', () => {
    it('returns no hit for ray going away from sphere', () => {
        const result = RayPrimitiveIntersector3D.intersectSphere(v3(-5, 0, 0), v3(-1, 0, 0), v3(0, 0, 0), 1, 100);
        expect(result.hit).toBe(false);
    });

    it('returns no hit for zero-radius sphere offset from ray', () => {
        const result = RayPrimitiveIntersector3D.intersectSphere(v3(-5, 0, 0), v3(1, 0, 0), v3(0, 5, 0), 0, 100);
        expect(result.hit).toBe(false);
    });

    it('returns no hit for parallel ray missing box', () => {
        const result = RayPrimitiveIntersector3D.intersectBox(v3(0, 10, 0), v3(1, 0, 0), v3(0, 0, 0), v3(1, 1, 1), 100);
        expect(result.hit).toBe(false);
    });
});
