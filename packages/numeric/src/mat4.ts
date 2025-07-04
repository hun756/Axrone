import { Comparer, CompareResult, EqualityComparer, Equatable, ICloneable } from '@axrone/utility';
import { EPSILON, HALF_PI, PI_2 } from './common';
import { IVec3Like } from './vec3';
import { IVec4Like } from './vec4';

export interface IMat4Like {
    readonly data: ArrayLike<number>;
}

export class Mat4 implements IMat4Like, ICloneable<Mat4>, Equatable {
    public readonly data: number[];

    constructor(values?: ArrayLike<number>) {
        if (values) {
            if (values.length < 16) {
                throw new RangeError('Matrix values array must have at least 16 elements');
            }
            this.data = [
                values[0],
                values[1],
                values[2],
                values[3],
                values[4],
                values[5],
                values[6],
                values[7],
                values[8],
                values[9],
                values[10],
                values[11],
                values[12],
                values[13],
                values[14],
                values[15],
            ];
        } else {
            this.data = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];
        }
    }

    static readonly IDENTITY: Readonly<Mat4> = Object.freeze(new Mat4());
    static readonly ZERO: Readonly<Mat4> = Object.freeze(
        new Mat4([0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0])
    );

    static from<T extends IMat4Like>(m: Readonly<T>): Mat4 {
        return new Mat4(m.data);
    }

    static fromArray(arr: ArrayLike<number>, offset: number = 0): Mat4 {
        if (offset < 0) {
            throw new RangeError('Offset cannot be negative');
        }

        if (arr.length < offset + 16) {
            throw new RangeError(
                `Array must have at least ${offset + 16} elements when using offset ${offset}`
            );
        }

        const values = Array.isArray(arr)
            ? arr.slice(offset, offset + 16)
            : Array.from(arr).slice(offset, offset + 16);

        return new Mat4(values);
    }

    static create(
        m00: number = 1,
        m01: number = 0,
        m02: number = 0,
        m03: number = 0,
        m10: number = 0,
        m11: number = 1,
        m12: number = 0,
        m13: number = 0,
        m20: number = 0,
        m21: number = 0,
        m22: number = 1,
        m23: number = 0,
        m30: number = 0,
        m31: number = 0,
        m32: number = 0,
        m33: number = 1
    ): Mat4 {
        return new Mat4([
            m00,
            m01,
            m02,
            m03,
            m10,
            m11,
            m12,
            m13,
            m20,
            m21,
            m22,
            m23,
            m30,
            m31,
            m32,
            m33,
        ]);
    }

    static createFromElements(
        m00: number,
        m01: number,
        m02: number,
        m03: number,
        m10: number,
        m11: number,
        m12: number,
        m13: number,
        m20: number,
        m21: number,
        m22: number,
        m23: number,
        m30: number,
        m31: number,
        m32: number,
        m33: number
    ): Mat4 {
        return new Mat4([
            m00,
            m01,
            m02,
            m03,
            m10,
            m11,
            m12,
            m13,
            m20,
            m21,
            m22,
            m23,
            m30,
            m31,
            m32,
            m33,
        ]);
    }

    clone(): Mat4 {
        return new Mat4(this.data);
    }

    equals(other: unknown): boolean {
        if (!(other instanceof Mat4)) return false;

        for (let i = 0; i < 16; i++) {
            if (Math.abs(this.data[i] - other.data[i]) >= EPSILON) return false;
        }
        return true;
    }

    getHashCode(): number {
        let h1 = 2166136261;
        for (let i = 0; i < 16; i++) {
            h1 = Math.imul(h1 ^ Math.floor(this.data[i] * 1000), 16777619);
        }
        return h1 >>> 0;
    }
}
