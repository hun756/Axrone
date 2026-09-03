// Part 1
import { Vec3, Quat, clamp, type IVec3Like } from '@axrone/numeric';
import type {
    ContactId,
    ICollisionFilter,
    IContactManifold3D,
    Impulse,
    ManifoldId,
} from '../types';
import { PhysicsConstants } from '../types';
import type {
    BodyId3D,
    ConstraintId3D,
    IContactListener3D,
    IPhysicsProfiler3D,
    ShapeId3D,
} from '../types/physics-3d';
import { BodyManager3D } from './physics-managers-3d';
import { DynamicAABBTree3D } from './broadphase-3d';
import { AABB3D } from '@axrone/geometry';
import {
    BODY_TYPE_STATIC,
    CONSTRAINT_TYPE_SLIDER,
    CONSTRAINT_TYPE_SPRING,
    IDENTITY_ROTATION,
    SHAPE_TYPE_BOX,
    SHAPE_TYPE_CAPSULE,
    SHAPE_TYPE_CONE,
    SHAPE_TYPE_CONVEX_HULL,
    SHAPE_TYPE_CYLINDER,
    SHAPE_TYPE_SPHERE,
    SHAPE_TYPE_TRIANGLE_MESH,
    type IAabb3D,
    type IConstraintDescriptor3D,
    type IResolvedContactManifold3D,
    type IShapeDescriptor3D,
    type IMutableContactPoint3D,
    type IShapePairCandidate3D,
    type SupportedConstraintDef3D,
    type SupportedShapeDef3D,
    buildOrthonormalBasis,
    inverseTransformPoint3D,
    midpointVec3,
    shouldShapeFiltersCollide,
    transformPoint3D,
    isSphereDef,
    isBoxDef,
    isCapsuleDef,
    isCylinderDef,
    isConeDef,
    isConvexHullDef,
    isTriangleMeshDef,
} from './physics-world-3d-shared';

import { GJK3D, supportFromVertices, type Support3D } from './gjk3d';

export interface IPhysicsWorld3DContactRuntimeHost {
    readonly bodyManager: BodyManager3D;
    readonly shapeDescriptors: ReadonlyMap<ShapeId3D, IShapeDescriptor3D>;
    readonly constraintDescriptors: ReadonlyMap<ConstraintId3D, IConstraintDescriptor3D>;
    readonly getProfiler: () => IPhysicsProfiler3D | null;
    readonly getContactListener: () => IContactListener3D | null;
    readonly getCollisionFilter: () => ICollisionFilter | null;
    readonly computeShapeAabb: (descriptor: IShapeDescriptor3D) => IAabb3D;
    readonly getShapeWorldCenter: (descriptor: IShapeDescriptor3D) => IVec3Like;
    readonly getConstraintAnchor: (
        def: SupportedConstraintDef3D,
        firstBody: boolean
    ) => IVec3Like;
}

export class PhysicsWorld3DContactRuntime {
    private _nextContactId = 1 as ContactId;
    private _nextManifoldId = 1;
    private _contactManifolds = new Map<string, IResolvedContactManifold3D>();
    private readonly _broadphase = new DynamicAABBTree3D<ShapeId3D>(1024);
    private readonly _shapeProxyMap = new Map<ShapeId3D, number>();
    private readonly _shapePreviousCenter = new Map<ShapeId3D, IVec3Like>();

    /** Warm-start impulse cache keyed by pairKey (shapeIdA:shapeIdB). */
    private readonly _warmImpulses = new Map<string, { normal: number; tangent: number }>();
    private _lastIslandCount = 0;

    constructor(private readonly _host: IPhysicsWorld3DContactRuntimeHost) {}

    get contactCount(): number {
        return this._contactManifolds.size;
    }

    get islandCount(): number {
        return this._lastIslandCount;
    }

    pruneShape(shapeId: ShapeId3D): void {
        for (const [k, m] of this._contactManifolds) {
            if (m.shapeIdA === shapeId || m.shapeIdB === shapeId) this._contactManifolds.delete(k);
        }
        const proxy = this._shapeProxyMap.get(shapeId);
        if (proxy !== undefined) {
            this._broadphase.destroyProxy(proxy);
            this._shapeProxyMap.delete(shapeId);
        }
        this._shapePreviousCenter.delete(shapeId);
    }

