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
