import type { TerrainDescriptor, TerrainRaycastHit } from '../types';
import { validateTerrainDescriptor } from '../types';
import type { TerrainHeightmap } from '../heightmap/terrain-heightmap';

export interface TerrainRay {
    readonly origin: { readonly x: number; readonly y: number; readonly z: number };
    readonly direction: { readonly x: number; readonly y: number; readonly z: number };
}

const DEFAULT_MAX_DISTANCE = 10_000;
const REFINE_STEPS = 24;

/**
 * Ray-marches a terrain heightmap in terrain-local space (origin-centered
 * grid, +Y up) and returns the first surface hit. Marching uses a step
 * derived from the grid cell size followed by a bisection refinement, which
 * is robust for editor picking without needing a spatial structure.
 */
export const raycastTerrainHeightmap = (
    heightmap: TerrainHeightmap,
    descriptor: TerrainDescriptor,
    ray: TerrainRay,
    maxDistance: number = DEFAULT_MAX_DISTANCE
): TerrainRaycastHit | null => {
    validateTerrainDescriptor(descriptor);

    const halfWidth = descriptor.width * 0.5;
    const halfLength = descriptor.length * 0.5;
    const direction = normalize(ray.direction);
    if (!direction) {
        return null;
    }

    const heightAt = (x: number, z: number): number =>
        heightmap.sampleHeight((x + halfWidth) / descriptor.width, (z + halfLength) / descriptor.length) *
        descriptor.maxHeight;

    const isAbove = (t: number): boolean => {
        const x = ray.origin.x + direction.x * t;
        const z = ray.origin.z + direction.z * t;
        const y = ray.origin.y + direction.y * t;
        return y > heightAt(x, z);
    };

    const isInsideFootprint = (t: number): boolean => {
        const x = ray.origin.x + direction.x * t;
        const z = ray.origin.z + direction.z * t;
        return x >= -halfWidth && x <= halfWidth && z >= -halfLength && z <= halfLength;
    };

    // March with a step proportional to the smaller cell size; clamp so very
    // large terrains still resolve in bounded iterations.
    const cellSize = Math.min(
        descriptor.width / (descriptor.resolution - 1),
        descriptor.length / (descriptor.resolution - 1)
    );
    const step = Math.max(cellSize * 0.5, maxDistance / 4096);

    let previousT = 0;
    let previousAbove = isAbove(0);

    for (let t = step; t <= maxDistance; t += step) {
        const above = isAbove(t);
        if (previousAbove && !above && isInsideFootprint(t)) {
            // Crossing found — bisect [previousT, t] down to the surface.
            let low = previousT;
            let high = t;
            for (let refine = 0; refine < REFINE_STEPS; refine += 1) {
                const middle = (low + high) * 0.5;
                if (isAbove(middle)) {
                    low = middle;
                } else {
                    high = middle;
                }
            }

            const hitT = (low + high) * 0.5;
            const hitX = ray.origin.x + direction.x * hitT;
            const hitZ = ray.origin.z + direction.z * hitT;
            if (
                hitX < -halfWidth ||
                hitX > halfWidth ||
                hitZ < -halfLength ||
                hitZ > halfLength
            ) {
                return null;
            }

            const u = (hitX + halfWidth) / descriptor.width;
            const v = (hitZ + halfLength) / descriptor.length;
            return {
                point: {
                    x: hitX,
                    y: heightAt(hitX, hitZ),
                    z: hitZ,
                },
                u,
                v,
                distance: hitT,
            };
        }

        previousAbove = above;
        previousT = t;
    }

    return null;
};

const normalize = (direction: {
    readonly x: number;
    readonly y: number;
    readonly z: number;
}): { x: number; y: number; z: number } | null => {
    const magnitude = Math.sqrt(
        direction.x * direction.x + direction.y * direction.y + direction.z * direction.z
    );
    if (!Number.isFinite(magnitude) || magnitude <= 0) {
        return null;
    }

    return {
        x: direction.x / magnitude,
        y: direction.y / magnitude,
        z: direction.z / magnitude,
    };
};
