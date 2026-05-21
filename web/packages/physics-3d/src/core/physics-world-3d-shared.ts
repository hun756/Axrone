import type { IQuatLike, IVec3Like } from '@axrone/numeric';
import type {
    ContactId,
    Density,
    Friction,
    IContactManifold3D,
    IContactPoint3D,
    Impulse,
    IMaterial,
    ManifoldId,
    Restitution,
} from '../types';
import type {
    BodyId3D,
    ConstraintId3D,
    IBoxShapeDef3D,
    ICapsuleShapeDef3D,
    ICollisionFilter3D,
    IConeShapeDef3D,
    IConeTwistConstraintDef3D,
    IConvexHullShapeDef3D,
    ICylinderShapeDef3D,
    IFixedConstraintDef3D,
    IGenericConstraintDef3D,
    IHeightFieldShapeDef3D,
    IHingeConstraintDef3D,
    IQueryFilter3D,
    ISliderConstraintDef3D,
    ISphereShapeDef3D,
    ISpringConstraintDef3D,
    ITriangleMeshShapeDef3D,
    ShapeId3D,
} from '../types/physics-3d';

export const BODY_TYPE_STATIC = 0;
export const BODY_TYPE_DYNAMIC = 2;

export const SHAPE_TYPE_CAPSULE = 1;
export const SHAPE_TYPE_BOX = 3;
export const SHAPE_TYPE_SPHERE = 5;
export const SHAPE_TYPE_CYLINDER = 6;
export const SHAPE_TYPE_CONE = 7;
export const SHAPE_TYPE_CONVEX_HULL = 8;
export const SHAPE_TYPE_TRIANGLE_MESH = 9;
export const SHAPE_TYPE_HEIGHTFIELD = 10;

export const CONSTRAINT_TYPE_FIXED = 0;
export const CONSTRAINT_TYPE_HINGE = 2;
export const CONSTRAINT_TYPE_SLIDER = 3;
export const CONSTRAINT_TYPE_CONE_TWIST = 4;
export const CONSTRAINT_TYPE_GENERIC = 5;
export const CONSTRAINT_TYPE_SPRING = 6;

export type SupportedShapeDef3D =
    | ({ readonly kind: typeof SHAPE_TYPE_SPHERE } & ISphereShapeDef3D)
    | ({ readonly kind: typeof SHAPE_TYPE_BOX } & IBoxShapeDef3D)
    | ({ readonly kind: typeof SHAPE_TYPE_CAPSULE } & ICapsuleShapeDef3D)
    | ({ readonly kind: typeof SHAPE_TYPE_CYLINDER } & ICylinderShapeDef3D)
    | ({ readonly kind: typeof SHAPE_TYPE_CONE } & IConeShapeDef3D)
    | ({ readonly kind: typeof SHAPE_TYPE_CONVEX_HULL } & IConvexHullShapeDef3D)
    | ({ readonly kind: typeof SHAPE_TYPE_TRIANGLE_MESH } & ITriangleMeshShapeDef3D)
    | ({ readonly kind: typeof SHAPE_TYPE_HEIGHTFIELD } & IHeightFieldShapeDef3D);

export type SupportedConstraintDef3D =
    | ({ readonly kind: typeof CONSTRAINT_TYPE_FIXED } & IFixedConstraintDef3D)
    | ({ readonly kind: typeof CONSTRAINT_TYPE_HINGE } & IHingeConstraintDef3D)
    | ({ readonly kind: typeof CONSTRAINT_TYPE_SLIDER } & ISliderConstraintDef3D)
    | ({ readonly kind: typeof CONSTRAINT_TYPE_SPRING } & ISpringConstraintDef3D)
    | ({ readonly kind: typeof CONSTRAINT_TYPE_CONE_TWIST } & IConeTwistConstraintDef3D)
    | ({ readonly kind: typeof CONSTRAINT_TYPE_GENERIC } & IGenericConstraintDef3D);

export interface IShapeDescriptor3D {
    readonly id: ShapeId3D;
    readonly bodyId: BodyId3D;
    readonly type: number;
    readonly def: SupportedShapeDef3D;
    material: IMaterial;
    isSensor: boolean;
    filter: ICollisionFilter3D;
    userData?: unknown;
}

