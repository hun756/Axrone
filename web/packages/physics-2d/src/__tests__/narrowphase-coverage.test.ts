import { describe, it, expect, beforeEach } from 'vitest';
import { Narrowphase2D, ShapeManager2D, ShapeType } from '@axrone/physics-2d';

function makeManifold(): any {
    return {
        pointCount: 0,
        normal: { x: 0, y: 0 },
        points: [
            { id: 0, localPointA: { x: 0, y: 0 }, localPointB: { x: 0, y: 0 }, normalImpulse: 0, tangentImpulse: 0, separation: 0 },
            { id: 1, localPointA: { x: 0, y: 0 }, localPointB: { x: 0, y: 0 }, normalImpulse: 0, tangentImpulse: 0, separation: 0 },
        ],
    };
}

const ctx = (posA: { x: number; y: number }, posB: { x: number; y: number }, rotA = 0, rotB = 0) => ({
    bodyIdA: 1,
    bodyIdB: 2,
    transformA: { position: posA, rotation: rotA },
    transformB: { position: posB, rotation: rotB },
});

describe('Narrowphase2D — collision pairs & rotation', () => {
    let narrowphase: Narrowphase2D;
    let shapes: ShapeManager2D;

    beforeEach(() => {
        narrowphase = new Narrowphase2D();
        shapes = new ShapeManager2D(128);
    });

    it('detects capsule vs polygon (previously untested pair)', () => {
        const cap = shapes.createCapsule(1 as any, { radius: 0.5, length: 2 });
        const poly = shapes.createPolygon(2 as any, {
            vertices: [
                { x: -1, y: -1 },
                { x: 1, y: -1 },
                { x: 1, y: 1 },
                { x: -1, y: 1 },
            ],
        });

        const m = makeManifold();
        narrowphase.collide(cap, poly, ShapeType.Capsule, ShapeType.Polygon, shapes, ctx({ x: 0, y: 0 }, { x: 1.5, y: 0 }), m);

        expect(m.pointCount).toBeGreaterThan(0);
    });

    it('detects box vs capsule (previously untested pair)', () => {
        const box = shapes.createBox(1 as any, { halfWidth: 1, halfHeight: 1 });
        const cap = shapes.createCapsule(2 as any, { radius: 0.5, length: 2 });

        const m = makeManifold();
        narrowphase.collide(box, cap, ShapeType.Box, ShapeType.Capsule, shapes, ctx({ x: 0, y: 0 }, { x: 1.5, y: 0 }), m);

        expect(m.pointCount).toBeGreaterThan(0);
    });

    it('separates box vs capsule when far apart', () => {
        const box = shapes.createBox(1 as any, { halfWidth: 1, halfHeight: 1 });
        const cap = shapes.createCapsule(2 as any, { radius: 0.5, length: 2 });

        const m = makeManifold();
        narrowphase.collide(box, cap, ShapeType.Box, ShapeType.Capsule, shapes, ctx({ x: 0, y: 0 }, { x: 10, y: 0 }), m);

        expect(m.pointCount).toBe(0);
    });

    it('handles a rotated box — rotation is applied through the SAT transform', () => {
        const boxA = shapes.createBox(1 as any, { halfWidth: 1, halfHeight: 1 });
        const boxB = shapes.createBox(2 as any, { halfWidth: 1, halfHeight: 1 });

        const m = makeManifold();
        narrowphase.collide(
            boxA,
            boxB,
            ShapeType.Box,
            ShapeType.Box,
            shapes,
            ctx({ x: 0, y: 0 }, { x: 1.2, y: 0 }, Math.PI / 4, 0),
            m
        );

        expect(m.pointCount).toBeGreaterThan(0);
        if (m.pointCount > 0) {
            const nlen = Math.hypot(m.normal.x, m.normal.y);
            expect(nlen).toBeCloseTo(1, 5);
        }
    });
});
