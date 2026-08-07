
import { clamp as numericClamp, Mat4 } from '@axrone/numeric';
import { ObjectPool } from '@axrone/memory';

export const ANIMATION_EPSILON = 1e-6;

export const clamp = (value: number, min: number, max: number): number =>
    numericClamp(value, min, max);

export const toFloat32Array = (value: readonly number[] | Float32Array): Float32Array =>
    value instanceof Float32Array ? new Float32Array(value) : new Float32Array(value);

export const vec3Set = (
    target: Float32Array,
    offset: number,
    x: number,
    y: number,
    z: number
): void => {
    target[offset] = x;
    target[offset + 1] = y;
    target[offset + 2] = z;
};

export const vec3Copy = (
    target: Float32Array,
    targetOffset: number,
    source: ArrayLike<number>,
    sourceOffset: number
): void => {
    target[targetOffset] = Number(source[sourceOffset] ?? 0);
    target[targetOffset + 1] = Number(source[sourceOffset + 1] ?? 0);
    target[targetOffset + 2] = Number(source[sourceOffset + 2] ?? 0);
};

export const vec3Add = (
    target: Float32Array,
    targetOffset: number,
    left: ArrayLike<number>,
    leftOffset: number,
    right: ArrayLike<number>,
    rightOffset: number
): void => {
    target[targetOffset] = left[leftOffset] + right[rightOffset];
    target[targetOffset + 1] = left[leftOffset + 1] + right[rightOffset + 1];
    target[targetOffset + 2] = left[leftOffset + 2] + right[rightOffset + 2];
};

export const vec3Subtract = (
    target: Float32Array,
    targetOffset: number,
    left: ArrayLike<number>,
    leftOffset: number,
    right: ArrayLike<number>,
    rightOffset: number
): void => {
    target[targetOffset] = left[leftOffset] - right[rightOffset];
    target[targetOffset + 1] = left[leftOffset + 1] - right[rightOffset + 1];
    target[targetOffset + 2] = left[leftOffset + 2] - right[rightOffset + 2];
};

export const vec3Multiply = (
    target: Float32Array,
    targetOffset: number,
    left: ArrayLike<number>,
    leftOffset: number,
    right: ArrayLike<number>,
    rightOffset: number
): void => {
    target[targetOffset] = left[leftOffset] * right[rightOffset];
    target[targetOffset + 1] = left[leftOffset + 1] * right[rightOffset + 1];
    target[targetOffset + 2] = left[leftOffset + 2] * right[rightOffset + 2];
};

export const vec3Scale = (
    target: Float32Array,
    targetOffset: number,
    source: ArrayLike<number>,
    sourceOffset: number,
    scalar: number
): void => {
    target[targetOffset] = source[sourceOffset] * scalar;
    target[targetOffset + 1] = source[sourceOffset + 1] * scalar;
    target[targetOffset + 2] = source[sourceOffset + 2] * scalar;
};

export const vec3Lerp = (
    target: Float32Array,
    targetOffset: number,
    left: ArrayLike<number>,
    leftOffset: number,
    right: ArrayLike<number>,
    rightOffset: number,
    alpha: number
): void => {
    const invAlpha = 1 - alpha;
    target[targetOffset] = left[leftOffset] * invAlpha + right[rightOffset] * alpha;
    target[targetOffset + 1] = left[leftOffset + 1] * invAlpha + right[rightOffset + 1] * alpha;
    target[targetOffset + 2] = left[leftOffset + 2] * invAlpha + right[rightOffset + 2] * alpha;
};

export const vec3Dot = (
    left: ArrayLike<number>,
    leftOffset: number,
    right: ArrayLike<number>,
    rightOffset: number
): number => {
    return (
        left[leftOffset] * right[rightOffset] +
        left[leftOffset + 1] * right[rightOffset + 1] +
        left[leftOffset + 2] * right[rightOffset + 2]
    );
};

