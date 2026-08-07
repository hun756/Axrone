import { Vec2, Vec3, IVec2Like, IVec3Like, EPSILON } from '@axrone/numeric';
import { AABB2D, type IAABB } from '@axrone/geometry';
import {
    IRay2D,
    IRay3D,
    IRaycastHit2D,
    IRaycastHit3D,
    IRaycastQuery2D,
    IRaycastQuery3D,
    IRaycastResult2D,
    IRaycastResult3D,
    RaycastFlags,
    LayerMask,
    RaycastPredicate2D,
    RaycastPredicate3D,
    IBarycentricCoords,
} from '../types/raycast-types';
import type { BodyId, ShapeId } from '../types/primitives';
import { RayPrimitiveIntersector2D, RayPrimitiveIntersector3D } from './raycast-primitives';
import { ShapeType } from '@axrone/physics-core';

/**
 * Structural contract for a 2D broadphase that can answer AABB proxy queries.
 * `DynamicAABBTree2D` from `@axrone/physics-2d` satisfies this shape; keeping
 * it structural avoids a raycast -> physics-2d package dependency.
 */
export interface IRaycastBroadphaseSource2D {
    query(callback: (proxyId: number) => boolean, aabb: AABB2D): void;
    getUserData(proxyId: number): unknown;
    getAABB(proxyId: number): IAABB<IVec2Like>;
}

const RAYCAST_HIT_POOL_SIZE = 512;
const DEFAULT_MAX_HITS = 128;

class RaycastHit2D implements IRaycastHit2D {
    public bodyId!: BodyId;
    public shapeId!: ShapeId;
    public readonly point: Vec2 = Vec2.ZERO.clone();
    public readonly normal: Vec2 = Vec2.ZERO.clone();
    public distance!: number;
    public fraction!: number;
    public layer!: LayerMask;

    public reset(): void {
        this.bodyId = 0 as BodyId;
        this.shapeId = 0 as ShapeId;
        this.point.x = 0;
        this.point.y = 0;
        this.normal.x = 0;
        this.normal.y = 0;
        this.distance = 0;
        this.fraction = 0;
        this.layer = 0 as LayerMask;
    }

    public copyFrom(other: IRaycastHit2D): void {
        this.bodyId = other.bodyId;
        this.shapeId = other.shapeId;
        this.point.x = other.point.x;
        this.point.y = other.point.y;
        this.normal.x = other.normal.x;
        this.normal.y = other.normal.y;
        this.distance = other.distance;
        this.fraction = other.fraction;
        this.layer = other.layer;
    }
}

class RaycastHit3D implements IRaycastHit3D {
    public bodyId!: BodyId;
    public shapeId!: ShapeId;
    public readonly point: Vec3 = Vec3.ZERO.clone();
    public readonly normal: Vec3 = Vec3.ZERO.clone();
    public distance!: number;
    public fraction!: number;
    public triangleIndex!: number;
    public barycentric: IBarycentricCoords | null = null;
    public layer!: LayerMask;

    public reset(): void {
        this.bodyId = 0 as BodyId;
        this.shapeId = 0 as ShapeId;
        this.point.x = 0;
        this.point.y = 0;
        this.point.z = 0;
        this.normal.x = 0;
        this.normal.y = 0;
        this.normal.z = 0;
        this.distance = 0;
        this.fraction = 0;
        this.triangleIndex = -1;
        this.barycentric = null;
        this.layer = 0 as LayerMask;
    }

    public copyFrom(other: IRaycastHit3D): void {
        this.bodyId = other.bodyId;
        this.shapeId = other.shapeId;
        this.point.x = other.point.x;
        this.point.y = other.point.y;
        this.point.z = other.point.z;
        this.normal.x = other.normal.x;
        this.normal.y = other.normal.y;
        this.normal.z = other.normal.z;
        this.distance = other.distance;
        this.fraction = other.fraction;
        this.triangleIndex = other.triangleIndex;
        this.barycentric = other.barycentric
            ? { u: other.barycentric.u, v: other.barycentric.v }
            : null;
        this.layer = other.layer;
    }
}

class ObjectPool<T> {
    private readonly _pool: T[] = [];
    private readonly _factory: () => T;
    private readonly _reset: (item: T) => void;

    constructor(factory: () => T, reset: (item: T) => void, initialSize: number) {
        this._factory = factory;
        this._reset = reset;

        for (let i = 0; i < initialSize; i++) {
            this._pool.push(factory());
        }
    }

