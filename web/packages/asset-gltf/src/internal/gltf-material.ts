import type {
    GltfMaterialAlphaMode,
    GltfMaterialJson,
    GltfMaterialTextureBinding,
    GltfRootJson,
    GltfSamplerJson,
    GltfTextureBindingJson,
    GltfTextureJson,
    GltfTextureSampler,
    GltfTextureUsage,
} from '../types';
import type { GltfMaterialDefinition } from '../asset-ir';
import { GltfSchemaError } from '../errors';
import { EMPTY_ARRAY } from './gltf-constants';
import { mapMinFilter, mapMagFilter, mapWrapMode, createMaterialTextureBinding } from './gltf-texture';

export const collectTextureUsages = (root: GltfRootJson): Map<number, Set<GltfTextureUsage>> => {
    const usages = new Map<number, Set<GltfTextureUsage>>();
    const addUsage = (textureIndex: number | undefined, usage: GltfTextureUsage): void => {
        if (textureIndex === undefined) {
            return;
        }

        const set = usages.get(textureIndex) ?? new Set<GltfTextureUsage>();
        if (!usages.has(textureIndex)) {
            usages.set(textureIndex, set);
        }
        set.add(usage);
    };

    for (const material of root.materials ?? EMPTY_ARRAY) {
        addUsage(material.pbrMetallicRoughness?.baseColorTexture?.index, 'baseColor');
        addUsage(
            material.pbrMetallicRoughness?.metallicRoughnessTexture?.index,
            'metallicRoughness'
        );
        addUsage(material.normalTexture?.index, 'normal');
        addUsage(material.occlusionTexture?.index, 'occlusion');
        addUsage(material.emissiveTexture?.index, 'emissive');
        addUsage(material.extensions?.KHR_materials_clearcoat?.clearcoatTexture?.index, 'clearcoat');
        addUsage(
            material.extensions?.KHR_materials_clearcoat?.clearcoatRoughnessTexture?.index,
            'clearcoatRoughness'
        );
        addUsage(
            material.extensions?.KHR_materials_clearcoat?.clearcoatNormalTexture?.index,
            'clearcoatNormal'
        );
    }

    return usages;
};

export const createSamplerDefinition = (
    index: number | undefined,
    sampler: GltfSamplerJson | undefined,
    fallbackId: string
): GltfTextureSampler =>
    Object.freeze({
        id: index === undefined ? fallbackId : `gltf/sampler/${index}`,
        minFilter: mapMinFilter(sampler?.minFilter),
        magFilter: mapMagFilter(sampler?.magFilter),
        wrapS: mapWrapMode(sampler?.wrapS),
        wrapT: mapWrapMode(sampler?.wrapT),
    });

export const resolveTextureImageIndex = (texture: GltfTextureJson): number | undefined =>
    texture.extensions?.KHR_texture_basisu?.source ?? texture.source;

export const scaleEmissiveFactor = (
    value: readonly [number, number, number] | undefined,
    strength: number
): readonly [number, number, number] =>
    Object.freeze([
        (value?.[0] ?? 0) * strength,
        (value?.[1] ?? 0) * strength,
        (value?.[2] ?? 0) * strength,
    ]) as readonly [number, number, number];

export const createDefaultMaterialDefinition = (
    shaderId: string
): GltfMaterialDefinition =>
    Object.freeze({
        id: '',
        shaderId,
        uniforms: Object.freeze({
            _BaseColorFactor: Object.freeze([0.84, 0.84, 0.86, 1]),
            _MetallicFactor: 0.04,
            _RoughnessFactor: 0.94,
            _EmissiveFactor: Object.freeze([0, 0, 0]),
            _AlphaMode: 0,
            _AlphaCutoff: 0.5,
            _DoubleSided: 0,
        }),
        textures: Object.freeze({}),
    });

export interface MaterialDefinitionResult {
    readonly definition: GltfMaterialDefinition;
    readonly textures: Readonly<Record<GltfTextureUsage, GltfMaterialTextureBinding>>;
    readonly alphaMode: GltfMaterialAlphaMode;
    readonly alphaCutoff: number;
    readonly doubleSided: boolean;
    readonly unlit: boolean;
}

