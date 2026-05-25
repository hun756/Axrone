import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import {
    AssetDatabase,
    type AssetImportSource,
    type AssetJsonValue,
} from '@axrone/asset-core';
import {
    createSceneMaterialInspectorSections,
    createSceneShaderDefinitionFromEffect,
} from '@axrone/scene-runtime';
import {
    createAssetShaderImportPipeline,
    normalizeShaderEffectJsonSource,
    type AssetShaderImportSchema,
} from '../shader-effect-importer';

const testDir = path.dirname(fileURLToPath(import.meta.url));
const examplesDir = path.resolve(testDir, '../../examples');

const readExampleText = async (fileName: string): Promise<string> =>
    fs.readFile(path.resolve(examplesDir, fileName), 'utf8');

const readExampleJson = async <T>(fileName: string): Promise<T> =>
    JSON.parse(await readExampleText(fileName)) as T;

describe('asset-shader effect import pipeline', () => {
    it('imports shorthand effect JSON and feeds the runtime shader and inspector workflow', async () => {
        const database = new AssetDatabase<AssetShaderImportSchema>({
            pipeline: createAssetShaderImportPipeline(),
        });

        const receipt = await database.import({
            kind: 'text',
            uri: 'content/hero-tint.effect.json',
            mimeType: 'application/json',
            data: await readExampleText('hero-tint.effect.json'),
        });
        const effect = receipt.primary.data as AssetShaderImportSchema['shaderEffect'];

        const shader = createSceneShaderDefinitionFromEffect(effect, {
            attributes: {
                position: 'a_Position',
            },
        });
        const inspectorSections = createSceneMaterialInspectorSections(shader, {
            id: 'materials/hero-tint',
            shaderId: shader.id,
            uniforms: {
                u_Tint: [0.25, 0.5, 0.75, 1],
            },
        });

        expect(receipt.importerId).toBe('asset-shader.effect.json');
        expect(receipt.primary.kind).toBe('shaderEffect');
    expect(effect.format).toBe('axrone.shader/effect');
    expect(effect.version).toBe(1);
    expect(effect.id).toBe('hero-tint');
    expect(effect.properties?.[0]?.inspector?.control).toBe('color');
        expect(shader.uniforms).toEqual(['u_Tint']);
        expect(inspectorSections).toHaveLength(1);
        expect(inspectorSections[0]?.title).toBe('Surface');
        expect(inspectorSections[0]?.controls[0]?.label).toBe('Tint');
    });

    it('imports wrapped effect JSON and preserves inspector select options and array uniforms', async () => {
        const database = new AssetDatabase<AssetShaderImportSchema>({
            pipeline: createAssetShaderImportPipeline(),
        });

        const receipt = await database.import({
            kind: 'json',
            uri: 'content/rig.shader.json',
            data: await readExampleJson<AssetJsonValue>('rig-preview.shader.json'),
        });
        const effect = receipt.primary.data as AssetShaderImportSchema['shaderEffect'];

        expect(effect.id).toBe('shader/rig-preview');
        expect(effect.properties?.[0]?.arrayLength).toBe(32);
        expect(effect.properties?.[1]?.inspector?.options).toEqual([
            { label: 'Opaque', value: 0 },
            { label: 'Blend', value: 2 },
        ]);
    });

    it('imports schema v2 effect metadata for keywords, property bindings, and techniques', async () => {
        const database = new AssetDatabase<AssetShaderImportSchema>({
            pipeline: createAssetShaderImportPipeline(),
        });

        const receipt = await database.import({
            kind: 'text',
            uri: 'content/advanced-surface.effect.json',
            mimeType: 'application/json',
            data: await readExampleText('advanced-surface.effect.json'),
        });
        const effect = receipt.primary.data as AssetShaderImportSchema['shaderEffect'];

        expect(effect.version).toBe(2);
        expect(effect.properties?.[0]?.binding).toEqual({
            target: 'u_SurfaceParams',
            channels: ['r', 'g', 'b', 'a'],
        });
        expect(effect.keywords).toEqual([
            { name: 'USE_FOG', stages: ['fragment'], options: undefined, defaultValue: false },
            {
                name: 'SURFACE_MODE',
                stages: undefined,
                options: ['OPAQUE', 'BLEND'],
                defaultValue: 'OPAQUE',
            },
        ]);
        expect(effect.defaultTechnique).toBe('forward');
        expect(effect.techniques?.[0]).toMatchObject({
            id: 'forward',
            label: 'Forward',
            passes: [
                {
                    id: 'lit',
                    keywords: ['USE_FOG', 'SURFACE_MODE'],
                    renderState: {
                        depthTest: true,
                        cull: true,
                        blend: false,
                    },
                },
            ],
        });
    });

    it('rejects version 1 payloads that declare schema v2-only fields', () => {
        expect(() =>
            normalizeShaderEffectJsonSource(
                {
                    kind: 'json',
                    uri: 'content/legacy.effect.json',
                    data: {},
                } as AssetImportSource,
                {
                    format: 'axrone.shader/effect',
                    version: 1,
                    id: 'legacy-effect',
                    properties: [
                        {
                            name: 'u_Tint',
                            type: 'vec4',
                            binding: {
                                target: 'u_SurfaceParams',
                            },
                        },
                    ],
                    vertex: {
                        main: ['gl_Position = vec4(0.0);'],
                    },
                    fragment: {
                        precision: 'highp',
                        outputs: [{ name: 'o_Color', type: 'vec4' }],
                        main: ['o_Color = vec4(1.0);'],
                    },
                },
            ),
        ).toThrow('Shader effect payload.version must be 2 when keywords, property bindings, or techniques are used');
    });

    it('claims only canonical shader asset extensions', async () => {
        const database = new AssetDatabase<AssetShaderImportSchema>({
            pipeline: createAssetShaderImportPipeline(),
        });

        await expect(
            database.import({
                kind: 'text',
                uri: 'content/hero-tint.json',
                mimeType: 'application/json',
                data: await readExampleText('hero-tint.effect.json'),
            })
        ).rejects.toThrow('No asset importer found');
    });
});
