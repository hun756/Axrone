import { describe, expect, it } from 'vitest';
import {
    DEFAULT_TERRAIN_NOISE_OPTIONS,
    TERRAIN_RESOLUTIONS,
    TerrainError,
    TerrainErrorCode,
    TerrainHeightmap,
    buildTerrainMesh,
    createTerrainHeightfieldSource,
    decodeHeightmapFromImageData,
    generateNoiseHeightmap,
    isTerrainResolution,
    resolveTerrainNoiseOptions,
    validateTerrainDescriptor,
    validateTerrainFoliageLayers,
    type TerrainDescriptor,
    type TerrainFoliageLayer,
} from '../index';

const createDescriptor = (overrides: Partial<TerrainDescriptor> = {}): TerrainDescriptor => ({
    width: 100,
    length: 80,
    maxHeight: 30,
    resolution: 33,
    ...overrides,
});

describe('TerrainError', () => {
    it('stores code, timestamp, details and prefixes the message', () => {
        const error = new TerrainError('bad resolution', 'INVALID_RESOLUTION' as any, {
            resolution: 42,
        });

        expect(error.message).toBe('Terrain: bad resolution');
        expect(error.code).toBe('INVALID_RESOLUTION');
        expect(error.name).toBe('TerrainError');
        expect(error.timestamp).toBeGreaterThan(0);
        expect(error.details).toEqual({ resolution: 42 });
    });

    it('survives instanceof through the prototype chain', () => {
        const error = new TerrainError('test', 'VALIDATION_FAILED' as any);
        expect(error).toBeInstanceOf(TerrainError);
        expect(error).toBeInstanceOf(Error);
    });

    it('works without details parameter', () => {
        const error = new TerrainError('msg', 'INVALID_DIMENSIONS' as any);
        expect(error.details).toBeUndefined();
        expect(error.message).toBe('Terrain: msg');
    });
});

describe('terrain descriptor validation', () => {
    it('accepts every supported resolution', () => {
        for (const resolution of TERRAIN_RESOLUTIONS) {
            expect(isTerrainResolution(resolution)).toBe(true);
            expect(() => validateTerrainDescriptor(createDescriptor({ resolution }))).not.toThrow();
        }
    });

    it('rejects unsupported resolutions with INVALID_RESOLUTION', () => {
        try {
            validateTerrainDescriptor(createDescriptor({ resolution: 64 as never }));
            expect.unreachable('expected validation to throw');
        } catch (error) {
            expect(error).toBeInstanceOf(TerrainError);
            expect((error as TerrainError).code).toBe(TerrainErrorCode.INVALID_RESOLUTION);
        }
    });

    it('rejects non-positive dimensions with INVALID_DIMENSIONS', () => {
        for (const overrides of [{ width: 0 }, { length: -5 }, { maxHeight: Number.NaN }]) {
            try {
                validateTerrainDescriptor(createDescriptor(overrides));
                expect.unreachable('expected validation to throw');
            } catch (error) {
                expect(error).toBeInstanceOf(TerrainError);
                expect((error as TerrainError).code).toBe(TerrainErrorCode.INVALID_DIMENSIONS);
            }
        }
    });

    it('accepts maxHeight of exactly zero', () => {
        expect(() => validateTerrainDescriptor(createDescriptor({ maxHeight: 0 }))).not.toThrow();
    });
});

describe('terrain noise options', () => {
    it('fills defaults and freezes the resolved options', () => {
        const resolved = resolveTerrainNoiseOptions({ seed: 7 });

        expect(resolved).toEqual({ ...DEFAULT_TERRAIN_NOISE_OPTIONS, seed: 7 });
        expect(Object.isFrozen(resolved)).toBe(true);
    });

    it('rejects out-of-range parameters with VALIDATION_FAILED', () => {
        for (const options of [
            { frequency: 0 },
            { octaves: 0 },
            { octaves: 9 },
            { octaves: 2.5 },
            { persistence: 0 },
            { persistence: 1.5 },
            { lacunarity: 0.5 },
            { offsetX: Number.NaN },
        ]) {
            try {
                resolveTerrainNoiseOptions(options);
                expect.unreachable('expected option validation to throw');
            } catch (error) {
                expect(error).toBeInstanceOf(TerrainError);
                expect((error as TerrainError).code).toBe(TerrainErrorCode.VALIDATION_FAILED);
            }
        }
    });

    it('accepts boundary values: persistence 1, lacunarity 1, octaves 1 and 8', () => {
        expect(() => resolveTerrainNoiseOptions({ persistence: 1 })).not.toThrow();
        expect(() => resolveTerrainNoiseOptions({ lacunarity: 1 })).not.toThrow();
        expect(() => resolveTerrainNoiseOptions({ octaves: 1 })).not.toThrow();
        expect(() => resolveTerrainNoiseOptions({ octaves: 8 })).not.toThrow();
    });
});

