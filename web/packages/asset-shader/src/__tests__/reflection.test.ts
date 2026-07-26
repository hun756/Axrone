import { describe, expect, it } from 'vitest';
import { reflectShaderEffect } from '../reflection';
import {
    attr,
    defineShaderEffect,
    fragStage,
    glsl,
    keyword,
    pass,
    prop,
    technique,
    vtxStage,
    varying,
} from '../authoring';

const effect = defineShaderEffect({
    id: 'reflection-fixture',
    attributes: [attr('a_Position', 'vec3', 0)],
    varyings: [varying('v_uv', 'vec2')],
    properties: [
        prop('u_Tint', 'vec4', 'material', [1, 1, 1, 1], {
            stages: ['fragment'],
            inspector: { label: 'Tint', control: 'color', group: 'Surface' },
        }),
        prop('u_Density', 'float', 'material', 0.5, { stages: ['fragment'] }),
    ],
    keywords: [
        keyword('USE_FOG', ['fragment'], undefined, false),
        keyword('QUALITY', ['fragment'], ['LOW', 'HIGH'], 'LOW'),
    ],
    defaultTechnique: 'forward',
    techniques: [
        technique('forward', [
            pass('lit', {
                keywords: ['USE_FOG'],
                renderState: { depthTest: true, cull: true, blend: false },
            }),
        ]),
    ],
    vertex: vtxStage(glsl`gl_Position = vec4(a_Position, 1.0);`),
    fragment: fragStage(glsl`o_Color = u_Tint;`, [], {
        outputs: [varying('o_Color', 'vec4')],
    }),
});

describe('reflectShaderEffect', () => {
    const reflection = reflectShaderEffect(effect);

    it('surfaces attributes, varyings and uniforms', () => {
        expect(reflection.attributes[0]).toEqual({ name: 'a_Position', type: 'vec3', location: 0 });
        expect(reflection.varyings[0]?.name).toBe('v_uv');
        expect(reflection.uniforms.map((u) => u.name)).toEqual(['u_Tint', 'u_Density']);
        expect(reflection.uniforms[0]?.inspectorGroup).toBe('Surface');
    });

    it('classifies keywords as toggle vs enum', () => {
        const fog = reflection.keywords.find((k) => k.name === 'USE_FOG');
        const quality = reflection.keywords.find((k) => k.name === 'QUALITY');
        expect(fog?.kind).toBe('toggle');
        expect(quality?.kind).toBe('enum');
        expect(quality?.options).toEqual(['LOW', 'HIGH']);
    });

    it('reports the variant count', () => {
        // USE_FOG (2) × QUALITY (2)
        expect(reflection.variantCount).toBe(4);
    });

    it('describes techniques and passes', () => {
        expect(reflection.defaultTechnique).toBe('forward');
        expect(reflection.techniques[0]?.passes[0]).toMatchObject({
            id: 'lit',
            keywords: ['USE_FOG'],
            depthTest: true,
            hasFragmentStage: false,
        });
    });

    it('serialises to JSON without loss', () => {
        expect(() => JSON.stringify(reflection)).not.toThrow();
    });
});