export const vec3Cross = (
    target: Float32Array,
    targetOffset: number,
    left: ArrayLike<number>,
    leftOffset: number,
    right: ArrayLike<number>,
    rightOffset: number
): void => {
    const lx = left[leftOffset];
    const ly = left[leftOffset + 1];
    const lz = left[leftOffset + 2];
    const rx = right[rightOffset];
    const ry = right[rightOffset + 1];
    const rz = right[rightOffset + 2];
    target[targetOffset] = ly * rz - lz * ry;
    target[targetOffset + 1] = lz * rx - lx * rz;
    target[targetOffset + 2] = lx * ry - ly * rx;
};

export const vec3LengthSquared = (source: ArrayLike<number>, offset: number): number => {
    const x = Number(source[offset] ?? 0);
    const y = Number(source[offset + 1] ?? 0);
    const z = Number(source[offset + 2] ?? 0);
    return x * x + y * y + z * z;
};

export const vec3Length = (source: ArrayLike<number>, offset: number): number =>
    Math.sqrt(vec3LengthSquared(source, offset));

export const vec3Normalize = (
    target: Float32Array,
    targetOffset: number,
    source: ArrayLike<number>,
    sourceOffset: number,
    fallbackX = 0,
    fallbackY = 0,
    fallbackZ = 0
): void => {
    const x = Number(source[sourceOffset] ?? 0);
    const y = Number(source[sourceOffset + 1] ?? 0);
    const z = Number(source[sourceOffset + 2] ?? 0);
    const length = Math.sqrt(x * x + y * y + z * z);
    if (length <= ANIMATION_EPSILON) {
        target[targetOffset] = fallbackX;
        target[targetOffset + 1] = fallbackY;
        target[targetOffset + 2] = fallbackZ;
        return;
    }
    const invLength = 1 / length;
    target[targetOffset] = x * invLength;
    target[targetOffset + 1] = y * invLength;
    target[targetOffset + 2] = z * invLength;
};

export const quatIdentity = (target: Float32Array, offset: number): void => {
    target[offset] = 0;
    target[offset + 1] = 0;
    target[offset + 2] = 0;
    target[offset + 3] = 1;
};

export const quatCopy = (
    target: Float32Array,
    targetOffset: number,
    source: ArrayLike<number>,
    sourceOffset: number
): void => {
    target[targetOffset] = Number(source[sourceOffset] ?? 0);
    target[targetOffset + 1] = Number(source[sourceOffset + 1] ?? 0);
    target[targetOffset + 2] = Number(source[sourceOffset + 2] ?? 0);
    target[targetOffset + 3] = Number(source[sourceOffset + 3] ?? 1);
};

export const quatNormalize = (
    target: Float32Array,
    targetOffset: number,
    source: ArrayLike<number>,
    sourceOffset: number
): void => {
    const x = Number(source[sourceOffset] ?? 0);
    const y = Number(source[sourceOffset + 1] ?? 0);
    const z = Number(source[sourceOffset + 2] ?? 0);
    const w = Number(source[sourceOffset + 3] ?? 1);
    const lengthSquared = x * x + y * y + z * z + w * w;
    if (lengthSquared <= ANIMATION_EPSILON) {
        quatIdentity(target, targetOffset);
        return;
    }
    const invLength = 1 / Math.sqrt(lengthSquared);
    target[targetOffset] = x * invLength;
    target[targetOffset + 1] = y * invLength;
    target[targetOffset + 2] = z * invLength;
    target[targetOffset + 3] = w * invLength;
};

export const quatDot = (
    left: ArrayLike<number>,
    leftOffset: number,
    right: ArrayLike<number>,
    rightOffset: number
): number =>
    Number(left[leftOffset] ?? 0) * Number(right[rightOffset] ?? 0) +
    Number(left[leftOffset + 1] ?? 0) * Number(right[rightOffset + 1] ?? 0) +
    Number(left[leftOffset + 2] ?? 0) * Number(right[rightOffset + 2] ?? 0) +
    Number(left[leftOffset + 3] ?? 1) * Number(right[rightOffset + 3] ?? 1);

