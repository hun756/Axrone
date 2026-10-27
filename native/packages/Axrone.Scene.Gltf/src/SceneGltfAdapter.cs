namespace Axrone.Scene.Gltf;

/// <summary>Stable glTF adapter error codes.</summary>
public static class SceneGltfErrorCodes
{
    /// <summary>The import result is malformed for scene conversion.</summary>
    public const string Invalid = "scene.gltf.invalid";
}

/// <summary>glTF adapter failure.</summary>
public sealed class SceneGltfException : SceneException
{
    /// <summary>Creates a glTF adapter failure.</summary>
    public SceneGltfException(string code, string message, Exception? inner = null)
        : base(code, message, inner)
    {
    }
}

/// <summary>One conversion diagnostic for unsupported or lossy glTF data.</summary>
public readonly record struct SceneGltfDiagnostic(string Code, string Message);

/// <summary>Conversion settings for <see cref="SceneGltfAdapter"/>.</summary>
public sealed record SceneGltfImportOptions
{
    /// <summary>Shader assigned to every imported material.</summary>
    public SceneAssetId<ShaderAssetTag> PbrShaderId { get; init; } =
        SceneAssetId<ShaderAssetTag>.FromName("gltf/pbr");

    /// <summary>Shader assigned to materials marked unlit; defaults to the PBR shader.</summary>
    public SceneAssetId<ShaderAssetTag>? UnlitShaderId { get; init; }

    /// <summary>Prefix applied to every imported asset name.</summary>
    public string AssetPrefix { get; init; } = "gltf/";

    /// <summary>Optional table used to intern asset names so snapshots stay serializable.</summary>
    public SceneNameTable? Names { get; init; }

    /// <summary>Shader definitions copied into the snapshot.</summary>
    public IReadOnlyList<SceneShaderDefinition>? Shaders { get; init; }

    /// <summary>Render-pass definitions copied into the snapshot.</summary>
    public IReadOnlyList<SceneRenderPassDefinition>? RenderPasses { get; init; }
}

/// <summary>Converted snapshot plus conversion diagnostics.</summary>
public sealed class SceneGltfImportResult
{
    /// <summary>Creates a conversion result.</summary>
    public SceneGltfImportResult(
        SceneSnapshot snapshot,
        IReadOnlyList<SceneGltfDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(diagnostics);

        Snapshot = snapshot;
        Diagnostics = [.. diagnostics];
    }

    /// <summary>Snapshot ready for <c>SceneSnapshotLoader</c>.</summary>
    public SceneSnapshot Snapshot { get; }

    /// <summary>Unsupported or lossy features encountered during conversion.</summary>
    public IReadOnlyList<SceneGltfDiagnostic> Diagnostics { get; }
}

/// <summary>
/// Converts an <see cref="GltfImportResult"/> into a scene snapshot: meshes,
/// materials, textures, samplers and a prefab with transform/camera/light/mesh
/// components. Features without native scene equivalents (morph targets, skins,
/// animations, compressed textures) are reported as diagnostics instead of
/// failing the import.
/// </summary>
public static class SceneGltfAdapter
{
    private const string Transform3DComponent = "Transform3D";
    private const string CameraComponent = "Camera";
    private const string MeshRendererComponent = "MeshRenderer";
    private const string DirectionalLightComponent = "DirectionalLight";
    private const string PointLightComponent = "PointLight";
    private const string SpotLightComponent = "SpotLight";

