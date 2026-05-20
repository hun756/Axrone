import type { IVec2Like } from '@axrone/numeric';
import type {
    BodyId,
    ShapeId,
    ICircleShapeDef,
    IBoxShapeDef2D,
    IPolygonShapeDef,
    ICapsuleShapeDef2D,
    ISegmentShapeDef,
    IShape2D,
    IRaycastResult2D,
    IQueryFilter,
    Density,
    IMassData2D,
    IMaterial,
    ShapeType,
} from '../types';
import { BodyType, ShapeType as ShapeTypeValue } from '../types';

import { BodyManager2D } from './body-manager';
import { ShapeManager2D } from './shape-manager';
import { Raycaster2D } from './continuous-collision';
import {
    buildBoxVertices,
    cloneMaterial,
    cloneVec2,
    computePolygonCentroid,
    computePolygonMassData,
    distanceSquared,
    distanceSquaredToSegment,
    GEOMETRY_EPSILON,
    getBodyWorldCenter,
    IAabb2D,
    IShapeDescriptor2D,
    IShapeFilter2D,
    intersectsAabb,
    inverseRotateVec2,
    lengthSquared,
    normalizeBounds,
    POINT_QUERY_EPSILON,
    pointInPolygon,
    raycastSegment,
    subtractVec2,
    toShapeFilter,
    toShapeMaterial,
    transformPoint2D,
} from './physics-world-2d-helpers';

export class PhysicsWorld2DShapeStore {
    private readonly _descriptors = new Map<ShapeId, IShapeDescriptor2D>();
    private readonly _views = new Map<ShapeId, IShape2D>();

    constructor(
        private readonly _bodyManager: BodyManager2D,
        private readonly _shapeManager: ShapeManager2D
    ) {}

    get size(): number {
        return this._descriptors.size;
    }

    clear(): void {
        this._descriptors.clear();
        this._views.clear();
    }

    entries(): IterableIterator<[ShapeId, IShapeDescriptor2D]> {
        return this._descriptors.entries();
    }

    hasShape(shapeId: ShapeId): boolean {
        return this._descriptors.has(shapeId);
    }

    getDescriptor(shapeId: ShapeId): IShapeDescriptor2D | null {
        return this._descriptors.get(shapeId) ?? null;
    }

    registerCircle(shapeId: ShapeId, bodyId: BodyId, def: ICircleShapeDef): void {
        this._descriptors.set(shapeId, {
            type: ShapeTypeValue.Circle,
            bodyId,
            material: toShapeMaterial(def),
            isSensor: def.isSensor ?? false,
            filter: toShapeFilter(def),
            userData: def.userData,
            center: cloneVec2(def.center ?? def.offset ?? { x: 0, y: 0 }),
            radius: def.radius,
            halfWidth: null,
            halfHeight: null,
            rotation: null,
            length: null,
            vertices: null,
            start: null,
            end: null,
        });
    }

    registerBox(shapeId: ShapeId, bodyId: BodyId, def: IBoxShapeDef2D): void {
        this._descriptors.set(shapeId, {
            type: ShapeTypeValue.Box,
            bodyId,
            material: toShapeMaterial(def),
            isSensor: def.isSensor ?? false,
            filter: toShapeFilter(def),
            userData: def.userData,
            center: cloneVec2(def.center ?? def.offset ?? { x: 0, y: 0 }),
            radius: null,
            halfWidth: def.halfWidth ?? (def.width !== undefined ? def.width / 2 : 0),
            halfHeight: def.halfHeight ?? (def.height !== undefined ? def.height / 2 : 0),
            rotation: def.rotation ?? 0,
            length: null,
            vertices: null,
            start: null,
            end: null,
        });
    }

    registerPolygon(shapeId: ShapeId, bodyId: BodyId, def: IPolygonShapeDef): void {
        this._descriptors.set(shapeId, {
            type: ShapeTypeValue.Polygon,
            bodyId,
            material: toShapeMaterial(def),
            isSensor: def.isSensor ?? false,
            filter: toShapeFilter(def),
            userData: def.userData,
            center: null,
            radius: null,
            halfWidth: null,
            halfHeight: null,
            rotation: null,
            length: null,
            vertices: def.vertices.map(cloneVec2),
            start: null,
            end: null,
        });
    }

