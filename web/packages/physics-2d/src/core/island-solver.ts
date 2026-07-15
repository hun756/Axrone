import { Vec2, clamp, type IVec2Like } from '@axrone/numeric';
import type { BodyId, ContactId, ConstraintId, SolverFlags } from '../types';
import type { BodyManager2D } from './body-manager';
import type { ContactManager2D } from './contact-manager';
import type { ConstraintManager2D } from './constraint-manager';
import { ConstraintSolver2D } from './constraint-solver';
import { PhysicsConstants } from '../types';

interface ProfilerData {
    solveVelocityTime: number;
    solvePositionTime: number;
}

const LINEAR_SLEEP_TOLERANCE_SQ = PhysicsConstants.LINEAR_SLEEP_TOLERANCE * PhysicsConstants.LINEAR_SLEEP_TOLERANCE;
const ANGULAR_SLEEP_TOLERANCE_SQ = PhysicsConstants.ANGULAR_SLEEP_TOLERANCE * PhysicsConstants.ANGULAR_SLEEP_TOLERANCE;
const MAX_TRANSLATION_SQ = PhysicsConstants.MAX_TRANSLATION * PhysicsConstants.MAX_TRANSLATION;
const MAX_ROTATION_SQ = PhysicsConstants.MAX_ROTATION * PhysicsConstants.MAX_ROTATION;

interface VelocityConstraintPoint {
    rA: IVec2Like;
    rB: IVec2Like;
    normalMass: number;
    tangentMass: number;
    velocityBias: number;
    normalImpulse: number;
    tangentImpulse: number;
}

interface VelocityConstraint {
    contactId: ContactId;
    indexA: number;
    indexB: number;
    invMassA: number;
    invMassB: number;
    invIA: number;
    invIB: number;
    friction: number;
    restitution: number;
    tangentSpeed: number;
    normal: Vec2;
    tangent: Vec2;
    pointCount: number;
    points: [VelocityConstraintPoint, VelocityConstraintPoint];
}

interface PositionConstraintPoint {
    localAnchorA: Vec2;
    localAnchorB: Vec2;
    separation: number;
}

interface PositionConstraint {
    contactId: ContactId;
    indexA: number;
    indexB: number;
    invMassA: number;
    invMassB: number;
    localCenterA: Vec2;
    localCenterB: Vec2;
    invIA: number;
    invIB: number;
    normal: Vec2;
    pointCount: number;
    points: [PositionConstraintPoint, PositionConstraintPoint];
}

const RESTITUTION_THRESHOLD = 1.0;
const POSITION_SLOP = 0.005;
const MAX_LINEAR_CORRECTION = 0.2;

export class IslandSolver2D {
    private readonly _bodyStack: BodyId[] = [];
    private readonly _contactStack: ContactId[] = [];
    private readonly _constraintStack: ConstraintId[] = [];
    private readonly _bodyIndex: Map<BodyId, number> = new Map();
    private readonly _visitedContacts: Set<ContactId> = new Set();
    private readonly _visitedConstraints: Set<ConstraintId> = new Set();
    private readonly _bodyManager: BodyManager2D;
    private readonly _contactManager: ContactManager2D;
    private readonly _constraintManager: ConstraintManager2D;
    private readonly _constraintSolver: ConstraintSolver2D;

    private readonly _velocities: Float64Array;
    private readonly _positions: Float64Array;
    private readonly _invMass: Float64Array;
    private readonly _invI: Float64Array;
    private readonly _localCenters: Float64Array;
    private readonly _velocityConstraints: VelocityConstraint[] = [];
    private readonly _positionConstraints: PositionConstraint[] = [];

    private readonly _tmpVelocityA = new Vec2();
    private readonly _tmpVelocityB = new Vec2();
    private readonly _tmpDelta = new Vec2();