    /// <summary>Converts a glTF import result into a scene snapshot.</summary>
    public static SceneGltfImportResult Import(
        GltfImportResult result,
        SceneGltfImportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        options ??= new SceneGltfImportOptions();

        if (options.Names is { } names)
        {
            options = options with
            {
                PbrShaderId = InternDefaultShader(options.PbrShaderId, names, "gltf/pbr"),
                UnlitShaderId = options.UnlitShaderId is { } unlit
                    ? InternDefaultShader(unlit, names, "gltf/unlit")
                    : null
            };
        }

        var diagnostics = new List<SceneGltfDiagnostic>();
        var meshes = ConvertMeshes(result.Meshes, options, diagnostics);
        var (textures, samplers) = ConvertTextures(result.Textures, options, diagnostics);
        var materials = ConvertMaterials(result.Materials, options, diagnostics);
        ScenePrefabDefinition prefab = ConvertPrefab(result, options, diagnostics);

        var snapshot = new SceneSnapshot(
            prefab,
            options.Shaders ?? [],
            meshes,
            materials,
            textures,
            samplers,
            options.RenderPasses ?? []);

        return new SceneGltfImportResult(snapshot, diagnostics);
    }

    private static List<SceneMeshDefinition> ConvertMeshes(
        ImmutableArray<GltfMeshAsset> source,
        SceneGltfImportOptions options,
        List<SceneGltfDiagnostic> diagnostics)
    {
        var meshes = new List<SceneMeshDefinition>(source.Length);

        foreach (GltfMeshAsset mesh in source)
        {
            GltfMeshDefinition definition = mesh.Definition;

            if (!definition.MorphTargets.IsDefaultOrEmpty)
            {
                diagnostics.Add(new SceneGltfDiagnostic(
                    "scene.gltf.morph-targets",
                    $"Mesh '{definition.Id}' has {definition.MorphTargets.Length} morph targets; the base mesh is imported without them."));
            }

            if (!definition.CustomAttributes.IsDefaultOrEmpty)
            {
                diagnostics.Add(new SceneGltfDiagnostic(
                    "scene.gltf.custom-attributes",
                    $"Mesh '{definition.Id}' declares {definition.CustomAttributes.Length} custom attributes; only standard semantics are imported."));
            }

            var attributes = new SceneVertexAttribute[definition.Attributes.Length];
            for (int i = 0; i < definition.Attributes.Length; i++)
            {
                GltfVertexAttribute attribute = definition.Attributes[i];
                attributes[i] = new SceneVertexAttribute(
                    MapSemantic(attribute.Semantic),
                    attribute.ComponentCount,
                    attribute.Offset,
                    attribute.Stride,
                    attribute.Normalized,
                    attribute.Integer);
            }

            SceneAssetId<MeshAssetTag> id = MakeId<MeshAssetTag>(definition.Id, options);

            var meshDefinition = new SceneMeshDefinition(
                id,
                definition.Vertices,
                attributes,
                definition.VertexCount,
                definition.Indices,
                definition.Index16Bit ? SceneIndexFormat.UInt16 : SceneIndexFormat.UInt32,
                definition.IndexCount,
                MapTopology(definition.Topology));

            meshes.Add(WithComputedBounds(meshDefinition));
        }

        return meshes;
    }

    private static SceneMeshDefinition WithComputedBounds(SceneMeshDefinition mesh)
    {
        if (!SceneMeshBounds.TryCompute(mesh, out BoundingSphere3 bounds))
        {
            return mesh;
        }

        return new SceneMeshDefinition(
            mesh.Id,
            mesh.Vertices,
            mesh.Attributes,
            mesh.VertexCount,
            mesh.Indices,
            mesh.IndexFormat,
            mesh.IndexCount,
            mesh.Topology,
            bounds);
    }

