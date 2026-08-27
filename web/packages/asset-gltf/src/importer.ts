import { EMPTY_ARRAY, DEFAULT_SAMPLER_ID, DEFAULT_MATERIAL_KEY_SUFFIX, DEFAULT_MATERIAL_NAME, DEFAULT_DOCUMENT_NAME, isSupportedExtension } from './internal/gltf-constants';
import type { AnimationClipMetadataIndex } from './internal/gltf-animation-types';
import { maybeFreeze, sanitizeName, ensureArray, accessorComponentCount } from './internal/gltf-utils';
import { inferTextureFormat } from './internal/gltf-texture';
import type {
    AssetImportDiagnostic,
    AssetImportResult,
    AssetImportSource,
    AssetWriteInput,
} from './asset-contract';
import { GltfSchemaError } from './errors';
import { GltfAccessorRuntime } from './internal/accessor-runtime';
import {
    basenameOfUri,
    GltfResourceRuntime,
    inferFormatFromSource,
    isGltfPackageSource,
    normalizeGltfSource,
    stripExtension,
    type NormalizedGltfSource,
} from './internal/source-runtime';
import { isPlainObject } from '@axrone/utility';
import { buildMeshDefinition, collectPrimitiveDiagnostics } from './internal/mesh-runtime';
import type {
    GltfAccessorJson,
    GltfAnimationClipAsset,
    GltfAnimationJson,
    GltfAssetKind,
    GltfAssetSchema,
    GltfAssetSchemaLike,
    GltfDocumentAsset,
    GltfDocumentSceneAsset,
    GltfImporter,
    GltfImporterOptions,
    GltfMaterialAsset,
    GltfMaterialJson,
    GltfMeshAsset,
    GltfMeshJson,
    GltfSkinAsset,
    GltfSkinJson,
    GltfRootJson,
    GltfTextureAsset,
    GltfTextureUsage,
} from './types';

import {
    resolvePortableAnimationManifest,
    resolvePortableAnimationClipMetadataSources,
    exportMotionFeaturesFromTracks,
    createAnimationManifestDiagnostic,
} from './internal/gltf-animation-metadata';

import { collectTextureUsages, createSamplerDefinition, resolveTextureImageIndex, createDefaultMaterialDefinition, createMaterialDefinition } from './internal/gltf-material';

import { buildPrefabDefinition } from './internal/gltf-prefab';

import {
    GltfTextureTranscoderRegistry,
    createGltfTextureTranscodeStage,
    createPassthroughGltfTextureTranscoder,
} from './internal/gltf-texture-transcode';

import { nodeIdFromIndex } from './internal/gltf-scene-transform';

const writeAsset = <TSchema extends GltfAssetSchemaLike, TKind extends GltfAssetKind>(
    kind: TKind,
    stableKey: string,
    name: string,
    data: TSchema[TKind],
    dependencies?: readonly string[]
): AssetWriteInput<TSchema> =>
    Object.freeze({
        kind,
        stableKey,
        name,
        data,
        ...(dependencies ? { dependencies: Object.freeze(dependencies) } : {}),
    }) as AssetWriteInput<TSchema>;

const listUnsupportedExtensions = (
    extensions: readonly string[] | undefined
): readonly string[] =>
    Object.freeze(
        [...new Set(extensions?.filter((extension) => !isSupportedExtension(extension)) ?? [])].sort(
            (left, right) => left.localeCompare(right)
        )
    );

const assertSupportedRequiredExtensions = (root: GltfRootJson): void => {
    const unsupported = listUnsupportedExtensions(root.extensionsRequired);
    if (unsupported.length === 0) {
        return;
    }

    throw new GltfSchemaError(
        `Unsupported required glTF extensions: ${unsupported.join(', ')}`
    );
};

const collectExtensionDiagnostics = (root: GltfRootJson): readonly AssetImportDiagnostic[] => {
    const required = new Set(root.extensionsRequired ?? EMPTY_ARRAY);

    return Object.freeze(
        listUnsupportedExtensions(root.extensionsUsed)
            .filter((extension) => required.has(extension) === false)
            .map(
                (extension) =>
                    Object.freeze({
                        level: 'warning',
                        code: 'gltf.extension.unsupported',
                        message: `glTF extension ${extension} is not supported and related data may be ignored`,
                    } satisfies AssetImportDiagnostic)
            )
    );
};

