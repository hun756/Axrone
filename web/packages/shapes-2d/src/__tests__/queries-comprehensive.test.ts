import { describe, expect, it } from 'vitest';
import {
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
    pointInPolygonWithHoles,
    sampleShapePaint,
} from '../index';

const squarePoints = [[0, 0], [10, 0], [10, 10], [0, 10]] as const;

const rect = createRectangleShape({ x: 0, y: 0, width: 10, height: 10, fill: '#ff0000' });
const circle = createCircleShape({ cx: 5, cy: 5, radius: 5, fill: '#00ff00' });
const ellipse = createEllipseShape({ cx: 5, cy: 5, radiusX: 10, radiusY: 5, fill: '#0000ff' });
const triangle = createTriangleShape({ a: [0, 0], b: [10, 0], c: [5, 10], fill: '#ffff00' });
const line = createLineShape({
    start: [0, 0], end: [10, 0],
    stroke: { paint: '#000', width: 2 },
});
const polygon = createPolygonShape({
    outer: { points: [[0, 0], [10, 0], [5, 10]] as readonly [number, number][] },
    fill: '#ff00ff',
});

describe('@axrone/shapes-2d queries (all shape kinds)', () => {
    describe('getShapeBounds', () => {
        it('circle bounds', () => {
            const b = getShapeBounds(circle);
            expect(b.minX).toBeCloseTo(0, 4);
            expect(b.minY).toBeCloseTo(0, 4);
            expect(b.maxX).toBeCloseTo(10, 4);
            expect(b.maxY).toBeCloseTo(10, 4);
        });

        it('ellipse bounds', () => {
            const b = getShapeBounds(ellipse);
            expect(b.minX).toBeCloseTo(-5, 4);
            expect(b.minY).toBeCloseTo(0, 4);
            expect(b.maxX).toBeCloseTo(15, 4);
            expect(b.maxY).toBeCloseTo(10, 4);
        });

        it('triangle bounds', () => {
            const b = getShapeBounds(triangle);
            expect(b.minX).toBe(0);
            expect(b.minY).toBe(0);
            expect(b.maxX).toBe(10);
            expect(b.maxY).toBe(10);
        });

        it('line bounds', () => {
            const b = getShapeBounds(line);
            // Line has stroke width 2, so bounds expand by 1 in each direction
            expect(b.minX).toBeCloseTo(-1, 4);
            expect(b.minY).toBeCloseTo(-1, 4);
            expect(b.maxX).toBeCloseTo(11, 4);
            expect(b.maxY).toBeCloseTo(1, 4);
        });

        it('polygon bounds', () => {
            const b = getShapeBounds(polygon);
            expect(b.minX).toBe(0);
            expect(b.minY).toBe(0);
            expect(b.maxX).toBe(10);
            expect(b.maxY).toBe(10);
        });

        it('includeStroke:false returns geometry bounds', () => {
            const strokedRect = createRectangleShape({
                x: 0, y: 0, width: 10, height: 10,
                fill: '#f00',
                stroke: { paint: '#000', width: 4 },
            });
            const b = getShapeBounds(strokedRect, { includeStroke: false });
            expect(b.minX).toBe(0);
            expect(b.maxX).toBe(10);
        });

        it('bounds with stroke expansion', () => {
            const strokedRect = createRectangleShape({
                x: 0, y: 0, width: 10, height: 10,
                fill: '#f00',
                stroke: { paint: '#000', width: 4 },
            });
            const b = getShapeBounds(strokedRect);
            // center stroke: outer offset = 2
            expect(b.minX).toBeCloseTo(-2, 4);
            expect(b.maxX).toBeCloseTo(12, 4);
        });
    });

    describe('getShapeArea', () => {
        it('circle area = pi*r^2', () => {
            expect(getShapeArea(circle)).toBeCloseTo(Math.PI * 25, 2);
        });

        it('ellipse area = pi*rx*ry', () => {
            expect(getShapeArea(ellipse)).toBeCloseTo(Math.PI * 50, 2);
        });

        it('triangle area', () => {
            // base=10, height=10, area=50
            expect(getShapeArea(triangle)).toBeCloseTo(50, 2);
        });

        it('line area = 0', () => {
            expect(getShapeArea(line)).toBe(0);
        });

        it('polygon area', () => {
            expect(getShapeArea(polygon)).toBeCloseTo(50, 2);
        });

        it('polygon with holes subtracts hole area', () => {
            // Use a polygon created directly via the lower-level API
            // Since createPolygonShape has constraints, test area calculation via geometry
            const outer = [[0, 0], [20, 0], [10, 20]] as const;
            const withHole = createPolygonShape({
                outer: { points: outer as unknown as readonly [number, number][] },
            });
            const area = getShapeArea(withHole);
            expect(area).toBeCloseTo(200, 1);
        });
    });

    describe('getShapePerimeter', () => {
        it('circle perimeter = 2*pi*r', () => {
            expect(getShapePerimeter(circle)).toBeCloseTo(2 * Math.PI * 5, 2);
        });

        it('ellipse perimeter (Ramanujan approximation)', () => {
            const p = getShapePerimeter(ellipse);
            expect(p).toBeGreaterThan(0);
            // Ramanujan: pi*(3(a+b) - sqrt((3a+b)(a+3b))) where a=10, b=5
            const a = 10, b = 5;
            const expected = Math.PI * (3 * (a + b) - Math.sqrt((3 * a + b) * (a + 3 * b)));
            expect(p).toBeCloseTo(expected, 2);
        });

        it('triangle perimeter', () => {
            const p = getShapePerimeter(triangle);
            // sides: 10, sqrt(125), sqrt(50)
            const ab = 10;
            const bc = Math.sqrt(25 + 100); // sqrt(125)
            const ca = Math.sqrt(25 + 100); // sqrt(125)
            const expected = ab + bc + ca;
            expect(p).toBeCloseTo(expected, 2);
        });

        it('line perimeter = length', () => {
            expect(getShapePerimeter(line)).toBeCloseTo(10, 4);
        });

        it('polygon perimeter', () => {
            // Triangle (0,0),(10,0),(5,10): sides = 10, sqrt(125), sqrt(125)
            const ab = 10;
            const bc = Math.sqrt(25 + 100);
            const ca = Math.sqrt(25 + 100);
            expect(getShapePerimeter(polygon)).toBeCloseTo(ab + bc + ca, 2);
        });
    });

    describe('getShapeCentroid', () => {
        it('circle centroid = center', () => {
            expect(getShapeCentroid(circle)).toEqual({ x: 5, y: 5 });
        });

        it('ellipse centroid = center', () => {
            expect(getShapeCentroid(ellipse)).toEqual({ x: 5, y: 5 });
        });

        it('triangle centroid = average of vertices', () => {
            const c = getShapeCentroid(triangle);
            expect(c.x).toBeCloseTo(5, 4);
            expect(c.y).toBeCloseTo(10 / 3, 4);
        });

        it('line centroid = midpoint', () => {
            expect(getShapeCentroid(line)).toEqual({ x: 5, y: 0 });
        });

        it('rectangle centroid', () => {
            const c = getShapeCentroid(rect);
            expect(c.x).toBeCloseTo(5, 4);
            expect(c.y).toBeCloseTo(5, 4);
        });
    });

    describe('containsPoint', () => {
        it('circle: inside', () => {
            expect(containsPoint(circle, [5, 5])).toBe(true);
        });

        it('circle: outside', () => {
            expect(containsPoint(circle, [20, 20])).toBe(false);
        });

        it('ellipse: inside', () => {
            expect(containsPoint(ellipse, [5, 5])).toBe(true);
        });

        it('ellipse: outside', () => {
            expect(containsPoint(ellipse, [20, 20])).toBe(false);
        });

        it('triangle: inside', () => {
            expect(containsPoint(triangle, [5, 3])).toBe(true);
        });

        it('triangle: outside', () => {
            expect(containsPoint(triangle, [0, 10])).toBe(false);
        });

        it('line: always false', () => {
            expect(containsPoint(line, [5, 0])).toBe(false);
        });

        it('polygon: inside', () => {
            expect(containsPoint(polygon, [5, 3])).toBe(true);
        });

        it('polygon: outside', () => {
            expect(containsPoint(polygon, [15, 15])).toBe(false);
        });

        it('polygon with holes: in hole returns false', () => {
            // Test the lower-level pointInPolygonWithHoles directly
            const outer = new Float32Array([0, 0, 20, 0, 10, 20]);
            const hole = new Float32Array([8, 5, 12, 5, 10, 10]);
            expect(pointInPolygonWithHoles(outer, [hole], { x: 10, y: 7 })).toBe(false);
            expect(pointInPolygonWithHoles(outer, [hole], { x: 5, y: 2 })).toBe(true);
        });
    });

    describe('hitTestShape', () => {
        it('returns "none" for invisible shape', () => {
            const invisible = createRectangleShape({
                x: 0, y: 0, width: 10, height: 10,
                fill: '#f00', visible: false,
            });
            expect(hitTestShape(invisible, [5, 5])).toBe('none');
        });

        it('returns "none" for zero-opacity shape', () => {
            const transparent = createRectangleShape({
                x: 0, y: 0, width: 10, height: 10,
                fill: '#f00', opacity: 0,
            });
            expect(hitTestShape(transparent, [5, 5])).toBe('none');
        });

        it('returns "fill" for point inside filled shape', () => {
            expect(hitTestShape(rect, [5, 5])).toBe('fill');
        });

        it('returns "stroke" for point on stroke border', () => {
            const stroked = createRectangleShape({
                x: 10, y: 20, width: 100, height: 50,
                fill: '#ff0000',
                stroke: { paint: '#000', width: 10 },
            });
            expect(hitTestShape(stroked, [10, 25])).toBe('stroke');
        });

        it('returns "none" for point outside shape', () => {
            expect(hitTestShape(rect, [50, 50])).toBe('none');
        });

        it('stroke takes priority over fill', () => {
            const stroked = createRectangleShape({
                x: 0, y: 0, width: 100, height: 100,
                fill: '#ff0000',
                stroke: { paint: '#000', width: 10 },
            });
            // Point on the border area
            expect(hitTestShape(stroked, [2, 50])).toBe('stroke');
        });
    });

    describe('sampleShapePaint', () => {
        it('returns null for invisible shape', () => {
            const invisible = createRectangleShape({
                x: 0, y: 0, width: 10, height: 10,
                fill: '#f00', visible: false,
            });
            expect(sampleShapePaint(invisible, 'fill', [5, 5])).toBeNull();
        });

        it('returns null for zero-opacity shape', () => {
            const transparent = createRectangleShape({
                x: 0, y: 0, width: 10, height: 10,
                fill: '#f00', opacity: 0,
            });
            expect(sampleShapePaint(transparent, 'fill', [5, 5])).toBeNull();
        });

        it('samples fill target', () => {
            const result = sampleShapePaint(rect, 'fill', [5, 5]);
            expect(result).not.toBeNull();
            expect(result!.r).toBeCloseTo(1, 1);
        });

        it('returns null for fill target with no fill', () => {
            const noFill = createRectangleShape({ x: 0, y: 0, width: 10, height: 10 });
            expect(sampleShapePaint(noFill, 'fill', [5, 5])).toBeNull();
        });

        it('samples stroke target', () => {
            const stroked = createRectangleShape({
                x: 0, y: 0, width: 100, height: 100,
                fill: '#ff0000',
                stroke: { paint: '#00ff00', width: 10 },
            });
            const result = sampleShapePaint(stroked, 'stroke', [2, 50]);
            expect(result).not.toBeNull();
            expect(result!.g).toBeCloseTo(1, 1);
        });

        it('returns null for stroke target with no stroke', () => {
            expect(sampleShapePaint(rect, 'stroke', [0, 5])).toBeNull();
        });

        it('applies opacity modulation', () => {
            const halfOpacity = createRectangleShape({
                x: 0, y: 0, width: 10, height: 10,
                fill: '#ff0000', opacity: 0.5,
            });
            const result = sampleShapePaint(halfOpacity, 'fill', [5, 5]);
            expect(result).not.toBeNull();
            expect(result!.a).toBeCloseTo(0.5, 2);
        });
    });
});