    constructor(
        bodyManager: BodyManager2D,
        contactManager: ContactManager2D,
        constraintManager: ConstraintManager2D,
        maxBodiesPerIsland: number = 1024
    ) {
        this._bodyManager = bodyManager;
        this._contactManager = contactManager;
        this._constraintManager = constraintManager;
        this._constraintSolver = new ConstraintSolver2D(constraintManager, bodyManager);

        this._velocities = new Float64Array(maxBodiesPerIsland * 3);
        this._positions = new Float64Array(maxBodiesPerIsland * 3);
        this._invMass = new Float64Array(maxBodiesPerIsland);
        this._invI = new Float64Array(maxBodiesPerIsland);
        this._localCenters = new Float64Array(maxBodiesPerIsland * 2);
    }

    getLastPreparedConstraintCount(): number {
        return this._constraintSolver.getLastPreparedConstraintCount();
    }

    getLastSolvedConstraintCount(): number {
        return this._constraintSolver.getLastSolvedConstraintCount();
    }

    solveIslands(
        deltaTime: number,
        velocityIterations: number,
        positionIterations: number,
        allowSleep: boolean,
        flags: SolverFlags,
        profiler?: ProfilerData
    ): void {
        const bodies = this._bodyManager.getBodyIds();
        const visitedBodies = new Set<BodyId>();

        for (const seedBodyId of bodies) {
            if (visitedBodies.has(seedBodyId)) continue;

            const type = this._bodyManager.getBodyType(seedBodyId);
            const isAwake = this._bodyManager.isAwake(seedBodyId);

            if (type === 0 || (allowSleep && !isAwake)) {
                visitedBodies.add(seedBodyId);
                continue;
            }

            this._buildIsland(seedBodyId, visitedBodies, allowSleep);

            if (this._bodyStack.length > 0) {
                this._solveIsland(
                    deltaTime,
                    velocityIterations,
                    positionIterations,
                    flags,
                    profiler
                );
                this._bodyStack.length = 0;
                this._contactStack.length = 0;
                this._constraintStack.length = 0;
                this._bodyIndex.clear();
                this._visitedContacts.clear();
                this._visitedConstraints.clear();
            }
        }
    }

    private _buildIsland(seedBodyId: BodyId, visited: Set<BodyId>, allowSleep: boolean): void {
        const stack: BodyId[] = [seedBodyId];
        visited.add(seedBodyId);
        this._bodyStack.push(seedBodyId);

        while (stack.length > 0) {
            const bodyId = stack.pop()!;

            if (
                allowSleep &&
                !this._bodyManager.isAwake(bodyId) &&
                this._bodyManager.getBodyType(bodyId) === 2
            ) {
                this._bodyManager.setAwake(bodyId, true);
            }

            const contacts = this._contactManager.getContactsForBody(bodyId);
            for (const contactId of contacts) {
                if (this._visitedContacts.has(contactId)) continue;
                this._visitedContacts.add(contactId);
                this._contactStack.push(contactId);

                const bodies = this._contactManager.getContactBodies(contactId);
                if (!bodies) continue;
                const otherBodyId = bodies.bodyIdA === bodyId ? bodies.bodyIdB : bodies.bodyIdA;
                if (!visited.has(otherBodyId)) {
                    visited.add(otherBodyId);
                    this._bodyStack.push(otherBodyId);
                    stack.push(otherBodyId);
                }
            }

            const constraints = this._constraintManager.getConstraintsForBody(bodyId);
            for (const constraintId of constraints) {
                if (this._visitedConstraints.has(constraintId)) continue;
                this._visitedConstraints.add(constraintId);
                this._constraintStack.push(constraintId);

                const bodies = this._constraintManager.getConstraintBodies(constraintId);
                if (!bodies) continue;
                const otherBodyId = bodies.bodyIdA === bodyId ? bodies.bodyIdB : bodies.bodyIdA;
                if (!visited.has(otherBodyId)) {
                    visited.add(otherBodyId);
                    this._bodyStack.push(otherBodyId);
                    stack.push(otherBodyId);
                }
            }
        }
    }