const createDocumentName = (
    normalized: NormalizedGltfSource,
    explicitName: string | undefined
): string =>
    sanitizeName(
        explicitName ??
            normalized.json.scenes?.[normalized.json.scene ?? 0]?.name ??
            stripExtension(basenameOfUri(normalized.sourceUri)) ??
            normalized.json.asset.generator,
        DEFAULT_DOCUMENT_NAME
    );

const createSkinAsset = async (
    root: GltfRootJson,
    skinIndex: number,
    accessors: GltfAccessorRuntime,
    freeze: boolean
): Promise<GltfSkinAsset> => {
    const skin = root.skins?.[skinIndex];
    if (!skin) {
        throw new GltfSchemaError(`Missing skin ${skinIndex}`);
    }

    const jointNodeIds = skin.joints.map((jointIndex) => {
        if (!root.nodes?.[jointIndex]) {
            throw new GltfSchemaError(`Skin ${skinIndex} references a missing joint node ${jointIndex}`);
        }

        return nodeIdFromIndex(jointIndex);
    });

    if (skin.skeleton !== undefined && !root.nodes?.[skin.skeleton]) {
        throw new GltfSchemaError(`Skin ${skinIndex} references a missing skeleton node ${skin.skeleton}`);
    }

    let inverseBindMatrices: Float32Array | undefined;
    if (skin.inverseBindMatrices !== undefined) {
        const decoded = await accessors.decodeAccessor(skin.inverseBindMatrices);
        if (decoded.componentCount !== 16) {
            throw new GltfSchemaError(
                `Skin ${skinIndex} inverse bind matrices must use MAT4 accessors`
            );
        }

        if (decoded.count !== skin.joints.length) {
            throw new GltfSchemaError(
                `Skin ${skinIndex} inverse bind matrix count does not match its joints`
            );
        }

        inverseBindMatrices = new Float32Array(decoded.values.length);
        for (let matrixOffset = 0; matrixOffset < decoded.values.length; matrixOffset += 16) {
            inverseBindMatrices[matrixOffset + 0] = decoded.values[matrixOffset + 0]!;
            inverseBindMatrices[matrixOffset + 1] = decoded.values[matrixOffset + 4]!;
            inverseBindMatrices[matrixOffset + 2] = decoded.values[matrixOffset + 8]!;
            inverseBindMatrices[matrixOffset + 3] = decoded.values[matrixOffset + 12]!;
            inverseBindMatrices[matrixOffset + 4] = decoded.values[matrixOffset + 1]!;
            inverseBindMatrices[matrixOffset + 5] = decoded.values[matrixOffset + 5]!;
            inverseBindMatrices[matrixOffset + 6] = decoded.values[matrixOffset + 9]!;
            inverseBindMatrices[matrixOffset + 7] = decoded.values[matrixOffset + 13]!;
            inverseBindMatrices[matrixOffset + 8] = decoded.values[matrixOffset + 2]!;
            inverseBindMatrices[matrixOffset + 9] = decoded.values[matrixOffset + 6]!;
            inverseBindMatrices[matrixOffset + 10] = decoded.values[matrixOffset + 10]!;
            inverseBindMatrices[matrixOffset + 11] = decoded.values[matrixOffset + 14]!;
            inverseBindMatrices[matrixOffset + 12] = decoded.values[matrixOffset + 3]!;
            inverseBindMatrices[matrixOffset + 13] = decoded.values[matrixOffset + 7]!;
            inverseBindMatrices[matrixOffset + 14] = decoded.values[matrixOffset + 11]!;
            inverseBindMatrices[matrixOffset + 15] = decoded.values[matrixOffset + 15]!;
        }
    }

    return maybeFreeze(
        {
            id: sanitizeName(skin.name, `Skin ${skinIndex}`),
            skinIndex,
            jointNodeIds: Object.freeze(jointNodeIds),
            jointNodeIndices: Object.freeze([...skin.joints]),
            ...(skin.skeleton !== undefined
                ? {
                      skeletonNodeId: nodeIdFromIndex(skin.skeleton),
                      skeletonNodeIndex: skin.skeleton,
                  }
                : {}),
            ...(inverseBindMatrices ? { inverseBindMatrices } : {}),
        } satisfies GltfSkinAsset,
        freeze
    );
};