describe('TerrainHeightmap', () => {
    it('creates a flat heightmap with uniform values', () => {
        const heightmap = TerrainHeightmap.createFlat(33, 0.25);

        expect(heightmap.resolution).toBe(33);
        expect(heightmap.heights.length).toBe(33 * 33);
        expect(heightmap.getHeight(0, 0)).toBeCloseTo(0.25);
        expect(heightmap.getHeight(32, 32)).toBeCloseTo(0.25);
    });

    it('rejects mismatched raw height buffers with HEIGHTMAP_SIZE_MISMATCH', () => {
        try {
            TerrainHeightmap.fromRawHeights(33, new Float32Array(10));
            expect.unreachable('expected factory to throw');
        } catch (error) {
            expect(error).toBeInstanceOf(TerrainError);
            expect((error as TerrainError).code).toBe(TerrainErrorCode.HEIGHTMAP_SIZE_MISMATCH);
        }
    });

    it('clamps raw heights into [0, 1] and copies the buffer', () => {
        const raw = new Float32Array(33 * 33);
        raw[0] = -2;
        raw[1] = 3;
        raw[2] = 0.5;

        const heightmap = TerrainHeightmap.fromRawHeights(33, raw);
        raw[2] = 0.9;

        expect(heightmap.getHeight(0, 0)).toBe(0);
        expect(heightmap.getHeight(1, 0)).toBe(1);
        expect(heightmap.getHeight(2, 0)).toBeCloseTo(0.5);
    });

    it('throws on out-of-grid integer samples', () => {
        const heightmap = TerrainHeightmap.createFlat(33);

        expect(() => heightmap.getHeight(-1, 0)).toThrow(TerrainError);
        expect(() => heightmap.getHeight(0, 33)).toThrow(TerrainError);
        expect(() => heightmap.getHeight(0.5, 0)).toThrow(TerrainError);
    });

    it('samples bilinearly between grid points', () => {
        const raw = new Float32Array(33 * 33);
        raw[0] = 0;
        raw[1] = 1;

        const heightmap = TerrainHeightmap.fromRawHeights(33, raw);
        const midpointU = 0.5 / 32;

        expect(heightmap.sampleHeight(0, 0)).toBeCloseTo(0);
        expect(heightmap.sampleHeight(1 / 32, 0)).toBeCloseTo(1);
        expect(heightmap.sampleHeight(midpointU, 0)).toBeCloseTo(0.5);
        expect(heightmap.sampleHeight(-1, -1)).toBeCloseTo(0);
    });

    it('normalizes heights to span the full range', () => {
        const raw = new Float32Array(33 * 33).fill(0.4);
        raw[0] = 0.2;
        raw[1] = 0.6;

        const normalized = TerrainHeightmap.fromRawHeights(33, raw).normalize();
        const statistics = normalized.getStatistics();

        expect(statistics.min).toBeCloseTo(0);
        expect(statistics.max).toBeCloseTo(1);
        expect(normalized.getHeight(0, 0)).toBeCloseTo(0);
        expect(normalized.getHeight(1, 0)).toBeCloseTo(1);
    });

    it('clones into an independent buffer', () => {
        const heightmap = TerrainHeightmap.createFlat(33, 0.5);
        const cloned = heightmap.clone();

        expect(cloned).not.toBe(heightmap);
        expect(cloned.getHeight(5, 5)).toBeCloseTo(0.5);
    });

    it('getStatistics returns correct min, max and mean', () => {
        const raw = new Float32Array(33 * 33);
        raw[0] = 0.1;
        raw[1] = 0.9;
        raw[2] = 0.5;

        const stats = TerrainHeightmap.fromRawHeights(33, raw).getStatistics();

        expect(stats.min).toBeCloseTo(0);
        expect(stats.max).toBeCloseTo(0.9);
        expect(stats.mean).toBeGreaterThan(0);
        expect(stats.mean).toBeLessThan(1);
        expect(Object.isFrozen(stats)).toBe(true);
    });

    it('normalize on a constant heightmap produces all zeros', () => {
        const heightmap = TerrainHeightmap.createFlat(33, 0.5);
        const normalized = heightmap.normalize();

        for (const value of normalized.heights) {
            expect(value).toBe(0);
        }
    });

    it('fromRawHeights rejects non-finite values with VALIDATION_FAILED', () => {
        const raw = new Float32Array(33 * 33);
        raw[0] = Infinity;
        try {
            TerrainHeightmap.fromRawHeights(33, raw);
            expect.unreachable('expected factory to throw');
        } catch (error) {
            expect(error).toBeInstanceOf(TerrainError);
            expect((error as TerrainError).code).toBe(TerrainErrorCode.VALIDATION_FAILED);
        }
    });

    it('sampleHeight clamps at the (1,1) corner', () => {
        const raw = new Float32Array(33 * 33);
        raw[32 * 33 + 32] = 0.8;
        const heightmap = TerrainHeightmap.fromRawHeights(33, raw);

        expect(heightmap.sampleHeight(1, 1)).toBeCloseTo(0.8);
        expect(heightmap.sampleHeight(5, 5)).toBeCloseTo(0.8);
    });
});

