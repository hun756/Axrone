import { describe, it, expect } from 'vitest';
import {
    RaycastHitComparator,
    RayBuilder2D,
    RayBuilder3D,
    LayerMaskBuilder,
    RaycastFlagsBuilder,
    interpolateHit2D,
    interpolateHit3D,
    createSphereCastOrigins3D,
    createBoxCastOrigins3D,
    RaycastFlags,
} from '../index';
import type { IRaycastHit2D, IRaycastHit3D, LayerMask } from '../index';
import type { IVec2Like, IVec3Like } from '@axrone/numeric';

const v2 = (x: number, y: number): IVec2Like => ({ x, y });
const v3 = (x: number, y: number, z: number): IVec3Like => ({ x, y, z });

function makeHit2D(overrides: Partial<IRaycastHit2D> = {}): IRaycastHit2D {
    return {
        bodyId: 0,
        shapeId: 0,
        point: v2(0, 0),
        normal: v2(0, 1),
        distance: 0,
        fraction: 0,
        layer: 0,
        ...overrides,
    };
}

function makeHit3D(overrides: Partial<IRaycastHit3D> = {}): IRaycastHit3D {
    return {
        bodyId: 0,
        shapeId: 0,
        point: v3(0, 0, 0),
        normal: v3(0, 1, 0),
        distance: 0,
        fraction: 0,
        triangleIndex: -1,
        barycentric: null,
        layer: 0,
        ...overrides,
    };
}

describe('RaycastHitComparator', () => {
    it('sortByDistance2D orders ascending', () => {
        const a = makeHit2D({ distance: 5 });
        const b = makeHit2D({ distance: 2 });
        expect(RaycastHitComparator.sortByDistance2D(a, b)).toBeGreaterThan(0);
        expect(RaycastHitComparator.sortByDistance2D(b, a)).toBeLessThan(0);
        expect(RaycastHitComparator.sortByDistance2D(a, a)).toBe(0);
    });

    it('sortByDistance3D orders ascending', () => {
        const a = makeHit3D({ distance: 3 });
        const b = makeHit3D({ distance: 7 });
        expect(RaycastHitComparator.sortByDistance3D(a, b)).toBeLessThan(0);
    });

    it('sortByFraction2D orders ascending', () => {
        const a = makeHit2D({ fraction: 0.1 });
        const b = makeHit2D({ fraction: 0.9 });
        expect(RaycastHitComparator.sortByFraction2D(a, b)).toBeLessThan(0);
    });

    it('sortByFraction3D orders ascending', () => {
        const a = makeHit3D({ fraction: 0.5 });
        const b = makeHit3D({ fraction: 0.5 });
        expect(RaycastHitComparator.sortByFraction3D(a, b)).toBe(0);
    });

    it('filterByLayer2D keeps only matching layers', () => {
        // filterByLayer uses bitmask: (hit.layer & layerMask) !== 0
        // layer=1 (0b01) matches mask=1; layer=2 (0b10) does NOT match mask=1; layer=3 (0b11) matches mask=1
        const hits = [makeHit2D({ layer: 1 }), makeHit2D({ layer: 2 }), makeHit2D({ layer: 4 })];
        const filtered = RaycastHitComparator.filterByLayer2D(hits, 1);
        expect(filtered.length).toBe(1);
        expect(filtered[0].layer).toBe(1);
    });

    it('filterByLayer3D keeps matching layers via bitmask', () => {
        // mask=3 (0b11): layer=1 (0b01) matches, layer=2 (0b10) matches, layer=4 (0b100) does not
        const hits = [makeHit3D({ layer: 1 }), makeHit3D({ layer: 2 }), makeHit3D({ layer: 4 })];
        const filtered = RaycastHitComparator.filterByLayer3D(hits, 3 as LayerMask);
        expect(filtered.length).toBe(2);
    });

    it('findClosest2D returns null for empty array', () => {
        expect(RaycastHitComparator.findClosest2D([])).toBeNull();
    });

    it('findClosest2D returns closest hit', () => {
        const hits = [makeHit2D({ distance: 5 }), makeHit2D({ distance: 2 }), makeHit2D({ distance: 8 })];
        expect(RaycastHitComparator.findClosest2D(hits)!.distance).toBe(2);
    });

    it('findClosest3D returns closest hit', () => {
        const hits = [makeHit3D({ distance: 3 }), makeHit3D({ distance: 1 })];
        expect(RaycastHitComparator.findClosest3D(hits)!.distance).toBe(1);
    });

    it('findFurthest2D returns null for empty and furthest for non-empty', () => {
        expect(RaycastHitComparator.findFurthest2D([])).toBeNull();
        const hits = [makeHit2D({ distance: 5 }), makeHit2D({ distance: 10 }), makeHit2D({ distance: 3 })];
        expect(RaycastHitComparator.findFurthest2D(hits)!.distance).toBe(10);
    });

    it('findFurthest3D returns furthest hit', () => {
        const hits = [makeHit3D({ distance: 1 }), makeHit3D({ distance: 99 })];
        expect(RaycastHitComparator.findFurthest3D(hits)!.distance).toBe(99);
    });
});

