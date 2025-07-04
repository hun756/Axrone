import { Comparer, CompareResult, EqualityComparer, Equatable, ICloneable } from '@axrone/utility';
import { EPSILON, HALF_PI, PI_2 } from './common';
import { IVec3Like } from './vec3';
import { IVec4Like } from './vec4';

declare const __matrix4Brand: unique symbol;
declare const __mutableBrand: unique symbol;
declare const __vec3Brand: unique symbol;
declare const __vec4Brand: unique symbol;

type Matrix4Data = number[] & { readonly [__matrix4Brand]: true };
type MutableMatrix4Data = number[] & {
    readonly [__matrix4Brand]: true;
    readonly [__mutableBrand]: true;
};

export interface IMat4Like<TData extends ArrayLike<number> = ArrayLike<number>> {
    readonly data: TData;
}

interface IMutableMat4<TData extends number[] = number[]> extends IMat4Like<TData> {
    data: TData;
}

type InferMatrixData<T> = T extends { data: infer U } ? U : never;

type IsMatrix4Compatible<T> = T extends { data: ArrayLike<number> } ? true : false;

type IsMutableMatrix4<T> = T extends { data: number[] } ? true : false;

type MatrixOperationReturnType<
    TOut extends IMat4Like | undefined,
    TDefault extends IMat4Like,
    TSecond extends IMat4Like = TDefault,
> = TOut extends IMutableMat4<infer U> ? TOut : TOut extends undefined ? Mat4 : never;

const asMutableMatrix4Data = <T extends number[]>(data: T): T & MutableMatrix4Data => {
    return data as T & MutableMatrix4Data;
};

const ensureMatrix4Data = <T extends ArrayLike<number>>(data: T): T & Matrix4Data => {
    return data as T & Matrix4Data;
};

