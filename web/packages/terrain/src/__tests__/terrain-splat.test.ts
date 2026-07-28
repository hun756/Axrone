import { describe, expect, it } from 'vitest';
import {
    TERRAIN_MAX_LAYERS,
    TERRAIN_SPLAT_RESOLUTIONS,
    TerrainError,
    TerrainErrorCode,
    applyTerrainSplatBrushStamp,
    createTerrainSplatBuffer,
    decodeTerrainSplat,
    encodeTerrainSplat,
    isTerrainSplatDataPayload,
    resolveTerrainBrushOptions,
    sampleTerrainSplatWeights,
    validateTerrainLayers,
    type TerrainDescriptor,
    type TerrainLayer,
} from '../index';

const createDescriptor = (overrides: Partial<TerrainDescriptor> = {}): TerrainDescriptor => ({
    width: 64,
    length: 64,
    maxHeight: 20,
    resolution: 33,
    ...overrides,
});

const createLayer = (overrides: Partial<TerrainLayer> = {}): TerrainLayer => ({
    id: 'layer_1',
    name: 'Grass',
    textureAsset: '',
    tiling: 8,
    ...overrides,
});

const channelSumAt = (splat: Uint8Array, texel: number): number =>
    splat[texel * 4]! + splat[texel * 4 + 1]! + splat[texel * 4 + 2]! + splat[texel * 4 + 3]!;

describe('createTerrainSplatBuffer', () => {
    it('assigns the full weight to the first layer channel', () => {
        for (const resolution of TERRAIN_SPLAT_RESOLUTIONS) {
            const splat = createTerrainSplatBuffer(resolution);
            expect(splat.length).toBe(resolution * resolution * 4);
            expect(splat[0]).toBe(255);
            expect(splat[1]).toBe(0);
            expect(channelSumAt(splat, 0)).toBe(255);
            expect(channelSumAt(splat, resolution * resolution - 1)).toBe(255);
        }
    });

    it('rejects unsupported resolutions', () => {
        try {
            createTerrainSplatBuffer(100 as never);
            expect.unreachable('expected buffer creation to throw');
        } catch (error) {
            expect(error).toBeInstanceOf(TerrainError);
            expect((error as TerrainError).code).toBe(TerrainErrorCode.INVALID_RESOLUTION);
        }
    });
});

describe('applyTerrainSplatBrushStamp', () => {
    it('increases the target channel and preserves the 255 total invariant', () => {
        const resolution = 64;
        const splat = createTerrainSplatBuffer(resolution);
        const descriptor = createDescriptor();
        const brush = resolveTerrainBrushOptions({ radius: 20, strength: 1, falloff: 1 });

        const changed = applyTerrainSplatBrushStamp({
            splat,
            resolution,
            descriptor,
            brush,
            layerIndex: 1,
            localX: 0,
            localZ: 0,
        });

        expect(changed).toBe(true);
        const centerTexel = (resolution / 2) * resolution + resolution / 2;
        expect(splat[centerTexel * 4 + 1]!).toBeGreaterThan(0);
        expect(splat[centerTexel * 4]!).toBeLessThan(255);

        for (let texel = 0; texel < resolution * resolution; texel += 1) {
            expect(channelSumAt(splat, texel)).toBe(255);
        }
    });

    it('applies weaker weight at the brush edge than the center', () => {
        const resolution = 64;
        const splat = createTerrainSplatBuffer(resolution);
        const descriptor = createDescriptor();
        const brush = resolveTerrainBrushOptions({ radius: 24, strength: 1, falloff: 1 });

        applyTerrainSplatBrushStamp({
            splat,
            resolution,
            descriptor,
            brush,
            layerIndex: 1,
            localX: 0,
            localZ: 0,
        });

        const half = resolution / 2;
        const centerWeight = splat[(half * resolution + half) * 4 + 1]!;
        const edgeWeight = splat[(half * resolution + (half + 10)) * 4 + 1]!;
        expect(centerWeight).toBeGreaterThan(edgeWeight);
        expect(edgeWeight).toBeGreaterThan(0);
    });

    it('converges toward full target weight under repeated stamps', () => {
        const resolution = 64;
        const splat = createTerrainSplatBuffer(resolution);
        const descriptor = createDescriptor();
        const brush = resolveTerrainBrushOptions({ radius: 20, strength: 1, falloff: 1 });
        const centerTexel = (resolution / 2) * resolution + resolution / 2;

        for (let stroke = 0; stroke < 25; stroke += 1) {
            applyTerrainSplatBrushStamp({
                splat,
                resolution,
                descriptor,
                brush,
                layerIndex: 2,
                localX: 0,
                localZ: 0,
            });
        }

        expect(splat[centerTexel * 4 + 2]!).toBeGreaterThan(240);
        expect(channelSumAt(splat, centerTexel)).toBe(255);
    });

    it('rejects out-of-range layer indices', () => {
        const resolution = 64;
        try {
            applyTerrainSplatBrushStamp({
                splat: createTerrainSplatBuffer(resolution),
                resolution,
                descriptor: createDescriptor(),
                brush: resolveTerrainBrushOptions(),
                layerIndex: TERRAIN_MAX_LAYERS,
                localX: 0,
                localZ: 0,
            });
            expect.unreachable('expected stamp to throw');
        } catch (error) {
            expect(error).toBeInstanceOf(TerrainError);
            expect((error as TerrainError).code).toBe(TerrainErrorCode.VALIDATION_FAILED);
        }
    });

    it('rejects mismatched splat buffers with SPLAT_SIZE_MISMATCH', () => {
        try {
            applyTerrainSplatBrushStamp({
                splat: new Uint8Array(16),
                resolution: 64,
                descriptor: createDescriptor(),
                brush: resolveTerrainBrushOptions(),
                layerIndex: 0,
                localX: 0,
                localZ: 0,
            });
            expect.unreachable('expected stamp to throw');
        } catch (error) {
            expect(error).toBeInstanceOf(TerrainError);
            expect((error as TerrainError).code).toBe(TerrainErrorCode.SPLAT_SIZE_MISMATCH);
        }
    });
});

