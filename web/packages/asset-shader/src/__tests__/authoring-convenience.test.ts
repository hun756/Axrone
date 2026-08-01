import { describe, expect, it } from 'vitest';
import {
    attr,
    defineShaderEffect,
    enumProp,
    fragStage,
    glsl,
    keyword,
    pass,
    prop,
    rangeProp,
    serializeShaderEffectToJson,
    technique,
    toggleProp,
    toShaderEffectSource,
    varying,
    vtxStage,
} from '../authoring';

describe('glsl tagged template — interpolation', () => {
    it('interpolates numeric values', () => {
        const size = 4;
        const result = glsl`vec${size} c = vec${size}(1.0);`;
        expect(result).toBe('vec4 c = vec4(1.0);');
    });

    it('interpolates string values', () => {
        const name = 'u_Color';
        const result = glsl`${name} = vec4(1.0);`;
        expect(result).toBe('u_Color = vec4(1.0);');
    });

    it('handles empty template', () => {
        const result = glsl``;
        expect(result).toBe('');
    });

    it('dedents with interpolated values', () => {
        const expr = 'a + b';
        const result = glsl`
            float result = ${expr};
        `;
        expect(result).toBe('float result = a + b;');
    });
});

describe('attr helper', () => {
    it('defaults to vec3 when no type given', () => {
        expect(attr('a_Pos')).toEqual({ name: 'a_Pos', type: 'vec3' });
    });

    it('includes location when provided', () => {
        const a = attr('a_Normal', 'vec3', 2);
        expect(a).toEqual({ name: 'a_Normal', type: 'vec3', location: 2 });
    });

    it('omits location when not provided', () => {
        const a = attr('a_UV', 'vec2');
        expect(a.location).toBeUndefined();
    });
});

describe('varying helper', () => {
    it('creates a basic varying without interpolation', () => {
        expect(varying('v_uv', 'vec2')).toEqual({ name: 'v_uv', type: 'vec2' });
    });

    it('includes flat interpolation', () => {
        const v = varying('v_flat', 'float', 'flat');
        expect(v).toEqual({ name: 'v_flat', type: 'float', interpolation: 'flat' });
    });

    it('includes smooth interpolation', () => {
        const v = varying('v_smooth', 'vec4', 'smooth');
        expect(v).toEqual({ name: 'v_smooth', type: 'vec4', interpolation: 'smooth' });
    });
});

describe('vtxStage / fragStage options', () => {
    it('sets precision on the stage', () => {
        const stage = vtxStage('gl_Position = vec4(0.0);', [], { precision: 'highp' });
        expect(stage.precision).toBe('highp');
    });

    it('sets directives on the stage', () => {
        const stage = vtxStage('gl_Position = vec4(0.0);', [], {
            directives: ['#extension GL_OES_standard_derivatives : enable'],
        });
        expect(stage.directives).toEqual(['#extension GL_OES_standard_derivatives : enable']);
    });

    it('sets inputs and outputs', () => {
        const input = varying('i_pos', 'vec3');
        const output = varying('o_color', 'vec4');
        const stage = fragStage('o_color = vec4(1.0);', [], {
            inputs: [input],
            outputs: [output],
        });
        expect(stage.inputs).toEqual([input]);
        expect(stage.outputs).toEqual([output]);
    });

    it('sets includes', () => {
        const stage = vtxStage('gl_Position = vec4(0.0);', [], {
            includes: ['common/chunk'],
        });
        expect(stage.includes).toEqual(['common/chunk']);
    });

    it('omits optional fields when not provided', () => {
        const stage = vtxStage('gl_Position = vec4(0.0);');
        expect(stage.precision).toBeUndefined();
        expect(stage.directives).toBeUndefined();
        expect(stage.inputs).toBeUndefined();
        expect(stage.outputs).toBeUndefined();
        expect(stage.includes).toBeUndefined();
    });
});

