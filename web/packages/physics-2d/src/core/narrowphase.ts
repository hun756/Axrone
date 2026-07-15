import { Vec2, IVec2Like, EPSILON } from '@axrone/numeric';
import { ObjectPool } from '@axrone/memory';
import type { ShapeId, ContactId, IContactPoint2D } from '../types';
import type { IContactManifold2D } from '../types/collision';
import type { ShapeManager2D } from './shape-manager';
import { ShapeType } from '../types';
import { GJK2D, SAT2D } from './collision-algorithms';

const enum CollisionConfig {
    MAX_MANIFOLD_POINTS = 2,
    CONTACT_SLOP = 0.005,
    LINEAR_SLOP = 0.005,
    PERSISTENT_THRESHOLD_SQ = 0.0001,
}

interface CollisionContext {
    bodyIdA: number;
    bodyIdB: number;
    transformA: { position: IVec2Like; rotation: number };
    transformB: { position: IVec2Like; rotation: number };
}

type CollisionFn = (
    shapeA: any,
    shapeB: any,
    ctx: CollisionContext,
    manifold: WritableManifold
) => void;

interface WritableManifold {
    pointCount: number;
    normal: { x: number; y: number };
    points: Array<{
        id: ContactId;
        localPointA: { x: number; y: number };
        localPointB: { x: number; y: number };
        normalImpulse: number;
        tangentImpulse: number;
        separation: number;
    }>;
}

function setNormal(out: { x: number; y: number }, dx: number, dy: number): void {
    const d = Math.sqrt(dx * dx + dy * dy);
    if (d > EPSILON) {
        out.x = dx / d;
        out.y = dy / d;
    } else {
        out.x = 0;
        out.y = 1;
    }
}

function collideCircleCircle(
    circleA: { center: IVec2Like; radius: number },
    circleB: { center: IVec2Like; radius: number },
    ctx: CollisionContext,
    manifold: WritableManifold
): void {
    const worldCenterA = transformPoint(circleA.center, ctx.transformA);
    const worldCenterB = transformPoint(circleB.center, ctx.transformB);
    const dx = worldCenterB.x - worldCenterA.x;
    const dy = worldCenterB.y - worldCenterA.y;
    const distSq = dx * dx + dy * dy;
    const radiusSum = circleA.radius + circleB.radius;
    const radiusSumSq = radiusSum * radiusSum;
    if (distSq > radiusSumSq) {
        manifold.pointCount = 0;
        return;
    }
    const dist = Math.sqrt(distSq);
    if (dist < EPSILON) {
        manifold.normal.x = 0;
        manifold.normal.y = 1;
        const contactX = worldCenterA.x;
        const contactY = worldCenterA.y;
        manifold.pointCount = 1;
        const point = manifold.points[0];
        point.localPointA = inverseTransformPoint({ x: contactX, y: contactY }, ctx.transformA);
        point.localPointB = inverseTransformPoint({ x: contactX, y: contactY }, ctx.transformB);
        point.separation = -radiusSum;
        return;
    }
    const invDist = 1 / dist;
    manifold.normal.x = dx * invDist;
    manifold.normal.y = dy * invDist;
    const separation = dist - radiusSum;
    const contactX = worldCenterA.x + manifold.normal.x * circleA.radius;
    const contactY = worldCenterA.y + manifold.normal.y * circleA.radius;
    manifold.pointCount = 1;
    const point = manifold.points[0];
    point.localPointA = inverseTransformPoint({ x: contactX, y: contactY }, ctx.transformA);
    point.localPointB = inverseTransformPoint({ x: contactX, y: contactY }, ctx.transformB);
    point.separation = separation;
}