    private static (List<SceneTextureDefinition> Textures, List<SceneSamplerDefinition> Samplers) ConvertTextures(
        ImmutableArray<GltfTextureAsset> source,
        SceneGltfImportOptions options,
        List<SceneGltfDiagnostic> diagnostics)
    {
        var textures = new List<SceneTextureDefinition>(source.Length);
        var samplers = new List<SceneSamplerDefinition>(source.Length);

        foreach (GltfTextureAsset texture in source)
        {
            if (texture.Payload is not GltfTranscodedTexturePayload payload ||
                payload.Levels.IsDefaultOrEmpty)
            {
                diagnostics.Add(new SceneGltfDiagnostic(
                    "scene.gltf.unsupported-texture",
                    $"Texture '{texture.Id}' uses a payload without decoded RGBA8 levels; it was skipped."));
                continue;
            }

            if (payload.Format != GltfTextureFormat.R8G8B8A8_UNorm)
            {
                diagnostics.Add(new SceneGltfDiagnostic(
                    "scene.gltf.unsupported-texture",
                    $"Texture '{texture.Id}' uses format '{payload.Format}'; only RGBA8 is supported."));
                continue;
            }

            if (payload.Bytes.IsEmpty)
            {
                diagnostics.Add(new SceneGltfDiagnostic(
                    "scene.gltf.unsupported-texture",
                    $"Texture '{texture.Id}' has no decoded bytes; it was skipped."));
                continue;
            }

            GltfTextureMipLevel level0 = payload.Levels[0];
            SceneAssetId<TextureAssetTag> textureId = MakeId<TextureAssetTag>(texture.Id, options);
            SceneAssetId<SamplerAssetTag> samplerId = MakeId<SamplerAssetTag>(texture.Id + "/sampler", options);

            samplers.Add(new SceneSamplerDefinition(
                samplerId,
                MapMinFilter(texture.Sampler.MinFilter),
                MapMagFilter(texture.Sampler.MagFilter),
                MapWrap(texture.Sampler.WrapS),
                MapWrap(texture.Sampler.WrapT),
                SceneWrapMode.Repeat));

            textures.Add(new SceneTextureDefinition(
                textureId,
                new SceneTextureData(payload.Bytes),
                SceneTextureFormat.Rgba8,
                level0.Width,
                level0.Height,
                samplerId));
        }

        return (textures, samplers);
    }