describe('decodeHeightmapFromImageData', () => {
    it('rejects malformed pixel buffers with SOURCE_DECODE_FAILED', () => {
        try {
            decodeHeightmapFromImageData({ data: new Uint8ClampedArray(7), width: 2, height: 2 }, 33);
            expect.unreachable('expected decode to throw');
        } catch (error) {
            expect(error).toBeInstanceOf(TerrainError);
            expect((error as TerrainError).code).toBe(TerrainErrorCode.SOURCE_DECODE_FAILED);
        }
    });

    it('resamples a luminance gradient onto the grid', () => {
        const width = 4;
        const height = 4;
        const data = new Uint8ClampedArray(width * height * 4);

        for (let z = 0; z < height; z += 1) {
            for (let x = 0; x < width; x += 1) {
                const value = Math.round((x / (width - 1)) * 255);
                const offset = (z * width + x) * 4;
                data[offset] = value;
                data[offset + 1] = value;
                data[offset + 2] = value;
                data[offset + 3] = 255;
            }
        }

        const heightmap = decodeHeightmapFromImageData({ data, width, height }, 33);

        expect(heightmap.getHeight(0, 0)).toBeCloseTo(0, 2);
        expect(heightmap.getHeight(32, 0)).toBeCloseTo(1, 2);
        expect(heightmap.getHeight(16, 16)).toBeGreaterThan(0.3);
        expect(heightmap.getHeight(16, 16)).toBeLessThan(0.7);
    });

    it('rejects images smaller than 2x2 pixels', () => {
        try {
            decodeHeightmapFromImageData(
                { data: new Uint8ClampedArray(1 * 3 * 4), width: 1, height: 3 },
                33
            );
            expect.unreachable('expected decode to throw');
        } catch (error) {
            expect(error).toBeInstanceOf(TerrainError);
            expect((error as TerrainError).code).toBe(TerrainErrorCode.SOURCE_DECODE_FAILED);
        }
    });

    it('rejects non-integer dimensions', () => {
        try {
            decodeHeightmapFromImageData(
                { data: new Uint8ClampedArray(2.5 * 2 * 4), width: 2.5, height: 2 },
                33
            );
            expect.unreachable('expected decode to throw');
        } catch (error) {
            expect(error).toBeInstanceOf(TerrainError);
            expect((error as TerrainError).code).toBe(TerrainErrorCode.SOURCE_DECODE_FAILED);
        }
    });

    it('applies correct luminance coefficients for pure red vs white', () => {
        const width = 2;
        const height = 2;
        const data = new Uint8ClampedArray(width * height * 4);
        // Pixel (0,0): pure red (255, 0, 0, 255)
        data[0] = 255;
        data[1] = 0;
        data[2] = 0;
        data[3] = 255;
        // Pixel (1,0): white (255, 255, 255, 255)
        data[4] = 255;
        data[5] = 255;
        data[6] = 255;
        data[7] = 255;
        // Pixel (0,1): white
        data[8] = 255;
        data[9] = 255;
        data[10] = 255;
        data[11] = 255;
        // Pixel (1,1): white
        data[12] = 255;
        data[13] = 255;
        data[14] = 255;
        data[15] = 255;

        const heightmap = decodeHeightmapFromImageData({ data, width, height }, 33);
        // Pure red luminance = 0.2126 * 255 / 255 = 0.2126
        expect(heightmap.getHeight(0, 0)).toBeCloseTo(0.2126, 2);
        // White luminance = 1.0
        expect(heightmap.getHeight(32, 0)).toBeCloseTo(1, 2);
    });
});

