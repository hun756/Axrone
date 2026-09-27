import { Vec3, Quat, clamp, type IVec3Like, type IVec2Like, type IQuatLike } from '@axrone/numeric';
import { PhysicsConstants } from '../types';
import type { BodyId3D, ShapeId3D } from '../types/physics-3d';
import {
    IDENTITY_ROTATION,
    SHAPE_TYPE_BOX,
    SHAPE_TYPE_CAPSULE,
    SHAPE_TYPE_CONE,
    SHAPE_TYPE_CONVEX_HULL,
    SHAPE_TYPE_CYLINDER,
    SHAPE_TYPE_HEIGHTFIELD,
    SHAPE_TYPE_SPHERE,
    SHAPE_TYPE_TRIANGLE_MESH,
    type IAabb3D,
    type IShapeDescriptor3D,
    isBoxDef,
    isCapsuleDef,
    isConeDef,
    isCylinderDef,
    isConvexHullDef,
    isHeightFieldDef,
    isSphereDef,
    isTriangleMeshDef,
    transformPoint3D,
    inverseTransformPoint3D,
    midpointVec3,
} from './physics-world-3d-shared';
import { GJK3D, supportFromVertices, type Support3D } from './gjk3d';

export interface INarrowphaseHost {
    readonly bodyManager: {
        getPosition(bodyId: BodyId3D): IVec3Like;
        getRotation(bodyId: BodyId3D): IQuatLike;
        getBodyType(bodyId: BodyId3D): number;
    };
    readonly getShapeWorldCenter: (descriptor: IShapeDescriptor3D) => IVec3Like;
}

export interface ICollisionResult {
    normal: IVec3Like;
    point: IVec3Like;
    penetration: number;
}

export class PhysicsWorld3DNarrowphase {
    constructor(private readonly _host: INarrowphaseHost) {}

    detectCollision(
        dA: IShapeDescriptor3D,
        dB: IShapeDescriptor3D,
        aabbA: IAabb3D,
        aabbB: IAabb3D
    ): ICollisionResult | null {
        const tA = dA.type, tB = dB.type;
        if (tA === SHAPE_TYPE_SPHERE && tB === SHAPE_TYPE_SPHERE) return this.sphSph(dA, dB);
        if (tA === SHAPE_TYPE_SPHERE && tB === SHAPE_TYPE_BOX) return this.sphBox(dA, dB);
        if (tA === SHAPE_TYPE_BOX && tB === SHAPE_TYPE_SPHERE) { const k = this.sphBox(dB, dA); return k ? { normal: Vec3.negate(k.normal), point: k.point, penetration: k.penetration } : null; }
        if (tA === SHAPE_TYPE_BOX && tB === SHAPE_TYPE_BOX) return this.boxBox(dA, dB);
        if (tA === SHAPE_TYPE_CAPSULE && tB === SHAPE_TYPE_CAPSULE) return this.capCap(dA, dB);
        if (tA === SHAPE_TYPE_CAPSULE && tB === SHAPE_TYPE_SPHERE) return this.capSph(dA, dB);
        if (tA === SHAPE_TYPE_SPHERE && tB === SHAPE_TYPE_CAPSULE) { const k = this.capSph(dB, dA); return k ? { normal: Vec3.negate(k.normal), point: k.point, penetration: k.penetration } : null; }
        if (tA === SHAPE_TYPE_CAPSULE && tB === SHAPE_TYPE_BOX) return this.capBox(dA, dB);
        if (tA === SHAPE_TYPE_BOX && tB === SHAPE_TYPE_CAPSULE) { const k = this.capBox(dB, dA); return k ? { normal: Vec3.negate(k.normal), point: k.point, penetration: k.penetration } : null; }
        if (
            tA === SHAPE_TYPE_CONVEX_HULL || tA === SHAPE_TYPE_TRIANGLE_MESH ||
            tB === SHAPE_TYPE_CONVEX_HULL || tB === SHAPE_TYPE_TRIANGLE_MESH ||
            tA === SHAPE_TYPE_CYLINDER || tA === SHAPE_TYPE_CONE ||
            tB === SHAPE_TYPE_CYLINDER || tB === SHAPE_TYPE_CONE
        ) {
            return this.convex(dA, dB, aabbA, aabbB);
        }
        if (tA === SHAPE_TYPE_HEIGHTFIELD || tB === SHAPE_TYPE_HEIGHTFIELD) {
            return this.convex(dA, dB, aabbA, aabbB);
        }
        return this.aabbApprox(dA, dB, aabbA, aabbB);
    }

