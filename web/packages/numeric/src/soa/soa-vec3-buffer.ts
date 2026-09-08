import { SoaBuffer, type SoaBufferOptions } from './soa-buffer';
import { SOA_EPSILON } from './soa-common';
import type { IVec3Like } from '../vec3';

export class SoaVec3View implements IVec3Like {
    private readonly _buf: SoaVec3Buffer;
    private _idx: number;

    public constructor(buf: SoaVec3Buffer, index: number) {
        this._buf = buf;
        this._idx = index | 0;
    }

    private get _offset(): number {
        return this._idx * 3;
    }

    public get x(): number { return this._buf.data[this._offset]!; }
    public set x(v: number) { this._buf.data[this._offset] = v; }
    public get y(): number { return this._buf.data[this._offset + 1]!; }
    public set y(v: number) { this._buf.data[this._offset + 1] = v; }
    public get z(): number { return this._buf.data[this._offset + 2]!; }
    public set z(v: number) { this._buf.data[this._offset + 2] = v; }

    public set(x: number, y: number, z: number): this {
        const d = this._buf.data;
        const o = this._offset;
        d[o] = x;
        d[o + 1] = y;
        d[o + 2] = z;
        return this;
    }

    public copyFrom(src: IVec3Like): this {
        const d = this._buf.data;
        const o = this._offset;
        d[o] = src.x;
        d[o + 1] = src.y;
        d[o + 2] = src.z;
        return this;
    }

    public copyTo<O extends IVec3Like>(target: O): O {
        const d = this._buf.data;
        const o = this._offset;
        target.x = d[o]!;
        target.y = d[o + 1]!;
        target.z = d[o + 2]!;
        return target;
    }

    public rebind(index: number): this {
        this._idx = index | 0;
        return this;
    }

    public get index(): number { return this._idx; }
}

export class SoaVec3Buffer extends SoaBuffer<Float32Array | Float64Array> implements Iterable<SoaVec3View> {
    private readonly _view: SoaVec3View;

    public constructor(options: SoaBufferOptions) {
        super(options, 3);
        this._view = new SoaVec3View(this, 0);
    }

    public get stride(): number { return 3; }

    public static vec3Set(target: Float32Array | Float64Array, to: number, x: number, y: number, z: number): void {
        target[to] = x;
        target[to + 1] = y;
        target[to + 2] = z;
    }

    public static vec3Copy(target: Float32Array | Float64Array, to: number, src: ArrayLike<number>, so: number): void {
        target[to] = src[so]!;
        target[to + 1] = src[so + 1]!;
        target[to + 2] = src[so + 2]!;
    }

    public static vec3Add(target: Float32Array | Float64Array, to: number, l: ArrayLike<number>, lo: number, r: ArrayLike<number>, ro: number): void {
        target[to] = l[lo]! + r[ro]!;
        target[to + 1] = l[lo + 1]! + r[ro + 1]!;
        target[to + 2] = l[lo + 2]! + r[ro + 2]!;
    }

    public static vec3Subtract(target: Float32Array | Float64Array, to: number, l: ArrayLike<number>, lo: number, r: ArrayLike<number>, ro: number): void {
        target[to] = l[lo]! - r[ro]!;
        target[to + 1] = l[lo + 1]! - r[ro + 1]!;
        target[to + 2] = l[lo + 2]! - r[ro + 2]!;
    }

    public static vec3Multiply(target: Float32Array | Float64Array, to: number, l: ArrayLike<number>, lo: number, r: ArrayLike<number>, ro: number): void {
        target[to] = l[lo]! * r[ro]!;
        target[to + 1] = l[lo + 1]! * r[ro + 1]!;
        target[to + 2] = l[lo + 2]! * r[ro + 2]!;
    }

    public static vec3Scale(target: Float32Array | Float64Array, to: number, src: ArrayLike<number>, so: number, s: number): void {
        target[to] = src[so]! * s;
        target[to + 1] = src[so + 1]! * s;
        target[to + 2] = src[so + 2]! * s;
    }

    public static vec3Lerp(target: Float32Array | Float64Array, to: number, l: ArrayLike<number>, lo: number, r: ArrayLike<number>, ro: number, t: number): void {
        const inv = 1.0 - t;
        target[to] = l[lo]! * inv + r[ro]! * t;
        target[to + 1] = l[lo + 1]! * inv + r[ro + 1]! * t;
        target[to + 2] = l[lo + 2]! * inv + r[ro + 2]! * t;
    }

    public static vec3Dot(l: ArrayLike<number>, lo: number, r: ArrayLike<number>, ro: number): number {
        return l[lo]! * r[ro]! + l[lo + 1]! * r[ro + 1]! + l[lo + 2]! * r[ro + 2]!;
    }

    public static vec3Cross(target: Float32Array | Float64Array, to: number, l: ArrayLike<number>, lo: number, r: ArrayLike<number>, ro: number): void {
        const lx = l[lo]!;
        const ly = l[lo + 1]!;
        const lz = l[lo + 2]!;
        const rx = r[ro]!;
        const ry = r[ro + 1]!;
        const rz = r[ro + 2]!;
        target[to] = ly * rz - lz * ry;
        target[to + 1] = lz * rx - lx * rz;
        target[to + 2] = lx * ry - ly * rx;
    }

    public static vec3LengthSquared(src: ArrayLike<number>, o: number): number {
        const x = src[o]!;
        const y = src[o + 1]!;
        const z = src[o + 2]!;
        return x * x + y * y + z * z;
    }