describe('generateNoiseHeightmap', () => {
    it('is deterministic for identical seeds and options', () => {
        const descriptor = createDescriptor({ resolution: 65 });
        const first = generateNoiseHeightmap(descriptor, { seed: 42 });
        const second = generateNoiseHeightmap(descriptor, { seed: 42 });

        expect(Array.from(first.heights)).toEqual(Array.from(second.heights));
    });

    it('produces different terrain for different seeds', () => {
        const descriptor = createDescriptor({ resolution: 33 });
        const first = generateNoiseHeightmap(descriptor, { seed: 1 });
        const second = generateNoiseHeightmap(descriptor, { seed: 2 });

        let differing = 0;
        for (let index = 0; index < first.heights.length; index += 1) {
            if (first.heights[index] !== second.heights[index]) {
                differing += 1;
            }
        }

        expect(differing).toBeGreaterThan(first.heights.length / 2);
    });

    it('keeps every generated height inside [0, 1]', () => {
        const heightmap = generateNoiseHeightmap(createDescriptor(), { seed: 1337, octaves: 6 });

        for (const value of heightmap.heights) {
            expect(value).toBeGreaterThanOrEqual(0);
            expect(value).toBeLessThanOrEqual(1);
        }
    });
});

describe('buildTerrainMesh', () => {
    it('produces the expected vertex and index layout', () => {
        const descriptor = createDescriptor({ resolution: 33 });
        const mesh = buildTerrainMesh(TerrainHeightmap.createFlat(33), descriptor);

        expect(mesh.vertexCount).toBe(33 * 33);
        expect(mesh.triangleCount).toBe(32 * 32 * 2);
        expect(mesh.positions.length).toBe(mesh.vertexCount * 3);
        expect(mesh.normals.length).toBe(mesh.vertexCount * 3);
        expect(mesh.uvs.length).toBe(mesh.vertexCount * 2);
        expect(mesh.indices.length).toBe(mesh.triangleCount * 3);
    });

    it('centers the grid on the origin and scales heights by maxHeight', () => {
        const descriptor = createDescriptor({ width: 100, length: 80, maxHeight: 30 });
        const mesh = buildTerrainMesh(TerrainHeightmap.createFlat(33, 1), descriptor);

        expect(mesh.positions[0]).toBeCloseTo(-50);
        expect(mesh.positions[1]).toBeCloseTo(30);
        expect(mesh.positions[2]).toBeCloseTo(-40);

        const lastOffset = (mesh.vertexCount - 1) * 3;
        expect(mesh.positions[lastOffset]).toBeCloseTo(50);
        expect(mesh.positions[lastOffset + 2]).toBeCloseTo(40);
    });

    it('emits unit-length upward-facing normals', () => {
        const descriptor = createDescriptor();
        const mesh = buildTerrainMesh(generateNoiseHeightmap(descriptor, { seed: 9 }), descriptor);

        for (let vertexIndex = 0; vertexIndex < mesh.vertexCount; vertexIndex += 1) {
            const offset = vertexIndex * 3;
            const x = mesh.normals[offset]!;
            const y = mesh.normals[offset + 1]!;
            const z = mesh.normals[offset + 2]!;

            expect(Math.sqrt(x * x + y * y + z * z)).toBeCloseTo(1, 4);
            expect(y).toBeGreaterThan(0);
        }
    });

    it('rejects heightmaps whose resolution differs from the descriptor', () => {
        try {
            buildTerrainMesh(TerrainHeightmap.createFlat(65), createDescriptor({ resolution: 33 }));
            expect.unreachable('expected mesh build to throw');
        } catch (error) {
            expect(error).toBeInstanceOf(TerrainError);
            expect((error as TerrainError).code).toBe(TerrainErrorCode.HEIGHTMAP_SIZE_MISMATCH);
        }
    });

    it('produces UVs spanning [0, 1] across the grid', () => {
        const descriptor = createDescriptor({ resolution: 33 });
        const mesh = buildTerrainMesh(TerrainHeightmap.createFlat(33), descriptor);

        expect(mesh.uvs[0]).toBeCloseTo(0);
        expect(mesh.uvs[1]).toBeCloseTo(0);
        const lastVertex = mesh.vertexCount - 1;
        expect(mesh.uvs[lastVertex * 2]).toBeCloseTo(1);
        expect(mesh.uvs[lastVertex * 2 + 1]).toBeCloseTo(1);
    });

    it('produces exactly upward normals for a flat terrain', () => {
        const descriptor = createDescriptor();
        const mesh = buildTerrainMesh(TerrainHeightmap.createFlat(33), descriptor);

        for (let i = 0; i < mesh.vertexCount; i += 1) {
            expect(mesh.normals[i * 3]).toBeCloseTo(0);
            expect(mesh.normals[i * 3 + 1]).toBeCloseTo(1);
            expect(mesh.normals[i * 3 + 2]).toBeCloseTo(0);
        }
    });

    it('maintains index continuity between adjacent cell rows', () => {
        const descriptor = createDescriptor({ resolution: 33 });
        const mesh = buildTerrainMesh(TerrainHeightmap.createFlat(33), descriptor);
        const resolution = 33;

        // Last cell in first row: cellZ=0, cellX=31 → cell index = 31.
        const lastCellFirstRow = resolution - 2;
        const lastOffset = lastCellFirstRow * 6;
        expect(mesh.indices[lastOffset + 5]).toBe(resolution + lastCellFirstRow + 1);

        // First cell in second row: cellZ=1, cellX=0 → cell index = resolution - 1.
        const firstCellSecondRow = resolution - 1;
        const secondRowOffset = firstCellSecondRow * 6;
        expect(mesh.indices[secondRowOffset]).toBe(resolution);
    });

    it('winds triangles CCW from below (-Y view)', () => {
        const descriptor = createDescriptor({ resolution: 33 });
        const mesh = buildTerrainMesh(TerrainHeightmap.createFlat(33), descriptor);

        // Terrain triangles are CCW when viewed from below (standard terrain convention).
        const i0 = mesh.indices[0]!;
        const i1 = mesh.indices[1]!;
        const i2 = mesh.indices[2]!;
        const ax = mesh.positions[i1 * 3]! - mesh.positions[i0 * 3]!;
        const az = mesh.positions[i1 * 3 + 2]! - mesh.positions[i0 * 3 + 2]!;
        const bx = mesh.positions[i2 * 3]! - mesh.positions[i0 * 3]!;
        const bz = mesh.positions[i2 * 3 + 2]! - mesh.positions[i0 * 3 + 2]!;
        expect(ax * bz - az * bx).toBeLessThan(0);
    });
});

