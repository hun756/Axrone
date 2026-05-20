import { Vec2 } from '@axrone/numeric';
import type { IVec2Like } from '@axrone/numeric';
import type {
    IPhysicsWorld2D,
    IPhysicsWorldConfig,
    IPhysicsWorldStatistics,
    IPhysicsProfiler,
    BodyId,
    ShapeId,
    ConstraintId,
    IPhysicsBodyDef2D,
    ICircleShapeDef,
    IBoxShapeDef2D,
    IPolygonShapeDef,
    ISegmentShapeDef,
    ICapsuleShapeDef2D,
    IDistanceConstraintDef2D,
    IRevoluteConstraintDef2D,
    IPrismaticConstraintDef2D,
    IWeldConstraintDef2D,
    IWheelConstraintDef2D,
    IMotorConstraintDef2D,
    IMouseConstraintDef2D,
    IGearConstraintDef,
    IRopeConstraintDef2D,
    IContactListener2D,
    ICollisionFilter,
    RaycastCallback2D,
    IRaycastResult2D,
    IQueryFilter,
    IAABBQueryCallback,
    IPhysicsBody2D,
    IShape2D,
    IConstraint2D,
} from '../types';
import { ConstraintType, SolverFlags } from '../types';

import { BodyManager2D } from './body-manager';
import { ShapeManager2D } from './shape-manager';
import { ConstraintManager2D } from './constraint-manager';
import { ContactManager2D } from './contact-manager';
import { IslandSolver2D } from './island-solver';
import { createPhysicsBody2DView } from './physics-world-2d-body-view';
import { PhysicsWorld2DConstraintStore } from './physics-world-2d-constraint-store';
import { PhysicsWorld2DShapeStore } from './physics-world-2d-shape-store';

export class PhysicsWorld2D implements IPhysicsWorld2D {
    readonly config: Readonly<IPhysicsWorldConfig>;
    private readonly _gravity: Vec2;

    private readonly _bodyManager: BodyManager2D;
    private readonly _shapeManager: ShapeManager2D;
    private readonly _constraintManager: ConstraintManager2D;
    private readonly _contactManager: ContactManager2D;
    private readonly _solver: IslandSolver2D;
    private readonly _bodyViews = new Map<BodyId, IPhysicsBody2D>();
    private readonly _shapeStore: PhysicsWorld2DShapeStore;
    private readonly _constraintStore: PhysicsWorld2DConstraintStore;

    private _autoClearForces = true;
    private _profiler: IPhysicsProfiler | null = null;
    private _disposed = false;
    private _stepTime = 0;

    constructor(config: IPhysicsWorldConfig = {}) {
        this.config = config;
        this._gravity = config.gravity
            ? Vec2.from(config.gravity as IVec2Like)
            : new Vec2(0, -9.81);

        const maxBodies = config.maxBodies ?? config.bodyCapacity ?? 1024;
        const maxShapes = config.maxShapes ?? config.shapeCapacity ?? 2048;
        const maxConstraints = config.maxConstraints ?? config.constraintCapacity ?? 1024;
        const maxContacts = config.maxContacts ?? config.contactCapacity ?? 4096;

        this._bodyManager = new BodyManager2D(maxBodies);
        this._shapeManager = new ShapeManager2D(maxShapes);
        this._constraintManager = new ConstraintManager2D(maxConstraints);
        this._contactManager = new ContactManager2D(maxContacts);
        this._shapeStore = new PhysicsWorld2DShapeStore(this._bodyManager, this._shapeManager);
        this._constraintStore = new PhysicsWorld2DConstraintStore(
            this._bodyManager,
            this._constraintManager
        );

        this._solver = new IslandSolver2D(
            this._bodyManager,
            this._contactManager,
            this._constraintManager
        );

        if (config.enableProfiler) {
            this._profiler = {
                stepTime: 0,
                collisionTime: 0,
                solveTime: 0,
                broadphaseTime: 0,
                narrowphaseTime: 0,
                solveInitTime: 0,
                solveVelocityTime: 0,
                solvePositionTime: 0,
                sleepTime: 0,
            };
        }
    }

    get gravity(): Readonly<IVec2Like> {
        return this._gravity;
    }

    getBodyManager(): BodyManager2D {
        return this._bodyManager;
    }

    getShapeManager(): ShapeManager2D {
        return this._shapeManager;
    }

