import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { AssetDatabase } from '@axrone/asset-core';
import {
    createSceneMaterialInspectorSections,
    createSceneShaderDefinitionFromEffect,
} from '@axrone/scene-runtime';
import {
    createAssetShaderImportPipeline,
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

        const shader = createSceneShaderDefinitionFromEffect(receipt.primary.data, {
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
        expect(receipt.primary.data.format).toBe('axrone.shader/effect');
        expect(receipt.primary.data.version).toBe(1);
        expect(receipt.primary.data.id).toBe('hero-tint');
        expect(receipt.primary.data.properties?.[0]?.inspector?.control).toBe('color');
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
            data: await readExampleJson<Record<string, unknown>>('rig-preview.shader.json'),
        });

        expect(receipt.primary.data.id).toBe('shader/rig-preview');
        expect(receipt.primary.data.properties?.[0]?.arrayLength).toBe(32);
        expect(receipt.primary.data.properties?.[1]?.inspector?.options).toEqual([
            { label: 'Opaque', value: 0 },
            { label: 'Blend', value: 2 },
        ]);
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
