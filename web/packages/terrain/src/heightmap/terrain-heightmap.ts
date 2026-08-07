import { TerrainError, TerrainErrorCode } from '../errors';
import { isTerrainResolution, type TerrainResolution } from '../types';

/**
 * Immutable-size, normalized heightmap backed by a Float32Array.
 *
 * Heights are stored row-major (`z * resolution + x`) in the [0, 1] range;
 * world-space scaling is applied downstream by the mesh builder and the
 * heightfield source factory.
 */
export class TerrainHeightmap {
    private readonly _resolution: TerrainResolution;
    private readonly _heights: Float32Array;

    private constructor(resolution: TerrainResolution, heights: Float32Array) {
        this._resolution = resolution;
        this._heights = heights;
    }

    static createFlat(resolution: TerrainResolution, height = 0): TerrainHeightmap {
        assertResolution(resolution);
        if (!Number.isFinite(height) || height < 0 || height > 1) {
            throw new TerrainError(
                `Invalid flat height: ${height}. Expected a value in [0, 1].`,
                TerrainErrorCode.VALIDATION_FAILED,
                { height }
            );
        }

        const heights = new Float32Array(resolution * resolution);
        if (height !== 0) {
            heights.fill(height);
        }

        return new TerrainHeightmap(resolution, heights);
    }

    static fromRawHeights(resolution: TerrainResolution, heights: ArrayLike<number>): TerrainHeightmap {
        assertResolution(resolution);

        const expectedLength = resolution * resolution;
        if (heights.length !== expectedLength) {
            throw new TerrainError(
                `Height buffer length ${heights.length} does not match resolution ${resolution} (expected ${expectedLength}).`,
                TerrainErrorCode.HEIGHTMAP_SIZE_MISMATCH,
                { resolution, expectedLength, actualLength: heights.length }
            );
        }

        const copied = new Float32Array(expectedLength);
        for (let index = 0; index < expectedLength; index += 1) {
            const value = Number(heights[index]);
            if (!Number.isFinite(value)) {
                throw new TerrainError(
                    `Height buffer contains a non-finite value at index ${index}.`,
                    TerrainErrorCode.VALIDATION_FAILED,
                    { index }
                );
            }

            copied[index] = value < 0 ? 0 : value > 1 ? 1 : value;
        }

        return new TerrainHeightmap(resolution, copied);
    }

    get resolution(): TerrainResolution {
        return this._resolution;
    }

    /** Read-only view over the normalized height values. */
    get heights(): Readonly<Float32Array> {
        return this._heights;
    }

    getHeight(x: number, z: number): number {
        const resolution = this._resolution;
        if (
            !Number.isInteger(x) ||
            !Number.isInteger(z) ||
            x < 0 ||
            z < 0 ||
            x >= resolution ||
            z >= resolution
        ) {
            throw new TerrainError(
                `Height sample (${x}, ${z}) is outside the ${resolution}x${resolution} grid.`,
                TerrainErrorCode.VALIDATION_FAILED,
                { x, z, resolution }
            );
        }

        return this._heights[z * resolution + x]!;
    }

    /** Bilinear sample using normalized [0, 1] grid coordinates. */
    sampleHeight(u: number, v: number): number {
        const resolution = this._resolution;
        const clampedU = u < 0 ? 0 : u > 1 ? 1 : u;
        const clampedV = v < 0 ? 0 : v > 1 ? 1 : v;

        const gridX = clampedU * (resolution - 1);
        const gridZ = clampedV * (resolution - 1);
        const x0 = Math.floor(gridX);
        const z0 = Math.floor(gridZ);
        const x1 = Math.min(x0 + 1, resolution - 1);
        const z1 = Math.min(z0 + 1, resolution - 1);
        const tx = gridX - x0;
        const tz = gridZ - z0;

        const heights = this._heights;
        const topLeft = heights[z0 * resolution + x0]!;
        const topRight = heights[z0 * resolution + x1]!;
        const bottomLeft = heights[z1 * resolution + x0]!;
        const bottomRight = heights[z1 * resolution + x1]!;

        const top = topLeft + (topRight - topLeft) * tx;
        const bottom = bottomLeft + (bottomRight - bottomLeft) * tx;
        return top + (bottom - top) * tz;
    }

    getStatistics(): { readonly min: number; readonly max: number; readonly mean: number } {
        const heights = this._heights;
        let min = Number.POSITIVE_INFINITY;
        let max = Number.NEGATIVE_INFINITY;
        let total = 0;

        for (let index = 0; index < heights.length; index += 1) {
            const value = heights[index]!;
            if (value < min) {
                min = value;
            }
            if (value > max) {
                max = value;
            }
            total += value;
        }

        return Object.freeze({
            min,
            max,
            mean: total / heights.length,
        });
    }

    /** Returns a copy rescaled so heights span the full [0, 1] range. */
    normalize(): TerrainHeightmap {
        const { min, max } = this.getStatistics();
        const range = max - min;
        const normalized = new Float32Array(this._heights.length);

        if (range > 0) {
            for (let index = 0; index < normalized.length; index += 1) {
                normalized[index] = (this._heights[index]! - min) / range;
            }
        }

        return new TerrainHeightmap(this._resolution, normalized);
    }

    clone(): TerrainHeightmap {
        return new TerrainHeightmap(this._resolution, new Float32Array(this._heights));
    }
}

const assertResolution = (resolution: number): void => {
    if (!isTerrainResolution(resolution)) {
        throw new TerrainError(
            `Invalid resolution: ${resolution}.`,
            TerrainErrorCode.INVALID_RESOLUTION,
            { resolution }
        );
    }
};
