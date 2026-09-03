import { Vec3, Quat, clamp, type IVec3Like } from '@axrone/numeric';
import type {
    IAABBQueryCallback,
    ICollisionFilter,
    IConstraint3D,
    IMassData3D,
    IMaterial,
    IPhysicsBody3D,
    IPhysicsWorldStatistics,
    IRaycastResult3D,
    IShape3D,
    Mass,
} from '../types';
import {
    BodyFlags,
    BodyType,
    ShapeType,
} from '../types';
import type {
    BodyId3D,
    ConstraintId3D,
    IBoxShapeDef3D,
    ICapsuleShapeDef3D,
    ICollisionFilter3D,
    IConeShapeDef3D,
    IConeTwistConstraintDef3D,
    IContactListener3D,
    IConvexHullShapeDef3D,
    ICylinderShapeDef3D,
    IFixedConstraintDef3D,
    IGenericConstraintDef3D,
    IHeightFieldShapeDef3D,
    IHingeConstraintDef3D,
    IPhysicsBodyDef3D,
    IPhysicsProfiler3D,
    IPhysicsWorld3DConfig,
    IQueryFilter3D,
    ISliderConstraintDef3D,
    ISphereShapeDef3D,
    ISpringConstraintDef3D,
    ITriangleMeshShapeDef3D,
    RaycastCallback3D,
    ShapeId3D,
} from '../types/physics-3d';
import {
    BodyManager3D,
    ConstraintManager3D,
    ShapeManager3D,
} from './physics-managers-3d';
import { PhysicsWorld3DContactRuntime } from './physics-world-3d-contact-runtime';
import {
    BODY_TYPE_DYNAMIC,
    BODY_TYPE_STATIC,
    CONSTRAINT_TYPE_CONE_TWIST,
    CONSTRAINT_TYPE_FIXED,
    CONSTRAINT_TYPE_GENERIC,
    CONSTRAINT_TYPE_HINGE,
    CONSTRAINT_TYPE_SLIDER,
    CONSTRAINT_TYPE_SPRING,
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
    type IConstraintDescriptor3D,
    type IShapeDescriptor3D,
    type IShapeOptions3D,
    type IShapeRayHit3D,
    type SupportedConstraintDef3D,
    type SupportedShapeDef3D,
    componentMax,
    componentMin,
    cylinderConeLocalHalfExtents,
    expandAabb,
    getAxisVector,
    getBoxWorldExtents,
    getHeightFieldLocalVertex,
    inverseTransformPoint3D,
    inverseVec3,
    intersectsAabb,
    linePointDistanceSquared,
    makeFilter,
    makeMaterial,
    midpointVec3,
    rayAabbHit,
    raySphereHit,
    rayTriangleHit,
    supportsQueryFilter,
    transformPoint3D,
} from './physics-world-3d-shared';

export { BodyManager3D, ShapeManager3D, ConstraintManager3D } from './physics-managers-3d';

export class PhysicsWorld3D implements Disposable {
    readonly config: Readonly<IPhysicsWorld3DConfig>;
    private readonly _gravity: Vec3;

    private readonly _bodyManager: BodyManager3D;
    private readonly _shapeManager: ShapeManager3D;
    private readonly _constraintManager: ConstraintManager3D;
    private readonly _shapeDescriptors = new Map<ShapeId3D, IShapeDescriptor3D>();
    private readonly _shapeViews = new Map<ShapeId3D, IShape3D>();
    private readonly _constraintDescriptors = new Map<ConstraintId3D, IConstraintDescriptor3D>();
    private readonly _constraintViews = new Map<ConstraintId3D, IConstraint3D>();
    private readonly _bodyViews = new Map<BodyId3D, IPhysicsBody3D>();
    private readonly _contactRuntime: PhysicsWorld3DContactRuntime;

    private _profiler: IPhysicsProfiler3D | null = null;
    private _contactListener: IContactListener3D | null = null;
    private _collisionFilter: ICollisionFilter | null = null;
    private _autoClearForces = true;
    private _disposed = false;

    constructor(config: IPhysicsWorld3DConfig = {}) {
        this.config = config;
        this._gravity = config.gravity ? Vec3.from(config.gravity) : new Vec3(0, -9.81, 0);

        const maxBodies = config.maxBodies ?? 4096;
        const maxShapes = config.maxShapes ?? 8192;
        const maxConstraints = config.maxConstraints ?? 2048;

        this._bodyManager = new BodyManager3D(maxBodies);
        this._shapeManager = new ShapeManager3D(maxShapes);
        this._constraintManager = new ConstraintManager3D(maxConstraints);

        if (config.enableProfiler) {
            this._profiler = {
                stepTime: 0,
                collisionTime: 0,
                solveTime: 0,
                broadphaseTime: 0,
                narrowphaseTime: 0,
                solveVelocityTime: 0,
                solvePositionTime: 0,
                sleepTime: 0,
                ccdTime: 0,
            };
        }

        this._contactRuntime = new PhysicsWorld3DContactRuntime({
            bodyManager: this._bodyManager,
            shapeDescriptors: this._shapeDescriptors,
            constraintDescriptors: this._constraintDescriptors,
            getProfiler: () => this._profiler,
            getContactListener: () => this._contactListener,
            getCollisionFilter: () => this._collisionFilter,
            computeShapeAabb: (descriptor) => this._computeShapeAabb(descriptor),
            getShapeWorldCenter: (descriptor) => this._getShapeWorldCenter(descriptor),
            getConstraintAnchor: (def, firstBody) => this._getConstraintAnchor(def, firstBody),
        });
    }

    get gravity(): Readonly<IVec3Like> {
        return this._gravity;
    }

    createBody(def: IPhysicsBodyDef3D): BodyId3D {
        return this._bodyManager.createBody(def);
    }

    destroyBody(bodyId: BodyId3D): void {
        const shapeIds = [...this._shapeManager.getShapesForBody(bodyId)];
        for (const shapeId of shapeIds) {
            this.destroyShape(shapeId);
        }

        const constraintIds = [...this._constraintManager.getConstraintsForBody(bodyId)];
        for (const constraintId of constraintIds) {
            this.destroyConstraint(constraintId);
        }

        this._bodyViews.delete(bodyId);
        this._bodyManager.destroyBody(bodyId);
    }

    getBody(bodyId: BodyId3D): IPhysicsBody3D | null {
        if (!this._bodyManager.hasBody(bodyId)) {
            return null;
        }

        let view = this._bodyViews.get(bodyId);
        if (!view) {
            view = this._createBodyView(bodyId);
            this._bodyViews.set(bodyId, view);
        }
        return view;
    }

    getBodies(): ReadonlyMap<BodyId3D, IPhysicsBody3D> {
        const bodies = new Map<BodyId3D, IPhysicsBody3D>();
        for (const bodyId of this._bodyManager.getBodyIds()) {
            const body = this.getBody(bodyId);
            if (body) {
                bodies.set(bodyId, body);
            }
        }
        return bodies;
    }

    createSphereShape(
        bodyId: BodyId3D,
        def: ISphereShapeDef3D,
        material?: Partial<IMaterial>,
        filter?: ICollisionFilter3D,
        options?: IShapeOptions3D
    ): ShapeId3D {
        const shapeId = this._shapeManager.createSphere(bodyId, def, material, filter);
        this._shapeDescriptors.set(shapeId, {
            id: shapeId,
            bodyId,
            type: SHAPE_TYPE_SPHERE,
            def: { ...def, kind: SHAPE_TYPE_SPHERE },
            material: makeMaterial(material),
            isSensor: options?.isSensor ?? false,
            filter: makeFilter(filter),
            ...(options?.userData !== undefined ? { userData: options.userData } : {}),
        });
        return shapeId;
    }

    createBoxShape(
        bodyId: BodyId3D,
        def: IBoxShapeDef3D,
        material?: Partial<IMaterial>,
        filter?: ICollisionFilter3D,
        options?: IShapeOptions3D
    ): ShapeId3D {
        const shapeId = this._shapeManager.createBox(bodyId, def, material, filter);
        this._shapeDescriptors.set(shapeId, {
            id: shapeId,
            bodyId,
            type: SHAPE_TYPE_BOX,
            def: { ...def, kind: SHAPE_TYPE_BOX },
            material: makeMaterial(material),
            isSensor: options?.isSensor ?? false,
            filter: makeFilter(filter),
            ...(options?.userData !== undefined ? { userData: options.userData } : {}),
        });
        return shapeId;
    }

