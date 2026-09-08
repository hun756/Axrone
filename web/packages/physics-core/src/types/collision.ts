import type { IVec2Like, IVec3Like } from '@axrone/numeric';
import type {
    BodyId,
    ShapeId,
    Impulse,
    IContactManifold2D,
    IContactManifold3D,
    IContactPoint2D,
    IContactPoint3D,
} from './primitives';

export type {
    IContactManifold2D,
    IContactManifold3D,
    IContactPoint2D,
    IContactPoint3D,
} from './primitives';

export const enum CollisionEventType {
    Begin = 0,
    Stay = 1,
    End = 2,
    PreSolve = 3,
    PostSolve = 4,
}

export const enum SensorEventType {
    Enter = 0,
    Stay = 1,
    Exit = 2,
}

export interface ICollisionEvent2D {
    readonly type: CollisionEventType;
    readonly bodyIdA: BodyId;
    readonly bodyIdB: BodyId;
    readonly shapeIdA: ShapeId;
    readonly shapeIdB: ShapeId;
    readonly manifold: IContactManifold2D;
    readonly timestamp: number;
}

export interface ICollisionEvent3D {
    readonly type: CollisionEventType;
    readonly bodyIdA: BodyId;
    readonly bodyIdB: BodyId;
    readonly shapeIdA: ShapeId;
    readonly shapeIdB: ShapeId;
    readonly manifold: IContactManifold3D;
    readonly timestamp: number;
}

export interface ISensorEvent2D {
    readonly type: SensorEventType;
    readonly sensorBodyId: BodyId;
    readonly sensorShapeId: ShapeId;
    readonly visitorBodyId: BodyId;
    readonly visitorShapeId: ShapeId;
    readonly timestamp: number;
}

export interface ISensorEvent3D {
    readonly type: SensorEventType;
    readonly sensorBodyId: BodyId;
    readonly sensorShapeId: ShapeId;
    readonly visitorBodyId: BodyId;
    readonly visitorShapeId: ShapeId;
    readonly timestamp: number;
}

export interface ICollisionFilter {
    shouldCollide(shapeIdA: ShapeId, shapeIdB: ShapeId): boolean;
}

export interface IContactListener2D {
    onCollisionBegin?(event: ICollisionEvent2D): void;
    onCollisionStay?(event: ICollisionEvent2D): void;
    onCollisionEnd?(event: ICollisionEvent2D): void;
    onPreSolve?(event: ICollisionEvent2D, oldManifold: IContactManifold2D): void;
    onPostSolve?(event: ICollisionEvent2D, impulse: { normal: Impulse; tangent: Impulse }): void;
    onSensorEnter?(event: ISensorEvent2D): void;
    onSensorStay?(event: ISensorEvent2D): void;
    onSensorExit?(event: ISensorEvent2D): void;
}

export interface IContactListener3D {
    onCollisionBegin?(event: ICollisionEvent3D): void;
    onCollisionStay?(event: ICollisionEvent3D): void;
    onCollisionEnd?(event: ICollisionEvent3D): void;
    onPreSolve?(event: ICollisionEvent3D, oldManifold: IContactManifold3D): void;
    onPostSolve?(
        event: ICollisionEvent3D,
        impulse: { normal: Impulse; tangent1: Impulse; tangent2: Impulse }
    ): void;
    onSensorEnter?(event: ISensorEvent3D): void;
    onSensorStay?(event: ISensorEvent3D): void;
    onSensorExit?(event: ISensorEvent3D): void;
}

export interface ITimeOfImpactInput2D {
    readonly shapeIdA: ShapeId;
    readonly shapeIdB: ShapeId;
    readonly sweepA: {
        readonly c0: IVec2Like;
        readonly c: IVec2Like;
        readonly a0: number;
        readonly a: number;
        readonly localCenter: IVec2Like;
    };
    readonly sweepB: {
        readonly c0: IVec2Like;
        readonly c: IVec2Like;
        readonly a0: number;
        readonly a: number;
        readonly localCenter: IVec2Like;
    };
    readonly tMax: number;
}

export interface ITimeOfImpactResult {
    readonly state: TOIState;
    readonly t: number;
}

export const enum TOIState {
    Unknown = 0,
    Failed = 1,
    Overlapped = 2,
    Touching = 3,
    Separated = 4,
}

export interface IGJK2DOutput {
    readonly pointA: IVec2Like;
    readonly pointB: IVec2Like;
    readonly distance: number;
    readonly iterations: number;
}

export interface IEPA2DOutput {
    readonly penetrationDepth: number;
    readonly normal: IVec2Like;
    readonly witnesses: { a: IVec2Like; b: IVec2Like };
}

export interface ISupportPoint2D {
    readonly point: IVec2Like;
    readonly indexA: number;
    readonly indexB: number;
}

export interface ISimplex2D {
    readonly vertices: ISupportPoint2D[];
    readonly count: number;
}

export type RaycastCallback2D = (
    shapeId: ShapeId,
    point: Readonly<IVec2Like>,
    normal: Readonly<IVec2Like>,
    fraction: number
) => number;

export type RaycastCallback3D = (
    shapeId: ShapeId,
    point: Readonly<IVec3Like>,
    normal: Readonly<IVec3Like>,
    fraction: number
) => number;
