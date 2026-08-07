import { describe, expect, it } from 'vitest';
import {
    createCircleShape,
    createEllipseShape,
    createLineShape,
    createPolygonShape,
    createRectangleShape,
    createTriangleShape,
    isCircleShape,
    isEllipseShape,
    isLineShape,
    isPolygonShape,
    isRectangleShape,
    isShape2D,
    isTriangleShape,
    matchShape,
} from '../index';

const rect = createRectangleShape({ x: 0, y: 0, width: 10, height: 10 });
const circle = createCircleShape({ cx: 0, cy: 0, radius: 5 });
const ellipse = createEllipseShape({ cx: 0, cy: 0, radiusX: 10, radiusY: 5 });
const triangle = createTriangleShape({ a: [0, 0], b: [1, 0], c: [0, 1] });
const line = createLineShape({ start: [0, 0], end: [1, 1], stroke: { paint: '#000', width: 1 } });
const polygon = createPolygonShape({
    outer: { points: [[0, 0], [10, 0], [5, 10]] as readonly [number, number][] },
});

describe('@axrone/shapes-2d shape type guards and matchShape', () => {
    describe('isRectangleShape', () => {
        it('returns true for rectangle', () => {
            expect(isRectangleShape(rect)).toBe(true);
        });
        it('returns false for non-rectangle shapes', () => {
            expect(isRectangleShape(circle)).toBe(false);
        });
        it('returns false for non-shapes', () => {
            expect(isRectangleShape(null)).toBe(false);
            expect(isRectangleShape(undefined)).toBe(false);
            expect(isRectangleShape(42)).toBe(false);
            expect(isRectangleShape('rect')).toBe(false);
            expect(isRectangleShape({ kind: 'rectangle' })).toBe(true);
        });
    });

    describe('isCircleShape', () => {
        it('returns true for circle', () => {
            expect(isCircleShape(circle)).toBe(true);
        });
        it('returns false for non-circle', () => {
            expect(isCircleShape(rect)).toBe(false);
        });
    });

    describe('isEllipseShape', () => {
        it('returns true for ellipse', () => {
            expect(isEllipseShape(ellipse)).toBe(true);
        });
        it('returns false for non-ellipse', () => {
            expect(isEllipseShape(circle)).toBe(false);
        });
    });

    describe('isTriangleShape', () => {
        it('returns true for triangle', () => {
            expect(isTriangleShape(triangle)).toBe(true);
        });
        it('returns false for non-triangle', () => {
            expect(isTriangleShape(rect)).toBe(false);
        });
    });

    describe('isLineShape', () => {
        it('returns true for line', () => {
            expect(isLineShape(line)).toBe(true);
        });
        it('returns false for non-line', () => {
            expect(isLineShape(rect)).toBe(false);
        });
    });

    describe('isPolygonShape', () => {
        it('returns true for polygon', () => {
            expect(isPolygonShape(polygon)).toBe(true);
        });
        it('returns false for non-polygon', () => {
            expect(isPolygonShape(rect)).toBe(false);
        });
    });

    describe('isShape2D', () => {
        it('returns true for all 6 shape kinds', () => {
            expect(isShape2D(rect)).toBe(true);
            expect(isShape2D(circle)).toBe(true);
            expect(isShape2D(ellipse)).toBe(true);
            expect(isShape2D(triangle)).toBe(true);
            expect(isShape2D(line)).toBe(true);
            expect(isShape2D(polygon)).toBe(true);
        });

        it('returns false for invalid values', () => {
            expect(isShape2D(null)).toBe(false);
            expect(isShape2D(undefined)).toBe(false);
            expect(isShape2D(42)).toBe(false);
            expect(isShape2D({ kind: 'unknown' })).toBe(false);
            expect(isShape2D({})).toBe(false);
        });
    });

    describe('matchShape', () => {
        const matcher = {
            rectangle: (s: typeof rect) => `rect:${s.width}x${s.height}`,
            circle: (s: typeof circle) => `circle:r${s.radius}`,
            ellipse: (s: typeof ellipse) => `ellipse:${s.radiusX}x${s.radiusY}`,
            triangle: (_s: typeof triangle) => 'triangle',
            line: (_s: typeof line) => 'line',
            polygon: (s: typeof polygon) => `polygon:${s.outer.points.length}pts`,
        };

        it('dispatches rectangle', () => {
            expect(matchShape(rect, matcher)).toBe('rect:10x10');
        });

        it('dispatches circle', () => {
            expect(matchShape(circle, matcher)).toBe('circle:r5');
        });

        it('dispatches ellipse', () => {
            expect(matchShape(ellipse, matcher)).toBe('ellipse:10x5');
        });

        it('dispatches triangle', () => {
            expect(matchShape(triangle, matcher)).toBe('triangle');
        });

        it('dispatches line', () => {
            expect(matchShape(line, matcher)).toBe('line');
        });

        it('dispatches polygon', () => {
            expect(matchShape(polygon, matcher)).toBe('polygon:3pts');
        });
    });
});