function collideBoxBox(
    boxA: { center: IVec2Like; halfWidth: number; halfHeight: number; rotation: number },
    boxB: { center: IVec2Like; halfWidth: number; halfHeight: number; rotation: number },
    ctx: CollisionContext,
    manifold: WritableManifold
): void {
    const verticesA = getBoxVertices(boxA);
    const verticesB = getBoxVertices(boxB);
    const result = SAT2D.testPolygonPolygon(verticesA, verticesB, ctx.transformA, ctx.transformB);
    if (!result.colliding) { manifold.pointCount = 0; return; }
    manifold.normal.x = result.normal.x;
    manifold.normal.y = result.normal.y;
    manifold.pointCount = 1;
    const contactPoint = findContactPoint(verticesA, verticesB, result.normal, ctx);
    const point = manifold.points[0];
    point.localPointA = inverseTransformPoint(contactPoint, ctx.transformA);
    point.localPointB = inverseTransformPoint(contactPoint, ctx.transformB);
    point.separation = -result.penetration;
}

function collidePolygonPolygon(
    polyA: { vertices: IVec2Like[] },
    polyB: { vertices: IVec2Like[] },
    ctx: CollisionContext,
    manifold: WritableManifold
): void {
    const result = SAT2D.testPolygonPolygon(polyA.vertices, polyB.vertices, ctx.transformA, ctx.transformB);
    if (!result.colliding) { manifold.pointCount = 0; return; }
    manifold.normal.x = result.normal.x;
    manifold.normal.y = result.normal.y;
    const contacts = findPolygonContacts(polyA.vertices, polyB.vertices, result.normal, ctx, result.penetration);
    manifold.pointCount = Math.min(contacts.length, CollisionConfig.MAX_MANIFOLD_POINTS);
    for (let i = 0; i < manifold.pointCount; i++) {
        const point = manifold.points[i];
        point.localPointA = inverseTransformPoint(contacts[i], ctx.transformA);
        point.localPointB = inverseTransformPoint(contacts[i], ctx.transformB);
        point.separation = -result.penetration;
    }
}

function collideCircleBox(
    circle: { center: IVec2Like; radius: number },
    box: { center: IVec2Like; halfWidth: number; halfHeight: number; rotation: number },
    ctx: CollisionContext,
    manifold: WritableManifold
): void {
    const worldCenter = transformPoint(circle.center, ctx.transformA);
    const boxVertices = getBoxVertices(box);
    const closestPoint = findClosestPointOnPolygon(worldCenter, boxVertices, ctx.transformB);
    const dx = worldCenter.x - closestPoint.x;
    const dy = worldCenter.y - closestPoint.y;
    const distSq = dx * dx + dy * dy;
    if (distSq > circle.radius * circle.radius) { manifold.pointCount = 0; return; }
    setNormal(manifold.normal, dx, dy);
    manifold.pointCount = 1;
    const point = manifold.points[0];
    point.localPointA = inverseTransformPoint(closestPoint, ctx.transformA);
    point.localPointB = inverseTransformPoint(closestPoint, ctx.transformB);
    point.separation = Math.sqrt(distSq) - circle.radius;
}

function collideCirclePolygon(
    circle: { center: IVec2Like; radius: number },
    poly: { vertices: IVec2Like[] },
    ctx: CollisionContext,
    manifold: WritableManifold
): void {
    const worldCenter = transformPoint(circle.center, ctx.transformA);
    const closestPoint = findClosestPointOnPolygon(worldCenter, poly.vertices, ctx.transformB);
    const dx = worldCenter.x - closestPoint.x;
    const dy = worldCenter.y - closestPoint.y;
    const distSq = dx * dx + dy * dy;
    if (distSq > circle.radius * circle.radius) { manifold.pointCount = 0; return; }
    setNormal(manifold.normal, dx, dy);
    manifold.pointCount = 1;
    const point = manifold.points[0];
    point.localPointA = inverseTransformPoint(closestPoint, ctx.transformA);
    point.localPointB = inverseTransformPoint(closestPoint, ctx.transformB);
    point.separation = Math.sqrt(distSq) - circle.radius;
}