    createCapsuleShape(
        bodyId: BodyId3D,
        def: ICapsuleShapeDef3D,
        material?: Partial<IMaterial>,
        filter?: ICollisionFilter3D,
        options?: IShapeOptions3D
    ): ShapeId3D {
        const shapeId = this._shapeManager.createCapsule(bodyId, def, material, filter);
        this._shapeDescriptors.set(shapeId, {
            id: shapeId,
            bodyId,
            type: SHAPE_TYPE_CAPSULE,
            def: { ...def, kind: SHAPE_TYPE_CAPSULE },
            material: makeMaterial(material),
            isSensor: options?.isSensor ?? false,
            filter: makeFilter(filter),
            ...(options?.userData !== undefined ? { userData: options.userData } : {}),
        });
        return shapeId;
    }

    createCylinderShape(
        bodyId: BodyId3D,
        def: ICylinderShapeDef3D,
        material?: Partial<IMaterial>,
        filter?: ICollisionFilter3D,
        options?: IShapeOptions3D
    ): ShapeId3D {
        const shapeId = this._shapeManager.createCylinder(bodyId, def, material, filter);
        this._shapeDescriptors.set(shapeId, {
            id: shapeId,
            bodyId,
            type: SHAPE_TYPE_CYLINDER,
            def: { ...def, kind: SHAPE_TYPE_CYLINDER },
            material: makeMaterial(material),
            isSensor: options?.isSensor ?? false,
            filter: makeFilter(filter),
            ...(options?.userData !== undefined ? { userData: options.userData } : {}),
        });
        return shapeId;
    }

    createConeShape(
        bodyId: BodyId3D,
        def: IConeShapeDef3D,
        material?: Partial<IMaterial>,
        filter?: ICollisionFilter3D,
        options?: IShapeOptions3D
    ): ShapeId3D {
        const shapeId = this._shapeManager.createCone(bodyId, def, material, filter);
        this._shapeDescriptors.set(shapeId, {
            id: shapeId,
            bodyId,
            type: SHAPE_TYPE_CONE,
            def: { ...def, kind: SHAPE_TYPE_CONE },
            material: makeMaterial(material),
            isSensor: options?.isSensor ?? false,
            filter: makeFilter(filter),
            ...(options?.userData !== undefined ? { userData: options.userData } : {}),
        });
        return shapeId;
    }

    createConvexHullShape(
        bodyId: BodyId3D,
        def: IConvexHullShapeDef3D,
        material?: Partial<IMaterial>,
        filter?: ICollisionFilter3D,
        options?: IShapeOptions3D
    ): ShapeId3D {
        const shapeId = this._shapeManager.createConvexHull(bodyId, def, material, filter);
        this._shapeDescriptors.set(shapeId, {
            id: shapeId,
            bodyId,
            type: SHAPE_TYPE_CONVEX_HULL,
            def: { ...def, vertices: def.vertices.map(Vec3.copy), kind: SHAPE_TYPE_CONVEX_HULL },
            material: makeMaterial(material),
            isSensor: options?.isSensor ?? false,
            filter: makeFilter(filter),
            ...(options?.userData !== undefined ? { userData: options.userData } : {}),
        });
        return shapeId;
    }

    createTriangleMeshShape(
        bodyId: BodyId3D,
        def: ITriangleMeshShapeDef3D,
        material?: Partial<IMaterial>,
        filter?: ICollisionFilter3D,
        options?: IShapeOptions3D
    ): ShapeId3D {
        const shapeId = this._shapeManager.createTriangleMesh(bodyId, def, material, filter);
        this._shapeDescriptors.set(shapeId, {
            id: shapeId,
            bodyId,
            type: SHAPE_TYPE_TRIANGLE_MESH,
            def: {
                vertices: def.vertices.map(Vec3.copy),
                indices: [...def.indices],
                kind: SHAPE_TYPE_TRIANGLE_MESH,
            },
            material: makeMaterial(material),
            isSensor: options?.isSensor ?? false,
            filter: makeFilter(filter),
            ...(options?.userData !== undefined ? { userData: options.userData } : {}),
        });
        return shapeId;
    }

    createHeightFieldShape(
        bodyId: BodyId3D,
        def: IHeightFieldShapeDef3D,
        material?: Partial<IMaterial>,
        filter?: ICollisionFilter3D,
        options?: IShapeOptions3D
    ): ShapeId3D {
        const shapeId = this._shapeManager.createHeightField(bodyId, def, material, filter);
        this._shapeDescriptors.set(shapeId, {
            id: shapeId,
            bodyId,
            type: SHAPE_TYPE_HEIGHTFIELD,
            def: {
                heights: new Float32Array(def.heights),
                width: def.width,
                depth: def.depth,
                scaleX: def.scaleX,
                scaleY: def.scaleY,
                scaleZ: def.scaleZ,
                kind: SHAPE_TYPE_HEIGHTFIELD,
            },
            material: makeMaterial(material),
            isSensor: options?.isSensor ?? false,
            filter: makeFilter(filter),
            ...(options?.userData !== undefined ? { userData: options.userData } : {}),
        });
        return shapeId;
    }

    destroyShape(shapeId: ShapeId3D): void {
        this._shapeViews.delete(shapeId);
        this._shapeDescriptors.delete(shapeId);
        this._contactRuntime.pruneShape(shapeId);
        this._shapeManager.destroyShape(shapeId);
    }

    getShape(shapeId: ShapeId3D): IShape3D | null {
        const descriptor = this._shapeDescriptors.get(shapeId);
        if (!descriptor) {
            return null;
        }

        let view = this._shapeViews.get(shapeId);
        if (!view) {
            view = this._createShapeView(descriptor);
            this._shapeViews.set(shapeId, view);
        }
        return view;
    }

    createFixedConstraint(def: IFixedConstraintDef3D): ConstraintId3D {
        return this._registerConstraint(
            this._constraintManager.createFixed(def),
            CONSTRAINT_TYPE_FIXED,
            { ...def, localAnchorA: Vec3.copy(def.localAnchorA), localAnchorB: Vec3.copy(def.localAnchorB), kind: CONSTRAINT_TYPE_FIXED }
        );
    }

    createHingeConstraint(def: IHingeConstraintDef3D): ConstraintId3D {
        return this._registerConstraint(
            this._constraintManager.createHinge(def),
            CONSTRAINT_TYPE_HINGE,
            {
                ...def,
                localAnchorA: Vec3.copy(def.localAnchorA),
                localAnchorB: Vec3.copy(def.localAnchorB),
                localAxisA: Vec3.copy(def.localAxisA),
                localAxisB: Vec3.copy(def.localAxisB),
                kind: CONSTRAINT_TYPE_HINGE,
            }
        );
    }

    createSliderConstraint(def: ISliderConstraintDef3D): ConstraintId3D {
        return this._registerConstraint(
            this._constraintManager.createSlider(def),
            CONSTRAINT_TYPE_SLIDER,
            {
                ...def,
                localAnchorA: Vec3.copy(def.localAnchorA),
                localAnchorB: Vec3.copy(def.localAnchorB),
                localAxisA: Vec3.copy(def.localAxisA),
                kind: CONSTRAINT_TYPE_SLIDER,
            }
        );
    }

    createSpringConstraint(def: ISpringConstraintDef3D): ConstraintId3D {
        return this._registerConstraint(
            this._constraintManager.createSpring(def),
            CONSTRAINT_TYPE_SPRING,
            { ...def, localAnchorA: Vec3.copy(def.localAnchorA), localAnchorB: Vec3.copy(def.localAnchorB), kind: CONSTRAINT_TYPE_SPRING }
        );
    }

    createConeTwistConstraint(def: IConeTwistConstraintDef3D): ConstraintId3D {
        return this._registerConstraint(
            this._constraintManager.createConeTwist(def),
            CONSTRAINT_TYPE_CONE_TWIST,
            {
                ...def,
                localFrameA: {
                    position: Vec3.copy(def.localFrameA.position),
                    rotation: Quat.copy(def.localFrameA.rotation),
                },
                localFrameB: {
                    position: Vec3.copy(def.localFrameB.position),
                    rotation: Quat.copy(def.localFrameB.rotation),
                },
                kind: CONSTRAINT_TYPE_CONE_TWIST,
            }
        );
    }

