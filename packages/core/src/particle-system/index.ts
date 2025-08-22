import { Mat4, Vec3 } from '@axrone/numeric';
import { AABB3D } from '../geometry';

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

export const enum CurveMode {
    Constant,
    Curve,
    Random,
    RandomCurve,
}

export const enum GradientMode {
    Blend,
    Fixed,
    Random,
}

export const enum CollisionMode {
    Planes,
    World,
    Subemitters,
}

export const enum QualityLevel {
    Low,
    Medium,
    High,
    Ultra,
}

export const enum UpdateMode {
    Normal,
    UnscaledTime,
    Manual,
}

export const enum StopAction {
    None,
    Disable,
    Destroy,
    Callback,
}

export const enum CullingMode {
    Automatic,
    PauseAndCatchup,
    Pause,
    AlwaysSimulate,
}

export const enum RingBufferMode {
    Disabled,
    PauseUntilReplaced,
    LoopUntilReplaced,
}

export const enum TextureSheetAnimation {
    WholeSheet,
    SingleRow,
    Sprites,
}

export const enum UVChannelFlags {
    UV0 = 1,
    UV1 = 2,
    UV2 = 4,
    UV3 = 8,
}

interface IAlignedArray {
    readonly buffer: ArrayBuffer;
    readonly byteOffset: number;
    readonly byteLength: number;
}

interface ISimdVec3 extends IAlignedArray {
    readonly x: Float32Array;
    readonly y: Float32Array;
    readonly z: Float32Array;
    readonly w?: Float32Array;
}

interface ISimdVec4 extends IAlignedArray {
    readonly x: Float32Array;
    readonly y: Float32Array;
    readonly z: Float32Array;
    readonly w: Float32Array;
}

interface ICurve {
    readonly mode: CurveMode;
    readonly constant: number;
    readonly constantMin: number;
    readonly constantMax: number;
    readonly curve?: Float32Array;
    readonly curveMin?: Float32Array;
    readonly curveMax?: Float32Array;
    readonly curveLength: number;
    readonly preWrapMode: number;
    readonly postWrapMode: number;
}

interface IGradient {
    readonly mode: GradientMode;
    readonly colorKeys: Float32Array;
    readonly alphaKeys: Float32Array;
    readonly keyCount: number;
    readonly blendMode: number;
}

interface IBurst {
    readonly time: number;
    readonly count: ICurve;
    readonly cycles: number;
    readonly interval: number;
    readonly probability: number;
    readonly repeatInterval: number;
}

interface IEmissionConfig {
    readonly enabled: boolean;
    readonly rateOverTime: ICurve;
    readonly rateOverDistance: ICurve;
    readonly bursts: readonly IBurst[];
    readonly type: number;
}

interface IShapeConfig {
    readonly enabled: boolean;
    readonly shape: EmitterShape;
    readonly radius: number;
    readonly radiusThickness: number;
    readonly radiusSpeed: ICurve;
    readonly radiusSpread: number;
    readonly angle: number;
    readonly length: number;
    readonly box: Vec3;
    readonly donutRadius: number;
    readonly position: Vec3;
    readonly rotation: Vec3;
    readonly scale: Vec3;
    readonly alignToDirection: boolean;
    readonly randomDirectionAmount: number;
    readonly sphericalDirectionAmount: number;
    readonly randomPositionAmount: number;
    readonly normalOffset: number;
    readonly meshSpawnSpeed: ICurve;
    readonly meshSpawnSpread: number;
    readonly useMeshMaterialIndex: boolean;
    readonly meshMaterialIndex: number;
    readonly useMeshColors: boolean;
    readonly texture: any;
    readonly textureClipChannel: number;
    readonly textureClipThreshold: number;
    readonly textureColorAffectsParticles: boolean;
    readonly textureAlphaAffectsParticles: boolean;
    readonly textureBilinearFiltering: boolean;
}

