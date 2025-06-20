import { Comparer, CompareResult, EqualityComparer, Equatable, ICloneable } from '@axrone/utility';
import { EPSILON, HALF_PI, PI_2, standardNormalDist } from './common';

export interface IVec3Like {
    x: number;
    y: number;
    z: number;
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

export class Vec3 implements IVec3Like, ICloneable<Vec3>, Equatable {
    constructor(
        public x: number = 0,
        public y: number = 0,
        public z: number = 0
    ) {}

    static readonly ZERO: Readonly<Vec3> = Object.freeze(new Vec3(0, 0, 0));
    static readonly ONE: Readonly<Vec3> = Object.freeze(new Vec3(1, 1, 1));
    static readonly NEG_ONE: Readonly<Vec3> = Object.freeze(new Vec3(-1, -1, -1));
    static readonly UNIT_X: Readonly<Vec3> = Object.freeze(new Vec3(1, 0, 0));
    static readonly UNIT_Y: Readonly<Vec3> = Object.freeze(new Vec3(0, 1, 0));
    static readonly UNIT_Z: Readonly<Vec3> = Object.freeze(new Vec3(0, 0, 1));
    static readonly UP: Readonly<Vec3> = Object.freeze(new Vec3(0, 1, 0));
    static readonly DOWN: Readonly<Vec3> = Object.freeze(new Vec3(0, -1, 0));
    static readonly LEFT: Readonly<Vec3> = Object.freeze(new Vec3(-1, 0, 0));
    static readonly RIGHT: Readonly<Vec3> = Object.freeze(new Vec3(1, 0, 0));
    static readonly FORWARD: Readonly<Vec3> = Object.freeze(new Vec3(0, 0, 1));
    static readonly BACK: Readonly<Vec3> = Object.freeze(new Vec3(0, 0, -1));

    static from<T extends IVec3Like>(v: Readonly<T>): Vec3 {
        return new Vec3(v.x, v.y, v.z);
    }

    static fromArray(arr: ArrayLike<number>, offset: number = 0): Vec3 {
        if (offset < 0) {
            throw new RangeError('Offset cannot be negative');
        }

        if (arr.length < offset + 3) {
            throw new RangeError(
                `Array must have at least ${offset + 3} elements when using offset ${offset}`
            );
        }

        return new Vec3(Number(arr[offset]), Number(arr[offset + 1]), Number(arr[offset + 2]));
    }

    static create(x: number = 0, y: number = 0, z: number = 0): Vec3 {
        return new Vec3(x, y, z);
    }

    clone(): Vec3 {
        return new Vec3(this.x, this.y, this.z);
    }

    equals(other: unknown): boolean {
        if (!(other instanceof Vec3)) return false;

        return (
            Math.abs(this.x - other.x) < EPSILON &&
            Math.abs(this.y - other.y) < EPSILON &&
            Math.abs(this.z - other.z) < EPSILON
        );
    }

    getHashCode(): number {
        let h1 = 2166136261;
        h1 = Math.imul(h1 ^ Math.floor(this.x * 1000), 16777619);
        h1 = Math.imul(h1 ^ Math.floor(this.y * 1000), 16777619);
        h1 = Math.imul(h1 ^ Math.floor(this.z * 1000), 16777619);
        return h1 >>> 0;
    }

    static add<T extends IVec3Like, U extends IVec3Like, V extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        out?: V
    ): V {
        if (out) {
            out.x = a.x + b.x;
            out.y = a.y + b.y;
            out.z = a.z + b.z;
            return out;
        } else {
            return { x: a.x + b.x, y: a.y + b.y, z: a.z + b.z } as V;
        }
    }

    static addScalar<T extends IVec3Like, V extends IVec3Like>(
        a: Readonly<T>,
        b: number,
        out?: V
    ): V {
        if (out) {
            out.x = a.x + b;
            out.y = a.y + b;
            out.z = a.z + b;
            return out;
        } else {
            return { x: a.x + b, y: a.y + b, z: a.z + b } as V;
        }
    }

