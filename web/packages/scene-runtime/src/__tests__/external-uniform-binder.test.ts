import { Vec2, Vec3 } from '@axrone/numeric';
import { describe, expect, it, vi } from 'vitest';
import { MATERIAL_EXTERNAL_UNIFORMS_V2 } from '@axrone/render-core/material-contract-v2';
import {
    SCENE_EXTERNAL_FRAME_UNIFORM_NAMES,
    SCENE_OBJECT_BOUNDS_UNIFORM_NAMES,
    SceneExternalUniformBinder,
} from '../external-uniform-binder';
import type { SceneLightingState } from '../lighting-collector';
import type { SceneShaderResource } from '../shader-registry';
import type { SceneUniformWriteTarget } from '../uniform-writer';

const createMockWriteTarget = (): SceneUniformWriteTarget & { writes: Map<string, unknown> } => {
    const writes = new Map<string, unknown>();
    return {
        writes,
        write: vi.fn((_shader: SceneShaderResource, name: string, value: unknown) => {
            writes.set(name, value);
        }),
    };
};

// The real SceneUniformWriter drops names the program does not declare; these
// shaders have no matching locations at all, so a binder that assumed a
// location existed would be caught by the writer-level tests instead. Here we
// only assert the binder never throws and still records its intended writes.
const createShader = (): SceneShaderResource =>
    ({
        id: 'test-shader',
        program: {} as WebGLProgram,
        uniformLocations: new Map(),
        uniformTypes: new Map(),
        uniformNames: [],
    }) as unknown as SceneShaderResource;

const createLighting = (overrides: {
    selectedDirectionalCount: number;
    directionalDirections?: Float32Array;
    directionalColors?: Float32Array;
    directionalIntensities?: Float32Array;
}): SceneLightingState =>
    ({
        stats: { selectedDirectionalCount: overrides.selectedDirectionalCount },
        directionalDirections: overrides.directionalDirections ?? new Float32Array(3),
        directionalColors: overrides.directionalColors ?? new Float32Array(3),
        directionalIntensities: overrides.directionalIntensities ?? new Float32Array(1),
    }) as unknown as SceneLightingState;