    registerCapsule(shapeId: ShapeId, bodyId: BodyId, def: ICapsuleShapeDef2D): void {
        this._descriptors.set(shapeId, {
            type: ShapeTypeValue.Capsule,
            bodyId,
            material: toShapeMaterial(def),
            isSensor: def.isSensor ?? false,
            filter: toShapeFilter(def),
            userData: def.userData,
            center: cloneVec2(def.center ?? def.offset ?? { x: 0, y: 0 }),
            radius: def.radius,
            halfWidth: null,
            halfHeight: null,
            rotation: def.rotation ?? 0,
            length: def.length,
            vertices: null,
            start: null,
            end: null,
        });
    }

    registerSegment(shapeId: ShapeId, bodyId: BodyId, def: ISegmentShapeDef): void {
        this._descriptors.set(shapeId, {
            type: ShapeTypeValue.Segment,
            bodyId,
            material: toShapeMaterial(def),
            isSensor: def.isSensor ?? false,
            filter: toShapeFilter(def),
            userData: def.userData,
            center: null,
            radius: null,
            halfWidth: null,
            halfHeight: null,
            rotation: null,
            length: null,
            vertices: null,
            start: cloneVec2(def.start),
            end: cloneVec2(def.end),
        });
    }

    removeShape(shapeId: ShapeId): IShapeDescriptor2D | null {
        this._views.delete(shapeId);
        const descriptor = this._descriptors.get(shapeId) ?? null;
        this._descriptors.delete(shapeId);
        return descriptor;
    }

    getShapeView(shapeId: ShapeId): IShape2D | null {
        const descriptor = this._descriptors.get(shapeId);
        if (!descriptor) {
            return null;
        }

        const existing = this._views.get(shapeId);
        if (existing) {
            return existing;
        }

        const store = this;
        const shape: IShape2D = {
            get id(): ShapeId {
                return shapeId;
            },
            get bodyId(): BodyId {
                return store._descriptors.get(shapeId)!.bodyId;
            },
            get type(): ShapeType {
                return store._descriptors.get(shapeId)!.type as ShapeType;
            },
            get material(): IMaterial {
                return cloneMaterial(store._descriptors.get(shapeId)!.material);
            },
            get isSensor(): boolean {
                return store._descriptors.get(shapeId)!.isSensor;
            },
            get filter() {
                const filter = store._descriptors.get(shapeId)!.filter;
                return {
                    categoryBits: filter.categoryBits,
                    maskBits: filter.maskBits,
                    groupIndex: filter.groupIndex,
                };
            },
            get userData(): unknown {
                return store._descriptors.get(shapeId)!.userData;
            },
            computeAABB(): { min: IVec2Like; max: IVec2Like } {
                return store._computeShapeAabb(shapeId);
            },
            computeMassData(density: Density): IMassData2D {
                return store._computeShapeMassData(shapeId, density);
            },
            testPoint(point: Readonly<IVec2Like>): boolean {
                return store._testPoint(shapeId, point);
            },
            rayCast(
                origin: Readonly<IVec2Like>,
                direction: Readonly<IVec2Like>,
                maxFraction: number
            ) {
                const hit = store._rayCastShape(shapeId, origin, direction, maxFraction);
                if (!hit) {
                    return null;
                }

                return {
                    hit: true,
                    fraction: hit.fraction,
                    normal: cloneVec2(hit.normal),
                };
            },
            getCenter(): IVec2Like {
                return store._getShapeWorldCenter(shapeId);
            },
        };

        this._views.set(shapeId, shape);
        return shape;
    }

