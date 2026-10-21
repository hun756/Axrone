namespace Axrone.AssetGltf;

/// <summary>Pipeline compile stage: canonical assets.</summary>
public static class GltfCompileExtensions
{
    /// <summary>Compiles a resolved pipeline into canonical assets.</summary>
    public static async ValueTask<GltfImportResult> CompileAsync<TDracoPolicy, TMeshoptPolicy, TTranscoderPolicy>(
        this GltfPipeline<StateResolved> pipeline,
        GltfImporterOptions options,
        CancellationToken cancellationToken = default)
        where TDracoPolicy : struct, IDracoMeshPolicy
        where TMeshoptPolicy : struct, IMeshoptCompressionPolicy
        where TTranscoderPolicy : struct, ITextureTranscoderPolicy
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(options);

        GltfRawModel rawModel = pipeline.RawModel!;
        GltfResourceRuntimeCache resourceRuntime = pipeline.ResourceRuntime!;
        List<GltfDiagnostic> diagnostics = pipeline.Diagnostics;

        List<GltfTextureAsset> textures = await CompileTexturesAsync<TMeshoptPolicy, TTranscoderPolicy>(
            rawModel, resourceRuntime, options, diagnostics, cancellationToken).ConfigureAwait(false);

        List<GltfMaterialAsset> materials = CompileMaterials(rawModel, options);

        (List<GltfMeshAsset> meshes, ImmutableArray<ImmutableArray<string>> meshKeysByMesh) = await CompileMeshesAsync<TDracoPolicy, TMeshoptPolicy>(
            rawModel, resourceRuntime, diagnostics, cancellationToken).ConfigureAwait(false);

        (List<GltfSkinAsset> skins, ImmutableDictionary<int, string> skinKeyMap) = await CompileSkinsAsync<TMeshoptPolicy>(
            rawModel, resourceRuntime, cancellationToken).ConfigureAwait(false);

        List<GltfAnimationClipAsset> animations = await CompileAnimationsAsync<TMeshoptPolicy>(
            rawModel, resourceRuntime, cancellationToken).ConfigureAwait(false);

        ImmutableArray<string> animIds = animations.ConvertAll(static a => a.Id).ToImmutableArray();

        var prefabs = ImmutableArray.CreateBuilder<GltfPrefabAsset>(Math.Max(1, rawModel.Scenes.Count));
        var sceneAssets = ImmutableArray.CreateBuilder<GltfDocumentSceneAsset>(rawModel.Scenes.Count);
        int defaultSceneIdx = rawModel.DefaultScene >= 0 && rawModel.DefaultScene < rawModel.Scenes.Count
            ? rawModel.DefaultScene
            : 0;

        for (int s = 0; s < rawModel.Scenes.Count; s++)
        {
            GltfPrefabAsset prefab = GltfPrefabEngine.BuildPrefab(
                s,
                rawModel,
                s == defaultSceneIdx,
                diagnostics,
                meshKeysByMesh,
                skinKeyMap,
                animIds);

            prefabs.Add(prefab);

            sceneAssets.Add(new GltfDocumentSceneAsset
            {
                SceneIndex = s,
                Name = rawModel.Scenes[s].Name ?? $"Scene_{s}",
                PrefabKey = prefab.Id,
                RootNodeIds = prefab.RootNodeIds,
                AnimationController = prefab.AnimationController,
            });
        }

        var stats = new GltfDocumentStats(
            rawModel.Scenes.Count,
            rawModel.Nodes.Count,
            rawModel.Cameras.Count,
            rawModel.Lights.Count,
            rawModel.Meshes.Count,
            meshes.Count,
            rawModel.Materials.Count,
            rawModel.Textures.Count,
            rawModel.Skins.Count,
            rawModel.Animations.Count);