const createAnimationClipAsset = async (
    root: GltfRootJson,
    animationIndex: number,
    accessors: GltfAccessorRuntime,
    clipMetadataSources: AnimationClipMetadataIndex,
    diagnostics: AssetImportDiagnostic[],
    freeze: boolean
): Promise<GltfAnimationClipAsset> => {
    const animation = root.animations?.[animationIndex];
    if (!animation) {
        throw new GltfSchemaError(`Missing animation ${animationIndex}`);
    }

    const tracks: GltfAnimationClipAsset['tracks'][number][] = [];
    let duration = 0;

    for (let channelIndex = 0; channelIndex < animation.channels.length; channelIndex += 1) {
        const channel = animation.channels[channelIndex]!;
        const sampler = animation.samplers[channel.sampler];
        if (!sampler) {
            throw new GltfSchemaError(
                `Animation ${animationIndex} channel ${channelIndex} references a missing sampler`
            );
        }

        const targetNodeIndex = channel.target.node;
        if (targetNodeIndex === undefined) {
            throw new GltfSchemaError(
                `Animation ${animationIndex} channel ${channelIndex} is missing a target node`
            );
        }

        if (!root.nodes?.[targetNodeIndex]) {
            throw new GltfSchemaError(
                `Animation ${animationIndex} channel ${channelIndex} references a missing node ${targetNodeIndex}`
            );
        }

        const input = await accessors.decodeAccessor(sampler.input);
        if (input.componentCount !== 1) {
            throw new GltfSchemaError(
                `Animation ${animationIndex} sampler ${channel.sampler} input must use SCALAR accessors`
            );
        }

        const output = await accessors.decodeAccessor(sampler.output);
        const interpolation = sampler.interpolation ?? 'LINEAR';
        const keyframeCount = input.count;
        const sampleStride =
            keyframeCount > 0 ? output.values.length / keyframeCount : accessorComponentCount(
                root.accessors?.[sampler.output]?.type ?? 'SCALAR'
            );

        if (!Number.isFinite(sampleStride) || Number.isInteger(sampleStride) === false) {
            throw new GltfSchemaError(
                `Animation ${animationIndex} sampler ${channel.sampler} output does not align with its keyframe count`
            );
        }

        const valueComponentCount =
            interpolation === 'CUBICSPLINE' ? sampleStride / 3 : sampleStride;
        if (
            interpolation === 'CUBICSPLINE' &&
            (sampleStride % 3 !== 0 || Number.isInteger(valueComponentCount) === false)
        ) {
            throw new GltfSchemaError(
                `Animation ${animationIndex} sampler ${channel.sampler} CUBICSPLINE output must pack in-tangent, value, and out-tangent triplets`
            );
        }

        for (const time of input.values) {
            duration = Math.max(duration, time);
        }

        tracks.push(
            maybeFreeze(
                {
                    channelIndex,
                    samplerIndex: channel.sampler,
                    inputAccessor: sampler.input,
                    outputAccessor: sampler.output,
                    targetNodeIndex,
                    targetNodeId: nodeIdFromIndex(targetNodeIndex),
                    path: channel.target.path,
                    interpolation,
                    keyframeCount,
                    valueComponentCount,
                    sampleStride,
                    times: input.values,
                    values: output.values,
                },
                freeze
            )
        );
    }

    const clipId = sanitizeName(animation.name, `Animation ${animationIndex}`);
    const clipMetadata =
        clipMetadataSources.byId.get(clipId) ??
        clipMetadataSources.byAnimationIndex.get(animationIndex);
    const exportedFeatures = clipMetadata?.featureExport
        ? exportMotionFeaturesFromTracks(
              clipId,
              Object.freeze(tracks),
              duration,
              clipMetadata.featureExport,
              diagnostics,
              (message) => createAnimationManifestDiagnostic(message),
              freeze
          )
        : undefined;
    const features =
        (clipMetadata?.features || exportedFeatures)
            ? Object.freeze(
                  [
                      ...(clipMetadata?.features ?? []),
                      ...(exportedFeatures ?? []),
                  ].sort((left, right) => left.time - right.time)
              )
            : undefined;

    return maybeFreeze(
        {
            id: clipId,
            animationIndex,
            duration,
            ...(clipMetadata?.events ? { events: clipMetadata.events } : {}),
            ...(clipMetadata?.footContacts ? { footContacts: clipMetadata.footContacts } : {}),
            ...(clipMetadata?.tags ? { tags: clipMetadata.tags } : {}),
            ...(features ? { features } : {}),
            ...(clipMetadata?.compression ? { compression: clipMetadata.compression } : {}),
            ...(clipMetadata?.streaming ? { streaming: clipMetadata.streaming } : {}),
            tracks: Object.freeze(tracks),
        } satisfies GltfAnimationClipAsset,
        freeze
    );
};

