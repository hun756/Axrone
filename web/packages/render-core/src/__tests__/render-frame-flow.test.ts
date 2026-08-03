import { describe, expect, it } from 'vitest';
import { RenderFrameClassifier } from '../render-frame-classifier';
import {
    estimateRenderFrameCostTotals,
    getRenderPostEffectCost,
    sumRenderPostEffectCost,
    degradeRenderFrame,
} from '../render-frame-budget-manager';
import { RenderPassPlanner } from '../render-pass-planner';
import { RenderTextureRegistry } from '../graph';
import { ReusableList } from '../memory';
import type {
    RenderCameraState,
    RenderLight,
    RenderPrimitiveInstance,
    RenderReflectionProbe,
    ResolvedPostProcessEffect,
    RenderViewport,
    RenderFrameInput,
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

function createMockPrimitives(count: number, opts: { transparent?: boolean; alphaClipped?: boolean } = {}): RenderPrimitiveInstance[] {
    return Array.from({ length: count }, (_, i) => ({
        id: `prim:${i}`,
        meshId: `mesh:${i}`,
        material: {
            id: `mat:${i}`,
            model: 'pbr' as const,
            transparent: opts.transparent,
            alphaClipped: opts.alphaClipped,
        },
        worldMatrix: identityMatrix,
        layerMask: 1,
        visible: true,
    }));
}

function createMockLights(count: number): RenderLight[] {
    return Array.from({ length: count }, (_, i) => ({
        type: 'directional' as const,
        id: `light:${i}`,
        direction: [0, -1, 0] as any,
        color: [1, 1, 1] as any,
        intensity: 1,
    }));
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

describe('Render Frame Flow — Integration', () => {
    describe('classifier → budget → pass planner pipeline', () => {
        it('produces correct pass sequence for a simple frame', () => {
            // 1. Classify primitives
            const classifier = new RenderFrameClassifier({
                maxTransparentPrimitives: 100,
                maxActiveLocalLights: 16,
                maxShadowedLights: 4,
                layerMask: 0xffffffff,
            });

            const opaquePrims = createMockPrimitives(10);
            const transparentPrims = createMockPrimitives(5, { transparent: true });
            const lights = createMockLights(2);

            const frameInput: RenderFrameInput = {
                camera: createMockCamera(),
                primitives: [...opaquePrims, ...transparentPrims],
                lights,
            };

            const warnings = new ReusableList<string>(4);
            classifier.classify(frameInput, 0, warnings);

            // 2. Estimate budget
            const costWithShadows = estimateRenderFrameCostTotals({
                deltaTime: 0.016,
                opaqueCount: classifier.opaque.length,
                transparentCount: classifier.transparent.length,
                activeLightCount: classifier.activeLights.length,
                shadowLightCount: classifier.shadowLights.length,
                shadowCasterCount: classifier.shadowCasters.length,
                postProcessCost: 0,
                probeUpdates: 0,
                bakeTaskCost: 0,
                gi: { mode: 'disabled' },
                volumetricsEnabled: false,
                shadowEnabled: true,
            });

            expect(costWithShadows).toBeGreaterThan(0);

            // 3. Plan passes
            const graph = new RenderTextureRegistry();
            const planner = new RenderPassPlanner(graph, createDefaultSettings());

            const viewport: RenderViewport = { width: 1920, height: 1080 };

            const passes = planner.plan({
                frame: 0,
                viewport,
                camera: createMockCamera(),
                environment: undefined,
                hdr: { enabled: true, colorFormat: 'r11g11b10f' as const, outputColorSpace: 'srgb' as const },
                gi: { mode: 'disabled' },
                volumetrics: { enabled: false, froxelResolution: [64, 64, 64] as const, temporalReprojection: false },
                shadowEnabled: classifier.shadowLights.length > 0,
                probeUpdateCount: 0,
                postEffects: [],
                bakeTasks: [],
                opaque: classifier.opaque,
                transparent: classifier.transparent,
                shadowCasters: classifier.shadowCasters,
                activeLights: classifier.activeLights,
                shadowLights: classifier.shadowLights,
                activeProbes: classifier.activeProbes,
                probeUpdates: classifier.probeUpdates,
            });

            // Verify: opaque and present always exist
            const kinds = passes.map((p) => p.kind);
            expect(kinds).toContain('opaque');
            expect(kinds).toContain('present');
            // With transparent prims, transparent pass should exist
            expect(kinds).toContain('transparent');
        });

        it('budget pressure triggers degradation', () => {
            const giSsgi = { mode: 'hybrid' as const };

            const costWithEverything = estimateRenderFrameCostTotals({
                deltaTime: 0.032,
                opaqueCount: 100,
                transparentCount: 20,
                activeLightCount: 8,
                shadowLightCount: 4,
                shadowCasterCount: 50,
                postProcessCost: 0.5,
                probeUpdates: 4,
                bakeTaskCost: 0.2,
                gi: giSsgi,
                volumetricsEnabled: true,
                shadowEnabled: true,
            });

            expect(costWithEverything).toBeGreaterThan(1);

            const degraded = degradeRenderFrame(
                { degradeStrategy: 'aggressive', frameBudgetMs: 1.0, maxPostProcessPasses: 8 },
                {
                    estimatedCost: costWithEverything,
                    gi: giSsgi,
                    volumetrics: { enabled: true },
                    shadowEnabled: true,
                    probeUpdates: 4,
                    postEffects: [],
                    bakeTasks: [{ type: 'lightmap' }] as any,
                    warnings: new ReusableList<string>(),
                }
            );

            // Aggressive degradation should reduce probe updates
            expect(degraded.probeUpdates).toBeLessThanOrEqual(4);
            // Volumetrics should be disabled in aggressive mode
            expect(degraded.volumetrics.enabled).toBe(false);
        });
    });

    describe('post-process cost estimation', () => {
        it('sums costs correctly', () => {
            const effects: ResolvedPostProcessEffect[] = [
                { category: 'builtin', name: 'bloom', phase: 'before-tonemap', quality: 'high', order: 0, settings: {} as any },
                { category: 'builtin', name: 'fxaa', phase: 'after-tonemap', quality: 'high', order: 1, settings: {} as any },
            ];
            const totalCost = sumRenderPostEffectCost(effects);
            const expected = getRenderPostEffectCost(effects[0]) + getRenderPostEffectCost(effects[1]);
            expect(totalCost).toBeCloseTo(expected);
        });
    });
});
