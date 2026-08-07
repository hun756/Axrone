import { deepFreeze, isPlainObject } from '@axrone/utility';
import { EMPTY_ARRAY } from './gltf-constants';
import type { GltfAccessorJson } from '../types';

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
    if (value === null || typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean') {
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
    if (!Array.isArray(value)) return undefined;
    const tags = [...new Set(value.filter((entry): entry is string => typeof entry === 'string' && entry.length > 0))];
    return tags.length > 0 ? Object.freeze(tags) : undefined;
};

export const normalizeVector3Tuple = (
    value: readonly [number, number, number]
): readonly [number, number, number] => {
    const length = Math.hypot(value[0], value[1], value[2]);
    if (length <= Number.EPSILON) return Object.freeze([0, 0, 1]) as readonly [number, number, number];
    return Object.freeze([value[0] / length, value[1] / length, value[2] / length]) as readonly [number, number, number];
};

export const normalizeQuaternionTuple = (
    value: readonly [number, number, number, number]
): readonly [number, number, number, number] => {
    const length = Math.hypot(value[0], value[1], value[2], value[3]);
    if (length <= Number.EPSILON) return Object.freeze([0, 0, 0, 1]) as readonly [number, number, number, number];
    return Object.freeze([value[0] / length, value[1] / length, value[2] / length, value[3] / length]) as readonly [number, number, number, number];
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

export const sanitizeName = (value: string | undefined, fallback: string): string => {
    const trimmed = value?.trim();
    return trimmed && trimmed.length > 0 ? trimmed : fallback;
};

export const ensureArray = <T>(value: readonly T[] | undefined): readonly T[] =>
    value ?? (EMPTY_ARRAY as readonly T[]);

export const freezeMap = <K, V>(map: Map<K, V>): ReadonlyMap<K, V> => {
    const entries = [...map.entries()];
    return Object.freeze(new Map(entries)) as ReadonlyMap<K, V>;
};

export const deduplicate = <T>(items: readonly T[]): readonly T[] =>
    Object.freeze([...new Set(items)]);

export const deduplicateStrings = (items: readonly (string | undefined)[]): readonly string[] =>
    Object.freeze([...new Set(items.filter((item): item is string => typeof item === 'string' && item.length > 0))]);

export const sortedEntries = <K, V>(map: ReadonlyMap<K, V>, compareFn?: (a: K, b: K) => number): readonly (readonly [K, V])[] =>
    Object.freeze([...map.entries()].sort(([a], [b]) => compareFn?.(a, b) ?? 0));

export const isNonNullable = <T>(value: T): value is NonNullable<T> =>
    value !== null && value !== undefined;

export const componentTypeByteSize = (
    componentType: GltfAccessorJson['componentType']
): 1 | 2 | 4 => {
    switch (componentType) {
        case 5120:
        case 5121:
            return 1;
        case 5122:
        case 5123:
            return 2;
        case 5125:
        case 5126:
            return 4;
    }
};

export const accessorComponentCount = (type: GltfAccessorJson['type']): number => {
    switch (type) {
        case 'SCALAR':
            return 1;
        case 'VEC2':
            return 2;
        case 'VEC3':
            return 3;
        case 'VEC4':
        case 'MAT2':
            return 4;
        case 'MAT3':
            return 9;
        case 'MAT4':
            return 16;
    }
};

export const inferCompressedContainer = (
    mimeType: string | undefined,
    uri: string | undefined
): 'ktx2' | 'basisu' | undefined => {
    const normalizedMime = mimeType?.toLowerCase();
    const normalizedUri = uri?.toLowerCase();
    if (normalizedMime === 'image/ktx2' || normalizedUri?.endsWith('.ktx2')) {
        return 'ktx2';
    }
    if (normalizedMime === 'image/basis' || normalizedUri?.endsWith('.basis')) {
        return 'basisu';
    }
    return undefined;
};