    solve(deltaTime: number, velIters: number, posIters: number): void {
        const bStart = performance.now();
        const pairs = this._collectPotentialCollisionPairs();
        const bTime = performance.now() - bStart;
        const nStart = performance.now();
        const next = new Map<string, IResolvedContactManifold3D>();
        for (const pair of pairs) {
            const m = this._buildContactManifold(pair);
            if (!m) continue;
            const warm = this._warmImpulses.get(pair.pairKey);
            if (warm) {
                m.points[0].normalImpulse = warm.normal as Impulse;
                m.points[0].tangentImpulse1 = warm.tangent as Impulse;
            }
            next.set(pair.pairKey, m);
        }
        const nTime = performance.now() - nStart;
        const profiler = this._host.getProfiler();
        if (profiler) {
            profiler.broadphaseTime = bTime;
            profiler.narrowphaseTime = nTime;
            profiler.collisionTime = bTime + nTime;
        }

        // Build islands (connected components of bodies via contacts) and report the count.
        this._lastIslandCount = this._buildIslandCount(next);

        // Warm start: apply cached accumulated impulses to velocities before iterating.
        for (const m of next.values()) this._warmStartContact(m);

        const vStart = performance.now();
        for (let i = 0; i < velIters; i++) {
            for (const m of next.values()) this._solveContactVelocity(m);
        }
        if (profiler) profiler.solveVelocityTime = performance.now() - vStart;

        const pStart = performance.now();
        for (let i = 0; i < posIters; i++) {
            for (const m of next.values()) this._correctContactPositions(m, 0.2);
        }
        for (const m of next.values()) this._correctContactPositions(m, 1.0);
        if (profiler) profiler.solvePositionTime = performance.now() - pStart;

        // Persist accumulated impulses for next-frame warm starting.
        this._warmImpulses.clear();
        for (const m of next.values()) {
            const p = m.points[0];
            this._warmImpulses.set(m.pairKey, {
                normal: p.normalImpulse as number,
                tangent: p.tangentImpulse1 as number,
            });
        }

        this._contactManifolds = next;
    }

    private _collectPotentialCollisionPairs(): IShapePairCandidate3D[] {
        const candidates: IShapePairCandidate3D[] = [];
        const filter = this._host.getCollisionFilter();
        for (const d of this._host.shapeDescriptors.values()) {
            if (!this._host.bodyManager.isEnabled(d.bodyId)) continue;
            const rawAabb = this._host.computeShapeAabb(d);
            const aabb = new AABB3D(rawAabb.min, rawAabb.max);
            const currentCenter = {
                x: (aabb.min.x + aabb.max.x) * 0.5,
                y: (aabb.min.y + aabb.max.y) * 0.5,
                z: (aabb.min.z + aabb.max.z) * 0.5,
            };
            const existing = this._shapeProxyMap.get(d.id);
            if (existing !== undefined) {
                const prev = this._shapePreviousCenter.get(d.id) ?? currentCenter;
                const disp = {
                    x: currentCenter.x - prev.x,
                    y: currentCenter.y - prev.y,
                    z: currentCenter.z - prev.z,
                };
                this._broadphase.moveProxy(existing, aabb, disp);
            } else {
                const pid = this._broadphase.createProxy(aabb, d.id);
                this._shapeProxyMap.set(d.id, pid);
            }
            this._shapePreviousCenter.set(d.id, currentCenter);
        }
        this._broadphase.queryPairs((pA, pB) => {
            const sA = this._broadphase.getUserData(pA);
            const sB = this._broadphase.getUserData(pB);
            if (!sA || !sB) return true;
            const dA = this._host.shapeDescriptors.get(sA);
            const dB = this._host.shapeDescriptors.get(sB);
            if (!dA || !dB) return true;
            if (dA.bodyId === dB.bodyId) return true;
            if (!this._host.bodyManager.isEnabled(dA.bodyId)) return true;
            if (!this._host.bodyManager.isEnabled(dB.bodyId)) return true;
            if (!shouldShapeFiltersCollide(dA.filter, dB.filter)) return true;
            if (filter && !filter.shouldCollide(dA.id, dB.id)) return true;
            const tA = this._host.bodyManager.getBodyType(dA.bodyId);
            const tB = this._host.bodyManager.getBodyType(dB.bodyId);
            if (tA === BODY_TYPE_STATIC && tB === BODY_TYPE_STATIC) return true;
            candidates.push({
                descriptorA: dA, descriptorB: dB,
                aabbA: this._broadphase.getAABB(pA), aabbB: this._broadphase.getAABB(pB),
                pairKey: dA.id + ':' + dB.id,
            });
            return true;
        });
        return candidates;
    }

    private _buildContactManifold(pair: IShapePairCandidate3D): IResolvedContactManifold3D | null {
        const c = this._detectCollision(pair.descriptorA, pair.descriptorB, pair.aabbA, pair.aabbB);
        if (!c) return null;
        const { tangent1, tangent2 } = buildOrthonormalBasis(c.normal);
        const wA = this._getContactPointOnShape(pair.descriptorA, c, true);
        const wB = this._getContactPointOnShape(pair.descriptorB, c, false);
        const lA = inverseTransformPoint3D(wA, this._host.bodyManager.getPosition(pair.descriptorA.bodyId), this._host.bodyManager.getRotation(pair.descriptorA.bodyId));
        const lB = inverseTransformPoint3D(wB, this._host.bodyManager.getPosition(pair.descriptorB.bodyId), this._host.bodyManager.getRotation(pair.descriptorB.bodyId));
        const friction = Math.sqrt(pair.descriptorA.material.friction * pair.descriptorB.material.friction);
        const restitution = Math.max(pair.descriptorA.material.restitution, pair.descriptorB.material.restitution);
        return {
            id: (this._nextManifoldId++ as ManifoldId),
            pairKey: pair.pairKey, descriptorA: pair.descriptorA, descriptorB: pair.descriptorB,
            bodyIdA: pair.descriptorA.bodyId, bodyIdB: pair.descriptorB.bodyId,
            shapeIdA: pair.descriptorA.id, shapeIdB: pair.descriptorB.id,
            normal: c.normal, tangent1, tangent2, pointCount: 1,
            points: [{
                id: (this._nextContactId++ as unknown) as ContactId,
                localPointA: lA, localPointB: lB,
                normalImpulse: 0 as Impulse, tangentImpulse1: 0 as Impulse, tangentImpulse2: 0 as Impulse,
                separation: Vec3.dot(Vec3.subtract(wB, wA), c.normal),
            }],
            sensor: pair.descriptorA.isSensor || pair.descriptorB.isSensor,
            friction, restitution,
        };
    }