export { GltfTextureTranscoderRegistry, createGltfTextureTranscodeStage, createPassthroughGltfTextureTranscoder };

interface GltfImportContext<TSchema extends GltfAssetSchemaLike> {
    readonly normalized: NormalizedGltfSource;
    readonly runtime: GltfResourceRuntime;
    readonly accessors: GltfAccessorRuntime;
    readonly diagnostics: AssetImportDiagnostic[];
    readonly animationManifest: ReturnType<typeof resolvePortableAnimationManifest>;
    readonly textureUsageMap: Map<number, Set<GltfTextureUsage>>;
    readonly clipMetadataSources: ReturnType<typeof resolvePortableAnimationClipMetadataSources>;
    readonly createSubKey: (suffix: string) => string;
    readonly freeze: boolean;
    readonly materialShaderId: string;
    readonly fallbackSamplerId: string;
    readonly additional: AssetWriteInput<TSchema>[];
}

const resolveTextureAssets = async <TSchema extends GltfAssetSchemaLike>(
    ctx: GltfImportContext<TSchema>,
    textureKeys: readonly string[]
): Promise<void> => {
    const explicitTextures = ctx.normalized.json.textures ?? EMPTY_ARRAY;
    for (let textureIndex = 0; textureIndex < explicitTextures.length; textureIndex += 1) {
        const texture = explicitTextures[textureIndex]!;
        const imageIndex = resolveTextureImageIndex(texture);
        if (imageIndex === undefined) {
            ctx.diagnostics.push({
                level: 'warning',
                code: 'gltf.texture.missing-source',
                message: `Texture ${textureIndex} does not declare an image source`,
            });
            continue;
        }

        const payload = await ctx.runtime.resolveImage(imageIndex);
        const sampler = createSamplerDefinition(
            texture.sampler,
            texture.sampler !== undefined
                ? ctx.normalized.json.samplers?.[texture.sampler]
                : undefined,
            ctx.fallbackSamplerId
        );
        const usageHints = Object.freeze([
            ...(ctx.textureUsageMap.get(textureIndex) ?? EMPTY_ARRAY),
        ]);
        const asset = maybeFreeze(
            {
                id: sanitizeName(texture.name, `Texture ${textureIndex}`),
                textureIndex,
                imageIndex,
                sampler,
                payload,
                usageHints,
                runtimeFormat: inferTextureFormat(payload),
                transcode: Object.freeze({
                    status: 'source',
                    targetFormat: inferTextureFormat(payload),
                }),
            } satisfies GltfTextureAsset,
            ctx.freeze
        );

        ctx.additional.push(
            writeAsset<TSchema, 'gltf.texture'>('gltf.texture', textureKeys[textureIndex], asset.id, asset as TSchema['gltf.texture'])
        );
    }
};

const resolveMaterialAssets = <TSchema extends GltfAssetSchemaLike>(
    ctx: GltfImportContext<TSchema>,
    explicitMaterials: readonly GltfMaterialJson[],
    materialKeys: readonly string[],
    textureKeys: readonly string[],
    defaultMaterialKey: string | undefined
): void => {
    const requiresDefaultMaterial = (ctx.normalized.json.meshes ?? EMPTY_ARRAY).some((mesh) =>
        mesh.primitives.some((primitive) => primitive.material === undefined)
    );
    if (requiresDefaultMaterial && !defaultMaterialKey) {
        return;
    }

    for (let materialIndex = 0; materialIndex < explicitMaterials.length; materialIndex += 1) {
        const material = explicitMaterials[materialIndex]!;
        const built = createMaterialDefinition(material, ctx.materialShaderId, textureKeys);
        const key = materialKeys[materialIndex]!;
        const asset = maybeFreeze(
            {
                id: sanitizeName(material.name, `Material ${materialIndex}`),
                materialIndex,
                definition: Object.freeze({
                    ...built.definition,
                    id: key,
                }),
                alphaMode: built.alphaMode,
                alphaCutoff: built.alphaCutoff,
                doubleSided: built.doubleSided,
                unlit: built.unlit,
                textures: built.textures,
            } satisfies GltfMaterialAsset,
            ctx.freeze
        );

        ctx.additional.push(
            writeAsset<TSchema, 'gltf.material'>('gltf.material', key, asset.id, asset as TSchema['gltf.material'],
                Object.values(asset.textures).map((binding) => binding.textureKey))
        );
    }
};

