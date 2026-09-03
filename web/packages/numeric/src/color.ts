import { Comparer, CompareResult, EqualityComparer, Equatable, ICloneable } from '@axrone/utility';
import { EPSILON, ensureFinite } from './common';
import { clamp, clamp01 } from './clamp';
import { Fnv1a32, IHasher, HashValue, IHashable } from '@axrone/hash';
import { rand } from '@axrone/random';

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
    let tt = t;
    if (tt < 0) tt += 1;
    if (tt > 1) tt -= 1;
    if (tt < 1 / 6) return p + (q - p) * 6 * tt;
    if (tt < 1 / 2) return q;
    if (tt < 2 / 3) return p + (q - p) * (2 / 3 - tt) * 6;
    return p;
};

const _hslToRgbRaw = (h: number, s: number, l: number): { r: number; g: number; b: number } => {
    if (s === 0) return { r: l, g: l, b: l };
    const q = l < 0.5 ? l * (1 + s) : l + s - l * s;
    const p = 2 * l - q;
    const hNorm = h / 360;
    return {
        r: _hueToRgb(p, q, hNorm + 1 / 3),
        g: _hueToRgb(p, q, hNorm),
        b: _hueToRgb(p, q, hNorm - 1 / 3),
    };
};

const _labToRgbRaw = (l: number, a: number, b: number): { r: number; g: number; b: number } => {
    const fy = (l + 16) / 116;
    const fx = a / 500 + fy;
    const fz = fy - b / 200;

    const xr = fx ** 3 > 0.008856 ? fx ** 3 : (116 * fx - 16) / 903.3;
    const yr = l > 8 ? fy ** 3 : l / 903.3;
    const zr = fz ** 3 > 0.008856 ? fz ** 3 : (116 * fz - 16) / 903.3;

    const x = xr * D65_X;
    const y = yr * D65_Y;
    const z = zr * D65_Z;

    return _xyzToLinearRgbRaw(x, y, z);
};

const _xyzToLinearRgbRaw = (x: number, y: number, z: number): { r: number; g: number; b: number } => {
    let r = x * 3.2406 + y * -1.5372 + z * -0.4986;
    let g = x * -0.9689 + y * 1.8758 + z * 0.0415;
    let b = x * 0.0557 + y * -0.204 + z * 1.057;
    return {
        r: _linearToSRGB(r),
        g: _linearToSRGB(g),
        b: _linearToSRGB(b),
    };
};

const D65_X = 0.95047;
const D65_Y = 1.0;
const D65_Z = 1.08883;

export class Color implements IColorLike, ICloneable<Color>, Equatable {
    private static _hasher = new Fnv1a32();