    createGenericConstraint(def: IGenericConstraintDef3D): ConstraintId3D {
        return this._registerConstraint(
            this._constraintManager.createGeneric(def),
            CONSTRAINT_TYPE_GENERIC,
            {
                ...def,
                localFrameA: {
                    position: Vec3.copy(def.localFrameA.position),
                    rotation: Quat.copy(def.localFrameA.rotation),
                },
                localFrameB: {
                    position: Vec3.copy(def.localFrameB.position),
                    rotation: Quat.copy(def.localFrameB.rotation),
                },
                linearLowerLimit: Vec3.copy(def.linearLowerLimit),
                linearUpperLimit: Vec3.copy(def.linearUpperLimit),
                angularLowerLimit: Vec3.copy(def.angularLowerLimit),
                angularUpperLimit: Vec3.copy(def.angularUpperLimit),
                ...(def.linearStiffness ? { linearStiffness: Vec3.copy(def.linearStiffness) } : {}),
                ...(def.angularStiffness ? { angularStiffness: Vec3.copy(def.angularStiffness) } : {}),
                ...(def.linearDamping ? { linearDamping: Vec3.copy(def.linearDamping) } : {}),
                ...(def.angularDamping ? { angularDamping: Vec3.copy(def.angularDamping) } : {}),
                kind: CONSTRAINT_TYPE_GENERIC,
            }
        );
    }

    destroyConstraint(constraintId: ConstraintId3D): void {
        this._constraintViews.delete(constraintId);
        this._constraintDescriptors.delete(constraintId);
        this._constraintManager.destroyConstraint(constraintId);
    }

    getConstraint(constraintId: ConstraintId3D): IConstraint3D | null {
        const descriptor = this._constraintDescriptors.get(constraintId);
        if (!descriptor) {
            return null;
        }

        let view = this._constraintViews.get(constraintId);
        if (!view) {
            view = this._createConstraintView(descriptor);
            this._constraintViews.set(constraintId, view);
        }
        return view;
    }

    getBodyManager(): BodyManager3D {
        return this._bodyManager;
    }

    getShapeManager(): ShapeManager3D {
        return this._shapeManager;
    }

    getConstraintManager(): ConstraintManager3D {
        return this._constraintManager;
    }

    step(deltaTime: number, velocityIterations: number = 10, positionIterations: number = 4): void {
        if (this._disposed) return;

        const t0 = performance.now();

        this._integrateVelocities(deltaTime);
        this._integratePositions(deltaTime);
        this._solveConstraints(deltaTime, velocityIterations, positionIterations);

        if (this._profiler) {
            this._profiler.stepTime = performance.now() - t0;
        }
    }

    setContactListener(listener: IContactListener3D | null): void {
        this._contactListener = listener;
    }

    setCollisionFilter(filter: ICollisionFilter | null): void {
        this._collisionFilter = filter;
    }

    setGravity(gravity: Readonly<IVec3Like>): void {
        this._gravity.x = gravity.x;
        this._gravity.y = gravity.y;
        this._gravity.z = gravity.z;
    }

    getGravity(): Readonly<IVec3Like> {
        return this._gravity;
    }

    raycast(
        origin: IVec3Like,
        direction: IVec3Like,
        maxDistance: number,
        callback: RaycastCallback3D,
        filter?: IQueryFilter3D
    ): void {
        for (const result of this.rayCastAll(origin, direction, maxDistance, filter)) {
            // Box2D-style continuation contract: returning 0 terminates the query.
            const continuation = callback(
                result.shapeId,
                result.point,
                result.normal,
                result.fraction
            );
            if (continuation === 0) {
                break;
            }
        }
    }

    rayCastClosest(
        origin: Readonly<IVec3Like>,
        direction: Readonly<IVec3Like>,
        maxFraction: number,
        filter?: IQueryFilter3D
    ): IRaycastResult3D | null {
        return this.rayCastAll(origin, direction, maxFraction, filter)[0] ?? null;
    }

    rayCastAll(
        origin: Readonly<IVec3Like>,
        direction: Readonly<IVec3Like>,
        maxFraction: number,
        filter?: IQueryFilter3D
    ): readonly IRaycastResult3D[] {
        const results: IRaycastResult3D[] = [];

        for (const descriptor of this._shapeDescriptors.values()) {
            if (!supportsQueryFilter(descriptor.filter, filter)) {
                continue;
            }

            const hit = this._rayCastShape(descriptor, origin, direction, maxFraction);
            if (!hit) {
                continue;
            }

            results.push({
                hit: true,
                bodyId: descriptor.bodyId,
                shapeId: descriptor.id,
                point: Vec3.add(origin, Vec3.multiplyScalar(direction, hit.fraction)),
                normal: hit.normal,
                fraction: hit.fraction,
            });
        }

        results.sort((left, right) => left.fraction - right.fraction);
        return results;
    }

    queryAABB(min: Readonly<IVec3Like>, max: Readonly<IVec3Like>, callback: IAABBQueryCallback): void {
        for (const shapeId of this.queryAABBAll(min, max)) {
            if (!callback(shapeId)) {
                break;
            }
        }
    }

    queryAABBAll(
        min: Readonly<IVec3Like>,
        max: Readonly<IVec3Like>,
        filter?: IQueryFilter3D
    ): readonly ShapeId3D[] {
        const queryBounds = { min: Vec3.copy(min), max: Vec3.copy(max) };
        const shapeIds: ShapeId3D[] = [];

        for (const descriptor of this._shapeDescriptors.values()) {
            if (!supportsQueryFilter(descriptor.filter, filter)) {
                continue;
            }
            if (intersectsAabb(this._computeShapeAabb(descriptor), queryBounds)) {
                shapeIds.push(descriptor.id);
            }
        }

        return shapeIds;
    }

    queryPoint(point: Readonly<IVec3Like>, callback: IAABBQueryCallback): void {
        for (const shapeId of this.queryPointAll(point)) {
            if (!callback(shapeId)) {
                break;
            }
        }
    }

    queryPointAll(point: Readonly<IVec3Like>, filter?: IQueryFilter3D): readonly ShapeId3D[] {
        const shapeIds: ShapeId3D[] = [];

        for (const descriptor of this._shapeDescriptors.values()) {
            if (!supportsQueryFilter(descriptor.filter, filter)) {
                continue;
            }
            if (this._testPointShape(descriptor, point)) {
                shapeIds.push(descriptor.id);
            }
        }

        return shapeIds;
    }

    shiftOrigin(newOrigin: Readonly<IVec3Like>): void {
        for (const bodyId of this._bodyManager.getBodyIds()) {
            const position = this._bodyManager.getPosition(bodyId);
            this._bodyManager.setPosition(bodyId, Vec3.subtract(position, newOrigin));
        }
    }

    clearForces(): void {
        // The current 3D runtime applies forces directly into velocity state,
        // so there is no accumulated force buffer to clear yet.
    }

    wakeAllBodies(): void {
        for (const bodyId of this._bodyManager.getBodyIds()) {
            this._bodyManager.setAwake(bodyId, true);
        }
    }

    getStatistics(): IPhysicsWorldStatistics {
        return {
            bodyCount: this._bodyManager.bodyCount,
            shapeCount: this._shapeManager.shapeCount,
            constraintCount: this._constraintManager.constraintCount,
            contactCount: this._contactRuntime.contactCount,
            proxyCount: this._shapeManager.shapeCount,
            islandCount: this._contactRuntime.islandCount,
            treeHeight: 0,
            treeBalance: 0,
            treeQuality: 0,
            stepTime: this._profiler?.stepTime ?? 0,
            collisionTime: this._profiler?.collisionTime ?? 0,
            solveTime: this._profiler?.solveTime ?? 0,
            broadphaseTime: this._profiler?.broadphaseTime ?? 0,
            narrowphaseTime: this._profiler?.narrowphaseTime ?? 0,
        };
    }

    getProfiler(): IPhysicsProfiler3D | null {
        return this._profiler;
    }

    setAutoClearForces(flag: boolean): void {
        this._autoClearForces = flag;
    }

    getAutoClearForces(): boolean {
        return this._autoClearForces;
    }

    getProxyCount(): number {
        return this._shapeManager.shapeCount;
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
        return !this._disposed;
    }

    private _integrateVelocities(dt: number): void {
        const bodyIds = this._bodyManager.getBodyIds();
        const gravityX = this._gravity.x * dt;
        const gravityY = this._gravity.y * dt;
        const gravityZ = this._gravity.z * dt;

        for (const bodyId of bodyIds) {
            if (this._bodyManager.getBodyType(bodyId) !== BODY_TYPE_DYNAMIC) continue;
            if (!this._bodyManager.isEnabled(bodyId)) continue;
            if (!this._bodyManager.isAwake(bodyId)) continue;

            const gravityScale = this._bodyManager.getGravityScale(bodyId);
            const velocity = this._bodyManager.getLinearVelocity(bodyId);
            const angularVelocity = this._bodyManager.getAngularVelocity(bodyId);
            const linearDamping = Math.max(0, 1 - this._bodyManager.getLinearDamping(bodyId) * dt);
            const angularDamping = Math.max(0, 1 - this._bodyManager.getAngularDamping(bodyId) * dt);

            this._bodyManager.setLinearVelocity(bodyId, {
                x: (velocity.x + gravityX * gravityScale) * linearDamping,
                y: (velocity.y + gravityY * gravityScale) * linearDamping,
                z: (velocity.z + gravityZ * gravityScale) * linearDamping,
            });

            this._bodyManager.setAngularVelocity(bodyId, {
                x: this._bodyManager.isFixedRotation(bodyId) ? 0 : angularVelocity.x * angularDamping,
                y: this._bodyManager.isFixedRotation(bodyId) ? 0 : angularVelocity.y * angularDamping,
                z: this._bodyManager.isFixedRotation(bodyId) ? 0 : angularVelocity.z * angularDamping,
            });
        }
    }

