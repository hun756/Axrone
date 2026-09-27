import { Vec3, clamp, type IVec3Like, type IQuatLike } from '@axrone/numeric';
import { PhysicsConstants } from '../types';
import type { Impulse } from '../types';
import type { BodyId3D } from '../types/physics-3d';
import {
    type IResolvedContactManifold3D,
    type IMutableContactPoint3D,
    midpointVec3,
    transformPoint3D,
} from './physics-world-3d-shared';

export interface ISolverHost {
    readonly bodyManager: {
        getPosition(bodyId: BodyId3D): IVec3Like;
        getRotation(bodyId: BodyId3D): IQuatLike;
        getBodyType(bodyId: BodyId3D): number;
        isEnabled(bodyId: BodyId3D): boolean;
        getInverseMass(bodyId: BodyId3D): number;
        getInverseInertia(bodyId: BodyId3D): IVec3Like;
        isFixedRotation(bodyId: BodyId3D): boolean;
        getLinearVelocity(bodyId: BodyId3D): IVec3Like;
        getAngularVelocity(bodyId: BodyId3D): IVec3Like;
        applyImpulse(bodyId: BodyId3D, impulse: IVec3Like, point: IVec3Like): void;
        setPosition(bodyId: BodyId3D, position: IVec3Like): void;
    };
}

export class PhysicsWorld3DSolver {
    constructor(private readonly _host: ISolverHost) {}

    solveContactVelocity(manifold: IResolvedContactManifold3D): void {
        if (manifold.sensor) return;

        const bm = this._host.bodyManager;
        const cA = bm.getPosition(manifold.bodyIdA);
        const cB = bm.getPosition(manifold.bodyIdB);
        const invMassA = this.invMass(manifold.bodyIdA);
        const invMassB = this.invMass(manifold.bodyIdB);
        const invIA = this.invInertia(manifold.bodyIdA);
        const invIB = this.invInertia(manifold.bodyIdB);
        const iSum = invMassA + invMassB;
        if (iSum <= PhysicsConstants.EPSILON) return;

        for (const point of manifold.points) {
            const wp = midpointVec3(
                this.lp2w(manifold.bodyIdA, point.localPointA),
                this.lp2w(manifold.bodyIdB, point.localPointB)
            );
            const rA = Vec3.subtract(wp, cA);
            const rB = Vec3.subtract(wp, cB);

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

            const relV = Vec3.subtract(this.getWPV(manifold.bodyIdB, wp), this.getWPV(manifold.bodyIdA, wp));
            const ns = Vec3.dot(relV, manifold.normal);
            if (ns < 0) {
                const rest = ns < -PhysicsConstants.VELOCITY_THRESHOLD ? manifold.restitution : 0;
                let dPn = normalMass * (-(1 + rest) * ns);
                const newPn = Math.max((point.normalImpulse as number) + dPn, 0);
                dPn = newPn - (point.normalImpulse as number);
                point.normalImpulse = newPn as unknown as Impulse;
                this.applyImp(manifold.bodyIdA, Vec3.negate(Vec3.multiplyScalar(manifold.normal, dPn)), wp);
                this.applyImp(manifold.bodyIdB, Vec3.multiplyScalar(manifold.normal, dPn), wp);
            } else {
                point.normalImpulse = 0 as unknown as Impulse;
                point.tangentImpulse1 = 0 as unknown as Impulse;
            }

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
            this.applyImp(manifold.bodyIdA, Vec3.negate(Vec3.multiplyScalar(tan, dPt)), wp);
            this.applyImp(manifold.bodyIdB, Vec3.multiplyScalar(tan, dPt), wp);
        }
    }

    warmStartContact(manifold: IResolvedContactManifold3D): void {
        for (let i = 0; i < manifold.pointCount; i++) {
            const point = manifold.points[i];
            const normal = (point.normalImpulse as number) ?? 0;
            if (normal === 0) continue;

            const wp = midpointVec3(
                this.lp2w(manifold.bodyIdA, point.localPointA),
                this.lp2w(manifold.bodyIdB, point.localPointB)
            );

            const normalImpulse = Vec3.multiplyScalar(manifold.normal, normal);
            this.applyImp(manifold.bodyIdA, Vec3.negate(normalImpulse), wp);
            this.applyImp(manifold.bodyIdB, normalImpulse, wp);
        }
    }

    correctContactPositions(manifold: IResolvedContactManifold3D, beta: number): void {
        if (manifold.sensor) return;

        for (const point of manifold.points) {
            const sep = this.getSep(manifold, point);
            const pen = Math.max(0, -sep);
            if (pen <= PhysicsConstants.ALLOWED_PENETRATION) continue;
            const iA = this.invMass(manifold.bodyIdA), iB = this.invMass(manifold.bodyIdB);
            const iSum = iA + iB;
            if (iSum <= PhysicsConstants.EPSILON) continue;
            const corr = Vec3.multiplyScalar(manifold.normal, ((pen - PhysicsConstants.ALLOWED_PENETRATION) * beta) / iSum);
            if (iA > 0) this._host.bodyManager.setPosition(manifold.bodyIdA, Vec3.subtract(this._host.bodyManager.getPosition(manifold.bodyIdA), Vec3.multiplyScalar(corr, iA)));
            if (iB > 0) this._host.bodyManager.setPosition(manifold.bodyIdB, Vec3.add(this._host.bodyManager.getPosition(manifold.bodyIdB), Vec3.multiplyScalar(corr, iB)));
            point.separation = this.getSep(manifold, point);
        }
    }

    countIslands(manifolds: ReadonlyMap<number, IResolvedContactManifold3D>): number {
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

    invMass(bodyId: BodyId3D): number {
        if (this._host.bodyManager.getBodyType(bodyId) !== 2 || !this._host.bodyManager.isEnabled(bodyId)) return 0;
        return this._host.bodyManager.getInverseMass(bodyId);
    }

    invInertia(bodyId: BodyId3D): IVec3Like {
        const bm = this._host.bodyManager;
        if (bm.getBodyType(bodyId) !== 2 || !bm.isEnabled(bodyId) || bm.isFixedRotation(bodyId)) {
            return { x: 0, y: 0, z: 0 };
        }
        return bm.getInverseInertia(bodyId);
    }

    private applyImp(bodyId: BodyId3D, impulse: IVec3Like, point: IVec3Like): void {
        if (this._host.bodyManager.getBodyType(bodyId) !== 2) return;
        this._host.bodyManager.applyImpulse(bodyId, impulse, point);
    }

    private getWPV(bodyId: BodyId3D, point: IVec3Like): IVec3Like {
        const center = this._host.bodyManager.getPosition(bodyId);
        return Vec3.add(this._host.bodyManager.getLinearVelocity(bodyId), Vec3.cross(this._host.bodyManager.getAngularVelocity(bodyId), Vec3.subtract(point, center)));
    }

    private lp2w(bodyId: BodyId3D, localPoint: IVec3Like): IVec3Like {
        return transformPoint3D(localPoint, this._host.bodyManager.getPosition(bodyId), this._host.bodyManager.getRotation(bodyId));
    }

    private getSep(manifold: IResolvedContactManifold3D, point: IMutableContactPoint3D): number {
        return Vec3.dot(Vec3.subtract(this.lp2w(manifold.bodyIdB, point.localPointB), this.lp2w(manifold.bodyIdA, point.localPointA)), manifold.normal);
    }
}
