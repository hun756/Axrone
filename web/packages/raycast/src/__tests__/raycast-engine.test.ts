import { describe, it, expect } from 'vitest';
import {
    createRaycastSystem2D,
    createRaycastSystem3D,
    RaycastLayer,
} from '../index';
import { ShapeType } from '@axrone/physics-core';
import type { BodyId, ShapeId } from '@axrone/physics-core';
import type { LayerMask } from '../index';
import type { IVec2Like, IVec3Like } from '@axrone/numeric';

const v2 = (x: number, y: number): IVec2Like => ({ x, y });
const v3 = (x: number, y: number, z: number): IVec3Like => ({ x, y, z });

const ALL: LayerMask = RaycastLayer.All as LayerMask;
const STATIC: LayerMask = RaycastLayer.Static as LayerMask;
const DEFAULT: LayerMask = RaycastLayer.Default as LayerMask;
const bid = (n: number) => n as BodyId;
const sid = (n: number) => n as ShapeId;

describe('RaycastEngine2D — dispatch correctness (regression for broken hit pipeline)', () => {
    it('returns a hit for a Circle along the ray', () => {
        const sys = createRaycastSystem2D();
        sys.registerShape(bid(1), sid(10), ALL, ShapeType.Circle, { center: { x: 5, y: 0 }, radius: 1 });

        const hit = sys.raycast(v2(-5, 0), v2(1, 0), 100, ALL);

        expect(hit).not.toBeNull();
        expect(hit!.distance).toBeCloseTo(9, 5);
        expect(hit!.normal.x).toBeCloseTo(-1, 5);
        expect(hit!.shapeId).toBe(sid(10));
    });

    it('returns a hit for a Box on its near face with a correct normal', () => {
        const sys = createRaycastSystem2D();
        sys.registerShape(bid(1), sid(11), ALL, ShapeType.Box, {
            center: { x: 5, y: 0 },
            halfWidth: 1,
            halfHeight: 1,
            rotation: 0,
        });

        const hit = sys.raycast(v2(-5, 0), v2(1, 0), 100, ALL);

        expect(hit).not.toBeNull();
        expect(hit!.distance).toBeCloseTo(9, 5);
        expect(hit!.normal.x).toBeCloseTo(-1, 5);
    });

    it('returns a hit for a Capsule', () => {
        const sys = createRaycastSystem2D();
        sys.registerShape(bid(1), sid(12), ALL, ShapeType.Capsule, {
            p1: { x: 4, y: -1 },
            p2: { x: 4, y: 1 },
            radius: 1,
        });

        const hit = sys.raycast(v2(-5, 0), v2(1, 0), 100, ALL);

        expect(hit).not.toBeNull();
        expect(hit!.distance).toBeCloseTo(9, 3);
    });

    it('returns a hit for a Polygon', () => {
        const sys = createRaycastSystem2D();
        sys.registerShape(bid(1), sid(13), ALL, ShapeType.Polygon, {
            vertices: [v2(4, -1), v2(6, -1), v2(6, 1), v2(4, 1)],
        });

        const hit = sys.raycast(v2(-5, 0), v2(1, 0), 100, ALL);

        expect(hit).not.toBeNull();
        expect(hit!.distance).toBeCloseTo(9, 4);
    });

    it('respects layer masks (only hits shapes on the queried layer)', () => {
        const sys = createRaycastSystem2D();
        sys.registerShape(bid(1), sid(10), STATIC, ShapeType.Circle, { center: { x: 5, y: 0 }, radius: 1 });

        expect(sys.raycast(v2(-5, 0), v2(1, 0), 100, DEFAULT)).toBeNull();
        const hit = sys.raycast(v2(-5, 0), v2(1, 0), 100, STATIC);
        expect(hit).not.toBeNull();
    });

    it('raycastAll returns hits sorted by ascending distance', () => {
        const sys = createRaycastSystem2D();
        sys.registerShape(bid(1), sid(10), ALL, ShapeType.Circle, { center: { x: 5, y: 0 }, radius: 1 });
        sys.registerShape(bid(2), sid(11), ALL, ShapeType.Circle, { center: { x: 20, y: 0 }, radius: 1 });

        const hits = sys.raycastAll(v2(-5, 0), v2(1, 0), 100, ALL, 8);

        expect(hits.length).toBe(2);
        expect(hits[0].distance).toBeLessThan(hits[1].distance);
    });
});

describe('RaycastEngine3D — dispatch correctness', () => {
    it('returns a hit for a Sphere with a correct normal', () => {
        const sys = createRaycastSystem3D();
        sys.registerShape(bid(1), sid(10), ALL, ShapeType.Sphere, { center: { x: 5, y: 0, z: 0 }, radius: 1 });

        const hit = sys.raycast(v3(-5, 0, 0), v3(1, 0, 0), 100, ALL);

        expect(hit).not.toBeNull();
        expect(hit!.distance).toBeCloseTo(9, 5);
        expect(hit!.normal.x).toBeCloseTo(-1, 5);
        expect(hit!.shapeId).toBe(sid(10));
    });

    it('returns a hit for a 3D Box on its near face', () => {
        const sys = createRaycastSystem3D();
        sys.registerShape(bid(1), sid(11), ALL, ShapeType.Box, {
            center: { x: 5, y: 0, z: 0 },
            extents: { x: 1, y: 1, z: 1 },
        });

        const hit = sys.raycast(v3(-5, 0, 0), v3(1, 0, 0), 100, ALL);

        expect(hit).not.toBeNull();
        expect(hit!.distance).toBeCloseTo(9, 5);
    });

    it('respects layer masks in 3D', () => {
        const sys = createRaycastSystem3D();
        sys.registerShape(bid(1), sid(10), STATIC, ShapeType.Sphere, { center: { x: 5, y: 0, z: 0 }, radius: 1 });

        expect(sys.raycast(v3(-5, 0, 0), v3(1, 0, 0), 100, DEFAULT)).toBeNull();
        expect(sys.raycast(v3(-5, 0, 0), v3(1, 0, 0), 100, STATIC)).not.toBeNull();
    });
});