describe('rangeProp', () => {
    it('creates a property with slider inspector', () => {
        const p = rangeProp('u_density', 'float', 0, 1, 0.05, 0.5);
        expect(p.name).toBe('u_density');
        expect(p.type).toBe('float');
        expect(p.scope).toBe('material');
        expect(p.defaultValue).toBe(0.5);
        expect(p.inspector?.control).toBe('slider');
        expect(p.inspector?.min).toBe(0);
        expect(p.inspector?.max).toBe(1);
        expect(p.inspector?.step).toBe(0.05);
    });

    it('merges custom inspector fields alongside slider', () => {
        const p = rangeProp('u_val', 'float', 0, 10, 1, 5, {
            inspector: { label: 'Value', group: 'Advanced' },
        });
        expect(p.inspector?.control).toBe('slider');
        expect(p.inspector?.label).toBe('Value');
        expect(p.inspector?.group).toBe('Advanced');
    });

    it('respects custom scope', () => {
        const p = rangeProp('u_x', 'float', 0, 1, 0.1, 0.5, { scope: 'object' });
        expect(p.scope).toBe('object');
    });
});

describe('toggleProp', () => {
    it('creates a bool property with toggle inspector, default false', () => {
        const p = toggleProp('u_enabled');
        expect(p.name).toBe('u_enabled');
        expect(p.type).toBe('bool');
        expect(p.defaultValue).toBe(false);
        expect(p.inspector?.control).toBe('toggle');
    });

    it('accepts a custom default value', () => {
        const p = toggleProp('u_enabled', true);
        expect(p.defaultValue).toBe(true);
    });

    it('merges custom inspector fields', () => {
        const p = toggleProp('u_flag', false, {
            inspector: { label: 'Flag', group: 'Features' },
        });
        expect(p.inspector?.control).toBe('toggle');
        expect(p.inspector?.label).toBe('Flag');
        expect(p.inspector?.group).toBe('Features');
    });
});

describe('enumProp', () => {
    it('creates an int property with select inspector', () => {
        const p = enumProp('u_mode', ['Opaque', 'Blend', 'Add']);
        expect(p.name).toBe('u_mode');
        expect(p.type).toBe('int');
        expect(p.defaultValue).toBe(0);
        expect(p.inspector?.control).toBe('select');
        expect(p.inspector?.options).toEqual([
            { label: 'Opaque', value: 0 },
            { label: 'Blend', value: 1 },
            { label: 'Add', value: 2 },
        ]);
    });

    it('accepts a custom default index', () => {
        const p = enumProp('u_mode', ['A', 'B'], 1);
        expect(p.defaultValue).toBe(1);
    });

    it('merges custom inspector fields', () => {
        const p = enumProp('u_quality', ['Low', 'High'], 0, {
            inspector: { label: 'Quality', group: 'Settings' },
        });
        expect(p.inspector?.control).toBe('select');
        expect(p.inspector?.label).toBe('Quality');
        expect(p.inspector?.group).toBe('Settings');
    });
});

describe('keyword helper', () => {
    it('creates a toggle keyword without options', () => {
        const k = keyword('USE_FOG');
        expect(k).toEqual({ name: 'USE_FOG' });
    });

    it('creates a keyword with stages', () => {
        const k = keyword('USE_FOG', ['fragment']);
        expect(k.stages).toEqual(['fragment']);
    });

    it('creates an enum keyword with options', () => {
        const k = keyword('QUALITY', ['fragment'], ['LOW', 'HIGH']);
        expect(k.options).toEqual(['LOW', 'HIGH']);
    });

    it('creates a keyword with default value and inspector', () => {
        const k = keyword('USE_SHADOW', ['fragment'], undefined, true, {
            label: 'Shadows',
            control: 'toggle',
        });
        expect(k.defaultValue).toBe(true);
        expect(k.inspector).toMatchObject({ label: 'Shadows', control: 'toggle' });
    });
});

describe('pass helper', () => {
    it('creates a pass with just an id', () => {
        expect(pass('main')).toEqual({ id: 'main' });
    });

    it('creates a pass with vertex and fragment stages', () => {
        const vtx = vtxStage('gl_Position = vec4(0.0);');
        const frg = fragStage('o_Color = vec4(1.0);');
        const p = pass('lit', { vertex: vtx, fragment: frg });
        expect(p.id).toBe('lit');
        expect(p.vertex).toBe(vtx);
        expect(p.fragment).toBe(frg);
    });

    it('creates a pass with renderState and keywords', () => {
        const p = pass('shadow', {
            renderState: { depthTest: true, cull: true },
            keywords: ['USE_SHADOW'],
        });
        expect(p.renderState).toEqual({ depthTest: true, cull: true });
        expect(p.keywords).toEqual(['USE_SHADOW']);
    });
});

