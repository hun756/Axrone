import { describe, it, expect } from 'vitest';
import {
    cloneVec2, rotateVec2, inverseRotateVec2, subtractVec2, dotVec2, crossVec2,
    lengthSquared, distanceSquared, clamp, normalizeBounds, intersectsAabb,
    cloneMaterial, toShapeMaterial, toShapeFilter,
    computePolygonCentroid, computePolygonMassData,
    pointInPolygon, distanceSquaredToSegment, raycastSegment,
    buildBoxVertices, transformPoint2D,
    STANDALONE_CONSTRAINT_ID_START, GEOMETRY_EPSILON, POINT_QUERY_EPSILON,
} from '../core/physics-world-2d-helpers';

describe('constants', () => {
    it('has expected constant values', () => {
        expect(STANDALONE_CONSTRAINT_ID_START).toBe(1_000_000);
        expect(GEOMETRY_EPSILON).toBeGreaterThan(0);
        expect(POINT_QUERY_EPSILON).toBeGreaterThan(0);
    });
});

describe('cloneVec2', () => {
    it('produces an independent copy', () => {
        const v = { x: 3, y: 4 };
        const c = cloneVec2(v);
        expect(c).toEqual(v);
        c.x = 99;
        expect(v.x).toBe(3);
    });
});

describe('rotateVec2 / inverseRotateVec2', () => {
    it('rotates a vector by PI/2', () => {
        const r = rotateVec2({ x: 1, y: 0 }, Math.PI / 2);
        expect(r.x).toBeCloseTo(0, 5);
        expect(r.y).toBeCloseTo(1, 5);
    });

    it('inverseRotateVec2 undoes rotateVec2', () => {
        const angle = 0.7;
        const v = { x: 3, y: 4 };
        const rotated = rotateVec2(v, angle);
        const back = inverseRotateVec2(rotated, angle);
        expect(back.x).toBeCloseTo(v.x, 5);
        expect(back.y).toBeCloseTo(v.y, 5);
    });

    it('rotation by 0 returns same vector', () => {
        const v = { x: 5, y: -2 };
        const r = rotateVec2(v, 0);
        expect(r.x).toBeCloseTo(5, 5);
        expect(r.y).toBeCloseTo(-2, 5);
    });
});

describe('subtractVec2', () => {
    it('subtracts component-wise', () => {
        expect(subtractVec2({ x: 5, y: 7 }, { x: 2, y: 3 })).toEqual({ x: 3, y: 4 });
    });
});

describe('dotVec2', () => {
    it('computes dot product', () => {
        expect(dotVec2({ x: 1, y: 2 }, { x: 3, y: 4 })).toBe(11);
    });

    it('perpendicular vectors have zero dot product', () => {
        expect(dotVec2({ x: 1, y: 0 }, { x: 0, y: 1 })).toBe(0);
    });
});

describe('crossVec2', () => {
    it('computes 2D cross product', () => {
        expect(crossVec2({ x: 1, y: 0 }, { x: 0, y: 1 })).toBe(1);
    });

    it('parallel vectors have zero cross product', () => {
        expect(crossVec2({ x: 2, y: 0 }, { x: 5, y: 0 })).toBe(0);
    });
});

describe('lengthSquared', () => {
    it('returns squared magnitude', () => {
        expect(lengthSquared({ x: 3, y: 4 })).toBe(25);
    });
});

describe('distanceSquared', () => {
    it('returns squared distance between two points', () => {
        expect(distanceSquared({ x: 0, y: 0 }, { x: 3, y: 4 })).toBe(25);
    });

    it('returns 0 for same point', () => {
        expect(distanceSquared({ x: 5, y: 5 }, { x: 5, y: 5 })).toBe(0);
    });
});

describe('clamp', () => {
    it('clamps within range', () => {
        expect(clamp(5, 0, 10)).toBe(5);
        expect(clamp(-1, 0, 10)).toBe(0);
        expect(clamp(15, 0, 10)).toBe(10);
    });
});

describe('normalizeBounds', () => {
    it('swaps inverted min/max', () => {
        const result = normalizeBounds({ x: 5, y: 5 }, { x: 1, y: 1 });
        expect(result.min.x).toBe(1);
        expect(result.min.y).toBe(1);
        expect(result.max.x).toBe(5);
        expect(result.max.y).toBe(5);
    });

    it('preserves already-normal bounds', () => {
        const result = normalizeBounds({ x: 0, y: 0 }, { x: 3, y: 3 });
        expect(result.min.x).toBe(0);
        expect(result.max.x).toBe(3);
    });
});

