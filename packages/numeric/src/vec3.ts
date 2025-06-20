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
}