/**
 * Shared sign-corrected weighted quaternion accumulation core used by pose
 * blending and root-motion delta blending. When `isFirst` is true the source
 * also becomes the hemisphere reference for subsequent contributions.
 */
export const quatAccumulateWeighted = (
    accumulator: Float32Array,
    accumulatorOffset: number,
    reference: Float32Array,
    referenceOffset: number,
    source: ArrayLike<number>,
    sourceOffset: number,
    weight: number,
    isFirst: boolean
): void => {
    const sign =
        !isFirst && quatDot(reference, referenceOffset, source, sourceOffset) < 0 ? -1 : 1;
    if (isFirst) {
        accumulator[accumulatorOffset] = 0;
        accumulator[accumulatorOffset + 1] = 0;
        accumulator[accumulatorOffset + 2] = 0;
        accumulator[accumulatorOffset + 3] = 0;
        quatCopy(reference, referenceOffset, source, sourceOffset);
    }
    accumulator[accumulatorOffset] += Number(source[sourceOffset] ?? 0) * weight * sign;
    accumulator[accumulatorOffset + 1] += Number(source[sourceOffset + 1] ?? 0) * weight * sign;
    accumulator[accumulatorOffset + 2] += Number(source[sourceOffset + 2] ?? 0) * weight * sign;
    accumulator[accumulatorOffset + 3] += Number(source[sourceOffset + 3] ?? 1) * weight * sign;
};

/**
 * Finalizes a {@link quatAccumulateWeighted} run: divides by the total weight
 * and renormalizes, falling back to identity for non-positive totals.
 */
export const quatFinalizeWeighted = (
    target: Float32Array,
    targetOffset: number,
    accumulator: ArrayLike<number>,
    accumulatorOffset: number,
    totalWeight: number
): void => {
    if (totalWeight <= 0) {
        quatIdentity(target, targetOffset);
        return;
    }
    const invWeight = 1 / totalWeight;
    target[targetOffset] = Number(accumulator[accumulatorOffset] ?? 0) * invWeight;
    target[targetOffset + 1] = Number(accumulator[accumulatorOffset + 1] ?? 0) * invWeight;
    target[targetOffset + 2] = Number(accumulator[accumulatorOffset + 2] ?? 0) * invWeight;
    target[targetOffset + 3] = Number(accumulator[accumulatorOffset + 3] ?? 0) * invWeight;
    quatNormalize(target, targetOffset, target, targetOffset);
};

export const quatMultiply = (
    target: Float32Array,
    targetOffset: number,
    left: ArrayLike<number>,
    leftOffset: number,
    right: ArrayLike<number>,
    rightOffset: number
): void => {
    const ax = Number(left[leftOffset] ?? 0);
    const ay = Number(left[leftOffset + 1] ?? 0);
    const az = Number(left[leftOffset + 2] ?? 0);
    const aw = Number(left[leftOffset + 3] ?? 1);
    const bx = Number(right[rightOffset] ?? 0);
    const by = Number(right[rightOffset + 1] ?? 0);
    const bz = Number(right[rightOffset + 2] ?? 0);
    const bw = Number(right[rightOffset + 3] ?? 1);
    target[targetOffset] = ax * bw + aw * bx + ay * bz - az * by;
    target[targetOffset + 1] = ay * bw + aw * by + az * bx - ax * bz;
    target[targetOffset + 2] = az * bw + aw * bz + ax * by - ay * bx;
    target[targetOffset + 3] = aw * bw - ax * bx - ay * by - az * bz;
};