interface IVelocityOverLifetimeConfig {
    readonly enabled: boolean;
    readonly linear: Vec3;
    readonly linearCurve: readonly [ICurve, ICurve, ICurve];
    readonly orbital: Vec3;
    readonly orbitalCurve: readonly [ICurve, ICurve, ICurve];
    readonly offset: Vec3;
    readonly offsetCurve: readonly [ICurve, ICurve, ICurve];
    readonly radial: ICurve;
    readonly speedModifier: ICurve;
    readonly space: SimulationSpace;
}

interface IForceOverLifetimeConfig {
    readonly enabled: boolean;
    readonly force: Vec3;
    readonly forceCurve: readonly [ICurve, ICurve, ICurve];
    readonly space: SimulationSpace;
    readonly randomized: boolean;
}

interface IColorOverLifetimeConfig {
    readonly enabled: boolean;
    readonly color: IGradient;
}

interface IColorBySpeedConfig {
    readonly enabled: boolean;
    readonly color: IGradient;
    readonly speedRange: Vec3;
}

interface ISizeOverLifetimeConfig {
    readonly enabled: boolean;
    readonly size: ICurve;
    readonly sizeCurve: readonly [ICurve, ICurve, ICurve];
    readonly separateAxes: boolean;
}

interface ISizeBySpeedConfig {
    readonly enabled: boolean;
    readonly size: ICurve;
    readonly sizeCurve: readonly [ICurve, ICurve, ICurve];
    readonly speedRange: Vec3;
    readonly separateAxes: boolean;
}

interface IRotationOverLifetimeConfig {
    readonly enabled: boolean;
    readonly angularVelocity: Vec3;
    readonly angularVelocityCurve: readonly [ICurve, ICurve, ICurve];
    readonly separateAxes: boolean;
}

interface IRotationBySpeedConfig {
    readonly enabled: boolean;
    readonly angularVelocity: Vec3;
    readonly angularVelocityCurve: readonly [ICurve, ICurve, ICurve];
    readonly speedRange: Vec3;
    readonly separateAxes: boolean;
}

interface INoiseConfig {
    readonly enabled: boolean;
    readonly strength: Vec3;
    readonly strengthCurve: readonly [ICurve, ICurve, ICurve];
    readonly frequency: number;
    readonly octaves: number;
    readonly octaveMultiplier: number;
    readonly octaveScale: number;
    readonly damping: boolean;
    readonly scrollSpeed: Vec3;
    readonly scrollSpeedCurve: readonly [ICurve, ICurve, ICurve];
    readonly separateAxes: boolean;
    readonly positionAmount: ICurve;
    readonly rotationAmount: ICurve;
    readonly sizeAmount: ICurve;
    readonly quality: QualityLevel;
    readonly remapEnabled: boolean;
    readonly remap: Vec3;
    readonly remapCurve: readonly [ICurve, ICurve, ICurve];
}

interface ICollisionConfig {
    readonly enabled: boolean;
    readonly type: CollisionMode;
    readonly mode: number;
    readonly dampen: ICurve;
    readonly bounce: ICurve;
    readonly lifetimeLoss: ICurve;
    readonly minKillSpeed: number;
    readonly maxKillSpeed: number;
    readonly radiusScale: number;
    readonly planes: readonly Plane[];
    readonly enableDynamicColliders: boolean;
    readonly quality: QualityLevel;
    readonly voxelSize: number;
    readonly collidesWith: number;
    readonly collidesWithDynamic: boolean;
    readonly interiorCollisions: boolean;
    readonly maxCollisionShapes: number;
    readonly sendCollisionMessages: boolean;
    readonly multiplyColliderForceByCollisionAngle: boolean;
    readonly multiplyColliderForceByParticleSpeed: boolean;
    readonly multiplyColliderForceByParticleSize: boolean;
}