    public acquire(): T {
        if (this._pool.length > 0) {
            return this._pool.pop()!;
        }
        return this._factory();
    }

    public release(item: T): void {
        this._reset(item);
        this._pool.push(item);
    }

    public releaseAll(items: T[]): void {
        for (const item of items) {
            this.release(item);
        }
    }

    public get size(): number {
        return this._pool.length;
    }
}

export class RaycastResult2D implements IRaycastResult2D {
    private readonly _hits: RaycastHit2D[] = [];
    private _hitCount: number = 0;

    public get hits(): readonly IRaycastHit2D[] {
        return this._hits.slice(0, this._hitCount) as readonly IRaycastHit2D[];
    }

    public get hitCount(): number {
        return this._hitCount;
    }

    public get hasHit(): boolean {
        return this._hitCount > 0;
    }

    public addHit(hit: RaycastHit2D): void {
        if (this._hitCount < this._hits.length) {
            this._hits[this._hitCount].copyFrom(hit);
        } else {
            const newHit = new RaycastHit2D();
            newHit.copyFrom(hit);
            this._hits.push(newHit);
        }
        this._hitCount++;
    }

    public clear(): void {
        this._hitCount = 0;
    }

    // In-place insertion sort — avoids allocation on hot path
    public sort(): void {
        if (this._hitCount <= 1) return;
        for (let i = 1; i < this._hitCount; i++) {
            const key = this._hits[i];
            let j = i - 1;
            while (j >= 0 && this._hits[j].distance > key.distance) {
                this._hits[j + 1] = this._hits[j];
                j--;
            }
            this._hits[j + 1] = key;
        }
    }
}

export class RaycastResult3D implements IRaycastResult3D {
    private readonly _hits: RaycastHit3D[] = [];
    private _hitCount: number = 0;

    public get hits(): readonly IRaycastHit3D[] {
        return this._hits.slice(0, this._hitCount) as readonly IRaycastHit3D[];
    }

    public get hitCount(): number {
        return this._hitCount;
    }

    public get hasHit(): boolean {
        return this._hitCount > 0;
    }

    public addHit(hit: RaycastHit3D): void {
        if (this._hitCount < this._hits.length) {
            this._hits[this._hitCount].copyFrom(hit);
        } else {
            const newHit = new RaycastHit3D();
            newHit.copyFrom(hit);
            this._hits.push(newHit);
        }
        this._hitCount++;
    }

    public clear(): void {
        this._hitCount = 0;
    }

    // In-place insertion sort — avoids allocation on hot path
    public sort(): void {
        if (this._hitCount <= 1) return;
        for (let i = 1; i < this._hitCount; i++) {
            const key = this._hits[i];
            let j = i - 1;
            while (j >= 0 && this._hits[j].distance > key.distance) {
                this._hits[j + 1] = this._hits[j];
                j--;
            }
            this._hits[j + 1] = key;
        }
    }
}

interface ShapeData2D {
    bodyId: BodyId;
    shapeId: ShapeId;
    layer: LayerMask;
    type: ShapeType;
    data: unknown;
}

interface ShapeData3D {
    bodyId: BodyId;
    shapeId: ShapeId;
    layer: LayerMask;
    type: ShapeType;
    data: unknown;
}

export class Raycaster2D {
    private readonly _hitPool: ObjectPool<RaycastHit2D>;
    private readonly _tempHit: RaycastHit2D = new RaycastHit2D();
    private readonly _invDirection: Vec2 = Vec2.ZERO.clone();
    private readonly _aabbTestResult = { tMin: 0, tMax: 0 };

    private _shapes: ShapeData2D[] = [];
    private _broadphase: IRaycastBroadphaseSource2D | null = null;

    constructor() {
        this._hitPool = new ObjectPool(
            () => new RaycastHit2D(),
            (hit) => hit.reset(),
            RAYCAST_HIT_POOL_SIZE
        );
    }

    public setBroadphase(broadphase: IRaycastBroadphaseSource2D): void {
        this._broadphase = broadphase;
    }

    public registerShape(
        bodyId: BodyId,
        shapeId: ShapeId,
        layer: LayerMask,
        type: ShapeType,
        data: unknown
    ): void {
        this._shapes.push({ bodyId, shapeId, layer, type, data });
    }

