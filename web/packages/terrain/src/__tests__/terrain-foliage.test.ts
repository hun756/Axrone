import { describe, expect, it } from 'vitest';
import {
    TERRAIN_MAX_FOLIAGE_INSTANCES,
    TERRAIN_MAX_FOLIAGE_LAYERS,
    TerrainError,
    TerrainErrorCode,
    TerrainHeightmap,
    applyTerrainFoliageDensityStamp,
    buildTerrainFoliageBatchMesh,
    createTerrainFoliageCardMesh,
    createTerrainFoliageDensityBuffer,
    decodeTerrainFoliageDensity,
    encodeTerrainFoliageDensity,
    isTerrainFoliageDensityPayload,
    resolveTerrainBrushOptions,
    sampleTerrainFoliageDensity,
    scatterTerrainFoliage,
    validateTerrainFoliageLayers,
    type TerrainDescriptor,
    type TerrainFoliageLayer,
    type TerrainFoliageSourceMesh,
} from '../index';

const createDescriptor = (overrides: Partial<TerrainDescriptor> = {}): TerrainDescriptor => ({
    width: 64,
    length: 64,
    maxHeight: 20,
    resolution: 33,
    ...overrides,
});

const createLayer = (overrides: Partial<TerrainFoliageLayer> = {}): TerrainFoliageLayer => ({
    id: 'foliage_1',
    name: 'Grass',
    meshAsset: '',
    density: 0.5,
    minScale: 0.8,
    maxScale: 1.4,
    alignToNormal: false,
    maxSlopeDeg: 45,
    ...overrides,
});

describe('validateTerrainFoliageLayers', () => {
    it('accepts a valid layer set', () => {
        expect(() => validateTerrainFoliageLayers([createLayer()])).not.toThrow();
    });

    it('rejects more than the layer cap', () => {
        const layers = Array.from({ length: TERRAIN_MAX_FOLIAGE_LAYERS + 1 }, (_, index) =>
            createLayer({ id: `foliage_${index}` })
        );
        try {
            validateTerrainFoliageLayers(layers);
            expect.unreachable('expected validation to throw');
        } catch (error) {
            expect(error).toBeInstanceOf(TerrainError);
            expect((error as TerrainError).code).toBe(TerrainErrorCode.VALIDATION_FAILED);
        }
    });

    it('rejects out-of-range density and scale', () => {
        expect(() => validateTerrainFoliageLayers([createLayer({ density: 1.5 })])).toThrow(
            TerrainError
        );
        expect(() =>
            validateTerrainFoliageLayers([createLayer({ minScale: 2, maxScale: 1 })])
        ).toThrow(TerrainError);
        expect(() => validateTerrainFoliageLayers([createLayer({ maxSlopeDeg: 120 })])).toThrow(
            TerrainError
        );
    });

    it('rejects empty-string id and negative density', () => {
        expect(() => validateTerrainFoliageLayers([createLayer({ id: '' })])).toThrow(
            TerrainError
        );
        expect(() => validateTerrainFoliageLayers([createLayer({ density: -0.5 })])).toThrow(
            TerrainError
        );
    });
});

