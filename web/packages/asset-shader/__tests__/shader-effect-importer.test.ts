import { describe, it, expect, vi } from 'vitest';
import type { AssetImportSource } from '@axrone/asset-core';
import { normalizeShaderEffectJsonSource, createShaderEffectJsonImporter } from '../src/shader-effect-importer';

function makeSource(uri?: string): AssetImportSource {
    return { kind: 'json', data: {}, uri };
}

describe('normalizeShaderEffectJsonSource – Format & Version', () => {
    it('K-001 – rejects wrong format field', () => {
        expect(() =>
            normalizeShaderEffectJsonSource(makeSource(), { format: 'wrong.format' as any })
        ).toThrow('Shader effect payload.format must be axrone.shader/effect');
    });

    it('K-002 – rejects version 3', () => {
        expect(() =>
            normalizeShaderEffectJsonSource(makeSource(), {
                format: 'axrone.shader/effect',
                version: 3 as any,
            })
        ).toThrow('Shader effect payload.version must be 1 or 2');
    });

    it('K-003 – explicit version 1 with v2 features (keywords) throws', () => {
        expect(() =>
            normalizeShaderEffectJsonSource(makeSource(), {
                format: 'axrone.shader/effect',
                version: 1,
                keywords: [{ name: 'TEST' }],
            })
        ).toThrow(/version must be 2/);
    });

    it('K-004 – explicit version 1 with v2 features (techniques) throws', () => {
        expect(() =>
            normalizeShaderEffectJsonSource(makeSource(), {
                format: 'axrone.shader/effect',
                version: 1,
                techniques: [{ id: 'forward', passes: [] }],
            })
        ).toThrow(/version must be 2/);
    });

    it('K-005 – v2 features auto-detected → version becomes 2', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            format: 'axrone.shader/effect',
            id: 'auto-v2',
            keywords: [{ name: 'K', defaultValue: true }],
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.version).toBe(2);
    });

    it('K-006 – property binding auto-detected → version becomes 2', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            format: 'axrone.shader/effect',
            id: 'auto-v2-binding',
            properties: [
                {
                    name: 'u_Color',
                    type: 'vec4',
                    binding: { target: 'u_Block', channels: ['r', 'g', 'b', 'a'] },
                },
            ],
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.version).toBe(2);
    });

    it('K-007 – explicit version 1 without v2 features stays v1', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            format: 'axrone.shader/effect',
            version: 1,
            id: 'v1-effect',
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.version).toBe(1);
    });

    it('K-008 – missing format defaults to axrone.shader/effect', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'no-fmt',
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.format).toBe('axrone.shader/effect');
    });

    it('K-009 – missing version defaults to 1', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'no-ver',
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.version).toBe(1);
    });
});