describe('RayBuilder2D', () => {
    it('chains setters and builds a ray', () => {
        const b = new RayBuilder2D().setOrigin(1, 2).setDirection(1, 0).setLength(50);
        expect(b.origin.x).toBe(1);
        expect(b.origin.y).toBe(2);
        expect(b.length).toBe(50);
        expect(b.direction.x).toBeCloseTo(1, 5);
    });

    it('setOriginVec copies from vector', () => {
        const b = new RayBuilder2D().setOriginVec(v2(3, 4));
        expect(b.origin.x).toBe(3);
        expect(b.origin.y).toBe(4);
    });

    it('setDirectionVec normalizes direction', () => {
        const b = new RayBuilder2D().setDirectionVec(v2(3, 4));
        const len = Math.hypot(b.direction.x, b.direction.y);
        expect(len).toBeCloseTo(1, 5);
    });

    it('setTarget points direction toward target', () => {
        const b = new RayBuilder2D().setOrigin(0, 0).setTarget(v2(10, 0));
        expect(b.direction.x).toBeCloseTo(1, 5);
        expect(b.direction.y).toBeCloseTo(0, 5);
    });

    it('setAngle sets direction from radians', () => {
        const b = new RayBuilder2D().setAngle(Math.PI / 2);
        expect(b.direction.x).toBeCloseTo(0, 5);
        expect(b.direction.y).toBeCloseTo(1, 5);
    });

    it('getEndPoint returns origin + direction * length', () => {
        const b = new RayBuilder2D().setOrigin(0, 0).setDirection(1, 0).setLength(10);
        const ep = b.getEndPoint();
        expect(ep.x).toBeCloseTo(10, 5);
        expect(ep.y).toBeCloseTo(0, 5);
    });

    it('clone produces independent copy', () => {
        const b = new RayBuilder2D().setOrigin(1, 2).setLength(5);
        const c = b.clone();
        c.setOrigin(99, 99);
        expect(b.origin.x).toBe(1);
    });
});