function collideCapsuleCapsule(
    capsuleA: { p1: IVec2Like; p2: IVec2Like; radius: number },
    capsuleB: { p1: IVec2Like; p2: IVec2Like; radius: number },
    ctx: CollisionContext,
    manifold: WritableManifold
): void {
    const worldA1 = transformPoint(capsuleA.p1, ctx.transformA);
    const worldA2 = transformPoint(capsuleA.p2, ctx.transformA);
    const worldB1 = transformPoint(capsuleB.p1, ctx.transformB);
    const worldB2 = transformPoint(capsuleB.p2, ctx.transformB);
    const { pointA, pointB, distSq } = closestPointsSegmentSegment(worldA1, worldA2, worldB1, worldB2);
    const radiusSum = capsuleA.radius + capsuleB.radius;
    if (distSq > radiusSum * radiusSum) { manifold.pointCount = 0; return; }
    const dx = pointB.x - pointA.x;
    const dy = pointB.y - pointA.y;
    setNormal(manifold.normal, dx, dy);
    manifold.pointCount = 1;
    const contactX = (pointA.x + pointB.x) * 0.5;
    const contactY = (pointA.y + pointB.y) * 0.5;
    const point = manifold.points[0];
    point.localPointA = inverseTransformPoint({ x: contactX, y: contactY }, ctx.transformA);
    point.localPointB = inverseTransformPoint({ x: contactX, y: contactY }, ctx.transformB);
    point.separation = Math.sqrt(distSq) - radiusSum;
}

function collideCircleCapsule(
    circle: { center: IVec2Like; radius: number },
    capsule: { p1: IVec2Like; p2: IVec2Like; radius: number },
    ctx: CollisionContext,
    manifold: WritableManifold
): void {
    const worldCenter = transformPoint(circle.center, ctx.transformA);
    const worldP1 = transformPoint(capsule.p1, ctx.transformB);
    const worldP2 = transformPoint(capsule.p2, ctx.transformB);
    const closestPoint = closestPointOnSegment(worldCenter, worldP1, worldP2);
    const dx = worldCenter.x - closestPoint.x;
    const dy = worldCenter.y - closestPoint.y;
    const distSq = dx * dx + dy * dy;
    const radiusSum = circle.radius + capsule.radius;
    if (distSq > radiusSum * radiusSum) { manifold.pointCount = 0; return; }
    setNormal(manifold.normal, dx, dy);
    manifold.pointCount = 1;
    const point = manifold.points[0];
    point.localPointA = inverseTransformPoint(closestPoint, ctx.transformA);
    point.localPointB = inverseTransformPoint(closestPoint, ctx.transformB);
    point.separation = Math.sqrt(distSq) - radiusSum;
}

function collideCapsulePolygon(
    capsule: { p1: IVec2Like; p2: IVec2Like; radius: number },
    poly: { vertices: IVec2Like[] },
    ctx: CollisionContext,
    manifold: WritableManifold
): void {
    const worldP1 = transformPoint(capsule.p1, ctx.transformA);
    const worldP2 = transformPoint(capsule.p2, ctx.transformA);
    let minDistSq = Infinity;
    let closestOnPoly: IVec2Like = { x: 0, y: 0 };
    let closestOnSeg: IVec2Like = { x: 0, y: 0 };
    for (let i = 0; i < poly.vertices.length; i++) {
        const v0 = transformPoint(poly.vertices[i], ctx.transformB);
        const v1 = transformPoint(poly.vertices[(i + 1) % poly.vertices.length], ctx.transformB);
        const { pointA, pointB, distSq } = closestPointsSegmentSegment(worldP1, worldP2, v0, v1);
        if (distSq < minDistSq) {
            minDistSq = distSq;
            closestOnSeg = pointA;
            closestOnPoly = pointB;
        }
    }
    if (minDistSq > capsule.radius * capsule.radius) { manifold.pointCount = 0; return; }
    const dx = closestOnSeg.x - closestOnPoly.x;
    const dy = closestOnSeg.y - closestOnPoly.y;
    setNormal(manifold.normal, dx, dy);
    manifold.pointCount = 1;
    const point = manifold.points[0];
    point.localPointA = inverseTransformPoint(closestOnSeg, ctx.transformA);
    point.localPointB = inverseTransformPoint(closestOnPoly, ctx.transformB);
    point.separation = Math.sqrt(minDistSq) - capsule.radius;
}