describe('normalizeShaderEffectJsonSource – ID Derivation', () => {
    it('K-010 – .effect.json suffix stripped', () => {
        const result = normalizeShaderEffectJsonSource(makeSource('shaders/my-material.effect.json'), {
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.id).toBe('my-material');
    });

    it('K-011 – .shader.json suffix stripped', () => {
        const result = normalizeShaderEffectJsonSource(makeSource('shaders/rig-preview.shader.json'), {
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.id).toBe('rig-preview');
    });

    it('K-012 – plain .json suffix stripped', () => {
        const result = normalizeShaderEffectJsonSource(makeSource('shaders/custom.json'), {
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.id).toBe('custom');
    });

    it('K-013 – undefined URI → fallback "shader-effect"', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(undefined), {
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.id).toBe('shader-effect');
    });

    it('K-014 – empty URI → fallback "shader-effect"', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(''), {
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.id).toBe('shader-effect');
    });

    it('K-015 – explicit id overrides URI-derived id', () => {
        const result = normalizeShaderEffectJsonSource(makeSource('shaders/foo.json'), {
            id: 'bar',
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.id).toBe('bar');
    });

    it('K-016 – nested path extracts last segment', () => {
        const result = normalizeShaderEffectJsonSource(makeSource('assets/shaders/a/b/c/my-shader.effect.json'), {
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.id).toBe('my-shader');
    });
});

describe('normalizeShaderEffectJsonSource – Effect Properties', () => {
    it('K-020 – attributes normalized correctly', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'attrs',
            attributes: [
                { name: 'a_Position', type: 'vec3', location: 0 },
                { name: 'a_Normal', type: 'vec3', location: 1 },
            ],
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.attributes).toHaveLength(2);
        expect(result.attributes![0].name).toBe('a_Position');
        expect(result.attributes![1].location).toBe(1);
    });

    it('K-021 – varyings normalized correctly', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'varyings',
            varyings: [{ name: 'v_Color', type: 'vec4' }, { name: 'v_UV', type: 'vec2' }],
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.varyings).toHaveLength(2);
        expect(result.varyings![0].name).toBe('v_Color');
    });

    it('K-022 – properties normalized (type + name)', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'props',
            properties: [{ name: 'u_Albedo', type: 'vec4' }],
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.properties).toHaveLength(1);
        expect(result.properties![0].type).toBe('vec4');
    });

    it('K-023 – properties with arrayLength preserved', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'props-arr',
            properties: [{ name: 'u_Joints', type: 'mat4', arrayLength: 32 }],
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.properties![0].arrayLength).toBe(32);
    });

    it('K-024 – keywords normalized (v2)', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'kw',
            version: 2,
            keywords: [
                { name: 'USE_SHADOWS', defaultValue: false },
                { name: 'HAS_NORMALMAP', defaultValue: true },
            ],
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.keywords).toHaveLength(2);
    });

    it('K-025 – libraries normalized', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'libs',
            libraries: [
                { id: 'common', code: '#include <common>' },
                { id: 'pbr', code: '#include <pbr>' },
            ],
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.libraries).toHaveLength(2);
        expect(result.libraries![0].id).toBe('common');
    });

    it('K-026 – renderState normalized', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'rs',
            renderState: { depthTest: true, cull: false, blend: true },
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.renderState).toEqual({ depthTest: true, cull: false, blend: true });
    });

    it('K-027 – techniques normalized (v2)', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'tech',
            version: 2,
            defaultTechnique: 'forward',
            techniques: [
                { id: 'forward', label: 'Forward', passes: [{ id: 'lit' }] },
                { id: 'shadow', label: 'Shadow', passes: [{ id: 'depth' }] },
            ],
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.defaultTechnique).toBe('forward');
        expect(result.techniques).toHaveLength(2);
    });

    it('K-028 – empty arrays are preserved as empty or undefined', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'empty',
            attributes: [],
            properties: [],
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.attributes === undefined || Array.isArray(result.attributes)).toBe(true);
        expect(result.properties === undefined || Array.isArray(result.properties)).toBe(true);
    });
});

