export interface IColorLike {
    r: number;
    g: number;
    b: number;
    a?: number;
}

export interface IColorHSL {
    h: number; // [0, 360)
    s: number; // [0, 1]
    l: number; // [0, 1]
    a: number; // [0, 1]
}

export interface IColorHSV {
    h: number; // [0, 360)
    s: number; // [0, 1]
    v: number; // [0, 1]
    a: number; // [0, 1]
}

export interface IColorCMYK {
    c: number; // [0, 1]
    m: number; // [0, 1]
    y: number; // [0, 1]
    k: number; // [0, 1]
    a: number; // [0, 1]
}

export interface IColorLab {
    l: number; // [0, 100]
    a: number; // [-128, 127]
    b: number; // [-128, 127]
    alpha: number; // [0, 1]
}

export interface IColorXYZ {
    x: number; // [0, 1]
    y: number; // [0, 1]
    z: number; // [0, 1]
    alpha: number; // [0, 1]
}

export enum ColorBlendMode {
    NORMAL,
    MULTIPLY,
    SCREEN,
    OVERLAY,
    SOFT_LIGHT,
    HARD_LIGHT,
    COLOR_DODGE,
    COLOR_BURN,
    DARKEN,
    LIGHTEN,
    DIFFERENCE,
    EXCLUSION,
    HUE,
    SATURATION,
    COLOR,
    LUMINOSITY,
}

export enum ColorHarmonyType {
    MONOCHROMATIC,
    COMPLEMENTARY,
    SPLIT_COMPLEMENTARY,
    TRIADIC,
    TETRADIC,
    ANALOGOUS,
    SQUARE,
}

export enum ColorComparisonMode {
    LUMINANCE,
    HUE,
    SATURATION,
    RGB_DISTANCE,
    LAB_DISTANCE,
    ALPHA,
}
