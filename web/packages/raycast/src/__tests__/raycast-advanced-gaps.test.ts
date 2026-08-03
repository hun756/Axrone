import { describe, it, expect } from 'vitest';
import {
    createRaycastSystem3D,
    createShapeCaster3D,
    createMultiRaycaster3D,
    RaycastLayer,
} from '../index';
import { ShapeType } from '@axrone/physics-core';
import type { BodyId, ShapeId } from '@axrone/physics-core';
import type { LayerMask } from '../index';
import type { IVec3Like } from '@axrone/numeric';

const v3 = (x: number, y: number, z: number): IVec3Like => ({ x, y, z });
const ALL: LayerMask = RaycastLayer.All as LayerMask;
const bid = (n: number) => n as BodyId;
const sid = (n: number) => n as ShapeId;

function makeSystemWithSphere() {
    const sys = createRaycastSystem3D();
    sys.registerShape(bid(1), sid(10), ALL, ShapeType.Sphere, {
        center: { x: 5, y: 0, z: 0 },
        radius: 1,
    });
    return sys;
}

describe('ShapeCaster3D.boxCast', () => {
    it('hits a sphere target with box cast', () => {
        const sys = makeSystemWithSphere();
        const caster = createShapeCaster3D(sys);
        const hit = caster.boxCast({
            origin: v3(-5, 0, 0),
            direction: v3(1, 0, 0),
            maxDistance: 100,
            layerMask: ALL,
            extents: v3(0.5, 0.5, 0.5),
        });
        expect(hit).not.toBeNull();
    });

    it('returns null when box cast misses', () => {
        const sys = makeSystemWithSphere();
        const caster = createShapeCaster3D(sys);
        const hit = caster.boxCast({
            origin: v3(-5, 50, 0),
            direction: v3(1, 0, 0),
            maxDistance: 100,
            layerMask: ALL,
            extents: v3(0.5, 0.5, 0.5),
        });
        expect(hit).toBeNull();
    });
});

describe('ShapeCaster3D.capsuleCast', () => {
    it('hits a sphere target with capsule cast', () => {
        const sys = makeSystemWithSphere();
        const caster = createShapeCaster3D(sys);
        const hit = caster.capsuleCast({
            origin: v3(-5, 0, 0),
            direction: v3(1, 0, 0),
            maxDistance: 100,
            layerMask: ALL,
            radius: 0.5,
            height: 2,
        });
        expect(hit).not.toBeNull();
    });

    it('returns null when capsule cast misses', () => {
        const sys = makeSystemWithSphere();
        const caster = createShapeCaster3D(sys);
        const hit = caster.capsuleCast({
            origin: v3(-5, 50, 0),
            direction: v3(1, 0, 0),
            maxDistance: 100,
            layerMask: ALL,
            radius: 0.5,
            height: 2,
        });
        expect(hit).toBeNull();
    });
});

describe('MultiRaycaster3D', () => {
    describe('fanCast', () => {
        it('rayCount=1 returns only center ray result', () => {
            const sys = makeSystemWithSphere();
            const multi = createMultiRaycaster3D(sys);
            const hits = multi.fanCast(v3(-5, 0, 0), v3(1, 0, 0), 100, ALL, Math.PI / 4, 1);
            expect(hits.length).toBe(1);
        });

        it('rayCount>1 returns results for spread rays', () => {
            const sys = makeSystemWithSphere();
            const multi = createMultiRaycaster3D(sys);
            const hits = multi.fanCast(v3(-5, 0, 0), v3(1, 0, 0), 100, ALL, Math.PI / 4, 5);
            expect(hits.length).toBeGreaterThanOrEqual(1);
        });

        it('returns empty array when no rays hit', () => {
            const sys = makeSystemWithSphere();
            const multi = createMultiRaycaster3D(sys);
            const hits = multi.fanCast(v3(-5, 50, 0), v3(1, 0, 0), 100, ALL, Math.PI / 4, 3);
            expect(hits.length).toBe(0);
        });
    });

    describe('coneCast', () => {
        it('includes center ray hit when aimed at target', () => {
            const sys = makeSystemWithSphere();
            const multi = createMultiRaycaster3D(sys);
            const hits = multi.coneCast(v3(-5, 0, 0), v3(1, 0, 0), 100, ALL, Math.PI / 6, 8);
            expect(hits.length).toBeGreaterThanOrEqual(1);
        });

        it('returns empty when cone misses all targets', () => {
            const sys = makeSystemWithSphere();
            const multi = createMultiRaycaster3D(sys);
            const hits = multi.coneCast(v3(-5, 50, 0), v3(1, 0, 0), 100, ALL, Math.PI / 6, 8);
            expect(hits.length).toBe(0);
        });
    });

    describe('radialCast', () => {
        it('returns hits from omnidirectional cast around origin', () => {
            // Use a larger sphere so that the discrete angular sampling hits it
            const sys = createRaycastSystem3D();
            sys.registerShape(bid(1), sid(10), ALL, ShapeType.Sphere, {
                center: { x: 5, y: 0, z: 0 },
                radius: 2,
            });
            const multi = createMultiRaycaster3D(sys);
            const hits = multi.radialCast(v3(0, 0, 0), 100, ALL, 32);
            expect(hits.length).toBeGreaterThanOrEqual(1);
        });

        it('returns empty when no targets surround origin', () => {
            const sys = makeSystemWithSphere();
            const multi = createMultiRaycaster3D(sys);
            const hits = multi.radialCast(v3(0, 100, 0), 10, ALL, 16);
            expect(hits.length).toBe(0);
        });
    });

    describe('gridCast', () => {
        it('returns 2D grid with correct row count', () => {
            const sys = makeSystemWithSphere();
            const multi = createMultiRaycaster3D(sys);
            const grid = multi.gridCast(v3(-5, 0, 0), v3(1, 0, 0), 100, ALL, 4, 4, 3, 3);
            expect(grid.length).toBe(3);
        });

        it('each row contains hits or is empty based on ray alignment', () => {
            const sys = makeSystemWithSphere();
            const multi = createMultiRaycaster3D(sys);
            const grid = multi.gridCast(v3(-5, 0, 0), v3(1, 0, 0), 100, ALL, 4, 4, 3, 3);
            const totalHits = grid.reduce((sum, row) => sum + row.length, 0);
            expect(totalHits).toBeGreaterThanOrEqual(1);
        });

        it('returns empty grid when all rays miss', () => {
            const sys = makeSystemWithSphere();
            const multi = createMultiRaycaster3D(sys);
            const grid = multi.gridCast(v3(-5, 100, 0), v3(1, 0, 0), 100, ALL, 4, 4, 3, 3);
            const totalHits = grid.reduce((sum, row) => sum + row.length, 0);
            expect(totalHits).toBe(0);
        });
    });
});