describe('intersectsAabb', () => {
    const aabb = (minX: number, minY: number, maxX: number, maxY: number) => ({
        min: { x: minX, y: minY },
        max: { x: maxX, y: maxY },
    });

    it('detects overlapping AABBs', () => {
        expect(intersectsAabb(aabb(0, 0, 2, 2), aabb(1, 1, 3, 3))).toBe(true);
    });

    it('detects separated AABBs', () => {
        expect(intersectsAabb(aabb(0, 0, 1, 1), aabb(5, 5, 6, 6))).toBe(false);
    });

    it('detects touching AABBs as overlapping', () => {
        expect(intersectsAabb(aabb(0, 0, 1, 1), aabb(1, 0, 2, 1))).toBe(true);
    });
});

describe('cloneMaterial', () => {
    it('produces an independent copy', () => {
        const m = { friction: 0.5, restitution: 0.3, density: 1.0 };
        const c = cloneMaterial(m);
        expect(c).toEqual(m);
        c.friction = 0.9;
        expect(m.friction).toBe(0.5);
    });

    it('preserves optional fields when present', () => {
        const m = { friction: 0.1, restitution: 0.2, density: 0.3, rollingFriction: 0.05, spinningFriction: 0.01 };
        const c = cloneMaterial(m);
        expect(c.rollingFriction).toBe(0.05);
        expect(c.spinningFriction).toBe(0.01);
    });

    it('omits optional fields when absent', () => {
        const m = { friction: 0.1, restitution: 0.2, density: 0.3 };
        const c = cloneMaterial(m);
        expect(c).not.toHaveProperty('rollingFriction');
        expect(c).not.toHaveProperty('spinningFriction');
    });
});

describe('toShapeMaterial', () => {
    it('uses material when provided', () => {
        const m = toShapeMaterial({ material: { friction: 0.8, restitution: 0.1, density: 2 } });
        expect(m.friction).toBe(0.8);
        expect(m.density).toBe(2);
    });

    it('uses defaults when no material or overrides', () => {
        const m = toShapeMaterial({});
        expect(m.friction).toBe(0.2);
        expect(m.restitution).toBe(0);
        expect(m.density).toBe(1);
    });

    it('overrides individual fields', () => {
        const m = toShapeMaterial({ friction: 0.9, density: 5 });
        expect(m.friction).toBe(0.9);
        expect(m.density).toBe(5);
        expect(m.restitution).toBe(0);
    });
});

describe('toShapeFilter', () => {
    it('uses provided filter', () => {
        const f = toShapeFilter({ filter: { categoryBits: 0x02, maskBits: 0x04, groupIndex: 1 } });
        expect(f.categoryBits).toBe(0x02);
        expect(f.maskBits).toBe(0x04);
        expect(f.groupIndex).toBe(1);
    });

    it('uses defaults when no filter provided', () => {
        const f = toShapeFilter({});
        expect(f.categoryBits).toBe(1);
        expect(f.maskBits).toBe(0xffff);
        expect(f.groupIndex).toBe(0);
    });
});

describe('computePolygonCentroid', () => {
    it('computes centroid of a unit square', () => {
        const vertices = [
            { x: 0, y: 0 }, { x: 1, y: 0 },
            { x: 1, y: 1 }, { x: 0, y: 1 },
        ];
        const c = computePolygonCentroid(vertices);
        expect(c.x).toBeCloseTo(0.5, 4);
        expect(c.y).toBeCloseTo(0.5, 4);
    });

    it('falls back to average for degenerate polygon', () => {
        const vertices = [{ x: 0, y: 0 }, { x: 0, y: 0 }];
        const c = computePolygonCentroid(vertices);
        expect(c.x).toBeCloseTo(0, 4);
        expect(c.y).toBeCloseTo(0, 4);
    });
});

describe('computePolygonMassData', () => {
    it('computes mass data for a unit square with density 1', () => {
        const vertices = [
            { x: 0, y: 0 }, { x: 1, y: 0 },
            { x: 1, y: 1 }, { x: 0, y: 1 },
        ];
        const md = computePolygonMassData(vertices, 1 as any);
        expect(md.mass).toBeCloseTo(1, 3);
        expect(md.inverseMass).toBeCloseTo(1, 3);
        expect(md.center.x).toBeCloseTo(0.5, 3);
        expect(md.center.y).toBeCloseTo(0.5, 3);
    });

    it('returns zero mass for degenerate polygon', () => {
        const vertices = [{ x: 0, y: 0 }, { x: 0, y: 0 }];
        const md = computePolygonMassData(vertices, 1 as any);
        expect(md.mass).toBe(0);
        expect(md.inverseMass).toBe(0);
    });

    it('scales mass with density', () => {
        const vertices = [
            { x: 0, y: 0 }, { x: 2, y: 0 },
            { x: 2, y: 2 }, { x: 0, y: 2 },
        ];
        const md1 = computePolygonMassData(vertices, 1 as any);
        const md2 = computePolygonMassData(vertices, 3 as any);
        expect(md2.mass).toBeCloseTo(md1.mass * 3, 3);
    });
});

