import { describe, expect, it } from 'vitest';
import {
    getRenderPostEffectCost,
    sumRenderPostEffectCost,
    estimateRenderFrameCostTotals,
    estimateRenderFrameCost,
    degradeRenderFrame,
    type RenderFrameBudgetSettings,
} from '../render-frame-budget-manager';
import { ReusableList } from '../memory';
import type { ResolvedPostProcessEffect, RenderGlobalIlluminationSettings } from '../types';

const giDisabled: RenderGlobalIlluminationSettings = { mode: 'disabled' };
const giSsgi: RenderGlobalIlluminationSettings = { mode: 'ssgi' };
const giDdgi: RenderGlobalIlluminationSettings = { mode: 'ddgi' };
const giHybrid: RenderGlobalIlluminationSettings = { mode: 'hybrid', baked: { mode: 'disabled' } };

const makeEffect = (name: string, phase: 'before-tonemap' | 'after-tonemap' = 'after-tonemap'): ResolvedPostProcessEffect =>
    ({ category: 'builtin', name, phase, quality: 'high', order: 0, settings: Object.freeze({}) }) as ResolvedPostProcessEffect;

const makeCustomEffect = (): ResolvedPostProcessEffect =>
    ({ category: 'custom', name: 'my-fx', phase: 'after-tonemap', quality: 'high', order: 0, settings: Object.freeze({}) }) as ResolvedPostProcessEffect;

describe('getRenderPostEffectCost', () => {
    it('returns correct cost for taa', () => {
        expect(getRenderPostEffectCost(makeEffect('taa'))).toBe(0.2);
    });

    it('returns correct cost for depth-of-field', () => {
        expect(getRenderPostEffectCost(makeEffect('depth-of-field'))).toBe(0.22);
    });

    it('returns correct cost for bloom', () => {
        expect(getRenderPostEffectCost(makeEffect('bloom'))).toBe(0.18);
    });

    it('returns correct cost for ssao', () => {
        expect(getRenderPostEffectCost(makeEffect('ssao'))).toBe(0.19);
    });

    it('returns correct cost for fxaa', () => {
        expect(getRenderPostEffectCost(makeEffect('fxaa'))).toBe(0.09);
    });

    it('returns default 0.12 for other builtin effects', () => {
        expect(getRenderPostEffectCost(makeEffect('vignette'))).toBe(0.12);
        expect(getRenderPostEffectCost(makeEffect('film-grain'))).toBe(0.12);
    });

    it('returns 0.16 for custom effects', () => {
        expect(getRenderPostEffectCost(makeCustomEffect())).toBe(0.16);
    });
});

describe('sumRenderPostEffectCost', () => {
    it('returns 0 for empty array', () => {
        expect(sumRenderPostEffectCost([])).toBe(0);
    });

    it('sums costs correctly', () => {
        const effects = [makeEffect('bloom'), makeEffect('fxaa')];
        expect(sumRenderPostEffectCost(effects)).toBeCloseTo(0.27);
    });
});

describe('estimateRenderFrameCostTotals', () => {
    const baseTotals = {
        deltaTime: 0.016,
        opaqueCount: 100,
        transparentCount: 10,
        activeLightCount: 4,
        shadowLightCount: 1,
        shadowCasterCount: 50,
        postProcessCost: 0.2,
        probeUpdates: 0,
        bakeTaskCost: 0,
        gi: giDisabled,
        volumetricsEnabled: false,
        shadowEnabled: true,
    };

    it('computes base cost', () => {
        const cost = estimateRenderFrameCostTotals(baseTotals);
        expect(cost).toBeGreaterThan(0);
    });

    it('shadow disabled reduces cost', () => {
        const withShadow = estimateRenderFrameCostTotals(baseTotals);
        const withoutShadow = estimateRenderFrameCostTotals({ ...baseTotals, shadowEnabled: false });
        expect(withoutShadow).toBeLessThan(withShadow);
    });

    it('ssgi adds more cost than disabled', () => {
        const disabled = estimateRenderFrameCostTotals({ ...baseTotals, gi: giDisabled });
        const ssgi = estimateRenderFrameCostTotals({ ...baseTotals, gi: giSsgi });
        expect(ssgi).toBeGreaterThan(disabled);
    });

    it('ddgi adds cost', () => {
        const disabled = estimateRenderFrameCostTotals({ ...baseTotals, gi: giDisabled });
        const ddgi = estimateRenderFrameCostTotals({ ...baseTotals, gi: giDdgi });
        expect(ddgi).toBeGreaterThan(disabled);
    });

    it('hybrid adds most GI cost', () => {
        const ssgi = estimateRenderFrameCostTotals({ ...baseTotals, gi: giSsgi });
        const hybrid = estimateRenderFrameCostTotals({ ...baseTotals, gi: giHybrid });
        expect(hybrid).toBeGreaterThan(ssgi);
    });

    it('volumetrics adds cost', () => {
        const without = estimateRenderFrameCostTotals({ ...baseTotals, volumetricsEnabled: false });
        const withVol = estimateRenderFrameCostTotals({ ...baseTotals, volumetricsEnabled: true });
        expect(withVol).toBeGreaterThan(without);
    });

    it('clamps deltaTime to 0.05', () => {
        const normal = estimateRenderFrameCostTotals({ ...baseTotals, deltaTime: 0.016 });
        const clamped = estimateRenderFrameCostTotals({ ...baseTotals, deltaTime: 1.0 });
        const diff = clamped - normal;
        expect(diff).toBeCloseTo(0, 0);
    });
});