    public unregisterShape(shapeId: ShapeId): void {
        const index = this._shapes.findIndex((s) => s.shapeId === shapeId);
        if (index !== -1) {
            this._shapes.splice(index, 1);
        }
    }

    public raycast(query: IRaycastQuery2D, predicate?: RaycastPredicate2D): RaycastResult2D {
        const result = new RaycastResult2D();

        const ray = query.ray;
        const maxHits = Math.min(query.maxHits || DEFAULT_MAX_HITS, DEFAULT_MAX_HITS);
        const closestOnly = (query.flags & RaycastFlags.ClosestOnly) !== 0;
        const stopAtFirst = (query.flags & RaycastFlags.StopAtFirstHit) !== 0;

        this._computeInvDirection(ray.direction, this._invDirection);

        const candidates = this._broadphaseQuery(ray, query.layerMask);

        for (const shape of candidates) {
            if ((shape.layer & query.layerMask) === 0) continue;
            if (predicate && !predicate(shape.bodyId, shape.shapeId)) continue;

            const hit = this._hitPool.acquire();
            const intersected = this._intersectShape2D(ray, shape, query.flags, hit);
            if (intersected) {
                result.addHit(hit);
                this._hitPool.release(hit);

                if (stopAtFirst) break;
                if (result.hitCount >= maxHits) break;
            } else {
                this._hitPool.release(hit);
            }
        }

        if ((query.flags & RaycastFlags.SortByDistance) !== 0) {
            result.sort();
        }

        if (closestOnly && result.hitCount > 1) {
            const closestHit = result.hits[0];
            result.clear();
            this._tempHit.copyFrom(closestHit as RaycastHit2D);
            result.addHit(this._tempHit);
        }

        return result;
    }

    public raycastSingle(
        origin: Readonly<IVec2Like>,
        direction: Readonly<IVec2Like>,
        maxDistance: number,
        layerMask: LayerMask,
        predicate?: RaycastPredicate2D
    ): IRaycastHit2D | null {
        const query: IRaycastQuery2D = {
            ray: {
                origin,
                direction,
                length: maxDistance,
            },
            layerMask,
            flags: RaycastFlags.ClosestOnly | RaycastFlags.StopAtFirstHit,
            maxHits: 1,
        };

        const result = this.raycast(query, predicate);
        return result.hasHit ? result.hits[0] : null;
    }

    private _computeInvDirection(direction: Readonly<IVec2Like>, out: Vec2): void {
        out.x = Math.abs(direction.x) > EPSILON ? 1.0 / direction.x : Number.MAX_VALUE;
        out.y = Math.abs(direction.y) > EPSILON ? 1.0 / direction.y : Number.MAX_VALUE;
    }

    private _broadphaseQuery(ray: IRay2D, layerMask: LayerMask): ShapeData2D[] {
        if (this._broadphase) {
            const candidates: ShapeData2D[] = [];
            const invDir = this._invDirection;
            const aabbResult = { tMin: 0, tMax: 0 };
            this._broadphase.query(
                (proxyId) => {
                    const userData = this._broadphase!.getUserData(proxyId) as ShapeId;
                    const shape = this._shapes.find((s) => s.shapeId === userData);
                    if (shape && (shape.layer & layerMask) !== 0) {
                        const aabb = this._broadphase!.getAABB(proxyId);
                        if (
                            RayPrimitiveIntersector2D.intersectAABB(
                                ray.origin,
                                invDir,
                                aabb,
                                ray.length,
                                aabbResult
                            )
                        ) {
                            candidates.push(shape);
                        }
                    }
                    return true;
                },
                // Query with a fat AABB covering the full ray
                (() => {
                    const endX = ray.origin.x + ray.direction.x * ray.length;
                    const endY = ray.origin.y + ray.direction.y * ray.length;
                    return new AABB2D(
                        { x: Math.min(ray.origin.x, endX), y: Math.min(ray.origin.y, endY) },
                        { x: Math.max(ray.origin.x, endX), y: Math.max(ray.origin.y, endY) }
                    );
                })()
            );
            return candidates;
        }
        return this._shapes.filter((s) => (s.layer & layerMask) !== 0);
    }