    getConstraintManager(): ConstraintManager2D {
        return this._constraintManager;
    }

    getContactManager(): ContactManager2D {
        return this._contactManager;
    }

    getSolver(): IslandSolver2D {
        return this._solver;
    }

    step(deltaTime: number, velocityIterations: number = 8, positionIterations: number = 3): void {
        if (this._disposed) {
            return;
        }

        const t0 = performance.now();
        const solverFlags = this.config.solverFlags ?? SolverFlags.Default;
        const allowSleep = this.config.allowSleep ?? true;

        this._solver.solveIslands(
            deltaTime,
            velocityIterations,
            positionIterations,
            allowSleep,
            solverFlags,
            this._profiler
        );

        if (this._autoClearForces) {
            this.clearForces();
        }

        this._stepTime = performance.now() - t0;
        if (this._profiler) {
            this._profiler.stepTime = this._stepTime;
        }
    }

    createBody(def: IPhysicsBodyDef2D): BodyId {
        return this._bodyManager.createBody(def);
    }

    destroyBody(bodyId: BodyId): void {
        const shapes = this._shapeManager.getShapesForBody(bodyId);
        for (const shapeId of shapes) {
            this._shapeManager.destroyShape(shapeId);
            this._shapeStore.removeShape(shapeId);
        }

        const constraints = this._constraintStore.getConstraintsForBody(bodyId);
        for (const constraintId of constraints) {
            this.destroyConstraint(constraintId);
        }

        const contacts = this._contactManager.getContactsForBody(bodyId);
        for (const contactId of Array.from(contacts)) {
            this._contactManager.destroyContact(contactId);
        }

        this._bodyViews.delete(bodyId);
        this._bodyManager.destroyBody(bodyId);
    }

    getBody(bodyId: BodyId): IPhysicsBody2D | null {
        if (!this._bodyManager.hasBody(bodyId)) {
            return null;
        }

        let view = this._bodyViews.get(bodyId);
        if (!view) {
            view = createPhysicsBody2DView(bodyId, {
                bodyManager: this._bodyManager,
                shapeManager: this._shapeManager,
                getBodyWorldCenter: (currentBodyId) => this._shapeStore.getBodyWorldCenter(currentBodyId),
                resetBodyMassData: (currentBodyId) => this._shapeStore.resetBodyMassData(currentBodyId),
            });
            this._bodyViews.set(bodyId, view);
        }

        return view;
    }

    getBodies(): ReadonlyMap<BodyId, IPhysicsBody2D> {
        const bodies = new Map<BodyId, IPhysicsBody2D>();
        for (const bodyId of this._bodyManager.getBodyIds()) {
            const body = this.getBody(bodyId);
            if (body) {
                bodies.set(bodyId, body);
            }
        }
        return bodies;
    }

    createCircleShape(bodyId: BodyId, def: ICircleShapeDef): ShapeId {
        const shapeId = this._shapeManager.createCircle(bodyId, def);
        this._shapeStore.registerCircle(shapeId, bodyId, def);
        this._shapeStore.resetBodyMassData(bodyId);
        return shapeId;
    }

    createBoxShape(bodyId: BodyId, def: IBoxShapeDef2D): ShapeId {
        const shapeId = this._shapeManager.createBox(bodyId, def);
        this._shapeStore.registerBox(shapeId, bodyId, def);
        this._shapeStore.resetBodyMassData(bodyId);
        return shapeId;
    }

    createPolygonShape(bodyId: BodyId, def: IPolygonShapeDef): ShapeId {
        const shapeId = this._shapeManager.createPolygon(bodyId, def);
        this._shapeStore.registerPolygon(shapeId, bodyId, def);
        this._shapeStore.resetBodyMassData(bodyId);
        return shapeId;
    }

    createCapsuleShape(bodyId: BodyId, def: ICapsuleShapeDef2D): ShapeId {
        const shapeId = this._shapeManager.createCapsule(bodyId, def);
        this._shapeStore.registerCapsule(shapeId, bodyId, def);
        this._shapeStore.resetBodyMassData(bodyId);
        return shapeId;
    }

    createSegmentShape(bodyId: BodyId, def: ISegmentShapeDef): ShapeId {
        const shapeId = this._shapeManager.createSegment(bodyId, def);
        this._shapeStore.registerSegment(shapeId, bodyId, def);
        this._shapeStore.resetBodyMassData(bodyId);
        return shapeId;
    }

