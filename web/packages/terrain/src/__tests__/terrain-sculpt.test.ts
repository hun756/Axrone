import { describe, expect, it } from 'vitest';
import {
    TerrainError,
    TerrainErrorCode,
    TerrainHeightmap,
    applyTerrainBrushStamp,
    decodeTerrainHeights,
    encodeTerrainHeights,
    generateNoiseHeightmap,
    isTerrainHeightDataPayload,
    raycastTerrainHeightmap,
    resolveTerrainBrushOptions,
    type TerrainDescriptor,
} from '../index';

const createDescriptor = (overrides: Partial<TerrainDescriptor> = {}): TerrainDescriptor => ({
    width: 64,
    length: 64,
    maxHeight: 20,
    resolution: 33,
    ...overrides,
});

describe('terrain brush options', () => {
    it('fills defaults and freezes the resolved options', () => {
        const resolved = resolveTerrainBrushOptions({ kind: 'smooth' });

        expect(resolved.kind).toBe('smooth');
        expect(resolved.radius).toBeGreaterThan(0);
        expect(Object.isFrozen(resolved)).toBe(true);
    });

    it('rejects out-of-range parameters with VALIDATION_FAILED', () => {
        for (const options of [
            { radius: 0 },
            { strength: 0 },
            { strength: 1.5 },
            { falloff: 0 },
            { flattenTarget: 2 },
            { kind: 'erode' as never },
        ]) {
            try {
                resolveTerrainBrushOptions(options);
                expect.unreachable('expected brush validation to throw');
            } catch (error) {
                expect(error).toBeInstanceOf(TerrainError);
                expect((error as TerrainError).code).toBe(TerrainErrorCode.VALIDATION_FAILED);
            }
        }
    });

    it('accepts boundary values: strength 1, falloff 8, flattenTarget 0 and 1', () => {
        expect(() => resolveTerrainBrushOptions({ strength: 1 })).not.toThrow();
        expect(() => resolveTerrainBrushOptions({ falloff: 8 })).not.toThrow();
        expect(() => resolveTerrainBrushOptions({ flattenTarget: 0 })).not.toThrow();
        expect(() => resolveTerrainBrushOptions({ flattenTarget: 1 })).not.toThrow();
    });
});