interface ILimitVelocityConfig {
    readonly enabled: boolean;
    readonly limit: Vec3;
    readonly limitCurve: readonly [ICurve, ICurve, ICurve];
    readonly dampen: number;
    readonly separateAxes: boolean;
    readonly space: SimulationSpace;
    readonly drag: ICurve;
    readonly multiplyDragBySize: boolean;
    readonly multiplyDragByVelocity: boolean;
}

interface ITextureSheetConfig {
    readonly enabled: boolean;
    readonly mode: TextureSheetAnimation;
    readonly tiles: Vec3;
    readonly animation: number;
    readonly frameOverTime: ICurve;
    readonly startFrame: ICurve;
    readonly cycleCount: number;
    readonly flipU: number;
    readonly flipV: number;
    readonly uvChannelMask: UVChannelFlags;
    readonly fps: number;
    readonly timeMode: number;
    readonly sprites: readonly any[];
    readonly spriteCount: number;
}

interface ITrailConfig {
    readonly enabled: boolean;
    readonly mode: number;
    readonly ratio: number;
    readonly lifetime: ICurve;
    readonly lifetimeMultiplier: number;
    readonly minVertexDistance: number;
    readonly textureMode: number;
    readonly worldSpace: boolean;
    readonly dieWithParticles: boolean;
    readonly sizeAffectsWidth: boolean;
    readonly sizeAffectsLifetime: boolean;
    readonly inheritParticleColor: boolean;
    readonly colorOverLifetime: IGradient;
    readonly widthOverTrail: ICurve;
    readonly colorOverTrail: IGradient;
    readonly generateLightingData: boolean;
    readonly shadowBias: number;
    readonly splitSubEmitterRibbons: boolean;
    readonly attachRibbonsToTransform: boolean;
    readonly ribbonCount: number;
}

interface ISubEmitterConfig {
    readonly enabled: boolean;
    readonly birth: readonly SystemId[];
    readonly collision: readonly SystemId[];
    readonly death: readonly SystemId[];
    readonly trigger: readonly SystemId[];
    readonly manualEmission: readonly SystemId[];
    readonly inherit: number;
    readonly emitProbability: number;
}

interface ILightsConfig {
    readonly enabled: boolean;
    readonly ratio: number;
    readonly useRandomDistribution: boolean;
    readonly light: any;
    readonly useParticleColor: boolean;
    readonly sizeAffectsRange: boolean;
    readonly alphaAffectsIntensity: boolean;
    readonly range: ICurve;
    readonly rangeMultiplier: number;
    readonly intensity: ICurve;
    readonly intensityMultiplier: number;
    readonly maxLights: number;
}

interface ICustomDataConfig {
    readonly enabled: boolean;
    readonly mode0: number;
    readonly vectorComponentCount0: number;
    readonly color0: IGradient;
    readonly vector0: readonly [ICurve, ICurve, ICurve, ICurve];
    readonly mode1: number;
    readonly vectorComponentCount1: number;
    readonly color1: IGradient;
    readonly vector1: readonly [ICurve, ICurve, ICurve, ICurve];
}

interface IMainConfig {
    readonly duration: number;
    readonly loop: boolean;
    readonly prewarm: boolean;
    readonly prewarmCycles: number;
    readonly startDelay: ICurve;
    readonly startLifetime: ICurve;
    readonly startSpeed: ICurve;
    readonly startSize: ICurve;
    readonly startSizeX: ICurve;
    readonly startSizeY: ICurve;
    readonly startSizeZ: ICurve;
    readonly startRotation: ICurve;
    readonly startRotationX: ICurve;
    readonly startRotationY: ICurve;
    readonly startRotationZ: ICurve;
    readonly startColor: IGradient;
    readonly gravityModifier: ICurve;
    readonly simulationSpace: SimulationSpace;
    readonly simulationSpeed: number;
    readonly deltaTimeScale: number;
    readonly maxParticles: number;
    readonly scalingMode: number;
    readonly playOnAwake: boolean;
    readonly startSize3D: boolean;
    readonly startRotation3D: boolean;
    readonly flipRotation: number;
    readonly stopAction: StopAction;
    readonly cullingMode: CullingMode;
    readonly customSimulationSpace?: Mat4;
    readonly emitterVelocityMode: number;
    readonly inheritVelocity: ICurve;
    readonly ringBufferMode: RingBufferMode;
    readonly ringBufferLoopRange: Vec3;
    readonly useUnscaledTime: boolean;
    readonly autoRandomSeed: boolean;
    readonly randomSeed: number;
}

