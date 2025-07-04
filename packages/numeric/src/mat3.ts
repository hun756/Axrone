import { Comparer, CompareResult, EqualityComparer, Equatable, ICloneable } from '@axrone/utility';
import { EPSILON, HALF_PI, PI_2 } from './common';
import { IVec2Like } from './vec2';
import { IVec3Like } from './vec3';

declare const __matrix3Brand: unique symbol;
declare const __mutableBrand: unique symbol;
declare const __vec2Brand: unique symbol;
declare const __vec3Brand: unique symbol;

type Matrix3Data = number[] & { readonly [__matrix3Brand]: true };
type MutableMatrix3Data = number[] & {
    readonly [__matrix3Brand]: true;
    readonly [__mutableBrand]: true;
};

export interface IMat3Like<TData extends ArrayLike<number> = ArrayLike<number>> {
    readonly data: TData;
}

interface IMutableMat3<TData extends number[] = number[]> extends IMat3Like<TData> {
    data: TData;
}

type InferMatrixData<T> = T extends { data: infer U } ? U : never;

type IsMatrix3Compatible<T> = T extends { data: ArrayLike<number> } ? true : false;

type IsMutableMatrix3<T> = T extends { data: number[] } ? true : false;

type MatrixOperationReturnType<
    TOut extends IMat3Like | undefined,
    TDefault extends IMat3Like,
    TSecond extends IMat3Like = TDefault,
> = TOut extends IMutableMat3<infer U> ? TOut : TOut extends undefined ? Mat3 : never;

const asMutableMatrix3Data = <T extends number[]>(data: T): T & MutableMatrix3Data => {
    return data as T & MutableMatrix3Data;
};

const ensureMatrix3Data = <T extends ArrayLike<number>>(data: T): T & Matrix3Data => {
    return data as T & Matrix3Data;
};

export class Mat3 implements IMat3Like<Matrix3Data>, ICloneable<Mat3>, Equatable {
    public readonly data: Matrix3Data;

    constructor(values?: ArrayLike<number>) {
        if (values) {
            if (values.length < 9) {
                throw new RangeError('Matrix values array must have at least 9 elements');
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
            ] as Matrix3Data;
        } else {
            // Identity matrix
            this.data = [1, 0, 0, 0, 1, 0, 0, 0, 1] as Matrix3Data;
        }
    }

    static readonly IDENTITY: Readonly<Mat3> = Object.freeze(new Mat3());
    static readonly ZERO: Readonly<Mat3> = Object.freeze(new Mat3([0, 0, 0, 0, 0, 0, 0, 0, 0]));

    static from<T extends IMat3Like>(m: Readonly<T>): Mat3 {
        return new Mat3(m.data);
    }

    static fromArray(arr: ArrayLike<number>, offset: number = 0): Mat3 {
        if (process.env.NODE_ENV === 'development') {
            if (offset < 0) {
                throw new RangeError('Offset cannot be negative');
            }
            if (arr.length < offset + 9) {
                throw new RangeError(
                    `Array must have at least ${offset + 9} elements when using offset ${offset}`
                );
            }
        }

        const values = Array.isArray(arr)
            ? arr.slice(offset, offset + 9)
            : Array.from(arr).slice(offset, offset + 9);

        return new Mat3(values);
    }

    static create(
        m00: number = 1,
        m01: number = 0,
        m02: number = 0,
        m10: number = 0,
        m11: number = 1,
        m12: number = 0,
        m20: number = 0,
        m21: number = 0,
        m22: number = 1
    ): Mat3 {
        return new Mat3([m00, m01, m02, m10, m11, m12, m20, m21, m22]);
    }

    static createFromElements(
        m00: number,
        m01: number,
        m02: number,
        m10: number,
        m11: number,
        m12: number,
        m20: number,
        m21: number,
        m22: number
    ): Mat3 {
        return new Mat3([m00, m01, m02, m10, m11, m12, m20, m21, m22]);
    }

    clone(): Mat3 {
        return new Mat3(this.data);
    }

    equals(other: unknown): boolean {
        if (!(other instanceof Mat3)) return false;

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
            Math.abs(a[8] - b[8]) < EPSILON
        );
    }

    getHashCode(): number {
        let h1 = 2166136261;
        for (let i = 0; i < 9; i++) {
            h1 = Math.imul(h1 ^ Math.floor(this.data[i] * 1000), 16777619);
        }
        return h1 >>> 0;
    }
}