    destroyShape(shapeId: ShapeId): void {
        const descriptor = this._shapeStore.getDescriptor(shapeId);
        this._shapeManager.destroyShape(shapeId);
        this._shapeStore.removeShape(shapeId);

        if (descriptor && this._bodyManager.hasBody(descriptor.bodyId)) {
            this._shapeStore.resetBodyMassData(descriptor.bodyId);
        }
    }

    getShape(shapeId: ShapeId): IShape2D | null {
        return this._shapeStore.getShapeView(shapeId);
    }

    createDistanceConstraint(def: IDistanceConstraintDef2D): ConstraintId {
        const constraintId = this._constraintManager.createDistanceConstraint(def);
        this._constraintStore.registerManagedConstraint(constraintId, {
            type: ConstraintType.Distance,
            bodyIdA: def.bodyIdA,
            bodyIdB: def.bodyIdB,
            collideConnected: def.collideConnected ?? false,
            enabled: true,
            userData: def.userData,
            localAnchorA: cloneVec(def.localAnchorA),
            localAnchorB: cloneVec(def.localAnchorB),
            localAxisA: null,
            linearOffset: null,
            target: null,
            constraintIdA: null,
            constraintIdB: null,
            referenceAngle: null,
            angularOffset: null,
            length: def.length ?? null,
            minLength: def.minLength ?? null,
            maxLength: def.maxLength ?? null,
            stiffness: def.stiffness ?? null,
            damping: def.damping ?? null,
            lowerTranslation: null,
            upperTranslation: null,
            motorSpeed: null,
            maxMotorTorque: null,
            maxMotorForce: null,
            maxForce: null,
            maxTorque: null,
            correctionFactor: null,
            ratio: null,
        });
        return constraintId;
    }

    createRevoluteConstraint(def: IRevoluteConstraintDef2D): ConstraintId {
        const constraintId = this._constraintManager.createRevoluteConstraint(def);
        this._constraintStore.registerManagedConstraint(constraintId, {
            type: ConstraintType.Revolute,
            bodyIdA: def.bodyIdA,
            bodyIdB: def.bodyIdB,
            collideConnected: def.collideConnected ?? false,
            enabled: true,
            userData: def.userData,
            localAnchorA: cloneVec(def.localAnchorA),
            localAnchorB: cloneVec(def.localAnchorB),
            localAxisA: null,
            linearOffset: null,
            target: null,
            constraintIdA: null,
            constraintIdB: null,
            referenceAngle: def.referenceAngle ?? 0,
            angularOffset: null,
            length: null,
            minLength: null,
            maxLength: null,
            stiffness: null,
            damping: null,
            lowerTranslation: null,
            upperTranslation: null,
            motorSpeed: def.motorSpeed ?? null,
            maxMotorTorque: def.maxMotorTorque ?? null,
            maxMotorForce: null,
            maxForce: null,
            maxTorque: null,
            correctionFactor: null,
            ratio: null,
        });
        return constraintId;
    }

    createPrismaticConstraint(def: IPrismaticConstraintDef2D): ConstraintId {
        const constraintId = this._constraintManager.createPrismaticConstraint(def);
        this._constraintStore.registerManagedConstraint(constraintId, {
            type: ConstraintType.Prismatic,
            bodyIdA: def.bodyIdA,
            bodyIdB: def.bodyIdB,
            collideConnected: def.collideConnected ?? false,
            enabled: true,
            userData: def.userData,
            localAnchorA: cloneVec(def.localAnchorA),
            localAnchorB: cloneVec(def.localAnchorB),
            localAxisA: cloneVec(def.localAxisA),
            linearOffset: null,
            target: null,
            constraintIdA: null,
            constraintIdB: null,
            referenceAngle: def.referenceAngle ?? 0,
            angularOffset: null,
            length: null,
            minLength: null,
            maxLength: null,
            stiffness: null,
            damping: null,
            lowerTranslation: def.lowerTranslation ?? null,
            upperTranslation: def.upperTranslation ?? null,
            motorSpeed: def.motorSpeed ?? null,
            maxMotorTorque: null,
            maxMotorForce: def.maxMotorForce ?? null,
            maxForce: null,
            maxTorque: null,
            correctionFactor: null,
            ratio: null,
        });
        return constraintId;
    }