export const quatInvert = (
    target: Float32Array,
    targetOffset: number,
    source: ArrayLike<number>,
    sourceOffset: number
): void => {
    const x = Number(source[sourceOffset] ?? 0);
    const y = Number(source[sourceOffset + 1] ?? 0);
    const z = Number(source[sourceOffset + 2] ?? 0);
    const w = Number(source[sourceOffset + 3] ?? 1);
    const lengthSquared = x * x + y * y + z * z + w * w;
    if (lengthSquared <= ANIMATION_EPSILON) {
        quatIdentity(target, targetOffset);
        return;
    }
    const invLengthSquared = 1 / lengthSquared;
    target[targetOffset] = -x * invLengthSquared;
    target[targetOffset + 1] = -y * invLengthSquared;
    target[targetOffset + 2] = -z * invLengthSquared;
    target[targetOffset + 3] = w * invLengthSquared;
};

export const quatSlerp = (
    target: Float32Array,
    targetOffset: number,
    left: ArrayLike<number>,
    leftOffset: number,
    right: ArrayLike<number>,
    rightOffset: number,
    alpha: number
): void => {
    const t = alpha < 0 ? 0 : alpha > 1 ? 1 : alpha;
    const ax = Number(left[leftOffset] ?? 0);
    const ay = Number(left[leftOffset + 1] ?? 0);
    const az = Number(left[leftOffset + 2] ?? 0);
    const aw = Number(left[leftOffset + 3] ?? 1);
    let bx = Number(right[rightOffset] ?? 0);
    let by = Number(right[rightOffset + 1] ?? 0);
    let bz = Number(right[rightOffset + 2] ?? 0);
    let bw = Number(right[rightOffset + 3] ?? 1);

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
    if (dot > 0.9995) {
        scale0 = 1 - t;
        scale1 = t;
    } else {
        const theta = Math.acos(dot);
        const sinTheta = Math.sin(theta);
        scale0 = Math.sin((1 - t) * theta) / sinTheta;
        scale1 = Math.sin(t * theta) / sinTheta;
    }

    const x = scale0 * ax + scale1 * bx;
    const y = scale0 * ay + scale1 * by;
    const z = scale0 * az + scale1 * bz;
    const w = scale0 * aw + scale1 * bw;
    const lengthSquared = x * x + y * y + z * z + w * w;
    if (lengthSquared <= ANIMATION_EPSILON) {
        quatIdentity(target, targetOffset);
        return;
    }
    const invLength = 1 / Math.sqrt(lengthSquared);
    target[targetOffset] = x * invLength;
    target[targetOffset + 1] = y * invLength;
    target[targetOffset + 2] = z * invLength;
    target[targetOffset + 3] = w * invLength;
};

export const quatApplyToVec3 = (
    target: Float32Array,
    targetOffset: number,
    quaternion: ArrayLike<number>,
    quaternionOffset: number,
    vector: ArrayLike<number>,
    vectorOffset: number
): void => {
    const qx = Number(quaternion[quaternionOffset] ?? 0);
    const qy = Number(quaternion[quaternionOffset + 1] ?? 0);
    const qz = Number(quaternion[quaternionOffset + 2] ?? 0);
    const qw = Number(quaternion[quaternionOffset + 3] ?? 1);
    const vx = Number(vector[vectorOffset] ?? 0);
    const vy = Number(vector[vectorOffset + 1] ?? 0);
    const vz = Number(vector[vectorOffset + 2] ?? 0);

    // t = 2 * cross(q.xyz, v); v' = v + q.w * t + cross(q.xyz, t)
    const tx = 2 * (qy * vz - qz * vy);
    const ty = 2 * (qz * vx - qx * vz);
    const tz = 2 * (qx * vy - qy * vx);
    target[targetOffset] = vx + qw * tx + qy * tz - qz * ty;
    target[targetOffset + 1] = vy + qw * ty + qz * tx - qx * tz;
    target[targetOffset + 2] = vz + qw * tz + qx * ty - qy * tx;
};