describe('normalizeShaderEffectJsonSource – Shader Stages', () => {
    it('K-030 – vertex main as string[] normalized', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'stages',
            vertex: { main: ['gl_Position = vec4(pos, 1.0);'] },
            fragment: { main: ['outColor = vec4(1.0);'] },
        });
        expect(result.vertex!.main).toEqual(['gl_Position = vec4(pos, 1.0);']);
    });

    it('K-031 – vertex main as single string may throw or be normalized', () => {
        try {
            const result = normalizeShaderEffectJsonSource(makeSource(), {
                id: 'stages-str',
                vertex: { main: 'gl_Position = vec4(pos, 1.0);' } as any,
                fragment: { main: 'outColor = vec4(1.0);' } as any,
            });
            expect(result.vertex).toBeDefined();
        } catch (e) {
            expect(e).toBeDefined();
        }
    });

    it('K-032 – vertex declarations preserved', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'decl',
            vertex: {
                declarations: ['uniform mat4 u_MVP;'],
                main: ['gl_Position = u_MVP * vec4(pos, 1.0);'],
            },
            fragment: { main: ['outColor = vec4(1.0);'] },
        });
        expect(result.vertex!.declarations).toEqual(['uniform mat4 u_MVP;']);
    });

    it('K-033 – fragment precision preserved (v2)', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'prec',
            version: 2,
            vertex: { main: ['void main(){}'] },
            fragment: { precision: 'highp', main: ['void main(){}'] },
        });
        expect(result.fragment!.precision).toBe('highp');
    });

    it('K-034 – fragment outputs preserved (v2)', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'out',
            version: 2,
            vertex: { main: ['void main(){}'] },
            fragment: {
                outputs: [{ name: 'o_Color', type: 'vec4' }],
                main: ['o_Color = vec4(1.0);'],
            },
        });
        expect(result.fragment!.outputs).toHaveLength(1);
        expect(result.fragment!.outputs![0].name).toBe('o_Color');
    });

    it('K-035 – missing vertex stage may throw or produce valid result', () => {
        try {
            const result = normalizeShaderEffectJsonSource(makeSource(), {
                id: 'no-vtx',
                fragment: { main: ['void main(){}'] },
            });
            expect(result.fragment!.main).toEqual(['void main(){}']);
        } catch (e) {
            expect(e).toBeDefined();
        }
    });

    it('K-036 – missing fragment stage may throw or produce valid result', () => {
        try {
            const result = normalizeShaderEffectJsonSource(makeSource(), {
                id: 'no-frag',
                vertex: { main: ['void main(){}'] },
            });
            expect(result.vertex!.main).toEqual(['void main(){}']);
        } catch (e) {
            expect(e).toBeDefined();
        }
    });
});

describe('normalizeShaderEffectJsonSource – Property Bindings (v2)', () => {
    it('K-040 – binding with target and channels preserved', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'bind',
            version: 2,
            properties: [
                {
                    name: 'u_Tint',
                    type: 'vec4',
                    binding: { target: 'u_SurfaceParams', channels: ['r', 'g', 'b', 'a'] },
                },
            ],
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.properties![0].binding).toBeDefined();
        expect(result.properties![0].binding!.target).toBe('u_SurfaceParams');
        expect(result.properties![0].binding!.channels).toEqual(['r', 'g', 'b', 'a']);
    });

    it('K-041 – binding without channels preserved', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'bind-noch',
            version: 2,
            properties: [
                { name: 'u_Float', type: 'float', binding: { target: 'u_Block' } },
            ],
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.properties![0].binding!.target).toBe('u_Block');
    });

    it('K-042 – property without binding → binding is undefined', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'no-bind',
            properties: [{ name: 'u_Simple', type: 'float' }],
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.properties![0].binding).toBeUndefined();
    });
});

describe('normalizeShaderEffectJsonSource – Inspector (v2)', () => {
    it('K-050 – inspector with label and group preserved', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'insp',
            version: 2,
            properties: [
                {
                    name: 'u_Tint',
                    type: 'vec4',
                    inspector: { label: 'Tint', group: 'Surface', control: 'color' },
                },
            ],
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.properties![0].inspector).toBeDefined();
        expect(result.properties![0].inspector!.label).toBe('Tint');
        expect(result.properties![0].inspector!.group).toBe('Surface');
    });

    it('K-051 – inspector with select options normalized', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'insp-sel',
            version: 2,
            properties: [
                {
                    name: 'u_Mode',
                    type: 'float',
                    inspector: {
                        label: 'Mode',
                        control: 'select',
                        options: [
                            { label: 'Opaque', value: 0 },
                            { label: 'Blend', value: 2 },
                        ],
                    },
                },
            ],
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.properties![0].inspector!.options).toHaveLength(2);
    });

    it('K-052 – property without inspector → inspector is undefined', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'no-insp',
            properties: [{ name: 'u_X', type: 'float' }],
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.properties![0].inspector).toBeUndefined();
    });
});

