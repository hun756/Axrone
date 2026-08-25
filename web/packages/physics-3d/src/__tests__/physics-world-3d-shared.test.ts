import { describe, it, expect } from 'vitest';
import { Vec3 } from '@axrone/numeric';
import {
    BODY_TYPE_STATIC, BODY_TYPE_DYNAMIC,
    SHAPE_TYPE_SPHERE, SHAPE_TYPE_BOX, SHAPE_TYPE_CAPSULE,
    CONSTRAINT_TYPE_FIXED, CONSTRAINT_TYPE_HINGE, CONSTRAINT_TYPE_SPRING,
    normalizeVec3,
    transformPoint3D, inverseTransformPoint3D,
    clamp, componentMin, componentMax, midpointVec3,
    inverseVec3, cylinderConeLocalHalfExtents,
    intersectsAabb, expandAabb,
    makeMaterial, makeFilter,
    supportsQueryFilter, shouldShapeFiltersCollide,
    buildOrthonormalBasis, getAxisVector, getBoxWorldExtents,
    linePointDistanceSquared, triangleNormal,
    rayTriangleHit, raySphereHit, rayAabbHit,
    getHeightFieldLocalVertex,
    isSphereDef, isBoxDef, isCapsuleDef, isCylinderDef, isConeDef,
    isFixedDef, isHingeDef, isSpringDef,
    IDENTITY_ROTATION, DEFAULT_MATERIAL, DEFAULT_FILTER,
} from '../core/physics-world-3d-shared';

