import { describe, expect, it } from 'vitest';
import { SceneDrawExecutionContextCache } from '../rendering/draw-execution-context';
import type { SceneDrawExecutorContext } from '../rendering/draw-executor';

// The render runtime feeds every draw through ONE retained context object that
// `SceneDrawExecutionContextCache.prepare()` copies field by field. A field
// added to `SceneDrawExecutorContext` but forgotten in `prepare()` is silently
// `undefined` at draw time (the interface says `number`, so TypeScript cannot
// catch it), which is exactly how the `_RenderTargetSize` feed would break.
// These assertions enumerate every declared context field and prove each one is
// actually copied.

const createParams = (
    overrides: Partial<SceneDrawExecutorContext> = {}
): SceneDrawExecutorContext => ({
    renderPass: { id: 'main', rendererPassId: 'main', materialPassId: null } as never,
    cameraFrame: { marker: 'camera-frame' } as never,
    lighting: { marker: 'lighting' } as never,
    lightingValues: { marker: 'lighting-values' } as never,
    fog: { enabled: true } as never,
    elapsedSeconds: 12.5,
    deltaSeconds: 1 / 60,
    frame: 7,
    viewportWidth: 800,
    viewportHeight: 600,
    renderTargetWidth: 512,
    renderTargetHeight: 256,
    ...overrides,
});

describe('SceneDrawExecutionContextCache prepare', () => {
    it('carries the active render target size into the prepared context', () => {
        const cache = new SceneDrawExecutionContextCache();

        const context = cache.prepare(createParams({ renderTargetWidth: 512, renderTargetHeight: 256 }));

        expect(context.renderTargetWidth).toBe(512);
        expect(context.renderTargetHeight).toBe(256);
    });

    it('copies EVERY declared context field — no silent undefined', () => {
        const cache = new SceneDrawExecutionContextCache();
        const params = createParams();

        const context = cache.prepare(params);

        // Object.keys on the retained context enumerates exactly the fields
        // `prepare()` assigns; the source of truth is the input object, so a
        // field dropped from `prepare()` shows up as `undefined` here.
        for (const key of Object.keys(params) as (keyof SceneDrawExecutorContext)[]) {
            expect(context[key], `prepare() must copy ${String(key)}`).toBeDefined();
            expect(context[key], `prepare() must copy ${String(key)}`).toEqual(params[key]);
        }

        expect(Object.keys(context).sort()).toEqual(Object.keys(params).sort());
    });

    it('mutates the same retained object instead of allocating on later frames', () => {
        const cache = new SceneDrawExecutionContextCache();
        const first = cache.prepare(createParams({ renderTargetWidth: 512, renderTargetHeight: 256 }));

        const second = cache.prepare(
            createParams({
                renderTargetWidth: 1024,
                renderTargetHeight: 768,
                viewportWidth: 1600,
                viewportHeight: 900,
                frame: 8,
            })
        );

        expect(second).toBe(first);
        expect(second.renderTargetWidth).toBe(1024);
        expect(second.renderTargetHeight).toBe(768);
        expect(second.viewportWidth).toBe(1600);
        expect(second.viewportHeight).toBe(900);
        expect(second.frame).toBe(8);
    });

    it('re-reads the render target size every prepare, so a mid-frame target switch propagates', () => {
        const cache = new SceneDrawExecutionContextCache();

        // Frame starts straight to the canvas, then an offscreen pass resizes
        // the active target: the executor must see the new numbers, not a
        // value latched from the first prepare().
        const canvasBound = cache.prepare(
            createParams({ renderTargetWidth: 800, renderTargetHeight: 600 })
        );
        expect(canvasBound.renderTargetWidth).toBe(800);
        expect(canvasBound.renderTargetHeight).toBe(600);

        const offscreenBound = cache.prepare(
            createParams({ renderTargetWidth: 256, renderTargetHeight: 128 })
        );
        expect(offscreenBound).toBe(canvasBound);
        expect(offscreenBound.renderTargetWidth).toBe(256);
        expect(offscreenBound.renderTargetHeight).toBe(128);
    });

    it('keeps resource references identity-copied rather than cloned', () => {
        const cache = new SceneDrawExecutionContextCache();
        const params = createParams();

        const context = cache.prepare(params);

        expect(context.renderPass).toBe(params.renderPass);
        expect(context.cameraFrame).toBe(params.cameraFrame);
        expect(context.lighting).toBe(params.lighting);
        expect(context.lightingValues).toBe(params.lightingValues);
        expect(context.fog).toBe(params.fog);
    });
});
