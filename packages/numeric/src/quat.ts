import { Equatable, ICloneable } from '@axrone/utility';
import { EPSILON } from './common';

export interface IQuatLike {
    x: number;
    y: number;
    z: number;
    w: number;
}

export class Quat implements IQuatLike, ICloneable<Quat>, Equatable {
    constructor(
        public x: number = 0,
        public y: number = 0,
        public z: number = 0,
        public w: number = 1
    ) {}

    static readonly ZERO: Readonly<Quat> = Object.freeze(new Quat(0, 0, 0, 0));
    static readonly IDENTITY: Readonly<Quat> = Object.freeze(new Quat(0, 0, 0, 1));
    static readonly UNIT_X: Readonly<Quat> = Object.freeze(new Quat(1, 0, 0, 0));
    static readonly UNIT_Y: Readonly<Quat> = Object.freeze(new Quat(0, 1, 0, 0));
    static readonly UNIT_Z: Readonly<Quat> = Object.freeze(new Quat(0, 0, 1, 0));
    static readonly UNIT_W: Readonly<Quat> = Object.freeze(new Quat(0, 0, 0, 1));

    static from<T extends IQuatLike>(q: T): Quat {
        return new Quat(q.x, q.y, q.z, q.w);
    }

    equals(other: unknown): boolean {
        if (!(other instanceof Quat)) return false;
        return this.x === other.x && this.y === other.y && this.z === other.z && this.w === other.w;
    }

    getHashCode(): number {
        return (
            (this.x * 73856093) ^ (this.y * 19349663) ^ (this.z * 83492791) ^ (this.w * 16777619)
        );
    }

    clone(): Quat {
        return new Quat(this.x, this.y, this.z, this.w);
    }

    static fromAxisAngle<T extends IQuatLike>(axis: IQuatLike, angle: number, out?: T): IQuatLike {
        const halfAngle = angle * 0.5;
        const sinHalfAngle = Math.sin(halfAngle);
        const x = axis.x * sinHalfAngle;
        const y = axis.y * sinHalfAngle;
        const z = axis.z * sinHalfAngle;
        const w = Math.cos(halfAngle);
        if (out) {
            out.x = x;
            out.y = y;
            out.z = z;
            out.w = w;
            return out as T;
        }
        return new Quat(x, y, z, w);
    }

    static fromEuler<T extends IQuatLike>(x: number, y: number, z: number, out?: T): IQuatLike {
        const halfX = x * 0.5;
        const halfY = y * 0.5;
        const halfZ = z * 0.5;

        const sinX = Math.sin(halfX);
        const cosX = Math.cos(halfX);
        const sinY = Math.sin(halfY);
        const cosY = Math.cos(halfY);
        const sinZ = Math.sin(halfZ);
        const cosZ = Math.cos(halfZ);

        const w = cosX * cosY * cosZ + sinX * sinY * sinZ;
        const xOut = sinX * cosY * cosZ - cosX * sinY * sinZ;
        const yOut = cosX * sinY * cosZ + sinX * cosY * sinZ;
        const zOut = cosX * cosY * sinZ - sinX * sinY * cosZ;

        if (out) {
            out.x = xOut;
            out.y = yOut;
            out.z = zOut;
            out.w = w;
            return out as T;
        }
        return new Quat(xOut, yOut, zOut, w);
    }

    static add<T extends IQuatLike>(a: T, b: T, out?: T): T {
        if (out) {
            out.x = a.x + b.x;
            out.y = a.y + b.y;
            out.z = a.z + b.z;
            out.w = a.w + b.w;
            return out;
        } else {
            return { x: a.x + b.x, y: a.y + b.y, z: a.z + b.z, w: a.w + b.w } as T;
        }
    }

    static addScalar<T extends IQuatLike>(a: T, b: number, out?: T): T {
        if (out) {
            out.x = a.x + b;
            out.y = a.y + b;
            out.z = a.z + b;
            out.w = a.w + b;
            return out;
        } else {
            return { x: a.x + b, y: a.y + b, z: a.z + b, w: a.w + b } as T;
        }
    }

    static subtract<T extends IQuatLike>(a: T, b: T, out?: T): T {
        if (out) {
            out.x = a.x - b.x;
            out.y = a.y - b.y;
            out.z = a.z - b.z;
            out.w = a.w - b.w;
            return out;
        } else {
            return { x: a.x - b.x, y: a.y - b.y, z: a.z - b.z, w: a.w - b.w } as T;
        }
    }

    static subtractScalar<T extends IQuatLike>(a: T, b: number, out?: T): T {
        if (out) {
            out.x = a.x - b;
            out.y = a.y - b;
            out.z = a.z - b;
            out.w = a.w - b;
            return out;
        } else {
            return { x: a.x - b, y: a.y - b, z: a.z - b, w: a.w - b } as T;
        }
    }

