// Part 1
import { Vec3, Quat, clamp, type IVec3Like } from '@axrone/numeric';
import type {
    ContactId,
    ICollisionEvent3D,
    ICollisionFilter,
    IContactManifold3D,
    Impulse,
    ISensorEvent3D,
    ManifoldId,
} from '../types';
import { CollisionEventType, SensorEventType } from '../types';
import { PhysicsConstants } from '../types';
import type {
    BodyId3D,
    ConstraintId3D,
    IContactListener3D,
    IPhysicsProfiler3D,
    ShapeId3D,
} from '../types/physics-3d';
import { BodyManager3D, ConstraintManager3D } from './physics-managers-3d';
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
    isHeightFieldDef,
    isConvexHullDef,
    isTriangleMeshDef,
} from './physics-world-3d-shared';
import { makeCollisionPairKey } from '@axrone/physics-core';
import { GJK3D, supportFromVertices, type Support3D } from './gjk3d';

// ─── Constraint Framework Imports ─────────────────────────────────────────────
// Side-effect imports: each module calls registerConstraintModule() at load time.
import './physics-world-3d-constraints-fixed';
import './physics-world-3d-constraints-hinge';
import {
    type JacobianRow3D,
    type SolverBody3D,
    prepareAllConstraints,
    solveAllVelocityConstraints,
    solveAllPositionConstraints,
    resetImpulses,
    commitSolverVelocities3D,
    commitSolverBodies3D,
    syncSolverBodiesFromManager,
    applyConstraintPositionCorrection,
} from './physics-world-3d-constraints-framework';

export interface IPhysicsWorld3DContactRuntimeHost {
    readonly bodyManager: BodyManager3D;
    readonly constraintManager: ConstraintManager3D;
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

/** Deterministic numeric pair key — delegates to canonical helper.
 *  `Number()` conversion is required because `ShapeId3D` values are BigInt
 *  at runtime (branded `number` type, but `_nextBodyId = 1n`). */
function _makePairKey(idA: ShapeId3D, idB: ShapeId3D): number {
    return makeCollisionPairKey(Number(idA), Number(idB));
}

export class PhysicsWorld3DContactRuntime {
    private _nextContactId = 1 as ContactId;
    private _nextManifoldId = 1;
    private _contactManifolds = new Map<number, IResolvedContactManifold3D>();
    private readonly _broadphase = new DynamicAABBTree3D<ShapeId3D>(1024);
    private readonly _shapeProxyMap = new Map<ShapeId3D, number>();
    private readonly _shapePreviousCenter = new Map<ShapeId3D, IVec3Like>();

    // ─── Constraint solver state (reused per step, allocated once) ───
    private _jacobianCache = new Map<ConstraintId3D, JacobianRow3D[]>();
    private _solverBodies = new Map<BodyId3D, SolverBody3D>();

    /**
     * Joint collision pair index for collideConnected filtering.
     * Only populated when constraints with collideConnected=false exist.
     * Early exit (size === 0) skips the filter entirely in the common case.
     */
    private readonly _jointCollisionCounts = new Map<number, number>();

    /** Warm-start impulse cache keyed by pairKey * 4 + pointIndex. */
    private readonly _warmImpulses = new Map<number, { normal: number; tangent: number }>();
    private _lastIslandCount = 0;

    /** Body→contact pairKey index for kinematic wake queries. (P1-4) */
    private readonly _bodyContactIndex = new Map<number, number[]>();

    // Persistent buffers to eliminate per-step allocations
    private readonly _candidatePairs: IShapePairCandidate3D[] = [];
    private readonly _scratchAabb = { min: { x: 0, y: 0, z: 0 }, max: { x: 0, y: 0, z: 0 } };
    private readonly _scratchCenter: IVec3Like = { x: 0, y: 0, z: 0 };
    private readonly _scratchDisp: IVec3Like = { x: 0, y: 0, z: 0 };

    /**
     * Reusable transient event objects for Stay events (fired every step per contact).
     * WARNING: These are MUTATED each step. Listeners MUST NOT retain references
     * beyond the callback — copy any data needed. This avoids O(contacts) allocations
     * per step, preserving the zero-allocation discipline from Phase 1.
     */
    private _stayCollisionEvent: ICollisionEvent3D | null = null;
    private _staySensorEvent: ISensorEvent3D | null = null;

    // Pre-allocated contact point pool (max 4 points per manifold)
    private static readonly MAX_CONTACT_POINTS = 4;

    constructor(private readonly _host: IPhysicsWorld3DContactRuntimeHost) {}

    get contactCount(): number {
        return this._contactManifolds.size;
    }

    get islandCount(): number {
        return this._lastIslandCount;
    }