export interface IParticleSystemConfig {
    readonly main: IMainConfig;
    readonly emission: IEmissionConfig;
    readonly shape: IShapeConfig;
    readonly velocityOverLifetime: IVelocityOverLifetimeConfig;
    readonly forceOverLifetime: IForceOverLifetimeConfig;
    readonly colorOverLifetime: IColorOverLifetimeConfig;
    readonly colorBySpeed: IColorBySpeedConfig;
    readonly sizeOverLifetime: ISizeOverLifetimeConfig;
    readonly sizeBySpeed: ISizeBySpeedConfig;
    readonly rotationOverLifetime: IRotationOverLifetimeConfig;
    readonly rotationBySpeed: IRotationBySpeedConfig;
    readonly noise: INoiseConfig;
    readonly collision: ICollisionConfig;
    readonly limitVelocity: ILimitVelocityConfig;
    readonly textureSheet: ITextureSheetConfig;
    readonly trails: ITrailConfig;
    readonly subEmitters: ISubEmitterConfig;
    readonly lights: ILightsConfig;
    readonly customData: ICustomDataConfig;
}

interface ParticleSOA {
    readonly positions: ISimdVec3;
    readonly velocities: ISimdVec3;
    readonly accelerations: ISimdVec3;
    readonly forces: ISimdVec3;
    readonly colors: ISimdVec4;
    readonly sizes: ISimdVec3;
    readonly rotations: ISimdVec3;
    readonly angularVelocities: ISimdVec3;
    readonly lifetimes: Float32Array;
    readonly normalizedLifetimes: Float32Array;
    readonly startLifetimes: Float32Array;
    readonly startSizes: ISimdVec3;
    readonly startColors: ISimdVec4;
    readonly startRotations: ISimdVec3;
    readonly startVelocities: ISimdVec3;
    readonly randomSeeds: Uint32Array;
    readonly customData1: ISimdVec4;
    readonly customData2: ISimdVec4;
    readonly textureData: ISimdVec4;
    readonly trailData: ISimdVec4;
    readonly userData: Float32Array;
    readonly active: Uint8Array;
    readonly sortKeys: Float32Array;
    readonly indices: Uint32Array;
    readonly birthTime: Float32Array;
    readonly emitterIndex: Uint16Array;
    readonly collisionFlags: Uint8Array;
    count: number;
    readonly capacity: number;
}

interface ISpatialCell {
    readonly particles: Uint32Array;
    count: number;
    readonly bounds: AABB3D;
    readonly centerMass: Vec3;
    density: number;
}

interface ISpatialGrid {
    readonly cells: Map<bigint, ISpatialCell>;
    readonly cellSize: number;
    readonly invCellSize: number;
    readonly dimensions: Vec3;
    readonly bounds: AABB3D;
    readonly maxParticlesPerCell: number;
}

interface IForceField {
    readonly type: number;
    readonly position: Vec3;
    readonly rotation: Vec3;
    readonly strength: number;
    readonly range: number;
    readonly falloff: ICurve;
    readonly enabled: boolean;
    readonly affectLifetime: boolean;
    readonly affectSize: boolean;
    readonly affectColor: boolean;
}

interface ICollisionEvent {
    readonly particleIndex: number;
    readonly position: Vec3;
    readonly velocity: Vec3;
    readonly normal: Vec3;
    readonly otherCollider: any;
}

