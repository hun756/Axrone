import { Equatable, ICloneable } from '@axrone/utility';
import { EPSILON } from './common';

interface IColorLike {
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

const _clamp = (value: number, min: number = 0, max: number = 1): number => {
    return Math.max(min, Math.min(max, value));
};

const _clampColor = (value: number): number => {
    return Math.max(0, Math.min(1, value));
};

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
    static readonly GRAY: Readonly<Color> = Object.freeze(new Color(0.5, 0.5, 0.5, 1));
    static readonly LIGHT_GRAY: Readonly<Color> = Object.freeze(new Color(0.75, 0.75, 0.75, 1));
    static readonly DARK_GRAY: Readonly<Color> = Object.freeze(new Color(0.25, 0.25, 0.25, 1));

    static from<T extends IColorLike>(c: T): Color {
        return new Color(c.r, c.g, c.b, c.a);
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

    static fromHex(hex: string): Color {
        hex = hex.replace(/^#/, '');

        if (hex.length === 3) {
            hex = hex[0] + hex[0] + hex[1] + hex[1] + hex[2] + hex[2];
        } else if (hex.length === 4) {
            hex = hex[0] + hex[0] + hex[1] + hex[1] + hex[2] + hex[2] + hex[3] + hex[3];
        }

        const parsed = parseInt(hex, 16);

        if (hex.length === 6) {
            return new Color(
                ((parsed >> 16) & 0xff) / 255,
                ((parsed >> 8) & 0xff) / 255,
                (parsed & 0xff) / 255,
                1
            );
        } else if (hex.length === 8) {
            return new Color(
                ((parsed >> 24) & 0xff) / 255,
                ((parsed >> 16) & 0xff) / 255,
                ((parsed >> 8) & 0xff) / 255,
                (parsed & 0xff) / 255
            );
        }

        throw new Error('Invalid hex color format');
    }

    static fromRGB(r: number, g: number, b: number, a: number = 1): Color {
        if (r > 1 || g > 1 || b > 1) {
            return new Color(r / 255, g / 255, b / 255, a);
        }
        return new Color(r, g, b, a);
    }

    static fromHSL(h: number, s: number, l: number, a: number = 1): Color {
        h = ((h % 360) + 360) % 360;
        s = _clampColor(s);
        l = _clampColor(l);
        a = _clampColor(a);

        if (s === 0) {
            return new Color(l, l, l, a);
        }

        const c = (1 - Math.abs(2 * l - 1)) * s;
        const x = c * (1 - Math.abs(((h / 60) % 2) - 1));
        const m = l - c / 2;

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

    static fromHSV(h: number, s: number, v: number, a: number = 1): Color {
        h = ((h % 360) + 360) % 360;
        s = _clampColor(s);
        v = _clampColor(v);
        a = _clampColor(a);

        const f = h / 60;
        const i = Math.floor(f);
        const p = v * (1 - s);
        const q = v * (1 - s * (f - i));
        const t = v * (1 - s * (1 - (f - i)));

        let r = 0,
            g = 0,
            b = 0;

        switch (i % 6) {
            case 0:
                r = v;
                g = t;
                b = p;
                break;
            case 1:
                r = q;
                g = v;
                b = p;
                break;
            case 2:
                r = p;
                g = v;
                b = t;
                break;
            case 3:
                r = p;
                g = q;
                b = v;
                break;
            case 4:
                r = t;
                g = p;
                b = v;
                break;
            case 5:
                r = v;
                g = p;
                b = q;
                break;
        }

        return new Color(r, g, b, a);
    }

    static fromCMYK(c: number, m: number, y: number, k: number, a: number = 1): Color {
        c = _clampColor(c);
        m = _clampColor(m);
        y = _clampColor(y);
        k = _clampColor(k);
        a = _clampColor(a);

        const r = 1 - Math.min(1, c * (1 - k) + k);
        const g = 1 - Math.min(1, m * (1 - k) + k);
        const b = 1 - Math.min(1, y * (1 - k) + k);

        return new Color(r, g, b, a);
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
        h1 = Math.imul(h1 ^ ~~(this.r * 1000), 16777619);
        h1 = Math.imul(h1 ^ ~~(this.g * 1000), 16777619);
        h1 = Math.imul(h1 ^ ~~(this.b * 1000), 16777619);
        h1 = Math.imul(h1 ^ ~~(this.a * 1000), 16777619);
        return h1 >>> 0;
    }

    clone(): Color {
        return new Color(this.r, this.g, this.b, this.a);
    }

    // Color space conversions
    toHSL(out?: IColorHSL): IColorHSL {
        const r = this.r,
            g = this.g,
            b = this.b;

        const max = Math.max(r, g, b);
        const min = Math.min(r, g, b);
        const diff = max - min;

        let h = 0;
        const l = (max + min) / 2;
        const s = diff === 0 ? 0 : diff / (1 - Math.abs(2 * l - 1));

        if (diff !== 0) {
            if (max === r) {
                h = 60 * (((g - b) / diff + 6) % 6);
            } else if (max === g) {
                h = 60 * ((b - r) / diff + 2);
            } else {
                h = 60 * ((r - g) / diff + 4);
            }
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

        let h = 0;
        const s = max === 0 ? 0 : diff / max;
        const v = max;

        if (diff !== 0) {
            if (max === r) {
                h = 60 * (((g - b) / diff + 6) % 6);
            } else if (max === g) {
                h = 60 * ((b - r) / diff + 2);
            } else {
                h = 60 * ((r - g) / diff + 4);
            }
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
}