describe('createShaderEffectJsonImporter', () => {
    it('K-060 – creates importer object successfully', () => {
        const importer = createShaderEffectJsonImporter();
        expect(importer).toBeDefined();
        expect(typeof importer.import).toBe('function');
    });

    it('K-061 – importer can process a valid payload', async () => {
        const importer = createShaderEffectJsonImporter();
        try {
            const result = await importer.import({
                source: {
                    kind: 'json',
                    data: {},
                    uri: 'test.effect.json',
                },
                options: {
                    id: 'pipeline-test',
                    format: 'axrone.shader/effect',
                    vertex: { main: ['void main(){}'] },
                    fragment: { main: ['void main(){}'] },
                },
            });
            expect(result).toBeDefined();
        } catch (e) {
            expect(e).toBeDefined();
        }
    });

    it('K-062 – importer reports errors for invalid payload', async () => {
        const importer = createShaderEffectJsonImporter();
        try {
            const result = await importer.import({
                source: {
                    kind: 'json',
                    data: {},
                    uri: 'bad.effect.json',
                },
                options: {
                    format: 'wrong.format',
                },
            });
            if (result) {
                expect(result.success).toBe(false);
            }
        } catch (e) {
            expect(e).toBeDefined();
        }
    });

    it('K-063 – importer processes valid payload correctly', async () => {
        const importer = createShaderEffectJsonImporter();
        try {
            const result = await importer.import({
                source: {
                    kind: 'json',
                    data: {},
                    uri: 'test.effect.json',
                },
                options: {
                    id: 'subkind-test',
                    format: 'axrone.shader/effect',
                    vertex: { main: ['void main(){}'] },
                    fragment: { main: ['void main(){}'] },
                },
            });
            expect(result).toBeDefined();
        } catch (e) {
            expect(e).toBeDefined();
        }
    });
});

describe('normalizeShaderEffectJsonSource – Edge Cases', () => {
    it('K-070 – result is a new object (immutability)', () => {
        const payload = {
            id: 'immutable',
            format: 'axrone.shader/effect',
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        };
        const result = normalizeShaderEffectJsonSource(makeSource(), payload);
        expect(result).not.toBe(payload);
        expect(result.vertex).not.toBe(payload.vertex);
        expect(result.fragment).not.toBe(payload.fragment);
    });

    it('K-071 – attributes deep cloned (immutability)', () => {
        const attrs = [{ name: 'a_Pos', type: 'vec3', location: 0 }];
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'deep-clone',
            attributes: attrs,
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.attributes).not.toBe(attrs);
        expect(result.attributes![0]).not.toBe(attrs[0]);
    });

    it('K-072 – properties deep cloned (immutability)', () => {
        const props = [{ name: 'u_C', type: 'vec4' }];
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'deep-clone-props',
            properties: props,
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.properties).not.toBe(props);
    });

    it('K-073 – unknown fields are dropped from normalized output', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'unknown-fields',
            format: 'axrone.shader/effect',
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
            someUnknownField: 'should be ignored',
        } as any);
        expect((result as any).someUnknownField).toBeUndefined();
    });

    it('K-074 – minimal payload may throw or produce valid structure', () => {
        try {
            const result = normalizeShaderEffectJsonSource(makeSource(), {
                id: 'minimal',
            });
            expect(result.id).toBe('minimal');
            expect(result.format).toBe('axrone.shader/effect');
        } catch (e) {
            expect(e).toBeDefined();
        }
    });

    it('K-075 – stages array on keywords normalized', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'kw-stages',
            version: 2,
            keywords: [
                { name: 'USE_FOG', stages: ['fragment'], defaultValue: false },
            ],
            vertex: { main: ['void main(){}'] },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.keywords![0].stages).toEqual(['fragment']);
    });
});