    private _getContactPointOnShape(d: IShapeDescriptor3D, c: { normal: IVec3Like; point: IVec3Like; penetration: number }, first: boolean): IVec3Like {
        if (isSphereDef(d.def)) {
            return Vec3.add(this._host.getShapeWorldCenter(d), Vec3.multiplyScalar(first ? c.normal : Vec3.negate(c.normal), d.def.radius));
        }
        return c.point;
    }

    private _detectCollision(dA: IShapeDescriptor3D, dB: IShapeDescriptor3D, aabbA: IAabb3D, aabbB: IAabb3D): { normal: IVec3Like; point: IVec3Like; penetration: number } | null {
        const tA = dA.type, tB = dB.type;
        if (tA === SHAPE_TYPE_SPHERE && tB === SHAPE_TYPE_SPHERE) return this._cSphSph(dA, dB);
        if (tA === SHAPE_TYPE_SPHERE && tB === SHAPE_TYPE_BOX) return this._cSphBox(dA, dB);
        if (tA === SHAPE_TYPE_BOX && tB === SHAPE_TYPE_SPHERE) { const k = this._cSphBox(dB, dA); return k ? { normal: Vec3.negate(k.normal), point: k.point, penetration: k.penetration } : null; }
        if (tA === SHAPE_TYPE_BOX && tB === SHAPE_TYPE_BOX) return this._cBoxBox(dA, dB);
        if (tA === SHAPE_TYPE_CAPSULE && tB === SHAPE_TYPE_CAPSULE) return this._cCapCap(dA, dB);
        if (tA === SHAPE_TYPE_CAPSULE && tB === SHAPE_TYPE_SPHERE) return this._cCapSph(dA, dB);
        if (tA === SHAPE_TYPE_SPHERE && tB === SHAPE_TYPE_CAPSULE) { const k = this._cCapSph(dB, dA); return k ? { normal: Vec3.negate(k.normal), point: k.point, penetration: k.penetration } : null; }
        if (tA === SHAPE_TYPE_CAPSULE && tB === SHAPE_TYPE_BOX) return this._cCapBox(dA, dB);
        if (tA === SHAPE_TYPE_BOX && tB === SHAPE_TYPE_CAPSULE) { const k = this._cCapBox(dB, dA); return k ? { normal: Vec3.negate(k.normal), point: k.point, penetration: k.penetration } : null; }
        // Convex hull / triangle mesh: real GJK/EPA narrowphase (replaces AABB fallback).
        if (
            tA === SHAPE_TYPE_CONVEX_HULL || tA === SHAPE_TYPE_TRIANGLE_MESH ||
            tB === SHAPE_TYPE_CONVEX_HULL || tB === SHAPE_TYPE_TRIANGLE_MESH
        ) {
            return this._cConvex(dA, dB, aabbA, aabbB);
        }
        return this._cAabbApprox(dA, dB, aabbA, aabbB);
    }

    /**
     * Generic convex-vs-convex narrowphase using GJK (collision) + EPA
     * (penetration). Works for any pair where at least one shape is a convex
     * hull or triangle mesh; the other shape is given an exact convex support
     * (sphere/box/capsule) or a vertex-set support (convex/mesh).
     */
    private _cConvex(
        dA: IShapeDescriptor3D,
        dB: IShapeDescriptor3D,
        aabbA: IAabb3D,
        aabbB: IAabb3D
    ): { normal: IVec3Like; point: IVec3Like; penetration: number } | null {
        const supportA = this._supportForShape(dA);
        const supportB = this._supportForShape(dB);
        if (!supportA || !supportB) {
            return this._cAabbApprox(dA, dB, aabbA, aabbB);
        }

        const result = GJK3D.intersect(supportA, supportB);
        if (!result.hit) return null;

        return {
            normal: { x: result.normal.x, y: result.normal.y, z: result.normal.z },
            point: { x: result.point.x, y: result.point.y, z: result.point.z },
            penetration: result.depth,
        };
    }

