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

    static multiply<T extends IMat4Like, U extends IMat4Like, V extends IMat4Like>(
        a: Readonly<T>,
        b: Readonly<U>,
        out?: V
    ): V {
        const a00 = a.data[0], a01 = a.data[1], a02 = a.data[2], a03 = a.data[3];
        const a10 = a.data[4], a11 = a.data[5], a12 = a.data[6], a13 = a.data[7];
        const a20 = a.data[8], a21 = a.data[9], a22 = a.data[10], a23 = a.data[11];
        const a30 = a.data[12], a31 = a.data[13], a32 = a.data[14], a33 = a.data[15];

        const b00 = b.data[0], b01 = b.data[1], b02 = b.data[2], b03 = b.data[3];
        const b10 = b.data[4], b11 = b.data[5], b12 = b.data[6], b13 = b.data[7];
        const b20 = b.data[8], b21 = b.data[9], b22 = b.data[10], b23 = b.data[11];
        const b30 = b.data[12], b31 = b.data[13], b32 = b.data[14], b33 = b.data[15];

        if (out) {
            (out as any).data[0] = a00 * b00 + a01 * b10 + a02 * b20 + a03 * b30;
            (out as any).data[1] = a00 * b01 + a01 * b11 + a02 * b21 + a03 * b31;
            (out as any).data[2] = a00 * b02 + a01 * b12 + a02 * b22 + a03 * b32;
            (out as any).data[3] = a00 * b03 + a01 * b13 + a02 * b23 + a03 * b33;

            (out as any).data[4] = a10 * b00 + a11 * b10 + a12 * b20 + a13 * b30;
            (out as any).data[5] = a10 * b01 + a11 * b11 + a12 * b21 + a13 * b31;
            (out as any).data[6] = a10 * b02 + a11 * b12 + a12 * b22 + a13 * b32;
            (out as any).data[7] = a10 * b03 + a11 * b13 + a12 * b23 + a13 * b33;

            (out as any).data[8] = a20 * b00 + a21 * b10 + a22 * b20 + a23 * b30;
            (out as any).data[9] = a20 * b01 + a21 * b11 + a22 * b21 + a23 * b31;
            (out as any).data[10] = a20 * b02 + a21 * b12 + a22 * b22 + a23 * b32;
            (out as any).data[11] = a20 * b03 + a21 * b13 + a22 * b23 + a23 * b33;

            (out as any).data[12] = a30 * b00 + a31 * b10 + a32 * b20 + a33 * b30;
            (out as any).data[13] = a30 * b01 + a31 * b11 + a32 * b21 + a33 * b31;
            (out as any).data[14] = a30 * b02 + a31 * b12 + a32 * b22 + a33 * b32;
            (out as any).data[15] = a30 * b03 + a31 * b13 + a32 * b23 + a33 * b33;

            return out;
        } else {
            return {
                data: [
                    a00 * b00 + a01 * b10 + a02 * b20 + a03 * b30,
                    a00 * b01 + a01 * b11 + a02 * b21 + a03 * b31,
                    a00 * b02 + a01 * b12 + a02 * b22 + a03 * b32,
                    a00 * b03 + a01 * b13 + a02 * b23 + a03 * b33,

                    a10 * b00 + a11 * b10 + a12 * b20 + a13 * b30,
                    a10 * b01 + a11 * b11 + a12 * b21 + a13 * b31,
                    a10 * b02 + a11 * b12 + a12 * b22 + a13 * b32,
                    a10 * b03 + a11 * b13 + a12 * b23 + a13 * b33,

                    a20 * b00 + a21 * b10 + a22 * b20 + a23 * b30,
                    a20 * b01 + a21 * b11 + a22 * b21 + a23 * b31,
                    a20 * b02 + a21 * b12 + a22 * b22 + a23 * b32,
                    a20 * b03 + a21 * b13 + a22 * b23 + a23 * b33,

                    a30 * b00 + a31 * b10 + a32 * b20 + a33 * b30,
                    a30 * b01 + a31 * b11 + a32 * b21 + a33 * b31,
                    a30 * b02 + a31 * b12 + a32 * b22 + a33 * b32,
                    a30 * b03 + a31 * b13 + a32 * b23 + a33 * b33
                ]
            } as unknown as V;
        }
    }
}