export const quatFromTo = (
    target: Float32Array,
    targetOffset: number,
    from: ArrayLike<number>,
    fromOffset: number,
    to: ArrayLike<number>,
    toOffset: number,
    scratch: Float32Array
): void => {
    let fx = Number(from[fromOffset] ?? 0);
    let fy = Number(from[fromOffset + 1] ?? 0);
    let fz = Number(from[fromOffset + 2] ?? 0);
    let tx = Number(to[toOffset] ?? 0);
    let ty = Number(to[toOffset + 1] ?? 0);
    let tz = Number(to[toOffset + 2] ?? 0);

    const fromLength = Math.sqrt(fx * fx + fy * fy + fz * fz);
    const toLength = Math.sqrt(tx * tx + ty * ty + tz * tz);
    if (fromLength <= ANIMATION_EPSILON || toLength <= ANIMATION_EPSILON) {
        quatIdentity(target, targetOffset);
        return;
    }

    const invFromLength = 1 / fromLength;
    fx *= invFromLength;
    fy *= invFromLength;
    fz *= invFromLength;
    const invToLength = 1 / toLength;
    tx *= invToLength;
    ty *= invToLength;
    tz *= invToLength;

    const dot = numericClamp(fx * tx + fy * ty + fz * tz, -1, 1);
    if (dot >= 1 - ANIMATION_EPSILON) {
        quatIdentity(target, targetOffset);
        return;
    }

    let axisX: number;
    let axisY: number;
    let axisZ: number;
    if (dot <= -1 + ANIMATION_EPSILON) {
        if (Math.abs(fx) > Math.abs(fz)) {
            axisX = -fy;
            axisY = fx;
            axisZ = 0;
        } else {
            axisX = 0;
            axisY = -fz;
            axisZ = fy;
        }
    } else {
        axisX = fy * tz - fz * ty;
        axisY = fz * tx - fx * tz;
        axisZ = fx * ty - fy * tx;
    }

    const axisLength = Math.sqrt(axisX * axisX + axisY * axisY + axisZ * axisZ);
    if (axisLength <= ANIMATION_EPSILON) {
        axisX = 1;
        axisY = 0;
        axisZ = 0;
    } else {
        const invAxisLength = 1 / axisLength;
        axisX *= invAxisLength;
        axisY *= invAxisLength;
        axisZ *= invAxisLength;
    }

    const angle = dot <= -1 + ANIMATION_EPSILON ? Math.PI : Math.acos(dot);
    const halfAngle = angle * 0.5;
    const sinHalfAngle = Math.sin(halfAngle);
    const qx = axisX * sinHalfAngle;
    const qy = axisY * sinHalfAngle;
    const qz = axisZ * sinHalfAngle;
    const qw = Math.cos(halfAngle);
    const invQuatLength = 1 / Math.sqrt(qx * qx + qy * qy + qz * qz + qw * qw);
    target[targetOffset] = qx * invQuatLength;
    target[targetOffset + 1] = qy * invQuatLength;
    target[targetOffset + 2] = qz * invQuatLength;
    target[targetOffset + 3] = qw * invQuatLength;

    if (scratch.length >= 9) {
        scratch[0] = fx;
        scratch[1] = fy;
        scratch[2] = fz;
        scratch[3] = tx;
        scratch[4] = ty;
        scratch[5] = tz;
        scratch[6] = axisX;
        scratch[7] = axisY;
        scratch[8] = axisZ;
    }
};