describe('applyTerrainBrushStamp', () => {
    it('raises heights inside the radius with edge falloff', () => {
        const descriptor = createDescriptor();
        const heights = new Float32Array(33 * 33);
        const brush = resolveTerrainBrushOptions({ kind: 'raise', radius: 12, strength: 1 });

        const changed = applyTerrainBrushStamp({
            heights,
            descriptor,
            brush,
            localX: 0,
            localZ: 0,
        });

        const center = heights[16 * 33 + 16]!;
        const edge = heights[16 * 33 + 20]!;
        const outside = heights[0]!;

        expect(changed).toBe(true);
        expect(center).toBeGreaterThan(0);
        expect(edge).toBeGreaterThan(0);
        expect(edge).toBeLessThan(center);
        expect(outside).toBe(0);
    });

    it('lower mirrors raise and clamps at zero', () => {
        const descriptor = createDescriptor();
        const heights = new Float32Array(33 * 33).fill(0.5);
        const brush = resolveTerrainBrushOptions({ kind: 'lower', radius: 12, strength: 1 });

        applyTerrainBrushStamp({ heights, descriptor, brush, localX: 0, localZ: 0 });
        expect(heights[16 * 33 + 16]!).toBeLessThan(0.5);

        const floor = new Float32Array(33 * 33);
        const changed = applyTerrainBrushStamp({
            heights: floor,
            descriptor,
            brush,
            localX: 0,
            localZ: 0,
        });
        expect(changed).toBe(false);
        expect(floor.every((value) => value === 0)).toBe(true);
    });

    it('smooth converges spikes toward the neighborhood mean', () => {
        const descriptor = createDescriptor();
        const heights = new Float32Array(33 * 33);
        heights[16 * 33 + 16] = 1;
        const brush = resolveTerrainBrushOptions({ kind: 'smooth', radius: 8, strength: 1 });

        applyTerrainBrushStamp({ heights, descriptor, brush, localX: 0, localZ: 0 });

        expect(heights[16 * 33 + 16]!).toBeLessThan(1);
        expect(heights[16 * 33 + 15]!).toBeGreaterThan(0);
    });

    it('flatten pulls the neighborhood toward the sampled center height', () => {
        const descriptor = createDescriptor();
        const heights = new Float32Array(33 * 33);
        for (let index = 0; index < heights.length; index += 1) {
            heights[index] = (index % 33) / 32;
        }

        const centerBefore = heights[16 * 33 + 16]!;
        const brush = resolveTerrainBrushOptions({ kind: 'flatten', radius: 16, strength: 1 });
        applyTerrainBrushStamp({ heights, descriptor, brush, localX: 0, localZ: 0 });

        const left = heights[16 * 33 + 12]!;
        const right = heights[16 * 33 + 20]!;
        expect(Math.abs(left - centerBefore)).toBeLessThan(Math.abs(12 / 32 - centerBefore));
        expect(Math.abs(right - centerBefore)).toBeLessThan(Math.abs(20 / 32 - centerBefore));
    });

    it('rejects mismatched working buffers with HEIGHTMAP_SIZE_MISMATCH', () => {
        try {
            applyTerrainBrushStamp({
                heights: new Float32Array(10),
                descriptor: createDescriptor(),
                brush: resolveTerrainBrushOptions(),
                localX: 0,
                localZ: 0,
            });
            expect.unreachable('expected stamp to throw');
        } catch (error) {
            expect(error).toBeInstanceOf(TerrainError);
            expect((error as TerrainError).code).toBe(TerrainErrorCode.HEIGHTMAP_SIZE_MISMATCH);
        }
    });

    it('returns false when stamp is centered outside the terrain footprint', () => {
        const descriptor = createDescriptor();
        const heights = new Float32Array(33 * 33);
        const brush = resolveTerrainBrushOptions({ kind: 'raise', radius: 5, strength: 1 });

        const changed = applyTerrainBrushStamp({
            heights,
            descriptor,
            brush,
            localX: 200,
            localZ: 200,
        });

        expect(changed).toBe(false);
        expect(heights.every((v) => v === 0)).toBe(true);
    });

    it('smooth uses pre-stamp snapshot for neighbor averaging', () => {
        const descriptor = createDescriptor();
        const heights = new Float32Array(33 * 33);
        heights[16 * 33 + 16] = 1;
        const brush = resolveTerrainBrushOptions({ kind: 'smooth', radius: 4, strength: 1 });

        applyTerrainBrushStamp({ heights, descriptor, brush, localX: 0, localZ: 0 });

        expect(heights[16 * 33 + 16]!).toBeLessThan(1);
        expect(heights[16 * 33 + 16]!).toBeGreaterThan(0);
    });

    it('raise clamps at 1.0 and returns false on saturated heights', () => {
        const descriptor = createDescriptor();
        const heights = new Float32Array(33 * 33).fill(1);
        const brush = resolveTerrainBrushOptions({ kind: 'raise', radius: 10, strength: 1 });

        const changed = applyTerrainBrushStamp({
            heights,
            descriptor,
            brush,
            localX: 0,
            localZ: 0,
        });

        expect(changed).toBe(false);
        expect(heights.every((v) => v === 1)).toBe(true);
    });
});

