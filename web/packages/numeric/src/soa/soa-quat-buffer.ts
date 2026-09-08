import { SoaBuffer, type SoaBufferOptions } from './soa-buffer';
import { SOA_EPSILON, SOA_SLERP_THRESHOLD } from './soa-common';
import type { IQuatLike } from '../quat';
import type { SoaVec3Buffer } from './soa-vec3-buffer';
import { clamp } from '../clamp';

export class SoaQuatView implements IQuatLike {

    constructor(
        private readonly _buf: SoaQuatBuffer,
        private _idx: number,
    ) {}

    public get _offset(): number { return this._idx * 4; }

    public get x(): number { return this._buf.data[this._offset]; }
    public set x(v: number) { this._buf.data[this._offset] = v; }
    public get y(): number { return this._buf.data[this._offset + 1]; }
    public set y(v: number) { this._buf.data[this._offset + 1] = v; }
    public get z(): number { return this._buf.data[this._offset + 2]; }
    public set z(v: number) { this._buf.data[this._offset + 2] = v; }
    public get w(): number { return this._buf.data[this._offset + 3]; }
    public set w(v: number) { this._buf.data[this._offset + 3] = v; }

    public set(x: number, y: number, z: number, w: number): this {
        const d = this._buf.data, o = this._offset;
        d[o] = x; d[o + 1] = y; d[o + 2] = z; d[o + 3] = w;
        return this;
    }

    public identity(): this { return this.set(0, 0, 0, 1); }

    public copyFrom(src: IQuatLike): this {
        this.x = src.x; this.y = src.y; this.z = src.z; this.w = src.w;
        return this;
    }

    public rebind(index: number): this { this._idx = index; return this; }
    public get index(): number { return this._idx; }
}