    constructor(
        public r: number = 0,
        public g: number = 0,
        public b: number = 0,
        public a: number = 1
    ) {
        // Validate inputs to catch NaN/Infinity early
        ensureFinite(r, 'Color.r');
        ensureFinite(g, 'Color.g');
        ensureFinite(b, 'Color.b');
        ensureFinite(a, 'Color.a');

        this.r = clamp01(r);
        this.g = clamp01(g);
        this.b = clamp01(b);
        this.a = clamp01(a);
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
    static readonly SILVER: Readonly<Color> = Object.freeze(
        new Color(0.7529411764705882, 0.7529411764705882, 0.7529411764705882, 1)
    );
    static readonly FUCHSIA: Readonly<Color> = Object.freeze(new Color(1, 0, 1, 1));

    static from<T extends IColorLike>(c: Readonly<T>): Color {
        return new Color(c.r, c.g, c.b, c.a ?? 1);
    }

    static copy<T extends IColorLike, V extends IColorLike>(source: Readonly<T>, out?: V): V {
        if (out) {
            out.r = source.r;
            out.g = source.g;
            out.b = source.b;
            if (source.a !== undefined) out.a = source.a;
            return out;
        }

        return { r: source.r, g: source.g, b: source.b, a: source.a ?? 1 } as V;
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

    static create(r: number = 0, g: number = 0, b: number = 0, a: number = 1): Color {
        return new Color(r, g, b, a);
    }

    static fromHex(hex: string): Color {
        const cleaned = hex.replace(/^#/, '').trim();

        if (cleaned.length !== 3 && cleaned.length !== 4 && cleaned.length !== 6 && cleaned.length !== 8) {
            throw new Error(
                `Invalid hex color format: must be 3, 4, 6, or 8 hex digits (got ${cleaned.length})`
            );
        }

        if (!/^[0-9A-Fa-f]+$/.test(cleaned)) {
            throw new Error('Invalid hex color format: contains non-hexadecimal characters');
        }

        let normalized = cleaned;
        if (normalized.length === 3) {
            normalized =
                normalized[0] + normalized[0] + normalized[1] + normalized[1] + normalized[2] + normalized[2];
        } else if (normalized.length === 4) {
            normalized =
                normalized[0] +
                normalized[0] +
                normalized[1] +
                normalized[1] +
                normalized[2] +
                normalized[2] +
                normalized[3] +
                normalized[3];
        }

        const parsed = parseInt(normalized, 16);

        if (normalized.length === 6) {
            return new Color(
                ((parsed >> 16) & 0xff) / 255,
                ((parsed >> 8) & 0xff) / 255,
                (parsed & 0xff) / 255,
                1
            );
        }

        return new Color(
            ((parsed >> 24) & 0xff) / 255,
            ((parsed >> 16) & 0xff) / 255,
            ((parsed >> 8) & 0xff) / 255,
            (parsed & 0xff) / 255
        );
    }

    /**
     * Creates a Color from RGB values in the 0-1 range.
     * For 0-255 range, use fromRGBBytes instead.
     */
    static fromRGB(r: number, g: number, b: number, a: number = 1): Color {
        return new Color(r, g, b, a);
    }

    /**
     * Creates a Color from RGB values in the 0-255 range.
     */
    static fromRGBBytes(r: number, g: number, b: number, a: number = 255): Color {
        return new Color(r / 255, g / 255, b / 255, a / 255);
    }

    static fromHSL(h: number, s: number, l: number, a: number = 1): Color {
        const hN = _mod(h, 360);
        const sN = clamp01(s);
        const lN = clamp01(l);
        const aN = clamp01(a);
        const { r, g, b } = _hslToRgbRaw(hN, sN, lN);
        return new Color(r, g, b, aN);
    }

    static fromHSV(h: number, s: number, v: number, a: number = 1): Color {
        h = _mod(h, 360);
        s = clamp01(s);
        v = clamp01(v);
        a = clamp01(a);

        const c = v * s;
        const x = c * (1 - Math.abs(((h / 60) % 2) - 1));
        const m = v - c;

        let r = 0,
            g = 0,
            b = 0;

        if (h < 60) {
            r = c;
            g = x;
            b = 0;
        } else if (h < 120) {
            r = x;
            g = c;
            b = 0;
        } else if (h < 180) {
            r = 0;
            g = c;
            b = x;
        } else if (h < 240) {
            r = 0;
            g = x;
            b = c;
        } else if (h < 300) {
            r = x;
            g = 0;
            b = c;
        } else {
            r = c;
            g = 0;
            b = x;
        }

        return new Color(r + m, g + m, b + m, a);
    }

    static fromCMYK(c: number, m: number, y: number, k: number, a: number = 1): Color {
        c = clamp01(c);
        m = clamp01(m);
        y = clamp01(y);
        k = clamp01(k);
        a = clamp01(a);

        const invK = 1 - k;
        const r = (1 - c) * invK;
        const g = (1 - m) * invK;
        const b = (1 - y) * invK;

        return new Color(r, g, b, a);
    }

    static fromLab(l: number, a: number, b: number, alpha: number = 1): Color {
        const { r, g, b: bb } = _labToRgbRaw(l, a, b);
        return new Color(clamp01(r), clamp01(g), clamp01(bb), clamp01(alpha));
    }

    static fromXYZ(x: number, y: number, z: number, alpha: number = 1): Color {
        const { r, g, b: bb } = _xyzToLinearRgbRaw(x, y, z);
        return new Color(clamp01(r), clamp01(g), clamp01(bb), clamp01(alpha));
    }

    static fromTemperature(kelvin: number, alpha: number = 1): Color {
        kelvin = clamp(kelvin, 1000, 40000);
        const temp = kelvin / 100;

        let r, g, b;

        if (temp <= 66) {
            r = 255;
            g = temp <= 19 ? 0 : 99.4708025861 * Math.log(temp - 10) - 161.1195681661;
            b =
                temp >= 66
                    ? 255
                    : temp <= 19
                      ? 0
                      : 138.5177312231 * Math.log(temp - 10) - 305.0447927307;
        } else {
            r = 329.698727446 * Math.pow(temp - 60, -0.1332047592);
            g = 288.1221695283 * Math.pow(temp - 60, -0.0755148492);
            b = 255;
        }

        return new Color(clamp01(r / 255), clamp01(g / 255), clamp01(b / 255), alpha);
    }

    static fromNamedColor(name: string): Color {
        const normalized = name.toLowerCase().replace(/\s+/g, '');
        const entry = NAMED_COLORS[normalized];
        if (!entry) {
            throw new Error(`Unknown color name: ${name}`);
        }
        return entry.clone();
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
        return Color._hasher
            .reset()
            .updateF32(this.r)
            .updateF32(this.g)
            .updateF32(this.b)
            .updateF32(this.a)
            .digest();
    }

    hashInto<H extends HashValue = any>(hasher: IHasher<H>): void {
        hasher.updateF32(this.r).updateF32(this.g).updateF32(this.b).updateF32(this.a);
    }

    toHSL<T extends IColorHSL>(out?: T): T {
        const r = this.r,
            g = this.g,
            b = this.b;

        const max = Math.max(r, g, b);
        const min = Math.min(r, g, b);
        const diff = max - min;

        const l = (max + min) / 2;
        let h = 0,
            s = 0;

        if (diff !== 0) {
            s = l > 0.5 ? diff / (2 - max - min) : diff / (max + min);

            switch (max) {
                case r:
                    h = (g - b) / diff + (g < b ? 6 : 0);
                    break;
                case g:
                    h = (b - r) / diff + 2;
                    break;
                case b:
                    h = (r - g) / diff + 4;
                    break;
            }
            h *= 60;
        }

        if (out) {
            out.h = h;
            out.s = s;
            out.l = l;
            out.a = this.a;
            return out;
        } else {
            return { h, s, l, a: this.a } as T;
        }
    }

    toHSV<T extends IColorHSV>(out?: T): T {
        const r = this.r,
            g = this.g,
            b = this.b;

        const max = Math.max(r, g, b);
        const min = Math.min(r, g, b);
        const diff = max - min;

        const v = max;
        const s = max === 0 ? 0 : diff / max;
        let h = 0;

        if (diff !== 0) {
            switch (max) {
                case r:
                    h = (g - b) / diff + (g < b ? 6 : 0);
                    break;
                case g:
                    h = (b - r) / diff + 2;
                    break;
                case b:
                    h = (r - g) / diff + 4;
                    break;
            }
            h *= 60;
        }

        if (out) {
            out.h = h;
            out.s = s;
            out.v = v;
            out.a = this.a;
            return out;
        } else {
            return { h, s, v, a: this.a } as T;
        }
    }

    toCMYK<T extends IColorCMYK>(out?: T): T {
        const k = 1 - Math.max(this.r, this.g, this.b);
        const invK = 1 - k;
        const c = k === 1 ? 0 : (1 - this.r - k) / invK;
        const m = k === 1 ? 0 : (1 - this.g - k) / invK;
        const y = k === 1 ? 0 : (1 - this.b - k) / invK;

        if (out) {
            out.c = c;
            out.m = m;
            out.y = y;
            out.k = k;
            out.a = this.a;
            return out;
        } else {
            return { c, m, y, k, a: this.a } as T;
        }
    }

    toXYZ<T extends IColorXYZ>(out?: T): T {
        const r = _sRGBToLinear(this.r);
        const g = _sRGBToLinear(this.g);
        const b = _sRGBToLinear(this.b);

        const x = r * 0.4124564 + g * 0.3575761 + b * 0.1804375;
        const y = r * 0.2126729 + g * 0.7151522 + b * 0.072175;
        const z = r * 0.0193339 + g * 0.119192 + b * 0.9503041;

        if (out) {
            out.x = x;
            out.y = y;
            out.z = z;
            out.alpha = this.a;
            return out;
        } else {
            return { x, y, z, alpha: this.a } as T;
        }
    }

    toLab<T extends IColorLab>(out?: T): T {
        const xyz = this.toXYZ();

        const xr = xyz.x / D65_X;
        const yr = xyz.y / D65_Y;
        const zr = xyz.z / D65_Z;

        const fx = xr > 0.008856 ? Math.pow(xr, 1 / 3) : (903.3 * xr + 16) / 116;
        const fy = yr > 0.008856 ? Math.pow(yr, 1 / 3) : (903.3 * yr + 16) / 116;
        const fz = zr > 0.008856 ? Math.pow(zr, 1 / 3) : (903.3 * zr + 16) / 116;

        const l = 116 * fy - 16;
        const a = 500 * (fx - fy);
        const b = 200 * (fy - fz);

        if (out) {
            out.l = l;
            out.a = a;
            out.b = b;
            out.alpha = this.a;
            return out;
        } else {
            return { l, a, b, alpha: this.a } as T;
        }
    }

    toHex(includeAlpha: boolean = false): string {
        const r = Math.round(this.r * 255);
        const g = Math.round(this.g * 255);
        const b = Math.round(this.b * 255);
        const a = Math.round(this.a * 255);

        const toHex = (n: number) => n.toString(16).padStart(2, '0').toUpperCase();

        if (includeAlpha) {
            return `#${toHex(r)}${toHex(g)}${toHex(b)}${toHex(a)}`;
        }

        return `#${toHex(r)}${toHex(g)}${toHex(b)}`;
    }

    toRGB(includeAlpha: boolean = false): string {
        const r = Math.round(this.r * 255);
        const g = Math.round(this.g * 255);
        const b = Math.round(this.b * 255);

        if (includeAlpha) {
            return `rgba(${r}, ${g}, ${b}, ${this.a})`;
        }

        return `rgb(${r}, ${g}, ${b})`;
    }

    toHSLString(): string {
        const hsl = this.toHSL();
        return `hsla(${Math.round(hsl.h)}, ${Math.round(hsl.s * 100)}%, ${Math.round(hsl.l * 100)}%, ${hsl.a})`;
    }

    toString(): string {
        return this.toHex(true);
    }

    static add<T extends IColorLike, U extends IColorLike, V extends IColorLike>(
        a: Readonly<T>,
        b: Readonly<U>,
        out?: V
    ): V {
        if (out) {
            out.r = clamp01(a.r + b.r);
            out.g = clamp01(a.g + b.g);
            out.b = clamp01(a.b + b.b);
            out.a = clamp01((a.a ?? 1) + (b.a ?? 1));
            return out;
        } else {
            return {
                r: clamp01(a.r + b.r),
                g: clamp01(a.g + b.g),
                b: clamp01(a.b + b.b),
                a: clamp01((a.a ?? 1) + (b.a ?? 1)),
            } as V;
        }
    }

    static subtract<T extends IColorLike, U extends IColorLike, V extends IColorLike>(
        a: Readonly<T>,
        b: Readonly<U>,
        out?: V
    ): V {
        if (out) {
            out.r = clamp01(a.r - b.r);
            out.g = clamp01(a.g - b.g);
            out.b = clamp01(a.b - b.b);
            out.a = clamp01((a.a ?? 1) - (b.a ?? 1));
            return out;
        } else {
            return {
                r: clamp01(a.r - b.r),
                g: clamp01(a.g - b.g),
                b: clamp01(a.b - b.b),
                a: clamp01((a.a ?? 1) - (b.a ?? 1)),
            } as V;
        }
    }

    static multiply<T extends IColorLike, U extends IColorLike, V extends IColorLike>(
        a: Readonly<T>,
        b: Readonly<U>,
        out?: V
    ): V {
        if (out) {
            out.r = a.r * b.r;
            out.g = a.g * b.g;
            out.b = a.b * b.b;
            out.a = (a.a ?? 1) * (b.a ?? 1);
            return out;
        } else {
            return {
                r: a.r * b.r,
                g: a.g * b.g,
                b: a.b * b.b,
                a: (a.a ?? 1) * (b.a ?? 1),
            } as V;
        }
    }

    static multiplyScalar<T extends IColorLike, V extends IColorLike>(
        a: Readonly<T>,
        scalar: number,
        out?: V
    ): V {
        if (out) {
            out.r = clamp01(a.r * scalar);
            out.g = clamp01(a.g * scalar);
            out.b = clamp01(a.b * scalar);
            out.a = a.a ?? 1;
            return out;
        } else {
            return {
                r: clamp01(a.r * scalar),
                g: clamp01(a.g * scalar),
                b: clamp01(a.b * scalar),
                a: a.a ?? 1,
            } as V;
        }
    }

    add<T extends IColorLike>(other: Readonly<T>): Color {
        this.r = clamp01(this.r + other.r);
        this.g = clamp01(this.g + other.g);
        this.b = clamp01(this.b + other.b);
        this.a = clamp01(this.a + (other.a ?? 1));
        return this;
    }

    subtract<T extends IColorLike>(other: Readonly<T>): Color {
        this.r = clamp01(this.r - other.r);
        this.g = clamp01(this.g - other.g);
        this.b = clamp01(this.b - other.b);
        this.a = clamp01(this.a - (other.a ?? 1));
        return this;
    }

    multiply<T extends IColorLike>(other: Readonly<T>): Color {
        this.r *= other.r;
        this.g *= other.g;
        this.b *= other.b;
        this.a *= other.a ?? 1;
        return this;
    }

    multiplyScalar(scalar: number): Color {
        this.r = clamp01(this.r * scalar);
        this.g = clamp01(this.g * scalar);
        this.b = clamp01(this.b * scalar);
        return this;
    }

    static lerp<T extends IColorLike, U extends IColorLike, V extends IColorLike>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out?: V
    ): V {
        const t1 = clamp(t, 0, 1);
        const aAlpha = a.a ?? 1;
        const bAlpha = b.a ?? 1;

        if (out) {
            out.r = a.r + (b.r - a.r) * t1;
            out.g = a.g + (b.g - a.g) * t1;
            out.b = a.b + (b.b - a.b) * t1;
            out.a = aAlpha + (bAlpha - aAlpha) * t1;
            return out;
        } else {
            return {
                r: a.r + (b.r - a.r) * t1,
                g: a.g + (b.g - a.g) * t1,
                b: a.b + (b.b - a.b) * t1,
                a: aAlpha + (bAlpha - aAlpha) * t1,
            } as V;
        }
    }

    static lerpHSL<T extends IColorLike, U extends IColorLike, V extends IColorLike>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out?: V
    ): V {
        const t1 = clamp(t, 0, 1);

        const hslA = a instanceof Color ? a.toHSL() : Color.from(a).toHSL();
        const hslB = b instanceof Color ? b.toHSL() : Color.from(b).toHSL();

        let dh = hslB.h - hslA.h;
        if (dh > 180) dh -= 360;
        if (dh < -180) dh += 360;

        const h = _mod(hslA.h + dh * t1, 360);
        const s = hslA.s + (hslB.s - hslA.s) * t1;
        const l = hslA.l + (hslB.l - hslA.l) * t1;
        const alpha = hslA.a + (hslB.a - hslA.a) * t1;

        if (out) {
            const hN = _mod(h, 360);
            const sN = clamp01(s);
            const lN = clamp01(l);
            const aN = clamp01(alpha);
            const { r, g, b: bb } = _hslToRgbRaw(hN, sN, lN);
            out.r = r;
            out.g = g;
            out.b = bb;
            out.a = aN;
            return out;
        }

        return Color.fromHSL(h, s, l, alpha) as unknown as V;
    }

    static lerpLab<T extends IColorLike, U extends IColorLike, V extends IColorLike>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out?: V
    ): V {
        const t1 = clamp(t, 0, 1);

        const labA = a instanceof Color ? a.toLab() : Color.from(a).toLab();
        const labB = b instanceof Color ? b.toLab() : Color.from(b).toLab();

        const l = labA.l + (labB.l - labA.l) * t1;
        const aVal = labA.a + (labB.a - labA.a) * t1;
        const bVal = labA.b + (labB.b - labA.b) * t1;
        const alpha = labA.alpha + (labB.alpha - labA.alpha) * t1;

        if (out) {
            const { r, g, b: bb } = _labToRgbRaw(l, aVal, bVal);
            out.r = clamp01(r);
            out.g = clamp01(g);
            out.b = clamp01(bb);
            out.a = clamp01(alpha);
            return out;
        }

        return Color.fromLab(l, aVal, bVal, alpha) as unknown as V;
    }