    private _solveIsland(
        dt: number,
        velIters: number,
        posIters: number,
        flags: SolverFlags,
        gravity: { x: number; y: number },
        allowSleep: boolean,
        profiler?: ProfilerData
    ): void {
        const h = dt;
        const bodyCount = this._bodyStack.length;

        for (let i = 0; i < bodyCount; i++) {
            const bodyId = this._bodyStack[i];
            const type = this._bodyManager.getBodyType(bodyId);
            this._bodyIndex.set(bodyId, i);

            const offset = i * 3;
            const position = this._bodyManager.getPosition(bodyId);
            const rotation = this._bodyManager.getRotation(bodyId);

            this._positions[offset] = position.x;
            this._positions[offset + 1] = position.y;
            this._positions[offset + 2] = rotation;

            if (type !== 0) {
                const velocity = this._bodyManager.getLinearVelocity(bodyId);
                const angularVelocity = this._bodyManager.getAngularVelocity(bodyId);

                this._velocities[offset] = velocity.x;
                this._velocities[offset + 1] = velocity.y;
                this._velocities[offset + 2] = angularVelocity;

                this._invMass[i] = this._bodyManager.getInverseMass(bodyId);
                this._invI[i] = this._bodyManager.getInverseInertia(bodyId);

                const localCenter = this._bodyManager.getLocalCenter(bodyId);
                this._localCenters[i * 2] = localCenter.x;
                this._localCenters[i * 2 + 1] = localCenter.y;
            } else {
                this._velocities[offset] = 0;
                this._velocities[offset + 1] = 0;
                this._velocities[offset + 2] = 0;
                this._invMass[i] = 0;
                this._invI[i] = 0;
                this._localCenters[i * 2] = 0;
                this._localCenters[i * 2 + 1] = 0;
            }
        }

        // Gravity integration: apply gravity force to dynamic bodies
        for (let i = 0; i < bodyCount; i++) {
            const bodyId = this._bodyStack[i];
            const type = this._bodyManager.getBodyType(bodyId);

            if (type !== 0) {
                const offset = i * 3;
                const gravityScale = this._bodyManager.getGravityScale(bodyId);
                const invMass = this._invMass[i];

                // Apply gravity: v += (g * gravityScale) * h
                this._velocities[offset] += gravity.x * gravityScale * h;
                this._velocities[offset + 1] += gravity.y * gravityScale * h;

                // Apply damping
                const linearDamping = this._bodyManager.getLinearDamping(bodyId);
                const angularDamping = this._bodyManager.getAngularDamping(bodyId);
                const linearDampingFactor = Math.max(0, 1 - linearDamping * h);
                const angularDampingFactor = Math.max(0, 1 - angularDamping * h);

                this._velocities[offset] *= linearDampingFactor;
                this._velocities[offset + 1] *= linearDampingFactor;
                this._velocities[offset + 2] *= angularDampingFactor;

                // Clamp velocities to prevent instability
                const vx = this._velocities[offset];
                const vy = this._velocities[offset + 1];
                const w = this._velocities[offset + 2];
                const speedSq = vx * vx + vy * vy;
                if (speedSq > MAX_TRANSLATION_SQ) {
                    const scale = PhysicsConstants.MAX_TRANSLATION / Math.sqrt(speedSq);
                    this._velocities[offset] = vx * scale;
                    this._velocities[offset + 1] = vy * scale;
                }
                if (w * w > MAX_ROTATION_SQ) {
                    this._velocities[offset + 2] = w > 0 ? PhysicsConstants.MAX_ROTATION : -PhysicsConstants.MAX_ROTATION;
                }
            }
        }

        this._initializeVelocityConstraints();

        if ((flags & 1) !== 0) {
            this._warmStart();
        }

        const t0 = performance.now();
        for (let i = 0; i < velIters; i++) {
            this._solveVelocityConstraints();
        }
        if (profiler) profiler.solveVelocityTime += performance.now() - t0;

        for (let i = 0; i < bodyCount; i++) {
            const bodyId = this._bodyStack[i];
            const type = this._bodyManager.getBodyType(bodyId);

            if (type !== 0) {
                const offset = i * 3;
                const vx = this._velocities[offset];
                const vy = this._velocities[offset + 1];
                const w = this._velocities[offset + 2];

                this._positions[offset] += vx * h;
                this._positions[offset + 1] += vy * h;
                this._positions[offset + 2] += w * h;
            }
        }

        this._initializePositionConstraints();

        const t1 = performance.now();
        let positionSolved = false;
        for (let i = 0; i < posIters; i++) {
            const minSeparation = this._solvePositionConstraints();
            positionSolved = minSeparation >= -POSITION_SLOP;
            if (positionSolved) break;
        }
        if (profiler) profiler.solvePositionTime += performance.now() - t1;

        for (let i = 0; i < bodyCount; i++) {
            const bodyId = this._bodyStack[i];
            const type = this._bodyManager.getBodyType(bodyId);

            if (type !== 0) {
                const offset = i * 3;
                this._bodyManager.setPosition(bodyId, {
                    x: this._positions[offset],
                    y: this._positions[offset + 1],
                });
                this._bodyManager.setRotation(bodyId, this._positions[offset + 2]);
                this._bodyManager.setLinearVelocity(bodyId, {
                    x: this._velocities[offset],
                    y: this._velocities[offset + 1],
                });
                this._bodyManager.setAngularVelocity(bodyId, this._velocities[offset + 2]);
            }
        }

        this._constraintSolver.solveConstraints(
            Array.from(new Set(this._constraintStack)),
            dt,
            velIters,
            posIters
        );

        // Sleep system: update sleep timers and put resting bodies to sleep
        if (allowSleep) {
            const minSleepTime = this._updateSleepTimers(h);
            if (minSleepTime >= PhysicsConstants.SLEEP_TIME) {
                this._putIslandToSleep();
            }
        }

        this._storeImpulses();
        this._velocityConstraints.length = 0;
        this._positionConstraints.length = 0;
    }