    private static List<SceneMaterialDefinition> ConvertMaterials(
        ImmutableArray<GltfMaterialAsset> source,
        SceneGltfImportOptions options,
        List<SceneGltfDiagnostic> diagnostics)
    {
        var materials = new List<SceneMaterialDefinition>(source.Length);

        foreach (GltfMaterialAsset material in source)
        {
            SceneAssetId<ShaderAssetTag> shaderId = material.Unlit
                ? options.UnlitShaderId ?? options.PbrShaderId
                : options.PbrShaderId;

            Vec4 albedo = ReadVec4(material.Definition.FloatUniforms, "_BaseColorFactor") ?? Vec4.One;
            Vec3 emissive = ReadVec3(material.Definition.FloatUniforms, "_EmissiveFactor") ?? Vec3.Zero;
            float metallic = ReadScalar(material.Definition.ScalarUniforms, "_MetallicFactor", 1.0f);
            float roughness = ReadScalar(material.Definition.ScalarUniforms, "_RoughnessFactor", 1.0f);

            SceneMaterialFeatures features = SceneMaterialFeatures.None;
            if (material.DoubleSided)
            {
                features |= SceneMaterialFeatures.TwoSided;
            }

            SceneMaterialTextureBinding? albedoMap = ReadBinding(
                material, GltfTextureUsage.BaseColor, SceneUvSet.Uv0, options);
            SceneMaterialTextureBinding? normalMap = ReadBinding(
                material, GltfTextureUsage.Normal, SceneUvSet.Uv0, options);
            SceneMaterialTextureBinding? pbrMap = ReadBinding(
                material, GltfTextureUsage.MetallicRoughness, SceneUvSet.Uv0, options);
            SceneMaterialTextureBinding? occlusionMap = ReadBinding(
                material, GltfTextureUsage.Occlusion, SceneUvSet.Uv0, options);
            SceneMaterialTextureBinding? emissiveMap = ReadBinding(
                material, GltfTextureUsage.Emissive, SceneUvSet.Uv0, options);

            if (albedoMap is not null) features |= SceneMaterialFeatures.AlbedoMap;
            if (normalMap is not null) features |= SceneMaterialFeatures.NormalMap;
            if (pbrMap is not null) features |= SceneMaterialFeatures.PbrMap;
            if (occlusionMap is not null) features |= SceneMaterialFeatures.OcclusionMap;
            if (emissiveMap is not null) features |= SceneMaterialFeatures.EmissiveMap;

            if (material.AlphaMode == GltfAlphaMode.Mask)
            {
                features |= SceneMaterialFeatures.AlphaTest;
            }

            var surface = new SceneMaterialSurface
            {
                ShadingModel = material.Unlit
                    ? SceneMaterialShadingModel.Unlit
                    : SceneMaterialShadingModel.Pbr,
                AlphaMode = MapAlphaMode(material.AlphaMode),
                AlphaCutoff = material.AlphaCutoff,
                Features = features,
                Albedo = albedo,
                Emissive = emissive,
                Metallic = Math.Clamp(metallic, 0.0f, 1.0f),
                Roughness = Math.Clamp(roughness, 0.0f, 1.0f),
                AlbedoMap = albedoMap,
                NormalMap = normalMap,
                PbrMap = pbrMap,
                OcclusionMap = occlusionMap,
                EmissiveMap = emissiveMap
            };

            var uniforms = new Dictionary<string, SceneUniformValue>(StringComparer.Ordinal);
            foreach ((string name, float value) in material.Definition.ScalarUniforms)
            {
                uniforms[name] = SceneUniformValue.FromFloat(value);
            }

            foreach ((string name, float[] values) in material.Definition.FloatUniforms)
            {
                SceneUniformValue? uniform = values.Length switch
                {
                    2 => SceneUniformValue.FromVec2(new Vec2(values[0], values[1])),
                    3 => SceneUniformValue.FromVec3(new Vec3(values[0], values[1], values[2])),
                    4 => SceneUniformValue.FromVec4(new Vec4(values[0], values[1], values[2], values[3])),
                    _ => null
                };

                if (uniform is { } value)
                {
                    uniforms[name] = value;
                }
                else
                {
                    diagnostics.Add(new SceneGltfDiagnostic(
                        "scene.gltf.unsupported-uniform",
                        $"Material '{material.Id}' uniform '{name}' has {values.Length} components and was skipped."));
                }
            }

            materials.Add(new SceneMaterialDefinition(
                MakeId<MaterialAssetTag>(material.Id, options),
                shaderId,
                surface,
                [
                    new SceneMaterialPass(
                        "main",
                        depthTest: true,
                        depthWrite: material.AlphaMode != GltfAlphaMode.Blend,
                        cull: !material.DoubleSided,
                        blend: material.AlphaMode == GltfAlphaMode.Blend)
                ],
                uniforms));
        }

        return materials;
    }

    private static ScenePrefabDefinition ConvertPrefab(
        GltfImportResult result,
        SceneGltfImportOptions options,
        List<SceneGltfDiagnostic> diagnostics)
    {
        GltfPrefabAsset? prefabAsset = FindDefaultPrefab(result);
        if (prefabAsset is null)
        {
            throw new SceneGltfException(
                SceneGltfErrorCodes.Invalid,
                "The glTF import result contains no prefab assets.");
        }

        if (!result.Skins.IsDefaultOrEmpty)
        {
            diagnostics.Add(new SceneGltfDiagnostic(
                "scene.gltf.skins",
                $"{result.Skins.Length} skin(s) were imported; scene skinning is not available yet."));
        }

        if (!result.Animations.IsDefaultOrEmpty)
        {
            diagnostics.Add(new SceneGltfDiagnostic(
                "scene.gltf.animations",
                $"{result.Animations.Length} animation clip(s) were imported; scene animators are not available yet."));
        }

        var actors = new List<SceneActorSnapshot>(prefabAsset.Definition.Actors.Length);

        foreach (GltfActorSnapshot actor in prefabAsset.Definition.Actors)
        {
            actors.Add(new SceneActorSnapshot(
                actor.NodeId,
                actor.Name,
                (ushort)Math.Clamp(actor.Layer, 0, ushort.MaxValue),
                actor.Tag,
                actor.Active,
                actor.Persistent,
                actor.Pooled,
                ConvertComponents(actor, options, diagnostics),
                actor.ParentNodeId));
        }

        return new ScenePrefabDefinition(prefabAsset.Id, actors);
    }