    /** Expose the broadphase tree for world-level queries (P1-13). */
    get broadphase(): DynamicAABBTree3D<ShapeId3D> {
        return this._broadphase;
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

    /**
     * Remove all solver state associated with a body. Called on destroy to prevent
     * stale warm impulses, manifold history, and contact index entries from leaking.
     */
    removeBodyState(bodyId: BodyId3D): void {
        const bid = Number(bodyId);
        // Remove warm impulse entries whose pair involves this body
        for (const [key, m] of this._contactManifolds) {
            if (Number(m.bodyIdA) === bid || Number(m.bodyIdB) === bid) {
                // Remove warm impulses for this manifold's pairKey
                for (let pi = 0; pi < m.pointCount; pi++) {
                    this._warmImpulses.delete(m.pairKey * 4 + pi);
                }
            }
        }
        // Also scan remaining warm impulse keys — pairKey encodes two shape IDs,
        // but we can't easily reverse that. Instead, clear entries for manifolds
        // that reference this body in _previousManifolds.
        for (const [key, m] of this._previousManifolds) {
            if (Number(m.bodyIdA) === bid || Number(m.bodyIdB) === bid) {
                this._previousManifolds.delete(key);
            }
        }
        // Remove body contact index entries
        this._bodyContactIndex.delete(bid);
        // Remove joint-collision entries whose pair involves this body
        if (this._jointCollisionCounts.size > 0) {
            for (const [key, m] of this._contactManifolds) {
                if (Number(m.bodyIdA) === bid || Number(m.bodyIdB) === bid) {
                    const pairBodyKey = makeCollisionPairKey(Number(m.bodyIdA), Number(m.bodyIdB));
                    this._jointCollisionCounts.delete(pairBodyKey);
                }
            }
        }
    }

    /**
     * Rebuild body→contact pairKey index from current manifolds. (P1-4)
     * O(C) where C = contact count. Called lazily when kinematic bodies move.
     */
    rebuildBodyContactIndex(): void {
        this._bodyContactIndex.clear();
        for (const [pairKey, m] of this._contactManifolds) {
            let arr = this._bodyContactIndex.get(Number(m.bodyIdA));
            if (!arr) { arr = []; this._bodyContactIndex.set(Number(m.bodyIdA), arr); }
            arr.push(pairKey);
            arr = this._bodyContactIndex.get(Number(m.bodyIdB));
            if (!arr) { arr = []; this._bodyContactIndex.set(Number(m.bodyIdB), arr); }
            arr.push(pairKey);
        }
    }

    /** Get contact pairKeys for a body from the pre-built index. (P1-4) */
    getContactPairKeysForBody(bodyId: BodyId3D): number[] | undefined {
        return this._bodyContactIndex.get(Number(bodyId));
    }

    /** Look up manifold bodyIds by pairKey. (P1-4) */
    getManifoldBodyIds(pairKey: number): { bodyIdA: BodyId3D; bodyIdB: BodyId3D } | null {
        const m = this._contactManifolds.get(pairKey);
        return m ? { bodyIdA: m.bodyIdA, bodyIdB: m.bodyIdB } : null;
    }

    /**
     * Register a joint collision pair — increments the counter for the body pair.
     * Called when a constraint with `collideConnected=false` is created.
     */
    registerJointCollisionPair(bodyIdA: BodyId3D, bodyIdB: BodyId3D): void {
        const key = makeCollisionPairKey(Number(bodyIdA), Number(bodyIdB));
        this._jointCollisionCounts.set(key, (this._jointCollisionCounts.get(key) ?? 0) + 1);
    }

    /**
     * Unregister a joint collision pair — decrements the counter, removes entry at zero.
     * Called when a constraint with `collideConnected=false` is destroyed.
     */
    unregisterJointCollisionPair(bodyIdA: BodyId3D, bodyIdB: BodyId3D): void {
        const key = makeCollisionPairKey(Number(bodyIdA), Number(bodyIdB));
        const count = this._jointCollisionCounts.get(key);
        if (count === undefined) return;
        if (count <= 1) {
            this._jointCollisionCounts.delete(key);
        } else {
            this._jointCollisionCounts.set(key, count - 1);
        }
    }

    /**
     * Legacy combined solve — kept for backward compat.
     * Internally delegates to the split pipeline.
     */
    solve(deltaTime: number, velIters: number, posIters: number): void {
        this.collectManifolds();
        this.warmStart();
        this.solveVelocity(velIters);
        this.solvePosition(posIters);
        this._persistWarmImpulses();
        this.dispatchEvents();
    }

    /** Phase 1: broadphase + narrowphase → build manifolds. */
    collectManifolds(): void {
        const bStart = performance.now();
        const pairs = this._collectPotentialCollisionPairs();
        const bTime = performance.now() - bStart;
        const nStart = performance.now();

        const next = this._contactManifolds;
        next.clear();

        for (const pair of pairs) {
            const m = this._buildContactManifold(pair);
            if (!m) continue;
            // Apply warm impulses from previous frame, scaled for stability.
            // Full warm starting can overshoot when the solver's accumulated impulse
            // was from an impact frame (large) and the current frame is resting.
            // A factor of 0.5 provides convergence benefit without instability.
            const WARM_SCALE = 0.5;
            for (let pi = 0; pi < m.pointCount; pi++) {
                const warmKey = pair.pairKey * 4 + pi;
                const warm = this._warmImpulses.get(warmKey);
                if (warm) {
                    m.points[pi].normalImpulse = (warm.normal * WARM_SCALE) as Impulse;
                    m.points[pi].tangentImpulse1 = (warm.tangent * WARM_SCALE) as Impulse;
                }
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

        this._lastIslandCount = this._buildIslandCount(next);
    }

    /** Phase 2: apply cached impulses to velocities. */
    warmStart(): void {
        for (const m of this._contactManifolds.values()) this._warmStartContact(m);
    }

    /** Phase 3: sequential impulse velocity solve. */
    solveVelocity(velIters: number, dt?: number): void {
        const vStart = performance.now();
        const manifolds = this._contactManifolds;

        // ─── Prepare Jacobian constraints (once per step, outside iteration loop) ───
        const stepDt = dt ?? (1 / 60);
        this._solverBodies.clear();
        this._jacobianCache = prepareAllConstraints(
            this._host.constraintManager.getAllConstraintIds(),
            this._host.constraintManager,
            this._host.constraintDescriptors,
            this._host.bodyManager,
            this._solverBodies,
            stepDt,
        );

        for (let i = 0; i < velIters; i++) {
            for (const m of manifolds.values()) this._solveContactVelocity(m);
            this._solveDistanceConstraints(velIters);
            // Sync solver body velocities from body manager (contact solve modifies it directly)
            syncSolverBodiesFromManager(this._solverBodies, this._host.bodyManager);
            // Jacobian-based constraint velocity solve (Fixed, Hinge, etc.)
            solveAllVelocityConstraints(this._jacobianCache, this._solverBodies);
            // Commit constraint corrections back to body manager
            commitSolverVelocities3D(this._solverBodies, this._host.bodyManager);
        }

        // Spring forces are applied ONCE per step (not per velocity iteration)
        // to prevent over-accumulation. Box2D convention: soft constraint bias
        // is computed in prepare phase; only the pre-biased impulse is iterated.
        this._solveSpringForces(dt);
        const profiler = this._host.getProfiler();
        if (profiler) profiler.solveVelocityTime = performance.now() - vStart;
    }

    /** Phase 4: Baumgarte positional correction. */
    solvePosition(posIters: number): void {
        const pStart = performance.now();
        const manifolds = this._contactManifolds;
        for (let i = 0; i < posIters; i++) {
            for (const m of manifolds.values()) this._correctContactPositions(m, 0.2);
        }
        for (const m of manifolds.values()) this._correctContactPositions(m, 1.0);

        // ─── Jacobian constraint position correction ───
        // Apply anchor-alignment position corrections for constraints (Fixed, Hinge, etc.)
        // This runs AFTER contact position corrections and AFTER position integration.
        if (this._jacobianCache.size > 0) {
            applyConstraintPositionCorrection(
                this._jacobianCache,
                this._host.bodyManager,
                this._host.constraintManager,
                1 / 60, // dt for bias computation
            );
        }

        const profiler = this._host.getProfiler();
        if (profiler) profiler.solvePositionTime = performance.now() - pStart;
    }

    /** Persist accumulated impulses for next-frame warm starting.
     *  Reuses existing cache entries in-place to avoid per-step object allocation. */
    private _persistWarmImpulses(): void {
        this._warmImpulses.clear();
        for (const m of this._contactManifolds.values()) {
            for (let pi = 0; pi < m.pointCount; pi++) {
                const p = m.points[pi];
                this._warmImpulses.set(m.pairKey * 4 + pi, {
                    normal: p.normalImpulse as number,
                    tangent: p.tangentImpulse1 as number,
                });
            }
        }
    }

    /**
     * Public accessor for warm-impulse persistence — called by the step() pipeline
     * after solvePosition(). Delegates to the private _persistWarmImpulses().
     */
    persistWarmImpulses(): void {
        this._persistWarmImpulses();
    }

    /** Expose warm impulse cache size for testing. */
    get warmImpulseCacheSize(): number {
        return this._warmImpulses.size;
    }

    /** Fire contact events by comparing previous vs current manifolds. */
    dispatchEvents(): void {
        const listener = this._host.getContactListener();
        if (!listener) return;
        const next = this._contactManifolds;
        const prev = this._previousManifolds;
        const now = performance.now();
        // Begin events: new contacts
        for (const [key, m] of next.entries()) {
            if (!prev.has(key)) {
                if (m.sensor) {
                    const event: ISensorEvent3D = {
                        type: SensorEventType.Enter,
                        sensorBodyId: m.bodyIdA,
                        sensorShapeId: m.shapeIdA,
                        visitorBodyId: m.bodyIdB,
                        visitorShapeId: m.shapeIdB,
                        timestamp: now,
                    };
                    listener.onSensorEnter?.(event);
                } else {
                    const event: ICollisionEvent3D = {
                        type: CollisionEventType.Begin,
                        bodyIdA: m.bodyIdA,
                        bodyIdB: m.bodyIdB,
                        shapeIdA: m.shapeIdA,
                        shapeIdB: m.shapeIdB,
                        manifold: this._toContactManifold(m),
                        timestamp: now,
                    };
                    listener.onCollisionBegin?.(event);
                }
            }
        }
        // Stay events: continuing contacts
        for (const [key, m] of next.entries()) {
            if (prev.has(key)) {
                if (m.sensor) {
                    if (!this._staySensorEvent) {
                        this._staySensorEvent = {
                            type: SensorEventType.Stay,
                            sensorBodyId: m.bodyIdA,
                            sensorShapeId: m.shapeIdA,
                            visitorBodyId: m.bodyIdB,
                            visitorShapeId: m.shapeIdB,
                            timestamp: now,
                        };
                    } else {
                        this._staySensorEvent.sensorBodyId = m.bodyIdA;
                        this._staySensorEvent.sensorShapeId = m.shapeIdA;
                        this._staySensorEvent.visitorBodyId = m.bodyIdB;
                        this._staySensorEvent.visitorShapeId = m.shapeIdB;
                        this._staySensorEvent.timestamp = now;
                    }
                    listener.onSensorStay?.(this._staySensorEvent);
                } else {
                    const manifold = this._toContactManifold(m);
                    if (!this._stayCollisionEvent) {
                        this._stayCollisionEvent = {
                            type: CollisionEventType.Stay,
                            bodyIdA: m.bodyIdA,
                            bodyIdB: m.bodyIdB,
                            shapeIdA: m.shapeIdA,
                            shapeIdB: m.shapeIdB,
                            manifold,
                            timestamp: now,
                        };
                    } else {
                        this._stayCollisionEvent.bodyIdA = m.bodyIdA;
                        this._stayCollisionEvent.bodyIdB = m.bodyIdB;
                        this._stayCollisionEvent.shapeIdA = m.shapeIdA;
                        this._stayCollisionEvent.shapeIdB = m.shapeIdB;
                        (this._stayCollisionEvent as { manifold: IContactManifold3D }).manifold = manifold;
                        this._stayCollisionEvent.timestamp = now;
                    }
                    listener.onCollisionStay?.(this._stayCollisionEvent);
                }
            }
        }
        // End events: removed contacts
        for (const [key, m] of prev.entries()) {
            if (!next.has(key)) {
                if (m.sensor) {
                    const event: ISensorEvent3D = {
                        type: SensorEventType.Exit,
                        sensorBodyId: m.bodyIdA,
                        sensorShapeId: m.shapeIdA,
                        visitorBodyId: m.bodyIdB,
                        visitorShapeId: m.shapeIdB,
                        timestamp: now,
                    };
                    listener.onSensorExit?.(event);
                } else {
                    const event: ICollisionEvent3D = {
                        type: CollisionEventType.End,
                        bodyIdA: m.bodyIdA,
                        bodyIdB: m.bodyIdB,
                        shapeIdA: m.shapeIdA,
                        shapeIdB: m.shapeIdB,
                        manifold: this._toContactManifold(m),
                        timestamp: now,
                    };
                    listener.onCollisionEnd?.(event);
                }
            }
        }
        // Snapshot current manifolds for next frame's event diff
        this._previousManifolds = new Map(next);
    }

    /** Previous-frame manifold snapshot for event dispatch diff. */
    private _previousManifolds = new Map<number, IResolvedContactManifold3D>();

    private _collectPotentialCollisionPairs(): IShapePairCandidate3D[] {
        // Reuse persistent array — clear but don't reallocate
        this._candidatePairs.length = 0;

        const filter = this._host.getCollisionFilter();
        for (const d of this._host.shapeDescriptors.values()) {
            if (!this._host.bodyManager.isEnabled(d.bodyId)) continue;

            const rawAabb = this._host.computeShapeAabb(d);
            // Use scratch AABB instead of new AABB3D
            this._scratchAabb.min.x = rawAabb.min.x;
            this._scratchAabb.min.y = rawAabb.min.y;
            this._scratchAabb.min.z = rawAabb.min.z;
            this._scratchAabb.max.x = rawAabb.max.x;
            this._scratchAabb.max.y = rawAabb.max.y;
            this._scratchAabb.max.z = rawAabb.max.z;

            // Use scratch center instead of new object
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
                this._broadphase.moveProxy(existing, this._scratchAabb, this._scratchDisp);
            } else {
                const pid = this._broadphase.createProxy(this._scratchAabb, d.id);
                this._shapeProxyMap.set(d.id, pid);
            }
            // Reuse existing center entry to avoid per-step allocation
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

            // collideConnected filter: skip pairs joined by a non-colliding constraint
            // Early exit: when no joints exist, skip entirely.
            if (this._jointCollisionCounts.size > 0) {
                const bodyPairKey = makeCollisionPairKey(Number(dA.bodyId), Number(dB.bodyId));
                if (this._jointCollisionCounts.has(bodyPairKey)) return true;
            }

            // Reuse persistent array — push object literal (unavoidable but pooled via array reuse)
            this._candidatePairs.push({
                descriptorA: dA,
                descriptorB: dB,
                aabbA: this._broadphase.getAABB(pA),
                aabbB: this._broadphase.getAABB(pB),
                pairKey: _makePairKey(dA.id, dB.id),
            });
            return true;
        });

        return this._candidatePairs;
    }

    private _buildContactManifold(pair: IShapePairCandidate3D): IResolvedContactManifold3D | null {
        const dA = pair.descriptorA, dB = pair.descriptorB;
        const c = this._detectCollision(dA, dB, pair.aabbA, pair.aabbB);
        if (!c) return null;
        const { tangent1, tangent2 } = buildOrthonormalBasis(c.normal);
        const friction = Math.sqrt(dA.material.friction * dB.material.friction);
        const restitution = Math.max(dA.material.restitution, dB.material.restitution);

        // Box-box: try clip-based multi-point manifold
        if (isBoxDef(dA.def) && isBoxDef(dB.def)) {
            const clipPoints = this._buildBoxBoxManifold(dA, dB, c.normal, c.penetration);
            if (clipPoints.length >= 2) {
                const points: IMutableContactPoint3D[] = [];
                const bm = this._host.bodyManager;
                const posA = bm.getPosition(dA.bodyId);
                const rotA = bm.getRotation(dA.bodyId);
                const posB = bm.getPosition(dB.bodyId);
                const rotB = bm.getRotation(dB.bodyId);
                for (const cp of clipPoints) {
                    const lA = inverseTransformPoint3D(cp.worldPoint, posA, rotA);
                    const lB = inverseTransformPoint3D(cp.worldPoint, posB, rotB);
                    points.push({
                        id: (this._nextContactId++ as unknown) as ContactId,
                        localPointA: lA, localPointB: lB,
                        normalImpulse: 0 as Impulse, tangentImpulse1: 0 as Impulse, tangentImpulse2: 0 as Impulse,
                        separation: cp.separation,
                    });
                }
                return {
                    id: (this._nextManifoldId++ as ManifoldId),
                    pairKey: pair.pairKey, descriptorA: dA, descriptorB: dB,
                    bodyIdA: dA.bodyId, bodyIdB: dB.bodyId,
                    shapeIdA: dA.id, shapeIdB: dB.id,
                    normal: c.normal, tangent1, tangent2, pointCount: points.length,
                    points, sensor: dA.isSensor || dB.isSensor,
                    friction, restitution,
                };
            }
        }

        // Fallback: single-point manifold (sphere-sphere, capsule, etc.)
        const wA = this._getContactPointOnShape(dA, c, true);
        const wB = this._getContactPointOnShape(dB, c, false);
        const lA = inverseTransformPoint3D(wA, this._host.bodyManager.getPosition(dA.bodyId), this._host.bodyManager.getRotation(dA.bodyId));
        const lB = inverseTransformPoint3D(wB, this._host.bodyManager.getPosition(dB.bodyId), this._host.bodyManager.getRotation(dB.bodyId));
        return {
            id: (this._nextManifoldId++ as ManifoldId),
            pairKey: pair.pairKey, descriptorA: dA, descriptorB: dB,
            bodyIdA: dA.bodyId, bodyIdB: dB.bodyId,
            shapeIdA: dA.id, shapeIdB: dB.id,
            normal: c.normal, tangent1, tangent2, pointCount: 1,
            points: [{
                id: (this._nextContactId++ as unknown) as ContactId,
                localPointA: lA, localPointB: lB,
                normalImpulse: 0 as Impulse, tangentImpulse1: 0 as Impulse, tangentImpulse2: 0 as Impulse,
                separation: Vec3.dot(Vec3.subtract(wB, wA), c.normal),
            }],
            sensor: dA.isSensor || dB.isSensor,
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
        // Convex hull / triangle mesh / cylinder / cone: real GJK/EPA narrowphase.
        if (
            tA === SHAPE_TYPE_CONVEX_HULL || tA === SHAPE_TYPE_TRIANGLE_MESH ||
            tB === SHAPE_TYPE_CONVEX_HULL || tB === SHAPE_TYPE_TRIANGLE_MESH ||
            tA === SHAPE_TYPE_CYLINDER || tA === SHAPE_TYPE_CONE ||
            tB === SHAPE_TYPE_CYLINDER || tB === SHAPE_TYPE_CONE
        ) {
            return this._cConvex(dA, dB, aabbA, aabbB);
        }
        // Heightfield: use analytic sphere/capsule-vs-heightfield when possible, else GJK.
        if (tA === SHAPE_TYPE_HEIGHTFIELD || tB === SHAPE_TYPE_HEIGHTFIELD) {
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

        if (isHeightFieldDef(def)) {
            const { heights, width, depth, scaleX = 1, scaleY = 1, scaleZ = 1 } = def;
            const center = def.center ?? { x: 0, y: 0, z: 0 };
            const c = transformPoint3D(center, pos, rot);
            const verts: IVec2Like[] = [];
            const halfW = (width - 1) * 0.5;
            const halfD = (depth - 1) * 0.5;
            // Sample a subset of heightfield vertices for GJK (full grid too expensive)
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

    /**
     * Clip-based box-box manifold: generates up to 4 contact points.
     * Uses the incident/reference face approach from Box2D.
     */
    private _buildBoxBoxManifold(
        dA: IShapeDescriptor3D, dB: IShapeDescriptor3D,
        normal: IVec3Like, totalPen: number
    ): { worldPoint: IVec3Like; separation: number }[] {
        const bm = this._host.bodyManager;
        const bDA = dA.def as { center: IVec3Like; halfExtents: IVec3Like; rotation?: IVec3Like };
        const bDB = dB.def as { center: IVec3Like; halfExtents: IVec3Like; rotation?: IVec3Like };

        // World-space transforms for both boxes
        const posA = bm.getPosition(dA.bodyId), rotA = bm.getRotation(dA.bodyId);
        const posB = bm.getPosition(dB.bodyId), rotB = bm.getRotation(dB.bodyId);
        const cA = transformPoint3D(bDA.center, posA, rotA);
        const cB = transformPoint3D(bDB.center, posB, rotB);
        const rA = Quat.multiply(rotA, bDA.rotation ?? IDENTITY_ROTATION);
        const rB = Quat.multiply(rotB, bDB.rotation ?? IDENTITY_ROTATION);

        // Local axes
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

        // Determine incident/reference face based on normal alignment
        const negNormal = Vec3.negate(normal);
        let bestDotA = -Infinity, bestA = 0;
        let bestDotB = -Infinity, bestB = 0;
        for (let i = 0; i < 3; i++) {
            const dA2 = Math.abs(Vec3.dot(axesA[i], normal));
            if (dA2 > bestDotA) { bestDotA = dA2; bestA = i; }
            const dB2 = Math.abs(Vec3.dot(axesB[i], negNormal));
            if (dB2 > bestDotB) { bestDotB = dB2; bestB = i; }
        }

        // Choose reference (most aligned with normal) and incident face
        let refCenter: IVec3Like, refAxes: IVec3Like[], refHalf: number[], refSign: number;
        let incCenter: IVec3Like, incAxes: IVec3Like[], incHalf: number[], incSign: number;
        let isARef: boolean;

        if (bestDotA >= bestDotB) {
            // Box A's face is the reference
            isARef = true;
            refCenter = cA; refAxes = axesA; refHalf = [bDA.halfExtents.x, bDA.halfExtents.y, bDA.halfExtents.z];
            refSign = Vec3.dot(normal, axesA[bestA]) > 0 ? -1 : 1;
            incCenter = cB; incAxes = axesB; incHalf = [bDB.halfExtents.x, bDB.halfExtents.y, bDB.halfExtents.z];
            incSign = Vec3.dot(negNormal, axesB[bestB]) > 0 ? -1 : 1;
        } else {
            // Box B's face is the reference
            isARef = false;
            refCenter = cB; refAxes = axesB; refHalf = [bDB.halfExtents.x, bDB.halfExtents.y, bDB.halfExtents.z];
            refSign = Vec3.dot(negNormal, axesB[bestB]) > 0 ? -1 : 1;
            incCenter = cA; incAxes = axesA; incHalf = [bDA.halfExtents.x, bDA.halfExtents.y, bDA.halfExtents.z];
            incSign = Vec3.dot(normal, axesA[bestA]) > 0 ? -1 : 1;
        }

        // Get incident face vertices (4 corners of the incident face)
        const incNormal = incAxes[bestB === bestDotB ? bestB : bestB]; // incident face axis
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

        // Reference face normal (pointing OUTWARD from reference body, away from interior)
        const refNormal = refAxes[bestDotA >= bestDotB ? bestA : bestB];
        const refFaceDist = refSign * refHalf[bestDotA >= bestDotB ? bestA : bestB];
        const refNormalActual = isARef
            ? (Vec3.dot(normal, refAxes[bestA]) > 0 ? refAxes[bestA] : Vec3.negate(refAxes[bestA]))
            : (Vec3.dot(negNormal, refAxes[bestB]) > 0 ? refAxes[bestB] : Vec3.negate(refAxes[bestB]));

        // Reference face tangent axes and half-extents
        const refFaceIdx = bestDotA >= bestDotB ? bestA : bestB;
        const refT1Idx = (refFaceIdx + 1) % 3;
        const refT2Idx = (refFaceIdx + 2) % 3;
        const refT1 = refAxes[refT1Idx];
        const refT2 = refAxes[refT2Idx];
        const refH1 = refHalf[refT1Idx];
        const refH2 = refHalf[refT2Idx];

        // Clip incident vertices against reference face side planes (Sutherland-Hodgman)
        let clipped = incVerts;
        clipped = this._clipSegmentToLine(clipped, refT1, refCenter, refH1);
        if (clipped.length < 2) return [];
        clipped = this._clipSegmentToLine(clipped, Vec3.negate(refT1), refCenter, refH1);
        if (clipped.length < 2) return [];
        clipped = this._clipSegmentToLine(clipped, refT2, refCenter, refH2);
        if (clipped.length < 2) return [];
        clipped = this._clipSegmentToLine(clipped, Vec3.negate(refT2), refCenter, refH2);
        if (clipped.length < 2) return [];

        // Keep only points behind the reference face, up to 4
        // After fix: refNormalActual points OUTWARD, so sep = dot(pt-center, outwardNormal)
        // is positive when pt is in front of the face, negative when behind.
        const result: { worldPoint: IVec3Like; separation: number }[] = [];
        const refFaceCenter = Vec3.add(refCenter, Vec3.multiplyScalar(refNormalActual, refFaceDist));
        for (const pt of clipped) {
            if (result.length >= 4) break;
            const sep = Vec3.dot(Vec3.subtract(pt, refFaceCenter), refNormalActual);
            // Keep points that are behind or near the reference face
            if (sep >= -(totalPen * 0.1 + PhysicsConstants.ALLOWED_PENETRATION)) {
                result.push({ worldPoint: pt, separation: sep - totalPen });
            }
        }
        return result;
    }

    /** Sutherland-Hodgman clip: keep vertices on the negative side of the plane. */
    private _clipSegmentToLine(
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
        // Sensors do not generate collision response — only overlap detection.
        if (manifold.sensor) return;

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
            } else {
                // Bodies separating — reset accumulated normal impulse to prevent
                // stale warm-start impulses from pushing bodies together.
                point.normalImpulse = 0 as unknown as Impulse;
                point.tangentImpulse1 = 0 as unknown as Impulse;
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
        for (let i = 0; i < manifold.pointCount; i++) {
            const point = manifold.points[i];
            const normal = (point.normalImpulse as number) ?? 0;
            if (normal === 0) continue;

            const wp = midpointVec3(
                this._lp2w(manifold.bodyIdA, point.localPointA),
                this._lp2w(manifold.bodyIdB, point.localPointB)
            );

            const normalImpulse = Vec3.multiplyScalar(manifold.normal, normal);
            this._applyImp(manifold.bodyIdA, Vec3.negate(normalImpulse), wp);
            this._applyImp(manifold.bodyIdB, normalImpulse, wp);
        }
    }

    private _correctContactPositions(manifold: IResolvedContactManifold3D, beta: number): void {
        // Sensors do not generate collision response — only overlap detection.
        if (manifold.sensor) return;

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
     * @deprecated Legacy anchor position correction. Now handled by the Jacobian
     * constraint framework (physics-world-3d-constraints-framework.ts).
     * Types 0 (Fixed) and 2 (Hinge) are solved by the new framework.
     * Kept as a no-op for backward compatibility; will be removed when all
     * constraint types migrate to the Jacobian framework.
     */
    private _solveDistanceConstraints(_iterations: number): void {
        // All rigid constraint types (0=Fixed, 2=Hinge) are now handled by the
        // Jacobian constraint framework in solveVelocity()/solvePosition().
        // Spring (6) has its own _solveSpringForces() path.
        // Other types (3=Slider, 4=ConeTwist, 5=Generic) are unsupported.
        return;
    }

    /**
     * Apply spring constraint forces ONCE per step (not per velocity iteration).
     * F = -k*x - c*v, converted to impulse via * dt.
     */
    private _solveSpringForces(dt?: number): void {
        const cm = this._host.constraintManager;
        const bm = this._host.bodyManager;
        const constraintIds = cm.getAllConstraintIds();
        if (constraintIds.length === 0) return;

        const BIAS = 0.2;
        // When dt is not provided (legacy solve() path), default to 1/60 for backward compat.
        const stepDt = dt ?? (1 / 60);

        for (const cid of constraintIds) {
            const type = cm.getConstraintType(cid);
            if (type !== 6) continue; // 6 = SPRING

            const { bodyIdA, bodyIdB } = cm.getConstraintBodyIds(cid);
            const typeA = bm.getBodyType(bodyIdA);
            const typeB = bm.getBodyType(bodyIdB);
            if (typeA !== 2 && typeB !== 2) continue;

            const posA = bm.getPosition(bodyIdA);
            const posB = bm.getPosition(bodyIdB);
            const rotA = bm.getRotation(bodyIdA);
            const rotB = bm.getRotation(bodyIdB);

            const localAnchorA = cm.getConstraintLocalAnchorA(cid);
            const localAnchorB = cm.getConstraintLocalAnchorB(cid);

            const worldAnchorA = transformPoint3D(localAnchorA, posA, rotA);
            const worldAnchorB = transformPoint3D(localAnchorB, posB, rotB);

            const delta = Vec3.subtract(worldAnchorB, worldAnchorA);
            const currentDist = Vec3.len(delta);

            const targetDist = cm.getConstraintParam(cid, 0); // restLength
            const error = currentDist - targetDist;
            if (Math.abs(error) < PhysicsConstants.EPSILON) continue;

            const dir = currentDist > PhysicsConstants.EPSILON
                ? Vec3.normalize(delta)
                : { x: 0, y: 1, z: 0 };

            // Baumgarte positional correction (applied once)
            const invMassA = typeA === 2 ? bm.getInverseMass(bodyIdA) : 0;
            const invMassB = typeB === 2 ? bm.getInverseMass(bodyIdB) : 0;
            const invMassSum = invMassA + invMassB;
            if (invMassSum > PhysicsConstants.EPSILON) {
                const correction = Vec3.multiplyScalar(dir, error * BIAS / invMassSum);
                if (typeA === 2) {
                    bm.setPosition(bodyIdA, Vec3.add(posA, Vec3.multiplyScalar(correction, invMassA)));
                }
                if (typeB === 2) {
                    bm.setPosition(bodyIdB, Vec3.subtract(posB, Vec3.multiplyScalar(correction, invMassB)));
                }
            }

            // Spring force: F = -k*x - c*v (both terms are forces in Newtons).
            // Convert to impulse by multiplying by dt: impulse = F * dt.
            const stiffness = cm.getConstraintParam(cid, 1);
            const damping = cm.getConstraintParam(cid, 2);
            const relVel = Vec3.subtract(
                bm.getLinearVelocity(bodyIdB),
                bm.getLinearVelocity(bodyIdA)
            );
            const velAlongDir = Vec3.dot(relVel, dir);
            const springForce = -stiffness * error - damping * velAlongDir;
            const impulse = Vec3.multiplyScalar(dir, springForce * stepDt);

            if (typeA === 2) {
                bm.applyImpulse(bodyIdA, Vec3.negate(impulse));
            }
            if (typeB === 2) {
                bm.applyImpulse(bodyIdB, impulse);
            }
        }
    }

    private _toContactManifold(m: IResolvedContactManifold3D): IContactManifold3D {
        return {
            bodyIdA: m.bodyIdA,
            bodyIdB: m.bodyIdB,
            shapeIdA: m.shapeIdA,
            shapeIdB: m.shapeIdB,
            normal: m.normal,
            points: m.points.map((p) => ({
                localPointA: p.localPointA,
                localPointB: p.localPointB,
                separation: p.separation,
                normalImpulse: p.normalImpulse,
                tangentImpulse1: p.tangentImpulse1,
                tangentImpulse2: p.tangentImpulse2,
            })),
        };
    }

    /**
     * Counts contact islands: connected components of bodies linked by active
     * contact manifolds. Enables island-level reporting and (combined with warm
     * starting) stable, ordered sequential-impulse solving.
     */
    private _buildIslandCount(manifolds: ReadonlyMap<number, IResolvedContactManifold3D>): number {
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
