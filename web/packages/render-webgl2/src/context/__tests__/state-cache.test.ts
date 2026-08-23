import { describe, it, expect, vi } from 'vitest';
import { GLStateCache } from '../state-cache';

/**
 * Creates a recording WebGL2 stub that tracks calls to texture/sampler binding.
 */
function createRecordingGL() {
    const calls: { method: string; args: unknown[] }[] = [];
    const gl = {
        TEXTURE0: 0x84c0,
        TEXTURE_2D: 0x0de1,
        TEXTURE_CUBE_MAP: 0x8513,
        ARRAY_BUFFER: 0x8892,
        ELEMENT_ARRAY_BUFFER: 0x8893,
        COPY_READ_BUFFER: 0x8f36,
        COPY_WRITE_BUFFER: 0x8f37,
        PIXEL_PACK_BUFFER: 0x88eb,
        PIXEL_UNPACK_BUFFER: 0x88ec,
        FRAMEBUFFER: 0x8d40,
        READ_FRAMEBUFFER: 0x8ca8,
        DRAW_FRAMEBUFFER: 0x8ca9,
        RENDERBUFFER: 0x8d41,
        BLEND: 0x0be2,
        CULL_FACE: 0x0b44,
        DEPTH_TEST: 0x0b71,
        SCISSOR_TEST: 0x0c11,
        STENCIL_TEST: 0x0b90,
        FRONT: 0x0404,
        BACK: 0x0405,
        FRONT_AND_BACK: 0x0408,
        UNIFORM_BUFFER: 0x8a11,
        TRANSFORM_FEEDBACK_BUFFER: 0x8c8e,

        activeTexture: vi.fn((unit: number) => {
            calls.push({ method: 'activeTexture', args: [unit] });
        }),
        bindTexture: vi.fn((target: number, texture: unknown) => {
            calls.push({ method: 'bindTexture', args: [target, texture] });
        }),
        bindSampler: vi.fn((unit: number, sampler: unknown) => {
            calls.push({ method: 'bindSampler', args: [unit, sampler] });
        }),
        bindBuffer: vi.fn(),
        bindFramebuffer: vi.fn(),
        bindRenderbuffer: vi.fn(),
        bindVertexArray: vi.fn(),
        useProgram: vi.fn(),
        enable: vi.fn(),
        disable: vi.fn(),
        viewport: vi.fn(),
        scissor: vi.fn(),
        blendFuncSeparate: vi.fn(),
        blendEquationSeparate: vi.fn(),
        depthFunc: vi.fn(),
        depthMask: vi.fn(),
        colorMask: vi.fn(),
        cullFace: vi.fn(),
        frontFace: vi.fn(),
        polygonOffset: vi.fn(),
        stencilFuncSeparate: vi.fn(),
        stencilOpSeparate: vi.fn(),
        stencilMaskSeparate: vi.fn(),
        bindBufferBase: vi.fn(),
    } as unknown as WebGL2RenderingContext;

    return { gl, calls };
}