    queryAABBAll(
        min: Readonly<IVec2Like>,
        max: Readonly<IVec2Like>,
        filter?: IQueryFilter
    ): readonly ShapeId[] {
        const queryBounds = normalizeBounds(min, max);
        const shapeIds: ShapeId[] = [];

        for (const [shapeId, descriptor] of this._descriptors) {
            if (!this._canQueryShape(shapeId, descriptor, filter)) {
                continue;
            }

            if (intersectsAabb(this._computeShapeAabb(shapeId), queryBounds)) {
                shapeIds.push(shapeId);
            }
        }

        return shapeIds;
    }

    queryPointAll(point: Readonly<IVec2Like>, filter?: IQueryFilter): readonly ShapeId[] {
        const shapeIds: ShapeId[] = [];

        for (const [shapeId, descriptor] of this._descriptors) {
            if (!this._canQueryShape(shapeId, descriptor, filter)) {
                continue;
            }

            if (this._testPoint(shapeId, point)) {
                shapeIds.push(shapeId);
            }
        }

        return shapeIds;
    }

    rayCastAll(
        origin: Readonly<IVec2Like>,
        direction: Readonly<IVec2Like>,
        maxFraction: number,
        filter?: IQueryFilter
    ): readonly IRaycastResult2D[] {
        if (maxFraction <= 0 || lengthSquared(direction) <= GEOMETRY_EPSILON) {
            return [];
        }

        const results: IRaycastResult2D[] = [];
        for (const [shapeId, descriptor] of this._descriptors) {
            if (!this._canQueryShape(shapeId, descriptor, filter)) {
                continue;
            }

            const hit = this._rayCastShape(shapeId, origin, direction, maxFraction);
            if (!hit) {
                continue;
            }

            results.push(hit);
        }

        results.sort((a, b) => a.fraction - b.fraction);
        return results;
    }

    resetBodyMassData(bodyId: BodyId): void {
        if (!this._bodyManager.hasBody(bodyId)) {
            return;
        }

        if (this._bodyManager.getBodyType(bodyId) !== BodyType.Dynamic) {
            this._bodyManager.setMassData(bodyId, 0, 0, { x: 0, y: 0 });
            return;
        }

        const shapeIds = this._shapeManager.getShapesForBody(bodyId);
        if (shapeIds.length === 0) {
            this._bodyManager.setMassData(bodyId, 0, 0, { x: 0, y: 0 });
            return;
        }

        let totalMass = 0;
        let centerX = 0;
        let centerY = 0;
        const massDataByShape: IMassData2D[] = [];

        for (const shapeId of shapeIds) {
            const descriptor = this._descriptors.get(shapeId);
            if (!descriptor) {
                continue;
            }

            const massData = this._computeShapeMassData(shapeId, descriptor.material.density);
            massDataByShape.push(massData);
            totalMass += massData.mass;
            centerX += massData.center.x * massData.mass;
            centerY += massData.center.y * massData.mass;
        }

        if (totalMass <= GEOMETRY_EPSILON) {
            this._bodyManager.setMassData(bodyId, 0, 0, { x: 0, y: 0 });
            return;
        }

        const center = {
            x: centerX / totalMass,
            y: centerY / totalMass,
        };

        let inertia = 0;
        for (const massData of massDataByShape) {
            const offset = subtractVec2(massData.center, center);
            inertia += massData.inertia + massData.mass * lengthSquared(offset);
        }

        this._bodyManager.setMassData(bodyId, totalMass, inertia, center);
    }

    getProxyCount(): number {
        return this._descriptors.size;
    }

    validate(): boolean {
        for (const [shapeId, descriptor] of this._descriptors) {
            if (!this._shapeManager.hasShape(shapeId)) {
                return false;
            }

            if (!this._bodyManager.hasBody(descriptor.bodyId)) {
                return false;
            }
        }

        return true;
    }

    getBodyWorldCenter(bodyId: BodyId): IVec2Like {
        return getBodyWorldCenter(this._bodyManager, bodyId);
    }

    private _canQueryShape(
        shapeId: ShapeId,
        descriptor: IShapeDescriptor2D,
        filter?: IQueryFilter
    ): boolean {
        return (
            this._descriptors.has(shapeId) &&
            this._bodyManager.hasBody(descriptor.bodyId) &&
            this._bodyManager.isEnabled(descriptor.bodyId) &&
            this._matchesFilter(descriptor.filter, filter)
        );
    }