describe('technique helper', () => {
    it('creates a technique without label', () => {
        const t = technique('forward', [pass('lit')]);
        expect(t.id).toBe('forward');
        expect(t.passes).toHaveLength(1);
        expect(t.passes[0]?.id).toBe('lit');
        expect(t.label).toBeUndefined();
    });

    it('creates a technique with label', () => {
        const t = technique('forward', [pass('lit')], 'Forward Render');
        expect(t.label).toBe('Forward Render');
    });
});

describe('defineShaderEffect — version and migrations', () => {
    it('defaults to version 2', () => {
        const effect = defineShaderEffect({
            id: 'test',
            vertex: vtxStage('gl_Position = vec4(0.0);'),
            fragment: fragStage('o_Color = vec4(1.0);'),
        });
        expect(effect.version).toBe(2);
    });

    it('accepts explicit version 1', () => {
        const effect = defineShaderEffect({
            id: 'test-v1',
            version: 1,
            vertex: vtxStage('gl_Position = vec4(0.0);'),
            fragment: fragStage('o_Color = vec4(1.0);'),
        });
        expect(effect.version).toBe(1);
    });

    it('includes migrations when provided', () => {
        const migrations = { fromV1: { id: 'test-migrated' } };
        const effect = defineShaderEffect({
            id: 'test-mig',
            migrations,
            vertex: vtxStage('gl_Position = vec4(0.0);'),
            fragment: fragStage('o_Color = vec4(1.0);'),
        });
        expect(effect.migrations).toBe(migrations);
    });
});

describe('toShaderEffectSource', () => {
    const effect = defineShaderEffect({
        id: 'src-test',
        vertex: vtxStage('gl_Position = vec4(0.0);'),
        fragment: fragStage('o_Color = vec4(1.0);'),
    });

    it('wraps an effect into a custom asset source', () => {
        const source = toShaderEffectSource(effect);
        expect(source.kind).toBe('custom');
        expect(source.format).toBe('axrone.shader/effect');
        expect(source.data).toBe(effect);
        expect(source.uri).toBeUndefined();
    });

    it('includes URI when provided', () => {
        const source = toShaderEffectSource(effect, 'shaders/test.effect.ts');
        expect(source.uri).toBe('shaders/test.effect.ts');
    });
});

describe('serializeShaderEffectToJson — round-trip fidelity', () => {
    it('preserves all fields through JSON serialization', () => {
        const effect = defineShaderEffect({
            id: 'round-trip-full',
            attributes: [attr('a_Pos', 'vec3', 0)],
            varyings: [varying('v_uv', 'vec2')],
            properties: [
                prop('u_Tint', 'vec4', 'material', [1, 0, 0, 1], {
                    stages: ['fragment'],
                    inspector: { label: 'Tint', control: 'color' },
                }),
            ],
            keywords: [keyword('USE_FOG', ['fragment'], undefined, false)],
            vertex: vtxStage(glsl`gl_Position = vec4(a_Pos, 1.0);`),
            fragment: fragStage(glsl`o_Color = u_Tint;`, [], {
                outputs: [varying('o_Color', 'vec4')],
            }),
            renderState: { depthTest: true, cull: true, blend: false },
        });

        const json = serializeShaderEffectToJson(effect);
        const reparsed = JSON.parse(JSON.stringify(json));

        expect(reparsed.id).toBe('round-trip-full');
        expect(reparsed.format).toBe('axrone.shader/effect');
        expect(reparsed.attributes[0].name).toBe('a_Pos');
        expect(reparsed.properties[0].inspector.control).toBe('color');
        expect(reparsed.keywords[0].name).toBe('USE_FOG');
        expect(reparsed.renderState).toEqual({ depthTest: true, cull: true, blend: false });
    });
});