    private _intersectShape2D(
        ray: IRay2D,
        shape: ShapeData2D,
        flags: RaycastFlags,
        out: RaycastHit2D
    ): boolean {
        let result;

        switch (shape.type) {
            case ShapeType.Circle: {
                const d = shape.data as { center: { x: number; y: number }; radius: number };
                result = RayPrimitiveIntersector2D.intersectCircle(
                    ray.origin, ray.direction, d.center, d.radius, ray.length
                );
                if (!result.hit) return false;
                out.point.x = ray.origin.x + ray.direction.x * result.distance;
                out.point.y = ray.origin.y + ray.direction.y * result.distance;
                const dx = out.point.x - d.center.x;
                const dy = out.point.y - d.center.y;
                const invR = d.radius > EPSILON ? 1 / d.radius : 0;
                out.normal.x = dx * invR;
                out.normal.y = dy * invR;
                break;
            }
            case ShapeType.Box: {
                const d = shape.data as { center: { x: number; y: number }; halfWidth: number; halfHeight: number; rotation: number };
                result = RayPrimitiveIntersector2D.intersectBox(
                    ray.origin, ray.direction, d.center,
                    { x: d.halfWidth, y: d.halfHeight }, d.rotation, ray.length
                );
                if (!result.hit) return false;
                out.point.x = ray.origin.x + ray.direction.x * result.distance;
                out.point.y = ray.origin.y + ray.direction.y * result.distance;
                // Compute face normal from local hit point
                const cos = Math.cos(-d.rotation);
                const sin = Math.sin(-d.rotation);
                const lx = (out.point.x - d.center.x) * cos - (out.point.y - d.center.y) * sin;
                const ly = (out.point.x - d.center.x) * sin + (out.point.y - d.center.y) * cos;
                const ax = Math.abs(lx) / d.halfWidth;
                const ay = Math.abs(ly) / d.halfHeight;
                if (ax > ay) {
                    out.normal.x = lx > 0 ? Math.cos(d.rotation) : -Math.cos(d.rotation);
                    out.normal.y = lx > 0 ? Math.sin(d.rotation) : -Math.sin(d.rotation);
                } else {
                    out.normal.x = ly > 0 ? -Math.sin(d.rotation) : Math.sin(d.rotation);
                    out.normal.y = ly > 0 ? Math.cos(d.rotation) : -Math.cos(d.rotation);
                }
                break;
            }
            case ShapeType.Capsule: {
                const d = shape.data as { p1: { x: number; y: number }; p2: { x: number; y: number }; radius: number };
                result = RayPrimitiveIntersector2D.intersectCapsule(
                    ray.origin, ray.direction, d.p1, d.p2, d.radius, ray.length
                );
                if (!result.hit) return false;
                out.point.x = ray.origin.x + ray.direction.x * result.distance;
                out.point.y = ray.origin.y + ray.direction.y * result.distance;
                // Normal: closest point on segment axis to hit point
                const abx = d.p2.x - d.p1.x;
                const aby = d.p2.y - d.p1.y;
                const ab2 = abx * abx + aby * aby;
                const t = ab2 > EPSILON ? Math.max(0, Math.min(1, ((out.point.x - d.p1.x) * abx + (out.point.y - d.p1.y) * aby) / ab2)) : 0;
                const closestX = d.p1.x + abx * t;
                const closestY = d.p1.y + aby * t;
                const nx = out.point.x - closestX;
                const ny = out.point.y - closestY;
                const nLen = Math.sqrt(nx * nx + ny * ny);
                out.normal.x = nLen > EPSILON ? nx / nLen : 0;
                out.normal.y = nLen > EPSILON ? ny / nLen : 1;
                break;
            }
            case ShapeType.Polygon: {
                const d = shape.data as { vertices: { x: number; y: number }[] };
                result = RayPrimitiveIntersector2D.intersectPolygon(
                    ray.origin, ray.direction, d.vertices, ray.length
                );
                if (!result.hit) return false;
                out.point.x = ray.origin.x + ray.direction.x * result.distance;
                out.point.y = ray.origin.y + ray.direction.y * result.distance;
                // Find the edge that was hit and compute its normal
                let bestDist = Number.MAX_VALUE;
                out.normal.x = 0;
                out.normal.y = 1;
                for (let i = 0; i < d.vertices.length; i++) {
                    const v0 = d.vertices[i];
                    const v1 = d.vertices[(i + 1) % d.vertices.length];
                    const edgeHit = RayPrimitiveIntersector2D.intersectSegment(
                        ray.origin, ray.direction, v0, v1, ray.length
                    );
                    if (edgeHit.hit && Math.abs(edgeHit.distance - result.distance) < 1e-4) {
                        const ex = v1.x - v0.x;
                        const ey = v1.y - v0.y;
                        const eLen = Math.sqrt(ex * ex + ey * ey);
                        out.normal.x = eLen > EPSILON ? -ey / eLen : 0;
                        out.normal.y = eLen > EPSILON ? ex / eLen : 1;
                        break;
                    }
                }
                break;
            }
            default:
                return false;
        }

        out.bodyId = shape.bodyId;
        out.shapeId = shape.shapeId;
        out.distance = result.distance;
        out.fraction = result.fraction;
        out.layer = shape.layer;
        return true;
    }
}

