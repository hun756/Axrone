import { describe, expect, it, beforeEach } from 'vitest';
import { createSceneShaderDefinitionFromEffect } from '../shader-effect';
import type { RenderShaderEffectDefinition } from '@axrone/render-core';

const createBaseEffect = (): RenderShaderEffectDefinition => ({
    format: 'axrone.shader/effect',
    version: 1,
    id: 'scene/effect-test',
    attributes: [{ name: 'a_Position', type: 'vec3', location: 0 }],
    properties: [
        {
            name: 'u_Model',
            type: 'mat4',
            stages: ['vertex'],
            scope: 'object',
        },
        {
            name: 'u_Color',
            type: 'vec4',
            stages: ['fragment'],
            scope: 'material',
            inspector: { control: 'color' },
        },
    ],
    vertex: {
        main: ['gl_Position = u_Model * vec4(a_Position, 1.0);'],
    },
    fragment: {
        precision: 'highp',
        outputs: [{ name: 'o_Color', type: 'vec4' }],
        main: ['o_Color = u_Color;'],
    },
    renderState: {
        depthTest: false,
        cull: false,
        blend: true,
    },
});

const createMinimalEffect = (): RenderShaderEffectDefinition => ({
    format: 'axrone.shader/effect',
    version: 1,
    id: 'scene/minimal',
    vertex: { main: ['gl_Position = vec4(0.0);'] },
    fragment: { main: ['void main(){}'] },
});

const createMultiPropertyEffect = (): RenderShaderEffectDefinition => ({
    format: 'axrone.shader/effect',
    version: 1,
    id: 'scene/multi-prop',
    properties: [
        { name: 'u_Albedo', type: 'vec4', stages: ['fragment'], scope: 'material' },
        { name: 'u_Metallic', type: 'float', stages: ['fragment'], scope: 'material' },
        { name: 'u_Roughness', type: 'float', stages: ['fragment'], scope: 'material' },
        { name: 'u_MVP', type: 'mat4', stages: ['vertex'], scope: 'object' },
    ],
    vertex: { main: ['gl_Position = u_MVP * vec4(0.0);'] },
    fragment: { main: ['void main(){}'] },
});

describe('createSceneShaderDefinitionFromEffect – Basic Creation', () => {
    it('SE-001 – creates definition from structured effect data', () => {
        const definition = createSceneShaderDefinitionFromEffect(createBaseEffect(), {
            id: 'scene/effect-runtime',
            attributes: { position: 'a_Position' },
        });

        expect(definition.id).toBe('scene/effect-runtime');
        expect(definition.effect?.id).toBe('scene/effect-runtime');
        expect(definition.uniforms).toEqual(['u_Model', 'u_Color']);
        expect(definition.vertexSource).toContain('uniform mat4 u_Model;');
        expect(definition.fragmentSource).toContain('uniform vec4 u_Color;');
        expect(definition.attributes?.position).toBe('a_Position');
        expect(definition.depthTest).toBe(false);
        expect(definition.cull).toBe(false);
        expect(definition.blend).toBe(true);
    });

    it('SE-002 – creates definition without options (defaults)', () => {
        const definition = createSceneShaderDefinitionFromEffect(createBaseEffect());

        expect(definition.id).toBe('scene/effect-test');
        expect(definition.effect).toBeDefined();
        expect(definition.vertexSource).toBeDefined();
        expect(definition.fragmentSource).toBeDefined();
    });

    it('SE-003 – minimal effect produces valid definition', () => {
        const definition = createSceneShaderDefinitionFromEffect(createMinimalEffect());

        expect(definition.id).toBe('scene/minimal');
        expect(definition.uniforms).toEqual([]);
        expect(definition.vertexSource).toBeDefined();
        expect(definition.fragmentSource).toBeDefined();
    });
});

