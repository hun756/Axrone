import { SoaBuffer } from './soa-buffer';
import type { SoaBufferOptions } from './soa-common';
import { SoaVec3Buffer } from './soa-vec3-buffer';
import { SoaQuatBuffer } from './soa-quat-buffer';
import { composeMatrix } from './soa-mat4-ops';

export class SoaMatrixPalette extends SoaBuffer<Float32Array> {

    public get stride(): number { return 16; }

    constructor(options: SoaBufferOptions & { boneCount: number }) {
        super({ ...options, arrayType: Float32Array }, 16);
        this.setCount(options.boneCount);
        for (let i = 0; i < this._capacity; i++) {
            const o = i * 16;
            this._data[o] = 1;
            this._data[o + 5] = 1;
            this._data[o + 10] = 1;
            this._data[o + 15] = 1;
        }
    }

    public composeFrom(t: SoaVec3Buffer, r: SoaQuatBuffer, s: SoaVec3Buffer): this {
        const n = Math.min(this._count, t.count, r.count, s.count);
        const td = t.data;
        const rd = r.data;
        const sd = s.data;
        const d = this._data;
        for (let i = 0; i < n; i++) {
            composeMatrix(d, i * 16, td, i * 3, rd, i * 4, sd, i * 3);
        }
        return this;
    }

    public multiplyParent(parent: SoaMatrixPalette): this {
        const d = this._data;
        const m = parent._data;
        const n = Math.min(this._count, parent._count);
        for (let i = 0; i < n; i++) {
            const o = i * 16;
            const a0 = d[o], a1 = d[o + 1], a2 = d[o + 2], a3 = d[o + 3];
            const a4 = d[o + 4], a5 = d[o + 5], a6 = d[o + 6], a7 = d[o + 7];
            const a8 = d[o + 8], a9 = d[o + 9], a10 = d[o + 10], a11 = d[o + 11];
            const a12 = d[o + 12], a13 = d[o + 13], a14 = d[o + 14], a15 = d[o + 15];
            d[o]    = m[o] * a0 + m[o + 1] * a4 + m[o + 2] * a8 + m[o + 3] * a12;
            d[o + 1]  = m[o] * a1 + m[o + 1] * a5 + m[o + 2] * a9 + m[o + 3] * a13;
            d[o + 2]  = m[o] * a2 + m[o + 1] * a6 + m[o + 2] * a10 + m[o + 3] * a14;
            d[o + 3]  = m[o] * a3 + m[o + 1] * a7 + m[o + 2] * a11 + m[o + 3] * a15;
            d[o + 4]  = m[o + 4] * a0 + m[o + 5] * a4 + m[o + 6] * a8 + m[o + 7] * a12;
            d[o + 5]  = m[o + 4] * a1 + m[o + 5] * a5 + m[o + 6] * a9 + m[o + 7] * a13;
            d[o + 6]  = m[o + 4] * a2 + m[o + 5] * a6 + m[o + 6] * a10 + m[o + 7] * a14;
            d[o + 7]  = m[o + 4] * a3 + m[o + 5] * a7 + m[o + 6] * a11 + m[o + 7] * a15;
            d[o + 8]  = m[o + 8] * a0 + m[o + 9] * a4 + m[o + 10] * a8 + m[o + 11] * a12;
            d[o + 9]  = m[o + 8] * a1 + m[o + 9] * a5 + m[o + 10] * a9 + m[o + 11] * a13;
            d[o + 10] = m[o + 8] * a2 + m[o + 9] * a6 + m[o + 10] * a10 + m[o + 11] * a14;
            d[o + 11] = m[o + 8] * a3 + m[o + 9] * a7 + m[o + 10] * a11 + m[o + 11] * a15;
            d[o + 12] = m[o + 12] * a0 + m[o + 13] * a4 + m[o + 14] * a8 + m[o + 15] * a12;
            d[o + 13] = m[o + 12] * a1 + m[o + 13] * a5 + m[o + 14] * a9 + m[o + 15] * a13;
            d[o + 14] = m[o + 12] * a2 + m[o + 13] * a6 + m[o + 14] * a10 + m[o + 15] * a14;
            d[o + 15] = m[o + 12] * a3 + m[o + 13] * a7 + m[o + 14] * a11 + m[o + 15] * a15;
        }
        return this;
    }

    public get gpuData(): Float32Array { return this._data; }
}