    /** Returns a convex support function for a shape descriptor, or null to fall back to AABB. */
    private _supportForShape(descriptor: IShapeDescriptor3D): Support3D | null {
        const bm = this._host.bodyManager;
        const pos = bm.getPosition(descriptor.bodyId);
        const rot = bm.getRotation(descriptor.bodyId);
        const def = descriptor.def;

        if (isSphereDef(def)) {
            const center = this._host.getShapeWorldCenter(descriptor);
            const r = def.radius;
            return (dir: IVec3Like): IVec3Like => {
                const len = Vec3.len(dir);
                const inv = len > 1e-6 ? r / len : 0;
                return { x: center.x + dir.x * inv, y: center.y + dir.y * inv, z: center.z + dir.z * inv };
            };
        }
        if (isBoxDef(def)) {
            const center = transformPoint3D(def.center, pos, rot);
            const halfExtents = def.halfExtents;
            const rotFull = Quat.multiply(rot, def.rotation ?? IDENTITY_ROTATION);
            const axes = [
                Quat.rotateVector(rotFull, { x: 1, y: 0, z: 0 }),
                Quat.rotateVector(rotFull, { x: 0, y: 1, z: 0 }),
                Quat.rotateVector(rotFull, { x: 0, y: 0, z: 1 }),
            ];
            const ext = [halfExtents.x, halfExtents.y, halfExtents.z];
            return (dir: IVec3Like): IVec3Like => {
                let x = center.x, y = center.y, z = center.z;
                for (let i = 0; i < 3; i++) {
                    const s = (dir.x * axes[i].x + dir.y * axes[i].y + dir.z * axes[i].z) >= 0 ? ext[i] : -ext[i];
                    x += axes[i].x * s;
                    y += axes[i].y * s;
                    z += axes[i].z * s;
                }
                return { x, y, z };
            };
        }
        if (isCapsuleDef(def)) {
            const p1 = transformPoint3D(def.p1, pos, rot);
            const p2 = transformPoint3D(def.p2, pos, rot);
            const r = def.radius;
            return (dir: IVec3Like): IVec3Like => {
                const d1 = dir.x * p1.x + dir.y * p1.y + dir.z * p1.z;
                const d2 = dir.x * p2.x + dir.y * p2.y + dir.z * p2.z;
                const base = d1 >= d2 ? p1 : p2;
                const len = Vec3.len(dir);
                const inv = len > 1e-6 ? r / len : 0;
                return { x: base.x + dir.x * inv, y: base.y + dir.y * inv, z: base.z + dir.z * inv };
            };
        }
        if (isConvexHullDef(def) || isTriangleMeshDef(def) || isCylinderDef(def) || isConeDef(def)) {
            const vertices = this._worldVerticesOf(descriptor);
            if (vertices.length === 0) return null;
            return supportFromVertices(vertices as IVec3Like[]);
        }
        return null;
    }

    private _worldVerticesOf(descriptor: IShapeDescriptor3D): IVec3Like[] {
        const bm = this._host.bodyManager;
        const pos = bm.getPosition(descriptor.bodyId);
        const rot = bm.getRotation(descriptor.bodyId);
        const def = descriptor.def;

        if (isConvexHullDef(def) || isTriangleMeshDef(def)) {
            return def.vertices.map((v) => transformPoint3D(v, pos, rot));
        }

        if (isCylinderDef(def) || isConeDef(def)) {
            const center = def.center ?? { x: 0, y: 0, z: 0 };
            const radius = def.radius ?? 0;
            const height = def.height ?? 0;
            const segments = 8;
            const c = transformPoint3D(center, pos, rot);
            const localY = Quat.rotateVector(rot, { x: 0, y: 1, z: 0 });
            const localX = Quat.rotateVector(rot, { x: 1, y: 0, z: 0 });
            const localZ = Quat.rotateVector(rot, { x: 0, y: 0, z: 1 });
            const ringOffset = Vec3.multiplyScalar(localY, height * 0.5);
            const top = Vec3.add(c, ringOffset);
            const bottom = Vec3.subtract(c, ringOffset);
            const verts: IVec3Like[] = [];
            for (let i = 0; i < segments; i++) {
                const a = (i / segments) * Math.PI * 2;
                const ox = Math.cos(a) * radius;
                const oz = Math.sin(a) * radius;
                const radial = Vec3.add(Vec3.multiplyScalar(localX, ox), Vec3.multiplyScalar(localZ, oz));
                verts.push(Vec3.add(top, radial));
                verts.push(Vec3.add(bottom, radial));
            }
            if (isConeDef(def)) {
                verts.push(Vec3.add(c, Vec3.multiplyScalar(localY, height)));
            }
            return verts;
        }

        return [];
    }

    private _cSphSph(dA: IShapeDescriptor3D, dB: IShapeDescriptor3D): { normal: IVec3Like; point: IVec3Like; penetration: number } | null {
        const cA = this._host.getShapeWorldCenter(dA), cB = this._host.getShapeWorldCenter(dB);
        const delta = Vec3.subtract(cB, cA);
        const dist = Vec3.len(delta);
        const rA = isSphereDef(dA.def) ? dA.def.radius : 0;
        const rB = isSphereDef(dB.def) ? dB.def.radius : 0;
        const rSum = rA + rB;
        if (dist > rSum) return null;
        const n = dist > PhysicsConstants.EPSILON ? Vec3.multiplyScalar(delta, 1 / dist) : { x: 1, y: 0, z: 0 };
        const pen = rSum - dist;
        return { normal: n, point: Vec3.add(cA, Vec3.multiplyScalar(n, rA - pen * 0.5)), penetration: pen };
    }