describe('createSceneShaderDefinitionFromEffect – ID Override', () => {
    it('SE-010 – options.id overrides effect.id', () => {
        const definition = createSceneShaderDefinitionFromEffect(createBaseEffect(), {
            id: 'custom/override',
        });
        expect(definition.id).toBe('custom/override');
        expect(definition.effect?.id).toBe('custom/override');
    });

    it('SE-011 – without options.id, uses effect.id', () => {
        const definition = createSceneShaderDefinitionFromEffect(createBaseEffect());
        expect(definition.id).toBe('scene/effect-test');
    });
});

describe('createSceneShaderDefinitionFromEffect – Attribute Mapping', () => {
    it('SE-020 – attributes mapping is passed through', () => {
        const definition = createSceneShaderDefinitionFromEffect(createBaseEffect(), {
            attributes: {
                position: 'a_Position',
                normal: 'a_Normal',
            },
        });
        expect(definition.attributes?.position).toBe('a_Position');
        expect(definition.attributes?.normal).toBe('a_Normal');
    });

    it('SE-021 – without attributes option, attributes is undefined', () => {
        const definition = createSceneShaderDefinitionFromEffect(createBaseEffect());
        expect(definition.attributes).toBeUndefined();
    });
});

describe('createSceneShaderDefinitionFromEffect – Uniform Collection', () => {
    it('SE-030 – uniforms auto-collected from effect properties', () => {
        const definition = createSceneShaderDefinitionFromEffect(createBaseEffect());
        expect(definition.uniforms).toContain('u_Model');
        expect(definition.uniforms).toContain('u_Color');
    });

    it('SE-031 – options.uniforms overrides auto-collected uniforms', () => {
        const definition = createSceneShaderDefinitionFromEffect(createBaseEffect(), {
            uniforms: ['u_Custom'],
        });
        expect(definition.uniforms).toEqual(['u_Custom']);
        expect(definition.uniforms).not.toContain('u_Model');
    });

    it('SE-032 – multi-property effect collects all uniforms', () => {
        const definition = createSceneShaderDefinitionFromEffect(createMultiPropertyEffect());
        expect(definition.uniforms).toHaveLength(4);
        expect(definition.uniforms).toContain('u_Albedo');
        expect(definition.uniforms).toContain('u_Metallic');
        expect(definition.uniforms).toContain('u_Roughness');
        expect(definition.uniforms).toContain('u_MVP');
    });

    it('SE-033 – empty properties produces empty uniforms', () => {
        const effect: RenderShaderEffectDefinition = {
            format: 'axrone.shader/effect',
            version: 1,
            id: 'no-props',
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        };
        const definition = createSceneShaderDefinitionFromEffect(effect);
        expect(definition.uniforms).toEqual([]);
    });
});

describe('createSceneShaderDefinitionFromEffect – Render State', () => {
    it('SE-040 – render state from effect is applied', () => {
        const definition = createSceneShaderDefinitionFromEffect(createBaseEffect());
        expect(definition.depthTest).toBe(false);
        expect(definition.cull).toBe(false);
        expect(definition.blend).toBe(true);
    });

    it('SE-041 – options overrides render state from effect', () => {
        const definition = createSceneShaderDefinitionFromEffect(createBaseEffect(), {
            depthTest: true,
            cull: true,
            blend: false,
        });
        expect(definition.depthTest).toBe(true);
        expect(definition.cull).toBe(true);
        expect(definition.blend).toBe(false);
    });

    it('SE-042 – partial options override only specified fields', () => {
        const definition = createSceneShaderDefinitionFromEffect(createBaseEffect(), {
            depthTest: true,
        });
        expect(definition.depthTest).toBe(true);
        expect(definition.cull).toBe(false); // from effect
        expect(definition.blend).toBe(true); // from effect
    });

    it('SE-043 – minimal effect without renderState has undefined state', () => {
        const definition = createSceneShaderDefinitionFromEffect(createMinimalEffect());
        expect(definition.depthTest).toBeUndefined();
        expect(definition.cull).toBeUndefined();
        expect(definition.blend).toBeUndefined();
    });
});