        return new GltfImportResult
        {
            Document = new GltfDocumentAsset
            {
                Id = options.Id,
                Name = Path.GetFileNameWithoutExtension(options.Id),
                Format = pipeline.ContainerFormat,
                Version = rawModel.Version,
                Generator = rawModel.Generator,
                Copyright = rawModel.Copyright,
                DefaultScene = defaultSceneIdx,
                Scenes = sceneAssets.MoveToImmutable(),
                MeshKeys = meshes.ConvertAll(static m => m.Id).ToImmutableArray(),
                SkinKeys = skins.ConvertAll(static s => s.Id).ToImmutableArray(),
                AnimationKeys = animIds,
                MaterialKeys = materials.ConvertAll(static m => m.Id).ToImmutableArray(),
                TextureKeys = textures.ConvertAll(static t => t.Id).ToImmutableArray(),
                ExtensionsUsed = rawModel.ExtensionsUsed.ToImmutableArray(),
                ExtensionsRequired = rawModel.ExtensionsRequired.ToImmutableArray(),
                Stats = stats,
            },
            Prefabs = prefabs.MoveToImmutable(),
            Meshes = meshes.ToImmutableArray(),
            Skins = skins.ToImmutableArray(),
            Animations = animations.ToImmutableArray(),
            Materials = materials.ToImmutableArray(),
            Textures = textures.ToImmutableArray(),
            Diagnostics = diagnostics.ToImmutableArray(),
        };
    }

    internal static async ValueTask<List<GltfTextureAsset>> CompileTexturesAsync<TMeshoptPolicy, TTranscoderPolicy>(
        GltfRawModel rawModel,
        GltfResourceRuntimeCache resourceRuntime,
        GltfImporterOptions options,
        List<GltfDiagnostic> diagnostics,
        CancellationToken cancellationToken)
        where TMeshoptPolicy : struct, IMeshoptCompressionPolicy
        where TTranscoderPolicy : struct, ITextureTranscoderPolicy
    {
        var textures = new List<GltfTextureAsset>(rawModel.Textures.Count);
        for (int i = 0; i < rawModel.Textures.Count; i++)
        {
            GltfRawModel.Texture texDef = rawModel.Textures[i];
            GltfRawModel.Sampler sampDef = (texDef.Sampler.HasValue && texDef.Sampler.Value < rawModel.Samplers.Count)
                ? rawModel.Samplers[texDef.Sampler.Value]
                : new GltfRawModel.Sampler();

            var sampler = new GltfTextureSampler(sampDef.MagFilter, sampDef.MinFilter, sampDef.WrapS, sampDef.WrapT);
            GltfTexturePayload payload;
            GltfTextureFormat runtimeFormat = GltfTextureFormat.Unknown;
            var transcodeState = new GltfTextureTranscodeState(true, null, null, GltfTextureFormat.Unknown);

            if (texDef.Source.HasValue && texDef.Source.Value < rawModel.Images.Count)
            {
                GltfRawModel.Image img = rawModel.Images[texDef.Source.Value];
                if (img.BufferView.HasValue && img.BufferView.Value < rawModel.BufferViews.Count)
                {
                    ReadOnlyMemory<byte> imgBytes = await resourceRuntime.ResolveBufferViewAsync<TMeshoptPolicy>(
                        img.BufferView.Value, rawModel, cancellationToken).ConfigureAwait(false);

                    if (string.Equals(img.MimeType, "image/ktx2", StringComparison.OrdinalIgnoreCase))
                    {
                        if (Ktx2Decoder.TryParse(imgBytes, out GltfCompressedTexturePayload? compressed, out string? error))
                        {
                            payload = compressed;
                            runtimeFormat = Ktx2Decoder.MapVkFormat(compressed.VkFormat);

                            if (options.TranscodeTargetFormat != GltfTextureFormat.Unknown
                                && TTranscoderPolicy.CanTranscode(runtimeFormat, options.TranscodeTargetFormat))
                            {
                                if (TTranscoderPolicy.TryTranscode(imgBytes, runtimeFormat, options.TranscodeTargetFormat, out ReadOnlyMemory<byte> transcoded, out string? failureReason))
                                {
                                    transcodeState = new GltfTextureTranscodeState(true, TTranscoderPolicy.PolicyId, null, options.TranscodeTargetFormat);
                                    runtimeFormat = options.TranscodeTargetFormat;
                                    payload = new GltfRawTexturePayload(transcoded, "application/octet-stream");
                                }
                                else
                                {
                                    transcodeState = new GltfTextureTranscodeState(false, null, failureReason, options.TranscodeTargetFormat);
                                    diagnostics.Add(new GltfDiagnostic(GltfDiagnosticLevel.Warning, "gltf.texture.transcode.failed", failureReason!));
                                }
                            }
                        }
                        else
                        {
                            diagnostics.Add(new GltfDiagnostic(GltfDiagnosticLevel.Error, "gltf.texture.transcode.failed", error!));
                            payload = new GltfRawTexturePayload(imgBytes, img.MimeType ?? "application/octet-stream");
                        }
                    }
                    else
                    {
                        payload = new GltfRawTexturePayload(imgBytes, img.MimeType ?? "image/png");
                        runtimeFormat = GltfTextureFormat.R8G8B8A8_UNorm;
                    }
                }
                else if (!string.IsNullOrEmpty(img.Uri))
                {
                    payload = new GltfExternalTexturePayload(img.Uri);
                }
                else
                {
                    payload = new GltfRawTexturePayload(ReadOnlyMemory<byte>.Empty, "unknown");
                }
            }
            else
            {
                payload = new GltfRawTexturePayload(ReadOnlyMemory<byte>.Empty, "unknown");
            }

            textures.Add(new GltfTextureAsset
            {
                Id = $"gltf/texture/{i}",
                TextureIndex = i,
                ImageIndex = texDef.Source ?? -1,
                Sampler = sampler,
                Payload = payload,
                UsageHints = ImmutableArray<GltfTextureUsage>.Empty,
                RuntimeFormat = runtimeFormat,
                TranscodeState = transcodeState,
            });
        }

        return textures;
    }

    internal static List<GltfMaterialAsset> CompileMaterials(GltfRawModel rawModel, GltfImporterOptions options)
    {
        var materials = new List<GltfMaterialAsset>(rawModel.Materials.Count);
        for (int i = 0; i < rawModel.Materials.Count; i++)
        {
            GltfRawModel.Material mDef = rawModel.Materials[i];
            var floatUniforms = ImmutableDictionary.CreateBuilder<string, float[]>(StringComparer.Ordinal);
            var scalarUniforms = ImmutableDictionary.CreateBuilder<string, float>(StringComparer.Ordinal);
            var intUniforms = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
            var texBindings = ImmutableDictionary.CreateBuilder<GltfTextureUsage, GltfMaterialTextureBinding>();

            float[] baseColor = mDef.Pbr.BaseColorFactor ?? [0.84f, 0.84f, 0.86f, 1.0f];
            floatUniforms.Add("_BaseColorFactor", baseColor);
            scalarUniforms.Add("_MetallicFactor", mDef.Pbr.MetallicFactor);
            scalarUniforms.Add("_RoughnessFactor", mDef.Pbr.RoughnessFactor);
            scalarUniforms.Add("_ClearcoatFactor", mDef.ClearcoatFactor);
            scalarUniforms.Add("_ClearcoatRoughnessFactor", mDef.ClearcoatRoughnessFactor);

            float[] emissive = mDef.EmissiveFactor ?? [0.0f, 0.0f, 0.0f];
            if (mDef.EmissiveStrength != 1.0f)
            {
                emissive =
                [
                    emissive[0] * mDef.EmissiveStrength,
                    emissive[1] * mDef.EmissiveStrength,
                    emissive[2] * mDef.EmissiveStrength,
                ];
            }

            floatUniforms.Add("_EmissiveFactor", emissive);

            GltfAlphaMode alphaMode = mDef.AlphaMode.ToUpperInvariant() switch
            {
                "MASK" => GltfAlphaMode.Mask,
                "BLEND" => GltfAlphaMode.Blend,
                _ => GltfAlphaMode.Opaque,
            };
            intUniforms.Add("_AlphaMode", (int)alphaMode);
            scalarUniforms.Add("_AlphaCutoff", mDef.AlphaCutoff);
            intUniforms.Add("_DoubleSided", mDef.DoubleSided ? 1 : 0);

            BindTexture(texBindings, floatUniforms, scalarUniforms, intUniforms, GltfTextureUsage.BaseColor, mDef.Pbr.BaseColorTexture, "_BaseColorTexture");
            BindTexture(texBindings, floatUniforms, scalarUniforms, intUniforms, GltfTextureUsage.MetallicRoughness, mDef.Pbr.MetallicRoughnessTexture, "_MetallicRoughnessTexture");
            BindTexture(texBindings, floatUniforms, scalarUniforms, intUniforms, GltfTextureUsage.Normal, mDef.NormalTexture, "_NormalTexture");
            BindTexture(texBindings, floatUniforms, scalarUniforms, intUniforms, GltfTextureUsage.Occlusion, mDef.OcclusionTexture, "_OcclusionTexture");
            BindTexture(texBindings, floatUniforms, scalarUniforms, intUniforms, GltfTextureUsage.Emissive, mDef.EmissiveTexture, "_EmissiveTexture");
            BindTexture(texBindings, floatUniforms, scalarUniforms, intUniforms, GltfTextureUsage.Clearcoat, mDef.ClearcoatTexture, "_ClearcoatTexture");
            BindTexture(texBindings, floatUniforms, scalarUniforms, intUniforms, GltfTextureUsage.ClearcoatRoughness, mDef.ClearcoatRoughnessTexture, "_ClearcoatRoughnessTexture");
            BindTexture(texBindings, floatUniforms, scalarUniforms, intUniforms, GltfTextureUsage.ClearcoatNormal, mDef.ClearcoatNormalTexture, "_ClearcoatNormalTexture");

            var definition = new GltfMaterialDefinition
            {
                ShaderId = mDef.Unlit ? "gltf/unlit" : options.MaterialShaderId,
                FloatUniforms = floatUniforms.ToImmutable(),
                ScalarUniforms = scalarUniforms.ToImmutable(),
                IntUniforms = intUniforms.ToImmutable(),
            };

            materials.Add(new GltfMaterialAsset
            {
                Id = $"gltf/material/{i}",
                MaterialIndex = i,
                Definition = definition,
                AlphaMode = alphaMode,
                AlphaCutoff = mDef.AlphaCutoff,
                DoubleSided = mDef.DoubleSided,
                Unlit = mDef.Unlit,
                Textures = texBindings.ToImmutable(),
            });
        }

        return materials;
    }

    private static void BindTexture(
        ImmutableDictionary<GltfTextureUsage, GltfMaterialTextureBinding>.Builder texBindings,
        ImmutableDictionary<string, float[]>.Builder floatUniforms,
        ImmutableDictionary<string, float>.Builder scalarUniforms,
        ImmutableDictionary<string, int>.Builder intUniforms,
        GltfTextureUsage usage,
        GltfRawModel.TextureInfo? info,
        string uniformPrefix)
    {
        if (info == null || info.Index < 0)
        {
            return;
        }

        texBindings[usage] = new GltfMaterialTextureBinding
        {
            TextureKey = $"gltf/texture/{info.Index}",
            TexCoord = info.TexCoord,
            Scale = info.Scale,
            Strength = info.Strength,
            Transform = info.Transform,
        };

        floatUniforms.Add($"{uniformPrefix}_ST", [info.Transform.Scale.X, info.Transform.Scale.Y, info.Transform.Offset.X, info.Transform.Offset.Y]);
        scalarUniforms.Add($"{uniformPrefix}_Rotation", info.Transform.Rotation);
        intUniforms.Add($"{uniformPrefix}_TexCoord", info.Transform.TexCoord);
        scalarUniforms.Add($"{uniformPrefix}_Scale", info.Scale);
        scalarUniforms.Add($"{uniformPrefix}_Strength", info.Strength);
    }

    internal static async ValueTask<(List<GltfMeshAsset> Meshes, ImmutableArray<ImmutableArray<string>> MeshKeysByMesh)> CompileMeshesAsync<TDracoPolicy, TMeshoptPolicy>(
        GltfRawModel rawModel,
        GltfResourceRuntimeCache resourceRuntime,
        List<GltfDiagnostic> diagnostics,
        CancellationToken cancellationToken)
        where TDracoPolicy : struct, IDracoMeshPolicy
        where TMeshoptPolicy : struct, IMeshoptCompressionPolicy
    {
        var meshes = new List<GltfMeshAsset>();
        var meshKeysByMesh = ImmutableArray.CreateBuilder<ImmutableArray<string>>(rawModel.Meshes.Count);

        for (int m = 0; m < rawModel.Meshes.Count; m++)
        {
            GltfRawModel.Mesh meshDef = rawModel.Meshes[m];
            var primitiveKeys = ImmutableArray.CreateBuilder<string>(meshDef.Primitives.Count);

            for (int p = 0; p < meshDef.Primitives.Count; p++)
            {
                GltfRawModel.Primitive prim = meshDef.Primitives[p];
                string meshAssetId = GltfKeys.Mesh(m, p);
                primitiveKeys.Add(meshAssetId);

                int vertexCount = 0;
                var attrLayouts = ImmutableArray.CreateBuilder<GltfVertexAttribute>(prim.Attributes.Count);
                int totalInterleavedStride = 0;

                foreach (KeyValuePair<string, int> kv in prim.Attributes)
                {
                    if (kv.Value < 0 || kv.Value >= rawModel.Accessors.Count)
                    {
                        continue;
                    }

                    GltfRawModel.Accessor acc = rawModel.Accessors[kv.Value];
                    vertexCount = acc.Count;

                    if (!TryParseSemantic(kv.Key, out GltfMeshSemantic semantic))
                    {
                        diagnostics.Add(new GltfDiagnostic(GltfDiagnosticLevel.Warning, "gltf.mesh.attribute.unsupported", $"Attribute '{kv.Key}' bypassed."));
                        continue;
                    }

                    int compCount = GltfAccessorRuntime.GetTypeElementCount(acc.Type);
                    int compByteSize = GltfAccessorRuntime.GetComponentByteSize(acc.ComponentType);
                    int attrSize = compCount * compByteSize;

                    attrLayouts.Add(new GltfVertexAttribute(
                        totalInterleavedStride,
                        0,
                        acc.ComponentType,
                        semantic,
                        (byte)compCount,
                        acc.Normalized,
                        acc.ComponentType != 5126));

                    totalInterleavedStride += attrSize;
                }

                byte[] interleavedVertices = GC.AllocateUninitializedArray<byte>(vertexCount * totalInterleavedStride, pinned: true);
                GltfMeshBounds bounds = GltfMeshBounds.Empty;

                for (int a = 0; a < attrLayouts.Count; a++)
                {
                    GltfVertexAttribute layout = attrLayouts[a];
                    string semStr = GetSemanticString(layout.Semantic);
                    GltfRawModel.Accessor acc = rawModel.Accessors[prim.Attributes[semStr]];

                    int compSize = GltfAccessorRuntime.GetComponentByteSize(acc.ComponentType);
                    int elemSize = layout.ComponentCount * compSize;
                    var decodedFloats = new float[vertexCount * layout.ComponentCount];

                    if (prim.Draco != null && TDracoPolicy.IsSupported)
                    {
                        ReadOnlyMemory<byte> dracoBytes = await resourceRuntime.ResolveBufferViewAsync<TMeshoptPolicy>(
                            prim.Draco.BufferView, rawModel, cancellationToken).ConfigureAwait(false);

                        byte[] tempAttr = GC.AllocateUninitializedArray<byte>(vertexCount * elemSize, pinned: true);
                        bool decoded;
                        int dracoVertexCount;
                        {
                            ReadOnlySpan<byte> dracoSpan = dracoBytes.Span;
                            decoded = TDracoPolicy.TryDecodePrimitive(dracoSpan, layout.Semantic, acc.ComponentType, tempAttr, out dracoVertexCount);
                        }

                        if (decoded)
                        {
                            GltfAccessorRuntime.DecodeFloats(tempAttr, dracoVertexCount, acc.ComponentType, layout.ComponentCount, elemSize, acc.Normalized, decodedFloats);
                        }
                    }
                    else
                    {
                        ReadOnlyMemory<byte> viewBytes = await resourceRuntime.ResolveBufferViewAsync<TMeshoptPolicy>(
                            acc.BufferView, rawModel, cancellationToken).ConfigureAwait(false);

                        {
                            ReadOnlySpan<byte> srcSpan = viewBytes.Span.Slice(acc.ByteOffset);
                            int srcStride = rawModel.BufferViews[acc.BufferView].ByteStride;
                            GltfAccessorRuntime.DecodeFloats(srcSpan, vertexCount, acc.ComponentType, layout.ComponentCount, srcStride, acc.Normalized, decodedFloats);
                        }

                        if (acc.Sparse != null)
                        {
                            ReadOnlyMemory<byte> sparseIndexBytes = await resourceRuntime.ResolveBufferViewAsync<TMeshoptPolicy>(
                                acc.Sparse.Indices.BufferView, rawModel, cancellationToken).ConfigureAwait(false);
                            ReadOnlyMemory<byte> sparseValueBytes = await resourceRuntime.ResolveBufferViewAsync<TMeshoptPolicy>(
                                acc.Sparse.Values.BufferView, rawModel, cancellationToken).ConfigureAwait(false);

                            {
                                ReadOnlySpan<byte> idxSpan = sparseIndexBytes.Span.Slice(acc.Sparse.Indices.ByteOffset);
                                ReadOnlySpan<byte> valSpan = sparseValueBytes.Span.Slice(acc.Sparse.Values.ByteOffset);
                                GltfAccessorRuntime.MergeSparseFloats(
                                    idxSpan,
                                    acc.Sparse.Indices.ComponentType,
                                    valSpan,
                                    acc.Sparse.Count,
                                    acc.ComponentType,
                                    layout.ComponentCount,
                                    acc.Normalized,
                                    decodedFloats);
                            }
                        }
                    }

                    for (int v = 0; v < vertexCount; v++)
                    {
                        int dstOffset = (v * totalInterleavedStride) + layout.Offset;
                        for (int c = 0; c < layout.ComponentCount; c++)
                        {
                            float val = decodedFloats[(v * layout.ComponentCount) + c];
                            int compDst = dstOffset + (c * compSize);

                            switch (acc.ComponentType)
                            {
                                case 5126:
                                    BinaryPrimitives.WriteSingleLittleEndian(interleavedVertices.AsSpan(compDst, 4), val);
                                    break;
                                case 5120:
                                    interleavedVertices[compDst] = (byte)(sbyte)(layout.Normalized ? val * 127.0f : val);
                                    break;
                                case 5121:
                                    interleavedVertices[compDst] = (byte)(layout.Normalized ? val * 255.0f : val);
                                    break;
                                case 5122:
                                    BinaryPrimitives.WriteInt16LittleEndian(interleavedVertices.AsSpan(compDst, 2), (short)(layout.Normalized ? val * 32767.0f : val));
                                    break;
                                case 5123:
                                    BinaryPrimitives.WriteUInt16LittleEndian(interleavedVertices.AsSpan(compDst, 2), (ushort)(layout.Normalized ? val * 65535.0f : val));
                                    break;
                                case 5125:
                                    BinaryPrimitives.WriteUInt32LittleEndian(interleavedVertices.AsSpan(compDst, 4), (uint)(layout.Normalized ? val * 4294967295.0f : val));
                                    break;
                                default:
                                    break;
                            }
                        }
                    }

                    if (layout.Semantic == GltfMeshSemantic.Position)
                    {
                        bounds = GltfMeshPipeline.ComputeBounds(decodedFloats);
                    }
                }

                var morphTargetsBuilder = ImmutableArray.CreateBuilder<GltfMorphTargetDefinition>(prim.Targets.Count);
                for (int t = 0; t < prim.Targets.Count; t++)
                {
                    Dictionary<string, int> targetDict = prim.Targets[t];
                    foreach (KeyValuePair<string, int> kv in targetDict)
                    {
                        if (kv.Value < 0 || kv.Value >= rawModel.Accessors.Count)
                        {
                            continue;
                        }

                        GltfRawModel.Accessor tAcc = rawModel.Accessors[kv.Value];
                        GltfMorphTargetSemantic morphSemantic = kv.Key switch
                        {
                            "NORMAL" => GltfMorphTargetSemantic.Normal,
                            "TANGENT" => GltfMorphTargetSemantic.Tangent,
                            _ => GltfMorphTargetSemantic.Position,
                        };

                        ReadOnlyMemory<byte> targetBytes = await resourceRuntime.ResolveBufferViewAsync<TMeshoptPolicy>(
                            tAcc.BufferView, rawModel, cancellationToken).ConfigureAwait(false);
                        int targetCompCount = GltfAccessorRuntime.GetTypeElementCount(tAcc.Type);

                        var deltaFloats = new float[tAcc.Count * targetCompCount];
                        {
                            ReadOnlySpan<byte> targetSpan = targetBytes.Span.Slice(tAcc.ByteOffset);
                            GltfAccessorRuntime.DecodeFloats(targetSpan, tAcc.Count, tAcc.ComponentType, targetCompCount, rawModel.BufferViews[tAcc.BufferView].ByteStride, false, deltaFloats);
                        }

                        byte[] deltaBytes = GC.AllocateUninitializedArray<byte>(deltaFloats.Length * 4, pinned: true);
                        MemoryMarshal.Cast<float, byte>(deltaFloats).CopyTo(deltaBytes);

                        morphTargetsBuilder.Add(new GltfMorphTargetDefinition(
                            morphSemantic,
                            deltaBytes,
                            targetCompCount * 4,
                            targetCompCount));
                    }
                }

                uint[] expandedIndices;
                if (prim.Indices.HasValue && prim.Indices.Value >= 0 && prim.Indices.Value < rawModel.Accessors.Count)
                {
                    GltfRawModel.Accessor indexAcc = rawModel.Accessors[prim.Indices.Value];
                    ReadOnlyMemory<byte> indexBytes = await resourceRuntime.ResolveBufferViewAsync<TMeshoptPolicy>(
                        indexAcc.BufferView, rawModel, cancellationToken).ConfigureAwait(false);

                    var decodedRawIndices = new uint[indexAcc.Count];
                    {
                        ReadOnlySpan<byte> indexSpan = indexBytes.Span.Slice(indexAcc.ByteOffset);
                        GltfAccessorRuntime.DecodeIndices(indexSpan, indexAcc.Count, indexAcc.ComponentType, rawModel.BufferViews[indexAcc.BufferView].ByteStride, decodedRawIndices);
                    }

                    expandedIndices = GltfMeshPipeline.ExpandTopology(prim.Mode, decodedRawIndices, vertexCount);
                }
                else
                {
                    expandedIndices = GltfMeshPipeline.ExpandTopology(prim.Mode, ReadOnlySpan<uint>.Empty, vertexCount);
                }

                (ReadOnlyMemory<byte> downcastedBytes, bool is16Bit) = GltfMeshPipeline.DowncastIndices(expandedIndices);

                var finalAttributes = ImmutableArray.CreateBuilder<GltfVertexAttribute>(attrLayouts.Count);
                for (int a = 0; a < attrLayouts.Count; a++)
                {
                    GltfVertexAttribute la = attrLayouts[a];
                    finalAttributes.Add(new GltfVertexAttribute(la.Offset, totalInterleavedStride, la.ComponentType, la.Semantic, la.ComponentCount, la.Normalized, la.Integer));
                }

                var definition = new GltfMeshDefinition(
                    meshAssetId,
                    interleavedVertices,
                    finalAttributes.MoveToImmutable(),
                    morphTargetsBuilder.ToImmutable(),
                    downcastedBytes,
                    vertexCount,
                    expandedIndices.Length,
                    is16Bit,
                    prim.Mode == 0 ? GltfMeshTopology.Points : (prim.Mode is 1 or 2 or 3 ? GltfMeshTopology.Lines : GltfMeshTopology.Triangles));

                meshes.Add(new GltfMeshAsset
                {
                    Id = meshAssetId,
                    MeshIndex = m,
                    PrimitiveIndex = p,
                    Definition = definition,
                    Bounds = bounds,
                    MaterialKey = prim.Material.HasValue ? $"gltf/material/{prim.Material.Value}" : null,
                });
            }

            meshKeysByMesh.Add(primitiveKeys.MoveToImmutable());
        }

        return (meshes, meshKeysByMesh.MoveToImmutable());
    }

    private static bool TryParseSemantic(string name, out GltfMeshSemantic semantic)
    {
        switch (name)
        {
            case "POSITION": semantic = GltfMeshSemantic.Position; return true;
            case "NORMAL": semantic = GltfMeshSemantic.Normal; return true;
            case "TEXCOORD_0": semantic = GltfMeshSemantic.Uv0; return true;
            case "TEXCOORD_1": semantic = GltfMeshSemantic.Uv1; return true;
            case "TANGENT": semantic = GltfMeshSemantic.Tangent; return true;
            case "COLOR_0": semantic = GltfMeshSemantic.Color0; return true;
            case "JOINTS_0": semantic = GltfMeshSemantic.Joints0; return true;
            case "WEIGHTS_0": semantic = GltfMeshSemantic.Weights0; return true;
            default: semantic = default; return false;
        }
    }

    private static string GetSemanticString(GltfMeshSemantic semantic) =>
        semantic switch
        {
            GltfMeshSemantic.Position => "POSITION",
            GltfMeshSemantic.Normal => "NORMAL",
            GltfMeshSemantic.Uv0 => "TEXCOORD_0",
            GltfMeshSemantic.Uv1 => "TEXCOORD_1",
            GltfMeshSemantic.Tangent => "TANGENT",
            GltfMeshSemantic.Color0 => "COLOR_0",
            GltfMeshSemantic.Joints0 => "JOINTS_0",
            GltfMeshSemantic.Weights0 => "WEIGHTS_0",
            _ => string.Empty,
        };

    internal static async ValueTask<(List<GltfSkinAsset> Skins, ImmutableDictionary<int, string> SkinKeyMap)> CompileSkinsAsync<TMeshoptPolicy>(
        GltfRawModel rawModel,
        GltfResourceRuntimeCache resourceRuntime,
        CancellationToken cancellationToken)
        where TMeshoptPolicy : struct, IMeshoptCompressionPolicy
    {
        var skins = new List<GltfSkinAsset>(rawModel.Skins.Count);
        var skinKeyMap = ImmutableDictionary.CreateBuilder<int, string>();

        for (int s = 0; s < rawModel.Skins.Count; s++)
        {
            GltfRawModel.Skin skinDef = rawModel.Skins[s];
            string skinId = $"gltf/skin/{s}";
            skinKeyMap[s] = skinId;

            var jointIds = ImmutableArray.CreateBuilder<string>(skinDef.Joints.Count);
            for (int j = 0; j < skinDef.Joints.Count; j++)
            {
                jointIds.Add($"node_{skinDef.Joints[j]}_{rawModel.Nodes[skinDef.Joints[j]].Name ?? "unnamed"}");
            }

            ReadOnlyMemory<float> rowMajorIbm = ReadOnlyMemory<float>.Empty;
            if (skinDef.InverseBindMatrices.HasValue && skinDef.InverseBindMatrices.Value < rawModel.Accessors.Count)
            {
                GltfRawModel.Accessor ibmAcc = rawModel.Accessors[skinDef.InverseBindMatrices.Value];
                ReadOnlyMemory<byte> viewBytes = await resourceRuntime.ResolveBufferViewAsync<TMeshoptPolicy>(
                    ibmAcc.BufferView, rawModel, cancellationToken).ConfigureAwait(false);

                var rawFloats = new float[ibmAcc.Count * 16];
                {
                    ReadOnlySpan<byte> srcSpan = viewBytes.Span.Slice(ibmAcc.ByteOffset);
                    GltfAccessorRuntime.DecodeFloats(srcSpan, ibmAcc.Count, ibmAcc.ComponentType, 16, rawModel.BufferViews[ibmAcc.BufferView].ByteStride, false, rawFloats);
                }

                float[] transposed = GC.AllocateUninitializedArray<float>(rawFloats.Length, pinned: true);
                for (int mIdx = 0; mIdx < ibmAcc.Count; mIdx++)
                {
                    int baseIdx = mIdx * 16;
                    for (int r = 0; r < 4; r++)
                    {
                        for (int c = 0; c < 4; c++)
                        {
                            transposed[baseIdx + (r * 4) + c] = rawFloats[baseIdx + (c * 4) + r];
                        }
                    }
                }

                rowMajorIbm = transposed;
            }

            skins.Add(new GltfSkinAsset
            {
                Id = skinId,
                SkinIndex = s,
                JointNodeIds = jointIds.MoveToImmutable(),
                JointNodeIndices = skinDef.Joints.ToImmutableArray(),
                SkeletonNodeId = skinDef.Skeleton.HasValue ? $"node_{skinDef.Skeleton.Value}_{rawModel.Nodes[skinDef.Skeleton.Value].Name ?? "unnamed"}" : null,
                SkeletonNodeIndex = skinDef.Skeleton,
                InverseBindMatrices = rowMajorIbm,
            });
        }

        return (skins, skinKeyMap.ToImmutable());
    }

    internal static async ValueTask<List<GltfAnimationClipAsset>> CompileAnimationsAsync<TMeshoptPolicy>(
        GltfRawModel rawModel,
        GltfResourceRuntimeCache resourceRuntime,
        CancellationToken cancellationToken)
        where TMeshoptPolicy : struct, IMeshoptCompressionPolicy
    {
        var animations = new List<GltfAnimationClipAsset>(rawModel.Animations.Count);

        for (int a = 0; a < rawModel.Animations.Count; a++)
        {
            GltfRawModel.Animation animDef = rawModel.Animations[a];
            string animId = $"gltf/animation/{a}";

            var tracks = ImmutableArray.CreateBuilder<GltfAnimationTrackAsset>(animDef.Channels.Count);
            float maxDuration = 0.0f;

            for (int c = 0; c < animDef.Channels.Count; c++)
            {
                GltfRawModel.AnimationChannel chan = animDef.Channels[c];
                GltfRawModel.AnimationSampler samp = animDef.Samplers[chan.Sampler];

                GltfRawModel.Accessor inAcc = rawModel.Accessors[samp.Input];
                GltfRawModel.Accessor outAcc = rawModel.Accessors[samp.Output];

                ReadOnlyMemory<byte> inBytes = await resourceRuntime.ResolveBufferViewAsync<TMeshoptPolicy>(
                    inAcc.BufferView, rawModel, cancellationToken).ConfigureAwait(false);
                ReadOnlyMemory<byte> outBytes = await resourceRuntime.ResolveBufferViewAsync<TMeshoptPolicy>(
                    outAcc.BufferView, rawModel, cancellationToken).ConfigureAwait(false);

                var times = new float[inAcc.Count];
                {
                    ReadOnlySpan<byte> inSpan = inBytes.Span.Slice(inAcc.ByteOffset);
                    GltfAccessorRuntime.DecodeFloats(inSpan, inAcc.Count, inAcc.ComponentType, 1, rawModel.BufferViews[inAcc.BufferView].ByteStride, false, times);
                }

                if (times.Length > 0 && times[^1] > maxDuration)
                {
                    maxDuration = times[^1];
                }

                GltfAnimationPath path = chan.TargetPath.ToLowerInvariant() switch
                {
                    "rotation" => GltfAnimationPath.Rotation,
                    "scale" => GltfAnimationPath.Scale,
                    "weights" => GltfAnimationPath.Weights,
                    _ => GltfAnimationPath.Translation,
                };

                GltfInterpolation interp = samp.Interpolation.ToUpperInvariant() switch
                {
                    "STEP" => GltfInterpolation.Step,
                    "CUBICSPLINE" => GltfInterpolation.CubicSpline,
                    _ => GltfInterpolation.Linear,
                };

                int valCompCount = path switch
                {
                    GltfAnimationPath.Rotation => 4,
                    GltfAnimationPath.Weights => GltfAccessorRuntime.GetTypeElementCount(outAcc.Type),
                    _ => 3,
                };

                int sampleStride = interp == GltfInterpolation.CubicSpline ? 3 * valCompCount : valCompCount;
                var values = new float[inAcc.Count * sampleStride];
                {
                    ReadOnlySpan<byte> outSpan = outBytes.Span.Slice(outAcc.ByteOffset);
                    GltfAccessorRuntime.DecodeFloats(
                        outSpan,
                        inAcc.Count * (interp == GltfInterpolation.CubicSpline ? 3 : 1),
                        outAcc.ComponentType,
                        valCompCount,
                        rawModel.BufferViews[outAcc.BufferView].ByteStride,
                        outAcc.Normalized,
                        values);
                }

                tracks.Add(new GltfAnimationTrackAsset
                {
                    ChannelIndex = c,
                    SamplerIndex = chan.Sampler,
                    InputAccessor = samp.Input,
                    OutputAccessor = samp.Output,
                    TargetNodeIndex = chan.TargetNode,
                    TargetNodeId = chan.TargetNode >= 0 ? $"node_{chan.TargetNode}_{rawModel.Nodes[chan.TargetNode].Name ?? "unnamed"}" : string.Empty,
                    Path = path,
                    Interpolation = interp,
                    KeyframeCount = inAcc.Count,
                    ValueComponentCount = valCompCount,
                    SampleStride = sampleStride,
                    Times = times,
                    Values = values,
                });
            }

            animations.Add(new GltfAnimationClipAsset
            {
                Id = animId,
                AnimationIndex = a,
                Duration = maxDuration,
                Tracks = tracks.MoveToImmutable(),
            });
        }

        return animations;
    }
}

/// <summary>Convenience importer with default (null) compression policies.</summary>
public sealed class GltfImporter
{
    private readonly GltfImporterOptions _options;

    /// <summary>Creates an importer.</summary>
    public GltfImporter(GltfImporterOptions? options = null)
    {
        _options = options ?? new GltfImporterOptions();
    }

    /// <summary>Imports glTF or GLB bytes.</summary>
    public async ValueTask<GltfImportResult> ImportAsync(
        ReadOnlyMemory<byte> sourceBytes,
        CancellationToken cancellationToken = default)
    {
        GltfPipeline<StateUnparsed> pipeline = GltfPipeline<StateUnparsed>.Create(sourceBytes);
        GltfPipeline<StateSchemaParsed> parsed = pipeline.ParseSchema();
        GltfPipeline<StateResolved> resolved = parsed.ResolveResources(_options.ResourceResolver);

        return await resolved.CompileAsync<NoDracoCompressionPolicy, NoMeshoptCompressionPolicy, PassthroughTranscoderPolicy>(
            _options,
            cancellationToken).ConfigureAwait(false);
    }
}