    private static GltfPrefabAsset? FindDefaultPrefab(GltfImportResult result)
    {
        if (result.Prefabs.IsDefaultOrEmpty)
        {
            return null;
        }

        int defaultScene = result.Document.DefaultScene;
        foreach (GltfPrefabAsset prefab in result.Prefabs)
        {
            if (prefab.SceneIndex == defaultScene)
            {
                return prefab;
            }
        }

        return result.Prefabs[0];
    }

    private static List<SceneComponentSnapshot> ConvertComponents(
        GltfActorSnapshot actor,
        SceneGltfImportOptions options,
        List<SceneGltfDiagnostic> diagnostics)
    {
        var components = new List<SceneComponentSnapshot>(actor.Components.Length);

        foreach (GltfComponentSnapshot component in actor.Components)
        {
            switch (component)
            {
                case GltfTransformComponent transform:
                    components.Add(new SceneComponentSnapshot(
                        Transform3DComponent,
                        WriteJson(writer =>
                        {
                            WriteVec3(writer, "position", transform.Position);
                            WriteQuat(writer, "rotation", transform.Rotation);
                            WriteVec3(writer, "scale", transform.Scale);
                        })));
                    break;

                case GltfCameraComponent camera:
                    components.Add(new SceneComponentSnapshot(
                        CameraComponent,
                        WriteJson(writer =>
                        {
                            writer.WriteBoolean("primary", camera.Primary);
                            writer.WriteBoolean("enabled", true);
                            writer.WriteBoolean("orthographic", camera.Orthographic);
                            writer.WriteString("fovAxis", "vertical");
                            writer.WriteNumber("fieldOfView", camera.FieldOfView);
                            writer.WriteNumber("orthographicSize", camera.OrthographicSize);
                            writer.WriteNumber("near", camera.Near);
                            writer.WriteNumber("far", camera.Far);
                            writer.WriteNumber("clearMask", (int)(SceneClearMask.Color | SceneClearMask.Depth));
                            writer.WriteStartArray("clearColor");
                            writer.WriteNumberValue(0.08f);
                            writer.WriteNumberValue(0.09f);
                            writer.WriteNumberValue(0.11f);
                            writer.WriteNumberValue(1.0f);
                            writer.WriteEndArray();
                            writer.WriteNumber("clearDepth", 1.0f);
                        })));
                    break;

                case GltfDirectionalLightComponent light:
                    components.Add(new SceneComponentSnapshot(
                        DirectionalLightComponent,
                        WriteJson(writer =>
                        {
                            WriteVec3(writer, "color", light.Color);
                            WriteVec3(writer, "ambient", Vec3.Zero);
                            writer.WriteNumber("intensity", light.Intensity);
                            writer.WriteBoolean("primary", light.Primary);
                            writer.WriteBoolean("enabled", true);
                        })));
                    break;

                case GltfPointLightComponent light:
                    components.Add(new SceneComponentSnapshot(
                        PointLightComponent,
                        WriteJson(writer =>
                        {
                            WriteVec3(writer, "color", light.Color);
                            writer.WriteNumber("intensity", light.Intensity);
                            writer.WriteNumber("range", light.Range);
                            writer.WriteNumber("attenuation", 2.0f);
                            writer.WriteBoolean("enabled", true);
                        })));
                    break;

                case GltfSpotLightComponent light:
                    components.Add(new SceneComponentSnapshot(
                        SpotLightComponent,
                        WriteJson(writer =>
                        {
                            WriteVec3(writer, "color", light.Color);
                            writer.WriteNumber("intensity", light.Intensity);
                            writer.WriteNumber("range", light.Range);
                            writer.WriteNumber("attenuation", 2.0f);
                            writer.WriteNumber("innerConeAngle", light.InnerConeAngle);
                            writer.WriteNumber("outerConeAngle", light.OuterConeAngle);
                            writer.WriteBoolean("enabled", true);
                        })));
                    break;

                case GltfMeshRendererComponent renderer:
                    if (string.IsNullOrEmpty(renderer.MeshId))
                    {
                        diagnostics.Add(new SceneGltfDiagnostic(
                            "scene.gltf.mesh-renderer",
                            $"Node '{actor.NodeId}' has a mesh renderer without a mesh id."));
                        break;
                    }

                    if (!string.IsNullOrEmpty(renderer.SkinId))
                    {
                        diagnostics.Add(new SceneGltfDiagnostic(
                            "scene.gltf.skinning",
                            $"Node '{actor.NodeId}' is skinned; it renders as a static mesh for now."));
                    }

                    if (renderer.MorphTargetCount > 0)
                    {
                        diagnostics.Add(new SceneGltfDiagnostic(
                            "scene.gltf.morph-targets",
                            $"Node '{actor.NodeId}' has {renderer.MorphTargetCount} morph targets; morph animation is not available yet."));
                    }

                    components.Add(new SceneComponentSnapshot(
                        MeshRendererComponent,
                        WriteJson(writer =>
                        {
                            writer.WriteString("mesh", Prefixed(renderer.MeshId, options));
                            if (!string.IsNullOrEmpty(renderer.MaterialId))
                            {
                                writer.WriteString("material", Prefixed(renderer.MaterialId, options));
                            }

                            writer.WriteString("pass", renderer.PassId);
                            writer.WriteBoolean("visible", renderer.Visible);
                            writer.WriteBoolean("receiveLighting", renderer.ReceiveLighting);
                            writer.WriteBoolean("transparent", false);
                            writer.WriteNumber("renderOrder", renderer.RenderOrder);
                        })));
                    break;

                case GltfAnimatorComponent:
                    diagnostics.Add(new SceneGltfDiagnostic(
                        "scene.gltf.animator",
                        $"Node '{actor.NodeId}' declares an animator; scene animators are not available yet."));
                    break;
            }
        }

        return components;
    }