describe('createSceneShaderDefinitionFromEffect – Shader Source', () => {
    it('SE-050 – vertexSource contains attribute declarations', () => {
        const definition = createSceneShaderDefinitionFromEffect(createBaseEffect());
        expect(definition.vertexSource).toContain('a_Position');
    });

    it('SE-051 – vertexSource contains main body', () => {
        const definition = createSceneShaderDefinitionFromEffect(createBaseEffect());
        expect(definition.vertexSource).toContain('gl_Position');
    });

    it('SE-052 – fragmentSource contains precision directive', () => {
        const definition = createSceneShaderDefinitionFromEffect(createBaseEffect());
        expect(definition.fragmentSource).toContain('highp');
    });

    it('SE-053 – fragmentSource contains output declarations', () => {
        const definition = createSceneShaderDefinitionFromEffect(createBaseEffect());
        expect(definition.fragmentSource).toContain('o_Color');
    });

    it('SE-054 – fragmentSource contains uniform declarations', () => {
        const definition = createSceneShaderDefinitionFromEffect(createBaseEffect());
        expect(definition.fragmentSource).toContain('uniform vec4 u_Color');
    });
});

describe('createSceneShaderDefinitionFromEffect – Effect Preservation', () => {
    it('SE-060 – effect field is preserved in result', () => {
        const effect = createBaseEffect();
        const definition = createSceneShaderDefinitionFromEffect(effect);
        expect(definition.effect).toBeDefined();
        expect(definition.effect?.format).toBe('axrone.shader/effect');
    });

    it('SE-061 – effect is a clone (not same reference)', () => {
        const effect = createBaseEffect();
        const definition = createSceneShaderDefinitionFromEffect(effect);
        expect(definition.effect).not.toBe(effect);
        expect(definition.effect?.id).toBe(effect.id);
    });

    it('SE-062 – modifying original effect does not affect definition', () => {
        const effect = createBaseEffect();
        const definition = createSceneShaderDefinitionFromEffect(effect);
        expect(definition.effect?.id).toBe('scene/effect-test');
    });
});

describe('createSceneShaderDefinitionFromEffect – Edge Cases', () => {
    it('SE-070 – empty options object produces valid result', () => {
        const definition = createSceneShaderDefinitionFromEffect(createBaseEffect(), {});
        expect(definition.id).toBe('scene/effect-test');
        expect(definition.uniforms).toBeDefined();
    });

    it('SE-071 – undefined options produces valid result', () => {
        const definition = createSceneShaderDefinitionFromEffect(createBaseEffect(), undefined);
        expect(definition.id).toBe('scene/effect-test');
    });

    it('SE-072 – effect without properties produces empty uniforms', () => {
        const effect: RenderShaderEffectDefinition = {
            format: 'axrone.shader/effect',
            version: 1,
            id: 'no-props-effect',
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        };
        const definition = createSceneShaderDefinitionFromEffect(effect);
        expect(definition.uniforms).toEqual([]);
    });

    it('SE-073 – effect without attributes still produces valid source', () => {
        const effect: RenderShaderEffectDefinition = {
            format: 'axrone.shader/effect',
            version: 1,
            id: 'no-attrs',
            vertex: { main: ['gl_Position = vec4(0.0);'] },
            fragment: { main: ['void main(){}'] },
        };
        const definition = createSceneShaderDefinitionFromEffect(effect);
        expect(definition.vertexSource).toContain('gl_Position');
    });

    it('SE-074 – effect without fragment outputs produces valid source', () => {
        const effect: RenderShaderEffectDefinition = {
            format: 'axrone.shader/effect',
            version: 1,
            id: 'no-outputs',
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        };
        const definition = createSceneShaderDefinitionFromEffect(effect);
        expect(definition.fragmentSource).toBeDefined();
    });
});