describe('createTerrainHeightfieldSource', () => {
    it('maps descriptor dimensions onto heightfield scales', () => {
        const descriptor = createDescriptor({ width: 64, length: 32, maxHeight: 12, resolution: 33 });
        const source = createTerrainHeightfieldSource(TerrainHeightmap.createFlat(33, 1), descriptor);

        expect(source.width).toBe(33);
        expect(source.depth).toBe(33);
        expect(source.scaleX).toBeCloseTo(2);
        expect(source.scaleZ).toBeCloseTo(1);
        expect(source.scaleY).toBe(12);
        expect(source.heights.length).toBe(33 * 33);
        expect(source.heights[0]).toBeCloseTo(1);
    });

    it('copies heights so later heightmap edits cannot leak into physics', () => {
        const heightmap = TerrainHeightmap.createFlat(33, 0.5);
        const source = createTerrainHeightfieldSource(heightmap, createDescriptor({ resolution: 33 }));

        expect(source.heights).not.toBe(heightmap.heights);
        expect(Object.isFrozen(source)).toBe(true);
    });

    it('rejects heightmaps whose resolution differs from the descriptor', () => {
        try {
            createTerrainHeightfieldSource(
                TerrainHeightmap.createFlat(65),
                createDescriptor({ resolution: 33 })
            );
            expect.unreachable('expected heightfield to throw');
        } catch (error) {
            expect(error).toBeInstanceOf(TerrainError);
            expect((error as TerrainError).code).toBe(TerrainErrorCode.HEIGHTMAP_SIZE_MISMATCH);
        }
    });
});