export class Raycaster3D {
    private readonly _hitPool: ObjectPool<RaycastHit3D>;
    private readonly _tempHit: RaycastHit3D = new RaycastHit3D();
    private readonly _invDirection: Vec3 = Vec3.ZERO.clone();
    private readonly _aabbTestResult = { tMin: 0, tMax: 0 };

    private _shapes: ShapeData3D[] = [];

    constructor() {
        this._hitPool = new ObjectPool(
            () => new RaycastHit3D(),
            (hit) => hit.reset(),
            RAYCAST_HIT_POOL_SIZE
        );
    }

    public registerShape(
        bodyId: BodyId,
        shapeId: ShapeId,
        layer: LayerMask,
        type: ShapeType,
        data: unknown
    ): void {
        this._shapes.push({ bodyId, shapeId, layer, type, data });
    }

    public unregisterShape(shapeId: ShapeId): void {
        const index = this._shapes.findIndex((s) => s.shapeId === shapeId);
        if (index !== -1) {
            this._shapes.splice(index, 1);
        }
    }

    public raycast(query: IRaycastQuery3D, predicate?: RaycastPredicate3D): RaycastResult3D {
        const result = new RaycastResult3D();

        const ray = query.ray;
        const maxHits = Math.min(query.maxHits || DEFAULT_MAX_HITS, DEFAULT_MAX_HITS);
        const closestOnly = (query.flags & RaycastFlags.ClosestOnly) !== 0;
        const stopAtFirst = (query.flags & RaycastFlags.StopAtFirstHit) !== 0;

        this._computeInvDirection(ray.direction, this._invDirection);

        const candidates = this._broadphaseQuery(ray, query.layerMask);

        for (const shape of candidates) {
            if ((shape.layer & query.layerMask) === 0) continue;
            if (predicate && !predicate(shape.bodyId, shape.shapeId)) continue;

            const hit = this._hitPool.acquire();
            const intersected = this._intersectShape3D(ray, shape, query.flags, hit);
            if (intersected) {
                result.addHit(hit);
                this._hitPool.release(hit);

                if (stopAtFirst) break;
                if (result.hitCount >= maxHits) break;
            } else {
                this._hitPool.release(hit);
            }
        }

        if ((query.flags & RaycastFlags.SortByDistance) !== 0) {
            result.sort();
        }

        if (closestOnly && result.hitCount > 1) {
            const closestHit = result.hits[0];
            result.clear();
            this._tempHit.copyFrom(closestHit as RaycastHit3D);
            result.addHit(this._tempHit);
        }

        return result;
    }

    public raycastSingle(
        origin: Readonly<IVec3Like>,
        direction: Readonly<IVec3Like>,
        maxDistance: number,
        layerMask: LayerMask,
        predicate?: RaycastPredicate3D
    ): IRaycastHit3D | null {
        const query: IRaycastQuery3D = {
            ray: {
                origin,
                direction,
                length: maxDistance,
            },
            layerMask,
            flags: RaycastFlags.ClosestOnly | RaycastFlags.StopAtFirstHit,
            maxHits: 1,
        };

        const result = this.raycast(query, predicate);
        return result.hasHit ? result.hits[0] : null;
    }

    private _computeInvDirection(direction: Readonly<IVec3Like>, out: Vec3): void {
        out.x = Math.abs(direction.x) > EPSILON ? 1.0 / direction.x : Number.MAX_VALUE;
        out.y = Math.abs(direction.y) > EPSILON ? 1.0 / direction.y : Number.MAX_VALUE;
        out.z = Math.abs(direction.z) > EPSILON ? 1.0 / direction.z : Number.MAX_VALUE;
    }

