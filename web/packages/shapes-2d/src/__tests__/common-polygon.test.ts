import { describe, expect, it } from 'vitest';
import {
    deduplicateVertices,
    isConvexPolygon,
    isSimplePolygon,
    pointInConvexPolygon,
    pointInPolygon,
    pointInPolygonWithHoles,
    pointToPolygonEdgeDistance,
    polygonAbsoluteArea,
    polygonBounds,
    polygonCentroid,
    polygonPerimeter,
    polygonSignedArea,
    removeCollinearVertices,
    triangulateEarClipping,
} from '../index';

// Unit square CCW
const unitSquare = new Float32Array([0, 0, 1, 0, 1, 1, 0, 1]);
// Unit square CW
const unitSquareCW = new Float32Array([0, 0, 0, 1, 1, 1, 1, 0]);
// Triangle
const triangle = new Float32Array([0, 0, 4, 0, 0, 3]);

describe('@axrone/shapes-2d common polygon utilities', () => {
    describe('polygonSignedArea', () => {
        it('returns positive area for CCW winding', () => {
            expect(polygonSignedArea(unitSquare)).toBeGreaterThan(0);
        });

        it('returns negative area for CW winding', () => {
            expect(polygonSignedArea(unitSquareCW)).toBeLessThan(0);
        });

        it('returns zero for degenerate polygon', () => {
            const degenerate = new Float32Array([0, 0, 1, 1, 2, 2]);
            expect(polygonSignedArea(degenerate)).toBeCloseTo(0, 5);
        });

        it('computes correct area for unit square', () => {
            expect(polygonSignedArea(unitSquare)).toBeCloseTo(1, 5);
        });
    });

    describe('polygonAbsoluteArea', () => {
        it('returns absolute value of signed area', () => {
            expect(polygonAbsoluteArea(unitSquare)).toBeCloseTo(1, 5);
            expect(polygonAbsoluteArea(unitSquareCW)).toBeCloseTo(1, 5);
        });

        it('returns zero for degenerate polygon', () => {
            const line = new Float32Array([0, 0, 1, 1]);
            expect(polygonAbsoluteArea(line)).toBeCloseTo(0, 5);
        });
    });

    describe('polygonPerimeter', () => {
        it('computes perimeter of unit square', () => {
            expect(polygonPerimeter(unitSquare)).toBeCloseTo(4, 5);
        });

        it('computes perimeter of triangle', () => {
            expect(polygonPerimeter(triangle)).toBeCloseTo(3 + 4 + 5, 5);
        });

        it('returns 0 for fewer than 2 points', () => {
            expect(polygonPerimeter(new Float32Array([5, 5]))).toBe(0);
        });
    });

    describe('polygonCentroid', () => {
        it('computes center of unit square', () => {
            const c = polygonCentroid(unitSquare);
            expect(c.x).toBeCloseTo(0.5, 4);
            expect(c.y).toBeCloseTo(0.5, 4);
        });

        it('computes centroid of triangle', () => {
            const c = polygonCentroid(triangle);
            expect(c.x).toBeCloseTo(4 / 3, 4);
            expect(c.y).toBeCloseTo(1, 4);
        });

        it('returns average for degenerate (zero-area) polygon', () => {
            const line = new Float32Array([0, 0, 2, 0, 4, 0]);
            const c = polygonCentroid(line);
            expect(c.x).toBeCloseTo(2, 4);
            expect(c.y).toBeCloseTo(0, 4);
        });

        it('returns {0,0} for empty input', () => {
            const c = polygonCentroid(new Float32Array(0));
            expect(c.x).toBe(0);
            expect(c.y).toBe(0);
        });
    });

    describe('polygonBounds', () => {
        it('computes bounds of unit square', () => {
            const b = polygonBounds(unitSquare);
            expect(b.minX).toBe(0);
            expect(b.minY).toBe(0);
            expect(b.maxX).toBe(1);
            expect(b.maxY).toBe(1);
            expect(b.width).toBe(1);
            expect(b.height).toBe(1);
        });

        it('returns zero bounds for empty input', () => {
            const b = polygonBounds(new Float32Array(0));
            expect(b.width).toBe(0);
            expect(b.height).toBe(0);
        });
    });

    describe('pointInConvexPolygon', () => {
        it('returns true for point inside CCW square', () => {
            expect(pointInConvexPolygon(unitSquare, { x: 0.5, y: 0.5 })).toBe(true);
        });

        it('returns false for point outside', () => {
            expect(pointInConvexPolygon(unitSquare, { x: 2, y: 2 })).toBe(false);
        });

        it('returns true for point inside CW square', () => {
            expect(pointInConvexPolygon(unitSquareCW, { x: 0.5, y: 0.5 })).toBe(true);
        });

        it('returns false for fewer than 3 points', () => {
            expect(pointInConvexPolygon(new Float32Array([0, 0, 1, 1]), { x: 0.5, y: 0.5 })).toBe(false);
        });
    });

    describe('pointInPolygon', () => {
        it('returns true for point inside (ray casting)', () => {
            expect(pointInPolygon(unitSquare, { x: 0.5, y: 0.5 })).toBe(true);
        });

        it('returns false for point outside', () => {
            expect(pointInPolygon(unitSquare, { x: 5, y: 5 })).toBe(false);
        });

        it('returns false for fewer than 3 points', () => {
            expect(pointInPolygon(new Float32Array([0, 0]), { x: 0, y: 0 })).toBe(false);
        });

        it('handles concave polygon', () => {
            // L-shaped polygon
            const lShape = new Float32Array([0, 0, 2, 0, 2, 1, 1, 1, 1, 2, 0, 2]);
            expect(pointInPolygon(lShape, { x: 0.5, y: 0.5 })).toBe(true);
            expect(pointInPolygon(lShape, { x: 1.5, y: 1.5 })).toBe(false);
        });
    });

    describe('pointInPolygonWithHoles', () => {
        it('returns true when in outer but not in hole', () => {
            const outer = new Float32Array([0, 0, 10, 0, 10, 10, 0, 10]);
            const hole = new Float32Array([3, 3, 7, 3, 7, 7, 3, 7]);
            expect(pointInPolygonWithHoles(outer, [hole], { x: 1, y: 1 })).toBe(true);
        });

        it('returns false when inside hole', () => {
            const outer = new Float32Array([0, 0, 10, 0, 10, 10, 0, 10]);
            const hole = new Float32Array([3, 3, 7, 3, 7, 7, 3, 7]);
            expect(pointInPolygonWithHoles(outer, [hole], { x: 5, y: 5 })).toBe(false);
        });

        it('returns false when outside outer', () => {
            const outer = new Float32Array([0, 0, 10, 0, 10, 10, 0, 10]);
            const hole = new Float32Array([3, 3, 7, 3, 7, 7, 3, 7]);
            expect(pointInPolygonWithHoles(outer, [hole], { x: 20, y: 20 })).toBe(false);
        });
    });

    describe('pointToPolygonEdgeDistance', () => {
        it('computes distance from center to edge of unit square', () => {
            const d = pointToPolygonEdgeDistance(unitSquare, { x: 0.5, y: 0.5 });
            expect(d).toBeCloseTo(0.5, 4);
        });

        it('computes distance from corner to nearest edge', () => {
            const d = pointToPolygonEdgeDistance(unitSquare, { x: 0, y: 0 });
            expect(d).toBeCloseTo(0, 4);
        });

        it('returns Infinity for fewer than 2 points', () => {
            expect(pointToPolygonEdgeDistance(new Float32Array([0, 0]), { x: 0, y: 0 })).toBe(Infinity);
        });
    });

    describe('isConvexPolygon', () => {
        it('returns true for convex square', () => {
            expect(isConvexPolygon(unitSquare)).toBe(true);
        });

        it('returns false for concave L-shape', () => {
            const lShape = new Float32Array([0, 0, 2, 0, 2, 1, 1, 1, 1, 2, 0, 2]);
            expect(isConvexPolygon(lShape)).toBe(false);
        });

        it('returns false for fewer than 3 points', () => {
            expect(isConvexPolygon(new Float32Array([0, 0, 1, 1]))).toBe(false);
        });

        it('returns true for triangle', () => {
            expect(isConvexPolygon(triangle)).toBe(true);
        });

        it('handles collinear vertices by skipping them', () => {
            // Square with extra collinear point on one edge
            const withCollinear = new Float32Array([0, 0, 0.5, 0, 1, 0, 1, 1, 0, 1]);
            expect(isConvexPolygon(withCollinear)).toBe(true);
        });
    });

    describe('isSimplePolygon', () => {
        it('returns true for simple square', () => {
            expect(isSimplePolygon(unitSquare)).toBe(true);
        });

        it('returns false for self-intersecting (bowtie)', () => {
            const bowtie = new Float32Array([0, 0, 1, 1, 1, 0, 0, 1]);
            expect(isSimplePolygon(bowtie)).toBe(false);
        });

        it('returns false for fewer than 3 points', () => {
            expect(isSimplePolygon(new Float32Array([0, 0, 1, 1]))).toBe(false);
        });

        it('returns true for triangle', () => {
            expect(isSimplePolygon(triangle)).toBe(true);
        });
    });

    describe('triangulateEarClipping', () => {
        it('triangulates a convex quad into 2 triangles (6 indices)', () => {
            const indices = triangulateEarClipping(unitSquare);
            expect(indices.length).toBe(6);
        });

        it('returns single triangle for 3 vertices', () => {
            const indices = triangulateEarClipping(triangle);
            expect(indices.length).toBe(3);
        });

        it('returns empty for fewer than 3 points', () => {
            const indices = triangulateEarClipping(new Float32Array([0, 0, 1, 1]));
            expect(indices.length).toBe(0);
        });

        it('triangulates a concave polygon', () => {
            const concave = new Float32Array([0, 0, 3, 0, 3, 3, 1.5, 1, 0, 3]);
            const indices = triangulateEarClipping(concave);
            expect(indices.length).toBeGreaterThan(0);
            // Should produce 3 triangles for 5 vertices
            expect(indices.length).toBe(9);
        });

        it('returns Uint16Array for small vertex counts', () => {
            const indices = triangulateEarClipping(unitSquare);
            expect(indices).toBeInstanceOf(Uint16Array);
        });
    });

    describe('removeCollinearVertices', () => {
        it('removes collinear vertices', () => {
            // Square with extra point on bottom edge
            const withCollinear = new Float32Array([0, 0, 0.5, 0, 1, 0, 1, 1, 0, 1]);
            const result = removeCollinearVertices(withCollinear);
            expect(result.length).toBe(8); // 4 points after removing collinear
        });

        it('preserves non-collinear polygon', () => {
            const result = removeCollinearVertices(unitSquare);
            expect(result.length).toBe(unitSquare.length);
        });

        it('passes through fewer than 3 points', () => {
            const input = new Float32Array([0, 0, 1, 1]);
            const result = removeCollinearVertices(input);
            // Fewer than 3 points: function returns input as-is (length 4 = 2 points * 2 coords)
            expect(result.length).toBe(4);
        });
    });

    describe('deduplicateVertices', () => {
        it('removes consecutive duplicate vertices', () => {
            const withDup = new Float32Array([0, 0, 0, 0, 1, 0, 1, 1, 0, 1]);
            const result = deduplicateVertices(withDup);
            // Should remove the duplicate (0,0)
            expect(result.length).toBeLessThan(withDup.length);
        });

        it('wraps around to check first==last', () => {
            const closing = new Float32Array([0, 0, 1, 0, 1, 1, 0, 1, 0, 0]);
            const result = deduplicateVertices(closing);
            // Last (0,0) is same as first (0,0), should be removed
            expect(result.length).toBe(8); // 4 unique points
        });

        it('passes through fewer than 2 points', () => {
            const single = new Float32Array([5, 5]);
            const result = deduplicateVertices(single);
            // Fewer than 2 points: returns input as-is
            expect(result.length).toBe(2);
        });

        it('supports custom epsilon', () => {
            const closePoints = new Float32Array([0, 0, 0.001, 0, 1, 0, 1, 1, 0, 1]);
            const result = deduplicateVertices(closePoints, 0.01);
            expect(result.length).toBeLessThan(closePoints.length);
        });
    });
});
