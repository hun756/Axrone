import { describe, expect, it } from 'vitest';
import { extendShaderEffect } from '../compose';
import { defineShaderEffect, fragStage, glsl, prop, vtxStage, varying } from '../authoring';

const base = defineShaderEffect({
    id: 'base-surface',
    attributes: [{ name: 'a_Position', type: 'vec3' }],
    varyings: [{ name: 'v_uv', type: 'vec2' }],
    properties: [
        prop('u_Tint', 'vec4', 'material', [1, 1, 1, 1], {
            stages: ['fragment'],
            inspector: { label: 'Tint', control: 'color', group: 'Surface' },
        }),
    ],
    vertex: vtxStage(glsl`
        v_uv = a_Position.xy;
        gl_Position = vec4(a_Position, 1.0);
    `),
    fragment: fragStage(
        glsl`o_Color = u_Tint;`,
        [],
        { outputs: [varying('o_Color', 'vec4')] }
    ),
});

describe('extendShaderEffect', () => {
    it('inherits base metadata when override is empty', () => {
        const child = extendShaderEffect(base, {});
        expect(child.id).toBe('base-surface');
        expect(child.properties?.[0]?.name).toBe('u_Tint');
    });

    it('appends new properties and overrides existing ones by name', () => {
        const child = extendShaderEffect(base, {
            properties: [
                prop('u_Tint', 'vec4', 'material', [0, 0, 0, 1], { stages: ['fragment'] }),
                prop('u_Emission', 'float', 'material', 1, { stages: ['fragment'] }),
            ],
        });
        const names = child.properties?.map((p) => p.name);
        expect(names).toEqual(['u_Tint', 'u_Emission']);
        expect(child.properties?.[0]?.defaultValue).toEqual([0, 0, 0, 1]);
    });

    it('replaces a stage main when provided, keeps declarations otherwise', () => {
        const child = extendShaderEffect(base, {
            fragment: { main: ['o_Color = vec4(1.0, 0.0, 0.0, 1.0);'] },
        });
        expect(child.fragment.main).toEqual(['o_Color = vec4(1.0, 0.0, 0.0, 1.0);']);
        expect(child.fragment.outputs?.[0]?.name).toBe('o_Color');
    });

    it('concatenates stage includes without duplicates', () => {
        const parent = extendShaderEffect(base, {});
        const withIncludes = extendShaderEffect(
            { ...parent, fragment: { ...parent.fragment, includes: ['shared'] } },
            { fragment: { includes: ['shared', 'fog'] } }
        );
        expect(withIncludes.fragment.includes).toEqual(['shared', 'fog']);
    });

    it('merges render state shallowly', () => {
        const child = extendShaderEffect(
            { ...base, renderState: { depthTest: true, cull: true, blend: false } },
            { renderState: { blend: true } }
        );
        expect(child.renderState).toEqual({ depthTest: true, cull: true, blend: true });
    });
});
