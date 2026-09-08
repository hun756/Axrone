import { SOA_EPSILON } from './soa-common';

export function composeMatrix(
    target: Float32Array | Float64Array,
    to: number,
    t: ArrayLike<number>,
    tOff: number,
    r: ArrayLike<number>,
    rOff: number,
    s: ArrayLike<number>,
    sOff: number,
): void {
    const tx = Number(t[tOff] ?? 0);
    const ty = Number(t[tOff + 1] ?? 0);
    const tz = Number(t[tOff + 2] ?? 0);
    const sx = Number(s[sOff] ?? 1);
    const sy = Number(s[sOff + 1] ?? 1);
    const sz = Number(s[sOff + 2] ?? 1);
    let qx = Number(r[rOff] ?? 0);
    let qy = Number(r[rOff + 1] ?? 0);
    let qz = Number(r[rOff + 2] ?? 0);
    let qw = Number(r[rOff + 3] ?? 1);

    const lengthSquared = qx * qx + qy * qy + qz * qz + qw * qw;
    if (lengthSquared <= SOA_EPSILON) {
        qx = 0;
        qy = 0;
        qz = 0;
        qw = 1;
    } else {
        const invLength = 1 / Math.sqrt(lengthSquared);
        qx *= invLength;
        qy *= invLength;
        qz *= invLength;
        qw *= invLength;
    }

    const x2 = qx + qx;
    const y2 = qy + qy;
    const z2 = qz + qz;
    const xx = qx * x2;
    const xy = qx * y2;
    const xz = qx * z2;
    const yy = qy * y2;
    const yz = qy * z2;
    const zz = qz * z2;
    const wx = qw * x2;
    const wy = qw * y2;
    const wz = qw * z2;

    target[to] = (1 - (yy + zz)) * sx;
    target[to + 1] = (xy - wz) * sy;
    target[to + 2] = (xz + wy) * sz;
    target[to + 3] = tx;
    target[to + 4] = (xy + wz) * sx;
    target[to + 5] = (1 - (xx + zz)) * sy;
    target[to + 6] = (yz - wx) * sz;
    target[to + 7] = ty;
    target[to + 8] = (xz - wy) * sx;
    target[to + 9] = (yz + wx) * sy;
    target[to + 10] = (1 - (xx + yy)) * sz;
    target[to + 11] = tz;
    target[to + 12] = 0;
    target[to + 13] = 0;
    target[to + 14] = 0;
    target[to + 15] = 1;
}

export function mat4Multiply(
    target: Float32Array | Float64Array,
    to: number,
    l: ArrayLike<number>,
    lo: number,
    r: ArrayLike<number>,
    ro: number,
): void {
    const a00 = Number(l[lo] ?? 1);
    const a01 = Number(l[lo + 1] ?? 0);
    const a02 = Number(l[lo + 2] ?? 0);
    const a03 = Number(l[lo + 3] ?? 0);
    const a10 = Number(l[lo + 4] ?? 0);
    const a11 = Number(l[lo + 5] ?? 1);
    const a12 = Number(l[lo + 6] ?? 0);
    const a13 = Number(l[lo + 7] ?? 0);
    const a20 = Number(l[lo + 8] ?? 0);
    const a21 = Number(l[lo + 9] ?? 0);
    const a22 = Number(l[lo + 10] ?? 1);
    const a23 = Number(l[lo + 11] ?? 0);
    const a30 = Number(l[lo + 12] ?? 0);
    const a31 = Number(l[lo + 13] ?? 0);
    const a32 = Number(l[lo + 14] ?? 0);
    const a33 = Number(l[lo + 15] ?? 1);
    const b00 = Number(r[ro] ?? 1);
    const b01 = Number(r[ro + 1] ?? 0);
    const b02 = Number(r[ro + 2] ?? 0);
    const b03 = Number(r[ro + 3] ?? 0);
    const b10 = Number(r[ro + 4] ?? 0);
    const b11 = Number(r[ro + 5] ?? 1);
    const b12 = Number(r[ro + 6] ?? 0);
    const b13 = Number(r[ro + 7] ?? 0);
    const b20 = Number(r[ro + 8] ?? 0);
    const b21 = Number(r[ro + 9] ?? 0);
    const b22 = Number(r[ro + 10] ?? 1);
    const b23 = Number(r[ro + 11] ?? 0);
    const b30 = Number(r[ro + 12] ?? 0);
    const b31 = Number(r[ro + 13] ?? 0);
    const b32 = Number(r[ro + 14] ?? 0);
    const b33 = Number(r[ro + 15] ?? 1);

    target[to] = a00 * b00 + a01 * b10 + a02 * b20 + a03 * b30;
    target[to + 1] = a00 * b01 + a01 * b11 + a02 * b21 + a03 * b31;
    target[to + 2] = a00 * b02 + a01 * b12 + a02 * b22 + a03 * b32;
    target[to + 3] = a00 * b03 + a01 * b13 + a02 * b23 + a03 * b33;
    target[to + 4] = a10 * b00 + a11 * b10 + a12 * b20 + a13 * b30;
    target[to + 5] = a10 * b01 + a11 * b11 + a12 * b21 + a13 * b31;
    target[to + 6] = a10 * b02 + a11 * b12 + a12 * b22 + a13 * b32;
    target[to + 7] = a10 * b03 + a11 * b13 + a12 * b23 + a13 * b33;
    target[to + 8] = a20 * b00 + a21 * b10 + a22 * b20 + a23 * b30;
    target[to + 9] = a20 * b01 + a21 * b11 + a22 * b21 + a23 * b31;
    target[to + 10] = a20 * b02 + a21 * b12 + a22 * b22 + a23 * b32;
    target[to + 11] = a20 * b03 + a21 * b13 + a22 * b23 + a23 * b33;
    target[to + 12] = a30 * b00 + a31 * b10 + a32 * b20 + a33 * b30;
    target[to + 13] = a30 * b01 + a31 * b11 + a32 * b21 + a33 * b31;
    target[to + 14] = a30 * b02 + a31 * b12 + a32 * b22 + a33 * b32;
    target[to + 15] = a30 * b03 + a31 * b13 + a32 * b23 + a33 * b33;
}

