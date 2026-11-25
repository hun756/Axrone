import type { BoundingSphere } from '@axrone/geometry';
import { Vec3 } from '@axrone/numeric';
import type { SceneMeshDefinition } from './types';

const FLOAT_COMPONENT_TYPE = 0x1406;

export type BoundingSphereCenter = Readonly<BoundingSphere>['center'];

interface SceneMeshLocalBox {
    readonly minX: number;
    readonly minY: number;
    readonly minZ: number;
    readonly maxX: number;
    readonly maxY: number;
    readonly maxZ: number;
}

const isFiniteNumber = (value: number): boolean => Number.isFinite(value);

/**
 * Scans the mesh's `position` attribute and returns the raw local-space extent.
 * Shared by both bounds consumers so the sphere and object-box paths can never
 * drift apart on attribute selection, stride/offset validation or reads.
 */
const scanSceneMeshLocalBox = (
    definition: Readonly<SceneMeshDefinition>
): SceneMeshLocalBox | undefined => {
    const positionAttribute = definition.attributes.find(
        (attribute) =>
            attribute.semantic === 'position' &&
            attribute.componentCount >= 3 &&
            (attribute.type === undefined || attribute.type === FLOAT_COMPONENT_TYPE)
    );

    if (!positionAttribute) {
        return undefined;
    }

    const source = definition.vertices;
    const byteOffset = ArrayBuffer.isView(source) ? source.byteOffset : 0;
    const byteLength = ArrayBuffer.isView(source) ? source.byteLength : source.byteLength;
    const vertexStride = Math.max(positionAttribute.stride, positionAttribute.componentCount * 4);
    const vertexCount = definition.vertexCount ?? Math.floor(byteLength / vertexStride);

    if (vertexCount <= 0 || positionAttribute.offset + 12 > vertexStride) {
        return undefined;
    }

    const dataView = new DataView(
        ArrayBuffer.isView(source) ? source.buffer : source,
        byteOffset,
        byteLength
    );

    let minX = Number.POSITIVE_INFINITY;
    let minY = Number.POSITIVE_INFINITY;
    let minZ = Number.POSITIVE_INFINITY;
    let maxX = Number.NEGATIVE_INFINITY;
    let maxY = Number.NEGATIVE_INFINITY;
    let maxZ = Number.NEGATIVE_INFINITY;

    for (let vertexIndex = 0; vertexIndex < vertexCount; vertexIndex += 1) {
        const positionOffset = vertexIndex * vertexStride + positionAttribute.offset;
        if (positionOffset + 12 > byteLength) {
            return undefined;
        }

        const x = dataView.getFloat32(positionOffset, true);
        const y = dataView.getFloat32(positionOffset + 4, true);
        const z = dataView.getFloat32(positionOffset + 8, true);

        minX = Math.min(minX, x);
        minY = Math.min(minY, y);
        minZ = Math.min(minZ, z);
        maxX = Math.max(maxX, x);
        maxY = Math.max(maxY, y);
        maxZ = Math.max(maxZ, z);
    }

    return { minX, minY, minZ, maxX, maxY, maxZ };
};

export const isBoundingSphereCenterTuple = (center: BoundingSphereCenter): center is readonly [number, number, number] =>
    Array.isArray(center);

const cloneCenter = (center: Readonly<BoundingSphere>['center']): readonly [number, number, number] =>
    isBoundingSphereCenterTuple(center) ? [center[0], center[1], center[2]] : [center.x, center.y, center.z];

export const cloneSceneMeshBounds = (
    bounds: Readonly<BoundingSphere> | undefined
): BoundingSphere | undefined => {
    if (!bounds) {
        return undefined;
    }

    return {
        kind: 'sphere',
        center: cloneCenter(bounds.center),
        radius: bounds.radius,
    };
};

export const resolveSceneMeshBounds = (
    definition: Readonly<SceneMeshDefinition>
): BoundingSphere | undefined => {
    if (definition.bounds?.kind === 'sphere') {
        return cloneSceneMeshBounds(definition.bounds);
    }

    const box = scanSceneMeshLocalBox(definition);
    if (!box) {
        return undefined;
    }

    const centerX = (box.minX + box.maxX) * 0.5;
    const centerY = (box.minY + box.maxY) * 0.5;
    const centerZ = (box.minZ + box.maxZ) * 0.5;
    const radius = Math.hypot(box.maxX - centerX, box.maxY - centerY, box.maxZ - centerZ);

    return {
        kind: 'sphere',
        center: [centerX, centerY, centerZ],
        radius,
    };
};

export interface SceneMeshObjectBounds {
    readonly center: Vec3;
    readonly size: Vec3;
}

/**
 * Keyed by definition identity: retained mesh resources are stable across
 * draws, so one AABB scan per mesh covers every draw that reuses it.
 */
const objectBoundsCache = new WeakMap<object, SceneMeshObjectBounds | undefined>();

const buildObjectBounds = (
    definition: Readonly<SceneMeshDefinition>
): SceneMeshObjectBounds | undefined => {
    const sphere = definition.bounds?.kind === 'sphere' ? definition.bounds : undefined;
    const box = sphere ? undefined : scanSceneMeshLocalBox(definition);

    if (!box) {
        if (!sphere) {
            return undefined;
        }

        // No usable position stream: fall back to a conservative cube around
        // the published bounding sphere rather than reporting no bounds at all.
        const center = cloneCenter(sphere.center);
        // Edge length, not half-extent — Unity's `_ObjectBoundsSize` convention.
        const extent = sphere.radius * 2;
        const bounds: SceneMeshObjectBounds = {
            center: new Vec3(center[0], center[1], center[2]),
            size: new Vec3(extent, extent, extent),
        };

        return isFiniteObjectBounds(bounds) ? bounds : undefined;
    }

    const bounds: SceneMeshObjectBounds = {
        center: new Vec3(
            (box.minX + box.maxX) * 0.5,
            (box.minY + box.maxY) * 0.5,
            (box.minZ + box.maxZ) * 0.5
        ),
        size: new Vec3(box.maxX - box.minX, box.maxY - box.minY, box.maxZ - box.minZ),
    };

    return isFiniteObjectBounds(bounds) ? bounds : undefined;
};

const isFiniteVec3 = (vector: Vec3): boolean =>
    isFiniteNumber(vector.x) && isFiniteNumber(vector.y) && isFiniteNumber(vector.z);

const isFiniteObjectBounds = (bounds: SceneMeshObjectBounds): boolean =>
    isFiniteVec3(bounds.center) && isFiniteVec3(bounds.size);

export const resolveSceneMeshObjectBounds = (
    definition: Readonly<SceneMeshDefinition> | undefined
): SceneMeshObjectBounds | undefined => {
    if (!definition) {
        return undefined;
    }

    // `has` before `get` so a memoised negative result is not rescanned per draw.
    if (objectBoundsCache.has(definition)) {
        return objectBoundsCache.get(definition);
    }

    const resolved = buildObjectBounds(definition);
    objectBoundsCache.set(definition, resolved);
    return resolved;
};