describe('sampleTerrainSplatWeights', () => {
    it('returns normalized weights that sum to ~1', () => {
        const resolution = 64;
        const splat = createTerrainSplatBuffer(resolution);
        applyTerrainSplatBrushStamp({
            splat,
            resolution,
            descriptor: createDescriptor(),
            brush: resolveTerrainBrushOptions({ radius: 24, strength: 1 }),
            layerIndex: 1,
            localX: 0,
            localZ: 0,
        });

        const weights = sampleTerrainSplatWeights(splat, resolution, 0.5, 0.5);
        const total = weights[0] + weights[1] + weights[2] + weights[3];
        expect(total).toBeCloseTo(1, 2);
        expect(weights[1]).toBeGreaterThan(0);
    });
});

describe('terrain splat codec', () => {
    it('round-trips a weight buffer exactly', () => {
        const resolution = 64;
        const splat = createTerrainSplatBuffer(resolution);
        applyTerrainSplatBrushStamp({
            splat,
            resolution,
            descriptor: createDescriptor(),
            brush: resolveTerrainBrushOptions({ radius: 18, strength: 0.7 }),
            layerIndex: 2,
            localX: 4,
            localZ: -3,
        });

        const payload = encodeTerrainSplat(splat);
        expect(isTerrainSplatDataPayload(payload)).toBe(true);

        const decoded = decodeTerrainSplat(payload, resolution);
        expect(Array.from(decoded)).toEqual(Array.from(splat));
    });

    it('rejects payloads without the version prefix', () => {
        try {
            decodeTerrainSplat('not-a-splat', 64);
            expect.unreachable('expected decode to throw');
        } catch (error) {
            expect(error).toBeInstanceOf(TerrainError);
            expect((error as TerrainError).code).toBe(TerrainErrorCode.SOURCE_DECODE_FAILED);
        }
    });

    it('rejects payloads whose length does not match the resolution', () => {
        const payload = encodeTerrainSplat(createTerrainSplatBuffer(64));
        try {
            decodeTerrainSplat(payload, 128);
            expect.unreachable('expected decode to throw');
        } catch (error) {
            expect(error).toBeInstanceOf(TerrainError);
            expect((error as TerrainError).code).toBe(TerrainErrorCode.SPLAT_SIZE_MISMATCH);
        }
    });
});

describe('validateTerrainLayers', () => {
    it('accepts up to the maximum layer count', () => {
        const layers = Array.from({ length: TERRAIN_MAX_LAYERS }, (_, index) =>
            createLayer({ id: `layer_${index}` })
        );
        expect(() => validateTerrainLayers(layers)).not.toThrow();
    });

    it('rejects more than the maximum layer count', () => {
        const layers = Array.from({ length: TERRAIN_MAX_LAYERS + 1 }, (_, index) =>
            createLayer({ id: `layer_${index}` })
        );
        try {
            validateTerrainLayers(layers);
            expect.unreachable('expected validation to throw');
        } catch (error) {
            expect(error).toBeInstanceOf(TerrainError);
            expect((error as TerrainError).code).toBe(TerrainErrorCode.VALIDATION_FAILED);
        }
    });

    it('rejects layers with invalid tiling', () => {
        try {
            validateTerrainLayers([createLayer({ tiling: 0 })]);
            expect.unreachable('expected validation to throw');
        } catch (error) {
            expect(error).toBeInstanceOf(TerrainError);
            expect((error as TerrainError).code).toBe(TerrainErrorCode.VALIDATION_FAILED);
        }
    });
});
