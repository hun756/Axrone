import { describe, expect, it } from 'vitest';
import { Vec3 } from '@axrone/numeric';
import { ColorSpace, FilterMode, TextureFormat, WrapMode } from '@axrone/render-webgl2';
import type {
    GltfMaterialAsset,
    GltfMaterialTextureBinding,
    GltfTextureAsset,
    GltfTextureUsage,
} from '@axrone/asset-gltf';
import {
    createGltfTextureDefinitionFromTextureAsset,
    normalizeGltfMaterialDefinition,
} from '../internal/runtime-scene-assets';

const createSampler = () => ({
    id: 'sampler/default',
    minFilter: FilterMode.LINEAR,
    magFilter: FilterMode.LINEAR,
    wrapS: WrapMode.REPEAT,
    wrapT: WrapMode.REPEAT,
});

const createMinimalMaterialAsset = (
    overrides: Partial<GltfMaterialAsset> = {}
): GltfMaterialAsset => ({
    id: 'mat/test',
    materialIndex: 0,
    definition: {
        id: 'mat/test',
        shaderId: 'gltf/pbr',
        uniforms: {},
    },
    alphaMode: 'OPAQUE',
    alphaCutoff: 0.5,
    doubleSided: false,
    unlit: false,
    textures: {},
    ...overrides,
});

const createTextureAsset = (
    usageHints: readonly GltfTextureUsage[],
    payloadOverrides: Partial<GltfTextureAsset['payload']> = {},
    assetOverrides: Partial<GltfTextureAsset> = {}
): GltfTextureAsset => ({
    id: 'tex/test',
    textureIndex: 0,
    imageIndex: 0,
    sampler: createSampler(),
    payload: {
        kind: 'raw',
        bytes: new Uint8Array([255, 255, 255, 255]),
        mimeType: 'image/png',
        width: 1,
        height: 1,
        ...payloadOverrides,
    } as GltfTextureAsset['payload'],
    usageHints,
    runtimeFormat: TextureFormat.RGBA8,
    transcode: { status: 'source' },
    ...assetOverrides,
});