    private static SceneMaterialTextureBinding? ReadBinding(
        GltfMaterialAsset material,
        GltfTextureUsage usage,
        SceneUvSet uvSet,
        SceneGltfImportOptions options)
    {
        if (!material.Textures.TryGetValue(usage, out GltfMaterialTextureBinding? binding) ||
            string.IsNullOrEmpty(binding.TextureKey))
        {
            return null;
        }

        SceneAssetId<TextureAssetTag> textureId = MakeId<TextureAssetTag>(binding.TextureKey, options);
        SceneAssetId<SamplerAssetTag> samplerId = MakeId<SamplerAssetTag>(binding.TextureKey + "/sampler", options);

        // KHR_texture_transform is baked into the binding scale/offset; UV set
        // selection follows the glTF texCoord index.
        return new SceneMaterialTextureBinding(
            textureId,
            samplerId,
            binding.TexCoord > 0 ? SceneUvSet.Uv1 : uvSet,
            unit: null,
            scale: binding.Transform.Scale,
            offset: binding.Transform.Offset,
            rotation: binding.Transform.Rotation);
    }

    private static SceneAssetId<ShaderAssetTag> InternDefaultShader(
        SceneAssetId<ShaderAssetTag> id,
        SceneNameTable names,
        string defaultName)
    {
        // Custom shader ids must be interned by the caller; the built-in
        // defaults are interned here so snapshots stay serializable.
        return id == SceneAssetId<ShaderAssetTag>.FromName(defaultName)
            ? SceneAssetId<ShaderAssetTag>.FromSymbol(names.Intern(defaultName))
            : id;
    }

