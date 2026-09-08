import { describe, expect, it, vi } from 'vitest';
import { LazyGLStateGuard, GL_STATE_UNIT0_TEXTURE, GL_STATE_UNIT0_SAMPLER, GL_STATE_ACTIVE_TEXTURE } from '../gl-state';

/**
 * Minimal mock WebGL2 context that tracks active texture unit and bindings.
 */
const createMockGL = () => {
    let activeTextureUnit = 0x84c1; // TEXTURE1 — caller was using unit 1
    const textureBindings = new Map<number, WebGLTexture | null>();
    const samplerBindings = new Map<number, WebGLSampler | null>();
    const fakeTexture = { kind: 'texture', id: 99 } as unknown as WebGLTexture;
    const fakeSampler = { kind: 'sampler', id: 42 } as unknown as WebGLSampler;

    const gl = {
        TEXTURE0: 0x84c0,
        TEXTURE_2D: 0x0de1,
        TEXTURE_BINDING_2D: 0x8069,
        SAMPLER_BINDING: 0x8919,
        ACTIVE_TEXTURE: 0x84e0,
        activeTexture: vi.fn((unit: number) => {
            activeTextureUnit = unit;
        }),
        bindTexture: vi.fn((_target: number, texture: WebGLTexture | null) => {
            textureBindings.set(activeTextureUnit, texture);
        }),
        bindSampler: vi.fn((unit: number, sampler: WebGLSampler | null) => {
            samplerBindings.set(unit, sampler);
        }),
        getParameter: vi.fn((parameter: number) => {
            switch (parameter) {
                case 0x84e0: return activeTextureUnit;
                case 0x8069: return textureBindings.get(activeTextureUnit) ?? null;
                case 0x8919: return samplerBindings.get(activeTextureUnit - 0x84c0) ?? null;
                default: return null;
            }
        }),
    } as unknown as WebGL2RenderingContext;

    return { gl, fakeTexture, fakeSampler, textureBindings, samplerBindings, getActiveUnit: () => activeTextureUnit };
};

describe('LazyGLStateGuard restore — active texture unit', () => {
    it('does NOT call activeTexture when only UNIT0_TEXTURE is captured (ACTIVE_TEXTURE not requested)', () => {
        const mock = createMockGL();
        const { gl } = mock;

        // Pre-set: caller is on TEXTURE1, unit 0 has a specific texture bound.
        const callerTexture = { kind: 'caller-tex' } as unknown as WebGLTexture;
        (gl.getParameter as ReturnType<typeof vi.fn>).mockImplementation((p: number) => {
            if (p === gl.TEXTURE_BINDING_2D) return callerTexture;
            if (p === gl.SAMPLER_BINDING) return null;
            return null;
        });

        const guard = new LazyGLStateGuard();
        // Only capture UNIT0_TEXTURE — NOT ACTIVE_TEXTURE.
        guard.capture(gl, GL_STATE_UNIT0_TEXTURE);

        // Simulate the consumer binding its own texture on unit 0.
        gl.activeTexture(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D, mock.fakeTexture);

        // Restore — should NOT call activeTexture because ACTIVE_TEXTURE was not captured.
        guard.restore(gl);

        const activeTextureCalls = (gl.activeTexture as ReturnType<typeof vi.fn>).mock.calls;
        // The only activeTexture call should be the consumer's, not from restore.
        // Filter calls that happened during restore: restore should not have called activeTexture at all.
        const restoreCalls = activeTextureCalls.filter(
            (call) => call[0] === gl.TEXTURE0,
        );
        // The consumer called activeTexture(TEXTURE0) once. Restore must NOT add another.
        expect(restoreCalls.length).toBe(1); // only the consumer's call
    });

    it('restores active texture unit when ACTIVE_TEXTURE was captured', () => {
        const mock = createMockGL();
        const { gl } = mock;

        // Caller is on TEXTURE1.
        const callerTexture = { kind: 'caller-tex' } as unknown as WebGLTexture;
        (gl.getParameter as ReturnType<typeof vi.fn>).mockImplementation((p: number) => {
            if (p === gl.ACTIVE_TEXTURE) return 0x84c1; // TEXTURE1
            if (p === gl.TEXTURE_BINDING_2D) return callerTexture;
            if (p === gl.SAMPLER_BINDING) return null;
            return null;
        });

        const guard = new LazyGLStateGuard();
        // Capture ACTIVE_TEXTURE + UNIT0_TEXTURE.
        guard.capture(gl, GL_STATE_ACTIVE_TEXTURE | GL_STATE_UNIT0_TEXTURE);

        // Consumer switches to TEXTURE0 and binds.
        gl.activeTexture(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D, mock.fakeTexture);

        // Restore — should switch back to TEXTURE1.
        guard.restore(gl);

        // The last activeTexture call should restore to TEXTURE1 (0x84c1).
        const calls = (gl.activeTexture as ReturnType<typeof vi.fn>).mock.calls;
        const lastCall = calls[calls.length - 1][0];
        expect(lastCall).toBe(0x84c1); // TEXTURE1
    });
});