function collideBoxCapsule(
    box: { center: IVec2Like; halfWidth: number; halfHeight: number; rotation: number },
    capsule: { p1: IVec2Like; p2: IVec2Like; radius: number },
    ctx: CollisionContext,
    manifold: WritableManifold
): void {
    const boxWorldVertices = getBoxVertices(box).map(v =>
        transformPoint({ x: v.x + box.center.x, y: v.y + box.center.y }, ctx.transformA)
    );
    const worldC1 = transformPoint(capsule.p1, ctx.transformB);
    const worldC2 = transformPoint(capsule.p2, ctx.transformB);
    let minDistSq = Infinity;
    let closestSeg: IVec2Like = { x: 0, y: 0 };
    let closestBox: IVec2Like = { x: 0, y: 0 };
    for (let i = 0; i < boxWorldVertices.length; i++) {
        const j = (i + 1) % boxWorldVertices.length;
        const { pointA, pointB, distSq } = closestPointsSegmentSegment(worldC1, worldC2, boxWorldVertices[i], boxWorldVertices[j]);
        if (distSq < minDistSq) { minDistSq = distSq; closestSeg = pointA; closestBox = pointB; }
    }
    if (minDistSq > capsule.radius * capsule.radius) { manifold.pointCount = 0; return; }
    const dx = closestSeg.x - closestBox.x;
    const dy = closestSeg.y - closestBox.y;
    setNormal(manifold.normal, dx, dy);
    manifold.pointCount = 1;
    const point = manifold.points[0];
    point.localPointA = inverseTransformPoint(closestBox, ctx.transformA);
    point.localPointB = inverseTransformPoint(closestSeg, ctx.transformB);
    point.separation = Math.sqrt(minDistSq) - capsule.radius;
}

const collideCapsuleCircle = (a: any, b: any, ctx: CollisionContext, m: WritableManifold) =>
    collideCircleCapsule(b, a, { ...ctx, transformA: ctx.transformB, transformB: ctx.transformA }, m);
const collidePolygonCircle = (a: any, b: any, ctx: CollisionContext, m: WritableManifold) =>
    collideCirclePolygon(b, a, { ...ctx, transformA: ctx.transformB, transformB: ctx.transformA }, m);
const collideBoxCircle = (a: any, b: any, ctx: CollisionContext, m: WritableManifold) =>
    collideCircleBox(b, a, { ...ctx, transformA: ctx.transformB, transformB: ctx.transformA }, m);
const collidePolygonCapsule = (a: any, b: any, ctx: CollisionContext, m: WritableManifold) =>
    collideCapsulePolygon(b, a, { ...ctx, transformA: ctx.transformB, transformB: ctx.transformA }, m);
const collideBoxPolygon = collidePolygonPolygon;
const collidePolygonBox = collideBoxPolygon;
const collideCapsuleBox = (a: any, b: any, ctx: CollisionContext, m: WritableManifold) =>
    collideBoxCapsule(b, a, { ...ctx, transformA: ctx.transformB, transformB: ctx.transformA }, m);

const COLLISION_MATRIX: ReadonlyArray<ReadonlyArray<CollisionFn | null>> = [
    [collideCircleCircle, collideCircleCapsule, collideCirclePolygon, collideCircleBox, null],
    [collideCapsuleCircle, collideCapsuleCapsule, collideCapsulePolygon, collideCapsuleBox, null],
    [collidePolygonCircle, collidePolygonCapsule, collidePolygonPolygon, collidePolygonBox, null],
    [collideBoxCircle, collideBoxCapsule, collideBoxPolygon, collideBoxBox, null],
    [null, null, null, null, null],
] as const;

export class Narrowphase2D {
    private readonly _tempVec: Vec2;
    private readonly _manifoldPool: ObjectPool<WritableManifold>;