    private _updateSleepTimers(h: number): number {
        let minSleepTime = Infinity;

        for (let i = 0; i < this._bodyStack.length; i++) {
            const bodyId = this._bodyStack[i];
            const type = this._bodyManager.getBodyType(bodyId);

            if (type !== 2) continue; // Only dynamic bodies sleep

            const offset = i * 3;
            const vx = this._velocities[offset];
            const vy = this._velocities[offset + 1];
            const w = this._velocities[offset + 2];

            const linVelSq = vx * vx + vy * vy;
            const angVelSq = w * w;

            if (linVelSq > LINEAR_SLEEP_TOLERANCE_SQ || angVelSq > ANGULAR_SLEEP_TOLERANCE_SQ) {
                this._bodyManager.setSleepTime(bodyId, 0);
                minSleepTime = 0;
            } else {
                const newSleepTime = this._bodyManager.getSleepTime(bodyId) + h;
                this._bodyManager.setSleepTime(bodyId, newSleepTime);
                if (newSleepTime < minSleepTime) {
                    minSleepTime = newSleepTime;
                }
            }
        }

        return minSleepTime;
    }

    private _putIslandToSleep(): void {
        for (let i = 0; i < this._bodyStack.length; i++) {
            const bodyId = this._bodyStack[i];
            const type = this._bodyManager.getBodyType(bodyId);

            if (type === 2) {
                this._bodyManager.setAwake(bodyId, false);
            }
        }
    }