describe('pointInPolygon', () => {
    const square = [
        { x: 0, y: 0 }, { x: 4, y: 0 },
        { x: 4, y: 4 }, { x: 0, y: 4 },
    ];

    it('returns true for point inside', () => {
        expect(pointInPolygon({ x: 2, y: 2 }, square)).toBe(true);
    });

    it('returns false for point outside', () => {
        expect(pointInPolygon({ x: 5, y: 5 }, square)).toBe(false);
    });

    it('returns false for point clearly outside', () => {
        expect(pointInPolygon({ x: -1, y: 2 }, square)).toBe(false);
    });
});

describe('distanceSquaredToSegment', () => {
    it('returns 0 for point on the segment', () => {
        const d = distanceSquaredToSegment({ x: 0.5, y: 0 }, { x: 0, y: 0 }, { x: 1, y: 0 });
        expect(d).toBeCloseTo(0, 5);
    });

    it('returns correct distance for point off segment', () => {
        const d = distanceSquaredToSegment({ x: 0.5, y: 3 }, { x: 0, y: 0 }, { x: 1, y: 0 });
        expect(d).toBeCloseTo(9, 5);
    });

    it('handles degenerate zero-length segment', () => {
        const d = distanceSquaredToSegment({ x: 3, y: 4 }, { x: 0, y: 0 }, { x: 0, y: 0 });
        expect(d).toBeCloseTo(25, 5);
    });

    it('clamps to start when projection is negative', () => {
        const d = distanceSquaredToSegment({ x: -1, y: 0 }, { x: 0, y: 0 }, { x: 1, y: 0 });
        expect(d).toBeCloseTo(1, 5);
    });

    it('clamps to end when projection exceeds 1', () => {
        const d = distanceSquaredToSegment({ x: 2, y: 0 }, { x: 0, y: 0 }, { x: 1, y: 0 });
        expect(d).toBeCloseTo(1, 5);
    });
});

describe('raycastSegment', () => {
    it('detects a hit against a segment', () => {
        const result = raycastSegment(
            { x: 0.5, y: -1 }, { x: 0, y: 1 },
            { x: 0, y: 0 }, { x: 1, y: 0 },
            10
        );
        expect(result.hit).toBe(true);
        expect(result.fraction).toBeCloseTo(1, 4);
    });

    it('returns no hit for parallel ray', () => {
        const result = raycastSegment(
            { x: 0, y: 0 }, { x: 1, y: 0 },
            { x: 0, y: 1 }, { x: 1, y: 1 },
            10
        );
        expect(result.hit).toBe(false);
    });

    it('returns no hit when segment is behind origin', () => {
        const result = raycastSegment(
            { x: 0.5, y: 1 }, { x: 0, y: 1 },
            { x: 0, y: 0 }, { x: 1, y: 0 },
            10
        );
        expect(result.hit).toBe(false);
    });

    it('returns no hit for degenerate zero-length segment', () => {
        const result = raycastSegment(
            { x: 0, y: 0 }, { x: 1, y: 0 },
            { x: 5, y: 5 }, { x: 5, y: 5 },
            10
        );
        expect(result.hit).toBe(false);
    });
});

describe('buildBoxVertices', () => {
    it('produces 4 corners for an axis-aligned box', () => {
        const verts = buildBoxVertices({ x: 0, y: 0 }, 1, 1, 0);
        expect(verts).toHaveLength(4);
        expect(verts[0]).toEqual({ x: -1, y: -1 });
        expect(verts[2]).toEqual({ x: 1, y: 1 });
    });

    it('translates vertices by center', () => {
        const verts = buildBoxVertices({ x: 5, y: 5 }, 1, 1, 0);
        expect(verts[0]).toEqual({ x: 4, y: 4 });
        expect(verts[2]).toEqual({ x: 6, y: 6 });
    });

    it('rotates vertices by angle', () => {
        const verts = buildBoxVertices({ x: 0, y: 0 }, 1, 0, Math.PI / 2);
        // After 90-degree rotation, the halfWidth axis maps to Y.
        expect(verts[0].x).toBeCloseTo(0, 4);
        expect(verts[0].y).toBeCloseTo(-1, 4);
    });
});

describe('transformPoint2D', () => {
    it('translates with zero rotation', () => {
        const p = transformPoint2D({ x: 10, y: 20 }, 0, { x: 1, y: 2 });
        expect(p.x).toBeCloseTo(11, 5);
        expect(p.y).toBeCloseTo(22, 5);
    });

    it('rotates and translates', () => {
        const p = transformPoint2D({ x: 0, y: 0 }, Math.PI / 2, { x: 1, y: 0 });
        expect(p.x).toBeCloseTo(0, 5);
        expect(p.y).toBeCloseTo(1, 5);
    });
});
