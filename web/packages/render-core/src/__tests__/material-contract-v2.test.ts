import { describe, expect, it } from 'vitest';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import {
    BUILTIN_MATERIAL_MODELS_V1,
} from '@axrone/render-core/material-contract';
import {
    BUILTIN_MATERIAL_MODELS_V2,
    MATERIAL_DOMAINS_V2,
    MATERIAL_EXTERNAL_UNIFORMS_V2,
    MATERIAL_MODEL_REGISTRY_V2,
    getMaterialDomainDefinitionV2,
    getMaterialExternalUniformV2,
    getMaterialModelDefinitionV2,
    isMaterialExternalUniformNameV2,
    isMaterialSlotValueTypeV2,
    listMaterialDomainsV2,
    listMaterialExternalUniformNamesV2,
    listMaterialExternalUniformsV2,
    listMaterialModelDefinitionsV2,
    resolveMaterialModelCapability,
    resolveModelIdFromLegacyShaderId,
    resolveModelOutputSlotV2,
    resolveModelStageSlotsV2,
    resolveOutputSlotCapability,
    resolveShaderIdFromModelId,
    serializeMaterialModelsV2,
    validateBuiltinMaterialModelsV2,
    validateMaterialExternalUniformsV2,
} from '@axrone/render-core/material-contract-v2';

const testDir = path.dirname(fileURLToPath(import.meta.url));
const modelsV2JsonPath = path.join(testDir, '..', 'material-models.v2.json');

describe('material contract v2 domains and registry', () => {
    it('defines all four contract domains including surface, decal, post-process, and supported volume', () => {
        expect(listMaterialDomainsV2()).toBe(MATERIAL_DOMAINS_V2);
        expect(MATERIAL_DOMAINS_V2.map((domain) => domain.id)).toEqual([
            'surface',
            'decal',
            'post-process',
            'volume',
        ]);
        expect(getMaterialDomainDefinitionV2('volume')?.capabilities[0]?.status).toBe('supported');
        expect(getMaterialDomainDefinitionV2('decal')?.supportsDepthOffset).toBe(true);
        expect(getMaterialDomainDefinitionV2('post-process')?.supportsVertexOffset).toBe(false);
    });

    it('registers all 13 V2 Material Models cleanly without disturbing V1', () => {
        expect(BUILTIN_MATERIAL_MODELS_V1).toHaveLength(3);
        expect(listMaterialModelDefinitionsV2()).toBe(BUILTIN_MATERIAL_MODELS_V2);
        expect(BUILTIN_MATERIAL_MODELS_V2.map((model) => model.id)).toEqual([
            'pbr',
            'unlit',
            'toon',
            'hair',
            'cloth',
            'water',
            'foliage',
            'sprite',
            'particle',
            'composable',
            'decal',
            'post-process',
            'volume',
        ]);
        expect(MATERIAL_MODEL_REGISTRY_V2.size).toBe(13);
        expect(getMaterialModelDefinitionV2('pbr')?.passPlan.map((pass) => pass.id)).toContain('refraction-copy');
        expect(getMaterialModelDefinitionV2('pbr')?.passPlan.map((pass) => pass.id)).toContain('sss-blur');
    });

    it('passes structural V2 contract validation across every model', () => {
        const validation = validateBuiltinMaterialModelsV2();
        expect(validation.errors).toEqual([]);
        expect(validation.valid).toBe(true);
    });

    it('upgrades V1 models with ambientOcclusion, opacityMap, and depthOffset slots', () => {
        expect(resolveModelOutputSlotV2('pbr', 'ambientOcclusion')?.bindingKey).toBe('_OcclusionFactor');
        expect(resolveModelOutputSlotV2('pbr', 'opacityMap')?.bindingKey).toBe('_OpacityTexture');
        expect(resolveModelOutputSlotV2('pbr', 'depthOffset')?.bindingKey).toBe('_DepthOffset');
        expect(resolveModelOutputSlotV2('toon', 'ambientOcclusion')?.capability).toBe('supported');
        expect(resolveModelOutputSlotV2('unlit', 'opacityMap')?.capability).toBe('supported');
        expect(resolveModelOutputSlotV2('composable', 'surfaceAttributes')?.valueType).toBe(
            'surfaceAttributes'
        );
        expect(isMaterialSlotValueTypeV2('surfaceAttributes')).toBe(true);
    });

    it('declares animated vertex channels on water and foliage models', () => {
        expect(resolveModelStageSlotsV2('water', 'vertex')).toEqual([
            'vertexOffset',
            'waveAmplitude',
            'waveFrequency',
            'waveSpeed',
        ]);
        expect(resolveModelStageSlotsV2('foliage', 'vertex')).toEqual([
            'vertexOffset',
            'windStrength',
            'windSpeed',
            'windTurbulence',
        ]);
        expect(getMaterialModelDefinitionV2('foliage')?.passPlan[0]?.cullMode).toBe('none');
    });

    it('evaluates Render Profile capability matrices consistently', () => {
        expect(resolveMaterialModelCapability('hair', 'webgl2-standard')?.status).toBe('supported');
        expect(resolveMaterialModelCapability('toon', 'webgl2-minimal')?.status).toBe('unsupported');
        expect(resolveOutputSlotCapability('pbr', 'clearcoat', 'webgl2-standard')).toBe('supported');
        expect(resolveOutputSlotCapability('pbr', 'clearcoat', 'webgl2-minimal')).toBe('unsupported');
        expect(resolveOutputSlotCapability('pbr', 'baseColor', 'native')).toBe('partial');
        expect(resolveOutputSlotCapability('water', 'foamColor', 'native')).toBe('unsupported');
    });

    it('maps legacy and V2 shader IDs onto canonical Material Model IDs round-trip', () => {
        for (const model of BUILTIN_MATERIAL_MODELS_V2) {
            if (model.id === 'composable') {
                continue;
            }
            expect(resolveModelIdFromLegacyShaderId(model.defaultShaderId)).toBe(model.id);
            expect(resolveShaderIdFromModelId(model.id)).toBe(model.defaultShaderId);
        }
        expect(resolveModelIdFromLegacyShaderId('scene/particle-point')).toBe('particle');
    });
});