    private _solveConstraints(
        deltaTime: number,
        velocityIterations: number,
        positionIterations: number
    ): void {
        this._contactRuntime.solve(deltaTime, velocityIterations, positionIterations);
    }

    private _integratePositions(dt: number): void {
        const bodyIds = this._bodyManager.getBodyIds();

        for (const bodyId of bodyIds) {
            if (this._bodyManager.getBodyType(bodyId) === BODY_TYPE_STATIC) continue;
            if (!this._bodyManager.isEnabled(bodyId)) continue;
            if (!this._bodyManager.isAwake(bodyId)) continue;

            const position = this._bodyManager.getPosition(bodyId);
            const velocity = this._bodyManager.getLinearVelocity(bodyId);
            const rotation = this._bodyManager.getRotation(bodyId);
            const angularVelocity = this._bodyManager.getAngularVelocity(bodyId);

            this._bodyManager.setPosition(bodyId, {
                x: position.x + velocity.x * dt,
                y: position.y + velocity.y * dt,
                z: position.z + velocity.z * dt,
            });

            const angularSpeed = Math.sqrt(
                angularVelocity.x * angularVelocity.x +
                    angularVelocity.y * angularVelocity.y +
                    angularVelocity.z * angularVelocity.z
            );

            if (angularSpeed > 1e-10 && !this._bodyManager.isFixedRotation(bodyId)) {
                const halfAngle = angularSpeed * dt * 0.5;
                const s = Math.sin(halfAngle) / angularSpeed;
                const c = Math.cos(halfAngle);

                const dqx = angularVelocity.x * s;
                const dqy = angularVelocity.y * s;
                const dqz = angularVelocity.z * s;
                const dqw = c;

                const newW =
                    dqw * rotation.w -
                    dqx * rotation.x -
                    dqy * rotation.y -
                    dqz * rotation.z;
                const newX =
                    dqw * rotation.x +
                    dqx * rotation.w +
                    dqy * rotation.z -
                    dqz * rotation.y;
                const newY =
                    dqw * rotation.y -
                    dqx * rotation.z +
                    dqy * rotation.w +
                    dqz * rotation.x;
                const newZ =
                    dqw * rotation.z +
                    dqx * rotation.y -
                    dqy * rotation.x +
                    dqz * rotation.w;

                const length = Math.sqrt(
                    newX * newX + newY * newY + newZ * newZ + newW * newW
                );
                const inverseLength = length > 1e-10 ? 1 / length : 0;

                this._bodyManager.setRotation(bodyId, {
                    x: newX * inverseLength,
                    y: newY * inverseLength,
                    z: newZ * inverseLength,
                    w: newW * inverseLength,
                });
            }
        }

        if (this._autoClearForces) {
            this.clearForces();
        }
    }

    private _registerConstraint(
        constraintId: ConstraintId3D,
        type: number,
        def: SupportedConstraintDef3D
    ): ConstraintId3D {
        this._constraintDescriptors.set(constraintId, {
            id: constraintId,
            type,
            def,
            enabled: true,
            collideConnected: def.collideConnected ?? false,
            ...(def.userData !== undefined ? { userData: def.userData } : {}),
        });
        return constraintId;
    }

    private _createBodyView(bodyId: BodyId3D): IPhysicsBody3D {
        const bodyWorld = this;

        return {
            id: bodyId,
            get type() {
                return bodyWorld._bodyManager.getBodyType(bodyId) as BodyType;
            },
            get transform() {
                return {
                    position: Vec3.copy(bodyWorld._bodyManager.getPosition(bodyId)),
                    rotation: Quat.copy(bodyWorld._bodyManager.getRotation(bodyId)),
                };
            },
            get velocity() {
                return {
                    linear: Vec3.copy(bodyWorld._bodyManager.getLinearVelocity(bodyId)),
                    angular: Vec3.copy(bodyWorld._bodyManager.getAngularVelocity(bodyId)),
                };
            },
            get massData() {
                return bodyWorld._getBodyMassData(bodyId);
            },
            get shapes() {
                return bodyWorld._shapeManager.getShapesForBody(bodyId);
            },
            get flags() {
                let flags = bodyWorld._bodyManager.getBodyFlags(bodyId) as BodyFlags;
                if (!bodyWorld._bodyManager.isAwake(bodyId)) {
                    flags |= BodyFlags.Sleeping;
                }
                return flags;
            },
            get gravityScale() {
                return bodyWorld._bodyManager.getGravityScale(bodyId);
            },
            get linearDamping() {
                return bodyWorld._bodyManager.getLinearDamping(bodyId);
            },
            get angularDamping() {
                return bodyWorld._bodyManager.getAngularDamping(bodyId);
            },
            get sleepTime() {
                return 0;
            },
            get userData() {
                return bodyWorld._bodyManager.getUserData(bodyId);
            },
            applyForce(force, point) {
                bodyWorld._bodyManager.applyForce(bodyId, force, point);
            },
            applyForceToCenter(force) {
                bodyWorld._bodyManager.applyForceToCenter(bodyId, force);
            },
            applyTorque(torque) {
                bodyWorld._bodyManager.applyTorque(bodyId, torque);
            },
            applyImpulse(impulse, point) {
                bodyWorld._bodyManager.applyImpulse(bodyId, impulse, point);
            },
            applyImpulseToCenter(impulse) {
                bodyWorld._bodyManager.applyImpulseToCenter(bodyId, impulse);
            },
            applyAngularImpulse(impulse) {
                bodyWorld._bodyManager.applyAngularImpulse(bodyId, impulse);
            },
            getPosition() {
                return Vec3.copy(bodyWorld._bodyManager.getPosition(bodyId));
            },
            setPosition(position) {
                bodyWorld._bodyManager.setPosition(bodyId, position);
            },
            getRotation() {
                return Quat.copy(bodyWorld._bodyManager.getRotation(bodyId));
            },
            setRotation(rotation) {
                bodyWorld._bodyManager.setRotation(bodyId, rotation);
            },
            getTransform() {
                return {
                    position: Vec3.copy(bodyWorld._bodyManager.getPosition(bodyId)),
                    rotation: Quat.copy(bodyWorld._bodyManager.getRotation(bodyId)),
                };
            },
            setTransform(position, rotation) {
                bodyWorld._bodyManager.setPosition(bodyId, position);
                bodyWorld._bodyManager.setRotation(bodyId, rotation);
            },
            getLinearVelocity() {
                return Vec3.copy(bodyWorld._bodyManager.getLinearVelocity(bodyId));
            },
            setLinearVelocity(velocity) {
                bodyWorld._bodyManager.setLinearVelocity(bodyId, velocity);
            },
            getAngularVelocity() {
                return Vec3.copy(bodyWorld._bodyManager.getAngularVelocity(bodyId));
            },
            setAngularVelocity(velocity) {
                bodyWorld._bodyManager.setAngularVelocity(bodyId, velocity);
            },
            getLocalPoint(worldPoint) {
                return inverseTransformPoint3D(
                    worldPoint,
                    bodyWorld._bodyManager.getPosition(bodyId),
                    bodyWorld._bodyManager.getRotation(bodyId)
                );
            },
            getWorldPoint(localPoint) {
                return transformPoint3D(
                    localPoint,
                    bodyWorld._bodyManager.getPosition(bodyId),
                    bodyWorld._bodyManager.getRotation(bodyId)
                );
            },
            getLocalVector(worldVector) {
                return Quat.rotateVector(Quat.conjugate(bodyWorld._bodyManager.getRotation(bodyId)), worldVector);
            },
            getWorldVector(localVector) {
                return Quat.rotateVector(bodyWorld._bodyManager.getRotation(bodyId), localVector);
            },
            getLinearVelocityAtPoint(point) {
                const relativePoint = Vec3.subtract(point, bodyWorld._bodyManager.getPosition(bodyId));
                return Vec3.add(
                    bodyWorld._bodyManager.getLinearVelocity(bodyId),
                    Vec3.cross(bodyWorld._bodyManager.getAngularVelocity(bodyId), relativePoint)
                );
            },
            getMass() {
                return bodyWorld._bodyManager.getMass(bodyId) as Mass;
            },
            getInertiaTensor() {
                return Vec3.copy(bodyWorld._bodyManager.getInertiaTensor(bodyId));
            },
            getMassData() {
                return bodyWorld._getBodyMassData(bodyId);
            },
            setMassData(massData) {
                bodyWorld._bodyManager.setMass(bodyId, massData.mass);
                bodyWorld._bodyManager.setInertiaTensor(bodyId, massData.inertiaTensor);
            },
            resetMassData() {
                const massData = bodyWorld._computeBodyMassData(bodyId);
                bodyWorld._bodyManager.setMass(bodyId, massData.mass);
                bodyWorld._bodyManager.setInertiaTensor(bodyId, massData.inertiaTensor);
            },
            isSleeping() {
                return !bodyWorld._bodyManager.isAwake(bodyId);
            },
            setSleeping(sleeping) {
                bodyWorld._bodyManager.setAwake(bodyId, !sleeping);
            },
            isAwake() {
                return bodyWorld._bodyManager.isAwake(bodyId);
            },
            setAwake(awake) {
                bodyWorld._bodyManager.setAwake(bodyId, awake);
            },
            isEnabled() {
                return bodyWorld._bodyManager.isEnabled(bodyId);
            },
            setEnabled(enabled) {
                bodyWorld._bodyManager.setEnabled(bodyId, enabled);
            },
            isFixedRotation() {
                return bodyWorld._bodyManager.isFixedRotation(bodyId);
            },
            setFixedRotation(fixed) {
                bodyWorld._bodyManager.setFixedRotation(bodyId, fixed);
            },
            isBullet() {
                return bodyWorld._bodyManager.isBullet(bodyId);
            },
            setBullet(bullet) {
                bodyWorld._bodyManager.setBullet(bodyId, bullet);
            },
            getWorldCenter() {
                const massData = bodyWorld._getBodyMassData(bodyId);
                return transformPoint3D(
                    massData.center,
                    bodyWorld._bodyManager.getPosition(bodyId),
                    bodyWorld._bodyManager.getRotation(bodyId)
                );
            },
            getLocalCenter() {
                return Vec3.copy(bodyWorld._getBodyMassData(bodyId).center);
            },
        };
    }