describe('runtime-scene-assets gaps', () => {
    describe('normalizeGltfMaterialDefinition', () => {
        it('produces a PBR material definition with cloned uniforms', () => {
            const asset = createMinimalMaterialAsset({
                definition: {
                    id: 'mat/pbr',
                    shaderId: 'gltf/pbr',
                    uniforms: {
                        _BaseColorFactor: [1, 0, 0, 1],
                        _MetallicFactor: 0.5,
                        _RoughnessFactor: 0.8,
                    },
                },
            });

            const result = normalizeGltfMaterialDefinition(asset, 'mat/pbr');

            expect(result.id).toBe('mat/pbr');
            expect(result.shaderId).toContain('gltf/pbr');
            expect(result.uniforms!['_MetallicFactor']).toBe(0.5);
            expect(result.uniforms!['_RoughnessFactor']).toBe(0.8);
            expect(result.surface).toBeDefined();
            expect(result.surface.shadingModel).toBe('pbr');
            expect(result.passes).toHaveLength(3);
        });

        it('produces an unlit surface for unlit materials', () => {
            const asset = createMinimalMaterialAsset({ unlit: true });

            const result = normalizeGltfMaterialDefinition(asset, 'mat/unlit');

            expect(result.surface.shadingModel).toBe('unlit');
        });

        it('maps alpha modes correctly', () => {
            const opaqueAsset = createMinimalMaterialAsset({ alphaMode: 'OPAQUE' });
            const opaqueResult = normalizeGltfMaterialDefinition(opaqueAsset, 'mat/opaque');
            expect(opaqueResult.surface.alphaMode).toBe('opaque');

            const blendAsset = createMinimalMaterialAsset({ alphaMode: 'BLEND' });
            const blendResult = normalizeGltfMaterialDefinition(blendAsset, 'mat/blend');
            expect(blendResult.surface.alphaMode).toBe('blend');

            const maskAsset = createMinimalMaterialAsset({ alphaMode: 'MASK' });
            const maskResult = normalizeGltfMaterialDefinition(maskAsset, 'mat/mask');
            expect(maskResult.surface.alphaMode).toBe('mask');
        });

        it('generates texture ST, Rotation, and TexCoord uniforms for all texture specs', () => {
            const binding: GltfMaterialTextureBinding = {
                textureKey: 'tex/base',
                usage: 'baseColor',
                texCoord: 0,
                colorSpace: 'srgb',
                transform: {
                    offset: [0.1, 0.2],
                    scale: [2, 3],
                    rotation: 0.5,
                    texCoord: 1,
                },
            };

            const asset = createMinimalMaterialAsset({
                textures: { baseColor: binding },
            });

            const result = normalizeGltfMaterialDefinition(asset, 'mat/transform');

            expect(result.uniforms!['_BaseColorTexture_ST']).toEqual([2, 3, 0.1, 0.2]);
            expect(result.uniforms!['_BaseColorTexture_Rotation']).toBe(0.5);
            expect(result.uniforms!['_BaseColorTexture_TexCoord']).toBe(1);
        });

        it('uses default ST values when no transform is present', () => {
            const binding: GltfMaterialTextureBinding = {
                textureKey: 'tex/base',
                usage: 'baseColor',
                texCoord: 0,
                colorSpace: 'srgb',
            };

            const asset = createMinimalMaterialAsset({
                textures: { baseColor: binding },
            });

            const result = normalizeGltfMaterialDefinition(asset, 'mat/default-st');

            expect(result.uniforms!['_BaseColorTexture_ST']).toEqual([1, 1, 0, 0]);
            expect(result.uniforms!['_BaseColorTexture_Rotation']).toBe(0);
            expect(result.uniforms!['_BaseColorTexture_TexCoord']).toBe(0);
        });

        it('injects default scale for normal textures and default strength for occlusion', () => {
            const asset = createMinimalMaterialAsset();

            const result = normalizeGltfMaterialDefinition(asset, 'mat/defaults');

            expect(result.uniforms!['_NormalTexture_Scale']).toBe(1);
            expect(result.uniforms!['_OcclusionTexture_Strength']).toBe(1);
        });

        it('does not override existing scale/strength uniforms', () => {
            const asset = createMinimalMaterialAsset({
                definition: {
                    id: 'mat/custom',
                    shaderId: 'gltf/pbr',
                    uniforms: {
                        _NormalTexture_Scale: 2.5,
                        _OcclusionTexture_Strength: 0.3,
                    },
                },
            });

            const result = normalizeGltfMaterialDefinition(asset, 'mat/custom');

            expect(result.uniforms!['_NormalTexture_Scale']).toBe(2.5);
            expect(result.uniforms!['_OcclusionTexture_Strength']).toBe(0.3);
        });

        it('activates clearcoat features when clearcoat factor > 0', () => {
            const asset = createMinimalMaterialAsset({
                definition: {
                    id: 'mat/clearcoat',
                    shaderId: 'gltf/pbr',
                    uniforms: { _ClearcoatFactor: 0.5 },
                },
            });

            const result = normalizeGltfMaterialDefinition(asset, 'mat/clearcoat');

            expect(result.surface.features.useClearcoat).toBe(true);
            expect(result.surface.clearcoat).toBe(0.5);
        });

        it('clones uniform values so mutations do not affect the source', () => {
            const sourceUniforms: Record<string, unknown> = {
                _BaseColorFactor: [1, 0, 0, 1],
                _MetallicFactor: 0.5,
            };
            const asset = createMinimalMaterialAsset({
                definition: {
                    id: 'mat/clone',
                    shaderId: 'gltf/pbr',
                    uniforms: sourceUniforms as GltfMaterialAsset['definition']['uniforms'],
                },
            });

            const result = normalizeGltfMaterialDefinition(asset, 'mat/clone');

            const resultArray = result.uniforms!['_BaseColorFactor'] as number[];
            resultArray[0] = 999;
            expect((sourceUniforms['_BaseColorFactor'] as number[])[0]).toBe(1);
        });

        it('clones Vec3 uniform values', () => {
            const vec = new Vec3(1, 2, 3);
            const asset = createMinimalMaterialAsset({
                definition: {
                    id: 'mat/vec3',
                    shaderId: 'gltf/pbr',
                    uniforms: { _CustomVec: vec } as unknown as GltfMaterialAsset['definition']['uniforms'],
                },
            });

            const result = normalizeGltfMaterialDefinition(asset, 'mat/vec3');

            const cloned = result.uniforms!['_CustomVec'];
            expect(cloned).toBeInstanceOf(Vec3);
            expect(cloned).not.toBe(vec);
            expect((cloned as Vec3).x).toBe(1);
        });
    });

    describe('createGltfTextureDefinitionFromTextureAsset (expanded)', () => {
        it('creates a url source for external payloads with loadable MIME', () => {
            const asset = createTextureAsset(['baseColor'], {
                kind: 'external',
                uri: 'https://cdn.example.com/tex.png',
            });

            const result = createGltfTextureDefinitionFromTextureAsset('tex/external', asset);

            expect(result.definition.source).toEqual({
                kind: 'url',
                url: 'https://cdn.example.com/tex.png',
            });
            expect(result.diagnostics).toHaveLength(0);
        });

        it('creates a fallback for raw payloads with non-loadable MIME type', () => {
            const asset = createTextureAsset(['baseColor'], {
                kind: 'raw',
                bytes: new Uint8Array([0]),
                mimeType: 'image/bmp',
            });

            const result = createGltfTextureDefinitionFromTextureAsset('tex/bmp', asset);

            expect(result.definition.source.kind).toBe('data');
            expect(result.diagnostics).toHaveLength(1);
            expect(result.diagnostics[0]!.code).toBe('gltf.texture.runtime-fallback');
        });

        it('creates a compressed source for compressed payloads with valid levels', () => {
            const asset = createTextureAsset(
                ['baseColor'],
                {
                    kind: 'compressed',
                    bytes: new Uint8Array([1, 2, 3, 4]),
                    container: 'ktx2',
                    levels: [
                        { level: 0, width: 4, height: 4, byteOffset: 0, byteLength: 16 },
                    ],
                    targetFormat: TextureFormat.RGBA8,
                },
                { runtimeFormat: TextureFormat.RGBA8 }
            );

            const result = createGltfTextureDefinitionFromTextureAsset('tex/compressed', asset);

            expect(result.definition.source.kind).toBe('compressed');
            expect(result.diagnostics).toHaveLength(0);
            expect(result.definition.format).toBe(TextureFormat.RGBA8);
        });

        it('creates a fallback for compressed payloads with missing format', () => {
            const asset = createTextureAsset(
                ['baseColor'],
                {
                    kind: 'compressed',
                    bytes: new Uint8Array([1, 2]),
                    container: 'basisu',
                },
                { runtimeFormat: undefined }
            );

            const result = createGltfTextureDefinitionFromTextureAsset('tex/noformat', asset);

            expect(result.definition.source.kind).toBe('data');
            expect(result.diagnostics).toHaveLength(1);
            expect(result.diagnostics[0]!.code).toBe('gltf.texture.runtime-format-missing');
        });

        it('synthesizes a single level from payload dimensions when levels are missing', () => {
            const asset = createTextureAsset(
                ['baseColor'],
                {
                    kind: 'compressed',
                    bytes: new Uint8Array([1, 2, 3, 4]),
                    container: 'ktx2',
                    width: 2,
                    height: 2,
                },
                { runtimeFormat: TextureFormat.RGBA8 }
            );

            // The KTX2 parse path will fail on invalid bytes, producing a fallback.
            // To test level synthesis, provide a valid runtimeFormat and levels
            // but no KTX2 container so the KTX2 branch is skipped.
            const nonKtx2Asset = createTextureAsset(
                ['baseColor'],
                {
                    kind: 'compressed',
                    bytes: new Uint8Array([1, 2, 3, 4]),
                    container: 'basisu',
                    width: 2,
                    height: 2,
                },
                { runtimeFormat: TextureFormat.RGBA8 }
            );

            const result = createGltfTextureDefinitionFromTextureAsset('tex/synth', nonKtx2Asset);

            expect(result.definition.source.kind).toBe('compressed');
            if (result.definition.source.kind === 'compressed') {
                expect(result.definition.source.levels).toHaveLength(1);
                expect(result.definition.source.levels[0]!.width).toBe(2);
                expect(result.definition.source.levels[0]!.height).toBe(2);
            }
            expect(result.diagnostics).toHaveLength(0);
        });

        it('infers MIME type from URI extension', () => {
            const pngAsset = createTextureAsset(['baseColor'], {
                kind: 'raw',
                bytes: new Uint8Array([255]),
                uri: 'textures/albedo.PNG',
            });
            const pngResult = createGltfTextureDefinitionFromTextureAsset('tex/png', pngAsset);
            expect(pngResult.definition.source.kind).toBe('bytes');

            const jpgAsset = createTextureAsset(['baseColor'], {
                kind: 'raw',
                bytes: new Uint8Array([255]),
                uri: 'textures/albedo.jpg',
            });
            const jpgResult = createGltfTextureDefinitionFromTextureAsset('tex/jpg', jpgAsset);
            expect(jpgResult.definition.source.kind).toBe('bytes');

            const webpAsset = createTextureAsset(['baseColor'], {
                kind: 'raw',
                bytes: new Uint8Array([255]),
                uri: 'textures/albedo.webp',
            });
            const webpResult = createGltfTextureDefinitionFromTextureAsset('tex/webp', webpAsset);
            expect(webpResult.definition.source.kind).toBe('bytes');
        });

        it('uses payload mimeType over URI when both are present', () => {
            const asset = createTextureAsset(['baseColor'], {
                kind: 'raw',
                bytes: new Uint8Array([255]),
                mimeType: 'image/png',
                uri: 'textures/albedo.unknown',
            });

            const result = createGltfTextureDefinitionFromTextureAsset('tex/precedence', asset);

            expect(result.definition.source.kind).toBe('bytes');
            expect(result.diagnostics).toHaveLength(0);
        });

        it('assigns sRGB color space for emissive textures', () => {
            const asset = createTextureAsset(['emissive']);

            const result = createGltfTextureDefinitionFromTextureAsset('tex/emissive', asset);

            expect(result.definition.colorSpace).toBe(ColorSpace.SRGB);
        });

        it('assigns linear color space for metallic-roughness textures', () => {
            const asset = createTextureAsset(['metallicRoughness']);

            const result = createGltfTextureDefinitionFromTextureAsset('tex/mr', asset);

            expect(result.definition.colorSpace).toBe(ColorSpace.LINEAR);
        });

        it('creates a normal-map fallback source with default normal color', () => {
            const asset = createTextureAsset(
                ['normal'],
                {
                    kind: 'compressed',
                    bytes: new Uint8Array([1, 2]),
                    container: 'basisu',
                },
                { runtimeFormat: undefined }
            );

            const result = createGltfTextureDefinitionFromTextureAsset('tex/normal-fallback', asset);

            expect(result.definition.source.kind).toBe('data');
            if (result.definition.source.kind === 'data') {
                expect(result.definition.source.data).toEqual([128, 128, 255, 255]);
            }
        });

        it('creates a fallback for KTX2 payloads with unsupported supercompression', () => {
            const KTX2_ID = [0xab, 0x4b, 0x54, 0x58, 0x20, 0x32, 0x30, 0xbb, 0x0d, 0x0a, 0x1a, 0x0a];
            const totalSize = 80 + 24 + 16;
            const ktx2Bytes = new Uint8Array(totalSize);
            ktx2Bytes.set(KTX2_ID, 0);
            const view = new DataView(ktx2Bytes.buffer);
            view.setUint32(20, 4, true);
            view.setUint32(24, 4, true);
            view.setUint32(28, 1, true);
            view.setUint32(32, 1, true);
            view.setUint32(36, 1, true);
            view.setUint32(40, 1, true);
            view.setUint32(44, 1, true);
            view.setUint32(80, 104, true);
            view.setUint32(88, 16, true);

            const asset = createTextureAsset(
                ['baseColor'],
                {
                    kind: 'compressed',
                    bytes: ktx2Bytes,
                    container: 'ktx2',
                },
                { runtimeFormat: undefined }
            );

            const result = createGltfTextureDefinitionFromTextureAsset('tex/supercompressed', asset);

            expect(result.definition.source.kind).toBe('data');
            expect(result.diagnostics).toHaveLength(1);
            expect(result.diagnostics[0]!.code).toBe('gltf.texture.runtime-supercompressed-unsupported');
        });

        it('creates a fallback for KTX2 payloads that fail to parse', () => {
            const asset = createTextureAsset(
                ['baseColor'],
                {
                    kind: 'compressed',
                    bytes: new Uint8Array([0, 1, 2, 3]),
                    container: 'ktx2',
                },
                { runtimeFormat: undefined }
            );

            const result = createGltfTextureDefinitionFromTextureAsset('tex/invalid-ktx2', asset);

            expect(result.definition.source.kind).toBe('data');
            expect(result.diagnostics).toHaveLength(1);
            expect(result.diagnostics[0]!.code).toBe('gltf.texture.runtime-ktx2-invalid');
        });

        it('infers MIME type from URI extension when payload has no explicit mimeType', () => {
            const jpegAsset = createTextureAsset(
                ['baseColor'],
                {
                    kind: 'raw',
                    bytes: new Uint8Array([0xff, 0xd8, 0xff]),
                    uri: 'textures/photo.jpeg',
                }
            );
            delete (jpegAsset.payload as any).mimeType;

            const result = createGltfTextureDefinitionFromTextureAsset('tex/jpeg-uri', jpegAsset);

            expect(result.definition.source.kind).toBe('bytes');
            if (result.definition.source.kind === 'bytes') {
                expect(result.definition.source.mimeType).toBe('image/jpeg');
            }
            expect(result.diagnostics).toHaveLength(0);
        });
    });
});
