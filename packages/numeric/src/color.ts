import { Equatable, ICloneable } from '@axrone/utility';
import { EPSILON } from './common';

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

export class Color implements IColorLike, ICloneable<Color>, Equatable {
    constructor(
        public r: number = 0,
        public g: number = 0,
        public b: number = 0,
        public a: number = 1
    ) {
        this.r = _clampColor(r);
        this.g = _clampColor(g);
        this.b = _clampColor(b);
        this.a = _clampColor(a);
    }

    static readonly TRANSPARENT: Readonly<Color> = Object.freeze(new Color(0, 0, 0, 0));
    static readonly BLACK: Readonly<Color> = Object.freeze(new Color(0, 0, 0, 1));
    static readonly WHITE: Readonly<Color> = Object.freeze(new Color(1, 1, 1, 1));
    static readonly RED: Readonly<Color> = Object.freeze(new Color(1, 0, 0, 1));
    static readonly GREEN: Readonly<Color> = Object.freeze(new Color(0, 1, 0, 1));
    static readonly BLUE: Readonly<Color> = Object.freeze(new Color(0, 0, 1, 1));
    static readonly YELLOW: Readonly<Color> = Object.freeze(new Color(1, 1, 0, 1));
    static readonly CYAN: Readonly<Color> = Object.freeze(new Color(0, 1, 1, 1));
    static readonly MAGENTA: Readonly<Color> = Object.freeze(new Color(1, 0, 1, 1));
    static readonly ORANGE: Readonly<Color> = Object.freeze(new Color(1, 0.5, 0, 1));
    static readonly PURPLE: Readonly<Color> = Object.freeze(new Color(0.5, 0, 0.5, 1));
    static readonly BROWN: Readonly<Color> = Object.freeze(new Color(0.6, 0.4, 0.2, 1));
    static readonly PINK: Readonly<Color> = Object.freeze(new Color(1, 0.75, 0.8, 1));
    static readonly GRAY: Readonly<Color> = Object.freeze(new Color(0.5, 0.5, 0.5, 1));
    static readonly LIGHT_GRAY: Readonly<Color> = Object.freeze(new Color(0.75, 0.75, 0.75, 1));
    static readonly DARK_GRAY: Readonly<Color> = Object.freeze(new Color(0.25, 0.25, 0.25, 1));
    static readonly NAVY: Readonly<Color> = Object.freeze(new Color(0, 0, 0.5, 1));
    static readonly MAROON: Readonly<Color> = Object.freeze(new Color(0.5, 0, 0, 1));
    static readonly OLIVE: Readonly<Color> = Object.freeze(new Color(0.5, 0.5, 0, 1));
    static readonly LIME: Readonly<Color> = Object.freeze(new Color(0.5, 1, 0, 1));
    static readonly AQUA: Readonly<Color> = Object.freeze(new Color(0, 1, 1, 1));
    static readonly TEAL: Readonly<Color> = Object.freeze(new Color(0, 0.5, 0.5, 1));
    static readonly SILVER: Readonly<Color> = Object.freeze(new Color(0.75, 0.75, 0.75, 1));
    static readonly FUCHSIA: Readonly<Color> = Object.freeze(new Color(1, 0, 1, 1));

    static from<T extends IColorLike>(c: Readonly<T>): Color {
        return new Color(c.r, c.g, c.b, c.a ?? 1);
    }

    static fromArray(arr: ArrayLike<number>, offset: number = 0): Color {
        if (offset < 0) {
            throw new RangeError('Offset cannot be negative');
        }

        if (arr.length < offset + 3) {
            throw new RangeError(
                `Array must have at least ${offset + 3} elements when using offset ${offset} for RGB`
            );
        }

        const a = arr.length >= offset + 4 ? Number(arr[offset + 3]) : 1;
        return new Color(Number(arr[offset]), Number(arr[offset + 1]), Number(arr[offset + 2]), a);
    }

    clone(): Color {
        return new Color(this.r, this.g, this.b, this.a);
    }

    equals(other: unknown): boolean {
        if (!(other instanceof Color)) return false;

        return (
            Math.abs(this.r - other.r) < EPSILON &&
            Math.abs(this.g - other.g) < EPSILON &&
            Math.abs(this.b - other.b) < EPSILON &&
            Math.abs(this.a - other.a) < EPSILON
        );
    }

    getHashCode(): number {
        let h1 = 2166136261;
        h1 = Math.imul(h1 ^ Math.floor(this.r * 1000), 16777619);
        h1 = Math.imul(h1 ^ Math.floor(this.g * 1000), 16777619);
        h1 = Math.imul(h1 ^ Math.floor(this.b * 1000), 16777619);
        h1 = Math.imul(h1 ^ Math.floor(this.a * 1000), 16777619);
        return h1 >>> 0;
    }
}
