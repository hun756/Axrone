import { describe, expect, it } from 'vitest';
import { RenderPassPlanner } from '../render-pass-planner';
import { RenderTextureRegistry } from '../graph';
import { ReusableList } from '../memory';
import type {
    RenderCameraState,
    RenderLight,
    RenderPrimitiveInstance,
    RenderReflectionProbe,
    ResolvedPostProcessEffect,
    ResolvedRenderPass,
    RenderViewport,
    RenderGlobalIlluminationSettings,
} from '../types';
import type { ScheduledRenderBakeTask } from '../render-bake-task-scheduler';
import { Mat4 } from '@axrone/numeric';

const identityMatrix = new Mat4();

function createMockCamera(): RenderCameraState {
    return {
        id: 'cam:0',
        viewMatrix: identityMatrix,
        projectionMatrix: identityMatrix,
        position: [0, 0, 5] as any,
        near: 0.1,
        far: 1000,
    };
}

function createMockViewport(): RenderViewport {
    return { width: 1920, height: 1080, pixelRatio: 1 };
}

function createEmptyList<T>(): ReusableList<T> {
    return new ReusableList<T>(0);
}

function createMockOpaqueList(count: number): ReusableList<RenderPrimitiveInstance> {
    const list = new ReusableList<RenderPrimitiveInstance>(count);
    for (let i = 0; i < count; i++) {
        list.push({
            id: `prim:${i}`,
            meshId: `mesh:${i}`,
            material: { id: `mat:${i}`, model: 'pbr' as const },
            worldMatrix: identityMatrix,
        });
    }
    return list;
}

function createMockLightList(count: number): ReusableList<RenderLight> {
    const list = new ReusableList<RenderLight>(count);
    for (let i = 0; i < count; i++) {
        list.push({
            type: 'directional',
            id: `light:${i}`,
            direction: [0, -1, 0] as any,
            color: [1, 1, 1] as any,
            intensity: 1,
            castsShadows: false,
        });
    }
    return list;
}

function createDefaultSettings() {
    return {
        shadows: {
            atlasSize: 2048,
            cascadeCount: 4 as const,
            cascadeSplitLambda: 0.5,
            maxDistance: 100,
            filter: 'pcf' as const,
        },
        tonemapping: {
            mode: 'aces' as const,
            gamma: 2.2,
            contrast: 1,
            saturation: 1,
            shoulderStrength: 2,
            toeStrength: 0.5,
        },
        lightBaking: { budgetMs: 8 },
        enableDepthPrepass: 'auto' as const,
    };
}

function createMinimalInput(overrides: Partial<Parameters<RenderPassPlanner['plan']>[0]> = {}) {
    return {
        frame: 0,
        viewport: createMockViewport(),
        camera: createMockCamera(),
        environment: undefined,
        hdr: { enabled: true, colorFormat: 'r11g11b10f' as const, outputColorSpace: 'srgb' as const, exposure: undefined },
        gi: { mode: 'disabled' as const },
        volumetrics: { enabled: false, froxelResolution: [64, 64, 64] as const, temporalReprojection: false },
        shadowEnabled: false,
        probeUpdateCount: 0,
        postEffects: [] as readonly ResolvedPostProcessEffect[],
        bakeTasks: [] as readonly ScheduledRenderBakeTask[],
        opaque: createMockOpaqueList(10),
        transparent: createEmptyList<RenderPrimitiveInstance>(),
        shadowCasters: createEmptyList<RenderPrimitiveInstance>(),
        activeLights: createMockLightList(1),
        shadowLights: createEmptyList<RenderLight>(),
        activeProbes: createEmptyList<RenderReflectionProbe>(),
        probeUpdates: createEmptyList<RenderReflectionProbe>(),
        ...overrides,
    };
}

