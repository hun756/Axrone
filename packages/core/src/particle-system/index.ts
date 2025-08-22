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

