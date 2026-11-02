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

export interface CapsuleShape {
    readonly kind: "capsule";
    readonly radius: number;
    readonly halfSegment: number;
}

export interface CylinderShape {
    readonly kind: "cylinder";
    readonly radius: number;
    readonly halfHeight: number;
}

export type Shape = SphereShape | BoxShape | CapsuleShape | CylinderShape;

export interface CapsuleAttachOptions {
    readonly radius: number;
    readonly halfSegment: number;
    readonly density?: number;
    readonly offset?: Vec3Tuple;
    readonly rotation?: QuatTuple;
}

export interface CylinderAttachOptions {
    readonly radius: number;
    readonly halfHeight: number;
    readonly density?: number;
    readonly offset?: Vec3Tuple;
    readonly rotation?: QuatTuple;
}

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

export interface FixedJointOptions {
    readonly localAnchorA: Vec3Tuple;
    readonly localAnchorB: Vec3Tuple;
    readonly collideConnected?: boolean;
}

export interface HingeJointOptions {
    readonly localAnchorA: Vec3Tuple;
    readonly localAnchorB: Vec3Tuple;
    readonly localAxisA: Vec3Tuple;
    readonly localAxisB: Vec3Tuple;
    readonly enableLimit?: boolean;
    readonly lowerLimit?: number;
    readonly upperLimit?: number;
    readonly enableMotor?: boolean;
    readonly motorSpeed?: number;
    readonly maxMotorTorque?: number;
    readonly collideConnected?: boolean;
}

export interface SliderJointOptions {
    readonly localAnchorA: Vec3Tuple;
    readonly localAnchorB: Vec3Tuple;
    readonly localAxisA: Vec3Tuple;
    readonly enableLimit?: boolean;
    readonly lowerLimit?: number;
    readonly upperLimit?: number;
    readonly enableMotor?: boolean;
    readonly motorSpeed?: number;
    readonly maxMotorForce?: number;
    readonly collideConnected?: boolean;
}

export interface SpringJointOptions {
    readonly localAnchorA: Vec3Tuple;
    readonly localAnchorB: Vec3Tuple;
    readonly restLength?: number;
    readonly stiffness?: number;
    readonly damping?: number;
    readonly collideConnected?: boolean;
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

export type ShapeMaterial = readonly [friction: number, restitution: number];

export type ShapeFilter = readonly [categoryBits: number, maskBits: number, groupIndex: number];

export type ContactEventKind =
    | "begin"
    | "stay"
    | "end"
    | "sensorEnter"
    | "sensorStay"
    | "sensorExit";

export interface ContactEventSnapshot {
    readonly kind: ContactEventKind;
    readonly bodyA: number;
    readonly bodyB: number;
    readonly shapeA: number;
    readonly shapeB: number;
    readonly normal: Vec3Tuple;
    readonly penetration: number;
}

export interface RaycastHit {
    readonly body: number;
    readonly shape: number;
    readonly distance: number;
    readonly point: Vec3Tuple;
    readonly normal: Vec3Tuple;
}