    private _cSphBox(s: IShapeDescriptor3D, b: IShapeDescriptor3D): { normal: IVec3Like; point: IVec3Like; penetration: number } | null {
        const sC = this._host.getShapeWorldCenter(s);
        const bP = this._host.bodyManager.getPosition(b.bodyId), bR = this._host.bodyManager.getRotation(b.bodyId);
        if (!isSphereDef(s.def) || !isBoxDef(b.def)) return null;
        const bC = transformPoint3D(b.def.center, bP, bR);
        const bRot = Quat.multiply(bR, b.def.rotation ?? IDENTITY_ROTATION);
        const localSC = inverseTransformPoint3D(sC, bC, bRot);
        const closestLocal = { x: clamp(localSC.x, -b.def.halfExtents.x, b.def.halfExtents.x), y: clamp(localSC.y, -b.def.halfExtents.y, b.def.halfExtents.y), z: clamp(localSC.z, -b.def.halfExtents.z, b.def.halfExtents.z) };
        const closestWorld = transformPoint3D(closestLocal, bC, bRot);
        const delta = Vec3.subtract(sC, closestWorld);
        const dist = Vec3.len(delta);
        const r = s.def.radius;
        if (dist > r) return null;
        if (dist > PhysicsConstants.EPSILON) return { normal: Vec3.multiplyScalar(delta, -1 / dist), point: closestWorld, penetration: r - dist };
        return { normal: { x: 0, y: 1, z: 0 }, point: closestWorld, penetration: r };
    }

    private _cBoxBox(dA: IShapeDescriptor3D, dB: IShapeDescriptor3D): { normal: IVec3Like; point: IVec3Like; penetration: number } | null {
        if (!isBoxDef(dA.def) || !isBoxDef(dB.def)) return null;
        const bDA = dA.def, bDB = dB.def;
        const cA = transformPoint3D(bDA.center, this._host.bodyManager.getPosition(dA.bodyId), this._host.bodyManager.getRotation(dA.bodyId));
        const cB = transformPoint3D(bDB.center, this._host.bodyManager.getPosition(dB.bodyId), this._host.bodyManager.getRotation(dB.bodyId));
        const rA = Quat.multiply(this._host.bodyManager.getRotation(dA.bodyId), bDA.rotation ?? IDENTITY_ROTATION);
        const rB = Quat.multiply(this._host.bodyManager.getRotation(dB.bodyId), bDB.rotation ?? IDENTITY_ROTATION);
        const xA = Quat.rotateVector(rA, { x: 1, y: 0, z: 0 }), yA = Quat.rotateVector(rA, { x: 0, y: 1, z: 0 }), zA = Quat.rotateVector(rA, { x: 0, y: 0, z: 1 });
        const xB = Quat.rotateVector(rB, { x: 1, y: 0, z: 0 }), yB = Quat.rotateVector(rB, { x: 0, y: 1, z: 0 }), zB = Quat.rotateVector(rB, { x: 0, y: 0, z: 1 });
        const axes = [xA, yA, zA, xB, yB, zB];
        const hA = bDA.halfExtents, hB = bDB.halfExtents;
        const delta = Vec3.subtract(cB, cA);
        let minP = Infinity; let bestN: IVec3Like = { x: 0, y: 1, z: 0 };
        for (const ax of axes) {
            const pA = hA.x * Math.abs(Vec3.dot(xA, ax)) + hA.y * Math.abs(Vec3.dot(yA, ax)) + hA.z * Math.abs(Vec3.dot(zA, ax));
            const pB = hB.x * Math.abs(Vec3.dot(xB, ax)) + hB.y * Math.abs(Vec3.dot(yB, ax)) + hB.z * Math.abs(Vec3.dot(zB, ax));
            const d = Math.abs(Vec3.dot(delta, ax));
            const pen = pA + pB - d;
            if (pen < 0) return null;
            if (pen < minP) { minP = pen; bestN = Vec3.dot(delta, ax) > 0 ? ax : Vec3.negate(ax); }
        }
        return { normal: bestN, point: midpointVec3(cA, cB), penetration: minP };
    }

    private _cCapCap(dA: IShapeDescriptor3D, dB: IShapeDescriptor3D): { normal: IVec3Like; point: IVec3Like; penetration: number } | null {
        if (!isCapsuleDef(dA.def) || !isCapsuleDef(dB.def)) return null;
        const p1A = transformPoint3D(dA.def.p1, this._host.bodyManager.getPosition(dA.bodyId), this._host.bodyManager.getRotation(dA.bodyId));
        const p2A = transformPoint3D(dA.def.p2, this._host.bodyManager.getPosition(dA.bodyId), this._host.bodyManager.getRotation(dA.bodyId));
        const p1B = transformPoint3D(dB.def.p1, this._host.bodyManager.getPosition(dB.bodyId), this._host.bodyManager.getRotation(dB.bodyId));
        const p2B = transformPoint3D(dB.def.p2, this._host.bodyManager.getPosition(dB.bodyId), this._host.bodyManager.getRotation(dB.bodyId));
        const closest = this._segSeg(p1A, p2A, p1B, p2B);
        const rSum = dA.def.radius + dB.def.radius;
        if (closest.distSq > rSum * rSum) return null;
        const dist = Math.sqrt(closest.distSq);
        const invD = dist > PhysicsConstants.EPSILON ? 1 / dist : 0;
        const dx = closest.pointB.x - closest.pointA.x, dy = closest.pointB.y - closest.pointA.y, dz = closest.pointB.z - closest.pointA.z;
        return { normal: { x: dx * invD, y: dy * invD, z: dz * invD }, point: midpointVec3(closest.pointA, closest.pointB), penetration: rSum - dist };
    }