    static subtract<T extends IVec3Like, U extends IVec3Like, V extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        out?: V
    ): V {
        if (out) {
            out.x = a.x - b.x;
            out.y = a.y - b.y;
            out.z = a.z - b.z;
            return out;
        } else {
            return { x: a.x - b.x, y: a.y - b.y, z: a.z - b.z } as V;
        }
    }

    static subtractScalar<T extends IVec3Like, V extends IVec3Like>(
        a: Readonly<T>,
        b: number,
        out?: V
    ): V {
        if (out) {
            out.x = a.x - b;
            out.y = a.y - b;
            out.z = a.z - b;
            return out;
        } else {
            return { x: a.x - b, y: a.y - b, z: a.z - b } as V;
        }
    }

    static multiply<T extends IVec3Like, U extends IVec3Like, V extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        out?: V
    ): V {
        if (out) {
            out.x = a.x * b.x;
            out.y = a.y * b.y;
            out.z = a.z * b.z;
            return out;
        } else {
            return { x: a.x * b.x, y: a.y * b.y, z: a.z * b.z } as V;
        }
    }

    static multiplyScalar<T extends IVec3Like, V extends IVec3Like>(
        a: Readonly<T>,
        b: number,
        out?: V
    ): V {
        if (out) {
            out.x = a.x * b;
            out.y = a.y * b;
            out.z = a.z * b;
            return out;
        } else {
            return { x: a.x * b, y: a.y * b, z: a.z * b } as V;
        }
    }

    static divide<T extends IVec3Like, U extends IVec3Like, V extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        out?: V
    ): V {
        if (Math.abs(b.x) < EPSILON || Math.abs(b.y) < EPSILON || Math.abs(b.z) < EPSILON) {
            throw new Error('Division by zero or near-zero value is not allowed');
        }

        if (out) {
            out.x = a.x / b.x;
            out.y = a.y / b.y;
            out.z = a.z / b.z;
            return out;
        } else {
            return { x: a.x / b.x, y: a.y / b.y, z: a.z / b.z } as V;
        }
    }

    static divideScalar<T extends IVec3Like, V extends IVec3Like>(
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
            return out;
        } else {
            return { x: a.x / b, y: a.y / b, z: a.z / b } as V;
        }
    }

    static negate<T extends IVec3Like, V extends IVec3Like>(a: Readonly<T>, out?: V): V {
        if (out) {
            out.x = -a.x;
            out.y = -a.y;
            out.z = -a.z;
            return out;
        } else {
            return { x: -a.x, y: -a.y, z: -a.z } as V;
        }
    }

    static inverse<T extends IVec3Like, V extends IVec3Like>(a: Readonly<T>, out?: V): V {
        if (out) {
            out.x = 1 / a.x;
            out.y = 1 / a.y;
            out.z = 1 / a.z;
            return out;
        } else {
            return { x: 1 / a.x, y: 1 / a.y, z: 1 / a.z } as V;
        }
    }

    static inverseSafe<T extends IVec3Like, V extends IVec3Like>(
        v: Readonly<T>,
        out?: V,
        defaultValue = 0
    ): V {
        const vx = v.x;
        const vy = v.y;
        const vz = v.z;

        if (Math.abs(vx) < EPSILON || Math.abs(vy) < EPSILON || Math.abs(vz) < EPSILON) {
            throw new Error('Inversion of zero or near-zero value');
        }

        if (out) {
            out.x = Math.abs(vx) < EPSILON ? defaultValue : 1 / vx;
            out.y = Math.abs(vy) < EPSILON ? defaultValue : 1 / vy;
            out.z = Math.abs(vz) < EPSILON ? defaultValue : 1 / vz;
            return out;
        } else {
            return {
                x: Math.abs(vx) < EPSILON ? defaultValue : 1 / vx,
                y: Math.abs(vy) < EPSILON ? defaultValue : 1 / vy,
                z: Math.abs(vz) < EPSILON ? defaultValue : 1 / vz,
            } as V;
        }
    }

    static dot<T extends IVec3Like, U extends IVec3Like>(a: Readonly<T>, b: Readonly<U>): number {
        return a.x * b.x + a.y * b.y + a.z * b.z;
    }

    static cross<T extends IVec3Like, U extends IVec3Like, V extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        out?: V
    ): V {
        const x = a.y * b.z - a.z * b.y;
        const y = a.z * b.x - a.x * b.z;
        const z = a.x * b.y - a.y * b.x;

        if (out) {
            out.x = x;
            out.y = y;
            out.z = z;
            return out;
        } else {
            return { x, y, z } as V;
        }
    }

    static len<T extends IVec3Like>(v: Readonly<T>): number {
        return Math.sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
    }

    static lengthSquared<T extends IVec3Like>(v: Readonly<T>): number {
        return v.x * v.x + v.y * v.y + v.z * v.z;
    }

    static fastLength<T extends IVec3Like>(v: Readonly<T>): number {
        const ax = Math.abs(v.x);
        const ay = Math.abs(v.y);
        const az = Math.abs(v.z);

        const max = Math.max(ax, ay, az);
        const mid = ax + ay + az - max - Math.min(ax, ay, az);
        const min = Math.min(ax, ay, az);

        return max + 0.4 * mid + 0.2 * min;
    }

    static normalize<T extends IVec3Like>(v: Readonly<T>, out?: T): T {
        const length = Math.sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
        if (length < EPSILON) {
            throw new Error('Cannot normalize a zero-length vector');
        }

        if (out) {
            out.x = v.x / length;
            out.y = v.y / length;
            out.z = v.z / length;
            return out;
        } else {
            return { x: v.x / length, y: v.y / length, z: v.z / length } as T;
        }
    }

    static normalizeFast<T extends IVec3Like>(v: Readonly<T>, out?: T): T {
        const vx = v.x;
        const vy = v.y;
        const vz = v.z;
        const lenSq = vx * vx + vy * vy + vz * vz;
        if (lenSq < EPSILON) {
            throw new Error('Cannot normalize a zero-length vector');
        }

        let i = 0;
        const buf = new ArrayBuffer(4);
        const view = new DataView(buf);
        view.setFloat32(0, lenSq);
        i = view.getInt32(0);
        i = 0x5f3759df - (i >> 1);
        view.setInt32(0, i);
        let invLen = view.getFloat32(0);
        invLen = invLen * (1.5 - lenSq * 0.5 * invLen * invLen);

        if (out) {
            out.x = vx * invLen;
            out.y = vy * invLen;
            out.z = vz * invLen;
            return out;
        } else {
            return { x: vx * invLen, y: vy * invLen, z: vz * invLen } as T;
        }
    }

    static distanceSquared<T extends IVec3Like, U extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>
    ): number {
        const dx = a.x - b.x;
        const dy = a.y - b.y;
        const dz = a.z - b.z;
        return dx * dx + dy * dy + dz * dz;
    }

    static distance<T extends IVec3Like, U extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>
    ): number {
        const dx = a.x - b.x;
        const dy = a.y - b.y;
        const dz = a.z - b.z;
        return Math.sqrt(dx * dx + dy * dy + dz * dz);
    }

    static distanceFast<T extends IVec3Like, U extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>
    ): number {
        const dx = Math.abs(a.x - b.x);
        const dy = Math.abs(a.y - b.y);
        const dz = Math.abs(a.z - b.z);

        const max = Math.max(dx, dy, dz);
        const mid = dx + dy + dz - max - Math.min(dx, dy, dz);
        const min = Math.min(dx, dy, dz);

        return max + 0.4 * mid + 0.2 * min;
    }

    static manhattanDistance<T extends IVec3Like, U extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>
    ): number {
        return Math.abs(a.x - b.x) + Math.abs(a.y - b.y) + Math.abs(a.z - b.z);
    }

    static chebyshevDistance<T extends IVec3Like, U extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>
    ): number {
        return Math.max(Math.abs(a.x - b.x), Math.abs(a.y - b.y), Math.abs(a.z - b.z));
    }

    static angleBetween<T extends IVec3Like, U extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>
    ): number {
        const dotProduct = Vec3.dot(a, b);
        const lengthA = Vec3.len(a);
        const lengthB = Vec3.len(b);

        if (lengthA < EPSILON || lengthB < EPSILON) {
            throw new Error('Cannot calculate angle with zero-length vector');
        }

        const cosTheta = dotProduct / (lengthA * lengthB);
        return Math.acos(Math.max(-1, Math.min(1, cosTheta)));
    }

    static angle2Deg<T extends IVec3Like, U extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>
    ): number {
        const angle = Vec3.angleBetween(a, b);
        return (angle * 180) / Math.PI;
    }

    static rotateX<T extends IVec3Like>(v: Readonly<T>, angle: number, out?: T): T {
        const cos = Math.cos(angle);
        const sin = Math.sin(angle);

        if (out) {
            out.x = v.x;
            out.y = v.y * cos - v.z * sin;
            out.z = v.y * sin + v.z * cos;
            return out;
        } else {
            return {
                x: v.x,
                y: v.y * cos - v.z * sin,
                z: v.y * sin + v.z * cos,
            } as T;
        }
    }

    static rotateY<T extends IVec3Like>(v: Readonly<T>, angle: number, out?: T): T {
        const cos = Math.cos(angle);
        const sin = Math.sin(angle);

        if (out) {
            out.x = v.x * cos + v.z * sin;
            out.y = v.y;
            out.z = -v.x * sin + v.z * cos;
            return out;
        } else {
            return {
                x: v.x * cos + v.z * sin,
                y: v.y,
                z: -v.x * sin + v.z * cos,
            } as T;
        }
    }

    static rotateZ<T extends IVec3Like>(v: Readonly<T>, angle: number, out?: T): T {
        const cos = Math.cos(angle);
        const sin = Math.sin(angle);

        if (out) {
            out.x = v.x * cos - v.y * sin;
            out.y = v.x * sin + v.y * cos;
            out.z = v.z;
            return out;
        } else {
            return {
                x: v.x * cos - v.y * sin,
                y: v.x * sin + v.y * cos,
                z: v.z,
            } as T;
        }
    }

    static rotateAxis<T extends IVec3Like, U extends IVec3Like>(
        v: Readonly<T>,
        axis: Readonly<U>,
        angle: number,
        out?: T
    ): T {
        const cosTheta = Math.cos(angle);
        const sinTheta = Math.sin(angle);
        const oneMinusCos = 1 - cosTheta;

        const axisNorm = Vec3.normalize(axis);
        const dotProduct = Vec3.dot(v, axisNorm);
        const crossProduct = Vec3.cross(axisNorm, v);

        if (out) {
            out.x =
                v.x * cosTheta + crossProduct.x * sinTheta + axisNorm.x * dotProduct * oneMinusCos;
            out.y =
                v.y * cosTheta + crossProduct.y * sinTheta + axisNorm.y * dotProduct * oneMinusCos;
            out.z =
                v.z * cosTheta + crossProduct.z * sinTheta + axisNorm.z * dotProduct * oneMinusCos;
            return out;
        } else {
            return {
                x:
                    v.x * cosTheta +
                    crossProduct.x * sinTheta +
                    axisNorm.x * dotProduct * oneMinusCos,
                y:
                    v.y * cosTheta +
                    crossProduct.y * sinTheta +
                    axisNorm.y * dotProduct * oneMinusCos,
                z:
                    v.z * cosTheta +
                    crossProduct.z * sinTheta +
                    axisNorm.z * dotProduct * oneMinusCos,
            } as T;
        }
    }

    static lerp<T extends IVec3Like, U extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out?: T
    ): T {
        const t1 = t < 0 ? 0 : t > 1 ? 1 : t;
        if (out) {
            out.x = a.x + (b.x - a.x) * t1;
            out.y = a.y + (b.y - a.y) * t1;
            out.z = a.z + (b.z - a.z) * t1;
            return out;
        } else {
            return {
                x: a.x + (b.x - a.x) * t1,
                y: a.y + (b.y - a.y) * t1,
                z: a.z + (b.z - a.z) * t1,
            } as T;
        }
    }

    static lerpUnClamped<T extends IVec3Like, U extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out?: T
    ): T {
        if (out) {
            out.x = a.x + (b.x - a.x) * t;
            out.y = a.y + (b.y - a.y) * t;
            out.z = a.z + (b.z - a.z) * t;
            return out;
        } else {
            return {
                x: a.x + (b.x - a.x) * t,
                y: a.y + (b.y - a.y) * t,
                z: a.z + (b.z - a.z) * t,
            } as T;
        }
    }

    static slerp<T extends IVec3Like, U extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out?: T
    ): T {
        const t1 = t < 0 ? 0 : t > 1 ? 1 : t;

        const dotProduct = Vec3.dot(a, b);
        const lenA = Vec3.len(a);
        const lenB = Vec3.len(b);

        if (lenA < EPSILON || lenB < EPSILON) {
            return Vec3.lerp(a, b, t1, out);
        }

        const cosTheta = dotProduct / (lenA * lenB);
        const theta = Math.acos(Math.max(-1, Math.min(1, cosTheta)));

        if (Math.abs(theta) < EPSILON) {
            return Vec3.lerp(a, b, t1, out);
        }

        const sinTheta = Math.sin(theta);
        const ratioA = Math.sin((1 - t1) * theta) / sinTheta;
        const ratioB = Math.sin(t1 * theta) / sinTheta;

        if (out) {
            out.x = ratioA * a.x + ratioB * b.x;
            out.y = ratioA * a.y + ratioB * b.y;
            out.z = ratioA * a.z + ratioB * b.z;
            return out;
        } else {
            return {
                x: ratioA * a.x + ratioB * b.x,
                y: ratioA * a.y + ratioB * b.y,
                z: ratioA * a.z + ratioB * b.z,
            } as T;
        }
    }

    static smoothStep<T extends IVec3Like, U extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out?: T
    ): T {
        const t1 = t < 0 ? 0 : t > 1 ? 1 : t;
        const t2 = t1 * t1 * (3 - 2 * t1);
        if (out) {
            out.x = a.x + (b.x - a.x) * t2;
            out.y = a.y + (b.y - a.y) * t2;
            out.z = a.z + (b.z - a.z) * t2;
            return out;
        } else {
            return {
                x: a.x + (b.x - a.x) * t2,
                y: a.y + (b.y - a.y) * t2,
                z: a.z + (b.z - a.z) * t2,
            } as T;
        }
    }

    static smootherStep<T extends IVec3Like, U extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out?: T
    ): T {
        const t1 = t < 0 ? 0 : t > 1 ? 1 : t;
        const t2 = t1 * t1 * t1 * (10 - 15 * t1 + 6 * t1 * t1);
        if (out) {
            out.x = a.x + (b.x - a.x) * t2;
            out.y = a.y + (b.y - a.y) * t2;
            out.z = a.z + (b.z - a.z) * t2;
            return out;
        } else {
            return {
                x: a.x + (b.x - a.x) * t2,
                y: a.y + (b.y - a.y) * t2,
                z: a.z + (b.z - a.z) * t2,
            } as T;
        }
    }

    static cubicBezier<
        T extends IVec3Like,
        U extends IVec3Like,
        V extends IVec3Like,
        W extends IVec3Like,
        O extends IVec3Like,
    >(a: Readonly<T>, c1: Readonly<U>, c2: Readonly<V>, b: Readonly<W>, t: number, out?: O): O {
        const t1 = t < 0 ? 0 : t > 1 ? 1 : t;
        const oneMinusT = 1 - t1;
        const oneMinusT2 = oneMinusT * oneMinusT;
        const t2 = t1 * t1;

        const oneMinusT3 = oneMinusT2 * oneMinusT;
        const t3 = t2 * t1;
        const oneMinusT2_3t = oneMinusT2 * 3 * t1;
        const oneMinusT_3t2 = oneMinusT * 3 * t2;

        if (out) {
            out.x = oneMinusT3 * a.x + oneMinusT2_3t * c1.x + oneMinusT_3t2 * c2.x + t3 * b.x;
            out.y = oneMinusT3 * a.y + oneMinusT2_3t * c1.y + oneMinusT_3t2 * c2.y + t3 * b.y;
            out.z = oneMinusT3 * a.z + oneMinusT2_3t * c1.z + oneMinusT_3t2 * c2.z + t3 * b.z;
            return out;
        } else {
            return {
                x: oneMinusT3 * a.x + oneMinusT2_3t * c1.x + oneMinusT_3t2 * c2.x + t3 * b.x,
                y: oneMinusT3 * a.y + oneMinusT2_3t * c1.y + oneMinusT_3t2 * c2.y + t3 * b.y,
                z: oneMinusT3 * a.z + oneMinusT2_3t * c1.z + oneMinusT_3t2 * c2.z + t3 * b.z,
            } as O;
        }
    }

    static hermite<
        T extends IVec3Like,
        U extends IVec3Like,
        V extends IVec3Like,
        W extends IVec3Like,
        O extends IVec3Like,
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
            return out;
        } else {
            return {
                x: h00 * p0.x + h10 * m0.x + h01 * p1.x + h11 * m1.x,
                y: h00 * p0.y + h10 * m0.y + h01 * p1.y + h11 * m1.y,
                z: h00 * p0.z + h10 * m0.z + h01 * p1.z + h11 * m1.z,
            } as O;
        }
    }

    static catmullRom<
        T extends IVec3Like,
        U extends IVec3Like,
        V extends IVec3Like,
        W extends IVec3Like,
        O extends IVec3Like,
    >(
        p0: Readonly<T>,
        p1: Readonly<U>,
        p2: Readonly<V>,
        p3: Readonly<W>,
        t: number,
        tension: number = 0.5,
        out?: O
    ): O {
        const t1 = t < 0 ? 0 : t > 1 ? 1 : t;

        if (!out) {
            out = { x: 0, y: 0, z: 0 } as O;
        }

        if (t1 === 0) {
            out.x = p1.x;
            out.y = p1.y;
            out.z = p1.z;
            return out;
        }

        if (t1 === 1) {
            out.x = p2.x;
            out.y = p2.y;
            out.z = p2.z;
            return out;
        }

        const t2 = t1 * t1;
        const t3 = t2 * t1;

        const h00 = 2 * t3 - 3 * t2 + 1;
        const h10 = t3 - 2 * t2 + t1;
        const h01 = -2 * t3 + 3 * t2;
        const h11 = t3 - t2;

        const alpha = (1 - tension) / 2;

        const m0x = alpha * (p2.x - p0.x);
        const m0y = alpha * (p2.y - p0.y);
        const m0z = alpha * (p2.z - p0.z);
        const m1x = alpha * (p3.x - p1.x);
        const m1y = alpha * (p3.y - p1.y);
        const m1z = alpha * (p3.z - p1.z);

        out.x = h00 * p1.x + h10 * m0x + h01 * p2.x + h11 * m1x;
        out.y = h00 * p1.y + h10 * m0y + h01 * p2.y + h11 * m1y;
        out.z = h00 * p1.z + h10 * m0z + h01 * p2.z + h11 * m1z;

        return out;
    }

    static random<T extends IVec3Like>(out?: T): T {
        const x = _normalRandom();
        const y = _normalRandom();
        const z = _normalRandom();

        if (out) {
            out.x = x;
            out.y = y;
            out.z = z;
            return out;
        } else {
            return { x, y, z } as T;
        }
    }

    static fastRandom(scale: number = 1): IVec3Like {
        const theta = Math.random() * PI_2;
        const phi = Math.acos(2 * Math.random() - 1);
        const sinPhi = Math.sin(phi);

        return {
            x: Math.cos(theta) * sinPhi * scale,
            y: Math.sin(theta) * sinPhi * scale,
            z: Math.cos(phi) * scale,
        };
    }

    static randomNormal(scale: number = 1): IVec3Like {
        const u1 = 1 - Math.random();
        const u2 = Math.random();
        const u3 = Math.random();

        const z0 = Math.sqrt(-2 * Math.log(u1)) * Math.cos(PI_2 * u2);
        const z1 = Math.sqrt(-2 * Math.log(u1)) * Math.sin(PI_2 * u2);
        const z2 = Math.sqrt(-2 * Math.log(u3)) * Math.cos(PI_2 * Math.random());

        return {
            x: z0 * scale,
            y: z1 * scale,
            z: z2 * scale,
        };
    }

    randomBox(
        minX: number,
        maxX: number,
        minY: number,
        maxY: number,
        minZ: number,
        maxZ: number
    ): IVec3Like {
        return {
            x: minX + Math.random() * (maxX - minX),
            y: minY + Math.random() * (maxY - minY),
            z: minZ + Math.random() * (maxZ - minZ),
        };
    }

    randomBoxNormal(
        minX: number,
        maxX: number,
        minY: number,
        maxY: number,
        minZ: number,
        maxZ: number
    ): IVec3Like {
        return {
            x: minX + (_normalRandom() + 1) * 0.5 * (maxX - minX),
            y: minY + (_normalRandom() + 1) * 0.5 * (maxY - minY),
            z: minZ + (_normalRandom() + 1) * 0.5 * (maxZ - minZ),
        };
    }

    add<T extends IVec3Like>(other: Readonly<T>): Vec3 {
        this.x += other.x;
        this.y += other.y;
        this.z += other.z;
        return this;
    }

    addScalar(num: number): Vec3 {
        this.x += num;
        this.y += num;
        this.z += num;
        return this;
    }

    subtract<T extends IVec3Like>(other: Readonly<T>): Vec3 {
        this.x -= other.x;
        this.y -= other.y;
        this.z -= other.z;
        return this;
    }

    subtractScalar(num: number): Vec3 {
        this.x -= num;
        this.y -= num;
        this.z -= num;
        return this;
    }

    multiply<T extends IVec3Like>(other: Readonly<T>): Vec3 {
        this.x *= other.x;
        this.y *= other.y;
        this.z *= other.z;
        return this;
    }

    multiplyScalar(num: number): Vec3 {
        this.x *= num;
        this.y *= num;
        this.z *= num;
        return this;
    }

    divide<T extends IVec3Like>(other: Readonly<T>): Vec3 {
        if (
            Math.abs(other.x) < EPSILON ||
            Math.abs(other.y) < EPSILON ||
            Math.abs(other.z) < EPSILON
        ) {
            throw new Error('Division by zero or near-zero value is not allowed');
        }

        this.x /= other.x;
        this.y /= other.y;
        this.z /= other.z;
        return this;
    }

    divideScalar(num: number): Vec3 {
        if (Math.abs(num) < EPSILON) {
            throw new Error('Division by zero or near-zero value is not allowed');
        }

        this.x /= num;
        this.y /= num;
        this.z /= num;
        return this;
    }

    dot<T extends IVec3Like>(other: Readonly<T>): number {
        return this.x * other.x + this.y * other.y + this.z * other.z;
    }

    cross<T extends IVec3Like>(other: Readonly<T>): Vec3 {
        const x = this.y * other.z - this.z * other.y;
        const y = this.z * other.x - this.x * other.z;
        const z = this.x * other.y - this.y * other.x;

        this.x = x;
        this.y = y;
        this.z = z;
        return this;
    }

    lengthSquared(): number {
        return this.x * this.x + this.y * this.y + this.z * this.z;
    }

    length(): number {
        return Math.sqrt(this.lengthSquared());
    }

    fastLength(): number {
        const ax = Math.abs(this.x);
        const ay = Math.abs(this.y);
        const az = Math.abs(this.z);

        const max = Math.max(ax, ay, az);
        const mid = ax + ay + az - max - Math.min(ax, ay, az);
        const min = Math.min(ax, ay, az);

        return max + 0.4 * mid + 0.2 * min;
    }

    inverse(): Vec3 {
        if (
            Math.abs(this.x) < EPSILON ||
            Math.abs(this.y) < EPSILON ||
            Math.abs(this.z) < EPSILON
        ) {
            throw new Error('Inversion of zero or near-zero value');
        }

        this.x = 1 / this.x;
        this.y = 1 / this.y;
        this.z = 1 / this.z;
        return this;
    }

    inverseSafe(defaultValue: number = 0): Vec3 {
        const vx = this.x;
        const vy = this.y;
        const vz = this.z;

        this.x = Math.abs(vx) < EPSILON ? defaultValue : 1 / vx;
        this.y = Math.abs(vy) < EPSILON ? defaultValue : 1 / vy;
        this.z = Math.abs(vz) < EPSILON ? defaultValue : 1 / vz;
        return this;
    }

    normalize(): Vec3 {
        const length = this.length();
        if (length < EPSILON) {
            throw new Error('Cannot normalize a zero-length vector');
        }

        this.x /= length;
        this.y /= length;
        this.z /= length;
        return this;
    }

    normalizeFast(): Vec3 {
        const lenSq = this.x * this.x + this.y * this.y + this.z * this.z;
        if (lenSq < EPSILON) {
            throw new Error('Cannot normalize a zero-length vector');
        }

        let i = 0;
        const buf = new ArrayBuffer(4);
        const view = new DataView(buf);
        view.setFloat32(0, lenSq);
        i = view.getInt32(0);
        i = 0x5f3759df - (i >> 1);
        view.setInt32(0, i);
        let invLen = view.getFloat32(0);
        invLen = invLen * (1.5 - lenSq * 0.5 * invLen * invLen);

        this.x *= invLen;
        this.y *= invLen;
        this.z *= invLen;
        return this;
    }
}