describe('estimateRenderFrameCost', () => {
    it('delegates to estimateRenderFrameCostTotals', () => {
        const cost = estimateRenderFrameCost({
            deltaTime: 0.016,
            opaqueCount: 50,
            transparentCount: 5,
            activeLightCount: 2,
            shadowLightCount: 1,
            shadowCasterCount: 20,
            postEffects: [makeEffect('bloom')],
            probeUpdates: 0,
            bakeTasks: [],
            gi: giDisabled,
            volumetricsEnabled: false,
            shadowEnabled: true,
        });
        expect(cost).toBeGreaterThan(0);
    });
});

describe('degradeRenderFrame', () => {
    const baseSettings: RenderFrameBudgetSettings = {
        frameBudgetMs: 1.0,
        degradeStrategy: 'moderate',
    };

    const expensiveInput = {
        estimatedCost: 5.0,
        gi: giSsgi,
        volumetrics: { enabled: true },
        shadowEnabled: true,
        probeUpdates: 4,
        postEffects: [makeEffect('bloom'), makeEffect('ssao'), makeEffect('fxaa'), makeEffect('vignette'), makeEffect('film-grain'), makeEffect('taa')] as readonly ResolvedPostProcessEffect[],
        bakeTasks: [{ type: 'lightmap' }] as readonly { type: 'lightmap' }[],
        warnings: new ReusableList<string>(),
    };

    it('returns unchanged when strategy is none', () => {
        const result = degradeRenderFrame(
            { ...baseSettings, degradeStrategy: 'none' },
            expensiveInput,
        );
        expect(result.degraded).toBe(false);
        expect(result.gi).toBe(giSsgi);
        expect(result.volumetrics.enabled).toBe(true);
    });

    it('defers bake tasks when over budget', () => {
        const result = degradeRenderFrame(baseSettings, {
            ...expensiveInput,
            warnings: new ReusableList<string>(),
        });
        expect(result.bakeTasks).toHaveLength(0);
        expect(result.degraded).toBe(true);
    });

    it('throttles probe updates (moderate -> 1)', () => {
        const result = degradeRenderFrame(baseSettings, {
            ...expensiveInput,
            bakeTasks: [],
            warnings: new ReusableList<string>(),
        });
        expect(result.probeUpdates).toBe(1);
    });

    it('throttles probe updates (aggressive -> 0)', () => {
        const result = degradeRenderFrame(
            { ...baseSettings, degradeStrategy: 'aggressive' },
            { ...expensiveInput, bakeTasks: [], warnings: new ReusableList<string>() },
        );
        expect(result.probeUpdates).toBe(0);
    });

    it('disables volumetrics when over budget', () => {
        const result = degradeRenderFrame(baseSettings, {
            ...expensiveInput,
            bakeTasks: [],
            probeUpdates: 0,
            warnings: new ReusableList<string>(),
        });
        expect(result.volumetrics.enabled).toBe(false);
    });

    it('downgrades ssgi to disabled', () => {
        const result = degradeRenderFrame(baseSettings, {
            ...expensiveInput,
            bakeTasks: [],
            probeUpdates: 0,
            volumetrics: { enabled: false },
            warnings: new ReusableList<string>(),
        });
        expect(result.gi.mode).toBe('disabled');
    });

    it('downgrades hybrid to baked fallback', () => {
        const result = degradeRenderFrame(baseSettings, {
            ...expensiveInput,
            gi: giHybrid,
            bakeTasks: [],
            probeUpdates: 0,
            volumetrics: { enabled: false },
            warnings: new ReusableList<string>(),
        });
        expect(result.gi.mode).not.toBe('hybrid');
    });

    it('skips shadows in aggressive mode when over budget', () => {
        const result = degradeRenderFrame(
            { frameBudgetMs: 0.1, degradeStrategy: 'aggressive' },
            {
                ...expensiveInput,
                gi: giDisabled,
                bakeTasks: [],
                probeUpdates: 0,
                volumetrics: { enabled: false },
                warnings: new ReusableList<string>(),
            },
        );
        expect(result.shadowEnabled).toBe(false);
    });

    it('truncates post-process stack when over budget', () => {
        const result = degradeRenderFrame(
            { frameBudgetMs: 0.01, degradeStrategy: 'aggressive' },
            {
                ...expensiveInput,
                gi: giDisabled,
                bakeTasks: [],
                probeUpdates: 0,
                volumetrics: { enabled: false },
                shadowEnabled: false,
                warnings: new ReusableList<string>(),
            },
        );
        expect(result.postEffects.length).toBeLessThan(expensiveInput.postEffects.length);
    });

    it('caps warnings at 16', () => {
        const warnings = new ReusableList<string>();
        degradeRenderFrame(
            { frameBudgetMs: 0.001, degradeStrategy: 'aggressive' },
            {
                estimatedCost: 100,
                gi: giSsgi,
                volumetrics: { enabled: true },
                shadowEnabled: true,
                probeUpdates: 10,
                postEffects: Array.from({ length: 20 }, (_, i) => makeEffect('bloom')),
                bakeTasks: Array.from({ length: 5 }, () => ({ type: 'lightmap' as const })),
                warnings,
            },
        );
        expect(warnings.length).toBeLessThanOrEqual(16);
    });
});