    private convex(
        dA: IShapeDescriptor3D,
        dB: IShapeDescriptor3D,
        aabbA: IAabb3D,
        aabbB: IAabb3D
    ): ICollisionResult | null {
        const supportA = this.supportForShape(dA);
        const supportB = this.supportForShape(dB);
        if (!supportA || !supportB) {
            return this.aabbApprox(dA, dB, aabbA, aabbB);
        }

        const result = GJK3D.intersect(supportA, supportB);
        if (!result.hit) return null;

        return {
            normal: { x: result.normal.x, y: result.normal.y, z: result.normal.z },
            point: { x: result.point.x, y: result.point.y, z: result.point.z },
            penetration: result.depth,
        };
    }

    private supportForShape(descriptor: IShapeDescriptor3D): Support3D | null {
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
            const vertices = this.worldVerticesOf(descriptor);
            if (vertices.length === 0) return null;
            return supportFromVertices(vertices as IVec3Like[]);
        }
        return null;
    }

    private worldVerticesOf(descriptor: IShapeDescriptor3D): IVec3Like[] {
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

        if (isHeightFieldDef(def)) {
            const { heights, width, depth, scaleX = 1, scaleY = 1, scaleZ = 1 } = def;
            const c = transformPoint3D({ x: 0, y: 0, z: 0 }, pos, rot);
            const verts: IVec2Like[] = [];
            const halfW = (width - 1) * 0.5;
            const halfD = (depth - 1) * 0.5;
            const stepX = Math.max(1, Math.floor(width / 8));
            const stepZ = Math.max(1, Math.floor(depth / 8));
            for (let iz = 0; iz < depth; iz += stepZ) {
                for (let ix = 0; ix < width; ix += stepX) {
                    const h = heights[iz * width + ix] ?? 0;
                    const lx = (ix - halfW) * scaleX;
                    const ly = h * scaleY;
                    const lz = (iz - halfD) * scaleZ;
                    const local = Vec3.add(c, Vec3.add(
                        Vec3.add(Vec3.multiplyScalar(Quat.rotateVector(rot, { x: 1, y: 0, z: 0 }), lx),
                            Vec3.multiplyScalar(Quat.rotateVector(rot, { x: 0, y: 1, z: 0 }), ly)),
                        Vec3.multiplyScalar(Quat.rotateVector(rot, { x: 0, y: 0, z: 1 }), lz)
                    ));
                    verts.push(local);
                }
            }
            return verts as IVec3Like[];
        }

        return [];
    }

    private sphSph(dA: IShapeDescriptor3D, dB: IShapeDescriptor3D): ICollisionResult | null {
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

    private sphBox(s: IShapeDescriptor3D, b: IShapeDescriptor3D): ICollisionResult | null {
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

    private boxBox(dA: IShapeDescriptor3D, dB: IShapeDescriptor3D): ICollisionResult | null {
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

    buildBoxBoxManifold(
        dA: IShapeDescriptor3D, dB: IShapeDescriptor3D,
        normal: IVec3Like, totalPen: number
    ): { worldPoint: IVec3Like; separation: number }[] {
        const bm = this._host.bodyManager;
        const bDA = dA.def as { center: IVec3Like; halfExtents: IVec3Like; rotation?: IVec3Like };
        const bDB = dB.def as { center: IVec3Like; halfExtents: IVec3Like; rotation?: IVec3Like };

        const posA = bm.getPosition(dA.bodyId), rotA = bm.getRotation(dA.bodyId);
        const posB = bm.getPosition(dB.bodyId), rotB = bm.getRotation(dB.bodyId);
        const cA = transformPoint3D(bDA.center, posA, rotA);
        const cB = transformPoint3D(bDB.center, posB, rotB);
        const rA = Quat.multiply(rotA, (bDA.rotation ?? IDENTITY_ROTATION) as IQuatLike);
        const rB = Quat.multiply(rotB, (bDB.rotation ?? IDENTITY_ROTATION) as IQuatLike);

        const axesA = [
            Quat.rotateVector(rA, { x: 1, y: 0, z: 0 }),
            Quat.rotateVector(rA, { x: 0, y: 1, z: 0 }),
            Quat.rotateVector(rA, { x: 0, y: 0, z: 1 }),
        ];
        const axesB = [
            Quat.rotateVector(rB, { x: 1, y: 0, z: 0 }),
            Quat.rotateVector(rB, { x: 0, y: 1, z: 0 }),
            Quat.rotateVector(rB, { x: 0, y: 0, z: 1 }),
        ];

        const negNormal = Vec3.negate(normal);
        let bestDotA = -Infinity, bestA = 0;
        let bestDotB = -Infinity, bestB = 0;
        for (let i = 0; i < 3; i++) {
            const dA2 = Math.abs(Vec3.dot(axesA[i], normal));
            if (dA2 > bestDotA) { bestDotA = dA2; bestA = i; }
            const dB2 = Math.abs(Vec3.dot(axesB[i], negNormal));
            if (dB2 > bestDotB) { bestDotB = dB2; bestB = i; }
        }

        let refCenter: IVec3Like, refAxes: IVec3Like[], refHalf: number[], refSign: number;
        let incCenter: IVec3Like, incAxes: IVec3Like[], incHalf: number[], incSign: number;
        let isARef: boolean;

        if (bestDotA >= bestDotB) {
            isARef = true;
            refCenter = cA; refAxes = axesA; refHalf = [bDA.halfExtents.x, bDA.halfExtents.y, bDA.halfExtents.z];
            refSign = Vec3.dot(normal, axesA[bestA]) > 0 ? -1 : 1;
            incCenter = cB; incAxes = axesB; incHalf = [bDB.halfExtents.x, bDB.halfExtents.y, bDB.halfExtents.z];
            incSign = Vec3.dot(negNormal, axesB[bestB]) > 0 ? -1 : 1;
        } else {
            isARef = false;
            refCenter = cB; refAxes = axesB; refHalf = [bDB.halfExtents.x, bDB.halfExtents.y, bDB.halfExtents.z];
            refSign = Vec3.dot(negNormal, axesB[bestB]) > 0 ? -1 : 1;
            incCenter = cA; incAxes = axesA; incHalf = [bDA.halfExtents.x, bDA.halfExtents.y, bDA.halfExtents.z];
            incSign = Vec3.dot(normal, axesA[bestA]) > 0 ? -1 : 1;
        }

        const incNormal = incAxes[bestB === bestDotB ? bestB : bestB];
        const incTangent1 = incAxes[(bestB + 1) % 3];
        const incTangent2 = incAxes[(bestB + 2) % 3];
        const incFaceOffset = incSign * incHalf[bestB];
        const hT1 = incHalf[(bestB + 1) % 3];
        const hT2 = incHalf[(bestB + 2) % 3];

        const incVerts: IVec3Like[] = [
            Vec3.add(incCenter, Vec3.add(
                Vec3.multiplyScalar(incNormal, incFaceOffset),
                Vec3.add(Vec3.multiplyScalar(incTangent1, -hT1), Vec3.multiplyScalar(incTangent2, -hT2))
            )),
            Vec3.add(incCenter, Vec3.add(
                Vec3.multiplyScalar(incNormal, incFaceOffset),
                Vec3.add(Vec3.multiplyScalar(incTangent1, hT1), Vec3.multiplyScalar(incTangent2, -hT2))
            )),
            Vec3.add(incCenter, Vec3.add(
                Vec3.multiplyScalar(incNormal, incFaceOffset),
                Vec3.add(Vec3.multiplyScalar(incTangent1, hT1), Vec3.multiplyScalar(incTangent2, hT2))
            )),
            Vec3.add(incCenter, Vec3.add(
                Vec3.multiplyScalar(incNormal, incFaceOffset),
                Vec3.add(Vec3.multiplyScalar(incTangent1, -hT1), Vec3.multiplyScalar(incTangent2, hT2))
            )),
        ];

        const refNormal = refAxes[bestDotA >= bestDotB ? bestA : bestB];
        const refFaceDist = refSign * refHalf[bestDotA >= bestDotB ? bestA : bestB];
        const refNormalActual = isARef
            ? (Vec3.dot(normal, refAxes[bestA]) > 0 ? refAxes[bestA] : Vec3.negate(refAxes[bestA]))
            : (Vec3.dot(negNormal, refAxes[bestB]) > 0 ? refAxes[bestB] : Vec3.negate(refAxes[bestB]));

        const refFaceIdx = bestDotA >= bestDotB ? bestA : bestB;
        const refT1Idx = (refFaceIdx + 1) % 3;
        const refT2Idx = (refFaceIdx + 2) % 3;
        const refT1 = refAxes[refT1Idx];
        const refT2 = refAxes[refT2Idx];
        const refH1 = refHalf[refT1Idx];
        const refH2 = refHalf[refT2Idx];

        let clipped = incVerts;
        clipped = this.clipSegmentToLine(clipped, refT1, refCenter, refH1);
        if (clipped.length < 2) return [];
        clipped = this.clipSegmentToLine(clipped, Vec3.negate(refT1), refCenter, refH1);
        if (clipped.length < 2) return [];
        clipped = this.clipSegmentToLine(clipped, refT2, refCenter, refH2);
        if (clipped.length < 2) return [];
        clipped = this.clipSegmentToLine(clipped, Vec3.negate(refT2), refCenter, refH2);
        if (clipped.length < 2) return [];

        const result: { worldPoint: IVec3Like; separation: number }[] = [];
        const refFaceCenter = Vec3.add(refCenter, Vec3.multiplyScalar(refNormalActual, refFaceDist));
        for (const pt of clipped) {
            if (result.length >= 4) break;
            const sep = Vec3.dot(Vec3.subtract(pt, refFaceCenter), refNormalActual);
            if (sep >= -(totalPen * 0.1 + PhysicsConstants.ALLOWED_PENETRATION)) {
                result.push({ worldPoint: pt, separation: sep - totalPen });
            }
        }
        return result;
    }

    private clipSegmentToLine(
        verts: IVec3Like[], planeNormal: IVec3Like, planePoint: IVec3Like, planeOffset: number
    ): IVec3Like[] {
        const out: IVec3Like[] = [];
        const n = verts.length;
        if (n < 2) return out;

        const planeDot = Vec3.dot(planePoint, planeNormal) + planeOffset;
        const ds: number[] = new Array(n);
        for (let i = 0; i < n; i++) {
            ds[i] = Vec3.dot(verts[i], planeNormal) - planeDot;
        }

        for (let i = 0; i < n; i++) {
            const curr = verts[i];
            const next = verts[(i + 1) % n];
            const dc = ds[i];
            const dn = ds[(i + 1) % n];
            if (dc <= 0) out.push(curr);
            if (dc * dn < 0) {
                const t = dc / (dc - dn);
                out.push({
                    x: curr.x + t * (next.x - curr.x),
                    y: curr.y + t * (next.y - curr.y),
                    z: curr.z + t * (next.z - curr.z),
                });
            }
        }
        return out;
    }

    private capCap(dA: IShapeDescriptor3D, dB: IShapeDescriptor3D): ICollisionResult | null {
        if (!isCapsuleDef(dA.def) || !isCapsuleDef(dB.def)) return null;
        const p1A = transformPoint3D(dA.def.p1, this._host.bodyManager.getPosition(dA.bodyId), this._host.bodyManager.getRotation(dA.bodyId));
        const p2A = transformPoint3D(dA.def.p2, this._host.bodyManager.getPosition(dA.bodyId), this._host.bodyManager.getRotation(dA.bodyId));
        const p1B = transformPoint3D(dB.def.p1, this._host.bodyManager.getPosition(dB.bodyId), this._host.bodyManager.getRotation(dB.bodyId));
        const p2B = transformPoint3D(dB.def.p2, this._host.bodyManager.getPosition(dB.bodyId), this._host.bodyManager.getRotation(dB.bodyId));
        const closest = this.segSeg(p1A, p2A, p1B, p2B);
        const rSum = dA.def.radius + dB.def.radius;
        if (closest.distSq > rSum * rSum) return null;
        const dist = Math.sqrt(closest.distSq);
        const invD = dist > PhysicsConstants.EPSILON ? 1 / dist : 0;
        const dx = closest.pointB.x - closest.pointA.x, dy = closest.pointB.y - closest.pointA.y, dz = closest.pointB.z - closest.pointA.z;
        return { normal: { x: dx * invD, y: dy * invD, z: dz * invD }, point: midpointVec3(closest.pointA, closest.pointB), penetration: rSum - dist };
    }

    private capSph(cap: IShapeDescriptor3D, sph: IShapeDescriptor3D): ICollisionResult | null {
        if (!isCapsuleDef(cap.def) || !isSphereDef(sph.def)) return null;
        const p1 = transformPoint3D(cap.def.p1, this._host.bodyManager.getPosition(cap.bodyId), this._host.bodyManager.getRotation(cap.bodyId));
        const p2 = transformPoint3D(cap.def.p2, this._host.bodyManager.getPosition(cap.bodyId), this._host.bodyManager.getRotation(cap.bodyId));
        const sC = this._host.getShapeWorldCenter(sph);
        const closest = this.closestSeg(sC, p1, p2);
        const delta = Vec3.subtract(sC, closest);
        const dist = Vec3.len(delta);
        const rSum = cap.def.radius + sph.def.radius;
        if (dist > rSum) return null;
        const invD = dist > PhysicsConstants.EPSILON ? 1 / dist : 0;
        return { normal: { x: delta.x * invD, y: delta.y * invD, z: delta.z * invD }, point: closest, penetration: rSum - dist };
    }

    private capBox(cap: IShapeDescriptor3D, box: IShapeDescriptor3D): ICollisionResult | null {
        if (!isCapsuleDef(cap.def) || !isBoxDef(box.def)) return null;
        const p1 = transformPoint3D(cap.def.p1, this._host.bodyManager.getPosition(cap.bodyId), this._host.bodyManager.getRotation(cap.bodyId));
        const p2 = transformPoint3D(cap.def.p2, this._host.bodyManager.getPosition(cap.bodyId), this._host.bodyManager.getRotation(cap.bodyId));
        const bC = transformPoint3D(box.def.center, this._host.bodyManager.getPosition(box.bodyId), this._host.bodyManager.getRotation(box.bodyId));
        const bRot = Quat.multiply(this._host.bodyManager.getRotation(box.bodyId), box.def.rotation ?? IDENTITY_ROTATION);
        const l1 = inverseTransformPoint3D(p1, bC, bRot), l2 = inverseTransformPoint3D(p2, bC, bRot);
        const hE = box.def.halfExtents;
        const c1 = { x: clamp(l1.x, -hE.x, hE.x), y: clamp(l1.y, -hE.y, hE.y), z: clamp(l1.z, -hE.z, hE.z) };
        const c2 = { x: clamp(l2.x, -hE.x, hE.x), y: clamp(l2.y, -hE.y, hE.y), z: clamp(l2.z, -hE.z, hE.z) };
        const closest = this.closestSeg({ x: 0, y: 0, z: 0 }, c1, c2);
        const dist = Vec3.len(closest);
        if (dist > cap.def.radius) return null;
        const invD = dist > PhysicsConstants.EPSILON ? 1 / dist : 0;
        const localN = { x: closest.x * invD, y: closest.y * invD, z: closest.z * invD };
        return { normal: Quat.rotateVector(bRot, localN), point: transformPoint3D({ x: 0, y: 0, z: 0 }, bC, bRot), penetration: cap.def.radius - dist };
    }

    private aabbApprox(dA: IShapeDescriptor3D, dB: IShapeDescriptor3D, aabbA: IAabb3D, aabbB: IAabb3D): ICollisionResult | null {
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

    private segSeg(a1: IVec3Like, a2: IVec3Like, b1: IVec3Like, b2: IVec3Like): { pointA: IVec3Like; pointB: IVec3Like; distSq: number } {
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

    private closestSeg(point: IVec3Like, a: IVec3Like, b: IVec3Like): IVec3Like {
        const ab = Vec3.subtract(b, a), ap = Vec3.subtract(point, a);
        const ab2 = Vec3.dot(ab, ab);
        const t = ab2 > PhysicsConstants.EPSILON ? clamp(Vec3.dot(ap, ab) / ab2, 0, 1) : 0;
        return { x: a.x + ab.x * t, y: a.y + ab.y * t, z: a.z + ab.z * t };
    }
}