    private _matchesFilter(shapeFilter: IShapeFilter2D, filter?: IQueryFilter): boolean {
        if (!filter) {
            return true;
        }

        if (
            filter.groupIndex !== undefined &&
            filter.groupIndex !== 0 &&
            shapeFilter.groupIndex !== filter.groupIndex
        ) {
            return false;
        }

        const queryCategoryBits = filter.categoryBits ?? CollisionFilter.All;
        const queryMaskBits = filter.maskBits ?? CollisionFilter.All;
        return (
            (shapeFilter.categoryBits & queryMaskBits) !== 0 &&
            (shapeFilter.maskBits & queryCategoryBits) !== 0
        );
    }

    private _getShapeWorldCenter(shapeId: ShapeId): IVec2Like {
        const descriptor = this._descriptors.get(shapeId)!;
        const bodyPosition = this._bodyManager.getPosition(descriptor.bodyId);
        const bodyRotation = this._bodyManager.getRotation(descriptor.bodyId);

        switch (descriptor.type) {
            case ShapeTypeValue.Circle:
            case ShapeTypeValue.Box:
            case ShapeTypeValue.Capsule:
                return transformPoint2D(bodyPosition, bodyRotation, descriptor.center ?? { x: 0, y: 0 });
            case ShapeTypeValue.Polygon:
                return transformPoint2D(
                    bodyPosition,
                    bodyRotation,
                    computePolygonCentroid(descriptor.vertices ?? [])
                );
            case ShapeTypeValue.Segment: {
                const start = transformPoint2D(bodyPosition, bodyRotation, descriptor.start ?? { x: 0, y: 0 });
                const end = transformPoint2D(bodyPosition, bodyRotation, descriptor.end ?? { x: 0, y: 0 });
                return {
                    x: (start.x + end.x) * 0.5,
                    y: (start.y + end.y) * 0.5,
                };
            }
            default:
                return cloneVec2(bodyPosition);
        }
    }

    private _getShapeWorldVertices(shapeId: ShapeId): readonly IVec2Like[] {
        const descriptor = this._descriptors.get(shapeId)!;
        const bodyPosition = this._bodyManager.getPosition(descriptor.bodyId);
        const bodyRotation = this._bodyManager.getRotation(descriptor.bodyId);

        switch (descriptor.type) {
            case ShapeTypeValue.Box: {
                const center = transformPoint2D(bodyPosition, bodyRotation, descriptor.center ?? { x: 0, y: 0 });
                return buildBoxVertices(
                    center,
                    descriptor.halfWidth ?? 0,
                    descriptor.halfHeight ?? 0,
                    bodyRotation + (descriptor.rotation ?? 0)
                );
            }
            case ShapeTypeValue.Polygon:
                return (descriptor.vertices ?? []).map((vertex) =>
                    transformPoint2D(bodyPosition, bodyRotation, vertex)
                );
            case ShapeTypeValue.Segment:
                return [
                    transformPoint2D(bodyPosition, bodyRotation, descriptor.start ?? { x: 0, y: 0 }),
                    transformPoint2D(bodyPosition, bodyRotation, descriptor.end ?? { x: 0, y: 0 }),
                ];
            default:
                return [];
        }
    }

    private _getCapsuleEndpoints(shapeId: ShapeId): { start: IVec2Like; end: IVec2Like; radius: number } {
        const descriptor = this._descriptors.get(shapeId)!;
        const center = this._getShapeWorldCenter(shapeId);
        const angle = this._bodyManager.getRotation(descriptor.bodyId) + (descriptor.rotation ?? 0);
        const halfLength = (descriptor.length ?? 0) * 0.5;
        const axis = {
            x: Math.cos(angle),
            y: Math.sin(angle),
        };
        return {
            start: {
                x: center.x - axis.x * halfLength,
                y: center.y - axis.y * halfLength,
            },
            end: {
                x: center.x + axis.x * halfLength,
                y: center.y + axis.y * halfLength,
            },
            radius: descriptor.radius ?? 0,
        };
    }

