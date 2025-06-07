import { Equatable, ICloneable } from '@axrone/utility';

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
}
