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
}