    private _cCapSph(cap: IShapeDescriptor3D, sph: IShapeDescriptor3D): { normal: IVec3Like; point: IVec3Like; penetration: number } | null {
        if (!isCapsuleDef(cap.def) || !isSphereDef(sph.def)) return null;
        const p1 = transformPoint3D(cap.def.p1, this._host.bodyManager.getPosition(cap.bodyId), this._host.bodyManager.getRotation(cap.bodyId));
        const p2 = transformPoint3D(cap.def.p2, this._host.bodyManager.getPosition(cap.bodyId), this._host.bodyManager.getRotation(cap.bodyId));
        const sC = this._host.getShapeWorldCenter(sph);
        const closest = this._closestSeg(sC, p1, p2);
        const delta = Vec3.subtract(sC, closest);
        const dist = Vec3.len(delta);
        const rSum = cap.def.radius + sph.def.radius;
        if (dist > rSum) return null;
        const invD = dist > PhysicsConstants.EPSILON ? 1 / dist : 0;
        return { normal: { x: delta.x * invD, y: delta.y * invD, z: delta.z * invD }, point: closest, penetration: rSum - dist };
    }

    private _cCapBox(cap: IShapeDescriptor3D, box: IShapeDescriptor3D): { normal: IVec3Like; point: IVec3Like; penetration: number } | null {
        if (!isCapsuleDef(cap.def) || !isBoxDef(box.def)) return null;
        const p1 = transformPoint3D(cap.def.p1, this._host.bodyManager.getPosition(cap.bodyId), this._host.bodyManager.getRotation(cap.bodyId));
        const p2 = transformPoint3D(cap.def.p2, this._host.bodyManager.getPosition(cap.bodyId), this._host.bodyManager.getRotation(cap.bodyId));
        const bC = transformPoint3D(box.def.center, this._host.bodyManager.getPosition(box.bodyId), this._host.bodyManager.getRotation(box.bodyId));
        const bRot = Quat.multiply(this._host.bodyManager.getRotation(box.bodyId), box.def.rotation ?? IDENTITY_ROTATION);
        const l1 = inverseTransformPoint3D(p1, bC, bRot), l2 = inverseTransformPoint3D(p2, bC, bRot);
        const hE = box.def.halfExtents;
        const c1 = { x: clamp(l1.x, -hE.x, hE.x), y: clamp(l1.y, -hE.y, hE.y), z: clamp(l1.z, -hE.z, hE.z) };
        const c2 = { x: clamp(l2.x, -hE.x, hE.x), y: clamp(l2.y, -hE.y, hE.y), z: clamp(l2.z, -hE.z, hE.z) };
        const closest = this._closestSeg({ x: 0, y: 0, z: 0 }, c1, c2);
        const dist = Vec3.len(closest);
        if (dist > cap.def.radius) return null;
        const invD = dist > PhysicsConstants.EPSILON ? 1 / dist : 0;
        const localN = { x: closest.x * invD, y: closest.y * invD, z: closest.z * invD };
        return { normal: Quat.rotateVector(bRot, localN), point: transformPoint3D({ x: 0, y: 0, z: 0 }, bC, bRot), penetration: cap.def.radius - dist };
    }

    private _cAabbApprox(dA: IShapeDescriptor3D, dB: IShapeDescriptor3D, aabbA: IAabb3D, aabbB: IAabb3D): { normal: IVec3Like; point: IVec3Like; penetration: number } | null {
        const oX = Math.min(aabbA.max.x, aabbB.max.x) - Math.max(aabbA.min.x, aabbB.min.x);
        const oY = Math.min(aabbA.max.y, aabbB.max.y) - Math.max(aabbA.min.y, aabbB.min.y);
        const oZ = Math.min(aabbA.max.z, aabbB.max.z) - Math.max(aabbA.min.z, aabbB.min.z);
        if (oX < 0 || oY < 0 || oZ < 0) return null;
        const cA = this._host.getShapeWorldCenter(dA), cB = this._host.getShapeWorldCenter(dB);
        let pen = oX; if (oY < pen) pen = oY; if (oZ < pen) pen = oZ;
        let normal: IVec3Like;
        if (pen === oX) normal = { x: cB.x >= cA.x ? 1 : -1, y: 0, z: 0 };
        else if (pen === oY) normal = { x: 0, y: cB.y >= cA.y ? 1 : -1, z: 0 };
        else normal = { x: 0, y: 0, z: cB.z >= cA.z ? 1 : -1 };
        return { normal, penetration: pen, point: { x: (Math.max(aabbA.min.x, aabbB.min.x) + Math.min(aabbA.max.x, aabbB.max.x)) * 0.5, y: (Math.max(aabbA.min.y, aabbB.min.y) + Math.min(aabbA.max.y, aabbB.max.y)) * 0.5, z: (Math.max(aabbA.min.z, aabbB.min.z) + Math.min(aabbA.max.z, aabbB.max.z)) * 0.5 } };
    }

