import { describe, expect, it } from 'vitest';
import * as renderCore from '@axrone/render-core';
import * as renderCorePlanner from '@axrone/render-core/planner';
import * as renderCorePipeline from '@axrone/render-core/pipeline';
import * as renderCoreShaderEffect from '@axrone/render-core/shader-effect';
import * as renderCoreNineSlice from '@axrone/render-core/nine-slice';

describe('render-core entry', () => {
    it('surfaces render pipeline planning primitives without leaking backend-specific texture APIs', () => {
        expect(renderCore.RenderPipeline).toBeDefined();
        expect(renderCore.createRenderPipeline).toBeDefined();
        expect(renderCore.RenderPipelineError).toBeDefined();
        expect(renderCore.createRenderPassGraph).toBeDefined();
        expect('TextureFormat' in renderCore).toBe(false);
        expect('TextureFormatInfo' in renderCore).toBe(false);

        expect(renderCorePipeline.RenderPipeline).toBeDefined();
        expect(renderCorePipeline.createRenderPipeline).toBeDefined();
        expect('compileRenderShaderEffect' in renderCorePipeline).toBe(false);

        expect(renderCorePlanner.RenderFrameClassifier).toBeDefined();
        expect(renderCorePlanner.RenderPassPlanner).toBeDefined();
        expect('RenderPipeline' in renderCorePlanner).toBe(false);

        expect(renderCoreShaderEffect.compileRenderShaderEffect).toBeDefined();
        expect(renderCoreShaderEffect.cloneRenderShaderEffectDefinition).toBeDefined();
        expect('RenderPipeline' in renderCoreShaderEffect).toBe(false);

        expect(renderCoreNineSlice.computeAxisBorderSizes).toBeDefined();
        expect(renderCoreNineSlice.computeAxisUvFractions).toBeDefined();
        expect(renderCoreNineSlice.ROUNDED_RECT_SDF_GLSL).toBeDefined();
        expect('computeAxisBorderSizes' in renderCore).toBe(true);
    });
});