    private _initializeVelocityConstraints(): void {
        for (const contactId of this._contactStack) {
            const contactData = this._contactManager.getContactData(contactId);
            if (!contactData || contactData.pointCount === 0) continue;

            const bodies = this._contactManager.getContactBodies(contactId);
            if (!bodies) continue;

            const indexA = this._bodyIndex.get(bodies.bodyIdA);
            const indexB = this._bodyIndex.get(bodies.bodyIdB);
            if (indexA === undefined || indexB === undefined) continue;

            const invMassA = this._invMass[indexA];
            const invMassB = this._invMass[indexB];
            const invIA = this._invI[indexA];
            const invIB = this._invI[indexB];
            const localCenterA = Vec2.fromArray(this._localCenters, indexA * 2);
            const localCenterB = Vec2.fromArray(this._localCenters, indexB * 2);
            const rotA = this._positions[indexA * 3 + 2];
            const rotB = this._positions[indexB * 3 + 2];

            const normal = new Vec2(contactData.normal.x, contactData.normal.y);
            const tangent = new Vec2(-normal.y, normal.x);

            const point0 = this._makeVelocityPoint(contactData.point0, localCenterA, localCenterB, rotA, rotB, normal, tangent, invMassA, invMassB, invIA, invIB);
            const point1 = this._makeVelocityPoint(
                contactData.point1 ?? { localA: new Vec2(), localB: new Vec2(), separation: 0 },
                localCenterA,
                localCenterB,
                rotA,
                rotB,
                normal,
                tangent,
                invMassA,
                invMassB,
                invIA,
                invIB
            );

            point0.velocityBias = this._computeRestitutionBias(indexA, indexB, point0, normal, contactData.restitution);
            point1.velocityBias = this._computeRestitutionBias(indexA, indexB, point1, normal, contactData.restitution);

            const points: [VelocityConstraintPoint, VelocityConstraintPoint] = [point0, point1];

            this._velocityConstraints.push({
                contactId,
                indexA,
                indexB,
                invMassA,
                invMassB,
                invIA,
                invIB,
                friction: contactData.friction,
                restitution: contactData.restitution,
                tangentSpeed: 0,
                normal,
                tangent,
                pointCount: contactData.pointCount,
                points,
            });
        }
    }

    private _makeVelocityPoint(
        local: { localA: IVec2Like; localB: IVec2Like; separation: number },
        localCenterA: Vec2,
        localCenterB: Vec2,
        rotA: number,
        rotB: number,
        normal: Vec2,
        tangent: Vec2,
        invMassA: number,
        invMassB: number,
        invIA: number,
        invIB: number
    ): VelocityConstraintPoint {
        const rA = Vec2.rotate(Vec2.subtract(local.localA, localCenterA), rotA);
        const rB = Vec2.rotate(Vec2.subtract(local.localB, localCenterB), rotB);

        const normalMass = 1 / (invMassA + invMassB + invIA * Vec2.cross(rA, normal) ** 2 + invIB * Vec2.cross(rB, normal) ** 2);
        const tangentMass = 1 / (invMassA + invMassB + invIA * Vec2.cross(rA, tangent) ** 2 + invIB * Vec2.cross(rB, tangent) ** 2);

        return {
            rA,
            rB,
            normalMass,
            tangentMass,
            velocityBias: 0,
            normalImpulse: 0,
            tangentImpulse: 0,
        };
    }

    private _computeRestitutionBias(
        indexA: number,
        indexB: number,
        point: VelocityConstraintPoint,
        normal: Vec2,
        restitution: number
    ): number {
        const dvB = this._getPointVelocity(indexB, point.rB, this._tmpVelocityA);
        const dvA = this._getPointVelocity(indexA, point.rA, this._tmpVelocityB);
        const vRelNormal = Vec2.dot(dvB, normal) - Vec2.dot(dvA, normal);
        if (vRelNormal < -RESTITUTION_THRESHOLD) {
            return -restitution * vRelNormal;
        }
        return 0;
    }

