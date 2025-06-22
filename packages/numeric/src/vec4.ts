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

        return new Vec4(Number(arr[offset]), Number(arr[offset + 1]), Number(arr[offset + 2]), Number(arr[offset + 3]));
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
}