    public static vec3Length(src: ArrayLike<number>, o: number): number {
        return Math.sqrt(SoaVec3Buffer.vec3LengthSquared(src, o));
    }

    public static vec3Normalize(target: Float32Array | Float64Array, to: number, src: ArrayLike<number>, so: number, fx = 0.0, fy = 0.0, fz = 0.0): void {
        const x = src[so]!;
        const y = src[so + 1]!;
        const z = src[so + 2]!;
        const lenSq = x * x + y * y + z * z;
        if (lenSq > SOA_EPSILON) {
            const inv = 1.0 / Math.sqrt(lenSq);
            target[to] = x * inv;
            target[to + 1] = y * inv;
            target[to + 2] = z * inv;
        } else {
            target[to] = fx;
            target[to + 1] = fy;
            target[to + 2] = fz;
        }
    }

    public getView(index: number): SoaVec3View {
        return this._view.rebind(index);
    }

    public createView(index: number): SoaVec3View {
        return new SoaVec3View(this, index);
    }

    public getElement(index: number, out: IVec3Like): IVec3Like {
        const o = index * 3;
        const d = this._data;
        out.x = d[o]!;
        out.y = d[o + 1]!;
        out.z = d[o + 2]!;
        return out;
    }

    public setElement(i: number, x: number, y: number, z: number): this {
        SoaVec3Buffer.vec3Set(this._data, i * 3, x, y, z);
        return this;
    }

    public addElement(di: number, a: SoaVec3Buffer, ai: number, b: SoaVec3Buffer, bi: number): this {
        SoaVec3Buffer.vec3Add(this._data, di * 3, a._data, ai * 3, b._data, bi * 3);
        return this;
    }

    public subtractElement(di: number, a: SoaVec3Buffer, ai: number, b: SoaVec3Buffer, bi: number): this {
        SoaVec3Buffer.vec3Subtract(this._data, di * 3, a._data, ai * 3, b._data, bi * 3);
        return this;
    }

    public multiplyElement(di: number, a: SoaVec3Buffer, ai: number, b: SoaVec3Buffer, bi: number): this {
        SoaVec3Buffer.vec3Multiply(this._data, di * 3, a._data, ai * 3, b._data, bi * 3);
        return this;
    }

    public scaleElement(di: number, src: SoaVec3Buffer, si: number, s: number): this {
        SoaVec3Buffer.vec3Scale(this._data, di * 3, src._data, si * 3, s);
        return this;
    }

    public lerpElement(di: number, a: SoaVec3Buffer, ai: number, b: SoaVec3Buffer, bi: number, t: number): this {
        SoaVec3Buffer.vec3Lerp(this._data, di * 3, a._data, ai * 3, b._data, bi * 3, t);
        return this;
    }

    public dotElement(a: SoaVec3Buffer, ai: number, b: SoaVec3Buffer, bi: number): number {
        return SoaVec3Buffer.vec3Dot(a._data, ai * 3, b._data, bi * 3);
    }

    public crossElement(di: number, a: SoaVec3Buffer, ai: number, b: SoaVec3Buffer, bi: number): this {
        SoaVec3Buffer.vec3Cross(this._data, di * 3, a._data, ai * 3, b._data, bi * 3);
        return this;
    }

    public normalizeElement(di: number, src: SoaVec3Buffer, si: number): this {
        SoaVec3Buffer.vec3Normalize(this._data, di * 3, src._data, si * 3);
        return this;
    }

    public lengthElement(i: number): number {
        return SoaVec3Buffer.vec3Length(this._data, i * 3);
    }

    public lengthSquaredElement(i: number): number {
        return SoaVec3Buffer.vec3LengthSquared(this._data, i * 3);
    }

    public lerpAll(a: SoaVec3Buffer, b: SoaVec3Buffer, t: number): this {
        const n = Math.min(this._count, a._count, b._count);
        const inv = 1.0 - t;
        const d = this._data;
        const ad = a._data;
        const bd = b._data;
        const total = n * 3;
        const unroll = total & ~3;

        for (let i = 0; i < unroll; i += 4) {
            d[i] = ad[i]! * inv + bd[i]! * t;
            d[i + 1] = ad[i + 1]! * inv + bd[i + 1]! * t;
            d[i + 2] = ad[i + 2]! * inv + bd[i + 2]! * t;
            d[i + 3] = ad[i + 3]! * inv + bd[i + 3]! * t;
        }

        for (let i = unroll; i < total; ++i) {
            d[i] = ad[i]! * inv + bd[i]! * t;
        }

        return this;
    }

    public copyAllFrom(src: SoaVec3Buffer): this {
        const n = Math.min(this._count, src._count) * 3;
        this._data.set(src._data.subarray(0, n));
        return this;
    }

    public forEach(fn: (view: SoaVec3View, index: number) => void): void {
        const count = this._count;
        const v = this._view;
        for (let i = 0; i < count; ++i) {
            v.rebind(i);
            fn(v, i);
        }
    }

    public [Symbol.iterator](): Iterator<SoaVec3View> {
        let index = 0;
        const count = this._count;
        const view = new SoaVec3View(this, 0);

        return {
            next: (): IteratorResult<SoaVec3View> => {
                if (index >= count) {
                    return { done: true, value: undefined };
                }
                view.rebind(index++);
                return { done: false, value: view };
            },
        };
    }
}
