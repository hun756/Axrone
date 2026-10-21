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
                DefaultScene = 0,
                Scenes = ImmutableArray<GltfDocumentSceneAsset>.Empty,
                MeshKeys = ImmutableArray<string>.Empty,
                SkinKeys = ImmutableArray<string>.Empty,
                AnimationKeys = ImmutableArray<string>.Empty,
                MaterialKeys = materials.ConvertAll(static m => m.Id).ToImmutableArray(),
                TextureKeys = textures.ConvertAll(static t => t.Id).ToImmutableArray(),
                ExtensionsUsed = rawModel.ExtensionsUsed.ToImmutableArray(),
                ExtensionsRequired = rawModel.ExtensionsRequired.ToImmutableArray(),
                Stats = new GltfDocumentStats(0, 0, 0, 0, 0, 0, rawModel.Materials.Count, rawModel.Textures.Count, 0, 0),
            },
            Prefabs = ImmutableArray<GltfPrefabAsset>.Empty,
            Meshes = ImmutableArray<GltfMeshAsset>.Empty,
            Skins = ImmutableArray<GltfSkinAsset>.Empty,
            Animations = ImmutableArray<GltfAnimationClipAsset>.Empty,
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
}