export interface IConstraintDescriptor3D {
    readonly id: ConstraintId3D;
    readonly type: number;
    readonly def: SupportedConstraintDef3D;
    enabled: boolean;
    readonly collideConnected: boolean;
    userData?: unknown;
}

export interface IShapeOptions3D {
    readonly isSensor?: boolean;
    readonly userData?: unknown;
}

export interface IAabb3D {
    readonly min: IVec3Like;
    readonly max: IVec3Like;
}

export interface IShapeRayHit3D {
    readonly fraction: number;
    readonly normal: IVec3Like;
}

export interface IMutableContactPoint3D extends IContactPoint3D {
    id: ContactId;
    localPointA: IVec3Like;
    localPointB: IVec3Like;
    normalImpulse: Impulse;
    tangentImpulse1: Impulse;
    tangentImpulse2: Impulse;
    separation: number;
}

export interface IMutableContactManifold3D extends IContactManifold3D {
    id: ManifoldId;
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

export interface IResolvedContactManifold3D extends IMutableContactManifold3D {
    readonly pairKey: string;
    readonly descriptorA: IShapeDescriptor3D;
    readonly descriptorB: IShapeDescriptor3D;
    readonly sensor: boolean;
    readonly friction: number;
    readonly restitution: number;
}

export interface IShapePairCandidate3D {
    readonly descriptorA: IShapeDescriptor3D;
    readonly descriptorB: IShapeDescriptor3D;
    readonly aabbA: IAabb3D;
    readonly aabbB: IAabb3D;
    readonly pairKey: string;
}

export const IDENTITY_ROTATION: IQuatLike = { x: 0, y: 0, z: 0, w: 1 };
export const DEFAULT_MATERIAL: IMaterial = {
    friction: 0.4 as unknown as Friction,
    restitution: 0 as unknown as Restitution,
    density: 1 as unknown as Density,
};
export const DEFAULT_FILTER: ICollisionFilter3D = { categoryBits: 1, maskBits: 0xffff, groupIndex: 0 };

export function cloneVec3(vector: Readonly<IVec3Like>): IVec3Like {
    return { x: vector.x, y: vector.y, z: vector.z };
}

export function cloneQuat(rotation: Readonly<IQuatLike>): IQuatLike {
    return { x: rotation.x, y: rotation.y, z: rotation.z, w: rotation.w };
}

export function addVec3(a: Readonly<IVec3Like>, b: Readonly<IVec3Like>): IVec3Like {
    return { x: a.x + b.x, y: a.y + b.y, z: a.z + b.z };
}

export function subVec3(a: Readonly<IVec3Like>, b: Readonly<IVec3Like>): IVec3Like {
    return { x: a.x - b.x, y: a.y - b.y, z: a.z - b.z };
}

export function scaleVec3(vector: Readonly<IVec3Like>, scalar: number): IVec3Like {
    return { x: vector.x * scalar, y: vector.y * scalar, z: vector.z * scalar };
}

export function negateVec3(vector: Readonly<IVec3Like>): IVec3Like {
    return { x: -vector.x, y: -vector.y, z: -vector.z };
}

export function dotVec3(a: Readonly<IVec3Like>, b: Readonly<IVec3Like>): number {
    return a.x * b.x + a.y * b.y + a.z * b.z;
}

export function crossVec3(a: Readonly<IVec3Like>, b: Readonly<IVec3Like>): IVec3Like {
    return {
        x: a.y * b.z - a.z * b.y,
        y: a.z * b.x - a.x * b.z,
        z: a.x * b.y - a.y * b.x,
    };
}

export function lengthSquaredVec3(vector: Readonly<IVec3Like>): number {
    return dotVec3(vector, vector);
}

export function lengthVec3(vector: Readonly<IVec3Like>): number {
    return Math.sqrt(lengthSquaredVec3(vector));
}

export function normalizeVec3(vector: Readonly<IVec3Like>): IVec3Like {
    const length = lengthVec3(vector);
    if (length <= 1e-10) {
        return { x: 0, y: 0, z: 0 };
    }

    const inverseLength = 1 / length;
    return scaleVec3(vector, inverseLength);
}

export function conjugateQuat(rotation: Readonly<IQuatLike>): IQuatLike {
    return { x: -rotation.x, y: -rotation.y, z: -rotation.z, w: rotation.w };
}

export function multiplyQuat(a: Readonly<IQuatLike>, b: Readonly<IQuatLike>): IQuatLike {
    return {
        x: a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
        y: a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
        z: a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
        w: a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z,
    };
}

export function rotateVec3(vector: Readonly<IVec3Like>, rotation: Readonly<IQuatLike>): IVec3Like {
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

export function inverseRotateVec3(vector: Readonly<IVec3Like>, rotation: Readonly<IQuatLike>): IVec3Like {
    return rotateVec3(vector, conjugateQuat(rotation));
}

export function transformPoint3D(
    point: Readonly<IVec3Like>,
    position: Readonly<IVec3Like>,
    rotation: Readonly<IQuatLike>
): IVec3Like {
    return addVec3(position, rotateVec3(point, rotation));
}

export function inverseTransformPoint3D(
    point: Readonly<IVec3Like>,
    position: Readonly<IVec3Like>,
    rotation: Readonly<IQuatLike>
): IVec3Like {
    return inverseRotateVec3(subVec3(point, position), rotation);
}

export function clamp(value: number, min: number, max: number): number {
    return Math.max(min, Math.min(max, value));
}

export function componentMin(a: Readonly<IVec3Like>, b: Readonly<IVec3Like>): IVec3Like {
    return { x: Math.min(a.x, b.x), y: Math.min(a.y, b.y), z: Math.min(a.z, b.z) };
}

export function componentMax(a: Readonly<IVec3Like>, b: Readonly<IVec3Like>): IVec3Like {
    return { x: Math.max(a.x, b.x), y: Math.max(a.y, b.y), z: Math.max(a.z, b.z) };
}

export function midpointVec3(a: Readonly<IVec3Like>, b: Readonly<IVec3Like>): IVec3Like {
    return scaleVec3(addVec3(a, b), 0.5);
}

export function expandAabb(aabb: IAabb3D, point: Readonly<IVec3Like>): IAabb3D {
    return {
        min: componentMin(aabb.min, point),
        max: componentMax(aabb.max, point),
    };
}

export function intersectsAabb(a: IAabb3D, b: IAabb3D): boolean {
    return !(
        a.max.x < b.min.x ||
        a.min.x > b.max.x ||
        a.max.y < b.min.y ||
        a.min.y > b.max.y ||
        a.max.z < b.min.z ||
        a.min.z > b.max.z
    );
}

export function makeMaterial(material?: Partial<IMaterial>): IMaterial {
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

export function makeFilter(filter?: ICollisionFilter3D): ICollisionFilter3D {
    return {
        categoryBits: filter?.categoryBits ?? DEFAULT_FILTER.categoryBits,
        maskBits: filter?.maskBits ?? DEFAULT_FILTER.maskBits,
        groupIndex: filter?.groupIndex ?? DEFAULT_FILTER.groupIndex,
    };
}

export function supportsQueryFilter(shapeFilter: Readonly<ICollisionFilter3D>, filter?: IQueryFilter3D): boolean {
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

export function shouldShapeFiltersCollide(
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

export function buildOrthonormalBasis(normal: Readonly<IVec3Like>): { tangent1: IVec3Like; tangent2: IVec3Like } {
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

export function getAxisVector(axis: 0 | 1 | 2 | undefined): IVec3Like {
    switch (axis ?? 1) {
        case 0:
            return { x: 1, y: 0, z: 0 };
        case 2:
            return { x: 0, y: 0, z: 1 };
        default:
            return { x: 0, y: 1, z: 0 };
    }
}

export function getBoxWorldExtents(
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

export function linePointDistanceSquared(
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

export function triangleNormal(
    a: Readonly<IVec3Like>,
    b: Readonly<IVec3Like>,
    c: Readonly<IVec3Like>
): IVec3Like {
    return normalizeVec3(crossVec3(subVec3(b, a), subVec3(c, a)));
}

export function rayTriangleHit(
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

export function getHeightFieldLocalVertex(
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

export function raySphereHit(
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

export function rayAabbHit(
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