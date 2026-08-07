import { describe, expect, it } from 'vitest';
import {
    RENDER_2D_DEFAULT_SPRITE_SHADER_ID,
    RENDER_2D_SPRITE_ATTRIBUTE_NAMES,
    RENDER_2D_SPRITE_EFFECT,
    RENDER_2D_SPRITE_FLOAT_STRIDE,
    RENDER_2D_SPRITE_FRAGMENT_SOURCE,
    RENDER_2D_SPRITE_INDICES_PER_QUAD,
    RENDER_2D_SPRITE_UNIFORM_NAMES,
    RENDER_2D_SPRITE_VERTEX_SOURCE,
    RENDER_2D_SPRITE_VERTEX_STRIDE,
    RENDER_2D_SPRITE_VERTICES_PER_QUAD,
} from '../sprite-shader';

describe('RENDER_2D_SPRITE_EFFECT', () => {
    it('has the correct format, version, and id', () => {
        expect(RENDER_2D_SPRITE_EFFECT.format).toBe('axrone.shader/effect');
        expect(RENDER_2D_SPRITE_EFFECT.version).toBe(1);
        expect(RENDER_2D_SPRITE_EFFECT.id).toBe(RENDER_2D_DEFAULT_SPRITE_SHADER_ID);
        expect(RENDER_2D_DEFAULT_SPRITE_SHADER_ID).toBe('Render2D/Sprite');
    });

    it('declares the expected attributes', () => {
        const names = RENDER_2D_SPRITE_EFFECT.attributes.map((a) => a.name);
        expect(names).toContain(RENDER_2D_SPRITE_ATTRIBUTE_NAMES.position);
        expect(names).toContain(RENDER_2D_SPRITE_ATTRIBUTE_NAMES.uv0);
        expect(names).toContain(RENDER_2D_SPRITE_ATTRIBUTE_NAMES.color0);
    });

    it('declares camera, material, and internal properties', () => {
        const propNames = RENDER_2D_SPRITE_EFFECT.properties.map((p) => p.name);
        expect(propNames).toContain('u_ViewProjection');
        expect(propNames).toContain('u_MainTex');
        expect(propNames).toContain('u_MaskShape');
        expect(propNames).toContain('u_MaskWorldToLocal');
        expect(propNames).toContain('u_MaskSize');
        expect(propNames).toContain('u_MaskAnchor');
        expect(propNames).toContain('u_MaskCornerRadius');
    });

    it('has vertex and fragment shader stages', () => {
        expect(RENDER_2D_SPRITE_EFFECT.vertex.main.length).toBeGreaterThan(0);
        expect(RENDER_2D_SPRITE_EFFECT.fragment.main.length).toBeGreaterThan(0);
        expect(RENDER_2D_SPRITE_EFFECT.fragment.outputs).toHaveLength(1);
        expect(RENDER_2D_SPRITE_EFFECT.fragment.outputs[0]!.name).toBe('o_Color');
    });

    it('has render state configured for 2D sprite rendering', () => {
        expect(RENDER_2D_SPRITE_EFFECT.renderState.depthTest).toBe(false);
        expect(RENDER_2D_SPRITE_EFFECT.renderState.cull).toBe(false);
        expect(RENDER_2D_SPRITE_EFFECT.renderState.blend).toBe(true);
    });
});

describe('compiled sprite shader outputs', () => {
    it('RENDER_2D_SPRITE_UNIFORM_NAMES includes expected uniforms', () => {
        expect(RENDER_2D_SPRITE_UNIFORM_NAMES.length).toBeGreaterThan(0);
        expect(RENDER_2D_SPRITE_UNIFORM_NAMES).toContain('u_ViewProjection');
        expect(RENDER_2D_SPRITE_UNIFORM_NAMES).toContain('u_MainTex');
    });

    it('vertex and fragment source strings are non-empty', () => {
        expect(RENDER_2D_SPRITE_VERTEX_SOURCE.length).toBeGreaterThan(0);
        expect(RENDER_2D_SPRITE_FRAGMENT_SOURCE.length).toBeGreaterThan(0);
    });
});

describe('sprite vertex layout constants', () => {
    it('vertex stride is 24 bytes (6 floats)', () => {
        expect(RENDER_2D_SPRITE_VERTEX_STRIDE).toBe(24);
        expect(RENDER_2D_SPRITE_FLOAT_STRIDE).toBe(6);
    });

    it('quad topology: 4 vertices and 6 indices per quad', () => {
        expect(RENDER_2D_SPRITE_VERTICES_PER_QUAD).toBe(4);
        expect(RENDER_2D_SPRITE_INDICES_PER_QUAD).toBe(6);
    });
});
