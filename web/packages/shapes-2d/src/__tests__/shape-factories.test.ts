import { describe, expect, it } from 'vitest';
import {
    createCircleShape,
    createEllipseShape,
    createLineShape,
    createPolygonShape,
    createRectangleShape,
    createTriangleShape,
    ShapeValidationError,
} from '../index';

describe('@axrone/shapes-2d shape factories', () => {
    describe('createRectangleShape', () => {
        it('creates a frozen rectangle with correct properties', () => {
            const rect = createRectangleShape({ x: 10, y: 20, width: 100, height: 50 });
            expect(Object.isFrozen(rect)).toBe(true);
            expect(rect.kind).toBe('rectangle');
            expect(rect.x).toBe(10);
            expect(rect.y).toBe(20);
            expect(rect.width).toBe(100);
            expect(rect.height).toBe(50);
            expect(rect.fill).toBeNull();
            expect(rect.stroke).toBeNull();
            expect(rect.opacity).toBe(1);
            expect(rect.visible).toBe(true);
            expect(rect.name).toBeUndefined();
        });

        it('throws for non-positive width', () => {
            expect(() => createRectangleShape({ x: 0, y: 0, width: 0, height: 10 })).toThrow(ShapeValidationError);
            expect(() => createRectangleShape({ x: 0, y: 0, width: -5, height: 10 })).toThrow(ShapeValidationError);
        });

        it('throws for non-positive height', () => {
            expect(() => createRectangleShape({ x: 0, y: 0, width: 10, height: 0 })).toThrow(ShapeValidationError);
        });

        it('throws for NaN coordinates', () => {
            expect(() => createRectangleShape({ x: NaN, y: 0, width: 10, height: 10 })).toThrow(ShapeValidationError);
        });

        it('throws for Infinity dimensions', () => {
            expect(() => createRectangleShape({ x: 0, y: 0, width: Infinity, height: 10 })).toThrow(ShapeValidationError);
        });

        it('applies appearance with opacity clamping', () => {
            const rect = createRectangleShape({
                x: 0, y: 0, width: 10, height: 10,
                fill: '#ff0000',
                opacity: 1.5,
                visible: false,
                name: '  test  ',
            });
            expect(rect.fill?.kind).toBe('solid');
            expect(rect.opacity).toBe(1);
            expect(rect.visible).toBe(false);
            expect(rect.name).toBe('test');
        });

        it('clamps opacity to 0 for negative values', () => {
            const rect = createRectangleShape({ x: 0, y: 0, width: 1, height: 1, opacity: -0.5 });
            expect(rect.opacity).toBe(0);
        });
    });

    describe('createCircleShape', () => {
        it('creates a frozen circle with correct properties', () => {
            const circle = createCircleShape({ cx: 5, cy: 10, radius: 25 });
            expect(Object.isFrozen(circle)).toBe(true);
            expect(circle.kind).toBe('circle');
            expect(circle.cx).toBe(5);
            expect(circle.cy).toBe(10);
            expect(circle.radius).toBe(25);
        });

        it('throws for non-positive radius', () => {
            expect(() => createCircleShape({ cx: 0, cy: 0, radius: 0 })).toThrow(ShapeValidationError);
            expect(() => createCircleShape({ cx: 0, cy: 0, radius: -1 })).toThrow(ShapeValidationError);
        });

        it('throws for NaN center', () => {
            expect(() => createCircleShape({ cx: NaN, cy: 0, radius: 5 })).toThrow(ShapeValidationError);
        });
    });

    describe('createEllipseShape', () => {
        it('creates a frozen ellipse', () => {
            const ellipse = createEllipseShape({ cx: 0, cy: 0, radiusX: 10, radiusY: 5 });
            expect(Object.isFrozen(ellipse)).toBe(true);
            expect(ellipse.kind).toBe('ellipse');
            expect(ellipse.radiusX).toBe(10);
            expect(ellipse.radiusY).toBe(5);
        });

        it('throws for non-positive radiusX', () => {
            expect(() => createEllipseShape({ cx: 0, cy: 0, radiusX: 0, radiusY: 5 })).toThrow(ShapeValidationError);
        });

        it('throws for non-positive radiusY', () => {
            expect(() => createEllipseShape({ cx: 0, cy: 0, radiusX: 10, radiusY: -1 })).toThrow(ShapeValidationError);
        });
    });

    describe('createTriangleShape', () => {
        it('creates a frozen triangle from point inputs', () => {
            const tri = createTriangleShape({ a: [0, 0], b: [4, 0], c: [0, 3] });
            expect(Object.isFrozen(tri)).toBe(true);
            expect(tri.kind).toBe('triangle');
            expect(tri.a).toEqual({ x: 0, y: 0 });
            expect(tri.b).toEqual({ x: 4, y: 0 });
            expect(tri.c).toEqual({ x: 0, y: 3 });
        });

        it('accepts IVec2Like point inputs', () => {
            const tri = createTriangleShape({
                a: { x: 0, y: 0 },
                b: { x: 1, y: 0 },
                c: { x: 0, y: 1 },
            });
            expect(tri.a).toEqual({ x: 0, y: 0 });
        });

        it('throws for collinear points', () => {
            expect(() =>
                createTriangleShape({ a: [0, 0], b: [1, 1], c: [2, 2] })
            ).toThrow(ShapeValidationError);
        });

        it('throws for nearly-collinear points (within EPSILON)', () => {
            expect(() =>
                createTriangleShape({ a: [0, 0], b: [1, 0], c: [0.5, 1e-12] })
            ).toThrow(ShapeValidationError);
        });
    });

    describe('createLineShape', () => {
        it('creates a frozen line with stroke', () => {
            const line = createLineShape({
                start: [0, 0],
                end: [10, 10],
                stroke: { paint: '#000', width: 2 },
            });
            expect(Object.isFrozen(line)).toBe(true);
            expect(line.kind).toBe('line');
            expect(line.start).toEqual({ x: 0, y: 0 });
            expect(line.end).toEqual({ x: 10, y: 10 });
            expect(line.stroke?.width).toBe(2);
        });

        it('throws for zero-length line without stroke', () => {
            expect(() =>
                createLineShape({ start: [5, 5], end: [5, 5] })
            ).toThrow(ShapeValidationError);
        });

        it('succeeds for zero-length line with stroke', () => {
            const line = createLineShape({
                start: [5, 5],
                end: [5, 5],
                stroke: { paint: '#000', width: 2 },
            });
            expect(line.kind).toBe('line');
        });

        it('does not support fill paint', () => {
            expect(() =>
                createLineShape({
                    start: [0, 0],
                    end: [1, 1],
                    fill: '#ff0000',
                    stroke: { paint: '#000', width: 1 },
                })
            ).toThrow(ShapeValidationError);
        });
    });

    describe('createPolygonShape', () => {
        // Use triangle points (3 vertices) to avoid isSimplePolygon edge cases with parallel edges
        const trianglePoints: readonly [number, number][] = [[0, 0], [10, 0], [5, 10]];

        it('creates a polygon from outer ring', () => {
            const poly = createPolygonShape({
                outer: { points: trianglePoints },
            });
            expect(Object.isFrozen(poly)).toBe(true);
            expect(poly.kind).toBe('polygon');
            expect(poly.outer.points).toHaveLength(3);
            expect(poly.closed).toBe(true);
            expect(poly.convex).toBe(true);
            expect(poly.holes).toHaveLength(0);
        });

        it('creates a polygon from points array', () => {
            const poly = createPolygonShape({ points: trianglePoints });
            expect(poly.kind).toBe('polygon');
            expect(poly.outer.points).toHaveLength(3);
        });

        it('throws when neither outer nor points provided', () => {
            expect(() => createPolygonShape({})).toThrow(ShapeValidationError);
        });

        it('throws for fewer than 3 points in ring', () => {
            expect(() =>
                createPolygonShape({ outer: { points: [[0, 0], [1, 0]] } })
            ).toThrow(ShapeValidationError);
        });

        it('detects convex polygon', () => {
            const poly = createPolygonShape({ outer: { points: trianglePoints } });
            expect(poly.convex).toBe(true);
        });
    });

    describe('appearance normalization', () => {
        it('sets fill to null when not provided', () => {
            const rect = createRectangleShape({ x: 0, y: 0, width: 1, height: 1 });
            expect(rect.fill).toBeNull();
        });

        it('sets stroke to null when not provided', () => {
            const rect = createRectangleShape({ x: 0, y: 0, width: 1, height: 1 });
            expect(rect.stroke).toBeNull();
        });

        it('trims and undefined-ifies empty names', () => {
            const rect = createRectangleShape({ x: 0, y: 0, width: 1, height: 1, name: '  ' });
            expect(rect.name).toBeUndefined();
        });

        it('defaults visible to true', () => {
            const rect = createRectangleShape({ x: 0, y: 0, width: 1, height: 1 });
            expect(rect.visible).toBe(true);
        });
    });
});
