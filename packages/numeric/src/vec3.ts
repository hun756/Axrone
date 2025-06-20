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
}