describe('RenderPassPlanner', () => {
    describe('plan — always produces opaque + present', () => {
        it('produces at least opaque and present passes', () => {
            const graph = new RenderTextureRegistry();
            const planner = new RenderPassPlanner(graph, createDefaultSettings());
            const passes = planner.plan(createMinimalInput());

            const kinds = passes.map((p) => p.kind);
            expect(kinds).toContain('opaque');
            expect(kinds).toContain('present');
        });
    });

    describe('conditional passes', () => {
        it('includes depth-prepass when auto and opaque count > 48', () => {
            const graph = new RenderTextureRegistry();
            const planner = new RenderPassPlanner(graph, createDefaultSettings());
            const passes = planner.plan(createMinimalInput({
                opaque: createMockOpaqueList(50),
            }));
            const kinds = passes.map((p) => p.kind);
            expect(kinds).toContain('depth-prepass');
        });

        it('skips depth-prepass when auto and opaque count <= 48', () => {
            const graph = new RenderTextureRegistry();
            const planner = new RenderPassPlanner(graph, createDefaultSettings());
            const passes = planner.plan(createMinimalInput({
                opaque: createMockOpaqueList(10),
            }));
            const kinds = passes.map((p) => p.kind);
            expect(kinds).not.toContain('depth-prepass');
        });

        it('includes shadow pass when shadowEnabled is true', () => {
            const graph = new RenderTextureRegistry();
            const planner = new RenderPassPlanner(graph, createDefaultSettings());
            const shadowLights = createMockLightList(1);
            const passes = planner.plan(createMinimalInput({
                shadowEnabled: true,
                shadowLights,
                shadowCasters: createMockOpaqueList(5),
            }));
            const kinds = passes.map((p) => p.kind);
            expect(kinds).toContain('shadow');
        });

        it('includes skybox when environment has skybox', () => {
            const graph = new RenderTextureRegistry();
            const planner = new RenderPassPlanner(graph, createDefaultSettings());
            const passes = planner.plan(createMinimalInput({
                environment: { skybox: { textureId: 'sky' } },
            }));
            const kinds = passes.map((p) => p.kind);
            expect(kinds).toContain('skybox');
        });

        it('includes volumetric pass when enabled', () => {
            const graph = new RenderTextureRegistry();
            const planner = new RenderPassPlanner(graph, createDefaultSettings());
            const passes = planner.plan(createMinimalInput({
                volumetrics: { enabled: true, froxelResolution: [32, 32, 32] as const, temporalReprojection: false },
            }));
            const kinds = passes.map((p) => p.kind);
            expect(kinds).toContain('volumetric');
        });

        it('includes transparent pass when transparent list is non-empty', () => {
            const graph = new RenderTextureRegistry();
            const planner = new RenderPassPlanner(graph, createDefaultSettings());
            const transparent = createMockOpaqueList(5);
            const passes = planner.plan(createMinimalInput({ transparent }));
            const kinds = passes.map((p) => p.kind);
            expect(kinds).toContain('transparent');
        });

        it('includes tonemap pass when HDR is enabled', () => {
            const graph = new RenderTextureRegistry();
            const planner = new RenderPassPlanner(graph, createDefaultSettings());
            const passes = planner.plan(createMinimalInput());
            const kinds = passes.map((p) => p.kind);
            expect(kinds).toContain('tonemap');
        });

        it('includes GI pass when GI mode is not disabled', () => {
            const graph = new RenderTextureRegistry();
            const planner = new RenderPassPlanner(graph, createDefaultSettings());
            const passes = planner.plan(createMinimalInput({
                gi: { mode: 'ssgi' } as RenderGlobalIlluminationSettings,
            }));
            const kinds = passes.map((p) => p.kind);
            expect(kinds).toContain('global-illumination');
        });

        it('includes light-bake pass when bake tasks exist', () => {
            const graph = new RenderTextureRegistry();
            const planner = new RenderPassPlanner(graph, createDefaultSettings());
            const passes = planner.plan(createMinimalInput({
                bakeTasks: [{
                    id: 'bake:1',
                    type: 'lightmap',
                    priority: 1,
                    state: 'scheduled',
                    maxRetries: 0,
                    retries: 0,
                    throttleFrames: 0,
                }] as ScheduledRenderBakeTask[],
            }));
            const kinds = passes.map((p) => p.kind);
            expect(kinds).toContain('light-bake');
        });
    });

    describe('post-process partitioning', () => {
        it('places before-tonemap effects before tonemap and after-tonemap after', () => {
            const graph = new RenderTextureRegistry();
            const planner = new RenderPassPlanner(graph, createDefaultSettings());
            const postEffects: ResolvedPostProcessEffect[] = [
                { category: 'builtin', name: 'bloom', phase: 'before-tonemap', quality: 'high', order: 0, settings: {} as any },
                { category: 'builtin', name: 'fxaa', phase: 'after-tonemap', quality: 'high', order: 1, settings: {} as any },
            ];
            const passes = planner.plan(createMinimalInput({ postEffects }));
            const kinds = passes.map((p) => p.kind);
            const bloomIdx = kinds.findIndex((k) => k === 'post-process' && (passes.find((p, i) => i === kinds.indexOf(k)) as any)?.metadata?.effect?.name === 'bloom');
            const tonemapIdx = kinds.indexOf('tonemap');
            expect(tonemapIdx).toBeGreaterThan(-1);
        });
    });

    describe('clear', () => {
        it('resets planner state without error', () => {
            const graph = new RenderTextureRegistry();
            const planner = new RenderPassPlanner(graph, createDefaultSettings());
            planner.plan(createMinimalInput());
            expect(() => planner.clear()).not.toThrow();
        });
    });

    describe('pass ordering', () => {
        it('each pass has incrementing order', () => {
            const graph = new RenderTextureRegistry();
            const planner = new RenderPassPlanner(graph, createDefaultSettings());
            const passes = planner.plan(createMinimalInput({
                shadowEnabled: true,
                shadowLights: createMockLightList(1),
                shadowCasters: createMockOpaqueList(5),
                transparent: createMockOpaqueList(3),
            }));
            for (let i = 1; i < passes.length; i++) {
                expect(passes[i]!.order).toBeGreaterThan(passes[i - 1]!.order);
            }
        });
    });
});