export const composeMatrix = (
    target: Float32Array,
    targetOffset: number,
    translation: ArrayLike<number>,
    translationOffset: number,
    rotation: ArrayLike<number>,
    rotationOffset: number,
    scale: ArrayLike<number>,
    scaleOffset: number
): void => {
    const tx = Number(translation[translationOffset] ?? 0);
    const ty = Number(translation[translationOffset + 1] ?? 0);
    const tz = Number(translation[translationOffset + 2] ?? 0);
    const sx = Number(scale[scaleOffset] ?? 1);
    const sy = Number(scale[scaleOffset + 1] ?? 1);
    const sz = Number(scale[scaleOffset + 2] ?? 1);
    let qx = Number(rotation[rotationOffset] ?? 0);
    let qy = Number(rotation[rotationOffset + 1] ?? 0);
    let qz = Number(rotation[rotationOffset + 2] ?? 0);
    let qw = Number(rotation[rotationOffset + 3] ?? 1);

    const lengthSquared = qx * qx + qy * qy + qz * qz + qw * qw;
    if (lengthSquared <= ANIMATION_EPSILON) {
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

    target[targetOffset] = (1 - (yy + zz)) * sx;
    target[targetOffset + 1] = (xy - wz) * sy;
    target[targetOffset + 2] = (xz + wy) * sz;
    target[targetOffset + 3] = tx;
    target[targetOffset + 4] = (xy + wz) * sx;
    target[targetOffset + 5] = (1 - (xx + zz)) * sy;
    target[targetOffset + 6] = (yz - wx) * sz;
    target[targetOffset + 7] = ty;
    target[targetOffset + 8] = (xz - wy) * sx;
    target[targetOffset + 9] = (yz + wx) * sy;
    target[targetOffset + 10] = (1 - (xx + yy)) * sz;
    target[targetOffset + 11] = tz;
    target[targetOffset + 12] = 0;
    target[targetOffset + 13] = 0;
    target[targetOffset + 14] = 0;
    target[targetOffset + 15] = 1;
};

export const mat4Multiply = (
    target: Float32Array,
    targetOffset: number,
    left: ArrayLike<number>,
    leftOffset: number,
    right: ArrayLike<number>,
    rightOffset: number
): void => {
    const a00 = Number(left[leftOffset] ?? 1);
    const a01 = Number(left[leftOffset + 1] ?? 0);
    const a02 = Number(left[leftOffset + 2] ?? 0);
    const a03 = Number(left[leftOffset + 3] ?? 0);
    const a10 = Number(left[leftOffset + 4] ?? 0);
    const a11 = Number(left[leftOffset + 5] ?? 1);
    const a12 = Number(left[leftOffset + 6] ?? 0);
    const a13 = Number(left[leftOffset + 7] ?? 0);
    const a20 = Number(left[leftOffset + 8] ?? 0);
    const a21 = Number(left[leftOffset + 9] ?? 0);
    const a22 = Number(left[leftOffset + 10] ?? 1);
    const a23 = Number(left[leftOffset + 11] ?? 0);
    const a30 = Number(left[leftOffset + 12] ?? 0);
    const a31 = Number(left[leftOffset + 13] ?? 0);
    const a32 = Number(left[leftOffset + 14] ?? 0);
    const a33 = Number(left[leftOffset + 15] ?? 1);
    const b00 = Number(right[rightOffset] ?? 1);
    const b01 = Number(right[rightOffset + 1] ?? 0);
    const b02 = Number(right[rightOffset + 2] ?? 0);
    const b03 = Number(right[rightOffset + 3] ?? 0);
    const b10 = Number(right[rightOffset + 4] ?? 0);
    const b11 = Number(right[rightOffset + 5] ?? 1);
    const b12 = Number(right[rightOffset + 6] ?? 0);
    const b13 = Number(right[rightOffset + 7] ?? 0);
    const b20 = Number(right[rightOffset + 8] ?? 0);
    const b21 = Number(right[rightOffset + 9] ?? 0);
    const b22 = Number(right[rightOffset + 10] ?? 1);
    const b23 = Number(right[rightOffset + 11] ?? 0);
    const b30 = Number(right[rightOffset + 12] ?? 0);
    const b31 = Number(right[rightOffset + 13] ?? 0);
    const b32 = Number(right[rightOffset + 14] ?? 0);
    const b33 = Number(right[rightOffset + 15] ?? 1);

    target[targetOffset] = a00 * b00 + a01 * b10 + a02 * b20 + a03 * b30;
    target[targetOffset + 1] = a00 * b01 + a01 * b11 + a02 * b21 + a03 * b31;
    target[targetOffset + 2] = a00 * b02 + a01 * b12 + a02 * b22 + a03 * b32;
    target[targetOffset + 3] = a00 * b03 + a01 * b13 + a02 * b23 + a03 * b33;
    target[targetOffset + 4] = a10 * b00 + a11 * b10 + a12 * b20 + a13 * b30;
    target[targetOffset + 5] = a10 * b01 + a11 * b11 + a12 * b21 + a13 * b31;
    target[targetOffset + 6] = a10 * b02 + a11 * b12 + a12 * b22 + a13 * b32;
    target[targetOffset + 7] = a10 * b03 + a11 * b13 + a12 * b23 + a13 * b33;
    target[targetOffset + 8] = a20 * b00 + a21 * b10 + a22 * b20 + a23 * b30;
    target[targetOffset + 9] = a20 * b01 + a21 * b11 + a22 * b21 + a23 * b31;
    target[targetOffset + 10] = a20 * b02 + a21 * b12 + a22 * b22 + a23 * b32;
    target[targetOffset + 11] = a20 * b03 + a21 * b13 + a22 * b23 + a23 * b33;
    target[targetOffset + 12] = a30 * b00 + a31 * b10 + a32 * b20 + a33 * b30;
    target[targetOffset + 13] = a30 * b01 + a31 * b11 + a32 * b21 + a33 * b31;
    target[targetOffset + 14] = a30 * b02 + a31 * b12 + a32 * b22 + a33 * b32;
    target[targetOffset + 15] = a30 * b03 + a31 * b13 + a32 * b23 + a33 * b33;
};

const setMat4Identity = (value: Mat4): void => {
    const data = value.data;
    data[0] = 1;
    data[1] = 0;
    data[2] = 0;
    data[3] = 0;
    data[4] = 0;
    data[5] = 1;
    data[6] = 0;
    data[7] = 0;
    data[8] = 0;
    data[9] = 0;
    data[10] = 1;
    data[11] = 0;
    data[12] = 0;
    data[13] = 0;
    data[14] = 0;
    data[15] = 1;
};

const loadMat4 = (source: ArrayLike<number>, offset: number, out: Mat4): Mat4 => {
    const data = out.data;
    for (let index = 0; index < 16; index += 1) {
        data[index] = Number(source[offset + index] ?? (index % 5 === 0 ? 1 : 0));
    }
    return out;
};

const writeMat4 = (target: Float32Array, offset: number, value: Mat4): void => {
    const data = value.data;
    for (let index = 0; index < 16; index += 1) {
        target[offset + index] = Number(data[index] ?? (index % 5 === 0 ? 1 : 0));
    }
};

const mat4Pool = new ObjectPool<Mat4>({
    initialCapacity: 2,
    maxCapacity: 8,
    minFree: 0,
    expansionStrategy: 'multiplicative',
    expansionFactor: 1.5,
    allocationStrategy: 'least-recently-used',
    evictionPolicy: 'lru',
    resetOnRecycle: true,
    preallocate: false,
    autoExpand: true,
    enableMetrics: false,
    name: 'AnimationMat4Pool',
    factory: () => new Mat4(),
    resetHandler: (value) => {
        setMat4Identity(value);
    },
});

export const mat4Invert = (
    target: Float32Array,
    targetOffset: number,
    source: ArrayLike<number>,
    sourceOffset: number
): boolean => {
    const sourceMatrix = mat4Pool.acquire();
    const resultMatrix = mat4Pool.acquire();
    try {
        try {
            Mat4.invert(loadMat4(source, sourceOffset, sourceMatrix), resultMatrix);
        } catch {
            return false;
        }
        writeMat4(target, targetOffset, resultMatrix);
        return true;
    } finally {
        mat4Pool.release(resultMatrix);
        mat4Pool.release(sourceMatrix);
    }
};
