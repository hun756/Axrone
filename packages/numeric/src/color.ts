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

    static create(r: number = 0, g: number = 0, b: number = 0, a: number = 1): Color {
        return new Color(r, g, b, a);
    }

    static fromHex(hex: string): Color {
        hex = hex.replace(/^#/, '').trim();

        if (!/^[0-9A-Fa-f]+$/.test(hex)) {
            throw new Error('Invalid hex color format: contains non-hexadecimal characters');
        }

        if (hex.length === 3) {
            hex = hex[0] + hex[0] + hex[1] + hex[1] + hex[2] + hex[2];
        } else if (hex.length === 4) {
            hex = hex[0] + hex[0] + hex[1] + hex[1] + hex[2] + hex[2] + hex[3] + hex[3];
        }

        if (hex.length !== 6 && hex.length !== 8) {
            throw new Error('Invalid hex color format: must be 3, 4, 6, or 8 characters');
        }

        const parsed = parseInt(hex, 16);

        if (hex.length === 6) {
            return new Color(
                ((parsed >> 16) & 0xff) / 255,
                ((parsed >> 8) & 0xff) / 255,
                (parsed & 0xff) / 255,
                1
            );
        } else {
            return new Color(
                ((parsed >> 24) & 0xff) / 255,
                ((parsed >> 16) & 0xff) / 255,
                ((parsed >> 8) & 0xff) / 255,
                (parsed & 0xff) / 255
            );
        }
    }

    static fromRGB(r: number, g: number, b: number, a: number = 1): Color {
        if (r > 1 || g > 1 || b > 1) {
            return new Color(r / 255, g / 255, b / 255, a > 1 ? a / 255 : a);
        }
        return new Color(r, g, b, a);
    }

    static fromHSL(h: number, s: number, l: number, a: number = 1): Color {
        h = _mod(h, 360);
        s = _clampColor(s);
        l = _clampColor(l);
        a = _clampColor(a);

        if (s === 0) {
            return new Color(l, l, l, a);
        }

        const q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        const p = 2 * l - q;
        const hNorm = h / 360;

        const r = _hueToRgb(p, q, hNorm + 1 / 3);
        const g = _hueToRgb(p, q, hNorm);
        const b = _hueToRgb(p, q, hNorm - 1 / 3);

        return new Color(r, g, b, a);
    }

    static fromHSV(h: number, s: number, v: number, a: number = 1): Color {
        h = _mod(h, 360);
        s = _clampColor(s);
        v = _clampColor(v);
        a = _clampColor(a);

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
        c = _clampColor(c);
        m = _clampColor(m);
        y = _clampColor(y);
        k = _clampColor(k);
        a = _clampColor(a);

        const invK = 1 - k;
        const r = (1 - c) * invK;
        const g = (1 - m) * invK;
        const b = (1 - y) * invK;

        return new Color(r, g, b, a);
    }

    static fromLab(l: number, a: number, b: number, alpha: number = 1): Color {
        const fy = (l + 16) / 116;
        const fx = a / 500 + fy;
        const fz = fy - b / 200;

        const xr = fx ** 3 > 0.008856 ? fx ** 3 : (116 * fx - 16) / 903.3;
        const yr = l > 8 ? fy ** 3 : l / 903.3;
        const zr = fz ** 3 > 0.008856 ? fz ** 3 : (116 * fz - 16) / 903.3;

        const x = xr * D65_X;
        const y = yr * D65_Y;
        const z = zr * D65_Z;

        return Color.fromXYZ(x, y, z, alpha);
    }

    static fromXYZ(x: number, y: number, z: number, alpha: number = 1): Color {
        let r = x * 3.2406 + y * -1.5372 + z * -0.4986;
        let g = x * -0.9689 + y * 1.8758 + z * 0.0415;
        let b = x * 0.0557 + y * -0.204 + z * 1.057;

        r = _linearToSRGB(r);
        g = _linearToSRGB(g);
        b = _linearToSRGB(b);

        return new Color(_clampColor(r), _clampColor(g), _clampColor(b), _clampColor(alpha));
    }

    static fromTemperature(kelvin: number, alpha: number = 1): Color {
        kelvin = _clamp(kelvin, 1000, 40000);
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

        return new Color(_clampColor(r / 255), _clampColor(g / 255), _clampColor(b / 255), alpha);
    }

    static fromNamedColor(name: string): Color {
        const namedColors: Record<string, Color> = {
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
        };

        const normalized = name.toLowerCase().replace(/\s+/g, '');
        if (!(normalized in namedColors)) {
            throw new Error(`Unknown color name: ${name}`);
        }

        return namedColors[normalized].clone();
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

    toHSL(out?: IColorHSL): IColorHSL {
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
            return { h, s, l, a: this.a };
        }
    }

    toHSV(out?: IColorHSV): IColorHSV {
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
            return { h, s, v, a: this.a };
        }
    }

    toCMYK(out?: IColorCMYK): IColorCMYK {
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
            return { c, m, y, k, a: this.a };
        }
    }

    toXYZ(out?: IColorXYZ): IColorXYZ {
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
            return { x, y, z, alpha: this.a };
        }
    }

    toLab(out?: IColorLab): IColorLab {
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
            return { l, a, b, alpha: this.a };
        }
    }

    toHex(includeAlpha: boolean = false): string {
        const r = Math.round(this.r * 255);
        const g = Math.round(this.g * 255);
        const b = Math.round(this.b * 255);
        const a = Math.round(this.a * 255);

        const toHex = (n: number) => n.toString(16).padStart(2, '0');

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
            out.r = _clampColor(a.r + b.r);
            out.g = _clampColor(a.g + b.g);
            out.b = _clampColor(a.b + b.b);
            out.a = _clampColor((a.a ?? 1) + (b.a ?? 1));
            return out;
        } else {
            return {
                r: _clampColor(a.r + b.r),
                g: _clampColor(a.g + b.g),
                b: _clampColor(a.b + b.b),
                a: _clampColor((a.a ?? 1) + (b.a ?? 1)),
            } as V;
        }
    }

    static subtract<T extends IColorLike, U extends IColorLike, V extends IColorLike>(
        a: Readonly<T>,
        b: Readonly<U>,
        out?: V
    ): V {
        if (out) {
            out.r = _clampColor(a.r - b.r);
            out.g = _clampColor(a.g - b.g);
            out.b = _clampColor(a.b - b.b);
            out.a = _clampColor((a.a ?? 1) - (b.a ?? 1));
            return out;
        } else {
            return {
                r: _clampColor(a.r - b.r),
                g: _clampColor(a.g - b.g),
                b: _clampColor(a.b - b.b),
                a: _clampColor((a.a ?? 1) - (b.a ?? 1)),
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
            out.r = _clampColor(a.r * scalar);
            out.g = _clampColor(a.g * scalar);
            out.b = _clampColor(a.b * scalar);
            out.a = a.a ?? 1;
            return out;
        } else {
            return {
                r: _clampColor(a.r * scalar),
                g: _clampColor(a.g * scalar),
                b: _clampColor(a.b * scalar),
                a: a.a ?? 1,
            } as V;
        }
    }

    add<T extends IColorLike>(other: Readonly<T>): Color {
        this.r = _clampColor(this.r + other.r);
        this.g = _clampColor(this.g + other.g);
        this.b = _clampColor(this.b + other.b);
        this.a = _clampColor(this.a + (other.a ?? 1));
        return this;
    }

    subtract<T extends IColorLike>(other: Readonly<T>): Color {
        this.r = _clampColor(this.r - other.r);
        this.g = _clampColor(this.g - other.g);
        this.b = _clampColor(this.b - other.b);
        this.a = _clampColor(this.a - (other.a ?? 1));
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
        this.r = _clampColor(this.r * scalar);
        this.g = _clampColor(this.g * scalar);
        this.b = _clampColor(this.b * scalar);
        return this;
    }
}