export function mat4Invert(
    target: Float32Array | Float64Array,
    to: number,
    src: ArrayLike<number>,
    so: number,
): boolean {
    const a00 = Number(src[so] ?? 1);
    const a01 = Number(src[so + 1] ?? 0);
    const a02 = Number(src[so + 2] ?? 0);
    const a03 = Number(src[so + 3] ?? 0);
    const a10 = Number(src[so + 4] ?? 0);
    const a11 = Number(src[so + 5] ?? 1);
    const a12 = Number(src[so + 6] ?? 0);
    const a13 = Number(src[so + 7] ?? 0);
    const a20 = Number(src[so + 8] ?? 0);
    const a21 = Number(src[so + 9] ?? 0);
    const a22 = Number(src[so + 10] ?? 1);
    const a23 = Number(src[so + 11] ?? 0);
    const a30 = Number(src[so + 12] ?? 0);
    const a31 = Number(src[so + 13] ?? 0);
    const a32 = Number(src[so + 14] ?? 0);
    const a33 = Number(src[so + 15] ?? 1);

    const b00 = a00 * a11 - a01 * a10;
    const b01 = a00 * a12 - a02 * a10;
    const b02 = a00 * a13 - a03 * a10;
    const b03 = a01 * a12 - a02 * a11;
    const b04 = a01 * a13 - a03 * a11;
    const b05 = a02 * a13 - a03 * a12;
    const b06 = a20 * a31 - a21 * a30;
    const b07 = a20 * a32 - a22 * a30;
    const b08 = a20 * a33 - a23 * a30;
    const b09 = a21 * a32 - a22 * a31;
    const b10 = a21 * a33 - a23 * a31;
    const b11 = a22 * a33 - a23 * a32;

    const det = b00 * b11 - b01 * b10 + b02 * b09 + b03 * b08 - b04 * b07 + b05 * b06;
    if (!Number.isFinite(det) || Math.abs(det) <= SOA_EPSILON) {
        return false;
    }

    const invDet = 1 / det;
    target[to] = (a11 * b11 - a12 * b10 + a13 * b09) * invDet;
    target[to + 1] = (a02 * b10 - a01 * b11 - a03 * b09) * invDet;
    target[to + 2] = (a31 * b05 - a32 * b04 + a33 * b03) * invDet;
    target[to + 3] = (a22 * b04 - a21 * b05 - a23 * b03) * invDet;
    target[to + 4] = (a12 * b08 - a10 * b11 - a13 * b07) * invDet;
    target[to + 5] = (a00 * b11 - a02 * b08 + a03 * b07) * invDet;
    target[to + 6] = (a32 * b02 - a30 * b05 - a33 * b01) * invDet;
    target[to + 7] = (a20 * b05 - a22 * b02 + a23 * b01) * invDet;
    target[to + 8] = (a10 * b10 - a11 * b08 + a13 * b06) * invDet;
    target[to + 9] = (a01 * b08 - a00 * b10 - a03 * b06) * invDet;
    target[to + 10] = (a30 * b04 - a31 * b02 + a33 * b00) * invDet;
    target[to + 11] = (a21 * b02 - a20 * b04 - a23 * b00) * invDet;
    target[to + 12] = (a11 * b07 - a10 * b09 - a12 * b06) * invDet;
    target[to + 13] = (a00 * b09 - a01 * b07 + a02 * b06) * invDet;
    target[to + 14] = (a31 * b01 - a30 * b03 - a32 * b00) * invDet;
    target[to + 15] = (a20 * b03 - a21 * b01 + a22 * b00) * invDet;
    return true;
}
