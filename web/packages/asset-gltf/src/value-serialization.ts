import { Mat4, Quat, Vec2, Vec3, Vec4 } from '@axrone/numeric';
import type { GltfSerializedValue } from './asset-ir';

/**
 * Rounds a Float32 value to 6 significant decimal digits before JSON
 * serialization. Float32 has ~7 significant digits; when spread into a JS
 * number (Float64) the extra digits are representation noise, not meaningful
 * data. Rounding cuts per-value text from ~18 chars to ~8 chars on average,
 * reducing animation keyframe JSON by ~55%.
 */
const roundFloat32ForJson = (value: number): number => {
    if (value === 0 || !Number.isFinite(value)) return value;
    const magnitude = Math.pow(10, 5 - Math.floor(Math.log10(Math.abs(value))));
    if (!Number.isFinite(magnitude)) return value;
    return Math.round(value * magnitude) / magnitude;
};

const asSerializedArray = (value: readonly unknown[]): readonly GltfSerializedValue[] =>
    value.map((item) => encodeGltfValue(item));

export const encodeGltfValue = (value: unknown): GltfSerializedValue => {
    if (value === undefined || value === null) {
        return null;
    }

    if (typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean') {
        return value;
    }

    if (value instanceof Vec2) {
        return { $type: 'Vec2', value: [value.x, value.y] };
    }

    if (value instanceof Vec3) {
        return { $type: 'Vec3', value: [value.x, value.y, value.z] };
    }

    if (value instanceof Vec4) {
        return { $type: 'Vec4', value: [value.x, value.y, value.z, value.w] };
    }

    if (value instanceof Quat) {
        return { $type: 'Quat', value: [value.x, value.y, value.z, value.w] };
    }

    if (value instanceof Mat4) {
        return { $type: 'Mat4', value: [...value.data] };
    }

    if (value instanceof Float32Array) {
        return {
            $type: 'Float32Array',
            value: Array.from(value, roundFloat32ForJson),
        };
    }

    if (
        value instanceof Int32Array ||
        value instanceof Uint32Array ||
        value instanceof Uint16Array ||
        value instanceof Uint8Array
    ) {
        return {
            $type: value.constructor.name,
            value: [...value],
        };
    }

    if (Array.isArray(value)) {
        return asSerializedArray(value);
    }

    if (typeof value === 'object') {
        const encoded: Record<string, GltfSerializedValue> = {};

        for (const [key, entry] of Object.entries(value)) {
            if (Object.hasOwn(value, key)) {
                encoded[key] = encodeGltfValue(entry);
            }
        }

        return encoded;
    }

    return String(value);
};