    static lighten<T extends IColorLike, U extends IColorLike>(
        color: Readonly<T>,
        amount: number,
        out?: U
    ): U {
        const hsl = color instanceof Color ? color.toHSL() : Color.from(color).toHSL();
        const lNew = clamp(hsl.l + amount, 0, 1);
        const hNew = _mod(hsl.h, 360);
        const sNew = clamp01(hsl.s);
        const aNew = clamp01(hsl.a);

        if (out) {
            const { r, g, b: bb } = _hslToRgbRaw(hNew, sNew, lNew);
            out.r = r;
            out.g = g;
            out.b = bb;
            out.a = aNew;
            return out;
        }

        return Color.fromHSL(hNew, sNew, lNew, aNew) as unknown as U;
    }

    static darken<T extends IColorLike, U extends IColorLike>(
        color: Readonly<T>,
        amount: number,
        out?: U
    ): U {
        return Color.lighten(color, -amount, out);
    }

    static saturate<T extends IColorLike, U extends IColorLike>(
        color: Readonly<T>,
        amount: number,
        out?: U
    ): U {
        const hsl = color instanceof Color ? color.toHSL() : Color.from(color).toHSL();
        const sNew = clamp(hsl.s + amount, 0, 1);
        const hNew = _mod(hsl.h, 360);
        const lNew = clamp01(hsl.l);
        const aNew = clamp01(hsl.a);

        if (out) {
            const { r, g, b: bb } = _hslToRgbRaw(hNew, sNew, lNew);
            out.r = r;
            out.g = g;
            out.b = bb;
            out.a = aNew;
            return out;
        }

        return Color.fromHSL(hNew, sNew, lNew, aNew) as unknown as U;
    }

