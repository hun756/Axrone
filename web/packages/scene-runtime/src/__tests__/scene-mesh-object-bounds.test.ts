import { describe, expect, it } from 'vitest';
import { resolveSceneMeshObjectBounds } from '../scene-mesh-bounds';
import type { SceneMeshDefinition } from '../types';

const createPositionMesh = (
    positions: number[],
    overrides: Partial<SceneMeshDefinition> = {}
): SceneMeshDefinition => ({
    id: 'mesh/test',
    vertices: new Float32Array(positions),
    attributes: [
        { semantic: 'position' as const, componentCount: 3 as const, offset: 0, stride: 12 },
    ],
    ...overrides,
});

describe('resolveSceneMeshObjectBounds', () => {
    it('derives an exact object-space AABB from known vertices', () => {
        // x in [1,3], y in [-2,2], z in [0,1] -> center (2,0,0.5), size (2,4,1)
        const mesh = createPositionMesh([
            1, -2, 0,
            3, 2, 1,
            2, 0, 0.5,
        ]);
        const bounds = resolveSceneMeshObjectBounds(mesh);

        expect(bounds).toBeDefined();
        expect(bounds!.center.x).toBeCloseTo(2);
        expect(bounds!.center.y).toBeCloseTo(0);
        expect(bounds!.center.z).toBeCloseTo(0.5);
        expect(bounds!.size.x).toBeCloseTo(2);
        expect(bounds!.size.y).toBeCloseTo(4);
        expect(bounds!.size.z).toBeCloseTo(1);
    });

    it('falls back to a conservative cube when only a bounds sphere exists', () => {
        const mesh: SceneMeshDefinition = {
            id: 'mesh/sphere',
            vertices: new Float32Array(0),
            attributes: [],
            bounds: { kind: 'sphere', center: [4, 5, 6], radius: 2 },
        };
        const bounds = resolveSceneMeshObjectBounds(mesh);

        expect(bounds).toBeDefined();
        expect(bounds!.center.x).toBe(4);
        expect(bounds!.center.y).toBe(5);
        expect(bounds!.center.z).toBe(6);
        // Size is the full edge length (radius * 2), not a half-extent.
        expect(bounds!.size.x).toBe(4);
        expect(bounds!.size.y).toBe(4);
        expect(bounds!.size.z).toBe(4);
    });

    it('accepts a Vec3-like sphere center', () => {
        const mesh: SceneMeshDefinition = {
            id: 'mesh/sphere-object',
            vertices: new Float32Array(0),
            attributes: [],
            bounds: { kind: 'sphere', center: { x: 1, y: 2, z: 3 }, radius: 1 },
        };
        const bounds = resolveSceneMeshObjectBounds(mesh);

        expect(bounds).toBeDefined();
        expect(bounds!.center.x).toBe(1);
        expect(bounds!.center.y).toBe(2);
        expect(bounds!.center.z).toBe(3);
        expect(bounds!.size.x).toBe(2);
    });

    it('returns undefined when there is neither a position attribute nor bounds', () => {
        const mesh: SceneMeshDefinition = {
            id: 'mesh/empty',
            vertices: new Float32Array([1, 0, 0, 0, 1, 0]),
            attributes: [
                { semantic: 'normal' as const, componentCount: 3 as const, offset: 0, stride: 12 },
            ],
        };
        expect(resolveSceneMeshObjectBounds(mesh)).toBeUndefined();
    });

    it('returns undefined for an undefined definition', () => {
        expect(resolveSceneMeshObjectBounds(undefined)).toBeUndefined();
    });

    it('memoises the resolved box by definition identity', () => {
        const mesh = createPositionMesh([
            -1, -1, -1,
            1, 1, 1,
        ]);
        const first = resolveSceneMeshObjectBounds(mesh);
        const second = resolveSceneMeshObjectBounds(mesh);

        expect(first).toBeDefined();
        expect(second).toBe(first);
    });

    it('memoises negative results too', () => {
        const mesh: SceneMeshDefinition = {
            id: 'mesh/no-position',
            vertices: new Float32Array([1, 0, 0]),
            attributes: [],
        };
        expect(resolveSceneMeshObjectBounds(mesh)).toBeUndefined();
        expect(resolveSceneMeshObjectBounds(mesh)).toBeUndefined();
    });
});