    createWeldConstraint(def: IWeldConstraintDef2D): ConstraintId {
        const constraintId = this._constraintManager.createWeldConstraint(def);
        this._constraintStore.registerManagedConstraint(constraintId, {
            type: ConstraintType.Weld,
            bodyIdA: def.bodyIdA,
            bodyIdB: def.bodyIdB,
            collideConnected: def.collideConnected ?? false,
            enabled: true,
            userData: def.userData,
            localAnchorA: cloneVec(def.localAnchorA),
            localAnchorB: cloneVec(def.localAnchorB),
            localAxisA: null,
            linearOffset: null,
            target: null,
            constraintIdA: null,
            constraintIdB: null,
            referenceAngle: def.referenceAngle ?? 0,
            angularOffset: null,
            length: null,
            minLength: null,
            maxLength: null,
            stiffness: def.stiffness ?? null,
            damping: def.damping ?? null,
            lowerTranslation: null,
            upperTranslation: null,
            motorSpeed: null,
            maxMotorTorque: null,
            maxMotorForce: null,
            maxForce: null,
            maxTorque: null,
            correctionFactor: null,
            ratio: null,
        });
        return constraintId;
    }

    createWheelConstraint(def: IWheelConstraintDef2D): ConstraintId {
        const constraintId = this._constraintManager.createWheelConstraint(def);
        this._constraintStore.registerManagedConstraint(constraintId, {
            type: ConstraintType.Wheel,
            bodyIdA: def.bodyIdA,
            bodyIdB: def.bodyIdB,
            collideConnected: def.collideConnected ?? false,
            enabled: true,
            userData: def.userData,
            localAnchorA: cloneVec(def.localAnchorA),
            localAnchorB: cloneVec(def.localAnchorB),
            localAxisA: cloneVec(def.localAxisA),
            linearOffset: null,
            target: null,
            constraintIdA: null,
            constraintIdB: null,
            referenceAngle: null,
            angularOffset: null,
            length: null,
            minLength: null,
            maxLength: null,
            stiffness: def.stiffness ?? null,
            damping: def.damping ?? null,
            lowerTranslation: def.lowerTranslation ?? null,
            upperTranslation: def.upperTranslation ?? null,
            motorSpeed: def.motorSpeed ?? null,
            maxMotorTorque: def.maxMotorTorque ?? null,
            maxMotorForce: null,
            maxForce: null,
            maxTorque: null,
            correctionFactor: null,
            ratio: null,
        });
        return constraintId;
    }

    createMotorConstraint(def: IMotorConstraintDef2D): ConstraintId {
        const constraintId = this._constraintManager.createMotorConstraint(def);
        this._constraintStore.registerManagedConstraint(constraintId, {
            type: ConstraintType.Motor,
            bodyIdA: def.bodyIdA,
            bodyIdB: def.bodyIdB,
            collideConnected: def.collideConnected ?? false,
            enabled: true,
            userData: def.userData,
            localAnchorA: null,
            localAnchorB: null,
            localAxisA: null,
            linearOffset: cloneVec(def.linearOffset),
            target: null,
            constraintIdA: null,
            constraintIdB: null,
            referenceAngle: null,
            angularOffset: def.angularOffset ?? 0,
            length: null,
            minLength: null,
            maxLength: null,
            stiffness: null,
            damping: null,
            lowerTranslation: null,
            upperTranslation: null,
            motorSpeed: null,
            maxMotorTorque: null,
            maxMotorForce: null,
            maxForce: def.maxForce ?? null,
            maxTorque: def.maxTorque ?? null,
            correctionFactor: def.correctionFactor ?? null,
            ratio: null,
        });
        return constraintId;
    }

    createMouseConstraint(def: IMouseConstraintDef2D): ConstraintId {
        const constraintId = this._constraintManager.createMouseConstraint(def);
        this._constraintStore.registerManagedConstraint(constraintId, {
            type: ConstraintType.Mouse,
            bodyIdA: def.bodyIdA,
            bodyIdB: def.bodyIdB,
            collideConnected: def.collideConnected ?? false,
            enabled: true,
            userData: def.userData,
            localAnchorA: null,
            localAnchorB: null,
            localAxisA: null,
            linearOffset: null,
            target: cloneVec(def.target),
            constraintIdA: null,
            constraintIdB: null,
            referenceAngle: null,
            angularOffset: null,
            length: null,
            minLength: null,
            maxLength: null,
            stiffness: def.stiffness ?? null,
            damping: def.damping ?? null,
            lowerTranslation: null,
            upperTranslation: null,
            motorSpeed: null,
            maxMotorTorque: null,
            maxMotorForce: null,
            maxForce: def.maxForce ?? null,
            maxTorque: null,
            correctionFactor: null,
            ratio: null,
        });
        return constraintId;
    }

