import { describe, expect, it } from 'vitest';
import { cloneSceneMeshBounds, resolveSceneMeshBounds } from '../scene-mesh-bounds';
import type { SceneMeshDefinition } from '../types';

describe('cloneSceneMeshBounds', () => {
    it('returns undefined for undefined input', () => {
        expect(cloneSceneMeshBounds(undefined)).toBeUndefined();
    });

    it('clones bounds with tuple center', () => {
        const bounds = { kind: 'sphere' as const, center: [1, 2, 3] as const, radius: 5 };
        const cloned = cloneSceneMeshBounds(bounds);
        expect(cloned).toBeDefined();
        expect(cloned!.kind).toBe('sphere');
        expect(cloned!.center).toEqual([1, 2, 3]);
        expect(cloned!.radius).toBe(5);
        expect(cloned!.center).not.toBe(bounds.center);
    });

    it('clones bounds with object center', () => {
        const bounds = { kind: 'sphere' as const, center: { x: 1, y: 2, z: 3 }, radius: 5 };
        const cloned = cloneSceneMeshBounds(bounds);
        expect(cloned).toBeDefined();
        expect(cloned!.center).toEqual([1, 2, 3]);
    });
});

const createFloat32Vertices = (positions: number[]): Float32Array => {
    return new Float32Array(positions);
};

const createMeshWithPositions = (
    positions: number[],
    overrides: Partial<SceneMeshDefinition> = {}
): SceneMeshDefinition => ({
    id: 'mesh/test',
    vertices: createFloat32Vertices(positions),
    attributes: [
        { semantic: 'position' as const, componentCount: 3 as const, offset: 0, stride: 12 },
    ],
    ...overrides,
});

describe('resolveSceneMeshBounds', () => {
    it('returns pre-computed bounds when present', () => {
        const mesh: SceneMeshDefinition = {
            ...createMeshWithPositions([0, 0, 0]),
            bounds: { kind: 'sphere', center: [1, 2, 3], radius: 10 },
        };
        const bounds = resolveSceneMeshBounds(mesh);
        expect(bounds).toBeDefined();
        expect(bounds!.kind).toBe('sphere');
        expect(bounds!.center).toEqual([1, 2, 3]);
        expect(bounds!.radius).toBe(10);
    });

    it('computes bounds from a single vertex', () => {
        const mesh = createMeshWithPositions([5, 10, 15]);
        const bounds = resolveSceneMeshBounds(mesh);
        expect(bounds).toBeDefined();
        expect(bounds!.center).toEqual([5, 10, 15]);
        expect(bounds!.radius).toBe(0);
    });

    it('computes bounds from a triangle', () => {
        const mesh = createMeshWithPositions([
            0, 0, 0,
            2, 0, 0,
            0, 2, 0,
        ]);
        const bounds = resolveSceneMeshBounds(mesh);
        expect(bounds).toBeDefined();
        expect(bounds!.center).toEqual([1, 1, 0]);
        expect(bounds!.radius).toBeCloseTo(Math.hypot(1, 1, 0));
    });

    it('computes bounds from a cube', () => {
        const mesh = createMeshWithPositions([
            -1, -1, -1,
            1, -1, -1,
            1, 1, -1,
            -1, 1, -1,
            -1, -1, 1,
            1, -1, 1,
            1, 1, 1,
            -1, 1, 1,
        ]);
        const bounds = resolveSceneMeshBounds(mesh);
        expect(bounds).toBeDefined();
        expect(bounds!.center[0]).toBeCloseTo(0);
        expect(bounds!.center[1]).toBeCloseTo(0);
        expect(bounds!.center[2]).toBeCloseTo(0);
        expect(bounds!.radius).toBeCloseTo(Math.sqrt(3));
    });

    it('returns undefined when no position attribute exists', () => {
        const mesh: SceneMeshDefinition = {
            id: 'mesh/test',
            vertices: new Float32Array([1, 0, 0, 0, 1, 0, 0, 0, 1]),
            attributes: [
                { semantic: 'normal' as const, componentCount: 3 as const, offset: 0, stride: 12 },
            ],
        };
        expect(resolveSceneMeshBounds(mesh)).toBeUndefined();
    });

    it('returns undefined for empty vertex data', () => {
        const mesh = createMeshWithPositions([]);
        expect(resolveSceneMeshBounds(mesh)).toBeUndefined();
    });

    it('handles interleaved vertex data with stride', () => {
        const vertices = new Float32Array([
            1, 2, 3, 0, 0,
            4, 5, 6, 0, 0,
        ]);
        const mesh: SceneMeshDefinition = {
            id: 'mesh/test',
            vertices,
            attributes: [
                { semantic: 'position' as const, componentCount: 3 as const, offset: 0, stride: 20 },
            ],
        };
        const bounds = resolveSceneMeshBounds(mesh);
        expect(bounds).toBeDefined();
        expect(bounds!.center).toEqual([2.5, 3.5, 4.5]);
    });

    it('respects explicit vertexCount', () => {
        const vertices = new Float32Array([
            0, 0, 0,
            10, 10, 10,
            999, 999, 999,
        ]);
        const mesh: SceneMeshDefinition = {
            ...createMeshWithPositions([]),
            vertices,
            vertexCount: 2,
        };
        const bounds = resolveSceneMeshBounds(mesh);
        expect(bounds).toBeDefined();
        expect(bounds!.center).toEqual([5, 5, 5]);
    });

    it('returns undefined when position offset exceeds stride', () => {
        const mesh: SceneMeshDefinition = {
            id: 'mesh/test',
            vertices: new Float32Array([0, 0, 0]),
            attributes: [
                { semantic: 'position' as const, componentCount: 3 as const, offset: 100, stride: 12 },
            ],
        };
        expect(resolveSceneMeshBounds(mesh)).toBeUndefined();
    });
});
