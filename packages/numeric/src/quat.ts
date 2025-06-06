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

    
}
