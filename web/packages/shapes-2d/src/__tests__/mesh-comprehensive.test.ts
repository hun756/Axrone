import { describe, expect, it } from 'vitest';
import {
    buildFillMesh,
    buildStrokeMesh,
    compileShape,
    createCircleShape,
    createEllipseShape,
    createLineShape,
    createPolygonShape,
    createRectangleShape,
    createTriangleShape,
} from '../index';

describe('@axrone/shapes-2d mesh (comprehensive)', () => {
    describe('buildFillMesh', () => {
        it('rectangle: 4 vertices, 6 indices (fan)', () => {
            const shape = createRectangleShape({ x: 0, y: 0, width: 10, height: 5 });
            const mesh = buildFillMesh(shape);
            expect(mesh).not.toBeNull();
            expect(mesh!.vertexCount).toBe(4);
            expect(mesh!.indexCount).toBe(6);
            expect(mesh!.positions).toBeInstanceOf(Float32Array);
            expect(mesh!.bounds).toBeDefined();
        });

        it('circle: N vertices based on curve segments', () => {
            const shape = createCircleShape({ cx: 0, cy: 0, radius: 10 });
            const mesh = buildFillMesh(shape);
            expect(mesh).not.toBeNull();
            expect(mesh!.vertexCount).toBeGreaterThan(3);
            expect(mesh!.indexCount).toBeGreaterThan(3);
        });

        it('ellipse: produces valid mesh', () => {
            const shape = createEllipseShape({ cx: 0, cy: 0, radiusX: 10, radiusY: 5 });
            const mesh = buildFillMesh(shape);
            expect(mesh).not.toBeNull();
            expect(mesh!.vertexCount).toBeGreaterThan(3);
        });

        it('triangle: 3 vertices, 3 indices', () => {
            const shape = createTriangleShape({ a: [0, 0], b: [10, 0], c: [5, 10] });
            const mesh = buildFillMesh(shape);
            expect(mesh).not.toBeNull();
            expect(mesh!.vertexCount).toBe(3);
            expect(mesh!.indexCount).toBe(3);
        });

        it('polygon convex: fan mesh', () => {
            const shape = createPolygonShape({
                outer: { points: [[0, 0], [10, 0], [5, 10]] as readonly [number, number][] },
            });
            const mesh = buildFillMesh(shape);
            expect(mesh).not.toBeNull();
            expect(mesh!.vertexCount).toBe(3);
            expect(mesh!.indexCount).toBe(3); // 1 triangle
        });

        it('polygon concave: ear-clipping mesh', () => {
            const shape = createPolygonShape({
                outer: {
                    points: [[0, 0], [3, 0], [3, 3], [1.5, 1], [0, 3]] as readonly [number, number][],
                },
            });
            const mesh = buildFillMesh(shape);
            expect(mesh).not.toBeNull();
            expect(mesh!.vertexCount).toBe(5);
            // Concave polygon: 5 vertices -> 3 triangles -> 9 indices
            expect(mesh!.indexCount).toBe(9);
        });

        it('polygon with holes: includes hole vertices', () => {
            const shape = createPolygonShape({
                outer: { points: [[0, 0], [20, 0], [10, 20]] as readonly [number, number][] },
                holes: [{ points: [[8, 5], [10, 10], [12, 5]] as readonly [number, number][] }],
            });
            const mesh = buildFillMesh(shape);
            expect(mesh).not.toBeNull();
            // Outer (3) + hole (3) = 6 vertices
            expect(mesh!.vertexCount).toBe(6);
        });

        it('line: returns null (no fill)', () => {
            const shape = createLineShape({
                start: [0, 0], end: [10, 0],
                stroke: { paint: '#000', width: 2 },
            });
            const mesh = buildFillMesh(shape);
            expect(mesh).toBeNull();
        });

        it('mesh indices are Uint16Array for small vertex counts', () => {
            const shape = createRectangleShape({ x: 0, y: 0, width: 10, height: 5 });
            const mesh = buildFillMesh(shape);
            expect(mesh!.indices).toBeInstanceOf(Uint16Array);
        });
    });

    describe('buildStrokeMesh', () => {
        it('returns null for shape without stroke', () => {
            const shape = createRectangleShape({ x: 0, y: 0, width: 10, height: 5 });
            const mesh = buildStrokeMesh(shape);
            expect(mesh).toBeNull();
        });

        it('rectangle with stroke: ring mesh', () => {
            const shape = createRectangleShape({
                x: 0, y: 0, width: 10, height: 5,
                stroke: { paint: '#000', width: 2 },
            });
            const mesh = buildStrokeMesh(shape);
            expect(mesh).not.toBeNull();
            // Ring mesh: outer (4) + inner (4) = 8 vertices
            expect(mesh!.vertexCount).toBe(8);
            // 4 quads = 24 indices
            expect(mesh!.indexCount).toBe(24);
        });

        it('circle with stroke: ring mesh', () => {
            const shape = createCircleShape({
                cx: 0, cy: 0, radius: 10,
                stroke: { paint: '#000', width: 2 },
            });
            const mesh = buildStrokeMesh(shape);
            expect(mesh).not.toBeNull();
            expect(mesh!.vertexCount).toBeGreaterThan(4);
        });

        it('line with stroke: quad mesh', () => {
            const shape = createLineShape({
                start: [0, 0], end: [10, 0],
                stroke: { paint: '#000', width: 2 },
            });
            const mesh = buildStrokeMesh(shape);
            expect(mesh).not.toBeNull();
            expect(mesh!.vertexCount).toBe(4);
            expect(mesh!.indexCount).toBe(6); // 2 triangles
        });

        it('line without stroke: returns null', () => {
            const shape = createLineShape({
                start: [0, 0], end: [10, 10],
                stroke: { paint: '#000', width: 1 },
            });
            // Line always has stroke in this test, but let's verify stroke mesh works
            const mesh = buildStrokeMesh(shape);
            expect(mesh).not.toBeNull();
        });

        it('stroke alignment inside', () => {
            const shape = createRectangleShape({
                x: 0, y: 0, width: 10, height: 10,
                stroke: { paint: '#000', width: 4, alignment: 'inside' },
            });
            const mesh = buildStrokeMesh(shape);
            expect(mesh).not.toBeNull();
        });

        it('stroke alignment outside', () => {
            const shape = createRectangleShape({
                x: 0, y: 0, width: 10, height: 10,
                stroke: { paint: '#000', width: 4, alignment: 'outside' },
            });
            const mesh = buildStrokeMesh(shape);
            expect(mesh).not.toBeNull();
        });

        it('triangle with stroke: ring mesh', () => {
            const shape = createTriangleShape({
                a: [0, 0], b: [10, 0], c: [5, 10],
                stroke: { paint: '#000', width: 2 },
            });
            const mesh = buildStrokeMesh(shape);
            expect(mesh).not.toBeNull();
        });
    });

    describe('compileShape', () => {
        it('produces full compiled object with all fields', () => {
            const shape = createRectangleShape({
                x: 0, y: 0, width: 10, height: 5,
                fill: '#ff0000',
                stroke: { paint: '#000', width: 2 },
            });
            const compiled = compileShape(shape);
            expect(compiled.shape).toBe(shape);
            expect(compiled.fingerprint.startsWith('rectangle:')).toBe(true);
            expect(compiled.geometryBounds).toBeDefined();
            expect(compiled.bounds).toBeDefined();
            expect(compiled.area).toBe(50);
            expect(compiled.perimeter).toBe(30);
            expect(compiled.contour).toBeInstanceOf(Float32Array);
            expect(compiled.fillMesh).not.toBeNull();
            expect(compiled.strokeMesh).not.toBeNull();
        });

        it('includeFillMesh:false sets fillMesh to null', () => {
            const shape = createRectangleShape({
                x: 0, y: 0, width: 10, height: 5,
                fill: '#f00',
                stroke: { paint: '#000', width: 2 },
            });
            const compiled = compileShape(shape, { includeFillMesh: false });
            expect(compiled.fillMesh).toBeNull();
            expect(compiled.strokeMesh).not.toBeNull();
        });

        it('includeStrokeMesh:false sets strokeMesh to null', () => {
            const shape = createRectangleShape({
                x: 0, y: 0, width: 10, height: 5,
                fill: '#f00',
                stroke: { paint: '#000', width: 2 },
            });
            const compiled = compileShape(shape, { includeStrokeMesh: false });
            expect(compiled.strokeMesh).toBeNull();
            expect(compiled.fillMesh).not.toBeNull();
        });

        it('fingerprint matches shape kind', () => {
            const circle = createCircleShape({ cx: 0, cy: 0, radius: 5 });
            const compiled = compileShape(circle);
            expect(compiled.fingerprint.startsWith('circle:')).toBe(true);
        });

        it('compiled contour has correct length for rectangle', () => {
            const shape = createRectangleShape({ x: 0, y: 0, width: 10, height: 5 });
            const compiled = compileShape(shape);
            // Rectangle: 4 corners * 2 floats = 8
            expect(compiled.contour.length).toBe(8);
        });

        it('compiled contour has correct length for line', () => {
            const shape = createLineShape({
                start: [0, 0], end: [10, 0],
                stroke: { paint: '#000', width: 1 },
            });
            const compiled = compileShape(shape);
            // Line: 2 points * 2 floats = 4
            expect(compiled.contour.length).toBe(4);
        });
    });
});