    private _broadphaseQuery(ray: IRay3D, layerMask: LayerMask): ShapeData3D[] {
        return this._shapes.filter((s) => (s.layer & layerMask) !== 0);
    }

    private _intersectShape3D(
        ray: IRay3D,
        shape: ShapeData3D,
        flags: RaycastFlags,
        out: RaycastHit3D
    ): boolean {
        let result;

        switch (shape.type) {
            case ShapeType.Sphere: {
                const d = shape.data as { center: { x: number; y: number; z: number }; radius: number };
                result = RayPrimitiveIntersector3D.intersectSphere(
                    ray.origin, ray.direction, d.center, d.radius, ray.length
                );
                if (!result.hit) return false;
                out.point.x = ray.origin.x + ray.direction.x * result.distance;
                out.point.y = ray.origin.y + ray.direction.y * result.distance;
                out.point.z = ray.origin.z + ray.direction.z * result.distance;
                const dx = out.point.x - d.center.x;
                const dy = out.point.y - d.center.y;
                const dz = out.point.z - d.center.z;
                const invR = d.radius > EPSILON ? 1 / d.radius : 0;
                out.normal.x = dx * invR;
                out.normal.y = dy * invR;
                out.normal.z = dz * invR;
                break;
            }
            case ShapeType.Box: {
                const d = shape.data as { center: { x: number; y: number; z: number }; extents: { x: number; y: number; z: number } };
                result = RayPrimitiveIntersector3D.intersectBox(
                    ray.origin, ray.direction, d.center, d.extents, ray.length
                );
                if (!result.hit) return false;
                out.point.x = ray.origin.x + ray.direction.x * result.distance;
                out.point.y = ray.origin.y + ray.direction.y * result.distance;
                out.point.z = ray.origin.z + ray.direction.z * result.distance;
                // Face normal from local hit point
                const lx = out.point.x - d.center.x;
                const ly = out.point.y - d.center.y;
                const lz = out.point.z - d.center.z;
                const ax = Math.abs(lx) / d.extents.x;
                const ay = Math.abs(ly) / d.extents.y;
                const az = Math.abs(lz) / d.extents.z;
                if (ax > ay && ax > az) { out.normal.x = lx > 0 ? 1 : -1; out.normal.y = 0; out.normal.z = 0; }
                else if (ay > az) { out.normal.x = 0; out.normal.y = ly > 0 ? 1 : -1; out.normal.z = 0; }
                else { out.normal.x = 0; out.normal.y = 0; out.normal.z = lz > 0 ? 1 : -1; }
                break;
            }
            case ShapeType.Capsule: {
                const d = shape.data as { p0: { x: number; y: number; z: number }; p1: { x: number; y: number; z: number }; radius: number };
                result = RayPrimitiveIntersector3D.intersectCapsule(
                    ray.origin, ray.direction, d.p0, d.p1, d.radius, ray.length
                );
                if (!result.hit) return false;
                out.point.x = ray.origin.x + ray.direction.x * result.distance;
                out.point.y = ray.origin.y + ray.direction.y * result.distance;
                out.point.z = ray.origin.z + ray.direction.z * result.distance;
                const abx = d.p1.x - d.p0.x;
                const aby = d.p1.y - d.p0.y;
                const abz = d.p1.z - d.p0.z;
                const ab2 = abx * abx + aby * aby + abz * abz;
                const t3 = ab2 > EPSILON ? Math.max(0, Math.min(1, ((out.point.x - d.p0.x) * abx + (out.point.y - d.p0.y) * aby + (out.point.z - d.p0.z) * abz) / ab2)) : 0;
                const cx = d.p0.x + abx * t3;
                const cy = d.p0.y + aby * t3;
                const cz = d.p0.z + abz * t3;
                const nx = out.point.x - cx;
                const ny = out.point.y - cy;
                const nz = out.point.z - cz;
                const nLen = Math.sqrt(nx * nx + ny * ny + nz * nz);
                out.normal.x = nLen > EPSILON ? nx / nLen : 0;
                out.normal.y = nLen > EPSILON ? ny / nLen : 1;
                out.normal.z = nLen > EPSILON ? nz / nLen : 0;
                break;
            }
            default:
                return false;
        }

        out.bodyId = shape.bodyId;
        out.shapeId = shape.shapeId;
        out.distance = result.distance;
        out.fraction = result.fraction;
        out.triangleIndex = -1;
        out.barycentric = null;
        out.layer = shape.layer;
        return true;
    }
}