    static desaturate<T extends IColorLike, U extends IColorLike>(
        color: Readonly<T>,
        amount: number,
        out?: U
    ): U {
        return Color.saturate(color, -amount, out);
    }

    static adjustHue<T extends IColorLike, U extends IColorLike>(
        color: Readonly<T>,
        degrees: number,
        out?: U
    ): U {
        const hsl = color instanceof Color ? color.toHSL() : Color.from(color).toHSL();
        const hNew = _mod(hsl.h + degrees, 360);
        const sNew = clamp01(hsl.s);
        const lNew = clamp01(hsl.l);
        const aNew = clamp01(hsl.a);

        if (out) {
            const { r, g, b: bb } = _hslToRgbRaw(hNew, sNew, lNew);
            out.r = r;
            out.g = g;
            out.b = bb;
            out.a = aNew;
            return out;
        }

        return Color.fromHSL(hNew, sNew, lNew, aNew) as unknown as U;
    }

    static invert<T extends IColorLike, U extends IColorLike>(color: Readonly<T>, out?: U): U {
        if (out) {
            out.r = 1 - color.r;
            out.g = 1 - color.g;
            out.b = 1 - color.b;
            out.a = color.a ?? 1;
            return out;
        } else {
            return {
                r: 1 - color.r,
                g: 1 - color.g,
                b: 1 - color.b,
                a: color.a ?? 1,
            } as U;
        }
    }

