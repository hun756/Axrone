import { Vec3, type IQuatLike, type IVec3Like } from '@axrone/numeric';
import type {
    BodyFlags,
    ContactId,
    IAABBQueryCallback,
    ICollisionFilter,
    IConstraint3D,
    IContactManifold3D,
    IContactPoint3D,
    Impulse,
    IMassData3D,
    IMaterial,
    IPhysicsBody3D,
    IPhysicsWorldStatistics,
    IRaycastResult3D,
    IShape3D,
} from '../types';
import {
    BodyType,
    PhysicsConstants,
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

export { BodyManager3D, ShapeManager3D, ConstraintManager3D } from './physics-managers-3d';

const BODY_TYPE_STATIC = 0;
const BODY_TYPE_DYNAMIC = 2;

const SHAPE_TYPE_CAPSULE = 1;
const SHAPE_TYPE_BOX = 3;
const SHAPE_TYPE_SPHERE = 5;
const SHAPE_TYPE_CYLINDER = 6;
const SHAPE_TYPE_CONE = 7;
const SHAPE_TYPE_CONVEX_HULL = 8;
const SHAPE_TYPE_TRIANGLE_MESH = 9;
const SHAPE_TYPE_HEIGHTFIELD = 10;

const CONSTRAINT_TYPE_FIXED = 0;
const CONSTRAINT_TYPE_HINGE = 2;
const CONSTRAINT_TYPE_SLIDER = 3;
const CONSTRAINT_TYPE_CONE_TWIST = 4;
const CONSTRAINT_TYPE_GENERIC = 5;
const CONSTRAINT_TYPE_SPRING = 6;

type SupportedShapeDef3D =
    | ({ readonly kind: typeof SHAPE_TYPE_SPHERE } & ISphereShapeDef3D)
    | ({ readonly kind: typeof SHAPE_TYPE_BOX } & IBoxShapeDef3D)
    | ({ readonly kind: typeof SHAPE_TYPE_CAPSULE } & ICapsuleShapeDef3D)
    | ({ readonly kind: typeof SHAPE_TYPE_CYLINDER } & ICylinderShapeDef3D)
    | ({ readonly kind: typeof SHAPE_TYPE_CONE } & IConeShapeDef3D)
    | ({ readonly kind: typeof SHAPE_TYPE_CONVEX_HULL } & IConvexHullShapeDef3D)
    | ({ readonly kind: typeof SHAPE_TYPE_TRIANGLE_MESH } & ITriangleMeshShapeDef3D)
    | ({ readonly kind: typeof SHAPE_TYPE_HEIGHTFIELD } & IHeightFieldShapeDef3D);

type SupportedConstraintDef3D =
    | ({ readonly kind: typeof CONSTRAINT_TYPE_FIXED } & IFixedConstraintDef3D)
    | ({ readonly kind: typeof CONSTRAINT_TYPE_HINGE } & IHingeConstraintDef3D)
    | ({ readonly kind: typeof CONSTRAINT_TYPE_SLIDER } & ISliderConstraintDef3D)
    | ({ readonly kind: typeof CONSTRAINT_TYPE_SPRING } & ISpringConstraintDef3D)
    | ({ readonly kind: typeof CONSTRAINT_TYPE_CONE_TWIST } & IConeTwistConstraintDef3D)
    | ({ readonly kind: typeof CONSTRAINT_TYPE_GENERIC } & IGenericConstraintDef3D);

interface IShapeDescriptor3D {
    readonly id: ShapeId3D;
    readonly bodyId: BodyId3D;
    readonly type: number;
    readonly def: SupportedShapeDef3D;
    material: IMaterial;
    isSensor: boolean;
    filter: ICollisionFilter3D;
    userData?: unknown;
}

interface IConstraintDescriptor3D {
    readonly id: ConstraintId3D;
    readonly type: number;
    readonly def: SupportedConstraintDef3D;
    enabled: boolean;
    readonly collideConnected: boolean;
    userData?: unknown;
}

interface IShapeOptions3D {
    readonly isSensor?: boolean;
    readonly userData?: unknown;
}

interface IAabb3D {
    readonly min: IVec3Like;
    readonly max: IVec3Like;
}

interface IShapeRayHit3D {
    readonly fraction: number;
    readonly normal: IVec3Like;
}

interface IMutableContactPoint3D extends IContactPoint3D {
    id: ContactId;
    localPointA: IVec3Like;
    localPointB: IVec3Like;
    normalImpulse: Impulse;
    tangentImpulse1: Impulse;
    tangentImpulse2: Impulse;
    separation: number;
}

interface IMutableContactManifold3D extends IContactManifold3D {
    id: number;
    bodyIdA: BodyId3D;
    bodyIdB: BodyId3D;
    shapeIdA: ShapeId3D;
    shapeIdB: ShapeId3D;
    normal: IVec3Like;
    tangent1: IVec3Like;
    tangent2: IVec3Like;
    pointCount: number;
    points: IMutableContactPoint3D[];
}

interface IResolvedContactManifold3D extends IMutableContactManifold3D {
    readonly pairKey: string;
    readonly descriptorA: IShapeDescriptor3D;
    readonly descriptorB: IShapeDescriptor3D;
    readonly sensor: boolean;
    readonly friction: number;
    readonly restitution: number;
}

interface IShapePairCandidate3D {
    readonly descriptorA: IShapeDescriptor3D;
    readonly descriptorB: IShapeDescriptor3D;
    readonly aabbA: IAabb3D;
    readonly aabbB: IAabb3D;
    readonly pairKey: string;
}

const IDENTITY_ROTATION: IQuatLike = { x: 0, y: 0, z: 0, w: 1 };
const DEFAULT_MATERIAL: IMaterial = { friction: 0.4, restitution: 0, density: 1 };
const DEFAULT_FILTER: ICollisionFilter3D = { categoryBits: 1, maskBits: 0xffff, groupIndex: 0 };

function cloneVec3(vector: Readonly<IVec3Like>): IVec3Like {
    return { x: vector.x, y: vector.y, z: vector.z };
}

function cloneQuat(rotation: Readonly<IQuatLike>): IQuatLike {
    return { x: rotation.x, y: rotation.y, z: rotation.z, w: rotation.w };
}

function addVec3(a: Readonly<IVec3Like>, b: Readonly<IVec3Like>): IVec3Like {
    return { x: a.x + b.x, y: a.y + b.y, z: a.z + b.z };
}

function subVec3(a: Readonly<IVec3Like>, b: Readonly<IVec3Like>): IVec3Like {
    return { x: a.x - b.x, y: a.y - b.y, z: a.z - b.z };
}

function scaleVec3(vector: Readonly<IVec3Like>, scalar: number): IVec3Like {
    return { x: vector.x * scalar, y: vector.y * scalar, z: vector.z * scalar };
}

function negateVec3(vector: Readonly<IVec3Like>): IVec3Like {
    return { x: -vector.x, y: -vector.y, z: -vector.z };
}

function dotVec3(a: Readonly<IVec3Like>, b: Readonly<IVec3Like>): number {
    return a.x * b.x + a.y * b.y + a.z * b.z;
}

function crossVec3(a: Readonly<IVec3Like>, b: Readonly<IVec3Like>): IVec3Like {
    return {
        x: a.y * b.z - a.z * b.y,
        y: a.z * b.x - a.x * b.z,
        z: a.x * b.y - a.y * b.x,
    };
}

function lengthSquaredVec3(vector: Readonly<IVec3Like>): number {
    return dotVec3(vector, vector);
}

function lengthVec3(vector: Readonly<IVec3Like>): number {
    return Math.sqrt(lengthSquaredVec3(vector));
}

function normalizeVec3(vector: Readonly<IVec3Like>): IVec3Like {
    const length = lengthVec3(vector);
    if (length <= 1e-10) {
        return { x: 0, y: 0, z: 0 };
    }

    const inverseLength = 1 / length;
    return scaleVec3(vector, inverseLength);
}

function conjugateQuat(rotation: Readonly<IQuatLike>): IQuatLike {
    return { x: -rotation.x, y: -rotation.y, z: -rotation.z, w: rotation.w };
}

function multiplyQuat(a: Readonly<IQuatLike>, b: Readonly<IQuatLike>): IQuatLike {
    return {
        x: a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
        y: a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
        z: a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
        w: a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z,
    };
}

function rotateVec3(vector: Readonly<IVec3Like>, rotation: Readonly<IQuatLike>): IVec3Like {
    const qx = rotation.x;
    const qy = rotation.y;
    const qz = rotation.z;
    const qw = rotation.w;

    const tx = 2 * (qy * vector.z - qz * vector.y);
    const ty = 2 * (qz * vector.x - qx * vector.z);
    const tz = 2 * (qx * vector.y - qy * vector.x);

    return {
        x: vector.x + qw * tx + (qy * tz - qz * ty),
        y: vector.y + qw * ty + (qz * tx - qx * tz),
        z: vector.z + qw * tz + (qx * ty - qy * tx),
    };
}

function inverseRotateVec3(vector: Readonly<IVec3Like>, rotation: Readonly<IQuatLike>): IVec3Like {
    return rotateVec3(vector, conjugateQuat(rotation));
}

function transformPoint3D(
    point: Readonly<IVec3Like>,
    position: Readonly<IVec3Like>,
    rotation: Readonly<IQuatLike>
): IVec3Like {
    return addVec3(position, rotateVec3(point, rotation));
}

function inverseTransformPoint3D(
    point: Readonly<IVec3Like>,
    position: Readonly<IVec3Like>,
    rotation: Readonly<IQuatLike>
): IVec3Like {
    return inverseRotateVec3(subVec3(point, position), rotation);
}

function clamp(value: number, min: number, max: number): number {
    return Math.max(min, Math.min(max, value));
}

function componentMin(a: Readonly<IVec3Like>, b: Readonly<IVec3Like>): IVec3Like {
    return { x: Math.min(a.x, b.x), y: Math.min(a.y, b.y), z: Math.min(a.z, b.z) };
}

function componentMax(a: Readonly<IVec3Like>, b: Readonly<IVec3Like>): IVec3Like {
    return { x: Math.max(a.x, b.x), y: Math.max(a.y, b.y), z: Math.max(a.z, b.z) };
}

function midpointVec3(a: Readonly<IVec3Like>, b: Readonly<IVec3Like>): IVec3Like {
    return scaleVec3(addVec3(a, b), 0.5);
}

function expandAabb(aabb: IAabb3D, point: Readonly<IVec3Like>): IAabb3D {
    return {
        min: componentMin(aabb.min, point),
        max: componentMax(aabb.max, point),
    };
}

function intersectsAabb(a: IAabb3D, b: IAabb3D): boolean {
    return !(
        a.max.x < b.min.x ||
        a.min.x > b.max.x ||
        a.max.y < b.min.y ||
        a.min.y > b.max.y ||
        a.max.z < b.min.z ||
        a.min.z > b.max.z
    );
}

function makeMaterial(material?: Partial<IMaterial>): IMaterial {
    return {
        friction: material?.friction ?? DEFAULT_MATERIAL.friction,
        restitution: material?.restitution ?? DEFAULT_MATERIAL.restitution,
        density: material?.density ?? DEFAULT_MATERIAL.density,
        ...(material?.rollingFriction !== undefined
            ? { rollingFriction: material.rollingFriction }
            : {}),
        ...(material?.spinningFriction !== undefined
            ? { spinningFriction: material.spinningFriction }
            : {}),
    };
}

function makeFilter(filter?: ICollisionFilter3D): ICollisionFilter3D {
    return {
        categoryBits: filter?.categoryBits ?? DEFAULT_FILTER.categoryBits,
        maskBits: filter?.maskBits ?? DEFAULT_FILTER.maskBits,
        groupIndex: filter?.groupIndex ?? DEFAULT_FILTER.groupIndex,
    };
}

function supportsQueryFilter(shapeFilter: Readonly<ICollisionFilter3D>, filter?: IQueryFilter3D): boolean {
    if (!filter) return true;
    if (filter.groupIndex !== undefined && shapeFilter.groupIndex !== filter.groupIndex) {
        return false;
    }
    if (
        filter.categoryBits !== undefined &&
        (shapeFilter.categoryBits & filter.categoryBits) === 0
    ) {
        return false;
    }
    if (filter.maskBits !== undefined && (shapeFilter.maskBits & filter.maskBits) === 0) {
        return false;
    }
    return true;
}

function shouldShapeFiltersCollide(
    filterA: Readonly<ICollisionFilter3D>,
    filterB: Readonly<ICollisionFilter3D>
): boolean {
    if (filterA.groupIndex !== 0 && filterA.groupIndex === filterB.groupIndex) {
        return filterA.groupIndex > 0;
    }

    return (
        (filterA.maskBits & filterB.categoryBits) !== 0 &&
        (filterB.maskBits & filterA.categoryBits) !== 0
    );
}

function buildOrthonormalBasis(normal: Readonly<IVec3Like>): { tangent1: IVec3Like; tangent2: IVec3Like } {
    const tangentSeed =
        Math.abs(normal.x) < 0.57735
            ? { x: 1, y: 0, z: 0 }
            : Math.abs(normal.y) < 0.57735
              ? { x: 0, y: 1, z: 0 }
              : { x: 0, y: 0, z: 1 };
    const tangent1 = normalizeVec3(crossVec3(normal, tangentSeed));
    const tangent2 = normalizeVec3(crossVec3(normal, tangent1));
    return { tangent1, tangent2 };
}

function getAxisVector(axis: 0 | 1 | 2 | undefined): IVec3Like {
    switch (axis ?? 1) {
        case 0:
            return { x: 1, y: 0, z: 0 };
        case 2:
            return { x: 0, y: 0, z: 1 };
        default:
            return { x: 0, y: 1, z: 0 };
    }
}

function getBoxWorldExtents(
    halfExtents: Readonly<IVec3Like>,
    rotation: Readonly<IQuatLike>
): IVec3Like {
    const xx = rotation.x * rotation.x;
    const yy = rotation.y * rotation.y;
    const zz = rotation.z * rotation.z;
    const xy = rotation.x * rotation.y;
    const xz = rotation.x * rotation.z;
    const yz = rotation.y * rotation.z;
    const wx = rotation.w * rotation.x;
    const wy = rotation.w * rotation.y;
    const wz = rotation.w * rotation.z;

    const m00 = 1 - 2 * (yy + zz);
    const m01 = 2 * (xy - wz);
    const m02 = 2 * (xz + wy);
    const m10 = 2 * (xy + wz);
    const m11 = 1 - 2 * (xx + zz);
    const m12 = 2 * (yz - wx);
    const m20 = 2 * (xz - wy);
    const m21 = 2 * (yz + wx);
    const m22 = 1 - 2 * (xx + yy);

    return {
        x:
            Math.abs(m00) * halfExtents.x +
            Math.abs(m01) * halfExtents.y +
            Math.abs(m02) * halfExtents.z,
        y:
            Math.abs(m10) * halfExtents.x +
            Math.abs(m11) * halfExtents.y +
            Math.abs(m12) * halfExtents.z,
        z:
            Math.abs(m20) * halfExtents.x +
            Math.abs(m21) * halfExtents.y +
            Math.abs(m22) * halfExtents.z,
    };
}

function linePointDistanceSquared(
    point: Readonly<IVec3Like>,
    lineStart: Readonly<IVec3Like>,
    lineEnd: Readonly<IVec3Like>
): number {
    const line = subVec3(lineEnd, lineStart);
    const lineLengthSquared = lengthSquaredVec3(line);
    if (lineLengthSquared <= 1e-10) {
        return lengthSquaredVec3(subVec3(point, lineStart));
    }

    const t = clamp(dotVec3(subVec3(point, lineStart), line) / lineLengthSquared, 0, 1);
    const closestPoint = addVec3(lineStart, scaleVec3(line, t));
    return lengthSquaredVec3(subVec3(point, closestPoint));
}

function triangleNormal(
    a: Readonly<IVec3Like>,
    b: Readonly<IVec3Like>,
    c: Readonly<IVec3Like>
): IVec3Like {
    return normalizeVec3(crossVec3(subVec3(b, a), subVec3(c, a)));
}

function rayTriangleHit(
    origin: Readonly<IVec3Like>,
    direction: Readonly<IVec3Like>,
    a: Readonly<IVec3Like>,
    b: Readonly<IVec3Like>,
    c: Readonly<IVec3Like>,
    maxFraction: number
): IShapeRayHit3D | null {
    const edge1 = subVec3(b, a);
    const edge2 = subVec3(c, a);
    const p = crossVec3(direction, edge2);
    const determinant = dotVec3(edge1, p);

    if (Math.abs(determinant) <= 1e-10) {
        return null;
    }

    const inverseDeterminant = 1 / determinant;
    const t = subVec3(origin, a);
    const u = dotVec3(t, p) * inverseDeterminant;
    if (u < 0 || u > 1) {
        return null;
    }

    const q = crossVec3(t, edge1);
    const v = dotVec3(direction, q) * inverseDeterminant;
    if (v < 0 || u + v > 1) {
        return null;
    }

    const fraction = dotVec3(edge2, q) * inverseDeterminant;
    if (fraction < 0 || fraction > maxFraction) {
        return null;
    }

    const normal = triangleNormal(a, b, c);
    return {
        fraction,
        normal: dotVec3(normal, direction) > 0 ? scaleVec3(normal, -1) : normal,
    };
}

function getHeightFieldLocalVertex(
    def: Readonly<IHeightFieldShapeDef3D>,
    xIndex: number,
    zIndex: number
): IVec3Like {
    const halfWidth = (def.width - 1) * 0.5;
    const halfDepth = (def.depth - 1) * 0.5;
    return {
        x: (xIndex - halfWidth) * def.scaleX,
        y: def.heights[zIndex * def.width + xIndex] * def.scaleY,
        z: (zIndex - halfDepth) * def.scaleZ,
    };
}

function raySphereHit(
    origin: Readonly<IVec3Like>,
    direction: Readonly<IVec3Like>,
    center: Readonly<IVec3Like>,
    radius: number,
    maxFraction: number
): IShapeRayHit3D | null {
    const m = subVec3(origin, center);
    const a = dotVec3(direction, direction);
    const b = dotVec3(m, direction);
    const c = dotVec3(m, m) - radius * radius;

    if (c <= 0) {
        return { fraction: 0, normal: normalizeVec3(m) };
    }
    if (a <= 1e-10) {
        return null;
    }

    const discriminant = b * b - a * c;
    if (discriminant < 0) {
        return null;
    }

    const fraction = (-b - Math.sqrt(discriminant)) / a;
    if (fraction < 0 || fraction > maxFraction) {
        return null;
    }

    const hitPoint = addVec3(origin, scaleVec3(direction, fraction));
    return { fraction, normal: normalizeVec3(subVec3(hitPoint, center)) };
}

function rayAabbHit(
    origin: Readonly<IVec3Like>,
    direction: Readonly<IVec3Like>,
    min: Readonly<IVec3Like>,
    max: Readonly<IVec3Like>,
    maxFraction: number
): IShapeRayHit3D | null {
    let tMin = 0;
    let tMax = maxFraction;
    let normal: IVec3Like = { x: 0, y: 0, z: 0 };

    for (const axis of ['x', 'y', 'z'] as const) {
        const originComponent = origin[axis];
        const directionComponent = direction[axis];
        const minComponent = min[axis];
        const maxComponent = max[axis];

        if (Math.abs(directionComponent) <= 1e-10) {
            if (originComponent < minComponent || originComponent > maxComponent) {
                return null;
            }
            continue;
        }

        const inverseDirection = 1 / directionComponent;
        let t1 = (minComponent - originComponent) * inverseDirection;
        let t2 = (maxComponent - originComponent) * inverseDirection;
        let axisNormal: IVec3Like =
            axis === 'x'
                ? { x: -1, y: 0, z: 0 }
                : axis === 'y'
                  ? { x: 0, y: -1, z: 0 }
                  : { x: 0, y: 0, z: -1 };

        if (t1 > t2) {
            const swap = t1;
            t1 = t2;
            t2 = swap;
            axisNormal = scaleVec3(axisNormal, -1);
        }

        if (t1 > tMin) {
            tMin = t1;
            normal = axisNormal;
        }
        tMax = Math.min(tMax, t2);

        if (tMin > tMax) {
            return null;
        }
    }

    return { fraction: tMin, normal };
}

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
    private _nextContactId = 1 as ContactId;
    private _nextManifoldId = 1;
    private _contactManifolds = new Map<string, IResolvedContactManifold3D>();

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
            def: { ...def, vertices: def.vertices.map(cloneVec3), kind: SHAPE_TYPE_CONVEX_HULL },
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
                vertices: def.vertices.map(cloneVec3),
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
            { ...def, localAnchorA: cloneVec3(def.localAnchorA), localAnchorB: cloneVec3(def.localAnchorB), kind: CONSTRAINT_TYPE_FIXED }
        );
    }

    createHingeConstraint(def: IHingeConstraintDef3D): ConstraintId3D {
        return this._registerConstraint(
            this._constraintManager.createHinge(def),
            CONSTRAINT_TYPE_HINGE,
            {
                ...def,
                localAnchorA: cloneVec3(def.localAnchorA),
                localAnchorB: cloneVec3(def.localAnchorB),
                localAxisA: cloneVec3(def.localAxisA),
                localAxisB: cloneVec3(def.localAxisB),
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
                localAnchorA: cloneVec3(def.localAnchorA),
                localAnchorB: cloneVec3(def.localAnchorB),
                localAxisA: cloneVec3(def.localAxisA),
                kind: CONSTRAINT_TYPE_SLIDER,
            }
        );
    }

    createSpringConstraint(def: ISpringConstraintDef3D): ConstraintId3D {
        return this._registerConstraint(
            this._constraintManager.createSpring(def),
            CONSTRAINT_TYPE_SPRING,
            { ...def, localAnchorA: cloneVec3(def.localAnchorA), localAnchorB: cloneVec3(def.localAnchorB), kind: CONSTRAINT_TYPE_SPRING }
        );
    }

    createConeTwistConstraint(def: IConeTwistConstraintDef3D): ConstraintId3D {
        return this._registerConstraint(
            this._constraintManager.createConeTwist(def),
            CONSTRAINT_TYPE_CONE_TWIST,
            {
                ...def,
                localFrameA: {
                    position: cloneVec3(def.localFrameA.position),
                    rotation: cloneQuat(def.localFrameA.rotation),
                },
                localFrameB: {
                    position: cloneVec3(def.localFrameB.position),
                    rotation: cloneQuat(def.localFrameB.rotation),
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
                    position: cloneVec3(def.localFrameA.position),
                    rotation: cloneQuat(def.localFrameA.rotation),
                },
                localFrameB: {
                    position: cloneVec3(def.localFrameB.position),
                    rotation: cloneQuat(def.localFrameB.rotation),
                },
                linearLowerLimit: cloneVec3(def.linearLowerLimit),
                linearUpperLimit: cloneVec3(def.linearUpperLimit),
                angularLowerLimit: cloneVec3(def.angularLowerLimit),
                angularUpperLimit: cloneVec3(def.angularUpperLimit),
                ...(def.linearStiffness ? { linearStiffness: cloneVec3(def.linearStiffness) } : {}),
                ...(def.angularStiffness ? { angularStiffness: cloneVec3(def.angularStiffness) } : {}),
                ...(def.linearDamping ? { linearDamping: cloneVec3(def.linearDamping) } : {}),
                ...(def.angularDamping ? { angularDamping: cloneVec3(def.angularDamping) } : {}),
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
            if (!callback(result)) {
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
                point: addVec3(origin, scaleVec3(direction, hit.fraction)),
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
        const queryBounds = { min: cloneVec3(min), max: cloneVec3(max) };
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
            this._bodyManager.setPosition(bodyId, subVec3(position, newOrigin));
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
            contactCount: this._contactManifolds.size,
            proxyCount: this._shapeManager.shapeCount,
            islandCount: 0,
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

        if (this._profiler) {
            this._profiler.broadphaseTime = broadphaseTime;
            this._profiler.narrowphaseTime = narrowphaseTime;
            this._profiler.collisionTime = broadphaseTime + narrowphaseTime;
        }

        this._emitPreSolveEvents(previousManifolds, nextManifolds);

        const constraints = [...this._constraintDescriptors.values()].filter((constraint) => constraint.enabled);
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

        if (this._profiler) {
            this._profiler.solveVelocityTime = solveVelocityTime;
            this._profiler.solvePositionTime = solvePositionTime;
            this._profiler.solveTime = solveVelocityTime + solvePositionTime;
        }

        this._emitContactLifecycleEvents(previousManifolds, nextManifolds);
        this._contactManifolds = nextManifolds;
    }

    private _collectPotentialCollisionPairs(): IShapePairCandidate3D[] {
        const descriptors = [...this._shapeDescriptors.values()].sort((left, right) => left.id - right.id);
        const candidates: IShapePairCandidate3D[] = [];
        const aabbs = new Map<ShapeId3D, IAabb3D>();

        for (const descriptor of descriptors) {
            aabbs.set(descriptor.id, this._computeShapeAabb(descriptor));
        }

        for (let leftIndex = 0; leftIndex < descriptors.length; leftIndex += 1) {
            const descriptorA = descriptors[leftIndex];
            if (!this._bodyManager.isEnabled(descriptorA.bodyId)) {
                continue;
            }

            for (let rightIndex = leftIndex + 1; rightIndex < descriptors.length; rightIndex += 1) {
                const descriptorB = descriptors[rightIndex];
                if (descriptorA.bodyId === descriptorB.bodyId) {
                    continue;
                }
                if (!this._bodyManager.isEnabled(descriptorB.bodyId)) {
                    continue;
                }
                if (!shouldShapeFiltersCollide(descriptorA.filter, descriptorB.filter)) {
                    continue;
                }
                if (
                    this._collisionFilter &&
                    !this._collisionFilter.shouldCollide(descriptorA.id, descriptorB.id)
                ) {
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
        for (const descriptor of this._constraintDescriptors.values()) {
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
        const localPointA = inverseTransformPoint3D(
            collision.point,
            this._bodyManager.getPosition(pair.descriptorA.bodyId),
            this._bodyManager.getRotation(pair.descriptorA.bodyId)
        );
        const localPointB = inverseTransformPoint3D(
            collision.point,
            this._bodyManager.getPosition(pair.descriptorB.bodyId),
            this._bodyManager.getRotation(pair.descriptorB.bodyId)
        );

        const friction = Math.sqrt(
            pair.descriptorA.material.friction * pair.descriptorB.material.friction
        );
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
                    separation: -collision.penetration,
                },
            ],
            sensor: pair.descriptorA.isSensor || pair.descriptorB.isSensor,
            friction,
            restitution,
        };
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
        const centerA = this._getShapeWorldCenter(descriptorA);
        const centerB = this._getShapeWorldCenter(descriptorB);
        const delta = subVec3(centerB, centerA);
        const distance = lengthVec3(delta);
        const radiusSum =
            (descriptorA.def as Extract<SupportedShapeDef3D, { kind: typeof SHAPE_TYPE_SPHERE }>).radius +
            (descriptorB.def as Extract<SupportedShapeDef3D, { kind: typeof SHAPE_TYPE_SPHERE }>).radius;

        if (distance > radiusSum) {
            return null;
        }

        const normal =
            distance > PhysicsConstants.EPSILON ? scaleVec3(delta, 1 / distance) : { x: 1, y: 0, z: 0 };
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
        const sphereCenter = this._getShapeWorldCenter(sphereDescriptor);
        const boxBodyPosition = this._bodyManager.getPosition(boxDescriptor.bodyId);
        const boxBodyRotation = this._bodyManager.getRotation(boxDescriptor.bodyId);
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
            this._bodyManager.getLinearVelocity(sphereDescriptor.bodyId),
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

        const centerA = this._getShapeWorldCenter(descriptorA);
        const centerB = this._getShapeWorldCenter(descriptorB);
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

        const point = {
            x: (Math.max(aabbA.min.x, aabbB.min.x) + Math.min(aabbA.max.x, aabbB.max.x)) * 0.5,
            y: (Math.max(aabbA.min.y, aabbB.min.y) + Math.min(aabbA.max.y, aabbB.max.y)) * 0.5,
            z: (Math.max(aabbA.min.z, aabbB.min.z) + Math.min(aabbA.max.z, aabbB.max.z)) * 0.5,
        };

        return { normal, point, penetration };
    }

    private _solveContactVelocity(manifold: IResolvedContactManifold3D): void {
        for (const point of manifold.points) {
            const worldPoint = midpointVec3(
                this._localPointToWorld(manifold.bodyIdA, point.localPointA),
                this._localPointToWorld(manifold.bodyIdB, point.localPointB)
            );
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
            const normalImpulseMagnitude =
                (-(1 + restitution) * normalSpeed) / inverseMassSum;
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
            point.tangentImpulse1 =
                ((point.tangentImpulse1 + tangentImpulseMagnitude) as unknown) as Impulse;
            this._applySolveImpulse(manifold.bodyIdA, negateVec3(tangentImpulse), worldPoint);
            this._applySolveImpulse(manifold.bodyIdB, tangentImpulse, worldPoint);
        }
    }

    private _solveContactPosition(manifold: IResolvedContactManifold3D): void {
        for (const point of manifold.points) {
            const penetration = -point.separation;
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
                ((penetration - PhysicsConstants.ALLOWED_PENETRATION) * 0.7) / inverseMassSum;
            const correction = scaleVec3(manifold.normal, correctionMagnitude);

            if (inverseMassA > 0) {
                const positionA = this._bodyManager.getPosition(manifold.bodyIdA);
                this._bodyManager.setPosition(
                    manifold.bodyIdA,
                    subVec3(positionA, scaleVec3(correction, inverseMassA))
                );
            }
            if (inverseMassB > 0) {
                const positionB = this._bodyManager.getPosition(manifold.bodyIdB);
                this._bodyManager.setPosition(
                    manifold.bodyIdB,
                    addVec3(positionB, scaleVec3(correction, inverseMassB))
                );
            }

            point.separation = Math.min(0, point.separation + correctionMagnitude);
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
            this._bodyManager.setPosition(
                descriptor.def.bodyIdA,
                addVec3(
                    this._bodyManager.getPosition(descriptor.def.bodyIdA),
                    scaleVec3(correction, inverseMassA)
                )
            );
        }
        if (inverseMassB > 0) {
            this._bodyManager.setPosition(
                descriptor.def.bodyIdB,
                subVec3(
                    this._bodyManager.getPosition(descriptor.def.bodyIdB),
                    scaleVec3(correction, inverseMassB)
                )
            );
        }
    }

    private _getConstraintAnchorsAndError(
        descriptor: IConstraintDescriptor3D
    ): { anchorA: IVec3Like; anchorB: IVec3Like; direction: IVec3Like; error: number } | null {
        const anchorA = this._getConstraintAnchor(descriptor.def, true);
        const anchorB = this._getConstraintAnchor(descriptor.def, false);
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
                    this._bodyManager.getRotation(sliderDef.bodyIdA)
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
        if (!this._contactListener || !('onPreSolve' in this._contactListener)) {
            return;
        }

        const onPreSolve = (this._contactListener as { onPreSolve?: (event: unknown, oldManifold: IContactManifold3D) => void }).onPreSolve;
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
        const timestamp = Date.now();

        for (const [pairKey, manifold] of nextManifolds) {
            const previous = previousManifolds.get(pairKey);
            if (manifold.sensor) {
                if (previous) {
                    this._emitSensorEvent('onSensorStay', manifold, timestamp, 1);
                } else {
                    this._emitSensorEvent('onSensorEnter', manifold, timestamp, 0);
                }
                continue;
            }

            if (previous) {
                this._emitCollisionEvent('onCollisionStay', manifold, timestamp, 1);
            } else {
                this._emitCollisionEvent('onCollisionBegin', manifold, timestamp, 0);
            }

            const onPostSolve = (this._contactListener as {
                onPostSolve?: (
                    event: unknown,
                    impulse: { normal: Impulse; tangent1: Impulse; tangent2: Impulse }
                ) => void;
            } | null)?.onPostSolve;
            if (onPostSolve) {
                const normalImpulse = manifold.points.reduce(
                    (sum, point) => sum + point.normalImpulse,
                    0
                );
                const tangentImpulse1 = manifold.points.reduce(
                    (sum, point) => sum + point.tangentImpulse1,
                    0
                );
                const tangentImpulse2 = manifold.points.reduce(
                    (sum, point) => sum + point.tangentImpulse2,
                    0
                );
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
                this._emitSensorEvent('onSensorExit', manifold, timestamp, 2);
            } else {
                this._emitCollisionEnd(manifold, timestamp);
            }
        }
    }

    private _emitCollisionEvent(
        methodName: 'onCollisionBegin' | 'onCollisionStay',
        manifold: IResolvedContactManifold3D,
        timestamp: number,
        type: number
    ): void {
        const handler = (this._contactListener as Record<string, ((payload: unknown) => void) | undefined> | null)?.[methodName];
        if (!handler) {
            return;
        }

        handler(this._createCollisionPayload(manifold, type, timestamp));
    }

    private _emitCollisionEnd(manifold: IResolvedContactManifold3D, timestamp: number): void {
        const handler = (this._contactListener as {
            onCollisionEnd?: ((bodyIdA: BodyId3D, bodyIdB: BodyId3D) => void) | ((event: unknown) => void);
        } | null)?.onCollisionEnd;
        if (!handler) {
            return;
        }

        if (handler.length >= 2) {
            (handler as (bodyIdA: BodyId3D, bodyIdB: BodyId3D) => void)(
                manifold.bodyIdA,
                manifold.bodyIdB
            );
            return;
        }

        (handler as (event: unknown) => void)(this._createCollisionPayload(manifold, 2, timestamp));
    }

    private _emitSensorEvent(
        methodName: 'onSensorEnter' | 'onSensorStay' | 'onSensorExit',
        manifold: IResolvedContactManifold3D,
        timestamp: number,
        type: number
    ): void {
        const handler = (this._contactListener as Record<string, ((...args: unknown[]) => void) | undefined> | null)?.[methodName];
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
        return this._bodyManager.getBodyType(bodyId) === BODY_TYPE_DYNAMIC &&
            this._bodyManager.isEnabled(bodyId)
            ? this._bodyManager.getInverseMass(bodyId)
            : 0;
    }

    private _applySolveImpulse(bodyId: BodyId3D, impulse: Readonly<IVec3Like>, point: Readonly<IVec3Like>): void {
        if (this._bodyManager.getBodyType(bodyId) !== BODY_TYPE_DYNAMIC) {
            return;
        }

        this._bodyManager.applyImpulse(bodyId, impulse, point);
    }

    private _getWorldPointVelocity(bodyId: BodyId3D, point: Readonly<IVec3Like>): IVec3Like {
        const center = this._bodyManager.getPosition(bodyId);
        return addVec3(
            this._bodyManager.getLinearVelocity(bodyId),
            crossVec3(this._bodyManager.getAngularVelocity(bodyId), subVec3(point, center))
        );
    }

    private _localPointToWorld(bodyId: BodyId3D, localPoint: Readonly<IVec3Like>): IVec3Like {
        return transformPoint3D(
            localPoint,
            this._bodyManager.getPosition(bodyId),
            this._bodyManager.getRotation(bodyId)
        );
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
                    position: cloneVec3(bodyWorld._bodyManager.getPosition(bodyId)),
                    rotation: cloneQuat(bodyWorld._bodyManager.getRotation(bodyId)),
                };
            },
            get velocity() {
                return {
                    linear: cloneVec3(bodyWorld._bodyManager.getLinearVelocity(bodyId)),
                    angular: cloneVec3(bodyWorld._bodyManager.getAngularVelocity(bodyId)),
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
                return cloneVec3(bodyWorld._bodyManager.getPosition(bodyId));
            },
            setPosition(position) {
                bodyWorld._bodyManager.setPosition(bodyId, position);
            },
            getRotation() {
                return cloneQuat(bodyWorld._bodyManager.getRotation(bodyId));
            },
            setRotation(rotation) {
                bodyWorld._bodyManager.setRotation(bodyId, rotation);
            },
            getTransform() {
                return {
                    position: cloneVec3(bodyWorld._bodyManager.getPosition(bodyId)),
                    rotation: cloneQuat(bodyWorld._bodyManager.getRotation(bodyId)),
                };
            },
            setTransform(position, rotation) {
                bodyWorld._bodyManager.setPosition(bodyId, position);
                bodyWorld._bodyManager.setRotation(bodyId, rotation);
            },
            getLinearVelocity() {
                return cloneVec3(bodyWorld._bodyManager.getLinearVelocity(bodyId));
            },
            setLinearVelocity(velocity) {
                bodyWorld._bodyManager.setLinearVelocity(bodyId, velocity);
            },
            getAngularVelocity() {
                return cloneVec3(bodyWorld._bodyManager.getAngularVelocity(bodyId));
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
                return inverseRotateVec3(worldVector, bodyWorld._bodyManager.getRotation(bodyId));
            },
            getWorldVector(localVector) {
                return rotateVec3(localVector, bodyWorld._bodyManager.getRotation(bodyId));
            },
            getLinearVelocityAtPoint(point) {
                const relativePoint = subVec3(point, bodyWorld._bodyManager.getPosition(bodyId));
                return addVec3(
                    bodyWorld._bodyManager.getLinearVelocity(bodyId),
                    crossVec3(bodyWorld._bodyManager.getAngularVelocity(bodyId), relativePoint)
                );
            },
            getMass() {
                return bodyWorld._bodyManager.getMass(bodyId);
            },
            getInertiaTensor() {
                return cloneVec3(bodyWorld._bodyManager.getInertiaTensor(bodyId));
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
                return cloneVec3(bodyWorld._getBodyMassData(bodyId).center);
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
            mass,
            inverseMass: mass > 0 ? 1 / mass : 0,
            inertiaTensor: cloneVec3(inertiaTensor),
            inverseInertiaTensor: {
                x: inertiaTensor.x > 0 ? 1 / inertiaTensor.x : 0,
                y: inertiaTensor.y > 0 ? 1 / inertiaTensor.y : 0,
                z: inertiaTensor.z > 0 ? 1 / inertiaTensor.z : 0,
            },
            center: this._computeBodyMassData(bodyId).center,
        };
    }

    private _computeBodyMassData(bodyId: BodyId3D): IMassData3D {
        const shapes = this._shapeManager.getShapesForBody(bodyId);
        if (shapes.length === 0 || this._bodyManager.getBodyType(bodyId) !== BODY_TYPE_DYNAMIC) {
            return {
                mass: this._bodyManager.getMass(bodyId),
                inverseMass: this._bodyManager.getInverseMass(bodyId),
                inertiaTensor: cloneVec3(this._bodyManager.getInertiaTensor(bodyId)),
                inverseInertiaTensor: {
                    x: this._bodyManager.getInertiaTensor(bodyId).x > 0 ? 1 / this._bodyManager.getInertiaTensor(bodyId).x : 0,
                    y: this._bodyManager.getInertiaTensor(bodyId).y > 0 ? 1 / this._bodyManager.getInertiaTensor(bodyId).y : 0,
                    z: this._bodyManager.getInertiaTensor(bodyId).z > 0 ? 1 / this._bodyManager.getInertiaTensor(bodyId).z : 0,
                },
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
            center = addVec3(center, scaleVec3(massData.center, massData.mass));
        }

        if (totalMass <= 1e-10) {
            return {
                mass: 0,
                inverseMass: 0,
                inertiaTensor: { x: 0, y: 0, z: 0 },
                inverseInertiaTensor: { x: 0, y: 0, z: 0 },
                center: { x: 0, y: 0, z: 0 },
            };
        }

        center = scaleVec3(center, 1 / totalMass);

        let inertiaTensor = { x: 0, y: 0, z: 0 };
        for (const massData of shapeMassData) {
            const offset = subVec3(massData.center, center);
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
            mass: totalMass,
            inverseMass: totalMass > 0 ? 1 / totalMass : 0,
            inertiaTensor,
            inverseInertiaTensor: {
                x: inertiaTensor.x > 0 ? 1 / inertiaTensor.x : 0,
                y: inertiaTensor.y > 0 ? 1 / inertiaTensor.y : 0,
                z: inertiaTensor.z > 0 ? 1 / inertiaTensor.z : 0,
            },
            center,
        };
    }

    private _computeShapeMassData(descriptor: IShapeDescriptor3D, density: number): IMassData3D {
        const safeDensity = Math.max(0, density);
        switch (descriptor.type) {
            case SHAPE_TYPE_SPHERE: {
                const radius = descriptor.def.radius;
                const mass = ((4 / 3) * Math.PI * radius * radius * radius) * safeDensity;
                const inertia = (2 / 5) * mass * radius * radius;
                return {
                    mass,
                    inverseMass: mass > 0 ? 1 / mass : 0,
                    inertiaTensor: { x: inertia, y: inertia, z: inertia },
                    inverseInertiaTensor: {
                        x: inertia > 0 ? 1 / inertia : 0,
                        y: inertia > 0 ? 1 / inertia : 0,
                        z: inertia > 0 ? 1 / inertia : 0,
                    },
                    center: cloneVec3(descriptor.def.center),
                };
            }
            case SHAPE_TYPE_BOX: {
                const halfExtents = descriptor.def.halfExtents;
                const fullExtents = scaleVec3(halfExtents, 2);
                const mass = fullExtents.x * fullExtents.y * fullExtents.z * safeDensity;
                const inertiaTensor = {
                    x: (mass * (fullExtents.y * fullExtents.y + fullExtents.z * fullExtents.z)) / 12,
                    y: (mass * (fullExtents.x * fullExtents.x + fullExtents.z * fullExtents.z)) / 12,
                    z: (mass * (fullExtents.x * fullExtents.x + fullExtents.y * fullExtents.y)) / 12,
                };
                return {
                    mass,
                    inverseMass: mass > 0 ? 1 / mass : 0,
                    inertiaTensor,
                    inverseInertiaTensor: {
                        x: inertiaTensor.x > 0 ? 1 / inertiaTensor.x : 0,
                        y: inertiaTensor.y > 0 ? 1 / inertiaTensor.y : 0,
                        z: inertiaTensor.z > 0 ? 1 / inertiaTensor.z : 0,
                    },
                    center: cloneVec3(descriptor.def.center),
                };
            }
            case SHAPE_TYPE_CAPSULE: {
                const segment = subVec3(descriptor.def.p2, descriptor.def.p1);
                const segmentLength = lengthVec3(segment);
                const radius = descriptor.def.radius;
                const cylinderMass = Math.PI * radius * radius * segmentLength * safeDensity;
                const sphereMass = ((4 / 3) * Math.PI * radius * radius * radius) * safeDensity;
                const mass = cylinderMass + sphereMass;
                const inertia = radius * radius * mass;
                return {
                    mass,
                    inverseMass: mass > 0 ? 1 / mass : 0,
                    inertiaTensor: { x: inertia, y: inertia, z: inertia },
                    inverseInertiaTensor: {
                        x: inertia > 0 ? 1 / inertia : 0,
                        y: inertia > 0 ? 1 / inertia : 0,
                        z: inertia > 0 ? 1 / inertia : 0,
                    },
                    center: scaleVec3(addVec3(descriptor.def.p1, descriptor.def.p2), 0.5),
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
                    mass,
                    inverseMass: mass > 0 ? 1 / mass : 0,
                    inertiaTensor,
                    inverseInertiaTensor: {
                        x: inertiaTensor.x > 0 ? 1 / inertiaTensor.x : 0,
                        y: inertiaTensor.y > 0 ? 1 / inertiaTensor.y : 0,
                        z: inertiaTensor.z > 0 ? 1 / inertiaTensor.z : 0,
                    },
                    center: cloneVec3(descriptor.def.center),
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
                    mass,
                    inverseMass: mass > 0 ? 1 / mass : 0,
                    inertiaTensor,
                    inverseInertiaTensor: {
                        x: inertiaTensor.x > 0 ? 1 / inertiaTensor.x : 0,
                        y: inertiaTensor.y > 0 ? 1 / inertiaTensor.y : 0,
                        z: inertiaTensor.z > 0 ? 1 / inertiaTensor.z : 0,
                    },
                    center: cloneVec3(descriptor.def.center),
                };
            }
            case SHAPE_TYPE_CONVEX_HULL: {
                const bounds = this._computeLocalConvexBounds(descriptor.def.vertices);
                const fullExtents = subVec3(bounds.max, bounds.min);
                const mass = fullExtents.x * fullExtents.y * fullExtents.z * safeDensity;
                const inertiaTensor = {
                    x: (mass * (fullExtents.y * fullExtents.y + fullExtents.z * fullExtents.z)) / 12,
                    y: (mass * (fullExtents.x * fullExtents.x + fullExtents.z * fullExtents.z)) / 12,
                    z: (mass * (fullExtents.x * fullExtents.x + fullExtents.y * fullExtents.y)) / 12,
                };
                return {
                    mass,
                    inverseMass: mass > 0 ? 1 / mass : 0,
                    inertiaTensor,
                    inverseInertiaTensor: {
                        x: inertiaTensor.x > 0 ? 1 / inertiaTensor.x : 0,
                        y: inertiaTensor.y > 0 ? 1 / inertiaTensor.y : 0,
                        z: inertiaTensor.z > 0 ? 1 / inertiaTensor.z : 0,
                    },
                    center: scaleVec3(addVec3(bounds.min, bounds.max), 0.5),
                };
            }
            case SHAPE_TYPE_TRIANGLE_MESH: {
                const bounds = this._computeLocalConvexBounds(descriptor.def.vertices);
                const fullExtents = subVec3(bounds.max, bounds.min);
                const mass = fullExtents.x * fullExtents.y * fullExtents.z * safeDensity;
                const inertiaTensor = {
                    x: (mass * (fullExtents.y * fullExtents.y + fullExtents.z * fullExtents.z)) / 12,
                    y: (mass * (fullExtents.x * fullExtents.x + fullExtents.z * fullExtents.z)) / 12,
                    z: (mass * (fullExtents.x * fullExtents.x + fullExtents.y * fullExtents.y)) / 12,
                };
                return {
                    mass,
                    inverseMass: mass > 0 ? 1 / mass : 0,
                    inertiaTensor,
                    inverseInertiaTensor: {
                        x: inertiaTensor.x > 0 ? 1 / inertiaTensor.x : 0,
                        y: inertiaTensor.y > 0 ? 1 / inertiaTensor.y : 0,
                        z: inertiaTensor.z > 0 ? 1 / inertiaTensor.z : 0,
                    },
                    center: scaleVec3(addVec3(bounds.min, bounds.max), 0.5),
                };
            }
            case SHAPE_TYPE_HEIGHTFIELD: {
                const bounds = this._computeLocalHeightFieldBounds(descriptor.def);
                const fullExtents = subVec3(bounds.max, bounds.min);
                const mass = fullExtents.x * fullExtents.y * fullExtents.z * safeDensity;
                const inertiaTensor = {
                    x: (mass * (fullExtents.y * fullExtents.y + fullExtents.z * fullExtents.z)) / 12,
                    y: (mass * (fullExtents.x * fullExtents.x + fullExtents.z * fullExtents.z)) / 12,
                    z: (mass * (fullExtents.x * fullExtents.x + fullExtents.y * fullExtents.y)) / 12,
                };
                return {
                    mass,
                    inverseMass: mass > 0 ? 1 / mass : 0,
                    inertiaTensor,
                    inverseInertiaTensor: {
                        x: inertiaTensor.x > 0 ? 1 / inertiaTensor.x : 0,
                        y: inertiaTensor.y > 0 ? 1 / inertiaTensor.y : 0,
                        z: inertiaTensor.z > 0 ? 1 / inertiaTensor.z : 0,
                    },
                    center: scaleVec3(addVec3(bounds.min, bounds.max), 0.5),
                };
            }
            default:
                return {
                    mass: 0,
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
        switch (descriptor.type) {
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
                const worldRotation = multiplyQuat(rotation, descriptor.def.rotation ?? IDENTITY_ROTATION);
                const extents = getBoxWorldExtents(descriptor.def.halfExtents, worldRotation);
                return {
                    min: subVec3(center, extents),
                    max: addVec3(center, extents),
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
                const localHalfExtents =
                    axis === 0
                        ? { x: descriptor.def.height * 0.5, y: descriptor.def.radius, z: descriptor.def.radius }
                        : axis === 2
                          ? { x: descriptor.def.radius, y: descriptor.def.radius, z: descriptor.def.height * 0.5 }
                          : { x: descriptor.def.radius, y: descriptor.def.height * 0.5, z: descriptor.def.radius };
                const extents = getBoxWorldExtents(localHalfExtents, rotation);
                return {
                    min: subVec3(center, extents),
                    max: addVec3(center, extents),
                };
            }
            case SHAPE_TYPE_CONE: {
                const center = transformPoint3D(descriptor.def.center, position, rotation);
                const axis = descriptor.def.axis ?? 1;
                const localHalfExtents =
                    axis === 0
                        ? { x: descriptor.def.height * 0.5, y: descriptor.def.radius, z: descriptor.def.radius }
                        : axis === 2
                          ? { x: descriptor.def.radius, y: descriptor.def.radius, z: descriptor.def.height * 0.5 }
                          : { x: descriptor.def.radius, y: descriptor.def.height * 0.5, z: descriptor.def.radius };
                const extents = getBoxWorldExtents(localHalfExtents, rotation);
                return {
                    min: subVec3(center, extents),
                    max: addVec3(center, extents),
                };
            }
            case SHAPE_TYPE_CONVEX_HULL: {
                let bounds: IAabb3D | null = null;
                for (const vertex of descriptor.def.vertices) {
                    const worldVertex = transformPoint3D(vertex, position, rotation);
                    bounds = bounds
                        ? expandAabb(bounds, worldVertex)
                        : { min: cloneVec3(worldVertex), max: cloneVec3(worldVertex) };
                }
                return bounds ?? { min: cloneVec3(position), max: cloneVec3(position) };
            }
            case SHAPE_TYPE_TRIANGLE_MESH: {
                let bounds: IAabb3D | null = null;
                for (const vertex of descriptor.def.vertices) {
                    const worldVertex = transformPoint3D(vertex, position, rotation);
                    bounds = bounds
                        ? expandAabb(bounds, worldVertex)
                        : { min: cloneVec3(worldVertex), max: cloneVec3(worldVertex) };
                }
                return bounds ?? { min: cloneVec3(position), max: cloneVec3(position) };
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
                        : { min: cloneVec3(worldCorner), max: cloneVec3(worldCorner) };
                }
                return bounds ?? { min: cloneVec3(position), max: cloneVec3(position) };
            }
            default:
                return { min: cloneVec3(position), max: cloneVec3(position) };
        }
    }

    private _testPointShape(descriptor: IShapeDescriptor3D, point: Readonly<IVec3Like>): boolean {
        const position = this._bodyManager.getPosition(descriptor.bodyId);
        const rotation = this._bodyManager.getRotation(descriptor.bodyId);

        switch (descriptor.type) {
            case SHAPE_TYPE_SPHERE: {
                const center = transformPoint3D(descriptor.def.center, position, rotation);
                return lengthSquaredVec3(subVec3(point, center)) <= descriptor.def.radius ** 2;
            }
            case SHAPE_TYPE_BOX: {
                const bodyLocal = inverseTransformPoint3D(point, position, rotation);
                const centered = subVec3(bodyLocal, descriptor.def.center);
                const localRotation = descriptor.def.rotation ?? IDENTITY_ROTATION;
                const localPoint = inverseRotateVec3(centered, localRotation);
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
                const centered = subVec3(localPoint, descriptor.def.center);
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
                const centered = subVec3(localPoint, descriptor.def.center);
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

        switch (descriptor.type) {
            case SHAPE_TYPE_SPHERE: {
                const center = transformPoint3D(descriptor.def.center, position, rotation);
                return raySphereHit(origin, direction, center, descriptor.def.radius, maxFraction);
            }
            case SHAPE_TYPE_BOX: {
                const worldRotation = multiplyQuat(rotation, descriptor.def.rotation ?? IDENTITY_ROTATION);
                const center = transformPoint3D(descriptor.def.center, position, rotation);
                const localOrigin = inverseTransformPoint3D(origin, center, worldRotation);
                const localDirection = inverseRotateVec3(direction, worldRotation);
                const hit = rayAabbHit(
                    localOrigin,
                    localDirection,
                    scaleVec3(descriptor.def.halfExtents, -1),
                    descriptor.def.halfExtents,
                    maxFraction
                );
                if (!hit) {
                    return null;
                }
                return { fraction: hit.fraction, normal: rotateVec3(hit.normal, worldRotation) };
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
        switch (descriptor.type) {
            case SHAPE_TYPE_SPHERE:
            case SHAPE_TYPE_BOX:
            case SHAPE_TYPE_CYLINDER:
            case SHAPE_TYPE_CONE:
                return transformPoint3D(descriptor.def.center, position, rotation);
            case SHAPE_TYPE_CAPSULE:
                return transformPoint3D(scaleVec3(addVec3(descriptor.def.p1, descriptor.def.p2), 0.5), position, rotation);
            case SHAPE_TYPE_CONVEX_HULL: {
                const bounds = this._computeLocalConvexBounds(descriptor.def.vertices);
                return transformPoint3D(scaleVec3(addVec3(bounds.min, bounds.max), 0.5), position, rotation);
            }
            case SHAPE_TYPE_TRIANGLE_MESH: {
                const bounds = this._computeLocalConvexBounds(descriptor.def.vertices);
                return transformPoint3D(scaleVec3(addVec3(bounds.min, bounds.max), 0.5), position, rotation);
            }
            case SHAPE_TYPE_HEIGHTFIELD: {
                const bounds = this._computeLocalHeightFieldBounds(descriptor.def);
                return transformPoint3D(scaleVec3(addVec3(bounds.min, bounds.max), 0.5), position, rotation);
            }
            default:
                return cloneVec3(position);
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
                : { min: cloneVec3(vertex), max: cloneVec3(vertex) };
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
                    : { min: cloneVec3(vertex), max: cloneVec3(vertex) };
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