import { describe, expect, it } from 'vitest';
import { SceneMaterialRegistry } from '../material-registry';
import type { SceneMaterialDefinition } from '../types';

const createPbrDefinition = (
    overrides: Partial<SceneMaterialDefinition> = {}
): SceneMaterialDefinition => ({
    id: 'test-material',
    shaderId: 'pbr',
    ...overrides,
});

describe('SceneMaterialRegistry — surface texture bridge', () => {
    it('bridges surface.albedoMap.textureId into _BaseColorTexture binding and TexCoord uniform', () => {
        const registry = new SceneMaterialRegistry();
        const definition = createPbrDefinition({
            surface: {
                albedoMap: { textureId: 'tex_albedo_01' },
            },
        });

        registry.create(definition);
        const resource = registry.get('test-material');

        expect(resource).toBeDefined();
        expect(resource!.textureBindings.has('_BaseColorTexture')).toBe(true);
        expect(resource!.textureBindings.get('_BaseColorTexture')!.textureId).toBe('tex_albedo_01');
        expect(resource!.uniforms.get('_BaseColorTexture_TexCoord')).toBe(0);
    });

    it('bridges all 8 surface map types to their corresponding uniform names', () => {
        const registry = new SceneMaterialRegistry();
        const definition = createPbrDefinition({
            surface: {
                albedoMap: { textureId: 'tex_albedo' },
                normalMap: { textureId: 'tex_normal' },
                metallicRoughnessMap: { textureId: 'tex_mr' },
                occlusionMap: { textureId: 'tex_ao' },
                emissiveMap: { textureId: 'tex_emissive' },
                clearcoatMap: { textureId: 'tex_clearcoat' },
                clearcoatRoughnessMap: { textureId: 'tex_clearcoat_rough' },
                clearcoatNormalMap: { textureId: 'tex_clearcoat_normal' },
            },
        });

        registry.create(definition);
        const resource = registry.get('test-material');

        expect(resource).toBeDefined();

        const expectedBindings: Array<[string, string]> = [
            ['_BaseColorTexture', 'tex_albedo'],
            ['_NormalTexture', 'tex_normal'],
            ['_MetallicRoughnessTexture', 'tex_mr'],
            ['_OcclusionTexture', 'tex_ao'],
            ['_EmissiveTexture', 'tex_emissive'],
            ['_ClearcoatTexture', 'tex_clearcoat'],
            ['_ClearcoatRoughnessTexture', 'tex_clearcoat_rough'],
            ['_ClearcoatNormalTexture', 'tex_clearcoat_normal'],
        ];

        for (const [uniformName, textureId] of expectedBindings) {
            expect(resource!.textureBindings.has(uniformName), `${uniformName} binding`).toBe(true);
            expect(resource!.textureBindings.get(uniformName)!.textureId).toBe(textureId);
            expect(resource!.uniforms.has(`${uniformName}_TexCoord`), `${uniformName}_TexCoord`).toBe(true);
        }
    });

    it('does not override explicit definition.textures bindings', () => {
        const registry = new SceneMaterialRegistry();
        const definition = createPbrDefinition({
            textures: {
                _BaseColorTexture: 'explicit_texture',
            },
            surface: {
                albedoMap: { textureId: 'surface_texture' },
            },
        });

        registry.create(definition);
        const resource = registry.get('test-material');

        expect(resource!.textureBindings.get('_BaseColorTexture')!.textureId).toBe('explicit_texture');
    });

    it('sets _ST uniform from surface map scale and offset', () => {
        const registry = new SceneMaterialRegistry();
        const definition = createPbrDefinition({
            surface: {
                albedoMap: {
                    textureId: 'tex_albedo',
                    scale: [2, 3],
                    offset: [0.5, 0.1],
                },
            },
        });

        registry.create(definition);
        const resource = registry.get('test-material');

        expect(resource!.uniforms.get('_BaseColorTexture_ST')).toEqual([2, 3, 0.5, 0.1]);
    });

    it('sets _Rotation uniform from surface map rotation', () => {
        const registry = new SceneMaterialRegistry();
        const definition = createPbrDefinition({
            surface: {
                normalMap: {
                    textureId: 'tex_normal',
                    rotation: 1.57,
                },
            },
        });

        registry.create(definition);
        const resource = registry.get('test-material');

        expect(resource!.uniforms.get('_NormalTexture_Rotation')).toBe(1.57);
    });

    it('uses explicit texCoord from surface map when provided', () => {
        const registry = new SceneMaterialRegistry();
        const definition = createPbrDefinition({
            surface: {
                albedoMap: {
                    textureId: 'tex_albedo',
                    texCoord: 1,
                },
            },
        });

        registry.create(definition);
        const resource = registry.get('test-material');

        expect(resource!.uniforms.get('_BaseColorTexture_TexCoord')).toBe(1);
    });

    it('skips surface maps with null or undefined textureId', () => {
        const registry = new SceneMaterialRegistry();
        const definition = createPbrDefinition({
            surface: {
                albedoMap: { textureId: null },
                normalMap: {},
            },
        });

        registry.create(definition);
        const resource = registry.get('test-material');

        expect(resource!.textureBindings.has('_BaseColorTexture')).toBe(false);
        expect(resource!.textureBindings.has('_NormalTexture')).toBe(false);
    });

    it('handles material with no surface definition', () => {
        const registry = new SceneMaterialRegistry();
        const definition = createPbrDefinition();

        expect(() => registry.create(definition)).not.toThrow();
        const resource = registry.get('test-material');
        expect(resource!.textureBindings.size).toBe(0);
    });

    it('bridges surface maps on cloned materials', () => {
        const registry = new SceneMaterialRegistry();
        const definition = createPbrDefinition({
            surface: {
                albedoMap: { textureId: 'tex_albedo' },
            },
        });

        registry.create(definition);
        registry.clone('test-material', 'cloned-material');
        const cloned = registry.get('cloned-material');

        expect(cloned).toBeDefined();
        expect(cloned!.textureBindings.has('_BaseColorTexture')).toBe(true);
        expect(cloned!.textureBindings.get('_BaseColorTexture')!.textureId).toBe('tex_albedo');
    });
});