    /**
     * Converts a color to grayscale using Rec.601 coefficients (0.299R + 0.587G + 0.114B)
     * on **gamma-encoded sRGB** values. Use this for display-referred operations like
     * desaturation effects or rendering pipeline conversions.
     *
     * For WCAG contrast calculations or any physically-correct luminance measurement,
     * use {@link luminance} instead, which linearizes the color first.
     */
    static grayscale<T extends IColorLike, U extends IColorLike>(color: Readonly<T>, out?: U): U {
        const gray = 0.299 * color.r + 0.587 * color.g + 0.114 * color.b;

        if (out) {
            out.r = gray;
            out.g = gray;
            out.b = gray;
            out.a = color.a ?? 1;
            return out;
        } else {
            return {
                r: gray,
                g: gray,
                b: gray,
                a: color.a ?? 1,
            } as U;
        }
    }

    static blend<T extends IColorLike, U extends IColorLike, V extends IColorLike>(
        base: Readonly<T>,
        overlay: Readonly<U>,
        mode: ColorBlendMode,
        out?: V
    ): V {
        // HSL blend modes require full-color conversion
        if (
            mode === ColorBlendMode.HUE ||
            mode === ColorBlendMode.SATURATION ||
            mode === ColorBlendMode.COLOR ||
            mode === ColorBlendMode.LUMINOSITY
        ) {
            const baseHSL = base instanceof Color ? base.toHSL() : Color.from(base).toHSL();
            const overlayHSL =
                overlay instanceof Color ? overlay.toHSL() : Color.from(overlay).toHSL();

            let h: number, s: number, l: number;

            switch (mode) {
                case ColorBlendMode.HUE:
                    h = overlayHSL.h;
                    s = baseHSL.s;
                    l = baseHSL.l;
                    break;
                case ColorBlendMode.SATURATION:
                    h = baseHSL.h;
                    s = overlayHSL.s;
                    l = baseHSL.l;
                    break;
                case ColorBlendMode.COLOR:
                    h = overlayHSL.h;
                    s = overlayHSL.s;
                    l = baseHSL.l;
                    break;
                case ColorBlendMode.LUMINOSITY:
                    h = baseHSL.h;
                    s = baseHSL.s;
                    l = overlayHSL.l;
                    break;
            }

            const result = Color.fromHSL(h!, s!, l!, overlayHSL.a);

            if (out) {
                out.r = result.r;
                out.g = result.g;
                out.b = result.b;
                out.a = result.a;
                return out;
            } else {
                return { r: result.r, g: result.g, b: result.b, a: result.a } as V;
            }
        }

        // Per-channel blend modes
        const blendFunc = BLEND_FUNCTIONS[mode];
        if (!blendFunc) {
            throw new Error(`Unsupported blend mode: ${mode}`);
        }

        const r = blendFunc(base.r, overlay.r);
        const g = blendFunc(base.g, overlay.g);
        const b = blendFunc(base.b, overlay.b);
        const a = overlay.a ?? 1;

        if (out) {
            out.r = r;
            out.g = g;
            out.b = b;
            out.a = a;
            return out;
        } else {
            return { r, g, b, a } as V;
        }
    }