describe('physics-world-3d-shared', () => {
    describe('constants', () => {
        it('has correct body type values', () => {
            expect(BODY_TYPE_STATIC).toBe(0);
            expect(BODY_TYPE_DYNAMIC).toBe(2);
        });

        it('has correct shape type values', () => {
            expect(SHAPE_TYPE_SPHERE).toBe(5);
            expect(SHAPE_TYPE_BOX).toBe(3);
            expect(SHAPE_TYPE_CAPSULE).toBe(1);
        });

        it('has correct constraint type values', () => {
            expect(CONSTRAINT_TYPE_FIXED).toBe(0);
            expect(CONSTRAINT_TYPE_HINGE).toBe(2);
            expect(CONSTRAINT_TYPE_SPRING).toBe(6);
        });

        it('has identity rotation quaternion', () => {
            expect(IDENTITY_ROTATION.w).toBe(1);
            expect(IDENTITY_ROTATION.x).toBe(0);
        });

        it('has sensible default material', () => {
            expect(DEFAULT_MATERIAL.friction).toBeGreaterThan(0);
            expect(DEFAULT_MATERIAL.density).toBeGreaterThan(0);
        });

        it('has sensible default filter', () => {
            expect(DEFAULT_FILTER.categoryBits).toBe(1);
            expect(DEFAULT_FILTER.maskBits).toBe(0xffff);
        });
    });

    describe('vector math', () => {
        it('normalizeVec3 returns unit vector', () => {
            const n = normalizeVec3({ x: 3, y: 0, z: 0 });
            expect(n.x).toBeCloseTo(1, 5);
            expect(Vec3.len(n)).toBeCloseTo(1, 5);
        });

        it('normalizeVec3 returns zero for zero-length vector', () => {
            const n = normalizeVec3({ x: 0, y: 0, z: 0 });
            expect(n.x).toBe(0);
            expect(n.y).toBe(0);
            expect(n.z).toBe(0);
        });

        it('componentMin takes per-axis minimum', () => {
            expect(componentMin({ x: 1, y: 5, z: 3 }, { x: 4, y: 2, z: 6 })).toEqual({ x: 1, y: 2, z: 3 });
        });

        it('componentMax takes per-axis maximum', () => {
            expect(componentMax({ x: 1, y: 5, z: 3 }, { x: 4, y: 2, z: 6 })).toEqual({ x: 4, y: 5, z: 6 });
        });

        it('midpointVec3 averages two points', () => {
            expect(midpointVec3({ x: 0, y: 0, z: 0 }, { x: 4, y: 6, z: 8 })).toEqual({ x: 2, y: 3, z: 4 });
        });

        it('inverseVec3 returns component-wise reciprocal for positive values', () => {
            const inv = inverseVec3({ x: 2, y: 4, z: 5 });
            expect(inv.x).toBeCloseTo(0.5, 5);
            expect(inv.y).toBeCloseTo(0.25, 5);
            expect(inv.z).toBeCloseTo(0.2, 5);
        });

        it('inverseVec3 returns zero for zero/negative components', () => {
            const inv = inverseVec3({ x: 0, y: -1, z: 5 });
            expect(inv.x).toBe(0);
            expect(inv.y).toBe(0);
            expect(inv.z).toBeCloseTo(0.2, 5);
        });
    });

    describe('transform operations', () => {
        it('transformPoint3D with identity returns translated point', () => {
            const p = transformPoint3D({ x: 1, y: 0, z: 0 }, { x: 10, y: 0, z: 0 }, IDENTITY_ROTATION);
            expect(p.x).toBeCloseTo(11, 5);
        });

        it('inverseTransformPoint3D undoes transformPoint3D', () => {
            const pos = { x: 5, y: 0, z: 0 };
            const rot = IDENTITY_ROTATION;
            const world = transformPoint3D({ x: 1, y: 0, z: 0 }, pos, rot);
            const local = inverseTransformPoint3D(world, pos, rot);
            expect(local.x).toBeCloseTo(1, 4);
        });
    });

    describe('scalar and AABB utilities', () => {
        it('clamp restricts value to range', () => {
            expect(clamp(5, 0, 10)).toBe(5);
            expect(clamp(-1, 0, 10)).toBe(0);
            expect(clamp(15, 0, 10)).toBe(10);
        });

        it('intersectsAabb detects overlap', () => {
            const a = { min: { x: 0, y: 0, z: 0 }, max: { x: 2, y: 2, z: 2 } };
            const b = { min: { x: 1, y: 1, z: 1 }, max: { x: 3, y: 3, z: 3 } };
            expect(intersectsAabb(a, b)).toBe(true);
        });

        it('intersectsAabb detects separation', () => {
            const a = { min: { x: 0, y: 0, z: 0 }, max: { x: 1, y: 1, z: 1 } };
            const b = { min: { x: 5, y: 5, z: 5 }, max: { x: 6, y: 6, z: 6 } };
            expect(intersectsAabb(a, b)).toBe(false);
        });

        it('expandAabb grows to include a point', () => {
            const aabb = { min: { x: 0, y: 0, z: 0 }, max: { x: 1, y: 1, z: 1 } };
            const expanded = expandAabb(aabb, { x: 5, y: -1, z: 0 });
            expect(expanded.max.x).toBe(5);
            expect(expanded.min.y).toBe(-1);
        });
    });

    describe('material and filter factories', () => {
        it('makeMaterial uses defaults for missing fields', () => {
            const m = makeMaterial();
            expect(m.friction).toBeCloseTo(DEFAULT_MATERIAL.friction, 5);
            expect(m.density).toBeCloseTo(DEFAULT_MATERIAL.density, 5);
        });

        it('makeMaterial overrides specified fields', () => {
            const m = makeMaterial({ friction: 0.9 });
            expect(m.friction).toBeCloseTo(0.9, 5);
        });

        it('makeFilter uses defaults for missing fields', () => {
            const f = makeFilter();
            expect(f.categoryBits).toBe(DEFAULT_FILTER.categoryBits);
            expect(f.maskBits).toBe(DEFAULT_FILTER.maskBits);
        });
    });

    describe('collision filter logic', () => {
        it('shouldShapeFiltersCollide with matching category/mask bits', () => {
            const a = { categoryBits: 0x01, maskBits: 0x02, groupIndex: 0 };
            const b = { categoryBits: 0x02, maskBits: 0x01, groupIndex: 0 };
            expect(shouldShapeFiltersCollide(a, b)).toBe(true);
        });

        it('shouldShapeFiltersCollide rejects non-matching masks', () => {
            const a = { categoryBits: 0x01, maskBits: 0x00, groupIndex: 0 };
            const b = { categoryBits: 0x02, maskBits: 0x01, groupIndex: 0 };
            expect(shouldShapeFiltersCollide(a, b)).toBe(false);
        });

        it('shouldShapeFiltersCollide uses groupIndex when both non-zero and equal', () => {
            const pos = { categoryBits: 1, maskBits: 1, groupIndex: 1 };
            const posB = { categoryBits: 1, maskBits: 1, groupIndex: 1 };
            expect(shouldShapeFiltersCollide(pos, posB)).toBe(true);

            const neg = { categoryBits: 1, maskBits: 1, groupIndex: -1 };
            const negB = { categoryBits: 1, maskBits: 1, groupIndex: -1 };
            expect(shouldShapeFiltersCollide(neg, negB)).toBe(false);
        });

        it('supportsQueryFilter returns true when no filter given', () => {
            expect(supportsQueryFilter({ categoryBits: 1, maskBits: 1, groupIndex: 0 })).toBe(true);
        });

        it('supportsQueryFilter checks category bits', () => {
            expect(supportsQueryFilter(
                { categoryBits: 0x01, maskBits: 0xffff, groupIndex: 0 },
                { categoryBits: 0x02 }
            )).toBe(false);
        });
    });

    describe('buildOrthonormalBasis', () => {
        it('produces two tangent vectors perpendicular to the normal', () => {
            const normal = { x: 0, y: 1, z: 0 };
            const { tangent1, tangent2 } = buildOrthonormalBasis(normal);
            expect(Vec3.dot(tangent1, normal)).toBeCloseTo(0, 5);
            expect(Vec3.dot(tangent2, normal)).toBeCloseTo(0, 5);
            expect(Vec3.dot(tangent1, tangent2)).toBeCloseTo(0, 5);
        });
    });

    describe('getAxisVector', () => {
        it('returns X for axis 0', () => {
            expect(getAxisVector(0)).toEqual({ x: 1, y: 0, z: 0 });
        });
        it('returns Y for axis 1 (default)', () => {
            expect(getAxisVector(1)).toEqual({ x: 0, y: 1, z: 0 });
            expect(getAxisVector(undefined)).toEqual({ x: 0, y: 1, z: 0 });
        });
        it('returns Z for axis 2', () => {
            expect(getAxisVector(2)).toEqual({ x: 0, y: 0, z: 1 });
        });
    });

    describe('cylinderConeLocalHalfExtents', () => {
        it('returns correct half extents for X axis', () => {
            const h = cylinderConeLocalHalfExtents(0, 2, 6);
            expect(h).toEqual({ x: 3, y: 2, z: 2 });
        });

        it('returns correct half extents for Y axis (default)', () => {
            const h = cylinderConeLocalHalfExtents(1, 1.5, 4);
            expect(h).toEqual({ x: 1.5, y: 2, z: 1.5 });
        });

        it('returns correct half extents for Z axis', () => {
            const h = cylinderConeLocalHalfExtents(2, 3, 10);
            expect(h).toEqual({ x: 3, y: 3, z: 5 });
        });
    });

    describe('getBoxWorldExtents', () => {
        it('returns same half extents for identity rotation', () => {
            const r = getBoxWorldExtents({ x: 1, y: 2, z: 3 }, IDENTITY_ROTATION);
            expect(r.x).toBeCloseTo(1, 5);
            expect(r.y).toBeCloseTo(2, 5);
            expect(r.z).toBeCloseTo(3, 5);
        });
    });

    describe('linePointDistanceSquared', () => {
        it('returns 0 for a point on the line', () => {
            const d = linePointDistanceSquared({ x: 0.5, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, { x: 1, y: 0, z: 0 });
            expect(d).toBeCloseTo(0, 5);
        });

        it('returns correct distance for a point off the line', () => {
            const d = linePointDistanceSquared({ x: 0.5, y: 1, z: 0 }, { x: 0, y: 0, z: 0 }, { x: 1, y: 0, z: 0 });
            expect(d).toBeCloseTo(1, 5);
        });

        it('handles degenerate zero-length line', () => {
            const d = linePointDistanceSquared({ x: 1, y: 0, z: 0 }, { x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: 0 });
            expect(d).toBeCloseTo(1, 5);
        });
    });

    describe('triangleNormal', () => {
        it('returns unit normal for a triangle in the XY plane', () => {
            const n = triangleNormal({ x: 0, y: 0, z: 0 }, { x: 1, y: 0, z: 0 }, { x: 0, y: 1, z: 0 });
            expect(n.z).toBeCloseTo(1, 5);
        });
    });

    describe('ray casting utilities', () => {
        it('rayTriangleHit detects a hit', () => {
            const hit = rayTriangleHit(
                { x: 0.25, y: 0.25, z: -1 },
                { x: 0, y: 0, z: 1 },
                { x: 0, y: 0, z: 0 },
                { x: 1, y: 0, z: 0 },
                { x: 0, y: 1, z: 0 },
                10
            );
            expect(hit).not.toBeNull();
            expect(hit!.fraction).toBeCloseTo(1, 3);
        });

        it('rayTriangleHit returns null for a miss', () => {
            const hit = rayTriangleHit(
                { x: 5, y: 5, z: -1 },
                { x: 0, y: 0, z: 1 },
                { x: 0, y: 0, z: 0 },
                { x: 1, y: 0, z: 0 },
                { x: 0, y: 1, z: 0 },
                10
            );
            expect(hit).toBeNull();
        });

        it('raySphereHit detects a hit', () => {
            const hit = raySphereHit(
                { x: -5, y: 0, z: 0 },
                { x: 1, y: 0, z: 0 },
                { x: 0, y: 0, z: 0 },
                1,
                10
            );
            expect(hit).not.toBeNull();
            expect(hit!.fraction).toBeCloseTo(4, 3);
        });

        it('raySphereHit returns null for a miss', () => {
            const hit = raySphereHit(
                { x: -5, y: 5, z: 0 },
                { x: 1, y: 0, z: 0 },
                { x: 0, y: 0, z: 0 },
                1,
                10
            );
            expect(hit).toBeNull();
        });

        it('rayAabbHit detects a hit', () => {
            const hit = rayAabbHit(
                { x: -5, y: 0, z: 0 },
                { x: 1, y: 0, z: 0 },
                { x: -1, y: -1, z: -1 },
                { x: 1, y: 1, z: 1 },
                10
            );
            expect(hit).not.toBeNull();
            expect(hit!.fraction).toBeCloseTo(4, 3);
        });

        it('rayAabbHit returns null for a miss', () => {
            const hit = rayAabbHit(
                { x: -5, y: 5, z: 0 },
                { x: 1, y: 0, z: 0 },
                { x: -1, y: -1, z: -1 },
                { x: 1, y: 1, z: 1 },
                10
            );
            expect(hit).toBeNull();
        });
    });

    describe('height field vertex', () => {
        it('computes correct local vertex for center of flat height field', () => {
            const def = { heights: new Float32Array([0, 0, 0, 0]), width: 2, depth: 2, scaleX: 1, scaleY: 1, scaleZ: 1 };
            const v = getHeightFieldLocalVertex(def, 0, 0);
            expect(v.y).toBeCloseTo(0, 5);
        });
    });

    describe('type guards', () => {
        it('isSphereDef identifies sphere defs', () => {
            expect(isSphereDef({ kind: SHAPE_TYPE_SPHERE, center: { x: 0, y: 0, z: 0 }, radius: 1 } as any)).toBe(true);
            expect(isBoxDef({ kind: SHAPE_TYPE_SPHERE, center: { x: 0, y: 0, z: 0 }, radius: 1 } as any)).toBe(false);
        });

        it('isBoxDef identifies box defs', () => {
            expect(isBoxDef({ kind: SHAPE_TYPE_BOX, center: { x: 0, y: 0, z: 0 }, halfExtents: { x: 1, y: 1, z: 1 } } as any)).toBe(true);
        });

        it('isCapsuleDef identifies capsule defs', () => {
            expect(isCapsuleDef({ kind: SHAPE_TYPE_CAPSULE } as any)).toBe(true);
        });

        it('constraint type guards work correctly', () => {
            expect(isFixedDef({ kind: CONSTRAINT_TYPE_FIXED } as any)).toBe(true);
            expect(isHingeDef({ kind: CONSTRAINT_TYPE_HINGE } as any)).toBe(true);
            expect(isSpringDef({ kind: CONSTRAINT_TYPE_SPRING } as any)).toBe(true);
            expect(isFixedDef({ kind: CONSTRAINT_TYPE_SPRING } as any)).toBe(false);
        });
    });
});
