import { Vec3, type IVec3Like } from '@axrone/numeric';
import { makeCollisionPairKey } from '@axrone/physics-core';
import { AABB3D } from '@axrone/geometry';
import type { BodyId3D, ShapeId3D } from '../types/physics-3d';
import {
    BODY_TYPE_STATIC,
    type IAabb3D,
    type IShapeDescriptor3D,
    type IShapePairCandidate3D,
    shouldShapeFiltersCollide,
} from './physics-world-3d-shared';
import type { DynamicAABBTree3D } from './broadphase-3d';

export interface IBroadphaseHost {
    readonly bodyManager: {
        isEnabled(bodyId: BodyId3D): boolean;
        getBodyType(bodyId: BodyId3D): number;
    };
    readonly shapeDescriptors: ReadonlyMap<ShapeId3D, IShapeDescriptor3D>;
    readonly getCollisionFilter: () => { shouldCollide(a: ShapeId3D, b: ShapeId3D): boolean } | null;
    readonly computeShapeAabb: (descriptor: IShapeDescriptor3D) => IAabb3D;
}

export class PhysicsWorld3DBroadphase {
    private readonly _shapeProxyMap = new Map<ShapeId3D, number>();
    private readonly _shapePreviousCenter = new Map<ShapeId3D, IVec3Like>();
    private readonly _candidatePairs: IShapePairCandidate3D[] = [];
    private readonly _scratchAabb: { min: IVec3Like; max: IVec3Like } = {
        min: { x: 0, y: 0, z: 0 },
        max: { x: 0, y: 0, z: 0 },
    };
    private readonly _scratchCenter: IVec3Like = { x: 0, y: 0, z: 0 };
    private readonly _scratchDisp: IVec3Like = { x: 0, y: 0, z: 0 };

    constructor(private readonly _broadphase: DynamicAABBTree3D<ShapeId3D>) {}

    collectPotentialCollisionPairs(host: IBroadphaseHost): IShapePairCandidate3D[] {
        this._candidatePairs.length = 0;

        const filter = host.getCollisionFilter();
        for (const d of host.shapeDescriptors.values()) {
            if (!host.bodyManager.isEnabled(d.bodyId)) continue;

            const rawAabb = host.computeShapeAabb(d);
            this._scratchAabb.min.x = rawAabb.min.x;
            this._scratchAabb.min.y = rawAabb.min.y;
            this._scratchAabb.min.z = rawAabb.min.z;
            this._scratchAabb.max.x = rawAabb.max.x;
            this._scratchAabb.max.y = rawAabb.max.y;
            this._scratchAabb.max.z = rawAabb.max.z;

            this._scratchCenter.x = (rawAabb.min.x + rawAabb.max.x) * 0.5;
            this._scratchCenter.y = (rawAabb.min.y + rawAabb.max.y) * 0.5;
            this._scratchCenter.z = (rawAabb.min.z + rawAabb.max.z) * 0.5;

            const existing = this._shapeProxyMap.get(d.id);
            if (existing !== undefined) {
                const prev = this._shapePreviousCenter.get(d.id);
                if (prev) {
                    this._scratchDisp.x = this._scratchCenter.x - prev.x;
                    this._scratchDisp.y = this._scratchCenter.y - prev.y;
                    this._scratchDisp.z = this._scratchCenter.z - prev.z;
                } else {
                    this._scratchDisp.x = 0;
                    this._scratchDisp.y = 0;
                    this._scratchDisp.z = 0;
                }
                this._broadphase.moveProxy(existing, this._scratchAabb as unknown as AABB3D, this._scratchDisp);
            } else {
                const pid = this._broadphase.createProxy(this._scratchAabb as unknown as AABB3D, d.id);
                this._shapeProxyMap.set(d.id, pid);
            }
            const prevCenter = this._shapePreviousCenter.get(d.id);
            if (prevCenter) {
                prevCenter.x = this._scratchCenter.x;
                prevCenter.y = this._scratchCenter.y;
                prevCenter.z = this._scratchCenter.z;
            } else {
                this._shapePreviousCenter.set(d.id, {
                    x: this._scratchCenter.x,
                    y: this._scratchCenter.y,
                    z: this._scratchCenter.z,
                });
            }
        }

        this._broadphase.queryPairs((pA, pB) => {
            const sA = this._broadphase.getUserData(pA);
            const sB = this._broadphase.getUserData(pB);
            if (!sA || !sB) return true;
            const dA = host.shapeDescriptors.get(sA);
            const dB = host.shapeDescriptors.get(sB);
            if (!dA || !dB) return true;
            if (dA.bodyId === dB.bodyId) return true;
            if (!host.bodyManager.isEnabled(dA.bodyId)) return true;
            if (!host.bodyManager.isEnabled(dB.bodyId)) return true;
            if (!shouldShapeFiltersCollide(dA.filter, dB.filter)) return true;
            if (filter && !filter.shouldCollide(dA.id, dB.id)) return true;
            const tA = host.bodyManager.getBodyType(dA.bodyId);
            const tB = host.bodyManager.getBodyType(dB.bodyId);
            if (tA === BODY_TYPE_STATIC && tB === BODY_TYPE_STATIC) return true;

            this._candidatePairs.push({
                descriptorA: dA,
                descriptorB: dB,
                aabbA: this._broadphase.getAABB(pA),
                aabbB: this._broadphase.getAABB(pB),
                pairKey: makeCollisionPairKey(Number(dA.id), Number(dB.id)),
            });
            return true;
        });

        return this._candidatePairs;
    }

    pruneShape(shapeId: ShapeId3D): void {
        const proxy = this._shapeProxyMap.get(shapeId);
        if (proxy !== undefined) {
            this._broadphase.destroyProxy(proxy);
            this._shapeProxyMap.delete(shapeId);
        }
        this._shapePreviousCenter.delete(shapeId);
    }
}