const resolveMeshAssets = async <TSchema extends GltfAssetSchemaLike>(
    ctx: GltfImportContext<TSchema>,
    explicitMeshes: readonly GltfMeshJson[],
    materialKeys: readonly string[],
    defaultMaterialKey: string | undefined,
    meshKeysByMesh: string[][],
    materialKeysByMesh: Array<Array<string | undefined>>
): Promise<void> => {
    for (let meshIndex = 0; meshIndex < explicitMeshes.length; meshIndex += 1) {
        const mesh = explicitMeshes[meshIndex]!;
        const primitiveKeys: string[] = [];
        const primitiveMaterialKeys: Array<string | undefined> = [];

        for (
            let primitiveIndex = 0;
            primitiveIndex < mesh.primitives.length;
            primitiveIndex += 1
        ) {
            const primitive = mesh.primitives[primitiveIndex]!;
            ctx.diagnostics.push(
                ...collectPrimitiveDiagnostics(primitive, meshIndex, primitiveIndex)
            );
            const built = await buildMeshDefinition(primitive, ctx.accessors, ctx.runtime);
            const key = String(
                ctx.createSubKey(`mesh/${meshIndex}/primitive/${primitiveIndex}`)
            );
            const resolvedMaterialKey =
                primitive.material !== undefined
                    ? materialKeys[primitive.material]
                    : defaultMaterialKey;
            const meshAsset = maybeFreeze(
                {
                    id: sanitizeName(
                        mesh.name,
                        `${sanitizeName(mesh.name, `Mesh ${meshIndex}`)} Primitive ${primitiveIndex}`
                    ),
                    meshIndex,
                    primitiveIndex,
                    definition: Object.freeze({
                        ...built.definition,
                        id: key,
                    }),
                    ...(built.bounds ? { bounds: built.bounds } : {}),
                    ...(resolvedMaterialKey ? { materialKey: resolvedMaterialKey } : {}),
                    ...(primitive.extras ? { extras: primitive.extras } : {}),
                } satisfies GltfMeshAsset,
                ctx.freeze
            );

            ctx.additional.push(
                writeAsset<TSchema, 'gltf.mesh'>('gltf.mesh', key, meshAsset.id, meshAsset as TSchema['gltf.mesh'],
                    resolvedMaterialKey ? [resolvedMaterialKey] : undefined)
            );
            primitiveKeys.push(key);
            primitiveMaterialKeys.push(resolvedMaterialKey);
        }

        meshKeysByMesh[meshIndex] = primitiveKeys;
        materialKeysByMesh[meshIndex] = primitiveMaterialKeys;
    }
};

const resolveSkinAssets = async <TSchema extends GltfAssetSchemaLike>(
    ctx: GltfImportContext<TSchema>,
    explicitSkins: readonly GltfSkinJson[],
    skinKeys: readonly string[],
    skinsByIndex: Array<GltfSkinAsset | undefined>
): Promise<void> => {
    for (let skinIndex = 0; skinIndex < explicitSkins.length; skinIndex += 1) {
        const key = skinKeys[skinIndex]!;
        const asset = await createSkinAsset(ctx.normalized.json, skinIndex, ctx.accessors, ctx.freeze);
        skinsByIndex[skinIndex] = asset;
        ctx.additional.push(
            writeAsset<TSchema, 'gltf.skin'>('gltf.skin', key, asset.id, asset as TSchema['gltf.skin'])
        );
    }
};