    private _computeShapeAabb(shapeId: ShapeId): IAabb2D {
        const descriptor = this._descriptors.get(shapeId)!;

        switch (descriptor.type) {
            case ShapeTypeValue.Circle: {
                const center = this._getShapeWorldCenter(shapeId);
                const radius = descriptor.radius ?? 0;
                return {
                    min: { x: center.x - radius, y: center.y - radius },
                    max: { x: center.x + radius, y: center.y + radius },
                };
            }
            case ShapeTypeValue.Box:
            case ShapeTypeValue.Polygon: {
                const vertices = this._getShapeWorldVertices(shapeId);
                let minX = Infinity;
                let minY = Infinity;
                let maxX = -Infinity;
                let maxY = -Infinity;
                for (const vertex of vertices) {
                    minX = Math.min(minX, vertex.x);
                    minY = Math.min(minY, vertex.y);
                    maxX = Math.max(maxX, vertex.x);
                    maxY = Math.max(maxY, vertex.y);
                }
                return {
                    min: { x: minX, y: minY },
                    max: { x: maxX, y: maxY },
                };
            }
            case ShapeTypeValue.Segment: {
                const [start, end] = this._getShapeWorldVertices(shapeId);
                return {
                    min: { x: Math.min(start.x, end.x), y: Math.min(start.y, end.y) },
                    max: { x: Math.max(start.x, end.x), y: Math.max(start.y, end.y) },
                };
            }
            case ShapeTypeValue.Capsule: {
                const capsule = this._getCapsuleEndpoints(shapeId);
                return {
                    min: {
                        x: Math.min(capsule.start.x, capsule.end.x) - capsule.radius,
                        y: Math.min(capsule.start.y, capsule.end.y) - capsule.radius,
                    },
                    max: {
                        x: Math.max(capsule.start.x, capsule.end.x) + capsule.radius,
                        y: Math.max(capsule.start.y, capsule.end.y) + capsule.radius,
                    },
                };
            }
            default:
                return {
                    min: { x: 0, y: 0 },
                    max: { x: 0, y: 0 },
                };
        }
    }

    private _computeShapeMassData(shapeId: ShapeId, density: Density): IMassData2D {
        const descriptor = this._descriptors.get(shapeId)!;

        switch (descriptor.type) {
            case ShapeTypeValue.Circle: {
                const radius = descriptor.radius ?? 0;
                const mass = density * Math.PI * radius * radius;
                const inertia = mass * radius * radius * 0.5;
                return {
                    mass: mass as IMassData2D['mass'],
                    inverseMass: mass > GEOMETRY_EPSILON ? 1 / mass : 0,
                    inertia: inertia as IMassData2D['inertia'],
                    inverseInertia: inertia > GEOMETRY_EPSILON ? 1 / inertia : 0,
                    center: cloneVec2(descriptor.center ?? { x: 0, y: 0 }),
                };
            }
            case ShapeTypeValue.Box: {
                const halfWidth = descriptor.halfWidth ?? 0;
                const halfHeight = descriptor.halfHeight ?? 0;
                const width = halfWidth * 2;
                const height = halfHeight * 2;
                const mass = density * width * height;
                const inertia = (mass / 12) * (width * width + height * height);
                return {
                    mass: mass as IMassData2D['mass'],
                    inverseMass: mass > GEOMETRY_EPSILON ? 1 / mass : 0,
                    inertia: inertia as IMassData2D['inertia'],
                    inverseInertia: inertia > GEOMETRY_EPSILON ? 1 / inertia : 0,
                    center: cloneVec2(descriptor.center ?? { x: 0, y: 0 }),
                };
            }
            case ShapeTypeValue.Polygon:
                return computePolygonMassData(descriptor.vertices ?? [], density);
            case ShapeTypeValue.Capsule: {
                const radius = descriptor.radius ?? 0;
                const length = descriptor.length ?? 0;
                const rectangleMass = density * length * radius * 2;
                const circleMass = density * Math.PI * radius * radius;
                const mass = rectangleMass + circleMass;
                const rectangleInertia = (rectangleMass / 12) * (length * length + 4 * radius * radius);
                const circleInertia =
                    0.5 * circleMass * radius * radius + circleMass * (length * length) * 0.25;
                const inertia = rectangleInertia + circleInertia;

                return {
                    mass: mass as IMassData2D['mass'],
                    inverseMass: mass > GEOMETRY_EPSILON ? 1 / mass : 0,
                    inertia: inertia as IMassData2D['inertia'],
                    inverseInertia: inertia > GEOMETRY_EPSILON ? 1 / inertia : 0,
                    center: cloneVec2(descriptor.center ?? { x: 0, y: 0 }),
                };
            }
            case ShapeTypeValue.Segment:
            default:
                return {
                    mass: 0 as IMassData2D['mass'],
                    inverseMass: 0,
                    inertia: 0 as IMassData2D['inertia'],
                    inverseInertia: 0,
                    center: this._getShapeLocalCenter(shapeId),
                };
        }
    }