describe('GLStateCache — texture & sampler dedup', () => {
    it('bindTexture issues GL call on first bind', () => {
        const { gl, calls } = createRecordingGL();
        const cache = new GLStateCache(gl);

        const tex = {} as WebGLTexture;
        cache.bindTexture(gl.TEXTURE_2D, tex);

        expect(gl.bindTexture).toHaveBeenCalledTimes(1);
        expect(gl.bindTexture).toHaveBeenCalledWith(gl.TEXTURE_2D, tex);
    });

    it('bindTexture dedupes identical re-bind (same target + texture)', () => {
        const { gl } = createRecordingGL();
        const cache = new GLStateCache(gl);

        const tex = {} as WebGLTexture;
        cache.bindTexture(gl.TEXTURE_2D, tex);
        cache.bindTexture(gl.TEXTURE_2D, tex);

        expect(gl.bindTexture).toHaveBeenCalledTimes(1);
    });

    it('bindTexture does NOT dedup when texture changes', () => {
        const { gl } = createRecordingGL();
        const cache = new GLStateCache(gl);

        const texA = {} as WebGLTexture;
        const texB = {} as WebGLTexture;
        cache.bindTexture(gl.TEXTURE_2D, texA);
        cache.bindTexture(gl.TEXTURE_2D, texB);

        expect(gl.bindTexture).toHaveBeenCalledTimes(2);
    });

    it('null bind clears the dedup entry so next real bind goes through', () => {
        const { gl } = createRecordingGL();
        const cache = new GLStateCache(gl);

        const tex = {} as WebGLTexture;

        // 1) bind real texture
        cache.bindTexture(gl.TEXTURE_2D, tex);
        expect(gl.bindTexture).toHaveBeenCalledTimes(1);

        // 2) unbind via null — must issue a GL call
        cache.bindTexture(gl.TEXTURE_2D, null);
        expect(gl.bindTexture).toHaveBeenCalledTimes(2);
        expect(gl.bindTexture).toHaveBeenLastCalledWith(gl.TEXTURE_2D, null);

        // 3) re-bind same texture — must NOT be deduped (cache now says null)
        cache.bindTexture(gl.TEXTURE_2D, tex);
        expect(gl.bindTexture).toHaveBeenCalledTimes(3);
        expect(gl.bindTexture).toHaveBeenLastCalledWith(gl.TEXTURE_2D, tex);
    });

    it('null re-bind is deduped (no redundant GL call)', () => {
        const { gl } = createRecordingGL();
        const cache = new GLStateCache(gl);

        cache.bindTexture(gl.TEXTURE_2D, null);
        cache.bindTexture(gl.TEXTURE_2D, null);

        // First null bind issues GL call; second is deduped
        expect(gl.bindTexture).toHaveBeenCalledTimes(1);
    });

    it('activeTexture dedupes same unit', () => {
        const { gl } = createRecordingGL();
        const cache = new GLStateCache(gl);

        // Initial unit is 0, so move to 1 first
        cache.activeTexture(1);
        expect(gl.activeTexture).toHaveBeenCalledTimes(1);

        // Re-binding same unit should be deduped
        cache.activeTexture(1);
        expect(gl.activeTexture).toHaveBeenCalledTimes(1);
    });

    it('activeTexture issues GL call when unit changes', () => {
        const { gl } = createRecordingGL();
        const cache = new GLStateCache(gl);

        // Initial unit is 0, move to 3
        cache.activeTexture(3);
        expect(gl.activeTexture).toHaveBeenCalledTimes(1);

        // Move to another unit
        cache.activeTexture(1);
        expect(gl.activeTexture).toHaveBeenCalledTimes(2);
    });

    it('bindSampler dedupes identical re-bind', () => {
        const { gl } = createRecordingGL();
        const cache = new GLStateCache(gl);

        const sampler = {} as WebGLSampler;
        cache.bindSampler(0, sampler);
        cache.bindSampler(0, sampler);

        expect(gl.bindSampler).toHaveBeenCalledTimes(1);
    });

    it('bindSampler null clears dedup so next real bind goes through', () => {
        const { gl } = createRecordingGL();
        const cache = new GLStateCache(gl);

        const sampler = {} as WebGLSampler;
        cache.bindSampler(0, sampler);
        expect(gl.bindSampler).toHaveBeenCalledTimes(1);

        cache.bindSampler(0, null);
        expect(gl.bindSampler).toHaveBeenCalledTimes(2);
        expect(gl.bindSampler).toHaveBeenLastCalledWith(0, null);

        cache.bindSampler(0, sampler);
        expect(gl.bindSampler).toHaveBeenCalledTimes(3);
    });

    it('reset() clears all texture and sampler bindings', () => {
        const { gl } = createRecordingGL();
        const cache = new GLStateCache(gl);

        const tex = {} as WebGLTexture;
        cache.bindTexture(gl.TEXTURE_2D, tex);
        cache.bindSampler(0, {} as WebGLSampler);

        cache.reset();

        // After reset, re-binding same texture should issue a GL call (cache was cleared)
        cache.bindTexture(gl.TEXTURE_2D, tex);
        expect(gl.bindTexture).toHaveBeenCalledTimes(2); // first bind + post-reset bind
    });

    it('different texture units are tracked independently', () => {
        const { gl } = createRecordingGL();
        const cache = new GLStateCache(gl);

        const texA = {} as WebGLTexture;
        const texB = {} as WebGLTexture;

        cache.activeTexture(0);
        cache.bindTexture(gl.TEXTURE_2D, texA);

        cache.activeTexture(1);
        cache.bindTexture(gl.TEXTURE_2D, texB);

        // Both binds should go through (different units)
        expect(gl.bindTexture).toHaveBeenCalledTimes(2);

        // Re-bind on unit 0 should be deduped
        cache.activeTexture(0);
        cache.bindTexture(gl.TEXTURE_2D, texA);
        expect(gl.bindTexture).toHaveBeenCalledTimes(2); // still 2
    });
});
