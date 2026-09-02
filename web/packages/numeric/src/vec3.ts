import { Comparer, CompareResult, EqualityComparer, Equatable, ICloneable } from '@axrone/utility';
import { EPSILON, HALF_PI, PI_2, ensureFinite } from './common';
import { clampNegOneOne, clamp01 } from './clamp';
import { Fnv1a32, IHasher, HashValue, IHashable } from '@axrone/hash';
import {
    sampleStandardNormal,
    sampleNormalInRange,
    sampleUniform,
    sampleUniformRange,
} from './box-muller';

export interface IVec3Like {
    x: number;
    y: number;
    z: number;
}

export class Vec3 implements IVec3Like, ICloneable<Vec3>, Equatable {
    private static _hasher = new Fnv1a32();

    constructor(
        public x: number = 0,
        public y: number = 0,
        public z: number = 0
    ) {
        ensureFinite(x, 'Vec3.x');
        ensureFinite(y, 'Vec3.y');
        ensureFinite(z, 'Vec3.z');
    }

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

    static copy<T extends IVec3Like, V extends IVec3Like>(source: Readonly<T>, out: V): V;
    static copy<T extends IVec3Like>(source: Readonly<T>): IVec3Like;
    static copy<T extends IVec3Like, V extends IVec3Like>(source: Readonly<T>, out?: V): V {
        if (out) {
            out.x = source.x;
            out.y = source.y;
            out.z = source.z;
            return out;
        }

        return { x: source.x, y: source.y, z: source.z } as V;
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
        return Vec3._hasher.reset().updateF32(this.x).updateF32(this.y).updateF32(this.z).digest();
    }