    private _createShapeView(descriptor: IShapeDescriptor3D): IShape3D {
        return {
            id: descriptor.id,
            bodyId: descriptor.bodyId,
            get type() {
                return descriptor.type as ShapeType;
            },
            get material() {
                return descriptor.material;
            },
            get isSensor() {
                return descriptor.isSensor;
            },
            get filter() {
                return descriptor.filter;
            },
            get userData() {
                return descriptor.userData;
            },
            computeAABB: () => this._computeShapeAabb(descriptor),
            computeMassData: (density) => this._computeShapeMassData(descriptor, density),
            testPoint: (point) => this._testPointShape(descriptor, point),
            rayCast: (origin, direction, maxFraction) => {
                const hit = this._rayCastShape(descriptor, origin, direction, maxFraction);
                if (!hit) {
                    return null;
                }
                return { hit: true, fraction: hit.fraction, normal: hit.normal };
            },
            getCenter: () => this._getShapeWorldCenter(descriptor),
        };
    }

    private _createConstraintView(descriptor: IConstraintDescriptor3D): IConstraint3D {
        return {
            id: descriptor.id,
            type: descriptor.type,
            bodyIdA: descriptor.def.bodyIdA,
            bodyIdB: descriptor.def.bodyIdB,
            collideConnected: descriptor.collideConnected,
            get userData() {
                return descriptor.userData;
            },
            getAnchorA: () => this._getConstraintAnchor(descriptor.def, true),
            getAnchorB: () => this._getConstraintAnchor(descriptor.def, false),
            getReactionForce: () => ({ x: 0, y: 0, z: 0 }),
            getReactionTorque: () => ({ x: 0, y: 0, z: 0 }),
            isEnabled: () => descriptor.enabled,
            setEnabled: (enabled) => {
                descriptor.enabled = enabled;
            },
        };
    }

    private _getBodyMassData(bodyId: BodyId3D): IMassData3D {
        const mass = this._bodyManager.getMass(bodyId);
        const inertiaTensor = this._bodyManager.getInertiaTensor(bodyId);
        return {
            mass: mass as Mass,
            inverseMass: mass > 0 ? 1 / mass : 0,
            inertiaTensor: Vec3.copy(inertiaTensor),
            inverseInertiaTensor: inverseVec3(inertiaTensor),
            center: this._computeBodyMassData(bodyId).center,
        };
    }

    private _computeBodyMassData(bodyId: BodyId3D): IMassData3D {
        const shapes = this._shapeManager.getShapesForBody(bodyId);
        if (shapes.length === 0 || this._bodyManager.getBodyType(bodyId) !== BODY_TYPE_DYNAMIC) {
            const inertiaTensor = this._bodyManager.getInertiaTensor(bodyId);
            return {
                mass: this._bodyManager.getMass(bodyId) as Mass,
                inverseMass: this._bodyManager.getInverseMass(bodyId),
                inertiaTensor: Vec3.copy(inertiaTensor),
                inverseInertiaTensor: inverseVec3(inertiaTensor),
                center: { x: 0, y: 0, z: 0 },
            };
        }

        let totalMass = 0;
        let center = { x: 0, y: 0, z: 0 };
        const shapeMassData: IMassData3D[] = [];

        for (const shapeId of shapes) {
            const descriptor = this._shapeDescriptors.get(shapeId);
            if (!descriptor) {
                continue;
            }
            const massData = this._computeShapeMassData(descriptor, descriptor.material.density);
            shapeMassData.push(massData);
            totalMass += massData.mass;
            center = Vec3.add(center, Vec3.multiplyScalar(massData.center, massData.mass));
        }

        if (totalMass <= 1e-10) {
            return {
                mass: 0 as Mass,
                inverseMass: 0,
                inertiaTensor: { x: 0, y: 0, z: 0 },
                inverseInertiaTensor: { x: 0, y: 0, z: 0 },
                center: { x: 0, y: 0, z: 0 },
            };
        }

        center = Vec3.multiplyScalar(center, 1 / totalMass);

        let inertiaTensor = { x: 0, y: 0, z: 0 };
        for (const massData of shapeMassData) {
            const offset = Vec3.subtract(massData.center, center);
            inertiaTensor = {
                x:
                    inertiaTensor.x +
                    massData.inertiaTensor.x +
                    massData.mass * (offset.y * offset.y + offset.z * offset.z),
                y:
                    inertiaTensor.y +
                    massData.inertiaTensor.y +
                    massData.mass * (offset.x * offset.x + offset.z * offset.z),
                z:
                    inertiaTensor.z +
                    massData.inertiaTensor.z +
                    massData.mass * (offset.x * offset.x + offset.y * offset.y),
            };
        }

        return {
            mass: totalMass as Mass,
            inverseMass: totalMass > 0 ? 1 / totalMass : 0,
            inertiaTensor,
            inverseInertiaTensor: inverseVec3(inertiaTensor),
            center,
        };
    }

