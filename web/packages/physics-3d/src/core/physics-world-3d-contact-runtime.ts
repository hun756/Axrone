// Part 1
import type { IVec3Like } from '@axrone/numeric';
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
    SHAPE_TYPE_SPHERE,
    type IAabb3D,
    type IConstraintDescriptor3D,
    type IResolvedContactManifold3D,
    type IShapeDescriptor3D,
    type IShapePairCandidate3D,
    type IMutableContactPoint3D,
    type SupportedConstraintDef3D,
    type SupportedShapeDef3D,
    addVec3,
    buildOrthonormalBasis,
    clamp,
    crossVec3,
    dotVec3,
    inverseRotateVec3,
    inverseTransformPoint3D,
    lengthVec3,
    midpointVec3,
    multiplyQuat,
    negateVec3,
    rotateVec3,
    scaleVec3,
    shouldShapeFiltersCollide,
    subVec3,
    transformPoint3D,
} from './physics-world-3d-shared';

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
    private readonly _broadphase = new DynamicAABBTree3D(1024);
    private readonly _shapeProxyMap = new Map<ShapeId3D, number>();

    constructor(private readonly _host: IPhysicsWorld3DContactRuntimeHost) {}

    get contactCount(): number {
        return this._contactManifolds.size;
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
    }

    solve(deltaTime: number, velIters: number, posIters: number): void {
        const bStart = performance.now();
        const pairs = this._collectPotentialCollisionPairs();
        const bTime = performance.now() - bStart;
        const nStart = performance.now();
        const next = new Map<string, IResolvedContactManifold3D>();
        for (const pair of pairs) {
            const m = this._buildContactManifold(pair);
            if (m) next.set(pair.pairKey, m);
        }
        const nTime = performance.now() - nStart;
        const profiler = this._host.getProfiler();
        if (profiler) {
            profiler.broadphaseTime = bTime;
            profiler.narrowphaseTime = nTime;
            profiler.collisionTime = bTime + nTime;
        }
        for (const m of next.values()) this._solveContactVelocity(m);
        for (let i = 0; i < posIters; i++) {
            for (const m of next.values()) this._solveContactPosition(m);
        }
        for (const m of next.values()) this._finalizeContactPosition(m);
        this._contactManifolds = next;
    }

    private _collectPotentialCollisionPairs(): IShapePairCandidate3D[] {
        const candidates: IShapePairCandidate3D[] = [];
        const filter = this._host.getCollisionFilter();
        for (const d of this._host.shapeDescriptors.values()) {
            if (!this._host.bodyManager.isEnabled(d.bodyId)) continue;
            const rawAabb = this._host.computeShapeAabb(d);
            const aabb = new AABB3D(rawAabb.min, rawAabb.max);
            const existing = this._shapeProxyMap.get(d.id);
            if (existing !== undefined) {
                const disp = { x: aabb.max.x - aabb.min.x, y: aabb.max.y - aabb.min.y, z: aabb.max.z - aabb.min.z };
                this._broadphase.moveProxy(existing, aabb, disp);
            } else {
                const pid = this._broadphase.createProxy(aabb, d.id);
                this._shapeProxyMap.set(d.id, pid);
            }
        }
        this._broadphase.queryPairs((pA, pB) => {
            const sA = this._broadphase.getUserData(pA) as ShapeId3D;
            const sB = this._broadphase.getUserData(pB) as ShapeId3D;
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
                separation: dotVec3(subVec3(wB, wA), c.normal),
            }],
            sensor: pair.descriptorA.isSensor || pair.descriptorB.isSensor,
            friction, restitution,
        };
    }

    private _getContactPointOnShape(d: IShapeDescriptor3D, c: { normal: IVec3Like; point: IVec3Like; penetration: number }, first: boolean): IVec3Like {
        if (d.type === SHAPE_TYPE_SPHERE) {
            const r = (d.def as any).radius;
            return addVec3(this._host.getShapeWorldCenter(d), scaleVec3(first ? c.normal : negateVec3(c.normal), r));
        }
        return c.point;
    }

    private _detectCollision(dA: IShapeDescriptor3D, dB: IShapeDescriptor3D, aabbA: IAabb3D, aabbB: IAabb3D): { normal: IVec3Like; point: IVec3Like; penetration: number } | null {
        const tA = dA.type, tB = dB.type;
        if (tA === SHAPE_TYPE_SPHERE && tB === SHAPE_TYPE_SPHERE) return this._cSphSph(dA, dB);
        if (tA === SHAPE_TYPE_SPHERE && tB === SHAPE_TYPE_BOX) return this._cSphBox(dA, dB);
        if (tA === SHAPE_TYPE_BOX && tB === SHAPE_TYPE_SPHERE) { const k = this._cSphBox(dB, dA); return k ? { normal: negateVec3(k.normal), point: k.point, penetration: k.penetration } : null; }
        if (tA === SHAPE_TYPE_BOX && tB === SHAPE_TYPE_BOX) return this._cBoxBox(dA, dB);
        if (tA === SHAPE_TYPE_CAPSULE && tB === SHAPE_TYPE_CAPSULE) return this._cCapCap(dA, dB);
        if (tA === SHAPE_TYPE_CAPSULE && tB === SHAPE_TYPE_SPHERE) return this._cCapSph(dA, dB);
        if (tA === SHAPE_TYPE_SPHERE && tB === SHAPE_TYPE_CAPSULE) { const k = this._cCapSph(dB, dA); return k ? { normal: negateVec3(k.normal), point: k.point, penetration: k.penetration } : null; }
        if (tA === SHAPE_TYPE_CAPSULE && tB === SHAPE_TYPE_BOX) return this._cCapBox(dA, dB);
        if (tA === SHAPE_TYPE_BOX && tB === SHAPE_TYPE_CAPSULE) { const k = this._cCapBox(dB, dA); return k ? { normal: negateVec3(k.normal), point: k.point, penetration: k.penetration } : null; }
        return this._cAabbApprox(dA, dB, aabbA, aabbB);
    }

    private _cSphSph(dA: IShapeDescriptor3D, dB: IShapeDescriptor3D): { normal: IVec3Like; point: IVec3Like; penetration: number } | null {
        const cA = this._host.getShapeWorldCenter(dA), cB = this._host.getShapeWorldCenter(dB);
        const delta = subVec3(cB, cA);
        const dist = lengthVec3(delta);
        const rSum = (dA.def as any).radius + (dB.def as any).radius;
        if (dist > rSum) return null;
        const n = dist > PhysicsConstants.EPSILON ? scaleVec3(delta, 1 / dist) : { x: 1, y: 0, z: 0 };
        const pen = rSum - dist;
        return { normal: n, point: addVec3(cA, scaleVec3(n, (dA.def as any).radius - pen * 0.5)), penetration: pen };
    }

    private _cSphBox(s: IShapeDescriptor3D, b: IShapeDescriptor3D): { normal: IVec3Like; point: IVec3Like; penetration: number } | null {
        const sC = this._host.getShapeWorldCenter(s);
        const bP = this._host.bodyManager.getPosition(b.bodyId), bR = this._host.bodyManager.getRotation(b.bodyId);
        const bD = b.def as any;
        const bC = transformPoint3D(bD.center, bP, bR);
        const bRot = multiplyQuat(bR, bD.rotation ?? IDENTITY_ROTATION);
        const localSC = inverseTransformPoint3D(sC, bC, bRot);
        const closestLocal = { x: clamp(localSC.x, -bD.halfExtents.x, bD.halfExtents.x), y: clamp(localSC.y, -bD.halfExtents.y, bD.halfExtents.y), z: clamp(localSC.z, -bD.halfExtents.z, bD.halfExtents.z) };
        const closestWorld = transformPoint3D(closestLocal, bC, bRot);
        const delta = subVec3(sC, closestWorld);
        const dist = lengthVec3(delta);
        const r = (s.def as any).radius;
        if (dist > r) return null;
        if (dist > PhysicsConstants.EPSILON) return { normal: scaleVec3(delta, -1 / dist), point: closestWorld, penetration: r - dist };
        return { normal: { x: 0, y: 1, z: 0 }, point: closestWorld, penetration: r };
    }

    private _cBoxBox(dA: IShapeDescriptor3D, dB: IShapeDescriptor3D): { normal: IVec3Like; point: IVec3Like; penetration: number } | null {
        const bDA = dA.def as any, bDB = dB.def as any;
        const cA = transformPoint3D(bDA.center, this._host.bodyManager.getPosition(dA.bodyId), this._host.bodyManager.getRotation(dA.bodyId));
        const cB = transformPoint3D(bDB.center, this._host.bodyManager.getPosition(dB.bodyId), this._host.bodyManager.getRotation(dB.bodyId));
        const rA = multiplyQuat(this._host.bodyManager.getRotation(dA.bodyId), bDA.rotation ?? IDENTITY_ROTATION);
        const rB = multiplyQuat(this._host.bodyManager.getRotation(dB.bodyId), bDB.rotation ?? IDENTITY_ROTATION);
        const axisX = (v: any) => rotateVec3(v, rA), axisY = (v: any) => rotateVec3(v, rA), axisZ = (v: any) => rotateVec3(v, rA);
        const bxX = (v: any) => rotateVec3(v, rB), byY = (v: any) => rotateVec3(v, rB), bzZ = (v: any) => rotateVec3(v, rB);
        const xA = rotateVec3({ x: 1, y: 0, z: 0 }, rA), yA = rotateVec3({ x: 0, y: 1, z: 0 }, rA), zA = rotateVec3({ x: 0, y: 0, z: 1 }, rA);
        const xB = rotateVec3({ x: 1, y: 0, z: 0 }, rB), yB = rotateVec3({ x: 0, y: 1, z: 0 }, rB), zB = rotateVec3({ x: 0, y: 0, z: 1 }, rB);
        const axes = [xA, yA, zA, xB, yB, zB];
        const hA = bDA.halfExtents, hB = bDB.halfExtents;
        const delta = subVec3(cB, cA);
        let minP = Infinity; let bestN: IVec3Like = { x: 0, y: 1, z: 0 };
        for (const ax of axes) {
            const pA = hA.x * Math.abs(dotVec3(xA, ax)) + hA.y * Math.abs(dotVec3(yA, ax)) + hA.z * Math.abs(dotVec3(zA, ax));
            const pB = hB.x * Math.abs(dotVec3(xB, ax)) + hB.y * Math.abs(dotVec3(yB, ax)) + hB.z * Math.abs(dotVec3(zB, ax));
            const d = Math.abs(dotVec3(delta, ax));
            const pen = pA + pB - d;
            if (pen < 0) return null;
            if (pen < minP) { minP = pen; bestN = dotVec3(delta, ax) > 0 ? ax : negateVec3(ax); }
        }
        return { normal: bestN, point: midpointVec3(cA, cB), penetration: minP };
    }

    private _cCapCap(dA: IShapeDescriptor3D, dB: IShapeDescriptor3D): { normal: IVec3Like; point: IVec3Like; penetration: number } | null {
        const cA = dA.def as any, cB = dB.def as any;
        const p1A = transformPoint3D(cA.p1, this._host.bodyManager.getPosition(dA.bodyId), this._host.bodyManager.getRotation(dA.bodyId));
        const p2A = transformPoint3D(cA.p2, this._host.bodyManager.getPosition(dA.bodyId), this._host.bodyManager.getRotation(dA.bodyId));
        const p1B = transformPoint3D(cB.p1, this._host.bodyManager.getPosition(dB.bodyId), this._host.bodyManager.getRotation(dB.bodyId));
        const p2B = transformPoint3D(cB.p2, this._host.bodyManager.getPosition(dB.bodyId), this._host.bodyManager.getRotation(dB.bodyId));
        const closest = this._segSeg(p1A, p2A, p1B, p2B);
        const rSum = cA.radius + cB.radius;
        if (closest.distSq > rSum * rSum) return null;
        const dist = Math.sqrt(closest.distSq);
        const invD = dist > PhysicsConstants.EPSILON ? 1 / dist : 0;
        const dx = closest.pointB.x - closest.pointA.x, dy = closest.pointB.y - closest.pointA.y, dz = closest.pointB.z - closest.pointA.z;
        return { normal: { x: dx * invD, y: dy * invD, z: dz * invD }, point: midpointVec3(closest.pointA, closest.pointB), penetration: rSum - dist };
    }

    private _cCapSph(cap: IShapeDescriptor3D, sph: IShapeDescriptor3D): { normal: IVec3Like; point: IVec3Like; penetration: number } | null {
        const cD = cap.def as any, sD = sph.def as any;
        const p1 = transformPoint3D(cD.p1, this._host.bodyManager.getPosition(cap.bodyId), this._host.bodyManager.getRotation(cap.bodyId));
        const p2 = transformPoint3D(cD.p2, this._host.bodyManager.getPosition(cap.bodyId), this._host.bodyManager.getRotation(cap.bodyId));
        const sC = this._host.getShapeWorldCenter(sph);
        const closest = this._closestSeg(sC, p1, p2);
        const delta = subVec3(sC, closest);
        const dist = lengthVec3(delta);
        const rSum = cD.radius + sD.radius;
        if (dist > rSum) return null;
        const invD = dist > PhysicsConstants.EPSILON ? 1 / dist : 0;
        return { normal: { x: delta.x * invD, y: delta.y * invD, z: delta.z * invD }, point: closest, penetration: rSum - dist };
    }

    private _cCapBox(cap: IShapeDescriptor3D, box: IShapeDescriptor3D): { normal: IVec3Like; point: IVec3Like; penetration: number } | null {
        const cD = cap.def as any, bD = box.def as any;
        const p1 = transformPoint3D(cD.p1, this._host.bodyManager.getPosition(cap.bodyId), this._host.bodyManager.getRotation(cap.bodyId));
        const p2 = transformPoint3D(cD.p2, this._host.bodyManager.getPosition(cap.bodyId), this._host.bodyManager.getRotation(cap.bodyId));
        const bC = transformPoint3D(bD.center, this._host.bodyManager.getPosition(box.bodyId), this._host.bodyManager.getRotation(box.bodyId));
        const bRot = multiplyQuat(this._host.bodyManager.getRotation(box.bodyId), bD.rotation ?? IDENTITY_ROTATION);
        const l1 = inverseTransformPoint3D(p1, bC, bRot), l2 = inverseTransformPoint3D(p2, bC, bRot);
        const c1 = { x: clamp(l1.x, -bD.halfExtents.x, bD.halfExtents.x), y: clamp(l1.y, -bD.halfExtents.y, bD.halfExtents.y), z: clamp(l1.z, -bD.halfExtents.z, bD.halfExtents.z) };
        const c2 = { x: clamp(l2.x, -bD.halfExtents.x, bD.halfExtents.x), y: clamp(l2.y, -bD.halfExtents.y, bD.halfExtents.y), z: clamp(l2.z, -bD.halfExtents.z, bD.halfExtents.z) };
        const closest = this._closestSeg({ x: 0, y: 0, z: 0 }, c1, c2);
        const delta = subVec3(closest, { x: 0, y: 0, z: 0 });
        const dist = lengthVec3(delta);
        if (dist > cD.radius) return null;
        const invD = dist > PhysicsConstants.EPSILON ? 1 / dist : 0;
        const localN = { x: delta.x * invD, y: delta.y * invD, z: delta.z * invD };
        return { normal: rotateVec3(localN, bRot), point: transformPoint3D({ x: 0, y: 0, z: 0 }, bC, bRot), penetration: cD.radius - dist };
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
        for (const point of manifold.points) {
            const wp = midpointVec3(this._lp2w(manifold.bodyIdA, point.localPointA), this._lp2w(manifold.bodyIdB, point.localPointB));
            const relV = subVec3(this._getWPV(manifold.bodyIdB, wp), this._getWPV(manifold.bodyIdA, wp));
            const ns = dotVec3(relV, manifold.normal);
            if (ns >= 0) continue;
            const iSum = this._invMass(manifold.bodyIdA) + this._invMass(manifold.bodyIdB);
            if (iSum <= PhysicsConstants.EPSILON) continue;
            const rest = ns < -PhysicsConstants.VELOCITY_THRESHOLD ? manifold.restitution : 0;
            const nImp = (-(1 + rest) * ns) / iSum;
            if (nImp <= 0) continue;
            point.normalImpulse = ((point.normalImpulse + nImp) as unknown) as Impulse;
            this._applyImp(manifold.bodyIdA, negateVec3(scaleVec3(manifold.normal, nImp)), wp);
            this._applyImp(manifold.bodyIdB, scaleVec3(manifold.normal, nImp), wp);
            const upV = subVec3(this._getWPV(manifold.bodyIdB, wp), this._getWPV(manifold.bodyIdA, wp));
            const tanV = subVec3(upV, scaleVec3(manifold.normal, dotVec3(upV, manifold.normal)));
            const tLen = lengthVec3(tanV);
            if (tLen <= PhysicsConstants.EPSILON) continue;
            const tan = scaleVec3(tanV, 1 / tLen);
            const tSpeed = dotVec3(upV, tan);
            const tImp = clamp(-tSpeed / iSum, -manifold.friction * nImp, manifold.friction * nImp);
            point.tangentImpulse1 = ((point.tangentImpulse1 + tImp) as unknown) as Impulse;
            this._applyImp(manifold.bodyIdA, negateVec3(scaleVec3(tan, tImp)), wp);
            this._applyImp(manifold.bodyIdB, scaleVec3(tan, tImp), wp);
        }
    }

    private _solveContactPosition(manifold: IResolvedContactManifold3D): void {
        for (const point of manifold.points) {
            const sep = this._getSep(manifold, point);
            const pen = Math.max(0, -sep);
            if (pen <= PhysicsConstants.ALLOWED_PENETRATION) continue;
            const iA = this._invMass(manifold.bodyIdA), iB = this._invMass(manifold.bodyIdB);
            const iSum = iA + iB;
            if (iSum <= PhysicsConstants.EPSILON) continue;
            const corr = scaleVec3(manifold.normal, ((pen - PhysicsConstants.ALLOWED_PENETRATION) * 0.2) / iSum);
            if (iA > 0) this._host.bodyManager.setPosition(manifold.bodyIdA, subVec3(this._host.bodyManager.getPosition(manifold.bodyIdA), scaleVec3(corr, iA)));
            if (iB > 0) this._host.bodyManager.setPosition(manifold.bodyIdB, addVec3(this._host.bodyManager.getPosition(manifold.bodyIdB), scaleVec3(corr, iB)));
            point.separation = this._getSep(manifold, point);
        }
    }

    private _finalizeContactPosition(manifold: IResolvedContactManifold3D): void {
        for (const point of manifold.points) {
            const sep = this._getSep(manifold, point);
            const pen = Math.max(0, -sep);
            if (pen <= PhysicsConstants.ALLOWED_PENETRATION) continue;
            const iA = this._invMass(manifold.bodyIdA), iB = this._invMass(manifold.bodyIdB);
            const iSum = iA + iB;
            if (iSum <= PhysicsConstants.EPSILON) continue;
            const corr = scaleVec3(manifold.normal, (pen - PhysicsConstants.ALLOWED_PENETRATION) / iSum);
            if (iA > 0) this._host.bodyManager.setPosition(manifold.bodyIdA, subVec3(this._host.bodyManager.getPosition(manifold.bodyIdA), scaleVec3(corr, iA)));
            if (iB > 0) this._host.bodyManager.setPosition(manifold.bodyIdB, addVec3(this._host.bodyManager.getPosition(manifold.bodyIdB), scaleVec3(corr, iB)));
            point.separation = this._getSep(manifold, point);
        }
    }

    private _segSeg(a1: IVec3Like, a2: IVec3Like, b1: IVec3Like, b2: IVec3Like): { pointA: IVec3Like; pointB: IVec3Like; distSq: number } {
        const d1 = subVec3(a2, a1), d2 = subVec3(b2, b1), r = subVec3(a1, b1);
        const a = dotVec3(d1, d1), e = dotVec3(d2, d2), f = dotVec3(d2, r);
        let s = 0, t = 0;
        if (a <= PhysicsConstants.EPSILON && e <= PhysicsConstants.EPSILON) { s = t = 0; }
        else if (a <= PhysicsConstants.EPSILON) { s = 0; t = clamp(f / e, 0, 1); }
        else { const c = dotVec3(d1, r); if (e <= PhysicsConstants.EPSILON) { t = 0; s = clamp(-c / a, 0, 1); } else { const b = dotVec3(d1, d2); const denom = a * e - b * b; if (denom !== 0) s = clamp((b * f - c * e) / denom, 0, 1); t = (b * s + f) / e; if (t < 0) { t = 0; s = clamp(-c / a, 0, 1); } else if (t > 1) { t = 1; s = clamp((b - c) / a, 0, 1); } } }
        const pA = { x: a1.x + d1.x * s, y: a1.y + d1.y * s, z: a1.z + d1.z * s };
        const pB = { x: b1.x + d2.x * t, y: b1.y + d2.y * t, z: b1.z + d2.z * t };
        const delta = subVec3(pB, pA);
        return { pointA: pA, pointB: pB, distSq: dotVec3(delta, delta) };
    }

    private _closestSeg(point: IVec3Like, a: IVec3Like, b: IVec3Like): IVec3Like {
        const ab = subVec3(b, a), ap = subVec3(point, a);
        const ab2 = dotVec3(ab, ab);
        const t = ab2 > PhysicsConstants.EPSILON ? clamp(dotVec3(ap, ab) / ab2, 0, 1) : 0;
        return { x: a.x + ab.x * t, y: a.y + ab.y * t, z: a.z + ab.z * t };
    }

    private _invMass(bodyId: BodyId3D): number {
        if (this._host.bodyManager.getBodyType(bodyId) !== 2 || !this._host.bodyManager.isEnabled(bodyId)) return 0;
        return this._host.bodyManager.getInverseMass(bodyId);
    }

    private _applyImp(bodyId: BodyId3D, impulse: IVec3Like, point: IVec3Like): void {
        if (this._host.bodyManager.getBodyType(bodyId) !== 2) return;
        this._host.bodyManager.applyImpulse(bodyId, impulse, point);
    }

    private _getWPV(bodyId: BodyId3D, point: IVec3Like): IVec3Like {
        const center = this._host.bodyManager.getPosition(bodyId);
        return addVec3(this._host.bodyManager.getLinearVelocity(bodyId), crossVec3(this._host.bodyManager.getAngularVelocity(bodyId), subVec3(point, center)));
    }

    private _lp2w(bodyId: BodyId3D, localPoint: IVec3Like): IVec3Like {
        return transformPoint3D(localPoint, this._host.bodyManager.getPosition(bodyId), this._host.bodyManager.getRotation(bodyId));
    }

    private _getSep(manifold: IResolvedContactManifold3D, point: IMutableContactPoint3D): number {
        return dotVec3(subVec3(this._lp2w(manifold.bodyIdB, point.localPointB), this._lp2w(manifold.bodyIdA, point.localPointA)), manifold.normal);
    }
}
