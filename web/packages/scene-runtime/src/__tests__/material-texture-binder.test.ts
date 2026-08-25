import { describe, it, expect, vi } from 'vitest';
import type { IGLStateCache, ITextureSampler } from '@axrone/render-webgl2';
import { SceneMaterialTextureBinder } from '../material-texture-binder';
import type { SceneMaterialResource } from '../material-registry';
import type { SceneShaderResource } from '../shader-registry';
import type { SceneTextureResource } from '../texture-registry';

/**
 * Regression test: bind→unbind→bind must issue a real GL bindTexture call
 * on the second bind. Previously, unbind() used raw GL calls which left the
 * state cache thinking the texture was still bound, causing the next bind
 * to be dedup-skipped → GPU sampled null → black materials.
 */
describe('SceneMaterialTextureBinder — state-cache integration', () => {
    function createFakeState() {
        return {
            activeTexture: vi.fn(),
            bindTexture: vi.fn(),
            bindSampler: vi.fn(),
        } as unknown as IGLStateCache;
    }

    function createFakeGL() {
        return {
            TEXTURE_2D: 0x0de1,
            TEXTURE0: 0x84c0,
        } as unknown as WebGL2RenderingContext;
    }

    function createResources(units: number[]) {
        const textures = new Map<string, SceneTextureResource>();
        const slots: {
            uniformName: string;
            binding: { textureId: string; samplerId: string | null };
            resolvedUnit: number;
        }[] = [];

        for (const unit of units) {
            const id = `tex_${unit}`;
            textures.set(id, {
                id,
                width: 4,
                height: 4,
                samplerId: null,
                texture: { bind: vi.fn() } as unknown as SceneTextureResource['texture'],
            });
            slots.push({
                uniformName: `u_Tex${unit}`,
                binding: { textureId: id, samplerId: null },
                resolvedUnit: unit,
            });
        }

        const sampler = { bind: vi.fn() } as unknown as ITextureSampler;

        return {
            resources: {
                materials: {
                    getTextureSlots: () => slots,
                },
                textures: {
                    get: (id: string) => textures.get(id),
                },
                resolveSampler: () => sampler,
            },
            textures,
            sampler,
        };
    }

    it('unbind routes through state cache (not raw GL)', () => {
        const gl = createFakeGL();
        const state = createFakeState();
        const binder = new SceneMaterialTextureBinder(gl, state);
        const shader = {} as SceneShaderResource;
        const material = { id: 'mat', shaderId: 's', uniforms: new Map(), textureBindings: new Map() } as SceneMaterialResource;
        const { resources } = createResources([0]);
        const setUniform = vi.fn();

        binder.bind(shader, material, resources, setUniform);
        binder.unbind();

        // The state cache must receive the null-bind, not raw GL
        expect(state.bindSampler).toHaveBeenCalledWith(0, null);
        expect(state.activeTexture).toHaveBeenCalledWith(0);
        expect(state.bindTexture).toHaveBeenCalledWith(0x0de1, null);
    });

    it('REGRESSION: bind→unbind→bind issues real bindTexture on second bind', () => {
        const gl = createFakeGL();

        // Use a state cache that tracks bindings like the real GLStateCache
        const textureBindings = new Map<number, { target: number; texture: WebGLTexture | null }>();
        const samplerBindings = new Map<number, WebGLSampler | null>();
        let activeUnit = 0;

        const state = {
            activeTexture: vi.fn((unit: number) => { activeUnit = unit; }),
            bindTexture: vi.fn((target: number, texture: WebGLTexture | null) => {
                textureBindings.set(activeUnit, { target, texture });
            }),
            bindSampler: vi.fn((unit: number, sampler: WebGLSampler | null) => {
                samplerBindings.set(unit, sampler);
            }),
        } as unknown as IGLStateCache;

        const binder = new SceneMaterialTextureBinder(gl, state);
        const shader = {} as SceneShaderResource;
        const material = { id: 'mat', shaderId: 's', uniforms: new Map(), textureBindings: new Map() } as SceneMaterialResource;
        const { resources, textures } = createResources([2]);
        const setUniform = vi.fn();

        // First bind
        binder.bind(shader, material, resources, setUniform);
        const firstBindCallCount = (textures.get('tex_2')!.texture.bind as ReturnType<typeof vi.fn>).mock.calls.length;
        expect(firstBindCallCount).toBe(1);

        // Unbind
        binder.unbind();

        // After unbind, state cache should record null for unit 2
        expect(state.bindTexture).toHaveBeenCalledWith(0x0de1, null);
        expect(textureBindings.get(2)?.texture).toBeNull();

        // Second bind — must NOT be dedup-skipped
        setUniform.mockClear();
        binder.bind(shader, material, resources, setUniform);

        // The texture.bind was called again (the real bind goes through)
        const secondBindCallCount = (textures.get('tex_2')!.texture.bind as ReturnType<typeof vi.fn>).mock.calls.length;
        expect(secondBindCallCount).toBe(2); // called in first bind + second bind
    });

    it('falls back to raw GL when no state cache is provided', () => {
        const rawGl = {
            TEXTURE_2D: 0x0de1,
            TEXTURE0: 0x84c0,
            bindSampler: vi.fn(),
            activeTexture: vi.fn(),
            bindTexture: vi.fn(),
        } as unknown as WebGL2RenderingContext;

        const binder = new SceneMaterialTextureBinder(rawGl);
        const shader = {} as SceneShaderResource;
        const material = { id: 'mat', shaderId: 's', uniforms: new Map(), textureBindings: new Map() } as SceneMaterialResource;
        const { resources } = createResources([0]);
        const setUniform = vi.fn();

        binder.bind(shader, material, resources, setUniform);
        binder.unbind();

        expect(rawGl.bindSampler).toHaveBeenCalledWith(0, null);
        expect(rawGl.activeTexture).toHaveBeenCalledWith(0x84c0); // TEXTURE0 + 0
        expect(rawGl.bindTexture).toHaveBeenCalledWith(0x0de1, null);
    });
});