describe('SceneExternalUniformBinder frame uniforms', () => {
    it('writes the NEGATED, normalised element 0 of directionalDirections', () => {
        const target = createMockWriteTarget();
        const binder = new SceneExternalUniformBinder(target);
        // Light forward (travel) direction points down: (0, 1, 0).
        const lighting = createLighting({
            selectedDirectionalCount: 1,
            directionalDirections: new Float32Array([0, 1, 0]),
            directionalColors: new Float32Array([1, 1, 1]),
            directionalIntensities: new Float32Array([1]),
        });

        binder.applyFrame(createShader(), 800, 600, lighting);

        const direction = target.writes.get('_MainLightDirection') as Vec3;
        expect(direction.x).toBeCloseTo(0);
        expect(direction.y).toBeCloseTo(-1);
        expect(direction.z).toBeCloseTo(0);
    });

    it('negates and normalises an oblique light direction', () => {
        const target = createMockWriteTarget();
        const binder = new SceneExternalUniformBinder(target);
        // Forward (2, 0, 0): toward-light must be (-1, 0, 0) after normalise.
        const lighting = createLighting({
            selectedDirectionalCount: 1,
            directionalDirections: new Float32Array([2, 0, 0]),
        });

        binder.applyFrame(createShader(), 100, 100, lighting);

        const direction = target.writes.get('_MainLightDirection') as Vec3;
        expect(direction.x).toBeCloseTo(-1);
        expect(direction.y).toBeCloseTo(0);
        expect(direction.z).toBeCloseTo(0);
    });

    it('writes colour premultiplied by intensity, clamped to >= 0', () => {
        const target = createMockWriteTarget();
        const binder = new SceneExternalUniformBinder(target);
        const lighting = createLighting({
            selectedDirectionalCount: 1,
            directionalDirections: new Float32Array([0, 1, 0]),
            directionalColors: new Float32Array([0.5, 0.25, 1]),
            directionalIntensities: new Float32Array([2]),
        });

        binder.applyFrame(createShader(), 800, 600, lighting);

        const color = target.writes.get('_MainLightColor') as Vec3;
        expect(color.x).toBeCloseTo(1);
        expect(color.y).toBeCloseTo(0.5);
        expect(color.z).toBeCloseTo(2);
    });

    it('clamps a negative intensity contribution to zero', () => {
        const target = createMockWriteTarget();
        const binder = new SceneExternalUniformBinder(target);
        const lighting = createLighting({
            selectedDirectionalCount: 1,
            directionalColors: new Float32Array([0.5, 0.25, 1]),
            directionalIntensities: new Float32Array([-3]),
        });

        binder.applyFrame(createShader(), 800, 600, lighting);

        const color = target.writes.get('_MainLightColor') as Vec3;
        expect(color.x).toBe(0);
        expect(color.y).toBe(0);
        expect(color.z).toBe(0);
    });

    it('falls back to (0,1,0)/(1,1,1) when there is no selected directional light', () => {
        const target = createMockWriteTarget();
        const binder = new SceneExternalUniformBinder(target);
        // Arrays deliberately hold garbage that must NOT leak through.
        const lighting = createLighting({
            selectedDirectionalCount: 0,
            directionalDirections: new Float32Array([9, 9, 9]),
            directionalColors: new Float32Array([9, 9, 9]),
            directionalIntensities: new Float32Array([9]),
        });

        binder.applyFrame(createShader(), 800, 600, lighting);

        const direction = target.writes.get('_MainLightDirection') as Vec3;
        const color = target.writes.get('_MainLightColor') as Vec3;
        expect(direction.x).toBe(0);
        expect(direction.y).toBe(1);
        expect(direction.z).toBe(0);
        expect(color.x).toBe(1);
        expect(color.y).toBe(1);
        expect(color.z).toBe(1);
    });

    it('always writes both light uniforms even on the fallback path', () => {
        const target = createMockWriteTarget();
        const binder = new SceneExternalUniformBinder(target);
        const lighting = createLighting({ selectedDirectionalCount: 0 });

        binder.applyFrame(createShader(), 800, 600, lighting);

        expect(target.writes.has('_MainLightDirection')).toBe(true);
        expect(target.writes.has('_MainLightColor')).toBe(true);
    });

    it('passes width and height verbatim to _RenderTargetSize', () => {
        const target = createMockWriteTarget();
        const binder = new SceneExternalUniformBinder(target);
        const lighting = createLighting({ selectedDirectionalCount: 0 });

        binder.applyFrame(createShader(), 1280, 720, lighting);

        const size = target.writes.get('_RenderTargetSize') as Vec2;
        expect(size.x).toBe(1280);
        expect(size.y).toBe(720);
    });

    it('does NOT write _RenderTargetSize when a dimension is zero', () => {
        const target = createMockWriteTarget();
        const binder = new SceneExternalUniformBinder(target);
        const lighting = createLighting({ selectedDirectionalCount: 0 });

        binder.applyFrame(createShader(), 0, 600, lighting);

        expect(target.writes.has('_RenderTargetSize')).toBe(false);
        // The light uniforms are still written (never skipped).
        expect(target.writes.has('_MainLightDirection')).toBe(true);
    });

    it('reuses the same pooled vector objects across calls (zero allocation)', () => {
        const target = createMockWriteTarget();
        const binder = new SceneExternalUniformBinder(target);
        const shader = createShader();

        binder.applyFrame(shader, 800, 600, createLighting({ selectedDirectionalCount: 0 }));
        const firstSize = target.writes.get('_RenderTargetSize');
        const firstDir = target.writes.get('_MainLightDirection');
        const firstColor = target.writes.get('_MainLightColor');

        target.writes.clear();
        binder.applyFrame(shader, 1024, 768, createLighting({ selectedDirectionalCount: 0 }));

        expect(target.writes.get('_RenderTargetSize')).toBe(firstSize);
        expect(target.writes.get('_MainLightDirection')).toBe(firstDir);
        expect(target.writes.get('_MainLightColor')).toBe(firstColor);
    });

    it('survives a shader with no matching uniform locations', () => {
        const target = createMockWriteTarget();
        const binder = new SceneExternalUniformBinder(target);
        const lighting = createLighting({
            selectedDirectionalCount: 1,
            directionalDirections: new Float32Array([0, 1, 0]),
        });

        expect(() => binder.applyFrame(createShader(), 800, 600, lighting)).not.toThrow();
    });
});

describe('SceneExternalUniformBinder object bounds', () => {
    it('writes center and size straight through without mutating them', () => {
        const target = createMockWriteTarget();
        const binder = new SceneExternalUniformBinder(target);
        const center = new Vec3(2, 0, 0.5);
        const size = new Vec3(2, 4, 1);

        binder.applyObjectBounds(createShader(), { center, size });

        expect(target.writes.get('_ObjectBoundsCenter')).toBe(center);
        expect(target.writes.get('_ObjectBoundsSize')).toBe(size);
    });

    it('returns immediately for undefined bounds', () => {
        const target = createMockWriteTarget();
        const binder = new SceneExternalUniformBinder(target);

        binder.applyObjectBounds(createShader(), undefined);

        expect(target.write).not.toHaveBeenCalled();
    });
});

describe('external uniform name constants', () => {
    it('declares the five engine-supplied uniform names', () => {
        expect([...SCENE_EXTERNAL_FRAME_UNIFORM_NAMES]).toEqual([
            '_RenderTargetSize',
            '_MainLightDirection',
            '_MainLightColor',
        ]);
        expect([...SCENE_OBJECT_BOUNDS_UNIFORM_NAMES]).toEqual([
            '_ObjectBoundsCenter',
            '_ObjectBoundsSize',
        ]);
    });
});

