import { Vec3 } from '@axrone/numeric';

declare const ParticleIdBrand: unique symbol;
export type ParticleId = number & { readonly [ParticleIdBrand]: never };

declare const SystemIdBrand: unique symbol;
export type SystemId = number & { readonly [SystemIdBrand]: never };

declare const EmitterIdBrand: unique symbol;
export type EmitterId = number & { readonly [EmitterIdBrand]: never };

export const enum EmitterShape {
    Box,
    Sphere,
    Circle,
    Cone,
    Mesh,
    Edge,
    Rectangle,
    Hemisphere,
    Donut,
    Line,
    Point,
}

export const enum SimulationSpace {
    Local,
    World,
    Custom,
}

export const enum SortMode {
    None,
    Distance,
    OldestFirst,
    YoungestFirst,
    Custom,
}

export const enum RenderMode {
    Billboard,
    Stretch,
    HorizontalBillboard,
    VerticalBillboard,
    Mesh,
    Trail,
    Ribbon,
}

export const enum StopAction {
    None,
    Disable,
    Destroy,
    Callback,
}

export const enum CullingMode {
    Automatic,
    Pause,
    PauseAndCatchup,
    AlwaysSimulate,
}

export const enum RingBufferMode {
    Disabled,
    PauseUntilReplaced,
    LoopUntilReplaced,
}

export const enum CustomDataType {
    Vector,
    Color,
}

export const enum ParticleSystemCurveMode {
    Constant,
    Curve,
    TwoCurves,
    TwoConstants,
}

export const enum ParticleSystemGradientMode {
    Color,
    Gradient,
    TwoColors,
    TwoGradients,
    RandomColor,
}

export const enum MainModuleFlag {
    StartLifetime = 1 << 0,
    StartSpeed = 1 << 1,
    StartSize = 1 << 2,
    StartRotation = 1 << 3,
    StartColor = 1 << 4,
    GravityModifier = 1 << 5,
    SimulationSpace = 1 << 6,
    SimulationSpeed = 1 << 7,
    ScalingMode = 1 << 8,
    PlayOnAwake = 1 << 9,
    MaxParticles = 1 << 10,
    All = (1 << 11) - 1,
}

export interface Curve {
    mode: ParticleSystemCurveMode;
    constant: number;
    constantMin: number;
    constantMax: number;
    curve?: Float32Array;
    curveMin?: Float32Array;
    curveMax?: Float32Array;
    curveMultiplier: number;
}

export interface Gradient {
    mode: ParticleSystemGradientMode;
    color: { r: number; g: number; b: number; a: number };
    colorMin: { r: number; g: number; b: number; a: number };
    colorMax: { r: number; g: number; b: number; a: number };
    gradient?: Float32Array;
    gradientMin?: Float32Array;
    gradientMax?: Float32Array;
}

export interface Burst {
    time: number;
    count: { value: number; variance: number };
    cycles: number;
    interval: number;
    probability: number;
}

export interface AnimationCurve {
    keys: {
        time: number;
        value: number;
        inTangent: number;
        outTangent: number;
        weightedMode: number;
        inWeight: number;
        outWeight: number;
    }[];
    preWrapMode: number;
    postWrapMode: number;
}

export interface MinMaxCurve extends Curve {
    scalar: number;
}

export interface MinMaxGradient extends Gradient {
    scalar: number;
}

export interface ParticleEventBase {
    particleId: ParticleId;
    position: Vec3;
    velocity: Vec3;
    data?: any;
}

export interface CollisionEventData extends ParticleEventBase {
    collider: any;
    normal: Vec3;
    surfaceVelocity: Vec3;
}

export interface DeathEventData extends ParticleEventBase {
    lifetime: number;
    age: number;
}

export interface SubEmitterEventData extends ParticleEventBase {
    subSystem: any;
    trigger: string;
}

// Event map for the particle system
export interface ParticleSystemEventMap {
    collision: CollisionEventData;
    death: DeathEventData;
    subemitter: SubEmitterEventData;
}

// Legacy types for backward compatibility
export interface ParticleEvent {
    type: string;
    particleId: ParticleId;
    position: Vec3;
    velocity: Vec3;
    data?: any;
}

export interface CollisionEvent extends ParticleEvent {
    type: 'collision';
    collider: any;
    normal: Vec3;
    surfaceVelocity: Vec3;
}

export interface DeathEvent extends ParticleEvent {
    type: 'death';
    lifetime: number;
    age: number;
}

export interface SubEmitterEvent extends ParticleEvent {
    type: 'subemitter';
    subSystem: any;
    trigger: string;
}
