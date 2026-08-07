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

    it('merges libraries by id (override replaces same id, appends new)', () => {
        const parent = defineShaderEffect({
            id: 'lib-base',
            libraries: [
                { id: 'common', code: 'float a = 1.0;' },
                { id: 'math', code: 'float b = 2.0;' },
            ],
            vertex: vtxStage(glsl`gl_Position = vec4(0.0);`),
            fragment: fragStage(glsl`o_Color = vec4(1.0);`),
        });
        const child = extendShaderEffect(parent, {
            libraries: [
                { id: 'common', code: 'float a = 99.0;' },
                { id: 'noise', code: 'float n = 0.5;' },
            ],
        });
        const ids = child.libraries?.map((l) => l.id);
        expect(ids).toEqual(['common', 'math', 'noise']);
        expect(child.libraries?.[0]?.code).toBe('float a = 99.0;');
    });

    it('merges techniques by id', () => {
        const parent = defineShaderEffect({
            id: 'tech-base',
            techniques: [
                { id: 'forward', passes: [{ id: 'lit' }] },
                { id: 'shadow', passes: [{ id: 'depth' }] },
            ],
            vertex: vtxStage(glsl`gl_Position = vec4(0.0);`),
            fragment: fragStage(glsl`o_Color = vec4(1.0);`),
        });
        const child = extendShaderEffect(parent, {
            techniques: [{ id: 'forward', passes: [{ id: 'lit2' }] }],
        });
        expect(child.techniques).toHaveLength(2);
        expect(child.techniques?.[0]?.id).toBe('forward');
        expect(child.techniques?.[0]?.passes[0]?.id).toBe('lit2');
    });

    it('concatenates stage declarations from base and override', () => {
        const parent = defineShaderEffect({
            id: 'decl-base',
            vertex: vtxStage(glsl`gl_Position = vec4(0.0);`, ['uniform mat4 u_MVP;']),
            fragment: fragStage(glsl`o_Color = vec4(1.0);`),
        });
        const child = extendShaderEffect(parent, {
            vertex: { declarations: ['uniform float u_Time;'] },
        });
        expect(child.vertex.declarations).toEqual([
            'uniform mat4 u_MVP;',
            'uniform float u_Time;',
        ]);
    });

    it('keeps base declarations when override has none', () => {
        const parent = defineShaderEffect({
            id: 'decl-keep',
            vertex: vtxStage(glsl`gl_Position = vec4(0.0);`, ['uniform mat4 u_MVP;']),
            fragment: fragStage(glsl`o_Color = vec4(1.0);`),
        });
        const child = extendShaderEffect(parent, {});
        expect(child.vertex.declarations).toEqual(['uniform mat4 u_MVP;']);
    });

    it('empty main array in override does NOT replace base main', () => {
        const parent = defineShaderEffect({
            id: 'main-keep',
            vertex: vtxStage(glsl`gl_Position = vec4(0.0);`),
            fragment: fragStage(glsl`o_Color = vec4(1.0);`),
        });
        const child = extendShaderEffect(parent, {
            vertex: { main: [] },
        });
        // empty main should not replace the base
        expect(child.vertex.main).toEqual(parent.vertex.main);
    });

    it('renderState is undefined when both base and override lack it', () => {
        const child = extendShaderEffect(base, {});
        expect(child.renderState).toBeUndefined();
    });

    it('overrides id and version', () => {
        const child = extendShaderEffect(base, { id: 'child-id', version: 1 });
        expect(child.id).toBe('child-id');
        expect(child.version).toBe(1);
    });

    it('overrides defaultTechnique', () => {
        const parent = defineShaderEffect({
            id: 'dt-base',
            defaultTechnique: 'forward',
            vertex: vtxStage(glsl`gl_Position = vec4(0.0);`),
            fragment: fragStage(glsl`o_Color = vec4(1.0);`),
        });
        const child = extendShaderEffect(parent, { defaultTechnique: 'deferred' });
        expect(child.defaultTechnique).toBe('deferred');
    });

    it('attributes merge: override adds new, keeps non-overlapping base', () => {
        const child = extendShaderEffect(base, {
            attributes: [
                { name: 'a_Position', type: 'vec4' }, // override existing
                { name: 'a_Normal', type: 'vec3' }, // new
            ],
        });
        const names = child.attributes?.map((a) => a.name);
        expect(names).toEqual(['a_Position', 'a_Normal']);
        expect(child.attributes?.[0]?.type).toBe('vec4'); // overridden
    });
});