    private _warmStart(): void {
        for (const vc of this._velocityConstraints) {
            const warmData = this._contactManager.getWarmStartImpulse(vc.contactId, 0);
            vc.points[0].normalImpulse = warmData.normalImpulse;
            vc.points[0].tangentImpulse = warmData.tangentImpulse;

            if (vc.pointCount > 1) {
                const warmData2 = this._contactManager.getWarmStartImpulse(vc.contactId, 1);
                vc.points[1].normalImpulse = warmData2.normalImpulse;
                vc.points[1].tangentImpulse = warmData2.tangentImpulse;
            }

            for (let j = 0; j < vc.pointCount; j++) {
                const vcp = vc.points[j];
                const impulseX = vcp.normalImpulse * vc.normal.x + vcp.tangentImpulse * vc.tangent.x;
                const impulseY = vcp.normalImpulse * vc.normal.y + vcp.tangentImpulse * vc.tangent.y;
                this._applyImpulse(vc.indexA, vc.indexB, impulseX, impulseY, vcp.rA, vcp.rB);
            }
        }
    }

    private _solveVelocityConstraints(): void {
        for (const vc of this._velocityConstraints) {
            for (let j = 0; j < vc.pointCount; j++) {
                const vcp = vc.points[j];

                const dvB = this._getPointVelocity(vc.indexB, vcp.rB, this._tmpVelocityA);
                const dvA = this._getPointVelocity(vc.indexA, vcp.rA, this._tmpVelocityB);
                const vRelTangent = Vec2.dot(dvB, vc.tangent) - Vec2.dot(dvA, vc.tangent);

                let dTangent = -vcp.tangentMass * vRelTangent;
                const maxFriction = vc.friction * vcp.normalImpulse;
                const newTangentImpulse = clamp(vcp.tangentImpulse + dTangent, -maxFriction, maxFriction);
                dTangent = newTangentImpulse - vcp.tangentImpulse;
                vcp.tangentImpulse = newTangentImpulse;

                this._applyImpulse(vc.indexA, vc.indexB, dTangent * vc.tangent.x, dTangent * vc.tangent.y, vcp.rA, vcp.rB);

                const dvB2 = this._getPointVelocity(vc.indexB, vcp.rB, this._tmpVelocityA);
                const dvA2 = this._getPointVelocity(vc.indexA, vcp.rA, this._tmpVelocityB);
                const vRelNormal = Vec2.dot(dvB2, vc.normal) - Vec2.dot(dvA2, vc.normal);

                let dNormal = -vcp.normalMass * (vRelNormal + vcp.velocityBias);
                const newNormalImpulse = Math.max(vcp.normalImpulse + dNormal, 0);
                dNormal = newNormalImpulse - vcp.normalImpulse;
                vcp.normalImpulse = newNormalImpulse;

                this._applyImpulse(vc.indexA, vc.indexB, dNormal * vc.normal.x, dNormal * vc.normal.y, vcp.rA, vcp.rB);
            }
        }
    }

    private _getPointVelocity(index: number, r: IVec2Like, out: Vec2): Vec2 {
        const offset = index * 3;
        const vx = this._velocities[offset];
        const vy = this._velocities[offset + 1];
        const w = this._velocities[offset + 2];
        out.x = vx - w * r.y;
        out.y = vy + w * r.x;
        return out;
    }

    private _applyImpulse(
        indexA: number,
        indexB: number,
        px: number,
        py: number,
        rA: IVec2Like,
        rB: IVec2Like
    ): void {
        const invMassA = this._invMass[indexA];
        const invMassB = this._invMass[indexB];
        const invIA = this._invI[indexA];
        const invIB = this._invI[indexB];

        const offsetA = indexA * 3;
        this._velocities[offsetA] -= invMassA * px;
        this._velocities[offsetA + 1] -= invMassA * py;
        this._velocities[offsetA + 2] -= invIA * Vec2.cross(rA, { x: px, y: py });

        const offsetB = indexB * 3;
        this._velocities[offsetB] += invMassB * px;
        this._velocities[offsetB + 1] += invMassB * py;
        this._velocities[offsetB + 2] += invIB * Vec2.cross(rB, { x: px, y: py });
    }