    constructor() {
        this._tempVec = Vec2.ZERO.clone();
        this._manifoldPool = new ObjectPool<WritableManifold>({
            name: 'NarrowphaseManifoldPool',
            initialCapacity: 64,
            maxCapacity: 512,
            factory: (): WritableManifold => ({
                pointCount: 0,
                normal: { x: 0, y: 0 },
                points: Array.from({ length: CollisionConfig.MAX_MANIFOLD_POINTS }, (_, i) => ({
                    id: i as ContactId,
                    localPointA: { x: 0, y: 0 },
                    localPointB: { x: 0, y: 0 },
                    normalImpulse: 0,
                    tangentImpulse: 0,
                    separation: 0,
                })),
            }),
            resetHandler: (manifold: WritableManifold): void => {
                manifold.pointCount = 0;
                manifold.normal.x = 0;
                manifold.normal.y = 0;
                for (const point of manifold.points) {
                    point.normalImpulse = 0;
                    point.tangentImpulse = 0;
                    point.separation = 0;
                }
            },
        });
    }

    acquireManifold(): WritableManifold { return this._manifoldPool.acquire(); }
    releaseManifold(manifold: WritableManifold): void { this._manifoldPool.release(manifold); }
    dispose(): void { this._manifoldPool[Symbol.dispose](); }

    collide(
        shapeIdA: ShapeId,
        shapeIdB: ShapeId,
        typeA: ShapeType,
        typeB: ShapeType,
        shapeManager: ShapeManager2D,
        ctx: CollisionContext,
        manifold: IContactManifold2D
    ): void {
        const collisionFn = COLLISION_MATRIX[typeA]?.[typeB];
        if (!collisionFn) { (manifold as any).pointCount = 0; return; }
        const shapeA = this.getShapeData(shapeIdA, typeA, shapeManager);
        const shapeB = this.getShapeData(shapeIdB, typeB, shapeManager);
        collisionFn(shapeA, shapeB, ctx, manifold as any);
    }

    private getShapeData(shapeId: ShapeId, type: ShapeType, manager: ShapeManager2D): any {
        switch (type) {
            case ShapeType.Circle: return manager.getCircleData(shapeId);
            case ShapeType.Box: return manager.getBoxData(shapeId);
            case ShapeType.Polygon: return manager.getPolygonData(shapeId);
            case ShapeType.Capsule: return manager.getCapsuleData(shapeId);
            default: return null;
        }
    }
}

function transformPoint(point: IVec2Like, t: { position: IVec2Like; rotation: number }): IVec2Like {
    const c = Math.cos(t.rotation), s = Math.sin(t.rotation);
    return { x: c * point.x - s * point.y + t.position.x, y: s * point.x + c * point.y + t.position.y };
}

function inverseTransformPoint(point: IVec2Like, t: { position: IVec2Like; rotation: number }): IVec2Like {
    const dx = point.x - t.position.x, dy = point.y - t.position.y;
    const c = Math.cos(-t.rotation), s = Math.sin(-t.rotation);
    return { x: c * dx - s * dy, y: s * dx + c * dy };
}

function getBoxVertices(box: { center: IVec2Like; halfWidth: number; halfHeight: number; rotation: number }): IVec2Like[] {
    return [
        { x: -box.halfWidth, y: -box.halfHeight },
        { x: box.halfWidth, y: -box.halfHeight },
        { x: box.halfWidth, y: box.halfHeight },
        { x: -box.halfWidth, y: box.halfHeight },
    ];
}

function findContactPoint(verticesA: IVec2Like[], _verticesB: IVec2Like[], normal: IVec2Like, ctx: CollisionContext): IVec2Like {
    let maxDepth = -Infinity;
    let deepest: IVec2Like = { x: 0, y: 0 };
    for (const v of verticesA) {
        const wv = transformPoint(v, ctx.transformA);
        const depth = normal.x * wv.x + normal.y * wv.y;
        if (depth > maxDepth) { maxDepth = depth; deepest = wv; }
    }
    return deepest;
}