describe('raycastTerrainHeightmap', () => {
    it('hits a flat terrain at the expected local point', () => {
        const descriptor = createDescriptor({ maxHeight: 10 });
        const heightmap = TerrainHeightmap.createFlat(33, 0.5);

        const hit = raycastTerrainHeightmap(heightmap, descriptor, {
            origin: { x: 4, y: 50, z: -6 },
            direction: { x: 0, y: -1, z: 0 },
        });

        expect(hit).not.toBeNull();
        expect(hit!.point.x).toBeCloseTo(4, 1);
        expect(hit!.point.z).toBeCloseTo(-6, 1);
        expect(hit!.point.y).toBeCloseTo(5, 1);
        expect(hit!.u).toBeCloseTo((4 + 32) / 64, 2);
        expect(hit!.v).toBeCloseTo((-6 + 32) / 64, 2);
    });

    it('hits noise terrain from an oblique angle and stays inside the footprint', () => {
        const descriptor = createDescriptor({ resolution: 65 });
        const heightmap = generateNoiseHeightmap(descriptor, { seed: 21 });

        const hit = raycastTerrainHeightmap(heightmap, descriptor, {
            origin: { x: -80, y: 60, z: -80 },
            direction: { x: 1, y: -0.6, z: 1 },
        });

        expect(hit).not.toBeNull();
        expect(Math.abs(hit!.point.x)).toBeLessThanOrEqual(32);
        expect(Math.abs(hit!.point.z)).toBeLessThanOrEqual(32);

        const sampled = heightmap.sampleHeight(hit!.u, hit!.v) * descriptor.maxHeight;
        expect(hit!.point.y).toBeCloseTo(sampled, 1);
    });

    it('misses rays that never cross the terrain footprint', () => {
        const descriptor = createDescriptor();
        const heightmap = TerrainHeightmap.createFlat(33, 0.2);

        const hit = raycastTerrainHeightmap(
            heightmap,
            descriptor,
            {
                origin: { x: 500, y: 10, z: 500 },
                direction: { x: 1, y: 0, z: 0 },
            },
            2000
        );

        expect(hit).toBeNull();
    });

    it('returns null for a zero-length direction vector', () => {
        const descriptor = createDescriptor();
        const heightmap = TerrainHeightmap.createFlat(33, 0.5);

        const hit = raycastTerrainHeightmap(heightmap, descriptor, {
            origin: { x: 0, y: 50, z: 0 },
            direction: { x: 0, y: 0, z: 0 },
        });

        expect(hit).toBeNull();
    });

    it('misses when ray starts below the terrain going upward', () => {
        const descriptor = createDescriptor({ maxHeight: 10 });
        const heightmap = TerrainHeightmap.createFlat(33, 0.5);

        const hit = raycastTerrainHeightmap(heightmap, descriptor, {
            origin: { x: 0, y: -1, z: 0 },
            direction: { x: 0, y: 1, z: 0 },
        });

        expect(hit).toBeNull();
    });

    it('misses when ray is horizontal and above the terrain', () => {
        const descriptor = createDescriptor({ maxHeight: 10 });
        const heightmap = TerrainHeightmap.createFlat(33, 0.2);

        const hit = raycastTerrainHeightmap(heightmap, descriptor, {
            origin: { x: -50, y: 50, z: 0 },
            direction: { x: 1, y: 0, z: 0 },
        });

        expect(hit).toBeNull();
    });

    it('misses when the hit is beyond maxDistance', () => {
        const descriptor = createDescriptor({ maxHeight: 10 });
        const heightmap = TerrainHeightmap.createFlat(33, 0.5);

        const farHit = raycastTerrainHeightmap(heightmap, descriptor, {
            origin: { x: 0, y: 50, z: 0 },
            direction: { x: 0, y: -1, z: 0 },
        });
        expect(farHit).not.toBeNull();

        const closeHit = raycastTerrainHeightmap(
            heightmap,
            descriptor,
            {
                origin: { x: 0, y: 50, z: 0 },
                direction: { x: 0, y: -1, z: 0 },
            },
            1
        );
        expect(closeHit).toBeNull();
    });

    it('hits at the footprint edge', () => {
        const descriptor = createDescriptor({ width: 64, length: 64, maxHeight: 10 });
        const heightmap = TerrainHeightmap.createFlat(33, 0.5);

        const hit = raycastTerrainHeightmap(heightmap, descriptor, {
            origin: { x: 32, y: 50, z: 0 },
            direction: { x: 0, y: -1, z: 0 },
        });

        expect(hit).not.toBeNull();
        expect(hit!.point.x).toBeCloseTo(32, 0);
        expect(hit!.point.y).toBeCloseTo(5, 1);
    });
});

