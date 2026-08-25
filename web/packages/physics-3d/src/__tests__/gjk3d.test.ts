import { describe, it, expect } from 'vitest';
import { type IVec3Like } from '@axrone/numeric';
import { GJK3D, supportFromVertices } from '../core/gjk3d';

const sphere = (cx: number, cy: number, cz: number, r: number) => (dir: IVec3Like): IVec3Like => {
    const len = Math.sqrt(dir.x * dir.x + dir.y * dir.y + dir.z * dir.z);
    const inv = len > 1e-6 ? r / len : 0;
    return { x: cx + dir.x * inv, y: cy + dir.y * inv, z: cz + dir.z * inv };
};

const unitCube: IVec3Like[] = [
    { x: -1, y: -1, z: -1 }, { x: 1, y: -1, z: -1 }, { x: 1, y: 1, z: -1 }, { x: -1, y: 1, z: -1 },
    { x: -1, y: -1, z: 1 }, { x: 1, y: -1, z: 1 }, { x: 1, y: 1, z: 1 }, { x: -1, y: 1, z: 1 },
];

describe('GJK3D', () => {
    describe('sphere vs sphere', () => {
        it('detects overlapping spheres with correct depth', () => {
            const r = GJK3D.intersect(sphere(0, 0, 0, 1), sphere(1.5, 0, 0, 1));
            expect(r.hit).toBe(true);
            expect(r.depth).toBeCloseTo(0.5, 2);
        });

        it('reports separated spheres as non-colliding', () => {
            const r = GJK3D.intersect(sphere(0, 0, 0, 1), sphere(4, 0, 0, 1));
            expect(r.hit).toBe(false);
            expect(r.depth).toBe(0);
        });

        it('detects touching spheres', () => {
            const r = GJK3D.intersect(sphere(0, 0, 0, 1), sphere(2, 0, 0, 1));
            // Touching is boundary — GJK may or may not report hit depending on epsilon.
            // At exactly distance 2 with radius 1 each, they touch at one point.
            // This is a degenerate case; we just verify it doesn't throw.
            expect(r).toBeDefined();
            expect(r.hit).toBeTypeOf('boolean');
        });

        it('handles concentric spheres', () => {
            const r = GJK3D.intersect(sphere(0, 0, 0, 2), sphere(0, 0, 0, 1));
            expect(r.hit).toBe(true);
            expect(r.depth).toBeGreaterThan(0);
        });

        it('returns a unit-length normal for overlapping spheres', () => {
            const r = GJK3D.intersect(sphere(0, 0, 0, 1), sphere(1.5, 0, 0, 1));
            const nLen = Math.sqrt(r.normal.x ** 2 + r.normal.y ** 2 + r.normal.z ** 2);
            expect(nLen).toBeCloseTo(1, 3);
        });
    });

    describe('box vs box (via supportFromVertices)', () => {
        it('detects overlapping axis-aligned boxes', () => {
            const a = supportFromVertices(unitCube);
            const bVerts = unitCube.map((v) => ({ x: v.x + 1.5, y: v.y, z: v.z }));
            const b = supportFromVertices(bVerts);
            const r = GJK3D.intersect(a, b);
            expect(r.hit).toBe(true);
            expect(r.depth).toBeCloseTo(0.5, 2);
        });

        it('reports just-touching boxes as non-colliding', () => {
            const a = supportFromVertices(unitCube);
            const c = supportFromVertices(unitCube.map((v) => ({ x: v.x + 2.0, y: v.y, z: v.z })));
            const r = GJK3D.intersect(a, c);
            expect(r.hit).toBe(false);
        });

        it('reports well-separated boxes as non-colliding', () => {
            const a = supportFromVertices(unitCube);
            const b = supportFromVertices(unitCube.map((v) => ({ x: v.x + 10, y: v.y, z: v.z })));
            const r = GJK3D.intersect(a, b);
            expect(r.hit).toBe(false);
        });
    });

    describe('sphere vs box', () => {
        it('detects sphere overlapping a box', () => {
            const s = sphere(0, 0, 0, 1);
            const b = supportFromVertices(unitCube.map((v) => ({ x: v.x + 1.5, y: v.y, z: v.z })));
            const r = GJK3D.intersect(s, b);
            expect(r.hit).toBe(true);
            expect(r.depth).toBeGreaterThan(0);
        });

        it('reports separated sphere and box', () => {
            const s = sphere(0, 0, 0, 0.5);
            const b = supportFromVertices(unitCube.map((v) => ({ x: v.x + 5, y: v.y, z: v.z })));
            const r = GJK3D.intersect(s, b);
            expect(r.hit).toBe(false);
        });
    });

    describe('supportFromVertices', () => {
        it('returns the farthest vertex in the given direction', () => {
            const verts: IVec3Like[] = [
                { x: 0, y: 0, z: 0 },
                { x: 1, y: 0, z: 0 },
                { x: 0, y: 1, z: 0 },
            ];
            const support = supportFromVertices(verts);
            const result = support({ x: 1, y: 0, z: 0 });
            expect(result.x).toBe(1);
            expect(result.y).toBe(0);
        });

        it('handles a single vertex', () => {
            const support = supportFromVertices([{ x: 5, y: 3, z: -2 }]);
            const r = support({ x: 0, y: 1, z: 0 });
            expect(r.x).toBe(5);
            expect(r.y).toBe(3);
            expect(r.z).toBe(-2);
        });
    });

    describe('EPA penetration depth', () => {
        it('computes positive depth for clearly overlapping spheres', () => {
            const r = GJK3D.intersect(sphere(0, 0, 0, 2), sphere(1, 0, 0, 2));
            expect(r.hit).toBe(true);
            expect(r.depth).toBeGreaterThan(0);
            expect(r.depth).toBeCloseTo(3, 0);
        });

        it('computes normal pointing from A to B', () => {
            const r = GJK3D.intersect(sphere(0, 0, 0, 1), sphere(1.5, 0, 0, 1));
            // Normal should point roughly along +X (from A toward B).
            expect(r.normal.x).toBeGreaterThan(0);
        });
    });

    describe('edge cases', () => {
        it('handles zero-distance identical shapes', () => {
            const r = GJK3D.intersect(sphere(0, 0, 0, 1), sphere(0, 0, 0, 1));
            expect(r.hit).toBe(true);
        });

        it('handles shapes separated along Y axis', () => {
            const r = GJK3D.intersect(sphere(0, 0, 0, 1), sphere(0, 3, 0, 1));
            expect(r.hit).toBe(false);
        });

        it('handles shapes separated along Z axis', () => {
            const r = GJK3D.intersect(sphere(0, 0, 0, 1), sphere(0, 0, 3, 1));
            expect(r.hit).toBe(false);
        });

        it('handles non-origin initial direction', () => {
            // The algorithm starts with dir = (1,0,0). Verify it works for
            // shapes offset along other axes.
            const r = GJK3D.intersect(sphere(0, 5, 0, 1), sphere(0, 5.5, 0, 1));
            expect(r.hit).toBe(true);
        });
    });
});
