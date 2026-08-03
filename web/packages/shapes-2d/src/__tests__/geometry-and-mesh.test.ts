import { describe, expect, it } from 'vitest';
import {
    buildFillMesh,
    buildStrokeMesh,
    compileShape,
    containsPoint,
    createCircleShape,
    createEllipseShape,
    createLineShape,
    createPolygonShape,
    createRectangleShape,
    createTriangleShape,
    getShapeArea,
    getShapeBounds,
    getShapeCentroid,
    getShapePerimeter,
    hitTestShape,
} from '../index';

describe('@axrone/shapes-2d geometry and mesh', () => {
    it('computes geometry and hit testing for rectangles', () => {
        const shape = createRectangleShape({
            x: 10,
            y: 20,
            width: 100,
            height: 50,
            fill: '#ff0000',
            stroke: {
                paint: '#000000',
                width: 10,
            },
        });

        expect(getShapeArea(shape)).toBe(5000);
        expect(getShapePerimeter(shape)).toBe(300);
        expect(getShapeCentroid(shape)).toEqual({ x: 60, y: 45 });
        expect(containsPoint(shape, [60, 45])).toBe(true);
        expect(hitTestShape(shape, [60, 45])).toBe('fill');
        expect(hitTestShape(shape, [10, 25])).toBe('stroke');
        expect(hitTestShape(shape, [0, 0])).toBe('none');

        const bounds = getShapeBounds(shape);
        expect(bounds).toMatchObject({
            minX: 5,
            minY: 15,
            maxX: 115,
            maxY: 75,
        });
    });

    it('builds meshes and compiled snapshots', () => {
        const shape = createRectangleShape({
            x: 10,
            y: 20,
            width: 100,
            height: 50,
            fill: '#ff0000',
            stroke: {
                paint: '#000000',
                width: 10,
            },
        });

        const fillMesh = buildFillMesh(shape);
        const strokeMesh = buildStrokeMesh(shape);
        const compiled = compileShape(shape);

        expect(fillMesh?.vertexCount).toBe(4);
        expect(fillMesh?.indexCount).toBe(6);
        expect(strokeMesh?.vertexCount).toBe(8);
        expect(strokeMesh?.indexCount).toBe(24);
        expect(compiled.fingerprint.startsWith('rectangle:')).toBe(true);
        expect(compiled.fillMesh?.vertexCount).toBe(4);
        expect(compiled.strokeMesh?.vertexCount).toBe(8);
    });

    it('computes geometry for circles', () => {
        const shape = createCircleShape({ cx: 0, cy: 0, radius: 10, fill: '#ff0000' });

        expect(getShapeArea(shape)).toBeCloseTo(Math.PI * 100, 2);
        expect(getShapePerimeter(shape)).toBeCloseTo(2 * Math.PI * 10, 2);
        expect(getShapeCentroid(shape)).toEqual({ x: 0, y: 0 });
        expect(containsPoint(shape, [0, 0])).toBe(true);
        expect(containsPoint(shape, [20, 20])).toBe(false);

        const bounds = getShapeBounds(shape);
        expect(bounds.minX).toBeCloseTo(-10, 4);
        expect(bounds.maxX).toBeCloseTo(10, 4);
    });

    it('computes geometry for ellipses', () => {
        const shape = createEllipseShape({ cx: 5, cy: 5, radiusX: 10, radiusY: 5, fill: '#00ff00' });

        expect(getShapeArea(shape)).toBeCloseTo(Math.PI * 50, 2);
        expect(getShapeCentroid(shape)).toEqual({ x: 5, y: 5 });
        expect(containsPoint(shape, [5, 5])).toBe(true);
        expect(containsPoint(shape, [20, 20])).toBe(false);
    });

    it('computes geometry for triangles', () => {
        const shape = createTriangleShape({ a: [0, 0], b: [10, 0], c: [0, 10], fill: '#0000ff' });

        expect(getShapeArea(shape)).toBeCloseTo(50, 2);
        expect(getShapeCentroid(shape).x).toBeCloseTo(10 / 3, 4);
        expect(getShapeCentroid(shape).y).toBeCloseTo(10 / 3, 4);
        expect(containsPoint(shape, [2, 2])).toBe(true);
        expect(containsPoint(shape, [10, 10])).toBe(false);
    });

    it('computes geometry for lines', () => {
        const shape = createLineShape({
            start: [0, 0],
            end: [10, 0],
            stroke: { paint: '#000', width: 2 },
        });

        expect(getShapeArea(shape)).toBe(0);
        expect(getShapePerimeter(shape)).toBeCloseTo(10, 4);
        expect(containsPoint(shape, [5, 0])).toBe(false);
    });

    it('computes geometry for polygons', () => {
        const shape = createPolygonShape({
            outer: { points: [[0, 0], [10, 0], [5, 10]] as readonly [number, number][] },
            fill: '#ff00ff',
        });

        expect(getShapeArea(shape)).toBeCloseTo(50, 2);
        expect(containsPoint(shape, [5, 5])).toBe(true);
        expect(containsPoint(shape, [15, 15])).toBe(false);
    });

    it('builds fill meshes for various shapes', () => {
        const circle = createCircleShape({ cx: 0, cy: 0, radius: 10 });
        expect(buildFillMesh(circle)).not.toBeNull();

        const triangle = createTriangleShape({ a: [0, 0], b: [10, 0], c: [5, 10] });
        const triMesh = buildFillMesh(triangle);
        expect(triMesh).not.toBeNull();
        expect(triMesh!.vertexCount).toBe(3);

        const line = createLineShape({ start: [0, 0], end: [10, 0], stroke: { paint: '#000', width: 1 } });
        expect(buildFillMesh(line)).toBeNull();
    });

    it('builds stroke meshes for various shapes', () => {
        const circle = createCircleShape({ cx: 0, cy: 0, radius: 10, stroke: { paint: '#000', width: 2 } });
        expect(buildStrokeMesh(circle)).not.toBeNull();

        const noStroke = createCircleShape({ cx: 0, cy: 0, radius: 10 });
        expect(buildStrokeMesh(noStroke)).toBeNull();
    });
});