describe('terrain height codec', () => {
    it('round-trips heights within quantization tolerance', () => {
        const descriptor = createDescriptor({ resolution: 33 });
        const heightmap = generateNoiseHeightmap(descriptor, { seed: 7 });

        const payload = encodeTerrainHeights(heightmap.heights);
        expect(isTerrainHeightDataPayload(payload)).toBe(true);

        const decoded = decodeTerrainHeights(payload, 33);
        expect(decoded.length).toBe(33 * 33);
        for (let index = 0; index < decoded.length; index += 1) {
            expect(Math.abs(decoded[index]! - heightmap.heights[index]!)).toBeLessThan(1e-4);
        }
    });

    it('rejects payloads without the version prefix', () => {
        try {
            decodeTerrainHeights('bogus-payload', 33);
            expect.unreachable('expected decode to throw');
        } catch (error) {
            expect(error).toBeInstanceOf(TerrainError);
            expect((error as TerrainError).code).toBe(TerrainErrorCode.SOURCE_DECODE_FAILED);
        }
    });

    it('rejects payloads whose length does not match the resolution', () => {
        const payload = encodeTerrainHeights(new Float32Array(33 * 33));
        try {
            decodeTerrainHeights(payload, 65);
            expect.unreachable('expected decode to throw');
        } catch (error) {
            expect(error).toBeInstanceOf(TerrainError);
            expect((error as TerrainError).code).toBe(TerrainErrorCode.HEIGHTMAP_SIZE_MISMATCH);
        }
    });

    it('clamps out-of-range values during encoding', () => {
        const heights = new Float32Array(33 * 33);
        heights[0] = -0.5;
        heights[1] = 1.5;
        heights[2] = 0.5;

        const payload = encodeTerrainHeights(heights);
        const decoded = decodeTerrainHeights(payload, 33);

        expect(decoded[0]).toBeCloseTo(0, 4);
        expect(decoded[1]).toBeCloseTo(1, 4);
        expect(decoded[2]).toBeCloseTo(0.5, 4);
    });

    it('isTerrainHeightDataPayload returns false for non-strings', () => {
        expect(isTerrainHeightDataPayload(42)).toBe(false);
        expect(isTerrainHeightDataPayload(null)).toBe(false);
        expect(isTerrainHeightDataPayload(undefined)).toBe(false);
        expect(isTerrainHeightDataPayload({})).toBe(false);
    });

    it('decodeTerrainHeights rejects invalid resolution', () => {
        const payload = encodeTerrainHeights(new Float32Array(33 * 33));
        try {
            decodeTerrainHeights(payload, 100 as never);
            expect.unreachable('expected decode to throw');
        } catch (error) {
            expect(error).toBeInstanceOf(TerrainError);
            expect((error as TerrainError).code).toBe(TerrainErrorCode.INVALID_RESOLUTION);
        }
    });

    it('decodeTerrainHeights throws SOURCE_DECODE_FAILED on invalid base64', () => {
        try {
            decodeTerrainHeights('athf1:!!!invalid-base64!!!', 33);
            expect.unreachable('expected decode to throw');
        } catch (error) {
            expect(error).toBeInstanceOf(TerrainError);
            expect((error as TerrainError).code).toBe(TerrainErrorCode.SOURCE_DECODE_FAILED);
        }
    });
});