describe('material contract v2 json artifact', () => {
    it('matches serializeMaterialModelsV2() byte for byte', () => {
        const onDisk = fs.readFileSync(modelsV2JsonPath, 'utf8').replace(/\r\n/g, '\n');
        expect(onDisk).toBe(serializeMaterialModelsV2());
    });
});

describe('material contract v2 external uniforms', () => {
    it('passes structural validation of the engine-supplied uniform registry', () => {
        const validation = validateMaterialExternalUniformsV2();
        expect(validation.errors).toEqual([]);
        expect(validation.valid).toBe(true);
    });

    it('pins exactly five externals in a fixed order and partitions them 3 frame / 2 object', () => {
        expect(MATERIAL_EXTERNAL_UNIFORMS_V2).toHaveLength(5);
        expect(listMaterialExternalUniformNamesV2()).toEqual([
            '_RenderTargetSize',
            '_MainLightDirection',
            '_MainLightColor',
            '_ObjectBoundsCenter',
            '_ObjectBoundsSize',
        ]);
        expect(listMaterialExternalUniformsV2()).toBe(MATERIAL_EXTERNAL_UNIFORMS_V2);

        const frame = listMaterialExternalUniformsV2('frame');
        const object = listMaterialExternalUniformsV2('object');
        expect(frame.map((uniform) => uniform.name)).toEqual([
            '_RenderTargetSize',
            '_MainLightDirection',
            '_MainLightColor',
        ]);
        expect(object.map((uniform) => uniform.name)).toEqual([
            '_ObjectBoundsCenter',
            '_ObjectBoundsSize',
        ]);
        // The two scopes are an exact partition: nothing dropped, nothing double-counted.
        expect(frame.length + object.length).toBe(MATERIAL_EXTERNAL_UNIFORMS_V2.length);
        expect(getMaterialExternalUniformV2('_MainLightColor')?.scope).toBe('frame');
        expect(getMaterialExternalUniformV2('nope')).toBeUndefined();
    });

    it('names underscore-prefixed uniforms with unique semantics and non-empty stages', () => {
        const semantics = new Set<string>();
        for (const uniform of MATERIAL_EXTERNAL_UNIFORMS_V2) {
            expect(uniform.name.startsWith('_'), `name ${uniform.name}`).toBe(true);
            expect(uniform.stages.length, `stages of ${uniform.name}`).toBeGreaterThan(0);
            expect(uniform.label.length, `label of ${uniform.name}`).toBeGreaterThan(0);
            expect(uniform.description.length, `description of ${uniform.name}`).toBeGreaterThan(0);
            expect(
                semantics.has(uniform.semantic),
                `semantic ${uniform.semantic} must be unique`
            ).toBe(false);
            semantics.add(uniform.semantic);
        }
        expect(semantics.size).toBe(MATERIAL_EXTERNAL_UNIFORMS_V2.length);
    });

    it('reaches the vertex stage for the light and bounds inputs only', () => {
        // `_RenderTargetSize` is sampled purely in fragment code (screen-space
        // UV recovery), so it must NOT advertise a vertex stage; the four
        // geometry/lighting inputs are needed in the vertex stage too.
        const vertexCapable = MATERIAL_EXTERNAL_UNIFORMS_V2.filter((uniform) =>
            uniform.stages.includes('vertex')
        ).map((uniform) => uniform.name);

        expect(vertexCapable.sort()).toEqual(
            [
                '_MainLightColor',
                '_MainLightDirection',
                '_ObjectBoundsCenter',
                '_ObjectBoundsSize',
            ].sort()
        );
        expect(getMaterialExternalUniformV2('_RenderTargetSize')?.stages).toEqual(['fragment']);
    });

    it('pins the documented fallback values of every external uniform', () => {
        // A shader that samples an external before the provider has fed it reads
        // these defaults, so they are part of the contract, not documentation.
        const defaults = new Map(
            MATERIAL_EXTERNAL_UNIFORMS_V2.map((uniform) => [uniform.name, [...uniform.defaultValue]])
        );

        expect([...defaults.get('_RenderTargetSize')!]).toEqual([1, 1]);
        expect([...defaults.get('_MainLightDirection')!]).toEqual([0, 1, 0]);
        expect([...defaults.get('_MainLightColor')!]).toEqual([1, 1, 1]);
        expect([...defaults.get('_ObjectBoundsCenter')!]).toEqual([0, 0, 0]);
        expect([...defaults.get('_ObjectBoundsSize')!]).toEqual([1, 1, 1]);

        // The default width must match the declared component count, which is
        // what validateMaterialExternalUniformsV2() checks structurally.
        for (const uniform of MATERIAL_EXTERNAL_UNIFORMS_V2) {
            const expectedComponents = uniform.valueType === 'vec2' ? 2 : 3;
            expect(
                uniform.defaultValue.length,
                `default width of ${uniform.name}`
            ).toBe(expectedComponents);
        }
    });

    it('keeps the registry out of the model contract: no bindingKey collision', () => {
        // The models are the author-facing contract; the external registry is
        // engine-owned. A shared binding key would let an authored slot shadow
        // an engine value, so this must stay empty by construction.
        expect(BUILTIN_MATERIAL_MODELS_V2).toHaveLength(13);
        expect(MATERIAL_MODEL_REGISTRY_V2.size).toBe(13);

        const externalNames = new Set<string>(MATERIAL_EXTERNAL_UNIFORMS_V2.map((u) => u.name));
        const collisions: string[] = [];
        for (const model of BUILTIN_MATERIAL_MODELS_V2) {
            for (const slot of model.outputSlots) {
                if (slot.bindingKey && externalNames.has(slot.bindingKey)) {
                    collisions.push(`${model.id}:${slot.key} -> ${slot.bindingKey}`);
                }
            }
        }

        expect(collisions).toEqual([]);
        // Two-sided proof that the name map is real and not a wildcard.
        expect(isMaterialExternalUniformNameV2('_BaseColorFactor')).toBe(false);
        expect(isMaterialExternalUniformNameV2('_MainLightColor')).toBe(true);
        expect(isMaterialExternalUniformNameV2('MainLightColor')).toBe(false);
        expect(isMaterialExternalUniformNameV2('')).toBe(false);
    });
});