    createGearConstraint(def: IGearConstraintDef): ConstraintId {
        const constraintId = this._constraintManager.createGearConstraint(def);
        this._constraintStore.registerManagedConstraint(constraintId, {
            type: ConstraintType.Gear,
            bodyIdA: def.bodyIdA,
            bodyIdB: def.bodyIdB,
            collideConnected: def.collideConnected ?? false,
            enabled: true,
            userData: def.userData,
            localAnchorA: null,
            localAnchorB: null,
            localAxisA: null,
            linearOffset: null,
            target: null,
            constraintIdA: def.constraintIdA,
            constraintIdB: def.constraintIdB,
            referenceAngle: null,
            angularOffset: null,
            length: null,
            minLength: null,
            maxLength: null,
            stiffness: null,
            damping: null,
            lowerTranslation: null,
            upperTranslation: null,
            motorSpeed: null,
            maxMotorTorque: null,
            maxMotorForce: null,
            maxForce: null,
            maxTorque: null,
            correctionFactor: null,
            ratio: def.ratio ?? 1,
        });
        return constraintId;
    }

    createRopeConstraint(def: IRopeConstraintDef2D): ConstraintId {
        const constraintId = this._constraintManager.createRopeConstraint(def);
        this._constraintStore.registerManagedConstraint(constraintId, {
            type: ConstraintType.Rope,
            bodyIdA: def.bodyIdA,
            bodyIdB: def.bodyIdB,
            collideConnected: def.collideConnected ?? false,
            enabled: true,
            userData: def.userData,
            localAnchorA: cloneVec(def.localAnchorA),
            localAnchorB: cloneVec(def.localAnchorB),
            localAxisA: null,
            linearOffset: null,
            target: null,
            constraintIdA: null,
            constraintIdB: null,
            referenceAngle: null,
            angularOffset: null,
            length: null,
            minLength: null,
            maxLength: def.maxLength,
            stiffness: null,
            damping: null,
            lowerTranslation: null,
            upperTranslation: null,
            motorSpeed: null,
            maxMotorTorque: null,
            maxMotorForce: null,
            maxForce: null,
            maxTorque: null,
            correctionFactor: null,
            ratio: null,
        });
        return constraintId;
    }

    destroyConstraint(constraintId: ConstraintId): void {
        const descriptor = this._constraintStore.getConstraintDescriptor(constraintId);
        if (!descriptor) {
            if (this._constraintManager.hasConstraint(constraintId)) {
                this._constraintManager.destroyConstraint(constraintId);
            }
            return;
        }

        if (descriptor.storage === 'manager' && this._constraintManager.hasConstraint(constraintId)) {
            this._constraintManager.destroyConstraint(constraintId);
        }

        this._constraintStore.removeConstraint(constraintId);
    }

    getConstraint(constraintId: ConstraintId): IConstraint2D | null {
        return this._constraintStore.getConstraintView(constraintId);
    }

    setGravity(gravity: Readonly<IVec2Like>): void {
        this._gravity.x = gravity.x;
        this._gravity.y = gravity.y;
    }

    getGravity(): Readonly<IVec2Like> {
        return this._gravity;
    }

    setContactListener(listener: IContactListener2D | null): void {
        this._contactManager.setContactListener(listener);
    }

    setCollisionFilter(filter: ICollisionFilter | null): void {
        this._contactManager.setCollisionFilter(filter);
    }

    rayCast(
        origin: Readonly<IVec2Like>,
        direction: Readonly<IVec2Like>,
        maxFraction: number,
        callback: RaycastCallback2D
    ): void {
        let clippedFraction = maxFraction;

        for (const hit of this._shapeStore.rayCastAll(origin, direction, maxFraction)) {
            if (hit.fraction > clippedFraction) {
                continue;
            }

            const callbackResult = callback(hit.shapeId, hit.point, hit.normal, hit.fraction);
            if (callbackResult === 0) {
                break;
            }

            if (Number.isFinite(callbackResult) && callbackResult > 0) {
                clippedFraction = Math.min(clippedFraction, callbackResult);
            }
        }
    }

