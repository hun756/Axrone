import { describe, expect, it } from 'vitest';
import * as render3D from '@axrone/render-3d';
import * as render3DPipeline from '@axrone/render-3d/pipeline';
import * as render3DShaderEffect from '@axrone/render-3d/shader-effect';

describe('render-3d entry', () => {
    it('keeps the render-3d root focused on capability metadata and pushes runtime contracts into subpaths', () => {
        expect(render3D.RENDER_3D_CAPABILITY_ID).toBe('render/3d');
        expect(render3D.getRender3DCapability().packageName).toBe('@axrone/render-3d');
        expect('RenderPipeline' in render3D).toBe(false);
        expect('createRenderPipeline' in render3D).toBe(false);

        expect(render3DPipeline.RenderPipeline).toBeDefined();
        expect(render3DPipeline.createRenderPipeline).toBeDefined();
        expect('compileRenderShaderEffect' in render3DPipeline).toBe(false);

        expect(render3DShaderEffect.compileRenderShaderEffect).toBeDefined();
        expect(render3DShaderEffect.cloneRenderShaderEffectDefinition).toBeDefined();
        expect('RenderPipeline' in render3DShaderEffect).toBe(false);
    });
});