describe('RayBuilder3D', () => {
    it('chains setters and builds a ray', () => {
        const b = new RayBuilder3D().setOrigin(1, 2, 3).setDirection(1, 0, 0).setLength(50);
        expect(b.origin.x).toBe(1);
        expect(b.length).toBe(50);
    });

    it('setOriginVec copies from vector', () => {
        const b = new RayBuilder3D().setOriginVec(v3(4, 5, 6));
        expect(b.origin.x).toBe(4);
        expect(b.origin.z).toBe(6);
    });

    it('setDirectionVec normalizes direction', () => {
        const b = new RayBuilder3D().setDirectionVec(v3(0, 3, 4));
        const len = Math.sqrt(b.direction.x ** 2 + b.direction.y ** 2 + b.direction.z ** 2);
        expect(len).toBeCloseTo(1, 5);
    });

    it('setTarget points direction toward target', () => {
        const b = new RayBuilder3D().setOrigin(0, 0, 0).setTarget(v3(10, 0, 0));
        expect(b.direction.x).toBeCloseTo(1, 5);
    });

    it('setEulerAngles produces unit direction', () => {
        const b = new RayBuilder3D().setEulerAngles(0.3, 0.7);
        const len = Math.sqrt(b.direction.x ** 2 + b.direction.y ** 2 + b.direction.z ** 2);
        expect(len).toBeCloseTo(1, 5);
    });

    it('getEndPoint returns origin + direction * length', () => {
        const b = new RayBuilder3D().setOrigin(0, 0, 0).setDirection(1, 0, 0).setLength(5);
        const ep = b.getEndPoint();
        expect(ep.x).toBeCloseTo(5, 5);
    });

    it('clone produces independent copy', () => {
        const b = new RayBuilder3D().setOrigin(1, 2, 3).setLength(10);
        const c = b.clone();
        c.setOrigin(0, 0, 0);
        expect(b.origin.x).toBe(1);
    });
});

describe('LayerMaskBuilder', () => {
    it('add/has/remove work correctly', () => {
        const b = new LayerMaskBuilder();
        b.add(1).add(2);
        expect(b.has(1)).toBe(true);
        expect(b.has(2)).toBe(true);
        expect(b.has(4)).toBe(false);
        b.remove(1);
        expect(b.has(1)).toBe(false);
    });

    it('toggle flips bits', () => {
        const b = new LayerMaskBuilder().add(1);
        b.toggle(1);
        expect(b.has(1)).toBe(false);
        b.toggle(1);
        expect(b.has(1)).toBe(true);
    });

    it('clear zeroes mask', () => {
        const b = new LayerMaskBuilder().add(1).add(2).clear();
        expect(b.has(1)).toBe(false);
        expect(b.build()).toBe(0);
    });

    it('setAll sets all bits', () => {
        const b = new LayerMaskBuilder().setAll();
        expect(b.has(1)).toBe(true);
        expect(b.has(0x80000000)).toBe(true);
    });

    it('static from builds mask from array', () => {
        const mask = LayerMaskBuilder.from([1, 4]);
        expect((mask as number) & 1).toBe(1);
        expect((mask as number) & 4).toBe(4);
    });

    it('static combine ORs masks', () => {
        const combined = LayerMaskBuilder.combine(1 as LayerMask, 2 as LayerMask);
        expect(combined).toBe(3);
    });

    it('static intersect ANDs masks', () => {
        const result = LayerMaskBuilder.intersect(3 as LayerMask, 2 as LayerMask);
        expect(result).toBe(2);
    });

    it('static intersect returns 0 for empty', () => {
        expect(LayerMaskBuilder.intersect()).toBe(0);
    });

    it('static exclude removes bits', () => {
        const result = LayerMaskBuilder.exclude(3 as LayerMask, 1 as LayerMask);
        expect(result).toBe(2);
    });
});

describe('RaycastFlagsBuilder', () => {
    it('add/has/remove work', () => {
        const b = new RaycastFlagsBuilder();
        b.add(RaycastFlags.ClosestOnly);
        expect(b.has(RaycastFlags.ClosestOnly)).toBe(true);
        b.remove(RaycastFlags.ClosestOnly);
        expect(b.has(RaycastFlags.ClosestOnly)).toBe(false);
    });

    it('toggle flips flags', () => {
        const b = new RaycastFlagsBuilder().add(RaycastFlags.AllHits);
        b.toggle(RaycastFlags.AllHits);
        expect(b.has(RaycastFlags.AllHits)).toBe(false);
    });

    it('clear zeroes flags', () => {
        const b = new RaycastFlagsBuilder().add(RaycastFlags.AllHits).clear();
        expect(b.build()).toBe(RaycastFlags.None);
    });

    it('static default includes ClosestOnly + StopAtFirstHit + SortByDistance', () => {
        const f = RaycastFlagsBuilder.default();
        expect(f & RaycastFlags.ClosestOnly).toBeTruthy();
        expect(f & RaycastFlags.StopAtFirstHit).toBeTruthy();
        expect(f & RaycastFlags.SortByDistance).toBeTruthy();
    });

    it('static allHits includes AllHits + SortByDistance', () => {
        const f = RaycastFlagsBuilder.allHits();
        expect(f & RaycastFlags.AllHits).toBeTruthy();
        expect(f & RaycastFlags.SortByDistance).toBeTruthy();
    });

    it('static precise includes ClosestOnly + PreciseHitNormal + StopAtFirstHit', () => {
        const f = RaycastFlagsBuilder.precise();
        expect(f & RaycastFlags.ClosestOnly).toBeTruthy();
        expect(f & RaycastFlags.PreciseHitNormal).toBeTruthy();
        expect(f & RaycastFlags.StopAtFirstHit).toBeTruthy();
    });
});