    rayCastClosest(
        origin: Readonly<IVec2Like>,
        direction: Readonly<IVec2Like>,
        maxFraction: number,
        filter?: IQueryFilter
    ): IRaycastResult2D | null {
        const hits = this._shapeStore.rayCastAll(origin, direction, maxFraction, filter);
        return hits.length > 0 ? hits[0] : null;
    }

    rayCastAll(
        origin: Readonly<IVec2Like>,
        direction: Readonly<IVec2Like>,
        maxFraction: number,
        filter?: IQueryFilter
    ): readonly IRaycastResult2D[] {
        return this._shapeStore.rayCastAll(origin, direction, maxFraction, filter);
    }

    queryAABB(
        min: Readonly<IVec2Like>,
        max: Readonly<IVec2Like>,
        callback: IAABBQueryCallback
    ): void {
        for (const shapeId of this._shapeStore.queryAABBAll(min, max)) {
            if (!callback(shapeId)) {
                break;
            }
        }
    }

    queryAABBAll(
        min: Readonly<IVec2Like>,
        max: Readonly<IVec2Like>,
        filter?: IQueryFilter
    ): readonly ShapeId[] {
        return this._shapeStore.queryAABBAll(min, max, filter);
    }

    queryPoint(point: Readonly<IVec2Like>, callback: IAABBQueryCallback): void {
        for (const shapeId of this._shapeStore.queryPointAll(point)) {
            if (!callback(shapeId)) {
                break;
            }
        }
    }

    queryPointAll(point: Readonly<IVec2Like>, filter?: IQueryFilter): readonly ShapeId[] {
        return this._shapeStore.queryPointAll(point, filter);
    }

    shiftOrigin(newOrigin: Readonly<IVec2Like>): void {
        for (const bodyId of this._bodyManager.getBodyIds()) {
            const position = this._bodyManager.getPosition(bodyId);
            this._bodyManager.setPosition(bodyId, {
                x: position.x - newOrigin.x,
                y: position.y - newOrigin.y,
            });
        }
    }

    clearForces(): void {
        this._bodyManager.clearForces();
    }

    wakeAllBodies(): void {
        for (const bodyId of this._bodyManager.getBodyIds()) {
            this._bodyManager.setAwake(bodyId, true);
        }
    }

    getStatistics(): IPhysicsWorldStatistics {
        return {
            bodyCount: this._bodyManager.bodyCount,
            shapeCount: this._shapeStore.size,
            constraintCount: this._constraintStore.size,
            contactCount: this._contactManager.contactCount,
            proxyCount: this.getProxyCount(),
            islandCount: 0,
            treeHeight: 0,
            treeBalance: 0,
            treeQuality: 0,
            stepTime: this._stepTime,
            collisionTime: 0,
            solveTime: 0,
            broadphaseTime: 0,
            narrowphaseTime: 0,
        };
    }

    getProfiler(): IPhysicsProfiler | null {
        return this._profiler;
    }

    setAutoClearForces(flag: boolean): void {
        this._autoClearForces = flag;
    }

    getAutoClearForces(): boolean {
        return this._autoClearForces;
    }

    getProxyCount(): number {
        return this._shapeStore.getProxyCount();
    }

    getTreeHeight(): number {
        return 0;
    }

    getTreeBalance(): number {
        return 0;
    }

    getTreeQuality(): number {
        return 0;
    }

    validate(): boolean {
        return this._shapeStore.validate() && this._constraintStore.validate();
    }

    dump(): void {}

    [Symbol.dispose](): void {
        if (this._disposed) {
            return;
        }

        this._disposed = true;
        this._bodyViews.clear();
        this._shapeStore.clear();
        this._constraintStore.clear();

        this._bodyManager[Symbol.dispose]();
        this._shapeManager[Symbol.dispose]();
        this._constraintManager[Symbol.dispose]();
        this._contactManager[Symbol.dispose]();
    }
}

function cloneVec(vector: Readonly<IVec2Like>): IVec2Like {
    return { x: vector.x, y: vector.y };
}