const resolveAnimationAssets = async <TSchema extends GltfAssetSchemaLike>(
    ctx: GltfImportContext<TSchema>,
    explicitAnimations: readonly GltfAnimationJson[],
    animationKeys: readonly string[],
    animationsByIndex: Array<GltfAnimationClipAsset | undefined>
): Promise<void> => {
    for (let animationIndex = 0; animationIndex < explicitAnimations.length; animationIndex += 1) {
        const key = animationKeys[animationIndex]!;
        const asset = await createAnimationClipAsset(
            ctx.normalized.json,
            animationIndex,
            ctx.accessors,
            ctx.clipMetadataSources,
            ctx.diagnostics,
            ctx.freeze
        );
        animationsByIndex[animationIndex] = asset;
        ctx.additional.push(
            writeAsset<TSchema, 'gltf.animation'>('gltf.animation', key, asset.id, asset as TSchema['gltf.animation'])
        );
    }
};

const buildSceneAndDocument = <TSchema extends GltfAssetSchemaLike>(
    ctx: GltfImportContext<TSchema>,
    meshKeysByMesh: readonly (readonly string[])[],
    materialKeysByMesh: readonly (readonly (string | undefined)[])[],
    skinsByIndex: readonly (GltfSkinAsset | undefined)[],
    skinKeys: readonly string[],
    animationsByIndex: readonly (GltfAnimationClipAsset | undefined)[],
    animationKeys: readonly string[],
    defaultMaterialKey: string | undefined,
    materialKeys: readonly string[],
    textureKeys: readonly string[],
    explicitMeshes: readonly GltfMeshJson[],
    explicitMaterials: readonly GltfMaterialJson[]
): {
    readonly document: GltfDocumentAsset;
    readonly sceneEntries: readonly GltfDocumentSceneAsset[];
} => {
    const scenes =
        ctx.normalized.json.scenes && ctx.normalized.json.scenes.length > 0
            ? ctx.normalized.json.scenes
            : Object.freeze([
                  Object.freeze({
                      name: 'Scene 0',
                      nodes: Object.freeze(
                          ensureArray(ctx.normalized.json.nodes).map((_, index) => index)
                      ),
                  }),
              ]);
    const defaultSceneIndex = Math.min(
        Math.max(ctx.normalized.json.scene ?? 0, 0),
        Math.max(0, scenes.length - 1)
    );
    const sceneEntries: GltfDocumentSceneAsset[] = [];

    for (let sceneIndex = 0; sceneIndex < scenes.length; sceneIndex += 1) {
        const built = buildPrefabDefinition({
            root: ctx.normalized.json,
            sceneIndex,
            defaultSceneIndex,
            meshKeysByMesh,
            materialKeysByMesh,
            skinsByIndex,
            skinKeysByIndex: skinKeys,
            animationsByIndex,
            animationKeysByIndex: animationKeys,
            manifest: ctx.animationManifest,
        });
        ctx.diagnostics.push(...built.diagnostics);
        const key = String(ctx.createSubKey(`scene/${sceneIndex}/prefab`));
        const asset = maybeFreeze(
            {
                id: sanitizeName(scenes[sceneIndex]?.name, `Scene ${sceneIndex}`),
                sceneIndex,
                definition: Object.freeze({
                    ...built.prefab,
                    id: key,
                }),
                rootNodeIds: built.rootNodeIds,
                nodeIds: built.nodeIds,
                meshKeys: built.meshKeys,
                skinKeys: built.skinKeys,
                animationKeys: built.animationKeys,
                materialKeys: built.materialKeys,
                ...(built.animationController
                    ? { animationController: built.animationController }
                    : {}),
            },
            ctx.freeze
        );

        ctx.additional.push(
            writeAsset<TSchema, 'gltf.prefab'>('gltf.prefab', key, asset.id, asset as TSchema['gltf.prefab'],
                [...built.meshKeys, ...built.skinKeys, ...built.animationKeys, ...built.materialKeys])
        );
        sceneEntries.push(
            maybeFreeze(
                {
                    sceneIndex,
                    name: asset.id,
                    prefabKey: key,
                    rootNodeIds: built.rootNodeIds,
                    ...(built.animationController
                        ? { animationController: built.animationController }
                        : {}),
                } satisfies GltfDocumentSceneAsset,
                ctx.freeze
            )
        );
    }

    const documentName = createDocumentName(ctx.normalized, undefined);
    const document = maybeFreeze(
        {
            id: documentName,
            uri: ctx.normalized.sourceUri,
            name: documentName,
            format: ctx.normalized.format,
            version: ctx.normalized.json.asset.version,
            ...(ctx.normalized.json.asset.generator
                ? { generator: ctx.normalized.json.asset.generator }
                : {}),
            ...(ctx.normalized.json.asset.copyright
                ? { copyright: ctx.normalized.json.asset.copyright }
                : {}),
            defaultScene: defaultSceneIndex,
            scenes: Object.freeze(sceneEntries),
            meshKeys: Object.freeze(meshKeysByMesh.flat()),
            skinKeys: Object.freeze([...skinKeys]),
            animationKeys: Object.freeze([...animationKeys]),
            materialKeys: Object.freeze(
                [
                    ...(defaultMaterialKey ? [defaultMaterialKey] : EMPTY_ARRAY),
                    ...materialKeys,
                ].filter((value): value is string => Boolean(value))
            ),
            textureKeys: Object.freeze(textureKeys.filter(Boolean)),
            extensionsUsed: Object.freeze([
                ...(ctx.normalized.json.extensionsUsed ?? EMPTY_ARRAY),
            ]),
            extensionsRequired: Object.freeze([
                ...(ctx.normalized.json.extensionsRequired ?? EMPTY_ARRAY),
            ]),
            stats: Object.freeze({
                sceneCount: sceneEntries.length,
                nodeCount: ensureArray(ctx.normalized.json.nodes).length,
                cameraCount: ensureArray(ctx.normalized.json.cameras).length,
                lightCount:
                    ensureArray(ctx.normalized.json.extensions?.KHR_lights_punctual?.lights)
                        .length,
                meshCount: explicitMeshes.length,
                primitiveCount: meshKeysByMesh.reduce(
                    (total, entries) => total + entries.length,
                    0
                ),
                materialCount:
                    explicitMaterials.length + (defaultMaterialKey ? 1 : 0),
                textureCount: textureKeys.length,
                skinCount: ensureArray(ctx.normalized.json.skins).length,
                animationCount: ensureArray(ctx.normalized.json.animations).length,
            }),
        } satisfies GltfDocumentAsset,
        ctx.freeze
    );

    return { document, sceneEntries };
};

