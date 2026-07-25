import { describe, it, expect } from 'vitest';
import { GJK3D, supportFromVertices, type IVec3 } from './core/gjk3d.ts';

const sphere = (cx: number, cy: number, cz: number, r: number) => (dir: IVec3): IVec3 => {
    const len = Math.sqrt(dir.x * dir.x + dir.y * dir.y + dir.z * dir.z);
    const inv = len > 1e-6 ? r / len : 0;
    return { x: cx + dir.x * inv, y: cy + dir.y * inv, z: cz + dir.z * inv };
};

describe('GJK3D', () => {
    it('detects overlapping spheres with correct depth', () => {
        const r = GJK3D.intersect(sphere(0, 0, 0, 1), sphere(1.5, 0, 0, 1));
        expect(r.hit).toBe(true);
        expect(r.depth).toBeCloseTo(0.5, 2);
    });

    it('reports separated spheres as non-colliding', () => {
        const r = GJK3D.intersect(sphere(0, 0, 0, 1), sphere(4, 0, 0, 1));
        expect(r.hit).toBe(false);
    });

    it('detects overlapping axis-aligned boxes', () => {
        const a: IVec3[] = [
            { x: -1, y: -1, z: -1 }, { x: 1, y: -1, z: -1 }, { x: 1, y: 1, z: -1 }, { x: -1, y: 1, z: -1 },
            { x: -1, y: -1, z: 1 }, { x: 1, y: -1, z: 1 }, { x: 1, y: 1, z: 1 }, { x: -1, y: 1, z: 1 },
        ];
        const b = a.map((v) => ({ x: v.x + 1.5, y: v.y, z: v.z }));
        const r = GJK3D.intersect(supportFromVertices(a), supportFromVertices(b));
        expect(r.hit).toBe(true);
        expect(r.depth).toBeCloseTo(0.5, 2);
    });

    it('reports just-touching boxes as non-colliding', () => {
        const a: IVec3[] = [
            { x: -1, y: -1, z: -1 }, { x: 1, y: -1, z: -1 }, { x: 1, y: 1, z: -1 }, { x: -1, y: 1, z: -1 },
            { x: -1, y: -1, z: 1 }, { x: 1, y: -1, z: 1 }, { x: 1, y: 1, z: 1 }, { x: -1, y: 1, z: 1 },
        ];
        const c = a.map((v) => ({ x: v.x + 2.0, y: v.y, z: v.z }));
        const r = GJK3D.intersect(supportFromVertices(a), supportFromVertices(c));
        expect(r.hit).toBe(false);
    });
});
