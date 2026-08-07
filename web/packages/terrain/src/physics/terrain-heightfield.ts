import type { TerrainDescriptor, TerrainHeightfieldSource } from '../types';
import { validateTerrainDescriptor } from '../types';
import { TerrainError, TerrainErrorCode } from '../errors';
import type { TerrainHeightmap } from '../heightmap/terrain-heightmap';

/**
 * Produces a heightfield collider source matching the terrain mesh layout:
 * heights stay normalized while `scaleY` carries the world height, and
 * `scaleX`/`scaleZ` describe the world-space cell size, mirroring the
 * physics-3d heightfield local-vertex convention (origin-centered grid).
 */
export const createTerrainHeightfieldSource = (
    heightmap: TerrainHeightmap,
    descriptor: TerrainDescriptor
): TerrainHeightfieldSource => {
    validateTerrainDescriptor(descriptor);

    const resolution = descriptor.resolution;
    if (heightmap.resolution !== resolution) {
        throw new TerrainError(
            `Heightmap resolution ${heightmap.resolution} does not match descriptor resolution ${resolution}.`,
            TerrainErrorCode.HEIGHTMAP_SIZE_MISMATCH,
            { heightmapResolution: heightmap.resolution, descriptorResolution: resolution }
        );
    }

    return Object.freeze({
        heights: new Float32Array(heightmap.heights),
        width: resolution,
        depth: resolution,
        scaleX: descriptor.width / (resolution - 1),
        scaleY: descriptor.maxHeight,
        scaleZ: descriptor.length / (resolution - 1),
    });
};