export const createMaterialDefinition = (
    material: GltfMaterialJson,
    shaderId: string,
    textureKeys: readonly string[]
): MaterialDefinitionResult => {
    const emissiveStrength =
        material.extensions?.KHR_materials_emissive_strength?.emissiveStrength ?? 1;
    const clearcoat = material.extensions?.KHR_materials_clearcoat;
    const uniforms: Record<string, number | readonly number[]> = {
        _BaseColorFactor: material.pbrMetallicRoughness?.baseColorFactor ?? [1, 1, 1, 1],
        _MetallicFactor: material.pbrMetallicRoughness?.metallicFactor ?? 1,
        _RoughnessFactor: material.pbrMetallicRoughness?.roughnessFactor ?? 1,
        _ClearcoatFactor: clearcoat?.clearcoatFactor ?? 0,
        _ClearcoatRoughnessFactor: clearcoat?.clearcoatRoughnessFactor ?? 0,
        _EmissiveFactor: scaleEmissiveFactor(material.emissiveFactor, emissiveStrength),
        _AlphaMode:
            material.alphaMode === 'MASK' ? 1 : material.alphaMode === 'BLEND' ? 2 : 0,
        _AlphaCutoff: material.alphaCutoff ?? 0.5,
        _DoubleSided: material.doubleSided ? 1 : 0,
    };
    const textureBindings: Record<string, string> = {};
    const textures: Partial<Record<GltfTextureUsage, GltfMaterialTextureBinding>> = {};

    const addTexture = (
        slot: GltfTextureUsage,
        source: GltfTextureBindingJson | undefined,
        uniformName: string,
        colorSpace: 'linear' | 'srgb'
    ): void => {
        if (!source) {
            return;
        }

        const textureKey = textureKeys[source.index];
        if (!textureKey) {
            throw new GltfSchemaError(
                `Material references missing texture ${source.index}`
            );
        }

        textureBindings[uniformName] = textureKey;
        const binding = createMaterialTextureBinding(slot, textureKey, source, colorSpace);
        textures[slot] = binding;
        if (binding.transform) {
            uniforms[`${uniformName}_ST`] = Object.freeze([
                binding.transform.scale[0],
                binding.transform.scale[1],
                binding.transform.offset[0],
                binding.transform.offset[1],
            ]);
            uniforms[`${uniformName}_Rotation`] = binding.transform.rotation;
            uniforms[`${uniformName}_TexCoord`] = binding.transform.texCoord;
        }
        if (binding.scale !== undefined) {
            uniforms[`${uniformName}_Scale`] = binding.scale;
        }
        if (binding.strength !== undefined) {
            uniforms[`${uniformName}_Strength`] = binding.strength;
        }
    };

    addTexture(
        'baseColor',
        material.pbrMetallicRoughness?.baseColorTexture,
        '_BaseColorTexture',
        'srgb'
    );
    addTexture(
        'metallicRoughness',
        material.pbrMetallicRoughness?.metallicRoughnessTexture,
        '_MetallicRoughnessTexture',
        'linear'
    );
    addTexture('normal', material.normalTexture, '_NormalTexture', 'linear');
    addTexture('occlusion', material.occlusionTexture, '_OcclusionTexture', 'linear');
    addTexture('emissive', material.emissiveTexture, '_EmissiveTexture', 'srgb');
    addTexture('clearcoat', clearcoat?.clearcoatTexture, '_ClearcoatTexture', 'linear');
    addTexture(
        'clearcoatRoughness',
        clearcoat?.clearcoatRoughnessTexture,
        '_ClearcoatRoughnessTexture',
        'linear'
    );
    addTexture(
        'clearcoatNormal',
        clearcoat?.clearcoatNormalTexture,
        '_ClearcoatNormalTexture',
        'linear'
    );

    const unlit = material.extensions?.KHR_materials_unlit !== undefined;

    return {
        definition: Object.freeze({
            id: '',
            shaderId: unlit ? 'gltf/unlit' : shaderId,
            uniforms: Object.freeze(uniforms),
            textures: Object.freeze(textureBindings),
        }),
        textures: Object.freeze(
            textures as Record<GltfTextureUsage, GltfMaterialTextureBinding>
        ),
        alphaMode: material.alphaMode ?? 'OPAQUE',
        alphaCutoff: material.alphaCutoff ?? 0.5,
        doubleSided: material.doubleSided ?? false,
        unlit,
    };
};