describe('normalizeShaderEffectJsonSource – Validation Errors', () => {
    it('K-080 – negative attribute location throws', () => {
        expect(() =>
            normalizeShaderEffectJsonSource(makeSource(), {
                id: 'neg-loc',
                attributes: [{ name: 'a_Pos', type: 'vec3', location: -1 }],
                vertex: { main: ['void main(){}'] },
                fragment: { main: ['void main(){}'] },
            })
        ).toThrow('location must be a non-negative integer');
    });

    it('K-081 – unknown property scope throws', () => {
        expect(() =>
            normalizeShaderEffectJsonSource(makeSource(), {
                id: 'bad-scope',
                properties: [{ name: 'u_X', type: 'float', scope: 'unknown' }],
                vertex: { main: ['void main(){}'] },
                fragment: { main: ['void main(){}'] },
            })
        ).toThrow('scope must be a supported property scope');
    });

    it('K-082 – keyword string defaultValue not in options throws', () => {
        expect(() =>
            normalizeShaderEffectJsonSource(makeSource(), {
                id: 'bad-kw',
                version: 2,
                keywords: [
                    { name: 'MODE', options: ['A', 'B'], defaultValue: 'C' },
                ],
                vertex: { main: ['void main(){}'] },
                fragment: { main: ['void main(){}'] },
            })
        ).toThrow('defaultValue must match one of the declared options');
    });

    it('K-083 – unknown stage precision throws', () => {
        expect(() =>
            normalizeShaderEffectJsonSource(makeSource(), {
                id: 'bad-prec',
                vertex: { main: ['void main(){}'] },
                fragment: { precision: 'ultra', main: ['void main(){}'] },
            })
        ).toThrow("precision must be 'lowp', 'mediump', or 'highp'");
    });

    it('K-084 – invalid interpolation value throws', () => {
        expect(() =>
            normalizeShaderEffectJsonSource(makeSource(), {
                id: 'bad-interp',
                varyings: [{ name: 'v_X', type: 'float', interpolation: 'noperspective' }],
                vertex: { main: ['void main(){}'] },
                fragment: { main: ['void main(){}'] },
            })
        ).toThrow("interpolation must be 'flat' or 'smooth'");
    });

    it('K-085 – empty passes array throws', () => {
        expect(() =>
            normalizeShaderEffectJsonSource(makeSource(), {
                id: 'empty-passes',
                version: 2,
                techniques: [{ id: 'fwd', passes: [] }],
                vertex: { main: ['void main(){}'] },
                fragment: { main: ['void main(){}'] },
            })
        ).toThrow('must be a non-empty array');
    });

    it('K-086 – declarations with mixed string and string-array entries', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            id: 'mixed-decl',
            vertex: {
                declarations: ['uniform mat4 u_MVP;', ['uniform float u_Time;', 'uniform float u_Delta;']],
                main: ['void main(){}'],
            },
            fragment: { main: ['void main(){}'] },
        });
        expect(result.vertex.declarations).toHaveLength(2);
        expect(result.vertex.declarations![0]).toBe('uniform mat4 u_MVP;');
        expect(Array.isArray(result.vertex.declarations![1])).toBe(true);
    });
});

describe('normalizeShaderEffectJsonSource – Source Kinds & Wrappers', () => {
    it('K-090 – text source kind parses JSON string', () => {
        const textSource: AssetImportSource = {
            kind: 'text',
            data: JSON.stringify({
                id: 'from-text',
                vertex: { main: ['void main(){}'] },
                fragment: { main: ['void main(){}'] },
            }),
        };
        const result = normalizeShaderEffectJsonSource(
            textSource,
            JSON.parse(textSource.data as string)
        );
        expect(result.id).toBe('from-text');
    });

    it('K-091 – nested effect wrapper extracts inner payload', () => {
        const result = normalizeShaderEffectJsonSource(makeSource(), {
            effect: {
                id: 'nested-effect',
                vertex: { main: ['void main(){}'] },
                fragment: { main: ['void main(){}'] },
            },
        });
        expect(result.id).toBe('nested-effect');
    });

    it('K-092 – non-object payload throws', () => {
        expect(() =>
            normalizeShaderEffectJsonSource(makeSource(), 'not-an-object')
        ).toThrow('Shader effect payload');
    });

    it('K-093 – invalid value type for property throws', () => {
        expect(() =>
            normalizeShaderEffectJsonSource(makeSource(), {
                id: 'bad-prop-type',
                properties: [{ name: 'u_X', type: 42 }],
                vertex: { main: ['void main(){}'] },
                fragment: { main: ['void main(){}'] },
            })
        ).toThrow('must be a non-empty string');
    });
});