    private _solveContactVelocity(manifold: IResolvedContactManifold3D): void {
        const bm = this._host.bodyManager;
        const cA = bm.getPosition(manifold.bodyIdA);
        const cB = bm.getPosition(manifold.bodyIdB);
        const invMassA = this._invMass(manifold.bodyIdA);
        const invMassB = this._invMass(manifold.bodyIdB);
        const invIA = this._invInertia(manifold.bodyIdA);
        const invIB = this._invInertia(manifold.bodyIdB);
        const iSum = invMassA + invMassB;
        if (iSum <= PhysicsConstants.EPSILON) return;

        for (const point of manifold.points) {
            const wp = midpointVec3(
                this._lp2w(manifold.bodyIdA, point.localPointA),
                this._lp2w(manifold.bodyIdB, point.localPointB)
            );
            const rA = Vec3.subtract(wp, cA);
            const rB = Vec3.subtract(wp, cB);

            // Normal effective mass (includes angular inertia via diagonal inertia tensors).
            const rnA = Vec3.cross(rA, manifold.normal);
            const rnB = Vec3.cross(rB, manifold.normal);
            const kNormal =
                invMassA +
                invMassB +
                invIA.x * rnA.x * rnA.x +
                invIA.y * rnA.y * rnA.y +
                invIA.z * rnA.z * rnA.z +
                invIB.x * rnB.x * rnB.x +
                invIB.y * rnB.y * rnB.y +
                invIB.z * rnB.z * rnB.z;
            const normalMass = kNormal > PhysicsConstants.EPSILON ? 1 / kNormal : 0;

            const relV = Vec3.subtract(this._getWPV(manifold.bodyIdB, wp), this._getWPV(manifold.bodyIdA, wp));
            const ns = Vec3.dot(relV, manifold.normal);
            if (ns < 0) {
                const rest = ns < -PhysicsConstants.VELOCITY_THRESHOLD ? manifold.restitution : 0;
                let dPn = normalMass * (-(1 + rest) * ns);
                const newPn = Math.max((point.normalImpulse as number) + dPn, 0);
                dPn = newPn - (point.normalImpulse as number);
                point.normalImpulse = newPn as unknown as Impulse;
                this._applyImp(manifold.bodyIdA, Vec3.negate(Vec3.multiplyScalar(manifold.normal, dPn)), wp);
                this._applyImp(manifold.bodyIdB, Vec3.multiplyScalar(manifold.normal, dPn), wp);
            }

            // Friction along the tangent defined by the current relative velocity.
            const vn = Vec3.dot(relV, manifold.normal);
            const tanV = Vec3.subtract(relV, Vec3.multiplyScalar(manifold.normal, vn));
            const tLen = Vec3.len(tanV);
            if (tLen <= PhysicsConstants.EPSILON) continue;

            const tan = Vec3.multiplyScalar(tanV, 1 / tLen);
            const rtA = Vec3.cross(rA, tan);
            const rtB = Vec3.cross(rB, tan);
            const kTangent =
                invMassA +
                invMassB +
                invIA.x * rtA.x * rtA.x +
                invIA.y * rtA.y * rtA.y +
                invIA.z * rtA.z * rtA.z +
                invIB.x * rtB.x * rtB.x +
                invIB.y * rtB.y * rtB.y +
                invIB.z * rtB.z * rtB.z;
            const tangentMass = kTangent > PhysicsConstants.EPSILON ? 1 / kTangent : 0;

            let dPt = tangentMass * -Vec3.dot(relV, tan);
            const maxPt = manifold.friction * (point.normalImpulse as number);
            const newPt = clamp((point.tangentImpulse1 as number) + dPt, -maxPt, maxPt);
            dPt = newPt - (point.tangentImpulse1 as number);
            point.tangentImpulse1 = newPt as unknown as Impulse;
            this._applyImp(manifold.bodyIdA, Vec3.negate(Vec3.multiplyScalar(tan, dPt)), wp);
            this._applyImp(manifold.bodyIdB, Vec3.multiplyScalar(tan, dPt), wp);
        }
    }

    private _warmStartContact(manifold: IResolvedContactManifold3D): void {
        const normal = (manifold.points[0].normalImpulse as number) ?? 0;
        if (normal === 0) return;

        const wp = midpointVec3(
            this._lp2w(manifold.bodyIdA, manifold.points[0].localPointA),
            this._lp2w(manifold.bodyIdB, manifold.points[0].localPointB)
        );

        const normalImpulse = Vec3.multiplyScalar(manifold.normal, normal);
        this._applyImp(manifold.bodyIdA, Vec3.negate(normalImpulse), wp);
        this._applyImp(manifold.bodyIdB, normalImpulse, wp);

        manifold.points[0].tangentImpulse1 = 0 as Impulse;
        manifold.points[0].tangentImpulse2 = 0 as Impulse;
    }

    private _correctContactPositions(manifold: IResolvedContactManifold3D, beta: number): void {
        for (const point of manifold.points) {
            const sep = this._getSep(manifold, point);
            const pen = Math.max(0, -sep);
            if (pen <= PhysicsConstants.ALLOWED_PENETRATION) continue;
            const iA = this._invMass(manifold.bodyIdA), iB = this._invMass(manifold.bodyIdB);
            const iSum = iA + iB;
            if (iSum <= PhysicsConstants.EPSILON) continue;
            const corr = Vec3.multiplyScalar(manifold.normal, ((pen - PhysicsConstants.ALLOWED_PENETRATION) * beta) / iSum);
            if (iA > 0) this._host.bodyManager.setPosition(manifold.bodyIdA, Vec3.subtract(this._host.bodyManager.getPosition(manifold.bodyIdA), Vec3.multiplyScalar(corr, iA)));
            if (iB > 0) this._host.bodyManager.setPosition(manifold.bodyIdB, Vec3.add(this._host.bodyManager.getPosition(manifold.bodyIdB), Vec3.multiplyScalar(corr, iB)));
            point.separation = this._getSep(manifold, point);
        }
    }