export const createGltfImporter = <
    TSchema extends GltfAssetSchemaLike = GltfAssetSchema,
>(
    options: GltfImporterOptions<TSchema> = {}
): GltfImporter<TSchema> => {
    const freeze = options.freeze !== false;
    const materialShaderId = options.materialShaderId ?? 'gltf/pbr';
    const fallbackSamplerId = options.defaultSamplerId ?? DEFAULT_SAMPLER_ID;

    const importer: GltfImporter<TSchema> = {
        id: options.id ?? 'asset.gltf',
        sourceKinds: ['bytes', 'text', 'json', 'custom'],
        extensions: ['gltf', 'glb'],
        mimeTypes: ['model/gltf+json', 'model/gltf-binary', 'application/json'],
        canImport: (context: Readonly<{ source: AssetImportSource }>) => {
            const { source } = context;
            if (isGltfPackageSource(source)) {
                return true;
            }

            if (source.kind === 'json') {
                return isPlainObject(source.data) && isPlainObject(source.data.asset);
            }

            if (source.kind === 'text') {
                return source.data.trimStart().startsWith('{');
            }

            if (source.kind === 'bytes') {
                const inferred = inferFormatFromSource(source);
                return inferred === 'glb' || inferred === 'gltf';
            }

            return false;
        },
        import: async (
            context: Readonly<{
                source: AssetImportSource;
                createSubKey: (suffix: string) => string;
            }>
        ) => {
            const { source, createSubKey } = context;
            const normalized = normalizeGltfSource(source);
            assertSupportedRequiredExtensions(normalized.json);
            const runtime = new GltfResourceRuntime(
                normalized,
                source,
                options.resourceResolver,
                options.dracoDecoder
            );
            const accessors = new GltfAccessorRuntime(runtime);
            const diagnostics: AssetImportDiagnostic[] = [
                ...collectExtensionDiagnostics(normalized.json),
            ];
            const animationManifest = resolvePortableAnimationManifest(normalized, diagnostics);
            const textureUsageMap = collectTextureUsages(normalized.json);
            const explicitTextures = normalized.json.textures ?? EMPTY_ARRAY;
            const explicitMaterials = normalized.json.materials ?? EMPTY_ARRAY;
            const explicitMeshes = normalized.json.meshes ?? EMPTY_ARRAY;
            const explicitSkins = normalized.json.skins ?? EMPTY_ARRAY;
            const explicitAnimations = normalized.json.animations ?? EMPTY_ARRAY;
            const clipMetadataSources = resolvePortableAnimationClipMetadataSources(
                animationManifest,
                diagnostics,
                freeze
            );
            const textureKeys = explicitTextures.map((_, index) =>
                String(createSubKey(`texture/${index}`))
            );
            const materialKeys = explicitMaterials.map((_, index) =>
                String(createSubKey(`material/${index}`))
            );
            const skinKeys = explicitSkins.map((_, index) => String(createSubKey(`skin/${index}`)));
            const animationKeys = explicitAnimations.map((_, index) =>
                String(createSubKey(`animation/${index}`))
            );
            const meshKeysByMesh: string[][] = [];
            const materialKeysByMesh: Array<Array<string | undefined>> = [];
            const skinsByIndex: Array<GltfSkinAsset | undefined> = [];
            const animationsByIndex: Array<GltfAnimationClipAsset | undefined> = [];
            const additional: AssetWriteInput<TSchema>[] = [];

            const ctx: GltfImportContext<TSchema> = {
                normalized,
                runtime,
                accessors,
                diagnostics,
                animationManifest,
                textureUsageMap,
                clipMetadataSources,
                createSubKey,
                freeze,
                materialShaderId,
                fallbackSamplerId,
                additional,
            };

            // Phase 1: Resolve texture assets
            await resolveTextureAssets(ctx, textureKeys);

            // Phase 2: Resolve default material and material assets
            let defaultMaterialKey: string | undefined;
            const requiresDefaultMaterial = explicitMeshes.some((mesh) =>
                mesh.primitives.some((primitive) => primitive.material === undefined)
            );
            if (requiresDefaultMaterial) {
                defaultMaterialKey = String(createSubKey(DEFAULT_MATERIAL_KEY_SUFFIX));
                const definition = createDefaultMaterialDefinition(materialShaderId);
                const asset = maybeFreeze(
                    {
                        id: DEFAULT_MATERIAL_NAME,
                        materialIndex: -1,
                        definition: Object.freeze({
                            ...definition,
                            id: defaultMaterialKey,
                        }),
                        alphaMode: 'OPAQUE',
                        alphaCutoff: 0.5,
                        doubleSided: false,
                        unlit: false,
                        textures: Object.freeze({}),
                    } satisfies GltfMaterialAsset,
                    freeze
                );
                additional.push(
                    writeAsset<TSchema, 'gltf.material'>('gltf.material', defaultMaterialKey, asset.id, asset as TSchema['gltf.material'])
                );
            }
            resolveMaterialAssets(ctx, explicitMaterials, materialKeys, textureKeys, defaultMaterialKey);

            // Phase 3: Resolve mesh assets
            await resolveMeshAssets(ctx, explicitMeshes, materialKeys, defaultMaterialKey, meshKeysByMesh, materialKeysByMesh);

            // Phase 4: Resolve skin assets
            await resolveSkinAssets(ctx, explicitSkins, skinKeys, skinsByIndex);

            // Phase 5: Resolve animation assets
            await resolveAnimationAssets(ctx, explicitAnimations, animationKeys, animationsByIndex);

            // Phase 6: Build scene prefabs and document
            const { document, sceneEntries } = buildSceneAndDocument(
                ctx,
                meshKeysByMesh,
                materialKeysByMesh,
                skinsByIndex,
                skinKeys,
                animationsByIndex,
                animationKeys,
                defaultMaterialKey,
                materialKeys,
                textureKeys,
                explicitMeshes,
                explicitMaterials
            );

            return Object.freeze({
                primary: writeAsset<TSchema, 'gltf.document'>('gltf.document', String(createSubKey('document')), document.name, document as TSchema['gltf.document'],
                    [...document.textureKeys, ...document.materialKeys, ...document.meshKeys, ...document.skinKeys, ...document.animationKeys, ...sceneEntries.map((scene) => scene.prefabKey)]),
                additional: Object.freeze(additional),
                diagnostics: Object.freeze(diagnostics),
            }) as AssetImportResult<TSchema>;
        },
    };

    return importer;
};
