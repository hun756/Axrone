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

describe('reflectShaderEffect — edge cases', () => {
    it('returns empty arrays when optional fields are absent', () => {
        const minimal = defineShaderEffect({
            id: 'minimal-reflect',
            vertex: vtxStage(glsl`gl_Position = vec4(0.0);`),
            fragment: fragStage(glsl`o_Color = vec4(1.0);`),
        });
        const r = reflectShaderEffect(minimal);
        expect(r.attributes).toEqual([]);
        expect(r.varyings).toEqual([]);
        expect(r.uniforms).toEqual([]);
        expect(r.keywords).toEqual([]);
        expect(r.techniques).toEqual([]);
        expect(r.defaultTechnique).toBeUndefined();
    });

    it('reflects uniform arrayLength and hasDefault', () => {
        const withArray = defineShaderEffect({
            id: 'array-reflect',
            properties: [
                prop('u_Joints', 'mat4', 'object', undefined, { arrayLength: 32 }),
                prop('u_NoDefault', 'float', 'material'),
            ],
            vertex: vtxStage(glsl`gl_Position = vec4(0.0);`),
            fragment: fragStage(glsl`o_Color = vec4(1.0);`),
        });
        const r = reflectShaderEffect(withArray);
        const joints = r.uniforms.find((u) => u.name === 'u_Joints');
        expect(joints?.arrayLength).toBe(32);
        expect(joints?.hasDefault).toBe(false);

        const noDefault = r.uniforms.find((u) => u.name === 'u_NoDefault');
        expect(noDefault?.hasDefault).toBe(false);
    });

    it('reflects uniform hasDefault as true when defaultValue is present', () => {
        const r = reflectShaderEffect(effect);
        const tint = r.uniforms.find((u) => u.name === 'u_Tint');
        expect(tint?.hasDefault).toBe(true);
    });

    it('reflects uniform inspector and inspectorGroup', () => {
        const r = reflectShaderEffect(effect);
        const tint = r.uniforms.find((u) => u.name === 'u_Tint');
        expect(tint?.inspector?.control).toBe('color');
        expect(tint?.inspectorGroup).toBe('Surface');
    });

    it('reflects stage counts correctly', () => {
        const r = reflectShaderEffect(effect);
        expect(r.stages.vertex.mainLineCount).toBeGreaterThan(0);
        expect(r.stages.fragment.outputCount).toBe(1);
        expect(r.stages.fragment.inputCount).toBe(0);
    });

    it('reflects technique label', () => {
        const withLabel = defineShaderEffect({
            id: 'label-reflect',
            techniques: [
                technique('fwd', [pass('lit')], 'Forward Render'),
            ],
            vertex: vtxStage(glsl`gl_Position = vec4(0.0);`),
            fragment: fragStage(glsl`o_Color = vec4(1.0);`),
        });
        const r = reflectShaderEffect(withLabel);
        expect(r.techniques[0]?.label).toBe('Forward Render');
    });

    it('reflects pass with vertex stage', () => {
        const withVtxPass = defineShaderEffect({
            id: 'pass-reflect',
            techniques: [
                technique('t', [
                    pass('p', {
                        vertex: vtxStage(glsl`gl_Position = vec4(0.0);`),
                        renderState: { depthTest: true, cull: false, blend: true },
                    }),
                ]),
            ],
            vertex: vtxStage(glsl`gl_Position = vec4(0.0);`),
            fragment: fragStage(glsl`o_Color = vec4(1.0);`),
        });
        const r = reflectShaderEffect(withVtxPass);
        const p = r.techniques[0]?.passes[0];
        expect(p?.hasVertexStage).toBe(true);
        expect(p?.hasFragmentStage).toBe(false);
        expect(p?.depthTest).toBe(true);
        expect(p?.cull).toBe(false);
        expect(p?.blend).toBe(true);
    });

    it('classifies keyword with no options as toggle', () => {
        const r = reflectShaderEffect(effect);
        const fog = r.keywords.find((k) => k.name === 'USE_FOG');
        expect(fog?.kind).toBe('toggle');
        expect(fog?.options).toBeUndefined();
    });

    it('classifies keyword with options as enum', () => {
        const r = reflectShaderEffect(effect);
        const quality = r.keywords.find((k) => k.name === 'QUALITY');
        expect(quality?.kind).toBe('enum');
        expect(quality?.options).toEqual(['LOW', 'HIGH']);
    });
});