    private static SceneAssetId<TTag> MakeId<TTag>(string name, SceneGltfImportOptions options)
        where TTag : struct
    {
        string prefixed = Prefixed(name, options);
        return options.Names is { } names
            ? SceneAssetId<TTag>.FromSymbol(names.Intern(prefixed))
            : SceneAssetId<TTag>.FromName(prefixed);
    }

    private static string Prefixed(string name, SceneGltfImportOptions options) =>
        options.AssetPrefix + name;

    private static SceneMeshSemantic MapSemantic(GltfMeshSemantic semantic) => semantic switch
    {
        GltfMeshSemantic.Position => SceneMeshSemantic.Position,
        GltfMeshSemantic.Normal => SceneMeshSemantic.Normal,
        GltfMeshSemantic.Uv0 => SceneMeshSemantic.Uv0,
        GltfMeshSemantic.Uv1 => SceneMeshSemantic.Uv1,
        GltfMeshSemantic.Tangent => SceneMeshSemantic.Tangent,
        GltfMeshSemantic.Color0 => SceneMeshSemantic.Color0,
        GltfMeshSemantic.Joints0 => SceneMeshSemantic.Joints0,
        _ => SceneMeshSemantic.Weights0
    };

    private static SceneMeshTopology MapTopology(GltfMeshTopology topology) => topology switch
    {
        GltfMeshTopology.Lines => SceneMeshTopology.Lines,
        GltfMeshTopology.Points => SceneMeshTopology.Points,
        _ => SceneMeshTopology.Triangles
    };

    private static SceneMaterialAlphaMode MapAlphaMode(GltfAlphaMode mode) => mode switch
    {
        GltfAlphaMode.Mask => SceneMaterialAlphaMode.Mask,
        GltfAlphaMode.Blend => SceneMaterialAlphaMode.Blend,
        _ => SceneMaterialAlphaMode.Opaque
    };

    private static SceneFilterMode MapMinFilter(int value) => value switch
    {
        9728 => SceneFilterMode.Nearest,
        9729 => SceneFilterMode.Linear,
        9984 => SceneFilterMode.NearestMipmapNearest,
        9985 => SceneFilterMode.LinearMipmapNearest,
        9986 => SceneFilterMode.NearestMipmapLinear,
        _ => SceneFilterMode.LinearMipmapLinear
    };

    private static SceneFilterMode MapMagFilter(int value) => value switch
    {
        9728 => SceneFilterMode.Nearest,
        _ => SceneFilterMode.Linear
    };

    private static SceneWrapMode MapWrap(int value) => value switch
    {
        33071 => SceneWrapMode.ClampToEdge,
        33648 => SceneWrapMode.MirroredRepeat,
        _ => SceneWrapMode.Repeat
    };

    private static float ReadScalar(
        ImmutableDictionary<string, float> values,
        string key,
        float fallback) =>
        values.TryGetValue(key, out float value) ? value : fallback;

    private static Vec3? ReadVec3(ImmutableDictionary<string, float[]> values, string key)
    {
        if (!values.TryGetValue(key, out float[]? array) || array.Length < 3)
        {
            return null;
        }

        return new Vec3(array[0], array[1], array[2]);
    }

    private static Vec4? ReadVec4(ImmutableDictionary<string, float[]> values, string key)
    {
        if (!values.TryGetValue(key, out float[]? array) || array.Length < 4)
        {
            return null;
        }

        return new Vec4(array[0], array[1], array[2], array[3]);
    }

    private static JsonElement WriteJson(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }

        using JsonDocument document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    private static void WriteVec3(Utf8JsonWriter writer, string name, in Vec3 value)
    {
        writer.WriteStartArray(name);
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteNumberValue(value.Z);
        writer.WriteEndArray();
    }

    private static void WriteQuat(Utf8JsonWriter writer, string name, in Quat value)
    {
        writer.WriteStartArray(name);
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteNumberValue(value.Z);
        writer.WriteNumberValue(value.W);
        writer.WriteEndArray();
    }
}