// ---------------------------------------------------------------------------
// Cross-package drift guard.
//
// scene-runtime deliberately keeps the five uniform names as local string
// literals (it must not import render-core's contract module into the draw
// path), so nothing at build time ties those literals to the contract registry.
// These assertions are that tie: renaming a constant on EITHER side, changing a
// declared type/scope, or renaming the binder class/methods the registry names
// as its provider turns this suite red.
// ---------------------------------------------------------------------------

const getRegistryEntry = (name: string) => {
    const entry = MATERIAL_EXTERNAL_UNIFORMS_V2.find((uniform) => uniform.name === name);
    if (!entry) {
        throw new Error(`render-core registry has no external uniform named ${name}`);
    }
    return entry;
};

describe('external uniform contract drift guard against render-core', () => {
    it('keeps the registry names exactly equal to the binder name constants', () => {
        const registryNames = [...MATERIAL_EXTERNAL_UNIFORMS_V2.map((uniform) => uniform.name)].sort();
        const binderNames = [
            ...SCENE_EXTERNAL_FRAME_UNIFORM_NAMES,
            ...SCENE_OBJECT_BOUNDS_UNIFORM_NAMES,
        ].sort();

        expect(registryNames).toEqual(binderNames);
        expect(registryNames).toEqual([
            '_MainLightColor',
            '_MainLightDirection',
            '_ObjectBoundsCenter',
            '_ObjectBoundsSize',
            '_RenderTargetSize',
        ]);
    });

    it('declares vec2 for _RenderTargetSize and vec3 for the other four', () => {
        expect(getRegistryEntry('_RenderTargetSize').valueType).toBe('vec2');
        expect(getRegistryEntry('_MainLightDirection').valueType).toBe('vec3');
        expect(getRegistryEntry('_MainLightColor').valueType).toBe('vec3');
        expect(getRegistryEntry('_ObjectBoundsCenter').valueType).toBe('vec3');
        expect(getRegistryEntry('_ObjectBoundsSize').valueType).toBe('vec3');
    });

    it('scopes the three frame names to frame and the two bounds names to object', () => {
        for (const name of SCENE_EXTERNAL_FRAME_UNIFORM_NAMES) {
            expect(getRegistryEntry(name).scope, `scope of ${name}`).toBe('frame');
        }
        for (const name of SCENE_OBJECT_BOUNDS_UNIFORM_NAMES) {
            expect(getRegistryEntry(name).scope, `scope of ${name}`).toBe('object');
        }
    });

    it('names SceneExternalUniformBinder methods that still exist on the class', () => {
        const prototype = SceneExternalUniformBinder.prototype as unknown as Record<string, unknown>;

        for (const uniform of MATERIAL_EXTERNAL_UNIFORMS_V2) {
            expect(uniform.provider, `provider of ${uniform.name}`).toContain(
                'SceneExternalUniformBinder'
            );

            const methodName = uniform.provider.split('.').pop()!;
            expect(['applyFrame', 'applyObjectBounds'], `provider method ${methodName}`).toContain(
                methodName
            );
            expect(
                typeof prototype[methodName],
                `SceneExternalUniformBinder#${methodName} must exist`
            ).toBe('function');
        }
    });

    it('writes exactly the registry default vectors when the frame has no light', () => {
        const target = createMockWriteTarget();
        const binder = new SceneExternalUniformBinder(target);

        binder.applyFrame(createShader(), 800, 600, createLighting({ selectedDirectionalCount: 0 }));

        const direction = target.writes.get('_MainLightDirection') as Vec3;
        const directionDefault = getRegistryEntry('_MainLightDirection').defaultValue;
        expect(directionDefault).toHaveLength(3);
        expect([direction.x, direction.y, direction.z]).toEqual([...directionDefault]);

        const color = target.writes.get('_MainLightColor') as Vec3;
        const colorDefault = getRegistryEntry('_MainLightColor').defaultValue;
        expect(colorDefault).toHaveLength(3);
        expect([color.x, color.y, color.z]).toEqual([...colorDefault]);
    });

    it('keeps the object-bounds defaults reachable from the registry', () => {
        // The binder itself never writes a literal default for bounds — it skips
        // the write and the GL program keeps whatever the material author set.
        // Pinning the registry values here means a shader that reads the uniform
        // before the first bounded draw sees the documented fallback.
        expect([...getRegistryEntry('_ObjectBoundsCenter').defaultValue]).toEqual([0, 0, 0]);
        expect([...getRegistryEntry('_ObjectBoundsSize').defaultValue]).toEqual([1, 1, 1]);
    });

    it('feeds the registry-declared render target size components to the writer', () => {
        const target = createMockWriteTarget();
        const binder = new SceneExternalUniformBinder(target);

        binder.applyFrame(createShader(), 512, 256, createLighting({ selectedDirectionalCount: 0 }));

        const size = target.writes.get('_RenderTargetSize') as Vec2;
        const entry = getRegistryEntry('_RenderTargetSize');
        expect(entry.valueType).toBe('vec2');
        expect(entry.defaultValue).toHaveLength(2);
        expect([size.x, size.y]).toEqual([512, 256]);
    });
});