    private _computeShapeMassData(descriptor: IShapeDescriptor3D, density: number): IMassData3D {
        const safeDensity = Math.max(0, density);
        switch (descriptor.def.kind) {
            case SHAPE_TYPE_SPHERE: {
                const radius = descriptor.def.radius;
                const mass = ((4 / 3) * Math.PI * radius * radius * radius) * safeDensity;
                const inertia = (2 / 5) * mass * radius * radius;
                return {
                    mass: mass as Mass,
                    inverseMass: mass > 0 ? 1 / mass : 0,
                    inertiaTensor: { x: inertia, y: inertia, z: inertia },
                    inverseInertiaTensor: inverseVec3({ x: inertia, y: inertia, z: inertia }),
                    center: Vec3.copy(descriptor.def.center),
                };
            }
            case SHAPE_TYPE_BOX: {
                const halfExtents = descriptor.def.halfExtents;
                const fullExtents = Vec3.multiplyScalar(halfExtents, 2);
                const mass = fullExtents.x * fullExtents.y * fullExtents.z * safeDensity;
                const inertiaTensor = {
                    x: (mass * (fullExtents.y * fullExtents.y + fullExtents.z * fullExtents.z)) / 12,
                    y: (mass * (fullExtents.x * fullExtents.x + fullExtents.z * fullExtents.z)) / 12,
                    z: (mass * (fullExtents.x * fullExtents.x + fullExtents.y * fullExtents.y)) / 12,
                };
                return {
                    mass: mass as Mass,
                    inverseMass: mass > 0 ? 1 / mass : 0,
                    inertiaTensor,
                    inverseInertiaTensor: inverseVec3(inertiaTensor),
                    center: Vec3.copy(descriptor.def.center),
                };
            }
            case SHAPE_TYPE_CAPSULE: {
                const segment = Vec3.subtract(descriptor.def.p2, descriptor.def.p1);
                const segmentLength = Vec3.len(segment);
                const radius = descriptor.def.radius;
                const cylinderMass = Math.PI * radius * radius * segmentLength * safeDensity;
                const sphereMass = ((4 / 3) * Math.PI * radius * radius * radius) * safeDensity;
                const mass = cylinderMass + sphereMass;
                const inertia = radius * radius * mass;
                return {
                    mass: mass as Mass,
                    inverseMass: mass > 0 ? 1 / mass : 0,
                    inertiaTensor: { x: inertia, y: inertia, z: inertia },
                    inverseInertiaTensor: inverseVec3({ x: inertia, y: inertia, z: inertia }),
                    center: midpointVec3(descriptor.def.p1, descriptor.def.p2),
                };
            }
            case SHAPE_TYPE_CYLINDER: {
                const radius = descriptor.def.radius;
                const height = descriptor.def.height;
                const mass = Math.PI * radius * radius * height * safeDensity;
                const radial = (mass * (3 * radius * radius + height * height)) / 12;
                const axial = 0.5 * mass * radius * radius;
                const axis = descriptor.def.axis ?? 1;
                const inertiaTensor =
                    axis === 0
                        ? { x: axial, y: radial, z: radial }
                        : axis === 2
                          ? { x: radial, y: radial, z: axial }
                          : { x: radial, y: axial, z: radial };
                return {
                    mass: mass as Mass,
                    inverseMass: mass > 0 ? 1 / mass : 0,
                    inertiaTensor,
                    inverseInertiaTensor: inverseVec3(inertiaTensor),
                    center: Vec3.copy(descriptor.def.center),
                };
            }
            case SHAPE_TYPE_CONE: {
                const radius = descriptor.def.radius;
                const height = descriptor.def.height;
                const mass = ((Math.PI * radius * radius * height) / 3) * safeDensity;
                const axis = descriptor.def.axis ?? 1;
                const transverse = ((3 / 20) * mass * radius * radius) + ((3 / 5) * mass * height * height);
                const axial = (3 / 10) * mass * radius * radius;
                const inertiaTensor =
                    axis === 0
                        ? { x: axial, y: transverse, z: transverse }
                        : axis === 2
                          ? { x: transverse, y: transverse, z: axial }
                          : { x: transverse, y: axial, z: transverse };
                return {
                    mass: mass as Mass,
                    inverseMass: mass > 0 ? 1 / mass : 0,
                    inertiaTensor,
                    inverseInertiaTensor: inverseVec3(inertiaTensor),
                    center: Vec3.copy(descriptor.def.center),
                };
            }
            case SHAPE_TYPE_CONVEX_HULL: {
                const bounds = this._computeLocalConvexBounds(descriptor.def.vertices);
                const fullExtents = Vec3.subtract(bounds.max, bounds.min);
                const mass = fullExtents.x * fullExtents.y * fullExtents.z * safeDensity;
                const inertiaTensor = {
                    x: (mass * (fullExtents.y * fullExtents.y + fullExtents.z * fullExtents.z)) / 12,
                    y: (mass * (fullExtents.x * fullExtents.x + fullExtents.z * fullExtents.z)) / 12,
                    z: (mass * (fullExtents.x * fullExtents.x + fullExtents.y * fullExtents.y)) / 12,
                };
                return {
                    mass: mass as Mass,
                    inverseMass: mass > 0 ? 1 / mass : 0,
                    inertiaTensor,
                    inverseInertiaTensor: inverseVec3(inertiaTensor),
                    center: midpointVec3(bounds.min, bounds.max),
                };
            }
            case SHAPE_TYPE_TRIANGLE_MESH: {
                const bounds = this._computeLocalConvexBounds(descriptor.def.vertices);
                const fullExtents = Vec3.subtract(bounds.max, bounds.min);
                const mass = fullExtents.x * fullExtents.y * fullExtents.z * safeDensity;
                const inertiaTensor = {
                    x: (mass * (fullExtents.y * fullExtents.y + fullExtents.z * fullExtents.z)) / 12,
                    y: (mass * (fullExtents.x * fullExtents.x + fullExtents.z * fullExtents.z)) / 12,
                    z: (mass * (fullExtents.x * fullExtents.x + fullExtents.y * fullExtents.y)) / 12,
                };
                return {
                    mass: mass as Mass,
                    inverseMass: mass > 0 ? 1 / mass : 0,
                    inertiaTensor,
                    inverseInertiaTensor: inverseVec3(inertiaTensor),
                    center: midpointVec3(bounds.min, bounds.max),
                };
            }
            case SHAPE_TYPE_HEIGHTFIELD: {
                const bounds = this._computeLocalHeightFieldBounds(descriptor.def);
                const fullExtents = Vec3.subtract(bounds.max, bounds.min);
                const mass = fullExtents.x * fullExtents.y * fullExtents.z * safeDensity;
                const inertiaTensor = {
                    x: (mass * (fullExtents.y * fullExtents.y + fullExtents.z * fullExtents.z)) / 12,
                    y: (mass * (fullExtents.x * fullExtents.x + fullExtents.z * fullExtents.z)) / 12,
                    z: (mass * (fullExtents.x * fullExtents.x + fullExtents.y * fullExtents.y)) / 12,
                };
                return {
                    mass: mass as Mass,
                    inverseMass: mass > 0 ? 1 / mass : 0,
                    inertiaTensor,
                    inverseInertiaTensor: inverseVec3(inertiaTensor),
                    center: midpointVec3(bounds.min, bounds.max),
                };
            }
            default:
                return {
                    mass: 0 as Mass,
                    inverseMass: 0,
                    inertiaTensor: { x: 0, y: 0, z: 0 },
                    inverseInertiaTensor: { x: 0, y: 0, z: 0 },
                    center: { x: 0, y: 0, z: 0 },
                };
        }
    }

    private _computeShapeAabb(descriptor: IShapeDescriptor3D): IAabb3D {
        const position = this._bodyManager.getPosition(descriptor.bodyId);
        const rotation = this._bodyManager.getRotation(descriptor.bodyId);
        switch (descriptor.def.kind) {
            case SHAPE_TYPE_SPHERE: {
                const center = transformPoint3D(descriptor.def.center, position, rotation);
                const radius = descriptor.def.radius;
                return {
                    min: { x: center.x - radius, y: center.y - radius, z: center.z - radius },
                    max: { x: center.x + radius, y: center.y + radius, z: center.z + radius },
                };
            }
            case SHAPE_TYPE_BOX: {
                const center = transformPoint3D(descriptor.def.center, position, rotation);
                const worldRotation = Quat.multiply(rotation, descriptor.def.rotation ?? IDENTITY_ROTATION);
                const extents = getBoxWorldExtents(descriptor.def.halfExtents, worldRotation);
                return {
                    min: Vec3.subtract(center, extents),
                    max: Vec3.add(center, extents),
                };
            }
            case SHAPE_TYPE_CAPSULE: {
                const p1 = transformPoint3D(descriptor.def.p1, position, rotation);
                const p2 = transformPoint3D(descriptor.def.p2, position, rotation);
                const radius = descriptor.def.radius;
                return {
                    min: {
                        x: Math.min(p1.x, p2.x) - radius,
                        y: Math.min(p1.y, p2.y) - radius,
                        z: Math.min(p1.z, p2.z) - radius,
                    },
                    max: {
                        x: Math.max(p1.x, p2.x) + radius,
                        y: Math.max(p1.y, p2.y) + radius,
                        z: Math.max(p1.z, p2.z) + radius,
                    },
                };
            }
            case SHAPE_TYPE_CYLINDER: {
                const center = transformPoint3D(descriptor.def.center, position, rotation);
                const axis = descriptor.def.axis ?? 1;
                const localHalfExtents = cylinderConeLocalHalfExtents(axis, descriptor.def.radius, descriptor.def.height);
                const extents = getBoxWorldExtents(localHalfExtents, rotation);
                return {
                    min: Vec3.subtract(center, extents),
                    max: Vec3.add(center, extents),
                };
            }
            case SHAPE_TYPE_CONE: {
                const center = transformPoint3D(descriptor.def.center, position, rotation);
                const axis = descriptor.def.axis ?? 1;
                const localHalfExtents = cylinderConeLocalHalfExtents(axis, descriptor.def.radius, descriptor.def.height);
                const extents = getBoxWorldExtents(localHalfExtents, rotation);
                return {
                    min: Vec3.subtract(center, extents),
                    max: Vec3.add(center, extents),
                };
            }
            case SHAPE_TYPE_CONVEX_HULL:
            case SHAPE_TYPE_TRIANGLE_MESH: {
                let bounds: IAabb3D | null = null;
                for (const vertex of descriptor.def.vertices) {
                    const worldVertex = transformPoint3D(vertex, position, rotation);
                    bounds = bounds
                        ? expandAabb(bounds, worldVertex)
                        : { min: Vec3.copy(worldVertex), max: Vec3.copy(worldVertex) };
                }
                return bounds ?? { min: Vec3.copy(position), max: Vec3.copy(position) };
            }
            case SHAPE_TYPE_HEIGHTFIELD: {
                const localBounds = this._computeLocalHeightFieldBounds(descriptor.def);
                const corners: readonly IVec3Like[] = [
                    { x: localBounds.min.x, y: localBounds.min.y, z: localBounds.min.z },
                    { x: localBounds.min.x, y: localBounds.min.y, z: localBounds.max.z },
                    { x: localBounds.min.x, y: localBounds.max.y, z: localBounds.min.z },
                    { x: localBounds.min.x, y: localBounds.max.y, z: localBounds.max.z },
                    { x: localBounds.max.x, y: localBounds.min.y, z: localBounds.min.z },
                    { x: localBounds.max.x, y: localBounds.min.y, z: localBounds.max.z },
                    { x: localBounds.max.x, y: localBounds.max.y, z: localBounds.min.z },
                    { x: localBounds.max.x, y: localBounds.max.y, z: localBounds.max.z },
                ];
                let bounds: IAabb3D | null = null;
                for (const corner of corners) {
                    const worldCorner = transformPoint3D(corner, position, rotation);
                    bounds = bounds
                        ? expandAabb(bounds, worldCorner)
                        : { min: Vec3.copy(worldCorner), max: Vec3.copy(worldCorner) };
                }
                return bounds ?? { min: Vec3.copy(position), max: Vec3.copy(position) };
            }
            default:
                return { min: Vec3.copy(position), max: Vec3.copy(position) };
        }
    }