    private _segSeg(a1: IVec3Like, a2: IVec3Like, b1: IVec3Like, b2: IVec3Like): { pointA: IVec3Like; pointB: IVec3Like; distSq: number } {
        const d1 = Vec3.subtract(a2, a1), d2 = Vec3.subtract(b2, b1), r = Vec3.subtract(a1, b1);
        const a = Vec3.dot(d1, d1), e = Vec3.dot(d2, d2), f = Vec3.dot(d2, r);
        let s = 0, t = 0;
        if (a <= PhysicsConstants.EPSILON && e <= PhysicsConstants.EPSILON) { s = t = 0; }
        else if (a <= PhysicsConstants.EPSILON) { s = 0; t = clamp(f / e, 0, 1); }
        else { const c = Vec3.dot(d1, r); if (e <= PhysicsConstants.EPSILON) { t = 0; s = clamp(-c / a, 0, 1); } else { const b = Vec3.dot(d1, d2); const denom = a * e - b * b; if (denom !== 0) s = clamp((b * f - c * e) / denom, 0, 1); t = (b * s + f) / e; if (t < 0) { t = 0; s = clamp(-c / a, 0, 1); } else if (t > 1) { t = 1; s = clamp((b - c) / a, 0, 1); } } }
        const pA = { x: a1.x + d1.x * s, y: a1.y + d1.y * s, z: a1.z + d1.z * s };
        const pB = { x: b1.x + d2.x * t, y: b1.y + d2.y * t, z: b1.z + d2.z * t };
        const delta = Vec3.subtract(pB, pA);
        return { pointA: pA, pointB: pB, distSq: Vec3.dot(delta, delta) };
    }

    private _closestSeg(point: IVec3Like, a: IVec3Like, b: IVec3Like): IVec3Like {
        const ab = Vec3.subtract(b, a), ap = Vec3.subtract(point, a);
        const ab2 = Vec3.dot(ab, ab);
        const t = ab2 > PhysicsConstants.EPSILON ? clamp(Vec3.dot(ap, ab) / ab2, 0, 1) : 0;
        return { x: a.x + ab.x * t, y: a.y + ab.y * t, z: a.z + ab.z * t };
    }

    private _invMass(bodyId: BodyId3D): number {
        if (this._host.bodyManager.getBodyType(bodyId) !== 2 || !this._host.bodyManager.isEnabled(bodyId)) return 0;
        return this._host.bodyManager.getInverseMass(bodyId);
    }

    private _invInertia(bodyId: BodyId3D): IVec3Like {
        const bm = this._host.bodyManager;
        if (bm.getBodyType(bodyId) !== 2 || !bm.isEnabled(bodyId) || bm.isFixedRotation(bodyId)) {
            return { x: 0, y: 0, z: 0 };
        }
        return bm.getInverseInertia(bodyId);
    }

    /**
     * Counts contact islands: connected components of bodies linked by active
     * contact manifolds. Enables island-level reporting and (combined with warm
     * starting) stable, ordered sequential-impulse solving.
     */
    private _buildIslandCount(manifolds: ReadonlyMap<string, IResolvedContactManifold3D>): number {
        const parent = new Map<number, number>();
        const find = (x: number): number => {
            let root = x;
            while (true) {
                const p = parent.get(root);
                if (p === undefined || p === root) break;
                root = p;
            }
            let curr = x;
            while (curr !== root) {
                const next = parent.get(curr)!;
                parent.set(curr, root);
                curr = next;
            }
            return root;
        };
        const union = (a: number, b: number): void => {
            const ra = find(a);
            const rb = find(b);
            if (ra !== rb) parent.set(ra, rb);
        };

        for (const m of manifolds.values()) {
            if (!parent.has(m.bodyIdA)) parent.set(m.bodyIdA, m.bodyIdA);
            if (!parent.has(m.bodyIdB)) parent.set(m.bodyIdB, m.bodyIdB);
            union(m.bodyIdA, m.bodyIdB);
        }

        const roots = new Set<number>();
        for (const key of parent.keys()) roots.add(find(key));
        return roots.size;
    }

    private _applyImp(bodyId: BodyId3D, impulse: IVec3Like, point: IVec3Like): void {
        if (this._host.bodyManager.getBodyType(bodyId) !== 2) return;
        this._host.bodyManager.applyImpulse(bodyId, impulse, point);
    }

    private _getWPV(bodyId: BodyId3D, point: IVec3Like): IVec3Like {
        const center = this._host.bodyManager.getPosition(bodyId);
        return Vec3.add(this._host.bodyManager.getLinearVelocity(bodyId), Vec3.cross(this._host.bodyManager.getAngularVelocity(bodyId), Vec3.subtract(point, center)));
    }

    private _lp2w(bodyId: BodyId3D, localPoint: IVec3Like): IVec3Like {
        return transformPoint3D(localPoint, this._host.bodyManager.getPosition(bodyId), this._host.bodyManager.getRotation(bodyId));
    }

    private _getSep(manifold: IResolvedContactManifold3D, point: IMutableContactPoint3D): number {
        return Vec3.dot(Vec3.subtract(this._lp2w(manifold.bodyIdB, point.localPointB), this._lp2w(manifold.bodyIdA, point.localPointA)), manifold.normal);
    }
}