function findPolygonContacts(verticesA: readonly IVec2Like[], verticesB: readonly IVec2Like[], normal: IVec2Like, ctx: CollisionContext, penetration: number): IVec2Like[] {
    const contacts: IVec2Like[] = [];
    const threshold = penetration + CollisionConfig.CONTACT_SLOP;
    // Use a reference point from polygon B to compute depth relative to the contact plane
    const refVertex = transformPoint(verticesB[0], ctx.transformB);
    const refDot = normal.x * refVertex.x + normal.y * refVertex.y;
    for (const v of verticesA) {
        const wv = transformPoint(v, ctx.transformA);
        const depth = (normal.x * wv.x + normal.y * wv.y) - refDot;
        if (depth <= threshold) contacts.push(wv);
    }
    // Also check vertices from polygon B against polygon A
    const refVertexA = transformPoint(verticesA[0], ctx.transformA);
    const refDotA = normal.x * refVertexA.x + normal.y * refVertexA.y;
    for (const v of verticesB) {
        const wv = transformPoint(v, ctx.transformB);
        const depth = refDotA - (normal.x * wv.x + normal.y * wv.y);
        if (depth <= threshold) contacts.push(wv);
    }
    return contacts;
}

function findClosestPointOnPolygon(point: IVec2Like, vertices: readonly IVec2Like[], t: { position: IVec2Like; rotation: number }): IVec2Like {
    let minD = Infinity;
    let best: IVec2Like = { x: 0, y: 0 };
    for (let i = 0; i < vertices.length; i++) {
        const j = (i + 1) % vertices.length;
        const v1 = transformPoint(vertices[i], t), v2 = transformPoint(vertices[j], t);
        const cp = closestPointOnSegment(point, v1, v2);
        const dx = point.x - cp.x, dy = point.y - cp.y;
        const d2 = dx * dx + dy * dy;
        if (d2 < minD) { minD = d2; best = cp; }
    }
    return best;
}

function closestPointOnSegment(p: IVec2Like, a: IVec2Like, b: IVec2Like): IVec2Like {
    const abx = b.x - a.x, aby = b.y - a.y;
    const apx = p.x - a.x, apy = p.y - a.y;
    const ab2 = abx * abx + aby * aby;
    const t = ab2 > EPSILON ? Math.max(0, Math.min(1, (apx * abx + apy * aby) / ab2)) : 0;
    return { x: a.x + abx * t, y: a.y + aby * t };
}

function closestPointsSegmentSegment(a1: IVec2Like, a2: IVec2Like, b1: IVec2Like, b2: IVec2Like): { pointA: IVec2Like; pointB: IVec2Like; distSq: number } {
    const d1x = a2.x - a1.x, d1y = a2.y - a1.y;
    const d2x = b2.x - b1.x, d2y = b2.y - b1.y;
    const rx = a1.x - b1.x, ry = a1.y - b1.y;
    const a = d1x * d1x + d1y * d1y;
    const e = d2x * d2x + d2y * d2y;
    const f = d2x * rx + d2y * ry;
    let s = 0, t = 0;
    if (a < EPSILON && e < EPSILON) { s = t = 0; }
    else if (a < EPSILON) { t = Math.max(0, Math.min(1, f / e)); }
    else {
        const c = d1x * rx + d1y * ry;
        if (e < EPSILON) { s = Math.max(0, Math.min(1, -c / a)); }
        else {
            const b = d1x * d2x + d1y * d2y;
            const denom = a * e - b * b;
            if (denom !== 0) s = Math.max(0, Math.min(1, (b * f - c * e) / denom));
            t = (b * s + f) / e;
            if (t < 0) { t = 0; s = Math.max(0, Math.min(1, -c / a)); }
            else if (t > 1) { t = 1; s = Math.max(0, Math.min(1, (b - c) / a)); }
        }
    }
    const pA = { x: a1.x + d1x * s, y: a1.y + d1y * s };
    const pB = { x: b1.x + d2x * t, y: b1.y + d2y * t };
    const dx = pB.x - pA.x, dy = pB.y - pA.y;
    return { pointA: pA, pointB: pB, distSq: dx * dx + dy * dy };
}