    /**
     * Computes the **photometric luminance** using Rec.709 coefficients (0.2126R + 0.7152G + 0.0722B)
     * on **linearized sRGB** values. This is the correct luminance for WCAG contrast ratio
     * calculations and any physically-meaningful brightness measurement.
     *
     * For display-referred grayscale conversion, use {@link grayscale} instead, which
     * works on gamma-encoded values directly.
     */
    static luminance<T extends IColorLike>(color: Readonly<T>): number {
        const r = _sRGBToLinear(color.r);
        const g = _sRGBToLinear(color.g);
        const b = _sRGBToLinear(color.b);

        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    static contrastRatio<T extends IColorLike, U extends IColorLike>(
        color1: Readonly<T>,
        color2: Readonly<U>
    ): number {
        const lum1 = Color.luminance(color1);
        const lum2 = Color.luminance(color2);

        const lighter = Math.max(lum1, lum2);
        const darker = Math.min(lum1, lum2);

        return (lighter + 0.05) / (darker + 0.05);
    }

    static distance<T extends IColorLike, U extends IColorLike>(
        a: Readonly<T>,
        b: Readonly<U>
    ): number {
        const dr = a.r - b.r;
        const dg = a.g - b.g;
        const db = a.b - b.b;
        const da = (a.a ?? 1) - (b.a ?? 1);

        return Math.sqrt(dr * dr + dg * dg + db * db + da * da);
    }

    static distanceLab<T extends IColorLike, U extends IColorLike>(
        a: Readonly<T>,
        b: Readonly<U>
    ): number {
        const labA = a instanceof Color ? a.toLab() : Color.from(a).toLab();
        const labB = b instanceof Color ? b.toLab() : Color.from(b).toLab();

        const dl = labA.l - labB.l;
        const da = labA.a - labB.a;
        const db = labA.b - labB.b;

        return Math.sqrt(dl * dl + da * da + db * db);
    }

    static isAccessible<T extends IColorLike, U extends IColorLike>(
        foreground: Readonly<T>,
        background: Readonly<U>,
        level: 'AA' | 'AAA' = 'AA'
    ): boolean {
        const ratio = Color.contrastRatio(foreground, background);
        return level === 'AA' ? ratio >= 4.5 : ratio >= 7;
    }

    static generateHarmony<T extends IColorLike>(
        baseColor: Readonly<T>,
        type: ColorHarmonyType,
        count?: number
    ): Color[] {
        const hsl = baseColor instanceof Color ? baseColor.toHSL() : Color.from(baseColor).toHSL();
        const colors: Color[] = [];

        switch (type) {
            case ColorHarmonyType.MONOCHROMATIC:
                for (let i = 0; i < (count ?? 5); i++) {
                    const lightness = clamp(hsl.l + (i - 2) * 0.15, 0, 1);
                    colors.push(Color.fromHSL(hsl.h, hsl.s, lightness, hsl.a));
                }
                break;

            case ColorHarmonyType.COMPLEMENTARY:
                colors.push(Color.from(baseColor));
                colors.push(Color.fromHSL(_mod(hsl.h + 180, 360), hsl.s, hsl.l, hsl.a));
                break;

            case ColorHarmonyType.SPLIT_COMPLEMENTARY:
                colors.push(Color.from(baseColor));
                colors.push(Color.fromHSL(_mod(hsl.h + 150, 360), hsl.s, hsl.l, hsl.a));
                colors.push(Color.fromHSL(_mod(hsl.h + 210, 360), hsl.s, hsl.l, hsl.a));
                break;

            case ColorHarmonyType.TRIADIC:
                colors.push(Color.from(baseColor));
                colors.push(Color.fromHSL(_mod(hsl.h + 120, 360), hsl.s, hsl.l, hsl.a));
                colors.push(Color.fromHSL(_mod(hsl.h + 240, 360), hsl.s, hsl.l, hsl.a));
                break;

            case ColorHarmonyType.TETRADIC:
                colors.push(Color.from(baseColor));
                colors.push(Color.fromHSL(_mod(hsl.h + 90, 360), hsl.s, hsl.l, hsl.a));
                colors.push(Color.fromHSL(_mod(hsl.h + 180, 360), hsl.s, hsl.l, hsl.a));
                colors.push(Color.fromHSL(_mod(hsl.h + 270, 360), hsl.s, hsl.l, hsl.a));
                break;

            case ColorHarmonyType.ANALOGOUS:
                for (let i = 0; i < (count ?? 5); i++) {
                    const hue = _mod(hsl.h + (i - 2) * 30, 360);
                    colors.push(Color.fromHSL(hue, hsl.s, hsl.l, hsl.a));
                }
                break;

            case ColorHarmonyType.SQUARE:
                colors.push(Color.from(baseColor));
                colors.push(Color.fromHSL(_mod(hsl.h + 90, 360), hsl.s, hsl.l, hsl.a));
                colors.push(Color.fromHSL(_mod(hsl.h + 180, 360), hsl.s, hsl.l, hsl.a));
                colors.push(Color.fromHSL(_mod(hsl.h + 270, 360), hsl.s, hsl.l, hsl.a));
                break;

            default:
                throw new Error(`Unsupported harmony type: ${type}`);
        }

        return colors;
    }

    static random(alpha: number = 1): Color {
        return new Color(rand.float(), rand.float(), rand.float(), alpha);
    }

    static randomHue(saturation: number = 1, lightness: number = 0.5, alpha: number = 1): Color {
        return Color.fromHSL(rand.float() * 360, saturation, lightness, alpha);
    }

    static randomPastel(alpha: number = 1): Color {
        return Color.fromHSL(
            rand.float() * 360,
            0.3 + rand.float() * 0.4,
            0.7 + rand.float() * 0.3,
            alpha
        );
    }

    static randomVibrant(alpha: number = 1): Color {
        return Color.fromHSL(
            rand.float() * 360,
            0.8 + rand.float() * 0.2,
            0.4 + rand.float() * 0.3,
            alpha
        );
    }

    lighten(amount: number): Color {
        Color.lighten(this, amount, this);
        return this;
    }

    darken(amount: number): Color {
        Color.darken(this, amount, this);
        return this;
    }

    saturate(amount: number): Color {
        Color.saturate(this, amount, this);
        return this;
    }

    desaturate(amount: number): Color {
        Color.desaturate(this, amount, this);
        return this;
    }

    adjustHue(degrees: number): Color {
        Color.adjustHue(this, degrees, this);
        return this;
    }

    invert(): Color {
        Color.invert(this, this);
        return this;
    }

    grayscale(): Color {
        Color.grayscale(this, this);
        return this;
    }

    luminance(): number {
        return Color.luminance(this);
    }

    contrastRatio<T extends IColorLike>(other: Readonly<T>): number {
        return Color.contrastRatio(this, other);
    }

    distance<T extends IColorLike>(other: Readonly<T>): number {
        return Color.distance(this, other);
    }

    isAccessible<T extends IColorLike>(
        background: Readonly<T>,
        level: 'AA' | 'AAA' = 'AA'
    ): boolean {
        return Color.isAccessible(this, background, level);
    }
}

type BlendFn = (a: number, b: number) => number;

const NAMED_COLORS: Readonly<Record<string, Readonly<Color>>> = Object.freeze({
    transparent: Color.TRANSPARENT,
    black: Color.BLACK,
    white: Color.WHITE,
    red: Color.RED,
    green: Color.GREEN,
    blue: Color.BLUE,
    yellow: Color.YELLOW,
    cyan: Color.CYAN,
    magenta: Color.MAGENTA,
    orange: Color.ORANGE,
    purple: Color.PURPLE,
    brown: Color.BROWN,
    pink: Color.PINK,
    gray: Color.GRAY,
    grey: Color.GRAY,
    lightgray: Color.LIGHT_GRAY,
    lightgrey: Color.LIGHT_GRAY,
    darkgray: Color.DARK_GRAY,
    darkgrey: Color.DARK_GRAY,
    navy: Color.NAVY,
    maroon: Color.MAROON,
    olive: Color.OLIVE,
    lime: Color.LIME,
    aqua: Color.AQUA,
    teal: Color.TEAL,
    silver: Color.SILVER,
    fuchsia: Color.FUCHSIA,
});

const BLEND_FUNCTIONS: Readonly<Record<ColorBlendMode, BlendFn>> = Object.freeze({
    [ColorBlendMode.NORMAL]: (_a, b) => b,
    [ColorBlendMode.MULTIPLY]: (a, b) => a * b,
    [ColorBlendMode.SCREEN]: (a, b) => 1 - (1 - a) * (1 - b),
    [ColorBlendMode.OVERLAY]: (a, b) => (a < 0.5 ? 2 * a * b : 1 - 2 * (1 - a) * (1 - b)),
    [ColorBlendMode.SOFT_LIGHT]: (a, b) =>
        b < 0.5 ? 2 * a * b + a * a * (1 - 2 * b) : 2 * a * (1 - b) + Math.sqrt(a) * (2 * b - 1),
    [ColorBlendMode.HARD_LIGHT]: (a, b) => (b < 0.5 ? 2 * a * b : 1 - 2 * (1 - a) * (1 - b)),
    [ColorBlendMode.COLOR_DODGE]: (a, b) => (b === 1 ? 1 : Math.min(1, a / (1 - b))),
    [ColorBlendMode.COLOR_BURN]: (a, b) => (b === 0 ? 0 : Math.max(0, 1 - (1 - a) / b)),
    [ColorBlendMode.DARKEN]: (a, b) => Math.min(a, b),
    [ColorBlendMode.LIGHTEN]: (a, b) => Math.max(a, b),
    [ColorBlendMode.DIFFERENCE]: (a, b) => Math.abs(a - b),
    [ColorBlendMode.EXCLUSION]: (a, b) => a + b - 2 * a * b,
    // HSL blend modes handled in blend() method
    [ColorBlendMode.HUE]: (_a, _b) => _b,
    [ColorBlendMode.SATURATION]: (_a, _b) => _b,
    [ColorBlendMode.COLOR]: (_a, _b) => _b,
    [ColorBlendMode.LUMINOSITY]: (_a, _b) => _b,
});

export class ColorComparer implements Comparer<Color> {
    private readonly mode: ColorComparisonMode;

