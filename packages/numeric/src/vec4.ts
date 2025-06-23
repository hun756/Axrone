import { Comparer, CompareResult, EqualityComparer, Equatable, ICloneable } from '@axrone/utility';
import { EPSILON, HALF_PI, PI_2, standardNormalDist } from './common';

export interface IVec4Like {
    x: number;
    y: number;
    z: number;
    w: number;
}

const _boundedNormalRandom = (): number => {
    const MAX_ATTEMPTS = 10;
    for (let i = 0; i < MAX_ATTEMPTS; i++) {
        const value = standardNormalDist.sample();
        if (value >= -1 && value <= 1) {
            return value;
        }
    }
    return Math.max(-1, Math.min(1, standardNormalDist.sample()));
};

const _normalRandom = (): number => _boundedNormalRandom();

export class Vec4 implements IVec4Like, ICloneable<Vec4>, Equatable {
    constructor(
        public x: number = 0,
        public y: number = 0,
        public z: number = 0,
        public w: number = 0
    ) {}

    static readonly ZERO: Readonly<Vec4> = Object.freeze(new Vec4(0, 0, 0, 0));
    static readonly ONE: Readonly<Vec4> = Object.freeze(new Vec4(1, 1, 1, 1));
    static readonly NEG_ONE: Readonly<Vec4> = Object.freeze(new Vec4(-1, -1, -1, -1));
    static readonly UNIT_X: Readonly<Vec4> = Object.freeze(new Vec4(1, 0, 0, 0));
    static readonly UNIT_Y: Readonly<Vec4> = Object.freeze(new Vec4(0, 1, 0, 0));
    static readonly UNIT_Z: Readonly<Vec4> = Object.freeze(new Vec4(0, 0, 1, 0));
    static readonly UNIT_W: Readonly<Vec4> = Object.freeze(new Vec4(0, 0, 0, 1));

    static from<T extends IVec4Like>(v: Readonly<T>): Vec4 {
        return new Vec4(v.x, v.y, v.z, v.w);
    }

    static fromArray(arr: ArrayLike<number>, offset: number = 0): Vec4 {
        if (offset < 0) {
            throw new RangeError('Offset cannot be negative');
        }

        if (arr.length < offset + 4) {
            throw new RangeError(
                `Array must have at least ${offset + 4} elements when using offset ${offset}`
            );
        }

        return new Vec4(
            Number(arr[offset]),
            Number(arr[offset + 1]),
            Number(arr[offset + 2]),
            Number(arr[offset + 3])
        );
    }

    static create(x: number = 0, y: number = 0, z: number = 0, w: number = 0): Vec4 {
        return new Vec4(x, y, z, w);
    }

    clone(): Vec4 {
        return new Vec4(this.x, this.y, this.z, this.w);
    }

    equals(other: unknown): boolean {
        if (!(other instanceof Vec4)) return false;

        return (
            Math.abs(this.x - other.x) < EPSILON &&
            Math.abs(this.y - other.y) < EPSILON &&
            Math.abs(this.z - other.z) < EPSILON &&
            Math.abs(this.w - other.w) < EPSILON
        );
    }

    getHashCode(): number {
        let h1 = 2166136261;
        h1 = Math.imul(h1 ^ Math.floor(this.x * 1000), 16777619);
        h1 = Math.imul(h1 ^ Math.floor(this.y * 1000), 16777619);
        h1 = Math.imul(h1 ^ Math.floor(this.z * 1000), 16777619);
        h1 = Math.imul(h1 ^ Math.floor(this.w * 1000), 16777619);
        return h1 >>> 0;
    }

