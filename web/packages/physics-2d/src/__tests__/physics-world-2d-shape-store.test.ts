import { describe, it, expect, beforeEach } from 'vitest';
import { PhysicsWorld2DShapeStore } from '../core/physics-world-2d-shape-store';
import type { BodyId, ShapeId } from '../types';

function createMockBodyManager(overrides: Record<string, any> = {}) {
    return {
        hasBody: (_id: BodyId) => true,
        getPosition: (_id: BodyId) => ({ x: 0, y: 0 }),
        getRotation: (_id: BodyId) => 0,
        getBodyType: (_id: BodyId) => 2,
        isEnabled: (_id: BodyId) => true,
        getLocalCenter: (_id: BodyId) => ({ x: 0, y: 0 }),
        getMass: (_id: BodyId) => 1,
        getInertia: (_id: BodyId) => 0.5,
        getInverseMass: (_id: BodyId) => 1,
        getInverseInertia: (_id: BodyId) => 2,
        setMassData: () => {},
        ...overrides,
    } as any;
}

function createMockShapeManager() {
    const bodyShapes = new Map<BodyId, ShapeId[]>();
    return {
        getShapesForBody: (id: BodyId) => bodyShapes.get(id) ?? [],
        hasShape: (_id: ShapeId) => true,
        _registerShape: (bodyId: BodyId, shapeId: ShapeId) => {
            let shapes = bodyShapes.get(bodyId);
            if (!shapes) { shapes = []; bodyShapes.set(bodyId, shapes); }
            shapes.push(shapeId);
        },
    } as any;
}

