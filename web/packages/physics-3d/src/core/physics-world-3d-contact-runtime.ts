import type { IVec3Like } from '@axrone/numeric';
import type {
    ContactId,
    ICollisionFilter,
    IContactManifold3D,
    Impulse,
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
import {
    BODY_TYPE_DYNAMIC,
    CONSTRAINT_TYPE_SLIDER,
    CONSTRAINT_TYPE_SPRING,
    IDENTITY_ROTATION,
    SHAPE_TYPE_BOX,
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
    intersectsAabb,
    lengthVec3,
    midpointVec3,
    multiplyQuat,
    negateVec3,
    normalizeVec3,
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

    constructor(private readonly _host: IPhysicsWorld3DContactRuntimeHost) {}

    get contactCount(): number {
        return this._contactManifolds.size;
    }

    pruneShape(shapeId: ShapeId3D): void {
        for (const [pairKey, manifold] of this._contactManifolds) {
            if (manifold.shapeIdA === shapeId || manifold.shapeIdB === shapeId) {
                this._contactManifolds.delete(pairKey);
            }
        }
    }

    solve(deltaTime: number, velocityIterations: number, positionIterations: number): void {
        const previousManifolds = this._contactManifolds;

        const broadphaseStart = performance.now();
        const pairs = this._collectPotentialCollisionPairs();
        const broadphaseTime = performance.now() - broadphaseStart;

        const narrowphaseStart = performance.now();
        const nextManifolds = new Map<string, IResolvedContactManifold3D>();
        for (const pair of pairs) {
            const manifold = this._buildContactManifold(
                pair,
                previousManifolds.get(pair.pairKey) ?? null
            );
            if (manifold) {
                nextManifolds.set(pair.pairKey, manifold);
            }
        }
        const narrowphaseTime = performance.now() - narrowphaseStart;

        const profiler = this._host.getProfiler();
        if (profiler) {
            profiler.broadphaseTime = broadphaseTime;
            profiler.narrowphaseTime = narrowphaseTime;
            profiler.collisionTime = broadphaseTime + narrowphaseTime;
        }

        this._emitPreSolveEvents(previousManifolds, nextManifolds);

        const constraints = [...this._host.constraintDescriptors.values()].filter((constraint) => constraint.enabled);
        const solvableContacts = [...nextManifolds.values()].filter((manifold) => !manifold.sensor);

        const solveVelocityStart = performance.now();
        for (let iteration = 0; iteration < velocityIterations; iteration += 1) {
            for (const manifold of solvableContacts) {
                this._solveContactVelocity(manifold);
            }
            for (const constraint of constraints) {
                this._solveManagedConstraintVelocity(constraint, deltaTime);
            }
        }
        const solveVelocityTime = performance.now() - solveVelocityStart;

        const solvePositionStart = performance.now();
        for (let iteration = 0; iteration < positionIterations; iteration += 1) {
            for (const manifold of solvableContacts) {
                this._solveContactPosition(manifold);
            }
            for (const constraint of constraints) {
                this._solveManagedConstraintPosition(constraint);
            }
        }
        const solvePositionTime = performance.now() - solvePositionStart;

        for (const manifold of solvableContacts) {
            this._stabilizeRestingContactVelocity(manifold);
        }

        for (const manifold of solvableContacts) {
            for (const point of manifold.points) {
                point.separation = this._getCurrentPointSeparation(manifold, point);
            }
        }

        if (profiler) {
            profiler.solveVelocityTime = solveVelocityTime;
            profiler.solvePositionTime = solvePositionTime;
            profiler.solveTime = solveVelocityTime + solvePositionTime;
        }

        this._emitContactLifecycleEvents(previousManifolds, nextManifolds);
        this._contactManifolds = nextManifolds;
    }

    private _collectPotentialCollisionPairs(): IShapePairCandidate3D[] {
        const descriptors = [...this._host.shapeDescriptors.values()].sort((left, right) => left.id - right.id);
        const candidates: IShapePairCandidate3D[] = [];
        const aabbs = new Map<ShapeId3D, IAabb3D>();
        const collisionFilter = this._host.getCollisionFilter();

        for (const descriptor of descriptors) {
            aabbs.set(descriptor.id, this._host.computeShapeAabb(descriptor));
        }

        for (let leftIndex = 0; leftIndex < descriptors.length; leftIndex += 1) {
            const descriptorA = descriptors[leftIndex];
            if (!this._host.bodyManager.isEnabled(descriptorA.bodyId)) {
                continue;
            }

            for (let rightIndex = leftIndex + 1; rightIndex < descriptors.length; rightIndex += 1) {
                const descriptorB = descriptors[rightIndex];
                if (descriptorA.bodyId === descriptorB.bodyId) {
                    continue;
                }
                if (!this._host.bodyManager.isEnabled(descriptorB.bodyId)) {
                    continue;
                }
                if (!shouldShapeFiltersCollide(descriptorA.filter, descriptorB.filter)) {
                    continue;
                }
                if (collisionFilter && !collisionFilter.shouldCollide(descriptorA.id, descriptorB.id)) {
                    continue;
                }
                if (!this._shouldConnectedBodiesCollide(descriptorA.bodyId, descriptorB.bodyId)) {
                    continue;
                }

                const aabbA = aabbs.get(descriptorA.id)!;
                const aabbB = aabbs.get(descriptorB.id)!;
                if (!intersectsAabb(aabbA, aabbB)) {
                    continue;
                }

                candidates.push({
                    descriptorA,
                    descriptorB,
                    aabbA,
                    aabbB,
                    pairKey: `${descriptorA.id}:${descriptorB.id}`,
                });
            }
        }

        return candidates;
    }

    private _shouldConnectedBodiesCollide(bodyIdA: BodyId3D, bodyIdB: BodyId3D): boolean {
        for (const descriptor of this._host.constraintDescriptors.values()) {
            if (!descriptor.enabled || descriptor.collideConnected) {
                continue;
            }

            const matchesForward =
                descriptor.def.bodyIdA === bodyIdA && descriptor.def.bodyIdB === bodyIdB;
            const matchesReverse =
                descriptor.def.bodyIdA === bodyIdB && descriptor.def.bodyIdB === bodyIdA;
            if (matchesForward || matchesReverse) {
                return false;
            }
        }

        return true;
    }

    private _buildContactManifold(
        pair: IShapePairCandidate3D,
        previous: IResolvedContactManifold3D | null
    ): IResolvedContactManifold3D | null {
        const collision = this._detectCollision(pair.descriptorA, pair.descriptorB, pair.aabbA, pair.aabbB);
        if (!collision) {
            return null;
        }

        const { tangent1, tangent2 } = buildOrthonormalBasis(collision.normal);
        const pointId = previous?.points[0]?.id ?? ((this._nextContactId++ as unknown) as ContactId);
        const worldPointA = this._getContactPointOnShape(pair.descriptorA, collision, true);
        const worldPointB = this._getContactPointOnShape(pair.descriptorB, collision, false);
        const localPointA = inverseTransformPoint3D(
            worldPointA,
            this._host.bodyManager.getPosition(pair.descriptorA.bodyId),
            this._host.bodyManager.getRotation(pair.descriptorA.bodyId)
        );
        const localPointB = inverseTransformPoint3D(
            worldPointB,
            this._host.bodyManager.getPosition(pair.descriptorB.bodyId),
            this._host.bodyManager.getRotation(pair.descriptorB.bodyId)
        );
        const friction = Math.sqrt(pair.descriptorA.material.friction * pair.descriptorB.material.friction);
        const restitution = Math.max(
            pair.descriptorA.material.restitution,
            pair.descriptorB.material.restitution
        );

        return {
            id: previous?.id ?? this._nextManifoldId++,
            pairKey: pair.pairKey,
            descriptorA: pair.descriptorA,
            descriptorB: pair.descriptorB,
            bodyIdA: pair.descriptorA.bodyId,
            bodyIdB: pair.descriptorB.bodyId,
            shapeIdA: pair.descriptorA.id,
            shapeIdB: pair.descriptorB.id,
            normal: collision.normal,
            tangent1,
            tangent2,
            pointCount: 1,
            points: [
                {
                    id: pointId,
                    localPointA,
                    localPointB,
                    normalImpulse: previous?.points[0]?.normalImpulse ?? (0 as Impulse),
                    tangentImpulse1: previous?.points[0]?.tangentImpulse1 ?? (0 as Impulse),
                    tangentImpulse2: previous?.points[0]?.tangentImpulse2 ?? (0 as Impulse),
                    separation: dotVec3(subVec3(worldPointB, worldPointA), collision.normal),
                },
            ],
            sensor: pair.descriptorA.isSensor || pair.descriptorB.isSensor,
            friction,
            restitution,
        };
    }

    private _getContactPointOnShape(
        descriptor: IShapeDescriptor3D,
        collision: { normal: IVec3Like; point: IVec3Like; penetration: number },
        firstShape: boolean
    ): IVec3Like {
        if (descriptor.type === SHAPE_TYPE_SPHERE) {
            const radius =
                (descriptor.def as Extract<SupportedShapeDef3D, { kind: typeof SHAPE_TYPE_SPHERE }>).radius;
            const direction = firstShape ? collision.normal : negateVec3(collision.normal);
            return addVec3(this._host.getShapeWorldCenter(descriptor), scaleVec3(direction, radius));
        }

        const offset = scaleVec3(
            collision.normal,
            collision.penetration * (firstShape ? 0.5 : -0.5)
        );
        return addVec3(collision.point, offset);
    }

    private _detectCollision(
        descriptorA: IShapeDescriptor3D,
        descriptorB: IShapeDescriptor3D,
        aabbA: IAabb3D,
        aabbB: IAabb3D
    ): { normal: IVec3Like; point: IVec3Like; penetration: number } | null {
        if (descriptorA.type === SHAPE_TYPE_SPHERE && descriptorB.type === SHAPE_TYPE_SPHERE) {
            return this._collideSphereSphere(descriptorA, descriptorB);
        }
        if (descriptorA.type === SHAPE_TYPE_SPHERE && descriptorB.type === SHAPE_TYPE_BOX) {
            return this._collideSphereBox(descriptorA, descriptorB);
        }
        if (descriptorA.type === SHAPE_TYPE_BOX && descriptorB.type === SHAPE_TYPE_SPHERE) {
            const collision = this._collideSphereBox(descriptorB, descriptorA);
            return collision
                ? {
                      normal: negateVec3(collision.normal),
                      point: collision.point,
                      penetration: collision.penetration,
                  }
                : null;
        }

        return this._collideAabbApproximation(descriptorA, descriptorB, aabbA, aabbB);
    }

    private _collideSphereSphere(
        descriptorA: Extract<IShapeDescriptor3D, { type: typeof SHAPE_TYPE_SPHERE }> | IShapeDescriptor3D,
        descriptorB: Extract<IShapeDescriptor3D, { type: typeof SHAPE_TYPE_SPHERE }> | IShapeDescriptor3D
    ): { normal: IVec3Like; point: IVec3Like; penetration: number } | null {
        const centerA = this._host.getShapeWorldCenter(descriptorA);
        const centerB = this._host.getShapeWorldCenter(descriptorB);
        const delta = subVec3(centerB, centerA);
        const distance = lengthVec3(delta);
        const radiusSum =
            (descriptorA.def as Extract<SupportedShapeDef3D, { kind: typeof SHAPE_TYPE_SPHERE }>).radius +
            (descriptorB.def as Extract<SupportedShapeDef3D, { kind: typeof SHAPE_TYPE_SPHERE }>).radius;

        if (distance > radiusSum) {
            return null;
        }

        const normal = distance > PhysicsConstants.EPSILON ? scaleVec3(delta, 1 / distance) : { x: 1, y: 0, z: 0 };
        const penetration = radiusSum - distance;
        const point = addVec3(
            centerA,
            scaleVec3(
                normal,
                (descriptorA.def as Extract<SupportedShapeDef3D, { kind: typeof SHAPE_TYPE_SPHERE }>).radius -
                    penetration * 0.5
            )
        );

        return { normal, point, penetration };
    }

    private _collideSphereBox(
        sphereDescriptor: Extract<IShapeDescriptor3D, { type: typeof SHAPE_TYPE_SPHERE }> | IShapeDescriptor3D,
        boxDescriptor: Extract<IShapeDescriptor3D, { type: typeof SHAPE_TYPE_BOX }> | IShapeDescriptor3D
    ): { normal: IVec3Like; point: IVec3Like; penetration: number } | null {
        const sphereCenter = this._host.getShapeWorldCenter(sphereDescriptor);
        const boxBodyPosition = this._host.bodyManager.getPosition(boxDescriptor.bodyId);
        const boxBodyRotation = this._host.bodyManager.getRotation(boxDescriptor.bodyId);
        const boxDef = boxDescriptor.def as Extract<SupportedShapeDef3D, { kind: typeof SHAPE_TYPE_BOX }>;
        const boxCenter = transformPoint3D(boxDef.center, boxBodyPosition, boxBodyRotation);
        const boxRotation = multiplyQuat(boxBodyRotation, boxDef.rotation ?? IDENTITY_ROTATION);
        const localSphereCenter = inverseTransformPoint3D(sphereCenter, boxCenter, boxRotation);
        const closestPointLocal = {
            x: clamp(localSphereCenter.x, -boxDef.halfExtents.x, boxDef.halfExtents.x),
            y: clamp(localSphereCenter.y, -boxDef.halfExtents.y, boxDef.halfExtents.y),
            z: clamp(localSphereCenter.z, -boxDef.halfExtents.z, boxDef.halfExtents.z),
        };
        const closestPointWorld = transformPoint3D(closestPointLocal, boxCenter, boxRotation);
        const delta = subVec3(sphereCenter, closestPointWorld);
        const distance = lengthVec3(delta);
        const sphereRadius =
            (sphereDescriptor.def as Extract<SupportedShapeDef3D, { kind: typeof SHAPE_TYPE_SPHERE }>).radius;

        if (distance > sphereRadius) {
            return null;
        }

        if (distance > PhysicsConstants.EPSILON) {
            return {
                normal: scaleVec3(delta, -1 / distance),
                point: closestPointWorld,
                penetration: sphereRadius - distance,
            };
        }

        const sphereVelocityLocal = inverseRotateVec3(
            this._host.bodyManager.getLinearVelocity(sphereDescriptor.bodyId),
            boxRotation
        );
        const velocityMagnitudes = [
            Math.abs(sphereVelocityLocal.x),
            Math.abs(sphereVelocityLocal.y),
            Math.abs(sphereVelocityLocal.z),
        ];
        const distancesToNearestFaces = [
            boxDef.halfExtents.x - Math.abs(localSphereCenter.x),
            boxDef.halfExtents.y - Math.abs(localSphereCenter.y),
            boxDef.halfExtents.z - Math.abs(localSphereCenter.z),
        ];

        let axisIndex = 0;
        if (velocityMagnitudes[1] > velocityMagnitudes[axisIndex]) axisIndex = 1;
        if (velocityMagnitudes[2] > velocityMagnitudes[axisIndex]) axisIndex = 2;
        if (velocityMagnitudes[axisIndex] <= PhysicsConstants.EPSILON) {
            axisIndex = 0;
            if (distancesToNearestFaces[1] < distancesToNearestFaces[axisIndex]) axisIndex = 1;
            if (distancesToNearestFaces[2] < distancesToNearestFaces[axisIndex]) axisIndex = 2;
        }

        const axisSign =
            axisIndex === 0
                ? Math.sign(sphereVelocityLocal.x) || Math.sign(localSphereCenter.x) || 1
                : axisIndex === 1
                  ? Math.sign(sphereVelocityLocal.y) || Math.sign(localSphereCenter.y) || 1
                  : Math.sign(sphereVelocityLocal.z) || Math.sign(localSphereCenter.z) || 1;
        const axisNormalLocal =
            axisIndex === 0
                ? { x: axisSign, y: 0, z: 0 }
                : axisIndex === 1
                  ? { x: 0, y: axisSign, z: 0 }
                  : { x: 0, y: 0, z: axisSign };
        const axisNormal = rotateVec3(axisNormalLocal, boxRotation);

        const escapeDistance =
            axisIndex === 0
                ? axisNormalLocal.x > 0
                    ? localSphereCenter.x + boxDef.halfExtents.x
                    : boxDef.halfExtents.x - localSphereCenter.x
                : axisIndex === 1
                  ? axisNormalLocal.y > 0
                      ? localSphereCenter.y + boxDef.halfExtents.y
                      : boxDef.halfExtents.y - localSphereCenter.y
                  : axisNormalLocal.z > 0
                    ? localSphereCenter.z + boxDef.halfExtents.z
                    : boxDef.halfExtents.z - localSphereCenter.z;
        const facePointLocal = {
            ...closestPointLocal,
            ...(axisIndex === 0 ? { x: axisNormalLocal.x > 0 ? -boxDef.halfExtents.x : boxDef.halfExtents.x } : {}),
            ...(axisIndex === 1 ? { y: axisNormalLocal.y > 0 ? -boxDef.halfExtents.y : boxDef.halfExtents.y } : {}),
            ...(axisIndex === 2 ? { z: axisNormalLocal.z > 0 ? -boxDef.halfExtents.z : boxDef.halfExtents.z } : {}),
        };

        return {
            normal: axisNormal,
            point: transformPoint3D(facePointLocal, boxCenter, boxRotation),
            penetration: sphereRadius + escapeDistance,
        };
    }

    private _collideAabbApproximation(
        descriptorA: IShapeDescriptor3D,
        descriptorB: IShapeDescriptor3D,
        aabbA: IAabb3D,
        aabbB: IAabb3D
    ): { normal: IVec3Like; point: IVec3Like; penetration: number } | null {
        const overlapX = Math.min(aabbA.max.x, aabbB.max.x) - Math.max(aabbA.min.x, aabbB.min.x);
        const overlapY = Math.min(aabbA.max.y, aabbB.max.y) - Math.max(aabbA.min.y, aabbB.min.y);
        const overlapZ = Math.min(aabbA.max.z, aabbB.max.z) - Math.max(aabbA.min.z, aabbB.min.z);

        if (overlapX < 0 || overlapY < 0 || overlapZ < 0) {
            return null;
        }

        const centerA = this._host.getShapeWorldCenter(descriptorA);
        const centerB = this._host.getShapeWorldCenter(descriptorB);
        let normal: IVec3Like;
        let penetration = overlapX;

        if (overlapY < penetration) penetration = overlapY;
        if (overlapZ < penetration) penetration = overlapZ;

        if (penetration === overlapX) {
            normal = { x: centerB.x >= centerA.x ? 1 : -1, y: 0, z: 0 };
        } else if (penetration === overlapY) {
            normal = { x: 0, y: centerB.y >= centerA.y ? 1 : -1, z: 0 };
        } else {
            normal = { x: 0, y: 0, z: centerB.z >= centerA.z ? 1 : -1 };
        }

        return {
            normal,
            point: {
                x: (Math.max(aabbA.min.x, aabbB.min.x) + Math.min(aabbA.max.x, aabbB.max.x)) * 0.5,
                y: (Math.max(aabbA.min.y, aabbB.min.y) + Math.min(aabbA.max.y, aabbB.max.y)) * 0.5,
                z: (Math.max(aabbA.min.z, aabbB.min.z) + Math.min(aabbA.max.z, aabbB.max.z)) * 0.5,
            },
            penetration,
        };
    }

    private _solveContactVelocity(manifold: IResolvedContactManifold3D): void {
        for (const point of manifold.points) {
            const worldPointA = this._localPointToWorld(manifold.bodyIdA, point.localPointA);
            const worldPointB = this._localPointToWorld(manifold.bodyIdB, point.localPointB);
            const worldPoint = midpointVec3(worldPointA, worldPointB);
            const relativeVelocity = subVec3(
                this._getWorldPointVelocity(manifold.bodyIdB, worldPoint),
                this._getWorldPointVelocity(manifold.bodyIdA, worldPoint)
            );
            const normalSpeed = dotVec3(relativeVelocity, manifold.normal);
            if (normalSpeed >= 0) {
                continue;
            }

            const inverseMassSum =
                this._getBodySolveInverseMass(manifold.bodyIdA) +
                this._getBodySolveInverseMass(manifold.bodyIdB);
            if (inverseMassSum <= PhysicsConstants.EPSILON) {
                continue;
            }

            const restitution =
                normalSpeed < -PhysicsConstants.VELOCITY_THRESHOLD ? manifold.restitution : 0;
            const normalImpulseMagnitude = (-(1 + restitution) * normalSpeed) / inverseMassSum;
            if (normalImpulseMagnitude <= 0) {
                continue;
            }

            point.normalImpulse = ((point.normalImpulse + normalImpulseMagnitude) as unknown) as Impulse;
            const normalImpulse = scaleVec3(manifold.normal, normalImpulseMagnitude);
            this._applySolveImpulse(manifold.bodyIdA, negateVec3(normalImpulse), worldPoint);
            this._applySolveImpulse(manifold.bodyIdB, normalImpulse, worldPoint);

            const updatedRelativeVelocity = subVec3(
                this._getWorldPointVelocity(manifold.bodyIdB, worldPoint),
                this._getWorldPointVelocity(manifold.bodyIdA, worldPoint)
            );
            const tangentialVelocity = subVec3(
                updatedRelativeVelocity,
                scaleVec3(manifold.normal, dotVec3(updatedRelativeVelocity, manifold.normal))
            );
            const tangentLength = lengthVec3(tangentialVelocity);
            if (tangentLength <= PhysicsConstants.EPSILON) {
                continue;
            }

            const tangent = scaleVec3(tangentialVelocity, 1 / tangentLength);
            const tangentSpeed = dotVec3(updatedRelativeVelocity, tangent);
            const tangentImpulseMagnitude = clamp(
                -tangentSpeed / inverseMassSum,
                -manifold.friction * normalImpulseMagnitude,
                manifold.friction * normalImpulseMagnitude
            );
            const tangentImpulse = scaleVec3(tangent, tangentImpulseMagnitude);
            point.tangentImpulse1 = ((point.tangentImpulse1 + tangentImpulseMagnitude) as unknown) as Impulse;
            this._applySolveImpulse(manifold.bodyIdA, negateVec3(tangentImpulse), worldPoint);
            this._applySolveImpulse(manifold.bodyIdB, tangentImpulse, worldPoint);
        }
    }

    private _solveContactPosition(manifold: IResolvedContactManifold3D): void {
        for (const point of manifold.points) {
            point.separation = this._getCurrentPointSeparation(manifold, point);
            const penetration = Math.max(0, -point.separation);
            if (penetration <= PhysicsConstants.ALLOWED_PENETRATION) {
                continue;
            }

            const inverseMassA = this._getBodySolveInverseMass(manifold.bodyIdA);
            const inverseMassB = this._getBodySolveInverseMass(manifold.bodyIdB);
            const inverseMassSum = inverseMassA + inverseMassB;
            if (inverseMassSum <= PhysicsConstants.EPSILON) {
                continue;
            }

            const correctionMagnitude =
                ((penetration - PhysicsConstants.ALLOWED_PENETRATION) * 0.8) / inverseMassSum;
            const correction = scaleVec3(manifold.normal, correctionMagnitude);

            if (inverseMassA > 0) {
                const positionA = this._host.bodyManager.getPosition(manifold.bodyIdA);
                this._host.bodyManager.setPosition(
                    manifold.bodyIdA,
                    subVec3(positionA, scaleVec3(correction, inverseMassA))
                );
            }
            if (inverseMassB > 0) {
                const positionB = this._host.bodyManager.getPosition(manifold.bodyIdB);
                this._host.bodyManager.setPosition(
                    manifold.bodyIdB,
                    addVec3(positionB, scaleVec3(correction, inverseMassB))
                );
            }

            point.separation = this._getCurrentPointSeparation(manifold, point);
        }
    }

    private _solveManagedConstraintVelocity(
        descriptor: IConstraintDescriptor3D,
        deltaTime: number
    ): void {
        const anchors = this._getConstraintAnchorsAndError(descriptor);
        if (!anchors) {
            return;
        }

        const inverseMassA = this._getBodySolveInverseMass(descriptor.def.bodyIdA);
        const inverseMassB = this._getBodySolveInverseMass(descriptor.def.bodyIdB);
        const inverseMassSum = inverseMassA + inverseMassB;
        if (inverseMassSum <= PhysicsConstants.EPSILON) {
            return;
        }

        const relativeVelocity = subVec3(
            this._getWorldPointVelocity(descriptor.def.bodyIdB, anchors.anchorB),
            this._getWorldPointVelocity(descriptor.def.bodyIdA, anchors.anchorA)
        );
        const relativeSpeed = dotVec3(relativeVelocity, anchors.direction);

        let impulseMagnitude = 0;
        if (descriptor.type === CONSTRAINT_TYPE_SPRING) {
            const springDef = descriptor.def as Extract<SupportedConstraintDef3D, { kind: typeof CONSTRAINT_TYPE_SPRING }>;
            const stiffness = springDef.stiffness ?? 10;
            const damping = springDef.damping ?? 0.5;
            impulseMagnitude =
                (-(stiffness * anchors.error + damping * relativeSpeed) * deltaTime) / inverseMassSum;
        } else {
            const bias = (anchors.error * 0.05) / Math.max(deltaTime, PhysicsConstants.EPSILON);
            impulseMagnitude = clamp((-(relativeSpeed + bias)) / inverseMassSum, -2.5, 2.5);
        }

        if (Math.abs(impulseMagnitude) <= PhysicsConstants.EPSILON) {
            return;
        }

        const impulse = scaleVec3(anchors.direction, impulseMagnitude);
        this._applySolveImpulse(descriptor.def.bodyIdA, negateVec3(impulse), anchors.anchorA);
        this._applySolveImpulse(descriptor.def.bodyIdB, impulse, anchors.anchorB);
    }

    private _stabilizeRestingContactVelocity(manifold: IResolvedContactManifold3D): void {
        for (const point of manifold.points) {
            point.separation = this._getCurrentPointSeparation(manifold, point);
            if (point.separation > PhysicsConstants.ALLOWED_PENETRATION * 2) {
                continue;
            }

            const worldPoint = midpointVec3(
                this._localPointToWorld(manifold.bodyIdA, point.localPointA),
                this._localPointToWorld(manifold.bodyIdB, point.localPointB)
            );
            const relativeVelocity = subVec3(
                this._getWorldPointVelocity(manifold.bodyIdB, worldPoint),
                this._getWorldPointVelocity(manifold.bodyIdA, worldPoint)
            );
            const normalSpeed = dotVec3(relativeVelocity, manifold.normal);
            if (normalSpeed >= -PhysicsConstants.EPSILON) {
                continue;
            }

            const inverseMassSum =
                this._getBodySolveInverseMass(manifold.bodyIdA) +
                this._getBodySolveInverseMass(manifold.bodyIdB);
            if (inverseMassSum <= PhysicsConstants.EPSILON) {
                continue;
            }

            const impulse = scaleVec3(manifold.normal, -normalSpeed / inverseMassSum);
            this._applySolveImpulse(manifold.bodyIdA, negateVec3(impulse), worldPoint);
            this._applySolveImpulse(manifold.bodyIdB, impulse, worldPoint);
        }
    }

    private _solveManagedConstraintPosition(descriptor: IConstraintDescriptor3D): void {
        const anchors = this._getConstraintAnchorsAndError(descriptor);
        if (!anchors || Math.abs(anchors.error) <= PhysicsConstants.LINEAR_SLOP) {
            return;
        }

        const inverseMassA = this._getBodySolveInverseMass(descriptor.def.bodyIdA);
        const inverseMassB = this._getBodySolveInverseMass(descriptor.def.bodyIdB);
        const inverseMassSum = inverseMassA + inverseMassB;
        if (inverseMassSum <= PhysicsConstants.EPSILON) {
            return;
        }

        const correctionMagnitude = Math.min(anchors.error * 0.25, 0.2) / inverseMassSum;
        const correction = scaleVec3(anchors.direction, correctionMagnitude);
        if (inverseMassA > 0) {
            this._host.bodyManager.setPosition(
                descriptor.def.bodyIdA,
                addVec3(
                    this._host.bodyManager.getPosition(descriptor.def.bodyIdA),
                    scaleVec3(correction, inverseMassA)
                )
            );
        }
        if (inverseMassB > 0) {
            this._host.bodyManager.setPosition(
                descriptor.def.bodyIdB,
                subVec3(
                    this._host.bodyManager.getPosition(descriptor.def.bodyIdB),
                    scaleVec3(correction, inverseMassB)
                )
            );
        }
    }

    private _getConstraintAnchorsAndError(
        descriptor: IConstraintDescriptor3D
    ): { anchorA: IVec3Like; anchorB: IVec3Like; direction: IVec3Like; error: number } | null {
        const anchorA = this._host.getConstraintAnchor(descriptor.def, true);
        const anchorB = this._host.getConstraintAnchor(descriptor.def, false);
        const delta = subVec3(anchorB, anchorA);

        if (descriptor.type === CONSTRAINT_TYPE_SPRING) {
            const length = lengthVec3(delta);
            if (length <= PhysicsConstants.EPSILON) {
                return null;
            }
            const springDef = descriptor.def as Extract<SupportedConstraintDef3D, { kind: typeof CONSTRAINT_TYPE_SPRING }>;
            return {
                anchorA,
                anchorB,
                direction: scaleVec3(delta, 1 / length),
                error: length - (springDef.restLength ?? 1),
            };
        }

        if (descriptor.type === CONSTRAINT_TYPE_SLIDER) {
            const sliderDef = descriptor.def as Extract<SupportedConstraintDef3D, { kind: typeof CONSTRAINT_TYPE_SLIDER }>;
            const axis = normalizeVec3(
                rotateVec3(
                    sliderDef.localAxisA,
                    this._host.bodyManager.getRotation(sliderDef.bodyIdA)
                )
            );
            const perpendicular = subVec3(delta, scaleVec3(axis, dotVec3(delta, axis)));
            const error = lengthVec3(perpendicular);
            if (error <= PhysicsConstants.EPSILON) {
                return null;
            }
            return {
                anchorA,
                anchorB,
                direction: scaleVec3(perpendicular, 1 / error),
                error,
            };
        }

        const error = lengthVec3(delta);
        if (error <= PhysicsConstants.EPSILON) {
            return null;
        }

        return {
            anchorA,
            anchorB,
            direction: scaleVec3(delta, 1 / error),
            error,
        };
    }

    private _emitPreSolveEvents(
        previousManifolds: ReadonlyMap<string, IResolvedContactManifold3D>,
        nextManifolds: ReadonlyMap<string, IResolvedContactManifold3D>
    ): void {
        const contactListener = this._host.getContactListener();
        if (!contactListener || !('onPreSolve' in contactListener)) {
            return;
        }

        const onPreSolve = (contactListener as {
            onPreSolve?: (event: unknown, oldManifold: IContactManifold3D) => void;
        }).onPreSolve;
        if (!onPreSolve) {
            return;
        }

        for (const [pairKey, manifold] of nextManifolds) {
            if (manifold.sensor) {
                continue;
            }

            const previous = previousManifolds.get(pairKey);
            if (!previous) {
                continue;
            }

            onPreSolve(this._createCollisionPayload(manifold, 3), previous);
        }
    }

    private _emitContactLifecycleEvents(
        previousManifolds: ReadonlyMap<string, IResolvedContactManifold3D>,
        nextManifolds: ReadonlyMap<string, IResolvedContactManifold3D>
    ): void {
        const contactListener = this._host.getContactListener();
        if (!contactListener) {
            return;
        }

        const timestamp = Date.now();

        for (const [pairKey, manifold] of nextManifolds) {
            const previous = previousManifolds.get(pairKey);
            if (manifold.sensor) {
                if (previous) {
                    this._emitSensorEvent(contactListener, 'onSensorStay', manifold, timestamp, 1);
                } else {
                    this._emitSensorEvent(contactListener, 'onSensorEnter', manifold, timestamp, 0);
                }
                continue;
            }

            if (previous) {
                this._emitCollisionEvent(contactListener, 'onCollisionStay', manifold, timestamp, 1);
            } else {
                this._emitCollisionEvent(contactListener, 'onCollisionBegin', manifold, timestamp, 0);
            }

            const onPostSolve = (contactListener as {
                onPostSolve?: (
                    event: unknown,
                    impulse: { normal: Impulse; tangent1: Impulse; tangent2: Impulse }
                ) => void;
            }).onPostSolve;
            if (onPostSolve) {
                const normalImpulse = manifold.points.reduce((sum, point) => sum + point.normalImpulse, 0);
                const tangentImpulse1 = manifold.points.reduce((sum, point) => sum + point.tangentImpulse1, 0);
                const tangentImpulse2 = manifold.points.reduce((sum, point) => sum + point.tangentImpulse2, 0);
                onPostSolve(this._createCollisionPayload(manifold, 4, timestamp), {
                    normal: (normalImpulse as unknown) as Impulse,
                    tangent1: (tangentImpulse1 as unknown) as Impulse,
                    tangent2: (tangentImpulse2 as unknown) as Impulse,
                });
            }
        }

        for (const [pairKey, manifold] of previousManifolds) {
            if (nextManifolds.has(pairKey)) {
                continue;
            }

            if (manifold.sensor) {
                this._emitSensorEvent(contactListener, 'onSensorExit', manifold, timestamp, 2);
            } else {
                this._emitCollisionEnd(contactListener, manifold, timestamp);
            }
        }
    }

    private _emitCollisionEvent(
        contactListener: IContactListener3D,
        methodName: 'onCollisionBegin' | 'onCollisionStay',
        manifold: IResolvedContactManifold3D,
        timestamp: number,
        type: number
    ): void {
        const handler = (contactListener as Record<string, ((payload: unknown) => void) | undefined>)[methodName];
        if (!handler) {
            return;
        }

        handler(this._createCollisionPayload(manifold, type, timestamp));
    }

    private _emitCollisionEnd(
        contactListener: IContactListener3D,
        manifold: IResolvedContactManifold3D,
        timestamp: number
    ): void {
        const handler = (contactListener as {
            onCollisionEnd?: ((bodyIdA: BodyId3D, bodyIdB: BodyId3D) => void) | ((event: unknown) => void);
        }).onCollisionEnd;
        if (!handler) {
            return;
        }

        if (handler.length >= 2) {
            (handler as (bodyIdA: BodyId3D, bodyIdB: BodyId3D) => void)(manifold.bodyIdA, manifold.bodyIdB);
            return;
        }

        (handler as (event: unknown) => void)(this._createCollisionPayload(manifold, 2, timestamp));
    }

    private _emitSensorEvent(
        contactListener: IContactListener3D,
        methodName: 'onSensorEnter' | 'onSensorStay' | 'onSensorExit',
        manifold: IResolvedContactManifold3D,
        timestamp: number,
        type: number
    ): void {
        const handler = (contactListener as Record<string, ((...args: unknown[]) => void) | undefined>)[methodName];
        if (!handler) {
            return;
        }

        const sensorShape = manifold.descriptorA.isSensor ? manifold.descriptorA : manifold.descriptorB;
        const visitorShape = manifold.descriptorA.isSensor ? manifold.descriptorB : manifold.descriptorA;

        if (handler.length >= 2 && methodName !== 'onSensorStay') {
            handler(sensorShape.bodyId, visitorShape.bodyId);
            return;
        }

        handler({
            type,
            sensorBodyId: sensorShape.bodyId,
            sensorShapeId: sensorShape.id,
            visitorBodyId: visitorShape.bodyId,
            visitorShapeId: visitorShape.id,
            timestamp,
        });
    }

    private _createCollisionPayload(
        manifold: IResolvedContactManifold3D,
        type: number,
        timestamp: number = Date.now()
    ): unknown {
        return {
            ...manifold,
            type,
            timestamp,
            manifold,
        };
    }

    private _getBodySolveInverseMass(bodyId: BodyId3D): number {
        return this._host.bodyManager.getBodyType(bodyId) === BODY_TYPE_DYNAMIC &&
            this._host.bodyManager.isEnabled(bodyId)
            ? this._host.bodyManager.getInverseMass(bodyId)
            : 0;
    }

    private _applySolveImpulse(bodyId: BodyId3D, impulse: Readonly<IVec3Like>, point: Readonly<IVec3Like>): void {
        if (this._host.bodyManager.getBodyType(bodyId) !== BODY_TYPE_DYNAMIC) {
            return;
        }

        this._host.bodyManager.applyImpulse(bodyId, impulse, point);
    }

    private _getWorldPointVelocity(bodyId: BodyId3D, point: Readonly<IVec3Like>): IVec3Like {
        const center = this._host.bodyManager.getPosition(bodyId);
        return addVec3(
            this._host.bodyManager.getLinearVelocity(bodyId),
            crossVec3(this._host.bodyManager.getAngularVelocity(bodyId), subVec3(point, center))
        );
    }

    private _localPointToWorld(bodyId: BodyId3D, localPoint: Readonly<IVec3Like>): IVec3Like {
        return transformPoint3D(
            localPoint,
            this._host.bodyManager.getPosition(bodyId),
            this._host.bodyManager.getRotation(bodyId)
        );
    }

    private _getCurrentPointSeparation(
        manifold: IResolvedContactManifold3D,
        point: IMutableContactPoint3D
    ): number {
        const worldPointA = this._localPointToWorld(manifold.bodyIdA, point.localPointA);
        const worldPointB = this._localPointToWorld(manifold.bodyIdB, point.localPointB);
        return dotVec3(subVec3(worldPointB, worldPointA), manifold.normal);
    }
}