    private _getShapeLocalCenter(shapeId: ShapeId): IVec2Like {
        const descriptor = this._descriptors.get(shapeId)!;
        switch (descriptor.type) {
            case ShapeTypeValue.Circle:
            case ShapeTypeValue.Box:
            case ShapeTypeValue.Capsule:
                return cloneVec2(descriptor.center ?? { x: 0, y: 0 });
            case ShapeTypeValue.Polygon:
                return computePolygonCentroid(descriptor.vertices ?? []);
            case ShapeTypeValue.Segment: {
                const start = descriptor.start ?? { x: 0, y: 0 };
                const end = descriptor.end ?? { x: 0, y: 0 };
                return {
                    x: (start.x + end.x) * 0.5,
                    y: (start.y + end.y) * 0.5,
                };
            }
            default:
                return { x: 0, y: 0 };
        }
    }

    private _testPoint(shapeId: ShapeId, point: Readonly<IVec2Like>): boolean {
        const descriptor = this._descriptors.get(shapeId)!;
        switch (descriptor.type) {
            case ShapeTypeValue.Circle:
                return (
                    distanceSquared(point, this._getShapeWorldCenter(shapeId)) <=
                    Math.pow(descriptor.radius ?? 0, 2)
                );
            case ShapeTypeValue.Box: {
                const center = this._getShapeWorldCenter(shapeId);
                const localPoint = inverseRotateVec2(
                    subtractVec2(point, center),
                    this._bodyManager.getRotation(descriptor.bodyId) + (descriptor.rotation ?? 0)
                );
                return (
                    Math.abs(localPoint.x) <= (descriptor.halfWidth ?? 0) + POINT_QUERY_EPSILON &&
                    Math.abs(localPoint.y) <= (descriptor.halfHeight ?? 0) + POINT_QUERY_EPSILON
                );
            }
            case ShapeTypeValue.Polygon:
                return pointInPolygon(point, this._getShapeWorldVertices(shapeId));
            case ShapeTypeValue.Segment: {
                const [start, end] = this._getShapeWorldVertices(shapeId);
                return (
                    distanceSquaredToSegment(point, start, end) <=
                    POINT_QUERY_EPSILON * POINT_QUERY_EPSILON
                );
            }
            case ShapeTypeValue.Capsule: {
                const capsule = this._getCapsuleEndpoints(shapeId);
                return (
                    distanceSquaredToSegment(point, capsule.start, capsule.end) <=
                    capsule.radius * capsule.radius
                );
            }
            default:
                return false;
        }
    }

