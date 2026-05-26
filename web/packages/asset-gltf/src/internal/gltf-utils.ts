/**
 * @fileoverview gltf-utils.ts
 *
 * Shared utility functions for the GLTF import pipeline.
 * Pure functions with no side effects — safe for tree-shaking.
 *
 * @packageDocumentation
 */

import { deepFreeze, isPlainObject } from '@axrone/utility';

export const isFiniteNumber = (value: unknown): value is number =>
    typeof value === 'number' && Number.isFinite(value);

export const isBooleanTuple3 = (value: unknown): value is readonly [boolean, boolean, boolean] =>
    Array.isArray(value) && value.length === 3 && value.every((entry) => typeof entry === 'boolean');

export const isNumberTuple3 = (value: unknown): value is readonly [number, number, number] =>
    Array.isArray(value) && value.length === 3 && value.every((entry) => isFiniteNumber(entry));

export const isNumberTuple4 = (value: unknown): value is readonly [number, number, number, number] =>
    Array.isArray(value) && value.length === 4 && value.every((entry) => isFiniteNumber(entry));

export const maybeFreeze = <T>(value: T, enabled: boolean): T =>
    (enabled ? (deepFreeze(value) as T) : value);

export const cloneSerializableMetadata = (value: unknown): unknown => {
    if (
        value === null ||
        typeof value === 'string' ||
        typeof value === 'number' ||
        typeof value === 'boolean'
    ) {
        return value;
    }

    if (Array.isArray(value)) {
        return value.map((entry) => cloneSerializableMetadata(entry));
    }

    if (!isPlainObject(value)) {
        return undefined;
    }

    const cloned: Record<string, unknown> = {};
    for (const [key, entry] of Object.entries(value)) {
        const clonedEntry = cloneSerializableMetadata(entry);
        if (clonedEntry !== undefined) {
            cloned[key] = clonedEntry;
        }
    }

    return cloned;
};

export const sanitizeAnimationTags = (value: unknown): readonly string[] | undefined => {
    if (!Array.isArray(value)) {
        return undefined;
    }

    const tags = [...new Set(value.filter((entry): entry is string => typeof entry === 'string' && entry.length > 0))];
    return tags.length > 0 ? Object.freeze(tags) : undefined;
};

export const normalizeVector3Tuple = (
    value: readonly [number, number, number]
): readonly [number, number, number] => {
    const length = Math.hypot(value[0], value[1], value[2]);
    if (length <= Number.EPSILON) {
        return Object.freeze([0, 0, 1]) as readonly [number, number, number];
    }
    return Object.freeze([value[0] / length, value[1] / length, value[2] / length]) as readonly [number, number, number];
};

export const normalizeQuaternionTuple = (
    value: readonly [number, number, number, number]
): readonly [number, number, number, number] => {
    const length = Math.hypot(value[0], value[1], value[2], value[3]);
    if (length <= Number.EPSILON) {
        return Object.freeze([0, 0, 0, 1]) as readonly [number, number, number, number];
    }
    return Object.freeze([
        value[0] / length,
        value[1] / length,
        value[2] / length,
        value[3] / length,
    ]) as readonly [number, number, number, number];
};

export const rotateVectorByQuaternion = (
    vector: readonly [number, number, number],
    quaternion: readonly [number, number, number, number]
): readonly [number, number, number] => {
    const [x, y, z, w] = normalizeQuaternionTuple(quaternion);
    const uvX = y * vector[2] - z * vector[1];
    const uvY = z * vector[0] - x * vector[2];
    const uvZ = x * vector[1] - y * vector[0];
    const uuvX = y * uvZ - z * uvY;
    const uuvY = z * uvX - x * uvZ;
    const uuvZ = x * uvY - y * uvX;
    return normalizeVector3Tuple([
        vector[0] + (uvX * w + uuvX) * 2,
        vector[1] + (uvY * w + uuvY) * 2,
        vector[2] + (uvZ * w + uuvZ) * 2,
    ]);
};