describe('PhysicsWorld2DShapeStore', () => {
    let bm: any;
    let sm: any;
    let store: PhysicsWorld2DShapeStore;

    beforeEach(() => {
        bm = createMockBodyManager();
        sm = createMockShapeManager();
        store = new PhysicsWorld2DShapeStore(bm, sm);
    });

    describe('initial state', () => {
        it('starts with size 0', () => {
            expect(store.size).toBe(0);
        });

        it('hasShape returns false for unknown id', () => {
            expect(store.hasShape(999 as ShapeId)).toBe(false);
        });
    });

    describe('registerCircle', () => {
        it('registers a circle shape', () => {
            store.registerCircle(1 as ShapeId, 10 as BodyId, { radius: 2, center: { x: 0, y: 0 } } as any);
            expect(store.size).toBe(1);
            expect(store.hasShape(1 as ShapeId)).toBe(true);
        });

        it('stores correct descriptor', () => {
            store.registerCircle(1 as ShapeId, 10 as BodyId, { radius: 2, center: { x: 1, y: 2 } } as any);
            const desc = store.getDescriptor(1 as ShapeId);
            expect(desc?.type).toBe(0); // Circle
            expect(desc?.bodyId).toBe(10);
            expect(desc?.radius).toBe(2);
            expect(desc?.center).toEqual({ x: 1, y: 2 });
        });

        it('uses default material when none provided', () => {
            store.registerCircle(1 as ShapeId, 10 as BodyId, { radius: 1 } as any);
            const desc = store.getDescriptor(1 as ShapeId);
            expect(desc?.material.friction).toBe(0.2);
            expect(desc?.material.density).toBe(1);
        });

        it('uses custom material when provided', () => {
            store.registerCircle(1 as ShapeId, 10 as BodyId, {
                radius: 1, material: { friction: 0.8, restitution: 0.5, density: 3 },
            } as any);
            const desc = store.getDescriptor(1 as ShapeId);
            expect(desc?.material.friction).toBe(0.8);
            expect(desc?.material.density).toBe(3);
        });
    });

    describe('registerBox', () => {
        it('registers a box shape with halfWidth/halfHeight', () => {
            store.registerBox(2 as ShapeId, 10 as BodyId, { halfWidth: 3, halfHeight: 4 } as any);
            const desc = store.getDescriptor(2 as ShapeId);
            expect(desc?.type).toBe(3); // Box
            expect(desc?.halfWidth).toBe(3);
            expect(desc?.halfHeight).toBe(4);
        });

        it('computes halfWidth from width when halfWidth not given', () => {
            store.registerBox(2 as ShapeId, 10 as BodyId, { width: 6, height: 8 } as any);
            const desc = store.getDescriptor(2 as ShapeId);
            expect(desc?.halfWidth).toBe(3);
            expect(desc?.halfHeight).toBe(4);
        });
    });

    describe('registerPolygon', () => {
        it('registers a polygon with vertices', () => {
            const verts = [{ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 0, y: 1 }];
            store.registerPolygon(3 as ShapeId, 10 as BodyId, { vertices: verts } as any);
            const desc = store.getDescriptor(3 as ShapeId);
            expect(desc?.type).toBe(2); // Polygon
            expect(desc?.vertices).toHaveLength(3);
        });

        it('clones vertices to prevent external mutation', () => {
            const verts = [{ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 0, y: 1 }];
            store.registerPolygon(3 as ShapeId, 10 as BodyId, { vertices: verts } as any);
            verts[0].x = 999;
            const desc = store.getDescriptor(3 as ShapeId);
            expect(desc?.vertices![0].x).toBe(0);
        });
    });

    describe('registerCapsule', () => {
        it('registers a capsule shape', () => {
            store.registerCapsule(4 as ShapeId, 10 as BodyId, { radius: 1, length: 4 } as any);
            const desc = store.getDescriptor(4 as ShapeId);
            expect(desc?.type).toBe(1); // Capsule
            expect(desc?.radius).toBe(1);
            expect(desc?.length).toBe(4);
        });
    });

    describe('registerSegment', () => {
        it('registers a segment shape', () => {
            store.registerSegment(5 as ShapeId, 10 as BodyId, {
                start: { x: 0, y: 0 }, end: { x: 5, y: 0 },
            } as any);
            const desc = store.getDescriptor(5 as ShapeId);
            expect(desc?.type).toBe(4); // Segment
            expect(desc?.start).toEqual({ x: 0, y: 0 });
            expect(desc?.end).toEqual({ x: 5, y: 0 });
        });
    });

    describe('removeShape', () => {
        it('removes the shape and returns its descriptor', () => {
            store.registerCircle(1 as ShapeId, 10 as BodyId, { radius: 1 } as any);
            const removed = store.removeShape(1 as ShapeId);
            expect(removed).not.toBeNull();
            expect(store.size).toBe(0);
            expect(store.hasShape(1 as ShapeId)).toBe(false);
        });

        it('returns null for unknown shape', () => {
            expect(store.removeShape(999 as ShapeId)).toBeNull();
        });
    });

    describe('getDescriptor', () => {
        it('returns null for unknown shape', () => {
            expect(store.getDescriptor(999 as ShapeId)).toBeNull();
        });
    });

    describe('clear', () => {
        it('removes all shapes', () => {
            store.registerCircle(1 as ShapeId, 10 as BodyId, { radius: 1 } as any);
            store.registerCircle(2 as ShapeId, 10 as BodyId, { radius: 2 } as any);
            store.clear();
            expect(store.size).toBe(0);
        });
    });

    describe('entries', () => {
        it('iterates over all registered shapes', () => {
            store.registerCircle(1 as ShapeId, 10 as BodyId, { radius: 1 } as any);
            store.registerCircle(2 as ShapeId, 10 as BodyId, { radius: 2 } as any);
            const all = [...store.entries()];
            expect(all).toHaveLength(2);
        });
    });

    describe('getShapeView', () => {
        it('returns null for unknown shape', () => {
            expect(store.getShapeView(999 as ShapeId)).toBeNull();
        });

        it('returns a view with correct id and type', () => {
            store.registerCircle(1 as ShapeId, 10 as BodyId, { radius: 2 } as any);
            const view = store.getShapeView(1 as ShapeId)!;
            expect(view.id).toBe(1);
            expect(view.bodyId).toBe(10);
            expect(view.type).toBe(0); // Circle
        });

        it('returns the same view instance on repeated calls', () => {
            store.registerCircle(1 as ShapeId, 10 as BodyId, { radius: 1 } as any);
            const v1 = store.getShapeView(1 as ShapeId);
            const v2 = store.getShapeView(1 as ShapeId);
            expect(v1).toBe(v2);
        });

        it('view.material returns a clone', () => {
            store.registerCircle(1 as ShapeId, 10 as BodyId, {
                radius: 1, material: { friction: 0.5, restitution: 0.3, density: 1 },
            } as any);
            const view = store.getShapeView(1 as ShapeId)!;
            const m1 = view.material;
            const m2 = view.material;
            expect(m1).toEqual(m2);
            expect(m1).not.toBe(m2);
        });

        it('view.filter returns filter data', () => {
            store.registerCircle(1 as ShapeId, 10 as BodyId, { radius: 1 } as any);
            const view = store.getShapeView(1 as ShapeId)!;
            expect(view.filter.categoryBits).toBe(1);
            expect(view.filter.maskBits).toBe(0xffff);
        });

        it('view.isSensor defaults to false', () => {
            store.registerCircle(1 as ShapeId, 10 as BodyId, { radius: 1 } as any);
            const view = store.getShapeView(1 as ShapeId)!;
            expect(view.isSensor).toBe(false);
        });

        it('view.isSensor reflects registration', () => {
            store.registerCircle(1 as ShapeId, 10 as BodyId, { radius: 1, isSensor: true } as any);
            const view = store.getShapeView(1 as ShapeId)!;
            expect(view.isSensor).toBe(true);
        });
    });

    describe('queryAABBAll', () => {
        it('finds shapes overlapping a query AABB', () => {
            store.registerCircle(1 as ShapeId, 10 as BodyId, { radius: 1, center: { x: 0, y: 0 } } as any);
            const hits = store.queryAABBAll({ x: -2, y: -2 }, { x: 2, y: 2 });
            expect(hits).toContain(1);
        });

        it('excludes shapes outside the query AABB', () => {
            store.registerCircle(1 as ShapeId, 10 as BodyId, { radius: 1, center: { x: 0, y: 0 } } as any);
            store.registerCircle(2 as ShapeId, 10 as BodyId, { radius: 1, center: { x: 100, y: 100 } } as any);
            const hits = store.queryAABBAll({ x: -2, y: -2 }, { x: 2, y: 2 });
            expect(hits).toContain(1);
            expect(hits).not.toContain(2);
        });

        it('returns empty for no matches', () => {
            store.registerCircle(1 as ShapeId, 10 as BodyId, { radius: 1, center: { x: 0, y: 0 } } as any);
            const hits = store.queryAABBAll({ x: 50, y: 50 }, { x: 60, y: 60 });
            expect(hits).toHaveLength(0);
        });
    });

    describe('queryPointAll', () => {
        it('finds circles containing the point', () => {
            store.registerCircle(1 as ShapeId, 10 as BodyId, { radius: 5, center: { x: 0, y: 0 } } as any);
            const hits = store.queryPointAll({ x: 1, y: 1 });
            expect(hits).toContain(1);
        });

        it('excludes circles not containing the point', () => {
            store.registerCircle(1 as ShapeId, 10 as BodyId, { radius: 1, center: { x: 0, y: 0 } } as any);
            const hits = store.queryPointAll({ x: 5, y: 5 });
            expect(hits).not.toContain(1);
        });
    });

    describe('getProxyCount', () => {
        it('returns the number of registered shapes', () => {
            expect(store.getProxyCount()).toBe(0);
            store.registerCircle(1 as ShapeId, 10 as BodyId, { radius: 1 } as any);
            expect(store.getProxyCount()).toBe(1);
        });
    });

    describe('validate', () => {
        it('returns true when all bodies and shapes exist', () => {
            store.registerCircle(1 as ShapeId, 10 as BodyId, { radius: 1 } as any);
            expect(store.validate()).toBe(true);
        });

        it('returns false when body does not exist', () => {
            bm.hasBody = (id: BodyId) => id !== (10 as BodyId);
            store.registerCircle(1 as ShapeId, 10 as BodyId, { radius: 1 } as any);
            expect(store.validate()).toBe(false);
        });
    });

    describe('getBodyWorldCenter', () => {
        it('returns body position when local center is zero', () => {
            bm.getPosition = () => ({ x: 5, y: 10 });
            const center = store.getBodyWorldCenter(10 as BodyId);
            expect(center.x).toBeCloseTo(5, 4);
            expect(center.y).toBeCloseTo(10, 4);
        });
    });

    describe('sensor and filter', () => {
        it('registers sensor flag', () => {
            store.registerCircle(1 as ShapeId, 10 as BodyId, { radius: 1, isSensor: true } as any);
            const desc = store.getDescriptor(1 as ShapeId);
            expect(desc?.isSensor).toBe(true);
        });

        it('registers custom filter', () => {
            store.registerCircle(1 as ShapeId, 10 as BodyId, {
                radius: 1,
                filter: { categoryBits: 0x02, maskBits: 0x04, groupIndex: 1 },
            } as any);
            const desc = store.getDescriptor(1 as ShapeId);
            expect(desc?.filter.categoryBits).toBe(0x02);
            expect(desc?.filter.maskBits).toBe(0x04);
        });
    });
});