describe('interpolateHit2D', () => {
    const h1 = makeHit2D({ point: v2(0, 0), normal: v2(1, 0), distance: 2, fraction: 0.2 });
    const h2 = makeHit2D({ point: v2(10, 10), normal: v2(0, 1), distance: 8, fraction: 0.8 });

    it('t=0 returns hit1 values', () => {
        const r = interpolateHit2D(h1, h2, 0);
        expect(r.distance).toBeCloseTo(2, 5);
        expect(r.point!.x).toBeCloseTo(0, 5);
    });

    it('t=1 returns hit2 values', () => {
        const r = interpolateHit2D(h1, h2, 1);
        expect(r.distance).toBeCloseTo(8, 5);
        expect(r.point!.x).toBeCloseTo(10, 5);
    });

    it('t=0.5 returns midpoint', () => {
        const r = interpolateHit2D(h1, h2, 0.5);
        expect(r.distance).toBeCloseTo(5, 5);
        expect(r.point!.x).toBeCloseTo(5, 5);
        expect(r.point!.y).toBeCloseTo(5, 5);
    });
});

describe('interpolateHit3D', () => {
    const h1 = makeHit3D({ point: v3(0, 0, 0), normal: v3(1, 0, 0), distance: 4, fraction: 0.4 });
    const h2 = makeHit3D({ point: v3(10, 10, 10), normal: v3(0, 0, 1), distance: 12, fraction: 0.9 });

    it('t=0.5 returns midpoint', () => {
        const r = interpolateHit3D(h1, h2, 0.5);
        expect(r.distance).toBeCloseTo(8, 5);
        expect(r.point!.z).toBeCloseTo(5, 5);
    });
});

describe('createSphereCastOrigins3D', () => {
    it('returns the requested number of samples', () => {
        // Direction must not be parallel to (0,1,0) to avoid degenerate cross product
        const origins = createSphereCastOrigins3D(v3(0, 0, 0), v3(1, 0, 0), 1, 8);
        expect(origins.length).toBe(8);
    });

    it('all origins lie at radius distance from center in perpendicular plane', () => {
        const dir = v3(1, 0, 0);
        const origin = v3(0, 0, 0);
        const radius = 2;
        const origins = createSphereCastOrigins3D(origin, dir, radius, 12);
        for (const o of origins) {
            const dist = Math.sqrt(o.x ** 2 + o.y ** 2 + o.z ** 2);
            expect(dist).toBeCloseTo(radius, 4);
        }
    });
});

describe('createBoxCastOrigins3D', () => {
    it('returns 26 offsets (3x3x3 minus center)', () => {
        const origins = createBoxCastOrigins3D(v3(0, 0, 0), v3(1, 0, 0), v3(1, 1, 1));
        expect(origins.length).toBe(26);
    });

    it('offsets are symmetric around origin', () => {
        const origins = createBoxCastOrigins3D(v3(0, 0, 0), v3(1, 0, 0), v3(1, 2, 3));
        const xs = origins.map((o) => o.x).sort((a, b) => a - b);
        expect(xs[0]).toBeCloseTo(-1, 5);
        expect(xs[xs.length - 1]).toBeCloseTo(1, 5);
    });
});
