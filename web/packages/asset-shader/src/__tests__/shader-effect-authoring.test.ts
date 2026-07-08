import { describe, expect, it } from 'vitest';
import { AssetDatabase } from '@axrone/asset-core';
import { compileRenderShaderEffect } from '@axrone/render-core/shader-effect';
import {
    attr,
    createShaderEffectModuleImporter,
    defineShaderEffect,
    fragStage,
    glsl,
    prop,
    serializeShaderEffectToJson,
    toShaderEffectSource,
    vtxStage,
    varying,
} from '../authoring';
import { createAssetShaderImportPipeline } from '../shader-effect-importer';
import { nebulaVeil } from '../../examples/nebula-veil.effect';

describe('shader effect authoring helpers', () => {
    it('dedents and splits GLSL written as a template literal', () => {
        const source = glsl`
            void main() {
                fragColor = vec4(1.0);
            }
        `;

        const stage = fragStage(source);

        expect(stage.main[0]).toBe('void main() {');
        expect(stage.main).toContain('    fragColor = vec4(1.0);');
        expect(stage.main[stage.main.length - 1]).toBe('}');
    });

    it('builds attribute and property definitions with sensible defaults', () => {
        const position = attr('a_position');
        expect(position).toEqual({ name: 'a_position', type: 'vec3' });

        const tint = prop('u_Tint', 'vec4', 'material', [1, 1, 1, 1], {
            stages: ['fragment'],
            inspector: { label: 'Tint', control: 'color', group: 'Surface' },
        });
        expect(tint).toMatchObject({
            name: 'u_Tint',
            type: 'vec4',
            scope: 'material',
            defaultValue: [1, 1, 1, 1],
            stages: ['fragment'],
        });
    });

    it('assembles a definition that compiles through the render-core compiler', () => {
        const effect = defineShaderEffect({
            id: 'authoring-smoke',
            attributes: [attr('a_Position')],
            properties: [prop('u_Tint', 'vec4', 'material', undefined, { stages: ['fragment'] })],
            vertex: vtxStage(glsl`
                gl_Position = vec4(a_Position, 1.0);
            `),
            fragment: fragStage(
                glsl`
                    o_Color = u_Tint;
                `,
                [],
                { outputs: [varying('o_Color', 'vec4')] }
            ),
        });

        const compiled = compileRenderShaderEffect(effect);

        expect(compiled.uniformNames).toEqual(['u_Tint']);
        expect(compiled.fragmentSource).toContain('out vec4 o_Color;');
        expect(compiled.fragmentSource).toContain('o_Color = u_Tint;');
    });

    it('serializes back to a plain JSON-compatible object', () => {
        const effect = defineShaderEffect({
            id: 'round-trip',
            vertex: vtxStage('gl_Position = vec4(0.0);'),
            fragment: fragStage('o_Color = vec4(1.0);', [], {
                outputs: [varying('o_Color', 'vec4')],
            }),
        });

        const json = serializeShaderEffectToJson(effect);

        expect(json.format).toBe('axrone.shader/effect');
        expect(json.version).toBe(2);
        expect(() => JSON.stringify(json)).not.toThrow();
    });
});

describe('shader effect module importer', () => {
    it('claims only custom sources tagged with the effect format', () => {
        const importer = createShaderEffectModuleImporter();

        expect(
            importer.canImport?.({
                source: {
                    kind: 'custom',
                    format: 'axrone.shader/effect',
                    data: { id: 'x' },
                },
                locale: 'en',
                database: {} as never,
            })
        ).toBe(true);

        expect(
            importer.canImport?.({
                source: { kind: 'text', data: '' },
                locale: 'en',
                database: {} as never,
            })
        ).toBe(false);
    });

    it('imports a .ts-authored effect through the asset pipeline', async () => {
        const database = new AssetDatabase({
            pipeline: createAssetShaderImportPipeline(),
        });

        const receipt = await database.import(
            toShaderEffectSource(nebulaVeil, 'shaders/nebula-veil.effect.ts')
        );

        expect(receipt.importerId).toBe('asset-shader.effect.module');
        expect(receipt.primary.kind).toBe('shaderEffect');
        expect(receipt.primary.name).toBe('nebulaVeil');
    });

    it('compiles the example nebula effect authored as a .ts module', () => {
        const compiled = compileRenderShaderEffect(nebulaVeil);

        expect(compiled.id).toBe('nebulaVeil');
        expect(compiled.vertexSource).toContain('#version 300 es');
        expect(compiled.fragmentSource).toContain('float hash13(vec3 p3)');
        expect(compiled.fragmentSource).toContain('aces(');
        expect(compiled.uniformNames).toContain('u_density');
    });
});
