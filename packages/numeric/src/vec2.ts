import { Equatable, ICloneable } from '@axrone/utility';
import { EPSILON } from './common';

export interface IVec2Like {
    x: number;
    y: number;
}

export class Vec2 implements IVec2Like, ICloneable<Vec2>, Equatable {
    constructor(
        public x: number = 0,
        public y: number = 0
    ) {}

    static readonly ZERO: Readonly<Vec2> = Object.freeze(new Vec2(0, 0));
    static readonly ONE: Readonly<Vec2> = Object.freeze(new Vec2(1, 1));
    static readonly NEG_ONE: Readonly<Vec2> = Object.freeze(new Vec2(-1, -1));
    static readonly UNIT_X: Readonly<Vec2> = Object.freeze(new Vec2(1, 0));
    static readonly UNIT_Y: Readonly<Vec2> = Object.freeze(new Vec2(0, 1));
    static readonly UP: Readonly<Vec2> = Object.freeze(new Vec2(0, 1));
    static readonly DOWN: Readonly<Vec2> = Object.freeze(new Vec2(0, -1));
    static readonly LEFT: Readonly<Vec2> = Object.freeze(new Vec2(-1, 0));
    static readonly RIGHT: Readonly<Vec2> = Object.freeze(new Vec2(1, 0));

    static from<T extends IVec2Like>(v: T): Vec2 {
        return new Vec2(v.x, v.y);
    }

    static fromArray(arr: ArrayLike<number>, offset: number = 0): Vec2 {
        if (offset < 0) {
            throw new RangeError('Offset cannot be negative');
        }

        if (arr.length < offset + 2) {
            throw new RangeError(
                `Array must have at least ${offset + 2} elements when using offset ${offset}`
            );
        }

        return new Vec2(Number(arr[offset]), Number(arr[offset + 1]));
    }

    static create(x: number = 0, y: number = 0): Vec2 {
        return new Vec2(x, y);
    }

    clone(): Vec2 {
        return new Vec2(this.x, this.y);
    }

    equals(other: unknown): boolean {
        if (!(other instanceof Vec2)) return false;

        return Math.abs(this.x - other.x) < EPSILON && Math.abs(this.y - other.y) < EPSILON;
    }

    getHashCode(): number {
        let h1 = 2166136261;
        h1 = Math.imul(h1 ^ Math.floor(this.x * 1000), 16777619);
        h1 = Math.imul(h1 ^ Math.floor(this.y * 1000), 16777619);
        return h1 >>> 0;
    }

    static add<T extends IVec2Like>(a: T, b: T, out?: T): T {
        if (out) {
            out.x = a.x + b.x;
            out.y = a.y + b.y;
            return out;
        } else {
            return { x: a.x + b.x, y: a.y + b.y } as T;
        }
    }

    static addScalar<T extends IVec2Like>(a: T, b: number, out?: T): T {
        if (out) {
            out.x = a.x + b;
            out.y = a.y + b;
            return out;
        } else {
            return { x: a.x + b, y: a.y + b } as T;
        }
    }

    static subtract<T extends IVec2Like>(a: T, b: T, out?: T): T {
        if (out) {
            out.x = a.x - b.x;
            out.y = a.y - b.y;
            return out;
        } else {
            return { x: a.x - b.x, y: a.y - b.y } as T;
        }
    }

    static subtractScalar<T extends IVec2Like>(a: T, b: number, out?: T): T {
        if (out) {
            out.x = a.x - b;
            out.y = a.y - b;
            return out;
        } else {
            return { x: a.x - b, y: a.y - b } as T;
        }
    }

    static multiply<T extends IVec2Like>(a: T, b: T, out?: T): T {
        if (out) {
            out.x = a.x * b.x;
            out.y = a.y * b.y;
            return out;
        } else {
            return { x: a.x * b.x, y: a.y * b.y } as T;
        }
    }

    static multiplyScalar<T extends IVec2Like>(a: T, b: number, out?: T): T {
        if (out) {
            out.x = a.x * b;
            out.y = a.y * b;
            return out;
        } else {
            return { x: a.x * b, y: a.y * b } as T;
        }
    }

    static divide<T extends IVec2Like>(a: T, b: T, out?: T): T {
        if (Math.abs(b.x) < EPSILON || Math.abs(b.y) < EPSILON) {
            throw new Error('Division by zero or near-zero value is not allowed');
        }

        if (out) {
            out.x = a.x / b.x;
            out.y = a.y / b.y;
            return out;
        } else {
            return { x: a.x / b.x, y: a.y / b.y } as T;
        }
    }

    static divideScalar<T extends IVec2Like>(a: T, b: number, out?: T): T {
        if (Math.abs(b) < EPSILON) {
            throw new Error('Division by zero or near-zero value is not allowed');
        }

        if (out) {
            out.x = a.x / b;
            out.y = a.y / b;
            return out;
        } else {
            return { x: a.x / b, y: a.y / b } as T;
        }
    }

    add<T extends IVec2Like>(other: T): Vec2 {
        this.x += other.x;
        this.y += other.y;
        return this;
    }

    addScalar(num: number): Vec2 {
        this.x += num;
        this.y += num;
        return this;
    }

    subtract<T extends IVec2Like>(other: T): Vec2 {
        this.x -= other.x;
        this.y -= other.y;
        return this;
    }

    subtractScalar(num: number): Vec2 {
        this.x -= num;
        this.y -= num;
        return this;
    }

    multiply<T extends IVec2Like>(other: T): Vec2 {
        this.x *= other.x;
        this.y *= other.y;
        return this;
    }

    multiplyScalar(num: number): Vec2 {
        this.x *= num;
        this.y *= num;
        return this;
    }

    divide<T extends IVec2Like>(other: T): Vec2 {
        if (Math.abs(other.x) < EPSILON || Math.abs(other.y) < EPSILON) {
            throw new Error('Division by zero or near-zero value is not allowed');
        }

        this.x /= other.x;
        this.y /= other.y;
        return this;
    }

    divideScalar(num: number): Vec2 {
        if (Math.abs(num) < EPSILON) {
            throw new Error('Division by zero or near-zero value is not allowed');
        }

        this.x /= num;
        this.y /= num;
        return this;
    }

    dot<T extends IVec2Like>(other: T): number {
        return this.x * other.x + this.y * other.y;
    }

    cross<T extends IVec2Like>(other: T): number {
        return this.x * other.y - this.y * other.x;
    }

    lengthSquared(): number {
        return this.x * this.x + this.y * this.y;
    }

    length(): number {
        return Math.sqrt(this.lengthSquared());
    }

    fastLength(): number {
        // Fast approximation of vector length (~3.4% error max)
        const min = Math.min(this.x, this.y);
        const max = Math.max(this.x, this.y);
        return max + 0.3 * min;
    }

    static fastLength<T extends IVec2Like>(v: T): number {
        // Fast approximation of vector length (~3.4% error max)
        const min = Math.min(v.x, v.y);
        const max = Math.max(v.x, v.y);
        return max + 0.3 * min;
    }
}