    private _testPointShape(descriptor: IShapeDescriptor3D, point: Readonly<IVec3Like>): boolean {
        const position = this._bodyManager.getPosition(descriptor.bodyId);
        const rotation = this._bodyManager.getRotation(descriptor.bodyId);

        switch (descriptor.def.kind) {
            case SHAPE_TYPE_SPHERE: {
                const center = transformPoint3D(descriptor.def.center, position, rotation);
                return Vec3.lengthSquared(Vec3.subtract(point, center)) <= descriptor.def.radius ** 2;
            }
            case SHAPE_TYPE_BOX: {
                const bodyLocal = inverseTransformPoint3D(point, position, rotation);
                const centered = Vec3.subtract(bodyLocal, descriptor.def.center);
                const localRotation = descriptor.def.rotation ?? IDENTITY_ROTATION;
                const localPoint = Quat.rotateVector(Quat.conjugate(localRotation), centered);
                return (
                    Math.abs(localPoint.x) <= descriptor.def.halfExtents.x &&
                    Math.abs(localPoint.y) <= descriptor.def.halfExtents.y &&
                    Math.abs(localPoint.z) <= descriptor.def.halfExtents.z
                );
            }
            case SHAPE_TYPE_CAPSULE: {
                const localPoint = inverseTransformPoint3D(point, position, rotation);
                return (
                    linePointDistanceSquared(localPoint, descriptor.def.p1, descriptor.def.p2) <=
                    descriptor.def.radius ** 2
                );
            }
            case SHAPE_TYPE_CYLINDER: {
                const localPoint = inverseTransformPoint3D(point, position, rotation);
                const centered = Vec3.subtract(localPoint, descriptor.def.center);
                const axis = descriptor.def.axis ?? 1;
                const halfHeight = descriptor.def.height * 0.5;
                if (axis === 0) {
                    return (
                        Math.abs(centered.x) <= halfHeight &&
                        centered.y * centered.y + centered.z * centered.z <= descriptor.def.radius ** 2
                    );
                }
                if (axis === 2) {
                    return (
                        Math.abs(centered.z) <= halfHeight &&
                        centered.x * centered.x + centered.y * centered.y <= descriptor.def.radius ** 2
                    );
                }
                return (
                    Math.abs(centered.y) <= halfHeight &&
                    centered.x * centered.x + centered.z * centered.z <= descriptor.def.radius ** 2
                );
            }
            case SHAPE_TYPE_CONE: {
                const localPoint = inverseTransformPoint3D(point, position, rotation);
                const centered = Vec3.subtract(localPoint, descriptor.def.center);
                const axis = descriptor.def.axis ?? 1;
                const halfHeight = descriptor.def.height * 0.5;
                const axial = axis === 0 ? centered.x : axis === 2 ? centered.z : centered.y;
                if (axial < -halfHeight || axial > halfHeight) {
                    return false;
                }
                const normalizedHeight = (axial + halfHeight) / descriptor.def.height;
                const allowedRadius = descriptor.def.radius * (1 - normalizedHeight);
                const radialSquared =
                    axis === 0
                        ? centered.y * centered.y + centered.z * centered.z
                        : axis === 2
                          ? centered.x * centered.x + centered.y * centered.y
                          : centered.x * centered.x + centered.z * centered.z;
                return radialSquared <= allowedRadius * allowedRadius;
            }
            case SHAPE_TYPE_CONVEX_HULL: {
                const localPoint = inverseTransformPoint3D(point, position, rotation);
                const bounds = this._computeLocalConvexBounds(descriptor.def.vertices);
                return (
                    localPoint.x >= bounds.min.x &&
                    localPoint.x <= bounds.max.x &&
                    localPoint.y >= bounds.min.y &&
                    localPoint.y <= bounds.max.y &&
                    localPoint.z >= bounds.min.z &&
                    localPoint.z <= bounds.max.z
                );
            }
            case SHAPE_TYPE_TRIANGLE_MESH: {
                const localPoint = inverseTransformPoint3D(point, position, rotation);
                const bounds = this._computeLocalConvexBounds(descriptor.def.vertices);
                if (
                    localPoint.x < bounds.min.x ||
                    localPoint.x > bounds.max.x ||
                    localPoint.y < bounds.min.y ||
                    localPoint.y > bounds.max.y ||
                    localPoint.z < bounds.min.z ||
                    localPoint.z > bounds.max.z
                ) {
                    return false;
                }

                let hitCount = 0;
                const localDirection = { x: 1, y: 0, z: 0 };
                for (let index = 0; index + 2 < descriptor.def.indices.length; index += 3) {
                    const a = descriptor.def.vertices[descriptor.def.indices[index]];
                    const b = descriptor.def.vertices[descriptor.def.indices[index + 1]];
                    const c = descriptor.def.vertices[descriptor.def.indices[index + 2]];
                    const hit = rayTriangleHit(
                        localPoint,
                        localDirection,
                        a,
                        b,
                        c,
                        Number.POSITIVE_INFINITY
                    );
                    if (hit && hit.fraction <= 1e-6) {
                        return true;
                    }
                    if (hit) {
                        hitCount += 1;
                    }
                }

                return (hitCount & 1) === 1;
            }
            case SHAPE_TYPE_HEIGHTFIELD: {
                const localPoint = inverseTransformPoint3D(point, position, rotation);
                const sampledHeight = this._sampleHeightFieldHeight(descriptor.def, localPoint.x, localPoint.z);
                return sampledHeight !== null && localPoint.y <= sampledHeight + 1e-4;
            }
            default:
                return false;
        }
    }