    constructor(mode: ColorComparisonMode = ColorComparisonMode.LUMINANCE) {
        this.mode = mode;
    }

    compare(a: Readonly<Color>, b: Readonly<Color>): CompareResult {
        switch (this.mode) {
            case ColorComparisonMode.LUMINANCE: {
                const lumA = Color.luminance(a);
                const lumB = Color.luminance(b);
                if (Math.abs(lumA - lumB) < EPSILON) return 0;
                return lumA < lumB ? -1 : 1;
            }

            case ColorComparisonMode.HUE: {
                const hslA = a.toHSL();
                const hslB = b.toHSL();
                if (Math.abs(hslA.h - hslB.h) < EPSILON) return 0;
                return hslA.h < hslB.h ? -1 : 1;
            }

            case ColorComparisonMode.SATURATION: {
                const hslA = a.toHSL();
                const hslB = b.toHSL();
                if (Math.abs(hslA.s - hslB.s) < EPSILON) return 0;
                return hslA.s < hslB.s ? -1 : 1;
            }

            case ColorComparisonMode.RGB_DISTANCE: {
                const distA = Math.sqrt(a.r * a.r + a.g * a.g + a.b * a.b);
                const distB = Math.sqrt(b.r * b.r + b.g * b.g + b.b * b.b);
                if (Math.abs(distA - distB) < EPSILON) return 0;
                return distA < distB ? -1 : 1;
            }

            case ColorComparisonMode.LAB_DISTANCE: {
                const labA = a.toLab();
                const labB = b.toLab();
                const distA = Math.sqrt(labA.l * labA.l + labA.a * labA.a + labA.b * labA.b);
                const distB = Math.sqrt(labB.l * labB.l + labB.a * labB.a + labB.b * labB.b);
                if (Math.abs(distA - distB) < EPSILON) return 0;
                return distA < distB ? -1 : 1;
            }

            case ColorComparisonMode.ALPHA: {
                if (Math.abs(a.a - b.a) < EPSILON) return 0;
                return a.a < b.a ? -1 : 1;
            }

            default:
                throw new Error(`Unsupported color comparison mode: ${this.mode}`);
        }
    }
}

export class ColorEqualityComparer implements EqualityComparer<Color> {
    private readonly epsilon: number;

    constructor(epsilon: number = EPSILON) {
        this.epsilon = epsilon;
    }

    equals(a: Readonly<Color>, b: Readonly<Color>): boolean {
        if (a === b) return true;
        if (!a || !b) return false;

        return (
            Math.abs(a.r - b.r) < this.epsilon &&
            Math.abs(a.g - b.g) < this.epsilon &&
            Math.abs(a.b - b.b) < this.epsilon &&
            Math.abs(a.a - b.a) < this.epsilon
        );
    }

    hash(obj: Readonly<Color>): number {
        if (!obj) return 0;
        return obj.getHashCode();
    }
}