export class Mat4 implements IMat4Like<Matrix4Data>, ICloneable<Mat4>, Equatable {
    public readonly data: Matrix4Data;

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
            ] as Matrix4Data;
        } else {
            this.data = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1] as Matrix4Data;
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
        if (process.env.NODE_ENV === 'development') {
            if (offset < 0) {
                throw new RangeError('Offset cannot be negative');
            }
            if (arr.length < offset + 16) {
                throw new RangeError(
                    `Array must have at least ${offset + 16} elements when using offset ${offset}`
                );
            }
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

        const a = this.data;
        const b = other.data;

        return (
            Math.abs(a[0] - b[0]) < EPSILON &&
            Math.abs(a[1] - b[1]) < EPSILON &&
            Math.abs(a[2] - b[2]) < EPSILON &&
            Math.abs(a[3] - b[3]) < EPSILON &&
            Math.abs(a[4] - b[4]) < EPSILON &&
            Math.abs(a[5] - b[5]) < EPSILON &&
            Math.abs(a[6] - b[6]) < EPSILON &&
            Math.abs(a[7] - b[7]) < EPSILON &&
            Math.abs(a[8] - b[8]) < EPSILON &&
            Math.abs(a[9] - b[9]) < EPSILON &&
            Math.abs(a[10] - b[10]) < EPSILON &&
            Math.abs(a[11] - b[11]) < EPSILON &&
            Math.abs(a[12] - b[12]) < EPSILON &&
            Math.abs(a[13] - b[13]) < EPSILON &&
            Math.abs(a[14] - b[14]) < EPSILON &&
            Math.abs(a[15] - b[15]) < EPSILON
        );
    }

    getHashCode(): number {
        let h1 = 2166136261;
        for (let i = 0; i < 16; i++) {
            h1 = Math.imul(h1 ^ Math.floor(this.data[i] * 1000), 16777619);
        }
        return h1 >>> 0;
    }

    static multiply<
        TMatA extends IMat4Like,
        TMatB extends IMat4Like,
        TOut extends IMat4Like | undefined = undefined,
    >(
        a: Readonly<TMatA>,
        b: Readonly<TMatB>,
        out?: TOut
    ): MatrixOperationReturnType<TOut, TMatA, TMatB> {
        const a00 = a.data[0],
            a01 = a.data[1],
            a02 = a.data[2],
            a03 = a.data[3];
        const a10 = a.data[4],
            a11 = a.data[5],
            a12 = a.data[6],
            a13 = a.data[7];
        const a20 = a.data[8],
            a21 = a.data[9],
            a22 = a.data[10],
            a23 = a.data[11];
        const a30 = a.data[12],
            a31 = a.data[13],
            a32 = a.data[14],
            a33 = a.data[15];

        const b00 = b.data[0],
            b01 = b.data[1],
            b02 = b.data[2],
            b03 = b.data[3];
        const b10 = b.data[4],
            b11 = b.data[5],
            b12 = b.data[6],
            b13 = b.data[7];
        const b20 = b.data[8],
            b21 = b.data[9],
            b22 = b.data[10],
            b23 = b.data[11];
        const b30 = b.data[12],
            b31 = b.data[13],
            b32 = b.data[14],
            b33 = b.data[15];

        if (out) {
            const outData = asMutableMatrix4Data((out as IMutableMat4).data);

            outData[0] = a00 * b00 + a01 * b10 + a02 * b20 + a03 * b30;
            outData[1] = a00 * b01 + a01 * b11 + a02 * b21 + a03 * b31;
            outData[2] = a00 * b02 + a01 * b12 + a02 * b22 + a03 * b32;
            outData[3] = a00 * b03 + a01 * b13 + a02 * b23 + a03 * b33;

            outData[4] = a10 * b00 + a11 * b10 + a12 * b20 + a13 * b30;
            outData[5] = a10 * b01 + a11 * b11 + a12 * b21 + a13 * b31;
            outData[6] = a10 * b02 + a11 * b12 + a12 * b22 + a13 * b32;
            outData[7] = a10 * b03 + a11 * b13 + a12 * b23 + a13 * b33;

            outData[8] = a20 * b00 + a21 * b10 + a22 * b20 + a23 * b30;
            outData[9] = a20 * b01 + a21 * b11 + a22 * b21 + a23 * b31;
            outData[10] = a20 * b02 + a21 * b12 + a22 * b22 + a23 * b32;
            outData[11] = a20 * b03 + a21 * b13 + a22 * b23 + a23 * b33;

            outData[12] = a30 * b00 + a31 * b10 + a32 * b20 + a33 * b30;
            outData[13] = a30 * b01 + a31 * b11 + a32 * b21 + a33 * b31;
            outData[14] = a30 * b02 + a31 * b12 + a32 * b22 + a33 * b32;
            outData[15] = a30 * b03 + a31 * b13 + a32 * b23 + a33 * b33;

            return out as MatrixOperationReturnType<TOut, TMatA, TMatB>;
        } else {
            return new Mat4([
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
                a30 * b03 + a31 * b13 + a32 * b23 + a33 * b33,
            ]) as MatrixOperationReturnType<TOut, TMatA, TMatB>;
        }
    }

    static transpose<T extends IMat4Like, V extends IMat4Like | undefined = undefined>(
        m: Readonly<T>,
        out?: V
    ): MatrixOperationReturnType<V, T> {
        if (out) {
            const outData = asMutableMatrix4Data((out as IMutableMat4).data);

            outData[0] = m.data[0];
            outData[1] = m.data[4];
            outData[2] = m.data[8];
            outData[3] = m.data[12];
            outData[4] = m.data[1];
            outData[5] = m.data[5];
            outData[6] = m.data[9];
            outData[7] = m.data[13];
            outData[8] = m.data[2];
            outData[9] = m.data[6];
            outData[10] = m.data[10];
            outData[11] = m.data[14];
            outData[12] = m.data[3];
            outData[13] = m.data[7];
            outData[14] = m.data[11];
            outData[15] = m.data[15];

            return out as MatrixOperationReturnType<V, T>;
        } else {
            return new Mat4([
                m.data[0],
                m.data[4],
                m.data[8],
                m.data[12],
                m.data[1],
                m.data[5],
                m.data[9],
                m.data[13],
                m.data[2],
                m.data[6],
                m.data[10],
                m.data[14],
                m.data[3],
                m.data[7],
                m.data[11],
                m.data[15],
            ]) as MatrixOperationReturnType<V, T>;
        }
    }

    static determinant<T extends IMat4Like>(m: Readonly<T>): number {
        const a = m.data;

        const a00 = a[0] * a[5] - a[1] * a[4];
        const a01 = a[0] * a[6] - a[2] * a[4];
        const a02 = a[0] * a[7] - a[3] * a[4];
        const a03 = a[1] * a[6] - a[2] * a[5];
        const a04 = a[1] * a[7] - a[3] * a[5];
        const a05 = a[2] * a[7] - a[3] * a[6];
        const b00 = a[8] * a[13] - a[9] * a[12];
        const b01 = a[8] * a[14] - a[10] * a[12];
        const b02 = a[8] * a[15] - a[11] * a[12];
        const b03 = a[9] * a[14] - a[10] * a[13];
        const b04 = a[9] * a[15] - a[11] * a[13];
        const b05 = a[10] * a[15] - a[11] * a[14];

        return a00 * b05 - a01 * b04 + a02 * b03 + a03 * b02 - a04 * b01 + a05 * b00;
    }

    static invert<T extends IMat4Like, V extends IMat4Like | undefined = undefined>(
        m: Readonly<T>,
        out?: V
    ): MatrixOperationReturnType<V, T> {
        const a = m.data;

        const a00 = a[0] * a[5] - a[1] * a[4];
        const a01 = a[0] * a[6] - a[2] * a[4];
        const a02 = a[0] * a[7] - a[3] * a[4];
        const a03 = a[1] * a[6] - a[2] * a[5];
        const a04 = a[1] * a[7] - a[3] * a[5];
        const a05 = a[2] * a[7] - a[3] * a[6];
        const b00 = a[8] * a[13] - a[9] * a[12];
        const b01 = a[8] * a[14] - a[10] * a[12];
        const b02 = a[8] * a[15] - a[11] * a[12];
        const b03 = a[9] * a[14] - a[10] * a[13];
        const b04 = a[9] * a[15] - a[11] * a[13];
        const b05 = a[10] * a[15] - a[11] * a[14];

        let det = a00 * b05 - a01 * b04 + a02 * b03 + a03 * b02 - a04 * b01 + a05 * b00;

        if (Math.abs(det) < EPSILON) {
            throw new Error('Matrix is not invertible (determinant is zero or near-zero)');
        }

        det = 1.0 / det;

        if (out) {
            const outData = asMutableMatrix4Data((out as IMutableMat4).data);

            outData[0] = (a[5] * b05 - a[6] * b04 + a[7] * b03) * det;
            outData[1] = (-a[1] * b05 + a[2] * b04 - a[3] * b03) * det;
            outData[2] = (a[13] * a05 - a[14] * a04 + a[15] * a03) * det;
            outData[3] = (-a[9] * a05 + a[10] * a04 - a[11] * a03) * det;
            outData[4] = (-a[4] * b05 + a[6] * b02 - a[7] * b01) * det;
            outData[5] = (a[0] * b05 - a[2] * b02 + a[3] * b01) * det;
            outData[6] = (-a[12] * a05 + a[14] * a02 - a[15] * a01) * det;
            outData[7] = (a[8] * a05 - a[10] * a02 + a[11] * a01) * det;
            outData[8] = (a[4] * b04 - a[5] * b02 + a[7] * b00) * det;
            outData[9] = (-a[0] * b04 + a[1] * b02 - a[3] * b00) * det;
            outData[10] = (a[12] * a04 - a[13] * a02 + a[15] * a00) * det;
            outData[11] = (-a[8] * a04 + a[9] * a02 - a[11] * a00) * det;
            outData[12] = (-a[4] * b03 + a[5] * b01 - a[6] * b00) * det;
            outData[13] = (a[0] * b03 - a[1] * b01 + a[2] * b00) * det;
            outData[14] = (-a[12] * a03 + a[13] * a01 - a[14] * a00) * det;
            outData[15] = (a[8] * a03 - a[9] * a01 + a[10] * a00) * det;

            return out as MatrixOperationReturnType<V, T>;
        } else {
            return new Mat4([
                (a[5] * b05 - a[6] * b04 + a[7] * b03) * det,
                (-a[1] * b05 + a[2] * b04 - a[3] * b03) * det,
                (a[13] * a05 - a[14] * a04 + a[15] * a03) * det,
                (-a[9] * a05 + a[10] * a04 - a[11] * a03) * det,
                (-a[4] * b05 + a[6] * b02 - a[7] * b01) * det,
                (a[0] * b05 - a[2] * b02 + a[3] * b01) * det,
                (-a[12] * a05 + a[14] * a02 - a[15] * a01) * det,
                (a[8] * a05 - a[10] * a02 + a[11] * a01) * det,
                (a[4] * b04 - a[5] * b02 + a[7] * b00) * det,
                (-a[0] * b04 + a[1] * b02 - a[3] * b00) * det,
                (a[12] * a04 - a[13] * a02 + a[15] * a00) * det,
                (-a[8] * a04 + a[9] * a02 - a[11] * a00) * det,
                (-a[4] * b03 + a[5] * b01 - a[6] * b00) * det,
                (a[0] * b03 - a[1] * b01 + a[2] * b00) * det,
                (-a[12] * a03 + a[13] * a01 - a[14] * a00) * det,
                (a[8] * a03 - a[9] * a01 + a[10] * a00) * det,
            ]) as MatrixOperationReturnType<V, T>;
        }
    }
}

