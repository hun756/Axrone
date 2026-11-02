export type Vec3Tuple = readonly [number, number, number];
export type QuatTuple = readonly [number, number, number, number];

export type MutableVec3 = [number, number, number];
export type MutableQuat = [number, number, number, number];

export type BodyType = "static" | "dynamic" | "kinematic";

export const BODY_FLAGS = Object.freeze({
    freezeRotationX: 1,
    freezeRotationY: 2,
    freezeRotationZ: 4,
    freezePositionX: 8,
    freezePositionY: 16,
    freezePositionZ: 32
});

export interface SphereShape {
    readonly kind: "sphere";
    readonly radius: number;
}

export interface BoxShape {
    readonly kind: "box";
    readonly halfExtents: Vec3Tuple;
}

export type Shape = SphereShape | BoxShape;

export interface BodyOptions {
    readonly type?: BodyType;
    readonly shape?: Shape;
    readonly position?: Vec3Tuple;
    readonly rotation?: QuatTuple;
    readonly density?: number;
    readonly friction?: number;
    readonly restitution?: number;
}

export interface WorldOptions {
    readonly maxBodies: number;
    readonly maxContacts?: number;
    readonly gravity?: Vec3Tuple;
}

export interface StepOptions {
    readonly velocityIterations?: number;
    readonly positionIterations?: number;
}

export interface TransformSnapshot {
    readonly position: MutableVec3;
    readonly rotation: MutableQuat;
}

export interface ContactSnapshot {
    readonly a: number;
    readonly b: number;
    readonly normal: MutableVec3;
    readonly penetration: number;
}

export interface RaycastHit {
    readonly body: number;
    readonly distance: number;
    readonly point: Vec3Tuple;
    readonly normal: Vec3Tuple;
}