    static multiply<T extends IQuatLike>(a: T, b: T, out?: T): T {
        const ax = a.x,
            ay = a.y,
            az = a.z,
            aw = a.w;
        const bx = b.x,
            by = b.y,
            bz = b.z,
            bw = b.w;

        if (out) {
            out.x = ax * bw + aw * bx + ay * bz - az * by;
            out.y = ay * bw + aw * by + az * bx - ax * bz;
            out.z = az * bw + aw * bz + ax * by - ay * bx;
            out.w = aw * bw - ax * bx - ay * by - az * bz;
            return out;
        } else {
            return {
                x: ax * bw + aw * bx + ay * bz - az * by,
                y: ay * bw + aw * by + az * bx - ax * bz,
                z: az * bw + aw * bz + ax * by - ay * bx,
                w: aw * bw - ax * bx - ay * by - az * bz,
            } as T;
        }
    }

    static multiplyScalar<T extends IQuatLike>(a: T, b: number, out?: T): T {
        if (out) {
            out.x = a.x * b;
            out.y = a.y * b;
            out.z = a.z * b;
            out.w = a.w * b;
            return out;
        } else {
            return { x: a.x * b, y: a.y * b, z: a.z * b, w: a.w * b } as T;
        }
    }

    static divide<T extends IQuatLike>(a: T, b: T, out?: T): T {
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
            return { x: a.x / b.x, y: a.y / b.y, z: a.z / b.z, w: a.w / b.w } as T;
        }
    }

    static divideScalar<T extends IQuatLike>(a: T, b: number, out?: T): T {
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
            return { x: a.x / b, y: a.y / b, z: a.z / b, w: a.w / b } as T;
        }
    }

    add<T extends IQuatLike>(b: T, out?: T): T {
        if (out) {
            out.x = this.x + b.x;
            out.y = this.y + b.y;
            out.z = this.z + b.z;
            out.w = this.w + b.w;
            return out;
        }

        return { x: this.x + b.x, y: this.y + b.y, z: this.z + b.z, w: this.w + b.w } as T;
    }

    addScalar<T extends IQuatLike>(b: number, out?: T): T {
        if (out) {
            out.x = this.x + b;
            out.y = this.y + b;
            out.z = this.z + b;
            out.w = this.w + b;
            return out;
        }

        return { x: this.x + b, y: this.y + b, z: this.z + b, w: this.w + b } as T;
    }

    subtract<T extends IQuatLike>(b: T, out?: T): T {
        if (out) {
            out.x = this.x - b.x;
            out.y = this.y - b.y;
            out.z = this.z - b.z;
            out.w = this.w - b.w;
            return out;
        }

        return { x: this.x - b.x, y: this.y - b.y, z: this.z - b.z, w: this.w - b.w } as T;
    }

    subtractScalar<T extends IQuatLike>(b: number, out?: T): T {
        if (out) {
            out.x = this.x - b;
            out.y = this.y - b;
            out.z = this.z - b;
            out.w = this.w - b;
            return out;
        }

        return { x: this.x - b, y: this.y - b, z: this.z - b, w: this.w - b } as T;
    }

    multiply<T extends IQuatLike>(b: T, out?: T): T {
        const ax = this.x,
            ay = this.y,
            az = this.z,
            aw = this.w;
        const bx = b.x,
            by = b.y,
            bz = b.z,
            bw = b.w;

        if (out) {
            out.x = ax * bw + aw * bx + ay * bz - az * by;
            out.y = ay * bw + aw * by + az * bx - ax * bz;
            out.z = az * bw + aw * bz + ax * by - ay * bx;
            out.w = aw * bw - ax * bx - ay * by - az * bz;
            return out;
        }

        return {
            x: ax * bw + aw * bx + ay * bz - az * by,
            y: ay * bw + aw * by + az * bx - ax * bz,
            z: az * bw + aw * bz + ax * by - ay * bx,
            w: aw * bw - ax * bx - ay * by - az * bz,
        } as T;
    }

    multiplyScalar<T extends IQuatLike>(b: number, out?: T): T {
        if (out) {
            out.x = this.x * b;
            out.y = this.y * b;
            out.z = this.z * b;
            out.w = this.w * b;
            return out;
        }

        return { x: this.x * b, y: this.y * b, z: this.z * b, w: this.w * b } as T;
    }

    divide<T extends IQuatLike>(b: T, out?: T): T {
        if (
            Math.abs(b.x) < EPSILON ||
            Math.abs(b.y) < EPSILON ||
            Math.abs(b.z) < EPSILON ||
            Math.abs(b.w) < EPSILON
        ) {
            throw new Error('Division by zero or near-zero value is not allowed');
        }

        if (out) {
            out.x = this.x / b.x;
            out.y = this.y / b.y;
            out.z = this.z / b.z;
            out.w = this.w / b.w;
            return out;
        }

        return { x: this.x / b.x, y: this.y / b.y, z: this.z / b.z, w: this.w / b.w } as T;
    }

    divideScalar<T extends IQuatLike>(b: number, out?: T): T {
        if (Math.abs(b) < EPSILON) {
            throw new Error('Division by zero or near-zero value is not allowed');
        }

        if (out) {
            out.x = this.x / b;
            out.y = this.y / b;
            out.z = this.z / b;
            out.w = this.w / b;
            return out;
        }

        return { x: this.x / b, y: this.y / b, z: this.z / b, w: this.w / b } as T;
    }
}
