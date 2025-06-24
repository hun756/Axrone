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

const _clamp = (value: number, min: number = 0, max: number = 1): number => {
    return Math.max(min, Math.min(max, value));
};

const _clampColor = (value: number): number => {
    return Math.max(0, Math.min(1, value));
};

const _mod = (n: number, m: number): number => {
    return ((n % m) + m) % m;
};

const _sRGBToLinear = (c: number): number => {
    if (c <= 0.04045) {
        return c / 12.92;
    }
    return Math.pow((c + 0.055) / 1.055, 2.4);
};

const _linearToSRGB = (c: number): number => {
    if (c <= 0.0031308) {
        return 12.92 * c;
    }
    return 1.055 * Math.pow(c, 1.0 / 2.4) - 0.055;
};

const _hueToRgb = (p: number, q: number, t: number): number => {
    if (t < 0) t += 1;
    if (t > 1) t -= 1;
    if (t < 1 / 6) return p + (q - p) * 6 * t;
    if (t < 1 / 2) return q;
    if (t < 2 / 3) return p + (q - p) * (2 / 3 - t) * 6;
    return p;
};

const D65_X = 0.95047;
const D65_Y = 1.0;
const D65_Z = 1.08883;