    static add<T extends IVec4Like, U extends IVec4Like, V extends IVec4Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        out?: V
    ): V {
        if (out) {
            out.x = a.x + b.x;
            out.y = a.y + b.y;
            out.z = a.z + b.z;
            out.w = a.w + b.w;
            return out;
        } else {
            return { x: a.x + b.x, y: a.y + b.y, z: a.z + b.z, w: a.w + b.w } as V;
        }
    }

    static addScalar<T extends IVec4Like, V extends IVec4Like>(
        a: Readonly<T>,
        b: number,
        out?: V
    ): V {
        if (out) {
            out.x = a.x + b;
            out.y = a.y + b;
            out.z = a.z + b;
            out.w = a.w + b;
            return out;
        } else {
            return { x: a.x + b, y: a.y + b, z: a.z + b, w: a.w + b } as V;
        }
    }

    static subtract<T extends IVec4Like, U extends IVec4Like, V extends IVec4Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        out?: V
    ): V {
        if (out) {
            out.x = a.x - b.x;
            out.y = a.y - b.y;
            out.z = a.z - b.z;
            out.w = a.w - b.w;
            return out;
        } else {
            return { x: a.x - b.x, y: a.y - b.y, z: a.z - b.z, w: a.w - b.w } as V;
        }
    }

    static subtractScalar<T extends IVec4Like, V extends IVec4Like>(
        a: Readonly<T>,
        b: number,
        out?: V
    ): V {
        if (out) {
            out.x = a.x - b;
            out.y = a.y - b;
            out.z = a.z - b;
            out.w = a.w - b;
            return out;
        } else {
            return { x: a.x - b, y: a.y - b, z: a.z - b, w: a.w - b } as V;
        }
    }

    static multiply<T extends IVec4Like, U extends IVec4Like, V extends IVec4Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        out?: V
    ): V {
        if (out) {
            out.x = a.x * b.x;
            out.y = a.y * b.y;
            out.z = a.z * b.z;
            out.w = a.w * b.w;
            return out;
        } else {
            return { x: a.x * b.x, y: a.y * b.y, z: a.z * b.z, w: a.w * b.w } as V;
        }
    }

    static multiplyScalar<T extends IVec4Like, V extends IVec4Like>(
        a: Readonly<T>,
        b: number,
        out?: V
    ): V {
        if (out) {
            out.x = a.x * b;
            out.y = a.y * b;
            out.z = a.z * b;
            out.w = a.w * b;
            return out;
        } else {
            return { x: a.x * b, y: a.y * b, z: a.z * b, w: a.w * b } as V;
        }
    }

    static divide<T extends IVec4Like, U extends IVec4Like, V extends IVec4Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        out?: V
    ): V {
        if (
            Math.abs(b.x) < EPSILON ||
            Math.abs(b.y) < EPSILON ||
            Math.abs(b.z) < EPSILON ||
            Math.abs(b.w) < EPSILON
        ) {
            throw new Error('Division by zero or near-zero value is not allowed');
        }

        if (out) {
            out.x = a.x / b.x;
            out.y = a.y / b.y;
            out.z = a.z / b.z;
            out.w = a.w / b.w;
            return out;
        } else {
            return { x: a.x / b.x, y: a.y / b.y, z: a.z / b.z, w: a.w / b.w } as V;
        }
    }

    static divideScalar<T extends IVec4Like, V extends IVec4Like>(
        a: Readonly<T>,
        b: number,
        out?: V
    ): V {
        if (Math.abs(b) < EPSILON) {
            throw new Error('Division by zero or near-zero value is not allowed');
        }

        if (out) {
            out.x = a.x / b;
            out.y = a.y / b;
            out.z = a.z / b;
            out.w = a.w / b;
            return out;
        } else {
            return { x: a.x / b, y: a.y / b, z: a.z / b, w: a.w / b } as V;
        }
    }

    static dot<T extends IVec4Like, U extends IVec4Like>(a: Readonly<T>, b: Readonly<U>): number {
        return a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
    }

    static len<T extends IVec4Like>(v: Readonly<T>): number {
        return Math.sqrt(v.x * v.x + v.y * v.y + v.z * v.z + v.w * v.w);
    }

    static lengthSquared<T extends IVec4Like>(v: Readonly<T>): number {
        return v.x * v.x + v.y * v.y + v.z * v.z + v.w * v.w;
    }

    static normalize<T extends IVec4Like, U extends IVec4Like>(v: Readonly<T>, out?: U): U {
        const length = Math.sqrt(v.x * v.x + v.y * v.y + v.z * v.z + v.w * v.w);

        if (length < EPSILON) {
            throw new Error('Cannot normalize a zero-length vector');
        }

        if (out) {
            out.x = v.x / length;
            out.y = v.y / length;
            out.z = v.z / length;
            out.w = v.w / length;
            return out;
        } else {
            return { x: v.x / length, y: v.y / length, z: v.z / length, w: v.w / length } as U;
        }
    }

    static distanceSquared<T extends IVec4Like, U extends IVec4Like>(
        a: Readonly<T>,
        b: Readonly<U>
    ): number {
        const dx = a.x - b.x;
        const dy = a.y - b.y;
        const dz = a.z - b.z;
        const dw = a.w - b.w;
        return dx * dx + dy * dy + dz * dz + dw * dw;
    }

    static distance<T extends IVec4Like, U extends IVec4Like>(
        a: Readonly<T>,
        b: Readonly<U>
    ): number {
        const dx = a.x - b.x;
        const dy = a.y - b.y;
        const dz = a.z - b.z;
        const dw = a.w - b.w;
        return Math.sqrt(dx * dx + dy * dy + dz * dz + dw * dw);
    }

    static lerp<T extends IVec4Like, U extends IVec4Like, V extends IVec4Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out?: V
    ): V {
        const t1 = t < 0 ? 0 : t > 1 ? 1 : t;
        if (out) {
            out.x = a.x + (b.x - a.x) * t1;
            out.y = a.y + (b.y - a.y) * t1;
            out.z = a.z + (b.z - a.z) * t1;
            out.w = a.w + (b.w - a.w) * t1;
            return out;
        } else {
            return {
                x: a.x + (b.x - a.x) * t1,
                y: a.y + (b.y - a.y) * t1,
                z: a.z + (b.z - a.z) * t1,
                w: a.w + (b.w - a.w) * t1,
            } as V;
        }
    }

    static lerUnclamped<T extends IVec4Like, U extends IVec4Like, V extends IVec4Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out?: V
    ): V {
        if (out) {
            out.x = a.x + (b.x - a.x) * t;
            out.y = a.y + (b.y - a.y) * t;
            out.z = a.z + (b.z - a.z) * t;
            out.w = a.w + (b.w - a.w) * t;
            return out;
        } else {
            return {
                x: a.x + (b.x - a.x) * t,
                y: a.y + (b.y - a.y) * t,
                z: a.z + (b.z - a.z) * t,
                w: a.w + (b.w - a.w) * t,
            } as V;
        }
    }

    static sleerp<T extends IVec4Like, U extends IVec4Like, V extends IVec4Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out?: V
    ): V {
        const t1 = t < 0 ? 0 : t > 1 ? 1 : t;

        const dotProduct = Vec4.dot(a, b);
        const lenA = Vec4.len(a);
        const lenB = Vec4.len(b);

        if (lenA < EPSILON || lenB < EPSILON) {
            return Vec4.lerp(a, b, t1, out);
        }

        let cosTheta = dotProduct / (lenA * lenB);
        cosTheta = Math.max(-1, Math.min(1, cosTheta));
        const theta = Math.acos(cosTheta);

        if (Math.abs(theta) < EPSILON) {
            return Vec4.lerp(a, b, t1, out);
        }

        const sinTheta = Math.sin(theta);
        const ratioA = Math.sin((1 - t1) * theta) / sinTheta;
        const ratioB = Math.sin(t1 * theta) / sinTheta;

        if (out) {
            out.x = ratioA * a.x + ratioB * b.x;
            out.y = ratioA * a.y + ratioB * b.y;
            out.z = ratioA * a.z + ratioB * b.z;
            out.w = ratioA * a.w + ratioB * b.w;
            return out;
        } else {
            return {
                x: ratioA * a.x + ratioB * b.x,
                y: ratioA * a.y + ratioB * b.y,
                z: ratioA * a.z + ratioB * b.z,
                w: ratioA * a.w + ratioB * b.w,
            } as V;
        }
    }

    static smoothstep<T extends IVec4Like, U extends IVec4Like, V extends IVec4Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out?: V
    ): V {
        const t1 = t < 0 ? 0 : t > 1 ? 1 : t;
        const t2 = t1 * t1 * (3 - 2 * t1);
        if (out) {
            out.x = a.x + (b.x - a.x) * t2;
            out.y = a.y + (b.y - a.y) * t2;
            out.z = a.z + (b.z - a.z) * t2;
            out.w = a.w + (b.w - a.w) * t2;
            return out;
        } else {
            return {
                x: a.x + (b.x - a.x) * t2,
                y: a.y + (b.y - a.y) * t2,
                z: a.z + (b.z - a.z) * t2,
                w: a.w + (b.w - a.w) * t2,
            } as V;
        }
    }

    static smootherStep<T extends IVec4Like, U extends IVec4Like, V extends IVec4Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out?: V
    ): V {
        const t1 = t < 0 ? 0 : t > 1 ? 1 : t;
        const t2 = t1 * t1 * t1 * (t1 * (t1 * 6 - 15) + 10);
        if (out) {
            out.x = a.x + (b.x - a.x) * t2;
            out.y = a.y + (b.y - a.y) * t2;
            out.z = a.z + (b.z - a.z) * t2;
            out.w = a.w + (b.w - a.w) * t2;
            return out;
        } else {
            return {
                x: a.x + (b.x - a.x) * t2,
                y: a.y + (b.y - a.y) * t2,
                z: a.z + (b.z - a.z) * t2,
                w: a.w + (b.w - a.w) * t2,
            } as V;
        }
    }

    static cubicBezier<
        T extends IVec4Like,
        U extends IVec4Like,
        V extends IVec4Like,
        W extends IVec4Like,
        O extends IVec4Like,
    >(a: Readonly<T>, c1: Readonly<U>, c2: Readonly<V>, b: Readonly<W>, t: number, out?: O): O {
        const t1 = t < 0 ? 0 : t > 1 ? 1 : t;
        const oneMinusT = 1 - t1;

        const oneMinusT2 = oneMinusT * oneMinusT;
        const t2 = t1 * t1;

        // Bernstein polinomials
        const B0 = oneMinusT * oneMinusT2; // (1-t)^3
        const B1 = 3 * t1 * oneMinusT2; // 3t(1-t)^2
        const B2 = 3 * t2 * oneMinusT; // 3t^2(1-t)
        const B3 = t1 * t2; // t^3

        if (out) {
            out.x = B0 * a.x + B1 * c1.x + B2 * c2.x + B3 * b.x;
            out.y = B0 * a.y + B1 * c1.y + B2 * c2.y + B3 * b.y;
            out.z = B0 * a.z + B1 * c1.z + B2 * c2.z + B3 * b.z;
            out.w = B0 * a.w + B1 * c1.w + B2 * c2.w + B3 * b.w;
            return out;
        } else {
            return {
                x: B0 * a.x + B1 * c1.x + B2 * c2.x + B3 * b.x,
                y: B0 * a.y + B1 * c1.y + B2 * c2.y + B3 * b.y,
                z: B0 * a.z + B1 * c1.z + B2 * c2.z + B3 * b.z,
                w: B0 * a.w + B1 * c1.w + B2 * c2.w + B3 * b.w,
            } as O;
        }
    }

    static hermite<
        T extends IVec4Like,
        U extends IVec4Like,
        V extends IVec4Like,
        W extends IVec4Like,
        O extends IVec4Like,
    >(p0: Readonly<T>, m0: Readonly<U>, p1: Readonly<V>, m1: Readonly<W>, t: number, out?: O): O {
        const t1 = t < 0 ? 0 : t > 1 ? 1 : t;
        const t2 = t1 * t1;
        const t3 = t2 * t1;

        const h00 = 2 * t3 - 3 * t2 + 1;
        const h10 = t3 - 2 * t2 + t1;
        const h01 = -2 * t3 + 3 * t2;
        const h11 = t3 - t2;

        if (out) {
            out.x = h00 * p0.x + h10 * m0.x + h01 * p1.x + h11 * m1.x;
            out.y = h00 * p0.y + h10 * m0.y + h01 * p1.y + h11 * m1.y;
            out.z = h00 * p0.z + h10 * m0.z + h01 * p1.z + h11 * m1.z;
            out.w = h00 * p0.w + h10 * m0.w + h01 * p1.w + h11 * m1.w;
            return out;
        } else {
            return {
                x: h00 * p0.x + h10 * m0.x + h01 * p1.x + h11 * m1.x,
                y: h00 * p0.y + h10 * m0.y + h01 * p1.y + h11 * m1.y,
                z: h00 * p0.z + h10 * m0.z + h01 * p1.z + h11 * m1.z,
                w: h00 * p0.w + h10 * m0.w + h01 * p1.w + h11 * m1.w,
            } as O;
        }
    }

    static catmullRom<
        T extends IVec4Like,
        U extends IVec4Like,
        V extends IVec4Like,
        W extends IVec4Like,
        O extends IVec4Like,
    >(
        p0: Readonly<T>,
        p1: Readonly<U>,
        p2: Readonly<V>,
        p3: Readonly<W>,
        t: number,
        tension: number = 0.5,
        out?: O
    ): O {
        const m0x = (1 - tension) * 0.5 * (p2.x - p0.x);
        const m0y = (1 - tension) * 0.5 * (p2.y - p0.y);
        const m0z = (1 - tension) * 0.5 * (p2.z - p0.z);
        const m0w = (1 - tension) * 0.5 * (p2.w - p0.w);

        const m1x = (1 - tension) * 0.5 * (p3.x - p1.x);
        const m1y = (1 - tension) * 0.5 * (p3.y - p1.y);
        const m1z = (1 - tension) * 0.5 * (p3.z - p1.z);
        const m1w = (1 - tension) * 0.5 * (p3.w - p1.w);

        const tempM0 = { x: m0x, y: m0y, z: m0z, w: m0w };
        const tempM1 = { x: m1x, y: m1y, z: m1z, w: m1w };

        return Vec4.hermite(p1, tempM0, p2, tempM1, t, out);
    }
}