    private _initializePositionConstraints(): void {
        for (const contactId of this._contactStack) {
            const contactData = this._contactManager.getContactData(contactId);
            if (!contactData || contactData.pointCount === 0) continue;

            const bodies = this._contactManager.getContactBodies(contactId);
            if (!bodies) continue;

            const indexA = this._bodyIndex.get(bodies.bodyIdA);
            const indexB = this._bodyIndex.get(bodies.bodyIdB);
            if (indexA === undefined || indexB === undefined) continue;

            this._positionConstraints.push({
                contactId,
                indexA,
                indexB,
                invMassA: this._invMass[indexA],
                invMassB: this._invMass[indexB],
                localCenterA: Vec2.fromArray(this._localCenters, indexA * 2),
                localCenterB: Vec2.fromArray(this._localCenters, indexB * 2),
                invIA: this._invI[indexA],
                invIB: this._invI[indexB],
                normal: new Vec2(contactData.normal.x, contactData.normal.y),
                pointCount: contactData.pointCount,
                points: [
                    {
                        localAnchorA: new Vec2(contactData.point0.localA.x, contactData.point0.localA.y),
                        localAnchorB: new Vec2(contactData.point0.localB.x, contactData.point0.localB.y),
                        separation: contactData.point0.separation,
                    },
                    {
                        localAnchorA: new Vec2(contactData.point1?.localA.x ?? 0, contactData.point1?.localA.y ?? 0),
                        localAnchorB: new Vec2(contactData.point1?.localB.x ?? 0, contactData.point1?.localB.y ?? 0),
                        separation: contactData.point1?.separation ?? 0,
                    },
                ],
            });
        }
    }

    private _solvePositionConstraints(): number {
        let minSeparation = 0;

        for (const pc of this._positionConstraints) {
            const centerA = Vec2.fromArray(this._positions, pc.indexA * 3);
            const centerB = Vec2.fromArray(this._positions, pc.indexB * 3);
            const rotA = this._positions[pc.indexA * 3 + 2];
            const rotB = this._positions[pc.indexB * 3 + 2];
            const normal = pc.normal;

            for (let j = 0; j < pc.pointCount; j++) {
                const pcp = pc.points[j];

                const rA = Vec2.rotate(Vec2.subtract(pcp.localAnchorA, pc.localCenterA), rotA);
                const pointA = Vec2.add(centerA, rA);
                const rB = Vec2.rotate(Vec2.subtract(pcp.localAnchorB, pc.localCenterB), rotB);
                const pointB = Vec2.add(centerB, rB);

                const separation = Vec2.dot(Vec2.subtract(pointB, pointA), normal);
                if (separation < minSeparation) {
                    minSeparation = separation;
                }

                const k = pc.invMassA + pc.invMassB +
                    pc.invIA * Vec2.cross(rA, normal) ** 2 +
                    pc.invIB * Vec2.cross(rB, normal) ** 2;
                const normalMass = k > 0 ? 1 / k : 0;

                let c = separation + POSITION_SLOP;
                if (c > 0) c = 0;

                const impulse = -normalMass * c;
                const px = impulse * normal.x;
                const py = impulse * normal.y;

                const offsetA = pc.indexA * 3;
                this._positions[offsetA] -= pc.invMassA * px;
                this._positions[offsetA + 1] -= pc.invMassA * py;
                this._positions[offsetA + 2] -= pc.invIA * Vec2.cross(rA, { x: px, y: py });

                const offsetB = pc.indexB * 3;
                this._positions[offsetB] += pc.invMassB * px;
                this._positions[offsetB + 1] += pc.invMassB * py;
                this._positions[offsetB + 2] += pc.invIB * Vec2.cross(rB, { x: px, y: py });
            }
        }

        return minSeparation;
    }

    private _storeImpulses(): void {
        for (const vc of this._velocityConstraints) {
            for (let j = 0; j < vc.pointCount; j++) {
                const vcp = vc.points[j];
                this._contactManager.setWarmStartImpulse(
                    vc.contactId,
                    j,
                    vcp.normalImpulse,
                    vcp.tangentImpulse
                );
            }
        }
    }
}