    private _rayCastShape(
        descriptor: IShapeDescriptor3D,
        origin: Readonly<IVec3Like>,
        direction: Readonly<IVec3Like>,
        maxFraction: number
    ): IShapeRayHit3D | null {
        const position = this._bodyManager.getPosition(descriptor.bodyId);
        const rotation = this._bodyManager.getRotation(descriptor.bodyId);

        switch (descriptor.def.kind) {
            case SHAPE_TYPE_SPHERE: {
                const center = transformPoint3D(descriptor.def.center, position, rotation);
                return raySphereHit(origin, direction, center, descriptor.def.radius, maxFraction);
            }
            case SHAPE_TYPE_BOX: {
                const worldRotation = Quat.multiply(rotation, descriptor.def.rotation ?? IDENTITY_ROTATION);
                const center = transformPoint3D(descriptor.def.center, position, rotation);
                const localOrigin = inverseTransformPoint3D(origin, center, worldRotation);
                const localDirection = Quat.rotateVector(Quat.conjugate(worldRotation), direction);
                const hit = rayAabbHit(
                    localOrigin,
                    localDirection,
                    Vec3.multiplyScalar(descriptor.def.halfExtents, -1),
                    descriptor.def.halfExtents,
                    maxFraction
                );
                if (!hit) {
                    return null;
                }
                return { fraction: hit.fraction, normal: Quat.rotateVector(worldRotation, hit.normal) };
            }
            case SHAPE_TYPE_TRIANGLE_MESH: {
                let closestHit: IShapeRayHit3D | null = null;
                for (let index = 0; index + 2 < descriptor.def.indices.length; index += 3) {
                    const a = transformPoint3D(
                        descriptor.def.vertices[descriptor.def.indices[index]],
                        position,
                        rotation
                    );
                    const b = transformPoint3D(
                        descriptor.def.vertices[descriptor.def.indices[index + 1]],
                        position,
                        rotation
                    );
                    const c = transformPoint3D(
                        descriptor.def.vertices[descriptor.def.indices[index + 2]],
                        position,
                        rotation
                    );
                    const hit = rayTriangleHit(origin, direction, a, b, c, maxFraction);
                    if (!hit || (closestHit && hit.fraction >= closestHit.fraction)) {
                        continue;
                    }
                    closestHit = hit;
                }
                return closestHit;
            }
            case SHAPE_TYPE_HEIGHTFIELD: {
                let closestHit: IShapeRayHit3D | null = null;
                for (let zIndex = 0; zIndex < descriptor.def.depth - 1; zIndex += 1) {
                    for (let xIndex = 0; xIndex < descriptor.def.width - 1; xIndex += 1) {
                        const topLeft = transformPoint3D(
                            getHeightFieldLocalVertex(descriptor.def, xIndex, zIndex),
                            position,
                            rotation
                        );
                        const topRight = transformPoint3D(
                            getHeightFieldLocalVertex(descriptor.def, xIndex + 1, zIndex),
                            position,
                            rotation
                        );
                        const bottomLeft = transformPoint3D(
                            getHeightFieldLocalVertex(descriptor.def, xIndex, zIndex + 1),
                            position,
                            rotation
                        );
                        const bottomRight = transformPoint3D(
                            getHeightFieldLocalVertex(descriptor.def, xIndex + 1, zIndex + 1),
                            position,
                            rotation
                        );

                        const firstHit = rayTriangleHit(
                            origin,
                            direction,
                            topLeft,
                            topRight,
                            bottomLeft,
                            maxFraction
                        );
                        if (firstHit && (!closestHit || firstHit.fraction < closestHit.fraction)) {
                            closestHit = firstHit;
                        }

                        const secondHit = rayTriangleHit(
                            origin,
                            direction,
                            bottomLeft,
                            topRight,
                            bottomRight,
                            maxFraction
                        );
                        if (secondHit && (!closestHit || secondHit.fraction < closestHit.fraction)) {
                            closestHit = secondHit;
                        }
                    }
                }
                return closestHit;
            }
            default: {
                const aabb = this._computeShapeAabb(descriptor);
                const hit = rayAabbHit(origin, direction, aabb.min, aabb.max, maxFraction);
                return hit ? { fraction: hit.fraction, normal: hit.normal } : null;
            }
        }
    }

    private _getShapeWorldCenter(descriptor: IShapeDescriptor3D): IVec3Like {
        const position = this._bodyManager.getPosition(descriptor.bodyId);
        const rotation = this._bodyManager.getRotation(descriptor.bodyId);
        switch (descriptor.def.kind) {
            case SHAPE_TYPE_SPHERE:
            case SHAPE_TYPE_BOX:
            case SHAPE_TYPE_CYLINDER:
            case SHAPE_TYPE_CONE:
                return transformPoint3D(descriptor.def.center, position, rotation);
            case SHAPE_TYPE_CAPSULE:
                return transformPoint3D(midpointVec3(descriptor.def.p1, descriptor.def.p2), position, rotation);
            case SHAPE_TYPE_CONVEX_HULL: {
                const bounds = this._computeLocalConvexBounds(descriptor.def.vertices);
                return transformPoint3D(midpointVec3(bounds.min, bounds.max), position, rotation);
            }
            case SHAPE_TYPE_TRIANGLE_MESH: {
                const bounds = this._computeLocalConvexBounds(descriptor.def.vertices);
                return transformPoint3D(midpointVec3(bounds.min, bounds.max), position, rotation);
            }
            case SHAPE_TYPE_HEIGHTFIELD: {
                const bounds = this._computeLocalHeightFieldBounds(descriptor.def);
                return transformPoint3D(midpointVec3(bounds.min, bounds.max), position, rotation);
            }
            default:
                return Vec3.copy(position);
        }
    }

    private _getConstraintAnchor(def: SupportedConstraintDef3D, firstBody: boolean): IVec3Like {
        if (def.kind === CONSTRAINT_TYPE_FIXED || def.kind === CONSTRAINT_TYPE_HINGE || def.kind === CONSTRAINT_TYPE_SLIDER || def.kind === CONSTRAINT_TYPE_SPRING) {
            const bodyId = firstBody ? def.bodyIdA : def.bodyIdB;
            const localAnchor = firstBody ? def.localAnchorA : def.localAnchorB;
            return transformPoint3D(
                localAnchor,
                this._bodyManager.getPosition(bodyId),
                this._bodyManager.getRotation(bodyId)
            );
        }

        const bodyId = firstBody ? def.bodyIdA : def.bodyIdB;
        const localFrame = firstBody ? def.localFrameA : def.localFrameB;
        return transformPoint3D(
            localFrame.position,
            this._bodyManager.getPosition(bodyId),
            this._bodyManager.getRotation(bodyId)
        );
    }

    private _computeLocalConvexBounds(vertices: readonly IVec3Like[]): IAabb3D {
        let bounds: IAabb3D | null = null;
        for (const vertex of vertices) {
            bounds = bounds
                ? expandAabb(bounds, vertex)
                : { min: Vec3.copy(vertex), max: Vec3.copy(vertex) };
        }
        return bounds ?? { min: { x: 0, y: 0, z: 0 }, max: { x: 0, y: 0, z: 0 } };
    }

    private _computeLocalHeightFieldBounds(def: Readonly<IHeightFieldShapeDef3D>): IAabb3D {
        let bounds: IAabb3D | null = null;
        for (let zIndex = 0; zIndex < def.depth; zIndex += 1) {
            for (let xIndex = 0; xIndex < def.width; xIndex += 1) {
                const vertex = getHeightFieldLocalVertex(def, xIndex, zIndex);
                bounds = bounds
                    ? expandAabb(bounds, vertex)
                    : { min: Vec3.copy(vertex), max: Vec3.copy(vertex) };
            }
        }

        return bounds ?? { min: { x: 0, y: 0, z: 0 }, max: { x: 0, y: 0, z: 0 } };
    }

    private _sampleHeightFieldHeight(
        def: Readonly<IHeightFieldShapeDef3D>,
        x: number,
        z: number
    ): number | null {
        if (def.width < 2 || def.depth < 2 || def.scaleX <= 0 || def.scaleZ <= 0) {
            return null;
        }

        const halfWidth = (def.width - 1) * 0.5;
        const halfDepth = (def.depth - 1) * 0.5;
        const gridX = x / def.scaleX + halfWidth;
        const gridZ = z / def.scaleZ + halfDepth;

        if (gridX < 0 || gridZ < 0 || gridX > def.width - 1 || gridZ > def.depth - 1) {
            return null;
        }

        const x0 = Math.min(def.width - 2, Math.max(0, Math.floor(gridX)));
        const z0 = Math.min(def.depth - 2, Math.max(0, Math.floor(gridZ)));
        const localX = gridX - x0;
        const localZ = gridZ - z0;

        const topLeft = def.heights[z0 * def.width + x0] * def.scaleY;
        const topRight = def.heights[z0 * def.width + x0 + 1] * def.scaleY;
        const bottomLeft = def.heights[(z0 + 1) * def.width + x0] * def.scaleY;
        const bottomRight = def.heights[(z0 + 1) * def.width + x0 + 1] * def.scaleY;

        if (localX + localZ <= 1) {
            return topLeft + (topRight - topLeft) * localX + (bottomLeft - topLeft) * localZ;
        }

        const u = 1 - localX;
        const v = 1 - localZ;
        return bottomRight + (bottomLeft - bottomRight) * u + (topRight - bottomRight) * v;
    }

    [Symbol.dispose](): void {
        if (this._disposed) return;
        this._disposed = true;
        this._bodyViews.clear();
        this._shapeViews.clear();
        this._shapeDescriptors.clear();
        this._constraintViews.clear();
        this._constraintDescriptors.clear();
        this._bodyManager[Symbol.dispose]();
        this._shapeManager[Symbol.dispose]();
        this._constraintManager[Symbol.dispose]();
    }
}