    private _rayCastShape(
        shapeId: ShapeId,
        origin: Readonly<IVec2Like>,
        direction: Readonly<IVec2Like>,
        maxFraction: number
    ): IRaycastResult2D | null {
        const descriptor = this._descriptors.get(shapeId)!;
        let result:
            | {
                  hit: boolean;
                  fraction: number;
                  point: IVec2Like;
                  normal: IVec2Like;
              }
            | null = null;

        switch (descriptor.type) {
            case ShapeTypeValue.Circle: {
                const hit = Raycaster2D.raycastCircle(
                    origin,
                    direction,
                    this._getShapeWorldCenter(shapeId),
                    descriptor.radius ?? 0,
                    maxFraction
                );
                if (hit.hit) {
                    result = {
                        hit: true,
                        fraction: hit.distance,
                        point: cloneVec2(hit.point),
                        normal: cloneVec2(hit.normal),
                    };
                }
                break;
            }
            case ShapeTypeValue.Box:
            case ShapeTypeValue.Polygon: {
                const vertices = this._getShapeWorldVertices(shapeId);
                const hit = Raycaster2D.raycastPolygon(
                    origin,
                    direction,
                    vertices,
                    { position: { x: 0, y: 0 }, rotation: 0 },
                    maxFraction
                );
                if (hit.hit) {
                    result = {
                        hit: true,
                        fraction: hit.distance,
                        point: cloneVec2(hit.point),
                        normal: cloneVec2(hit.normal),
                    };
                }
                break;
            }
            case ShapeTypeValue.Segment: {
                const [start, end] = this._getShapeWorldVertices(shapeId);
                const hit = raycastSegment(origin, direction, start, end, maxFraction);
                if (hit.hit) {
                    result = hit;
                }
                break;
            }
            case ShapeTypeValue.Capsule: {
                const capsule = this._getCapsuleEndpoints(shapeId);
                const hits: Array<{ hit: boolean; fraction: number; point: IVec2Like; normal: IVec2Like }> = [];
                const startCircle = Raycaster2D.raycastCircle(
                    origin,
                    direction,
                    capsule.start,
                    capsule.radius,
                    maxFraction
                );
                if (startCircle.hit) {
                    hits.push({
                        hit: true,
                        fraction: startCircle.distance,
                        point: cloneVec2(startCircle.point),
                        normal: cloneVec2(startCircle.normal),
                    });
                }

                const endCircle = Raycaster2D.raycastCircle(
                    origin,
                    direction,
                    capsule.end,
                    capsule.radius,
                    maxFraction
                );
                if (endCircle.hit) {
                    hits.push({
                        hit: true,
                        fraction: endCircle.distance,
                        point: cloneVec2(endCircle.point),
                        normal: cloneVec2(endCircle.normal),
                    });
                }

                const axis = subtractVec2(capsule.end, capsule.start);
                const axisLength = Math.sqrt(lengthSquared(axis));
                if (axisLength > GEOMETRY_EPSILON) {
                    const perpendicular = {
                        x: (-axis.y / axisLength) * capsule.radius,
                        y: (axis.x / axisLength) * capsule.radius,
                    };
                    const rectangleVertices = [
                        { x: capsule.start.x + perpendicular.x, y: capsule.start.y + perpendicular.y },
                        { x: capsule.end.x + perpendicular.x, y: capsule.end.y + perpendicular.y },
                        { x: capsule.end.x - perpendicular.x, y: capsule.end.y - perpendicular.y },
                        { x: capsule.start.x - perpendicular.x, y: capsule.start.y - perpendicular.y },
                    ];
                    const rectHit = Raycaster2D.raycastPolygon(
                        origin,
                        direction,
                        rectangleVertices,
                        { position: { x: 0, y: 0 }, rotation: 0 },
                        maxFraction
                    );
                    if (rectHit.hit) {
                        hits.push({
                            hit: true,
                            fraction: rectHit.distance,
                            point: cloneVec2(rectHit.point),
                            normal: cloneVec2(rectHit.normal),
                        });
                    }
                }

                hits.sort((a, b) => a.fraction - b.fraction);
                result = hits.length > 0 ? hits[0] : null;
                break;
            }
            default:
                break;
        }

        if (!result?.hit) {
            return null;
        }

        return {
            hit: true,
            bodyId: descriptor.bodyId,
            shapeId,
            point: cloneVec2(result.point),
            normal: cloneVec2(result.normal),
            fraction: result.fraction,
        };
    }
}