    hashInto<H extends HashValue = any>(hasher: IHasher<H>): void {
        hasher.updateF32(this.x).updateF32(this.y).updateF32(this.z);
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
        out: V
    ): V;
    static divide<T extends IVec3Like, U extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>
    ): IVec3Like;
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
            out.x = a.x === 0 ? 0 : -a.x;
            out.y = a.y === 0 ? 0 : -a.y;
            out.z = a.z === 0 ? 0 : -a.z;
            return out;
        } else {
            return {
                x: a.x === 0 ? 0 : -a.x,
                y: a.y === 0 ? 0 : -a.y,
                z: a.z === 0 ? 0 : -a.z,
            } as V;
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

    static normalize<T extends IVec3Like, U extends IVec3Like>(v: Readonly<T>, out?: U): U {
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
            return { x: v.x / length, y: v.y / length, z: v.z / length } as U;
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
        return Math.acos(clampNegOneOne(cosTheta));
    }

    static angle2Deg<T extends IVec3Like, U extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>
    ): number {
        const angle = Vec3.angleBetween(a, b);
        return (angle * 180) / Math.PI;
    }

    static rotateX<T extends IVec3Like, V extends IVec3Like>(v: Readonly<T>, angle: number, out?: V): V {
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
            } as V;
        }
    }

    static rotateY<T extends IVec3Like, V extends IVec3Like>(v: Readonly<T>, angle: number, out?: V): V {
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
            } as V;
        }
    }

    static rotateZ<T extends IVec3Like, V extends IVec3Like>(v: Readonly<T>, angle: number, out?: V): V {
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
            } as V;
        }
    }

    static rotateAxis<T extends IVec3Like, U extends IVec3Like, V extends IVec3Like>(
        v: Readonly<T>,
        axis: Readonly<U>,
        angle: number,
        out?: V
    ): V {
        const cosTheta = Math.cos(angle);
        const sinTheta = Math.sin(angle);
        const oneMinusCos = 1 - cosTheta;

        // Inline normalize to avoid allocation
        const axisLen = Math.sqrt(axis.x * axis.x + axis.y * axis.y + axis.z * axis.z);
        if (axisLen < EPSILON) {
            throw new Error('Cannot rotate around zero-length axis');
        }
        const invLen = 1 / axisLen;
        const ax = axis.x * invLen;
        const ay = axis.y * invLen;
        const az = axis.z * invLen;

        // Inline dot product
        const dotProduct = v.x * ax + v.y * ay + v.z * az;

        // Inline cross product (axis × v)
        const cx = ay * v.z - az * v.y;
        const cy = az * v.x - ax * v.z;
        const cz = ax * v.y - ay * v.x;

        if (out) {
            out.x = v.x * cosTheta + cx * sinTheta + ax * dotProduct * oneMinusCos;
            out.y = v.y * cosTheta + cy * sinTheta + ay * dotProduct * oneMinusCos;
            out.z = v.z * cosTheta + cz * sinTheta + az * dotProduct * oneMinusCos;
            return out;
        } else {
            return {
                x: v.x * cosTheta + cx * sinTheta + ax * dotProduct * oneMinusCos,
                y: v.y * cosTheta + cy * sinTheta + ay * dotProduct * oneMinusCos,
                z: v.z * cosTheta + cz * sinTheta + az * dotProduct * oneMinusCos,
            } as V;
        }
    }

    static lerp<T extends IVec3Like, U extends IVec3Like, V extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out?: V
    ): V {
        const t1 = clamp01(t);
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
            } as V;
        }
    }

    static lerpUnClamped<T extends IVec3Like, U extends IVec3Like, V extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out?: V
    ): V {
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
            } as V;
        }
    }

    static slerp<T extends IVec3Like, U extends IVec3Like, V extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out: V
    ): V;
    static slerp<T extends IVec3Like, U extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number
    ): IVec3Like;
    static slerp<T extends IVec3Like, U extends IVec3Like, V extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out?: V
    ): V {
        const t1 = clamp01(t);

        const dotProduct = Vec3.dot(a, b);
        const lenA = Vec3.len(a);
        const lenB = Vec3.len(b);

        if (lenA < EPSILON || lenB < EPSILON) {
            return Vec3.lerp(a, b, t1, out);
        }

        const cosTheta = dotProduct / (lenA * lenB);
        const theta = Math.acos(clampNegOneOne(cosTheta));

        if (Math.abs(theta) < EPSILON) {
            return Vec3.lerp(a, b, t1, out);
        }

        const sinTheta = Math.sin(theta);

        if (Math.abs(sinTheta) < EPSILON) {
            return Vec3.lerp(a, b, t1, out);
        }

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
            } as V;
        }
    }

    static smoothStep<T extends IVec3Like, U extends IVec3Like, V extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out: V
    ): V;
    static smoothStep<T extends IVec3Like, U extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number
    ): IVec3Like;
    static smoothStep<T extends IVec3Like, U extends IVec3Like, V extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out?: V
    ): V {
        const t1 = clamp01(t);
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
            } as V;
        }
    }

    static smootherStep<T extends IVec3Like, U extends IVec3Like, V extends IVec3Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        t: number,
        out?: V
    ): V {
        const t1 = clamp01(t);
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
            } as V;
        }
    }

    static cubicBezier<
        T extends IVec3Like,
        U extends IVec3Like,
        V extends IVec3Like,
        W extends IVec3Like,
        O extends IVec3Like,
    >(a: Readonly<T>, c1: Readonly<U>, c2: Readonly<V>, b: Readonly<W>, t: number, out?: O): O {
        const t1 = clamp01(t);
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
        const t1 = clamp01(t);
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
        const t1 = clamp01(t);

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

    static random<T extends IVec3Like, V extends IVec3Like>(scale: number = 1, out?: V): V {
        const x = sampleStandardNormal();
        const y = sampleStandardNormal();
        const z = sampleStandardNormal();

        const lengthSq = x * x + y * y + z * z;
        if (!(lengthSq > 0)) {
            return Vec3.fastRandom(scale, out);
        }

        const invLength = scale / Math.sqrt(lengthSq);

        if (out) {
            out.x = x * invLength;
            out.y = y * invLength;
            out.z = z * invLength;
            return out;
        } else {
            return { x: x * invLength, y: y * invLength, z: z * invLength } as V;
        }
    }

    static fastRandom<T extends IVec3Like, V extends IVec3Like>(scale: number = 1, out?: V): V {
        const theta = sampleUniform() * PI_2;
        const phi = Math.acos(2 * sampleUniform() - 1);
        const sinPhi = Math.sin(phi);

        if (out) {
            out.x = Math.cos(theta) * sinPhi * scale;
            out.y = Math.sin(theta) * sinPhi * scale;
            out.z = Math.cos(phi) * scale;
            return out;
        } else {
            return {
                x: Math.cos(theta) * sinPhi * scale,
                y: Math.sin(theta) * sinPhi * scale,
                z: Math.cos(phi) * scale,
            } as V;
        }
    }

    static randomNormal<T extends IVec3Like, V extends IVec3Like>(scale: number = 1, out?: V): V {
        const x = sampleStandardNormal() * scale;
        const y = sampleStandardNormal() * scale;
        const z = sampleStandardNormal() * scale;

        if (out) {
            out.x = x;
            out.y = y;
            out.z = z;
            return out;
        } else {
            return { x, y, z } as V;
        }
    }

    static randomBox<T extends IVec3Like, V extends IVec3Like>(
        minX: number,
        maxX: number,
        minY: number,
        maxY: number,
        minZ: number,
        maxZ: number,
        out?: V
    ): V {
        if (out) {
            out.x = sampleUniformRange(minX, maxX);
            out.y = sampleUniformRange(minY, maxY);
            out.z = sampleUniformRange(minZ, maxZ);
            return out;
        } else {
            return {
                x: sampleUniformRange(minX, maxX),
                y: sampleUniformRange(minY, maxY),
                z: sampleUniformRange(minZ, maxZ),
            } as V;
        }
    }

    static randomBoxNormal<T extends IVec3Like, V extends IVec3Like>(
        minX: number,
        maxX: number,
        minY: number,
        maxY: number,
        minZ: number,
        maxZ: number,
        out?: V
    ): V {
        const centerX = (minX + maxX) * 0.5;
        const centerY = (minY + maxY) * 0.5;
        const centerZ = (minZ + maxZ) * 0.5;
        const rangeX = maxX - minX;
        const rangeY = maxY - minY;
        const rangeZ = maxZ - minZ;

        const x = sampleNormalInRange(centerX, rangeX);
        const y = sampleNormalInRange(centerY, rangeY);
        const z = sampleNormalInRange(centerZ, rangeZ);

        if (out) {
            out.x = x;
            out.y = y;
            out.z = z;
            return out;
        } else {
            return { x, y, z } as V;
        }
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

    distanceSquared<T extends IVec3Like>(other: Readonly<T>): number {
        const dx = this.x - other.x;
        const dy = this.y - other.y;
        const dz = this.z - other.z;
        return dx * dx + dy * dy + dz * dz;
    }

    distance<T extends IVec3Like>(other: Readonly<T>): number {
        const dx = this.x - other.x;
        const dy = this.y - other.y;
        const dz = this.z - other.z;
        return Math.sqrt(dx * dx + dy * dy + dz * dz);
    }

    manhattanDistance<T extends IVec3Like>(other: Readonly<T>): number {
        return Math.abs(this.x - other.x) + Math.abs(this.y - other.y) + Math.abs(this.z - other.z);
    }

    chebyshevDistance<T extends IVec3Like>(other: Readonly<T>): number {
        return Math.max(
            Math.abs(this.x - other.x),
            Math.abs(this.y - other.y),
            Math.abs(this.z - other.z)
        );
    }

    angleBetween<T extends IVec3Like>(other: Readonly<T>): number {
        const dotProduct = this.dot(other);
        const lengthA = this.length();
        const lengthB = Vec3.len(other);

        if (lengthA < EPSILON || lengthB < EPSILON) {
            throw new Error('Cannot calculate angle with zero-length vector');
        }

        const cosTheta = dotProduct / (lengthA * lengthB);
        return Math.acos(clampNegOneOne(cosTheta));
    }

    angle2Deg<T extends IVec3Like>(other: Readonly<T>): number {
        const angle = this.angleBetween(other);
        return (angle * 180) / Math.PI;
    }

    rotateX(angle: number): Vec3 {
        const cos = Math.cos(angle);
        const sin = Math.sin(angle);

        const y = this.y * cos - this.z * sin;
        const z = this.y * sin + this.z * cos;

        this.y = y;
        this.z = z;
        return this;
    }

    rotateY(angle: number): Vec3 {
        const cos = Math.cos(angle);
        const sin = Math.sin(angle);

        const x = this.x * cos + this.z * sin;
        const z = -this.x * sin + this.z * cos;

        this.x = x;
        this.z = z;
        return this;
    }

    rotateZ(angle: number): Vec3 {
        const cos = Math.cos(angle);
        const sin = Math.sin(angle);

        const x = this.x * cos - this.y * sin;
        const y = this.x * sin + this.y * cos;

        this.x = x;
        this.y = y;
        return this;
    }

    rotateAxis<T extends IVec3Like>(axis: Readonly<T>, angle: number): Vec3 {
        Vec3.rotateAxis(this, axis, angle, this);
        return this;
    }

    static project<T extends IVec3Like, U extends IVec3Like, V extends IVec3Like>(
        v: Readonly<T>,
        onto: Readonly<U>,
        out?: V
    ): V {
        const dotProduct = Vec3.dot(v, onto);
        const ontoLengthSq = Vec3.lengthSquared(onto);

        if (ontoLengthSq < EPSILON) {
            throw new Error('Cannot project onto zero-length vector');
        }

        const scalar = dotProduct / ontoLengthSq;

        if (out) {
            out.x = onto.x * scalar;
            out.y = onto.y * scalar;
            out.z = onto.z * scalar;
            return out;
        } else {
            return {
                x: onto.x * scalar,
                y: onto.y * scalar,
                z: onto.z * scalar,
            } as V;
        }
    }

    static reject<T extends IVec3Like, U extends IVec3Like, V extends IVec3Like>(
        v: Readonly<T>,
        onto: Readonly<U>,
        out?: V
    ): V {
        const dotProduct = Vec3.dot(v, onto);
        const ontoLengthSq = Vec3.lengthSquared(onto);

        if (ontoLengthSq < EPSILON) {
            throw new Error('Cannot project onto zero-length vector');
        }

        const scalar = dotProduct / ontoLengthSq;

        if (out) {
            out.x = v.x - onto.x * scalar;
            out.y = v.y - onto.y * scalar;
            out.z = v.z - onto.z * scalar;
            return out;
        } else {
            return {
                x: v.x - onto.x * scalar,
                y: v.y - onto.y * scalar,
                z: v.z - onto.z * scalar,
            } as V;
        }
    }

    static reflect<T extends IVec3Like, U extends IVec3Like, V extends IVec3Like>(
        v: Readonly<T>,
        normal: Readonly<U>,
        out?: V
    ): V {
        const dotProduct = Vec3.dot(v, normal);
        const factor = 2 * dotProduct;

        if (out) {
            out.x = v.x - factor * normal.x;
            out.y = v.y - factor * normal.y;
            out.z = v.z - factor * normal.z;
            return out;
        } else {
            return {
                x: v.x - factor * normal.x,
                y: v.y - factor * normal.y,
                z: v.z - factor * normal.z,
            } as V;
        }
    }

    project<T extends IVec3Like>(onto: Readonly<T>): Vec3 {
        Vec3.project(this, onto, this);
        return this;
    }

    reject<T extends IVec3Like>(onto: Readonly<T>): Vec3 {
        Vec3.reject(this, onto, this);
        return this;
    }

    reflect<T extends IVec3Like>(normal: Readonly<T>): Vec3 {
        Vec3.reflect(this, normal, this);
        return this;
    }
}

export enum Vec3ComparisonMode {
    LEXICOGRAPHIC,
    MAGNITUDE,
    MANHATTAN,
}

export class Vec3Comparer implements Comparer<Vec3> {
    private readonly mode: Vec3ComparisonMode;

    constructor(mode: Vec3ComparisonMode = Vec3ComparisonMode.LEXICOGRAPHIC) {
        this.mode = mode;
    }

    compare(a: Readonly<Vec3>, b: Readonly<Vec3>): CompareResult {
        switch (this.mode) {
            case Vec3ComparisonMode.LEXICOGRAPHIC:
                if (Math.abs(a.x - b.x) < EPSILON) {
                    if (Math.abs(a.y - b.y) < EPSILON) {
                        if (Math.abs(a.z - b.z) < EPSILON) return 0;
                        return a.z < b.z ? -1 : 1;
                    }
                    return a.y < b.y ? -1 : 1;
                }
                return a.x < b.x ? -1 : 1;

            case Vec3ComparisonMode.MAGNITUDE: {
                const lenA = a.lengthSquared();
                const lenB = b.lengthSquared();
                if (Math.abs(lenA - lenB) < EPSILON) return 0;
                return lenA < lenB ? -1 : 1;
            }

            case Vec3ComparisonMode.MANHATTAN: {
                const distA = Math.abs(a.x) + Math.abs(a.y) + Math.abs(a.z);
                const distB = Math.abs(b.x) + Math.abs(b.y) + Math.abs(b.z);
                if (Math.abs(distA - distB) < EPSILON) return 0;
                return distA < distB ? -1 : 1;
            }

            default:
                throw new Error(`Unsupported Vec3 comparison mode: ${this.mode}`);
        }
    }
}

export class Vec3EqualityComparer implements EqualityComparer<Vec3> {
    private readonly epsilon: number;

    constructor(epsilon: number = EPSILON) {
        this.epsilon = epsilon;
    }

    equals(a: Readonly<Vec3>, b: Readonly<Vec3>): boolean {
        if (a === b) return true;
        if (!a || !b) return false;

        return (
            Math.abs(a.x - b.x) < this.epsilon &&
            Math.abs(a.y - b.y) < this.epsilon &&
            Math.abs(a.z - b.z) < this.epsilon
        );
    }

    hash(obj: Readonly<Vec3>): number {
        if (!obj) return 0;
        return new Fnv1a32().updateF32(obj.x).updateF32(obj.y).updateF32(obj.z).digest();
    }
}