export class SoaQuatBuffer extends SoaBuffer<Float32Array | Float64Array>
    implements Iterable<SoaQuatView> {

    private readonly _view: SoaQuatView;

    public get stride(): number { return 4; }

    constructor(options: SoaBufferOptions) {
        super(options, 4);
        this._view = new SoaQuatView(this, 0);
    }

    public static quatIdentity(target: Float32Array | Float64Array, o: number): void {
        target[o] = 0;
        target[o + 1] = 0;
        target[o + 2] = 0;
        target[o + 3] = 1;
    }

    public static quatCopy(
        target: Float32Array | Float64Array,
        to: number,
        src: ArrayLike<number>,
        so: number,
    ): void {
        target[to] = src[so]!;
        target[to + 1] = src[so + 1]!;
        target[to + 2] = src[so + 2]!;
        target[to + 3] = src[so + 3]!;
    }

    public static quatNormalize(
        target: Float32Array | Float64Array,
        to: number,
        src: ArrayLike<number>,
        so: number,
    ): void {
        const x = src[so]!;
        const y = src[so + 1]!;
        const z = src[so + 2]!;
        const w = src[so + 3]!;
        const lenSq = x * x + y * y + z * z + w * w;
        if (lenSq <= SOA_EPSILON) {
            SoaQuatBuffer.quatIdentity(target, to);
            return;
        }
        const invLen = 1.0 / Math.sqrt(lenSq);
        target[to] = x * invLen;
        target[to + 1] = y * invLen;
        target[to + 2] = z * invLen;
        target[to + 3] = w * invLen;
    }

    public static quatDot(
        l: ArrayLike<number>,
        lo: number,
        r: ArrayLike<number>,
        ro: number,
    ): number {
        return (
            l[lo]! * r[ro]! +
            l[lo + 1]! * r[ro + 1]! +
            l[lo + 2]! * r[ro + 2]! +
            l[lo + 3]! * r[ro + 3]!
        );
    }

    public static quatMultiply(
        target: Float32Array | Float64Array,
        to: number,
        l: ArrayLike<number>,
        lo: number,
        r: ArrayLike<number>,
        ro: number,
    ): void {
        const ax = l[lo]!;
        const ay = l[lo + 1]!;
        const az = l[lo + 2]!;
        const aw = l[lo + 3]!;
        const bx = r[ro]!;
        const by = r[ro + 1]!;
        const bz = r[ro + 2]!;
        const bw = r[ro + 3]!;
        target[to] = ax * bw + aw * bx + ay * bz - az * by;
        target[to + 1] = ay * bw + aw * by + az * bx - ax * bz;
        target[to + 2] = az * bw + aw * bz + ax * by - ay * bx;
        target[to + 3] = aw * bw - ax * bx - ay * by - az * bz;
    }

    public static quatInvert(
        target: Float32Array | Float64Array,
        to: number,
        src: ArrayLike<number>,
        so: number,
    ): void {
        const x = src[so]!;
        const y = src[so + 1]!;
        const z = src[so + 2]!;
        const w = src[so + 3]!;
        const lenSq = x * x + y * y + z * z + w * w;
        if (lenSq <= SOA_EPSILON) {
            SoaQuatBuffer.quatIdentity(target, to);
            return;
        }
        const invLenSq = 1.0 / lenSq;
        target[to] = -x * invLenSq;
        target[to + 1] = -y * invLenSq;
        target[to + 2] = -z * invLenSq;
        target[to + 3] = w * invLenSq;
    }

    public static quatSlerp(
        target: Float32Array | Float64Array,
        to: number,
        l: ArrayLike<number>,
        lo: number,
        r: ArrayLike<number>,
        ro: number,
        t: number,
    ): void {
        const ct = t < 0 ? 0 : t > 1 ? 1 : t;
        const ax = l[lo]!;
        const ay = l[lo + 1]!;
        const az = l[lo + 2]!;
        const aw = l[lo + 3]!;
        let bx = r[ro]!;
        let by = r[ro + 1]!;
        let bz = r[ro + 2]!;
        let bw = r[ro + 3]!;

        let dot = ax * bx + ay * by + az * bz + aw * bw;
        if (dot < 0) {
            dot = -dot;
            bx = -bx;
            by = -by;
            bz = -bz;
            bw = -bw;
        }

        let scale0: number;
        let scale1: number;
        if (dot > SOA_SLERP_THRESHOLD) {
            scale0 = 1 - ct;
            scale1 = ct;
        } else {
            const theta = Math.acos(dot);
            const sinTheta = Math.sin(theta);
            scale0 = Math.sin((1 - ct) * theta) / sinTheta;
            scale1 = Math.sin(ct * theta) / sinTheta;
        }

        const x = scale0 * ax + scale1 * bx;
        const y = scale0 * ay + scale1 * by;
        const z = scale0 * az + scale1 * bz;
        const w = scale0 * aw + scale1 * bw;
        const lenSq = x * x + y * y + z * z + w * w;
        if (lenSq <= SOA_EPSILON) {
            SoaQuatBuffer.quatIdentity(target, to);
            return;
        }
        const invLen = 1.0 / Math.sqrt(lenSq);
        target[to] = x * invLen;
        target[to + 1] = y * invLen;
        target[to + 2] = z * invLen;
        target[to + 3] = w * invLen;
    }

    public static quatApplyToVec3(
        target: Float32Array | Float64Array,
        to: number,
        q: ArrayLike<number>,
        qo: number,
        v: ArrayLike<number>,
        vo: number,
    ): void {
        const qx = q[qo]!;
        const qy = q[qo + 1]!;
        const qz = q[qo + 2]!;
        const qw = q[qo + 3]!;
        const vx = v[vo]!;
        const vy = v[vo + 1]!;
        const vz = v[vo + 2]!;

        const tx = 2 * (qy * vz - qz * vy);
        const ty = 2 * (qz * vx - qx * vz);
        const tz = 2 * (qx * vy - qy * vx);
        target[to] = vx + qw * tx + qy * tz - qz * ty;
        target[to + 1] = vy + qw * ty + qz * tx - qx * tz;
        target[to + 2] = vz + qw * tz + qx * ty - qy * tx;
    }

    public static quatAccumulateWeighted(
        acc: Float32Array | Float64Array,
        ao: number,
        ref: Float32Array | Float64Array,
        ro: number,
        src: ArrayLike<number>,
        so: number,
        w: number,
        first: boolean,
    ): void {
        const sign = !first && SoaQuatBuffer.quatDot(ref, ro, src, so) < 0 ? -1 : 1;
        if (first) {
            acc[ao] = 0;
            acc[ao + 1] = 0;
            acc[ao + 2] = 0;
            acc[ao + 3] = 0;
            SoaQuatBuffer.quatCopy(ref, ro, src, so);
        }
        acc[ao] += src[so]! * w * sign;
        acc[ao + 1] += src[so + 1]! * w * sign;
        acc[ao + 2] += src[so + 2]! * w * sign;
        acc[ao + 3] += src[so + 3]! * w * sign;
    }

    public static quatFinalizeWeighted(
        target: Float32Array | Float64Array,
        to: number,
        acc: ArrayLike<number>,
        ao: number,
        tw: number,
    ): void {
        if (tw <= 0) {
            SoaQuatBuffer.quatIdentity(target, to);
            return;
        }
        const invWeight = 1.0 / tw;
        target[to] = acc[ao]! * invWeight;
        target[to + 1] = acc[ao + 1]! * invWeight;
        target[to + 2] = acc[ao + 2]! * invWeight;
        target[to + 3] = acc[ao + 3]! * invWeight;
        SoaQuatBuffer.quatNormalize(target, to, target, to);
    }

    public static quatFromTo(
        target: Float32Array | Float64Array,
        to: number,
        from: ArrayLike<number>,
        fo: number,
        to_vec: ArrayLike<number>,
        tvo: number,
        scratch?: Float32Array,
    ): void {
        let fx = from[fo]!;
        let fy = from[fo + 1]!;
        let fz = from[fo + 2]!;
        let tx = to_vec[tvo]!;
        let ty = to_vec[tvo + 1]!;
        let tz = to_vec[tvo + 2]!;

        const fromLen = Math.sqrt(fx * fx + fy * fy + fz * fz);
        const toLen = Math.sqrt(tx * tx + ty * ty + tz * tz);
        if (fromLen <= SOA_EPSILON || toLen <= SOA_EPSILON) {
            SoaQuatBuffer.quatIdentity(target, to);
            return;
        }

        const invFrom = 1.0 / fromLen;
        fx *= invFrom; fy *= invFrom; fz *= invFrom;
        const invTo = 1.0 / toLen;
        tx *= invTo; ty *= invTo; tz *= invTo;

        const dot = clamp(fx * tx + fy * ty + fz * tz, -1, 1);
        if (dot >= 1 - SOA_EPSILON) {
            SoaQuatBuffer.quatIdentity(target, to);
            return;
        }

        let axisX: number, axisY: number, axisZ: number;
        if (dot <= -1 + SOA_EPSILON) {
            if (Math.abs(fx) > Math.abs(fz)) {
                axisX = -fy; axisY = fx; axisZ = 0;
            } else {
                axisX = 0; axisY = -fz; axisZ = fy;
            }
        } else {
            axisX = fy * tz - fz * ty;
            axisY = fz * tx - fx * tz;
            axisZ = fx * ty - fy * tx;
        }

        const axisLen = Math.sqrt(axisX * axisX + axisY * axisY + axisZ * axisZ);
        if (axisLen <= SOA_EPSILON) {
            axisX = 1; axisY = 0; axisZ = 0;
        } else {
            const invAxis = 1.0 / axisLen;
            axisX *= invAxis; axisY *= invAxis; axisZ *= invAxis;
        }

        const angle = dot <= -1 + SOA_EPSILON ? Math.PI : Math.acos(dot);
        const half = angle * 0.5;
        const sinH = Math.sin(half);
        const qx = axisX * sinH;
        const qy = axisY * sinH;
        const qz = axisZ * sinH;
        const qw = Math.cos(half);
        const invQ = 1.0 / Math.sqrt(qx * qx + qy * qy + qz * qz + qw * qw);
        target[to] = qx * invQ;
        target[to + 1] = qy * invQ;
        target[to + 2] = qz * invQ;
        target[to + 3] = qw * invQ;

        if (scratch !== undefined && scratch.length >= 9) {
            scratch[0] = fx; scratch[1] = fy; scratch[2] = fz;
            scratch[3] = tx; scratch[4] = ty; scratch[5] = tz;
            scratch[6] = axisX; scratch[7] = axisY; scratch[8] = axisZ;
        }
    }

    public getView(index: number): SoaQuatView { return this._view.rebind(index); }
    public createView(index: number): SoaQuatView { return new SoaQuatView(this, index); }

    public setElement(i: number, x: number, y: number, z: number, w: number): this {
        const o = i * 4;
        this._data[o] = x; this._data[o + 1] = y; this._data[o + 2] = z; this._data[o + 3] = w;
        return this;
    }

    public getElement(i: number, out: IQuatLike): IQuatLike {
        const o = i * 4;
        out.x = this._data[o]; out.y = this._data[o + 1];
        out.z = this._data[o + 2]; out.w = this._data[o + 3];
        return out;
    }

    public multiplyElement(di: number, a: SoaQuatBuffer, ai: number, b: SoaQuatBuffer, bi: number): this {
        SoaQuatBuffer.quatMultiply(this._data, di * 4, a._data, ai * 4, b._data, bi * 4);
        return this;
    }

    public slerpElement(di: number, a: SoaQuatBuffer, ai: number, b: SoaQuatBuffer, bi: number, t: number): this {
        SoaQuatBuffer.quatSlerp(this._data, di * 4, a._data, ai * 4, b._data, bi * 4, t);
        return this;
    }

    public normalizeElement(di: number, src: SoaQuatBuffer, si: number): this {
        SoaQuatBuffer.quatNormalize(this._data, di * 4, src._data, si * 4);
        return this;
    }

    public invertElement(di: number, src: SoaQuatBuffer, si: number): this {
        SoaQuatBuffer.quatInvert(this._data, di * 4, src._data, si * 4);
        return this;
    }

    public dotElement(a: SoaQuatBuffer, ai: number, b: SoaQuatBuffer, bi: number): number {
        return SoaQuatBuffer.quatDot(a._data, ai * 4, b._data, bi * 4);
    }

    public applyToVec3(
        qi: number,
        dstVec: SoaVec3Buffer, dstVi: number,
        srcVec: SoaVec3Buffer, srcVi: number,
    ): void {
        SoaQuatBuffer.quatApplyToVec3(dstVec.data, dstVi * 3, this._data, qi * 4, srcVec.data, srcVi * 3);
    }

    public accumulateWeighted(
        accI: number,
        ref: SoaQuatBuffer, refI: number,
        src: SoaQuatBuffer, srcI: number,
        weight: number,
        isFirst: boolean,
    ): this {
        SoaQuatBuffer.quatAccumulateWeighted(
            this._data, accI * 4,
            ref._data, refI * 4,
            src._data, srcI * 4,
            weight, isFirst,
        );
        return this;
    }

    public finalizeWeighted(di: number, totalWeight: number): this {
        SoaQuatBuffer.quatFinalizeWeighted(this._data, di * 4, this._data, di * 4, totalWeight);
        return this;
    }

    public slerpAll(a: SoaQuatBuffer, b: SoaQuatBuffer, t: number): this {
        const n = Math.min(this._count, a._count, b._count);
        for (let i = 0; i < n; i++) this.slerpElement(i, a, i, b, i, t);
        return this;
    }

    public normalizeAll(src: SoaQuatBuffer): this {
        const n = Math.min(this._count, src._count);
        for (let i = 0; i < n; i++) this.normalizeElement(i, src, i);
        return this;
    }

    public copyAllFrom(src: SoaQuatBuffer): this {
        this._data.set(src._data.subarray(0, Math.min(this._count, src._count) * 4));
        return this;
    }

    public forEach(fn: (view: SoaQuatView, index: number) => void): void {
        for (let i = 0; i < this._count; i++) {
            this._view.rebind(i);
            fn(this._view, i);
        }
    }

    public [Symbol.iterator](): Iterator<SoaQuatView> {
        let i = 0;
        const v = new SoaQuatView(this, 0);
        return {
            next: (): IteratorResult<SoaQuatView> => {
                if (i >= this._count) return { done: true, value: undefined };
                v.rebind(i++);
                return { done: false, value: v };
            },
        };
    }
}