describe('applyTerrainFoliageDensityStamp', () => {
    it('raises density inside the brush radius only', () => {
        const density = createTerrainFoliageDensityBuffer(64);
        const changed = applyTerrainFoliageDensityStamp({
            density,
            resolution: 64,
            descriptor: createDescriptor(),
            brush: resolveTerrainBrushOptions({ radius: 10, strength: 1 }),
            localX: 0,
            localZ: 0,
        });

        expect(changed).toBe(true);
        expect(sampleTerrainFoliageDensity(density, 64, 0.5, 0.5)).toBeGreaterThan(0);
        expect(sampleTerrainFoliageDensity(density, 64, 0, 0)).toBe(0);
    });

    it('erases previously painted density', () => {
        const density = createTerrainFoliageDensityBuffer(64);
        const descriptor = createDescriptor();
        const brush = resolveTerrainBrushOptions({ radius: 12, strength: 1 });
        for (let stamp = 0; stamp < 12; stamp += 1) {
            applyTerrainFoliageDensityStamp({
                density,
                resolution: 64,
                descriptor,
                brush,
                localX: 0,
                localZ: 0,
            });
        }
        const painted = sampleTerrainFoliageDensity(density, 64, 0.5, 0.5);
        expect(painted).toBeGreaterThan(0.8);

        for (let stamp = 0; stamp < 24; stamp += 1) {
            applyTerrainFoliageDensityStamp({
                density,
                resolution: 64,
                descriptor,
                brush,
                localX: 0,
                localZ: 0,
                erase: true,
            });
        }
        expect(sampleTerrainFoliageDensity(density, 64, 0.5, 0.5)).toBe(0);
    });

    it('rejects mismatched buffer lengths', () => {
        try {
            applyTerrainFoliageDensityStamp({
                density: new Uint8Array(16),
                resolution: 64,
                descriptor: createDescriptor(),
                brush: resolveTerrainBrushOptions(),
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

describe('createTerrainFoliageDensityBuffer', () => {
    it('creates an all-zero buffer of the correct length', () => {
        const density = createTerrainFoliageDensityBuffer(64);
        expect(density.length).toBe(64 * 64);
        expect(density.every((v) => v === 0)).toBe(true);
    });

    it('rejects invalid resolutions', () => {
        expect(() => createTerrainFoliageDensityBuffer(100 as never)).toThrow(TerrainError);
    });
});

describe('sampleTerrainFoliageDensity', () => {
    it('returns exact texel values at corners', () => {
        const density = createTerrainFoliageDensityBuffer(64);
        density[0] = 128;
        density[63] = 255;

        expect(sampleTerrainFoliageDensity(density, 64, 0, 0)).toBeCloseTo(128 / 255, 2);
        expect(sampleTerrainFoliageDensity(density, 64, 1, 0)).toBeCloseTo(1, 2);
    });

    it('clamps sampling outside [0, 1] to edge texels', () => {
        const density = createTerrainFoliageDensityBuffer(64);
        density[0] = 200;

        const inside = sampleTerrainFoliageDensity(density, 64, 0, 0);
        const outside = sampleTerrainFoliageDensity(density, 64, -0.5, -0.5);
        expect(outside).toBeCloseTo(inside, 5);
    });
});

describe('foliage density codec', () => {
    it('round-trips painted buffers', () => {
        const density = createTerrainFoliageDensityBuffer(64);
        applyTerrainFoliageDensityStamp({
            density,
            resolution: 64,
            descriptor: createDescriptor(),
            brush: resolveTerrainBrushOptions({ radius: 16, strength: 1 }),
            localX: 4,
            localZ: -6,
        });

        const payload = encodeTerrainFoliageDensity(density);
        expect(isTerrainFoliageDensityPayload(payload)).toBe(true);
        expect(decodeTerrainFoliageDensity(payload, 64)).toEqual(density);
    });

    it('rejects foreign payloads and wrong resolutions', () => {
        expect(isTerrainFoliageDensityPayload('atsp1:AAAA')).toBe(false);
        expect(() => decodeTerrainFoliageDensity('nope', 64)).toThrow(TerrainError);

        const payload = encodeTerrainFoliageDensity(createTerrainFoliageDensityBuffer(64));
        try {
            decodeTerrainFoliageDensity(payload, 128);
            expect.unreachable('expected decode to throw');
        } catch (error) {
            expect((error as TerrainError).code).toBe(TerrainErrorCode.SPLAT_SIZE_MISMATCH);
        }
    });

    it('isTerrainFoliageDensityPayload returns false for non-strings', () => {
        expect(isTerrainFoliageDensityPayload(42)).toBe(false);
        expect(isTerrainFoliageDensityPayload(null)).toBe(false);
        expect(isTerrainFoliageDensityPayload(undefined)).toBe(false);
    });
});

describe('scatterTerrainFoliage', () => {
    const paintEverywhere = (): Uint8Array => {
        const density = createTerrainFoliageDensityBuffer(64);
        density.fill(255);
        return density;
    };

    it('is deterministic for identical inputs', () => {
        const options = {
            heightmap: TerrainHeightmap.createFlat(33, 0.25),
            descriptor: createDescriptor(),
            density: paintEverywhere(),
            densityResolution: 64 as const,
            layer: createLayer(),
            seed: 1337,
        };

        const first = scatterTerrainFoliage(options);
        const second = scatterTerrainFoliage(options);
        expect(first.length).toBeGreaterThan(0);
        expect(second).toEqual(first);

        const reseeded = scatterTerrainFoliage({ ...options, seed: 7331 });
        expect(reseeded).not.toEqual(first);
    });

    it('scales the instance count with painted density', () => {
        const base = {
            heightmap: TerrainHeightmap.createFlat(33, 0.25),
            descriptor: createDescriptor(),
            densityResolution: 64 as const,
            layer: createLayer({ density: 1 }),
            seed: 42,
        };

        const empty = scatterTerrainFoliage({
            ...base,
            density: createTerrainFoliageDensityBuffer(64),
        });
        expect(empty).toHaveLength(0);

        const half = paintEverywhere();
        half.fill(128);
        const sparse = scatterTerrainFoliage({ ...base, density: half });
        const dense = scatterTerrainFoliage({ ...base, density: paintEverywhere() });
        expect(sparse.length).toBeGreaterThan(0);
        expect(dense.length).toBeGreaterThan(sparse.length * 1.5);
        expect(dense.length).toBeLessThanOrEqual(TERRAIN_MAX_FOLIAGE_INSTANCES);
    });

    it('respects the max slope filter', () => {
        // Yukseklik x ekseninde 0 → 1 rampasi: egim ~17 derece (64 genislik, 20 yukseklik).
        const resolution = 33;
        const heights = new Float32Array(resolution * resolution);
        for (let z = 0; z < resolution; z += 1) {
            for (let x = 0; x < resolution; x += 1) {
                heights[z * resolution + x] = x / (resolution - 1);
            }
        }
        const ramp = TerrainHeightmap.fromRawHeights(resolution, heights);
        const base = {
            heightmap: ramp,
            descriptor: createDescriptor(),
            density: paintEverywhere(),
            densityResolution: 64 as const,
            seed: 42,
        };

        const permissive = scatterTerrainFoliage({
            ...base,
            layer: createLayer({ maxSlopeDeg: 90 }),
        });
        const blocked = scatterTerrainFoliage({
            ...base,
            layer: createLayer({ maxSlopeDeg: 5 }),
        });
        expect(permissive.length).toBeGreaterThan(0);
        expect(blocked).toHaveLength(0);
    });

    it('places instances on the heightmap surface within terrain bounds', () => {
        const descriptor = createDescriptor();
        const instances = scatterTerrainFoliage({
            heightmap: TerrainHeightmap.createFlat(33, 0.5),
            descriptor,
            density: paintEverywhere(),
            densityResolution: 64,
            layer: createLayer(),
            seed: 7,
        });

        for (const instance of instances) {
            expect(Math.abs(instance.x)).toBeLessThanOrEqual(descriptor.width / 2);
            expect(Math.abs(instance.z)).toBeLessThanOrEqual(descriptor.length / 2);
            expect(instance.y).toBeCloseTo(descriptor.maxHeight * 0.5, 5);
            expect(instance.scale).toBeGreaterThanOrEqual(0.8);
            expect(instance.scale).toBeLessThanOrEqual(1.4);
        }
    });

    it('returns an empty frozen list for zero layer density', () => {
        const instances = scatterTerrainFoliage({
            heightmap: TerrainHeightmap.createFlat(33),
            descriptor: createDescriptor(),
            density: paintEverywhere(),
            densityResolution: 64,
            layer: createLayer({ density: 0 }),
            seed: 1,
        });
        expect(instances).toHaveLength(0);
        expect(Object.isFrozen(instances)).toBe(true);
    });

    it('produces instances with scale within [minScale, maxScale]', () => {
        const instances = scatterTerrainFoliage({
            heightmap: TerrainHeightmap.createFlat(33, 0.5),
            descriptor: createDescriptor(),
            density: paintEverywhere(),
            densityResolution: 64,
            layer: createLayer({ minScale: 0.5, maxScale: 2.0 }),
            seed: 99,
        });

        for (const instance of instances) {
            expect(instance.scale).toBeGreaterThanOrEqual(0.5);
            expect(instance.scale).toBeLessThanOrEqual(2.0);
        }
    });

    it('respects the TERRAIN_MAX_FOLIAGE_INSTANCES cap', () => {
        const descriptor = createDescriptor({ width: 512, length: 512 });
        const highDensity = createTerrainFoliageDensityBuffer(64);
        highDensity.fill(255);

        const instances = scatterTerrainFoliage({
            heightmap: TerrainHeightmap.createFlat(33, 0.5),
            descriptor,
            density: highDensity,
            densityResolution: 64,
            layer: createLayer({ density: 1 }),
            seed: 42,
        });

        expect(instances.length).toBeLessThanOrEqual(TERRAIN_MAX_FOLIAGE_INSTANCES);
    });
});

describe('buildTerrainFoliageBatchMesh', () => {
    it('expands every instance with the built-in card mesh', () => {
        const card = createTerrainFoliageCardMesh();
        const cardVertexCount = card.positions.length / 3;
        const instances = [
            { x: 0, y: 2, z: 0, rotationY: 0, scale: 1 },
            { x: 5, y: 0, z: -3, rotationY: Math.PI / 2, scale: 2 },
        ];

        const mesh = buildTerrainFoliageBatchMesh(instances);
        expect(mesh.vertexCount).toBe(cardVertexCount * 2);
        expect(mesh.triangleCount).toBe((card.indices.length / 3) * 2);
        expect(mesh.positions.length).toBe(mesh.vertexCount * 3);
        expect(mesh.uvs.length).toBe(mesh.vertexCount * 2);

        // Ilk instance: y translasyonu uygulanmis olmali.
        expect(mesh.positions[1]).toBeCloseTo(2, 5);
        // Ikinci instance: 2x olcek karti 2 birim yukseltir.
        const secondBaseY = mesh.positions[(cardVertexCount + 2) * 3 + 1]!;
        expect(secondBaseY).toBeCloseTo(2, 5);
        // Index bloklari instance bazinda kaydirilmis olmali.
        expect(mesh.indices[card.indices.length]).toBe(card.indices[0]! + cardVertexCount);
    });

    it('produces an empty mesh without instances', () => {
        const mesh = buildTerrainFoliageBatchMesh([]);
        expect(mesh.vertexCount).toBe(0);
        expect(mesh.triangleCount).toBe(0);
        expect(mesh.indices).toHaveLength(0);
    });

    it('rejects inconsistent source meshes', () => {
        try {
            buildTerrainFoliageBatchMesh([], {
                positions: new Float32Array(4),
                normals: new Float32Array(4),
                uvs: new Float32Array(2),
                indices: new Uint32Array(3),
            });
            expect.unreachable('expected batch build to throw');
        } catch (error) {
            expect(error).toBeInstanceOf(TerrainError);
            expect((error as TerrainError).code).toBe(TerrainErrorCode.VALIDATION_FAILED);
        }
    });

    it('createTerrainFoliageCardMesh produces 8 vertices and 12 indices', () => {
        const card = createTerrainFoliageCardMesh();
        expect(card.positions.length / 3).toBe(8);
        expect(card.indices.length).toBe(12);
        expect(card.normals.length).toBe(8 * 3);
        expect(card.uvs.length).toBe(8 * 2);
    });

    it('card mesh normals are outward-facing for each quad plane', () => {
        const card = createTerrainFoliageCardMesh();
        const n0 = { x: card.normals[0]!, y: card.normals[1]!, z: card.normals[2]! };
        const n4 = { x: card.normals[12]!, y: card.normals[13]!, z: card.normals[14]! };
        expect(n0.z).toBe(1);
        expect(n4.x).toBe(1);
    });

    it('rotates positions correctly by PI/2 around Y', () => {
        const instances = [{ x: 0, y: 0, z: 0, rotationY: Math.PI / 2, scale: 1 }];
        const mesh = buildTerrainFoliageBatchMesh(instances);

        const card = createTerrainFoliageCardMesh();
        const srcX = card.positions[0]!;
        const srcZ = card.positions[2]!;

        expect(mesh.positions[0]).toBeCloseTo(-srcZ, 5);
        expect(mesh.positions[2]).toBeCloseTo(-srcX, 5);
    });

    it('expands a custom source mesh correctly', () => {
        const source: TerrainFoliageSourceMesh = {
            positions: new Float32Array([0, 0, 0, 1, 0, 0, 1, 1, 0]),
            normals: new Float32Array([0, 0, 1, 0, 0, 1, 0, 0, 1]),
            uvs: new Float32Array([0, 0, 1, 0, 1, 1]),
            indices: new Uint32Array([0, 1, 2]),
        };

        const instances = [
            { x: 10, y: 0, z: 20, rotationY: 0, scale: 1 },
            { x: -5, y: 3, z: 7, rotationY: 0, scale: 2 },
        ];

        const mesh = buildTerrainFoliageBatchMesh(instances, source);
        expect(mesh.vertexCount).toBe(6);
        expect(mesh.triangleCount).toBe(2);
        expect(mesh.positions[0]).toBeCloseTo(10);
        expect(mesh.positions[1]).toBeCloseTo(0);
        expect(mesh.positions[2]).toBeCloseTo(20);
        expect(mesh.positions[3]).toBeCloseTo(11);
        expect(mesh.positions[9]).toBeCloseTo(-5);
        expect(mesh.positions[10]).toBeCloseTo(3);
        expect(mesh.positions[12]).toBeCloseTo(-3);
    });
});
