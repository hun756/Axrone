namespace Axrone.AssetGltf;

/// <summary>glTF asset importer bridge (bytes/text to canonical writes).</summary>
public sealed class GltfAssetImporter : IAssetImporter
{
    /// <inheritdoc/>
    public AssetImporterId Id => new("axrone.importer.gltf");

    /// <inheritdoc/>
    public int Priority => 20;

    /// <inheritdoc/>
    public IReadOnlyList<string> SourceKinds => ["bytes", "text"];

    /// <inheritdoc/>
    public IReadOnlyList<string> Extensions => [".gltf", ".glb"];

    /// <inheritdoc/>
    public IReadOnlyList<string> MimeTypes => ["model/gltf+json", "model/gltf-binary"];

    /// <inheritdoc/>
    public bool CanImport(AssetImportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!TryGetSourceBytes(context, out byte[]? bytes) || bytes.Length == 0)
        {
            return false;
        }

        return HasGlbMagic(bytes) || HasJsonObjectStart(bytes);
    }

    /// <inheritdoc/>
    public async ValueTask<AssetImportResult> ImportAsync(AssetImportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!TryGetSourceBytes(context, out byte[]? bytes))
        {
            throw new GltfException("gltf.source.unsupported", $"Unsupported import source kind '{context.Source.Kind}'.");
        }

        string id = context.SourceUri?.Value ?? context.BaseKey.Value ?? "asset.gltf";
        var importer = new GltfImporter(new GltfImporterOptions { Id = id });
        GltfImportResult result = await importer.ImportAsync(bytes, cancellationToken).ConfigureAwait(false);

        return MapResult(context, result);
    }

    private static AssetImportResult MapResult(AssetImportContext context, GltfImportResult result)
    {
        AssetKey SubKey(string suffix) => new($"{context.BaseKey}#{suffix}");

        AssetKey docKey = SubKey("document");

        var textureKeys = new Dictionary<string, AssetKey>(StringComparer.Ordinal);
        var textureWrites = new List<AssetWriteInput<object>>(result.Textures.Length);
        foreach (GltfTextureAsset texture in result.Textures)
        {
            AssetKey key = SubKey($"texture/{texture.TextureIndex}");
            textureKeys[texture.Id] = key;
            textureWrites.Add(new AssetWriteInput<object>(
                Kind: "gltf.texture",
                Id: null,
                Key: key,
                Name: $"Texture {texture.TextureIndex}",
                Data: (object)texture,
                Metadata: context.Metadata));
        }

        var materialKeys = new Dictionary<string, AssetKey>(StringComparer.Ordinal);
        var materialWrites = new List<AssetWriteInput<object>>(result.Materials.Length);
        foreach (GltfMaterialAsset material in result.Materials)
        {
            AssetKey key = SubKey($"material/{material.MaterialIndex}");
            materialKeys[material.Id] = key;

            var deps = new List<AssetDependencyInput>(material.Textures.Count);
            foreach (GltfMaterialTextureBinding binding in material.Textures.Values)
            {
                if (textureKeys.TryGetValue(binding.TextureKey, out AssetKey textureKey))
                {
                    deps.Add(textureKey);
                }
            }

            materialWrites.Add(new AssetWriteInput<object>(
                Kind: "gltf.material",
                Id: null,
                Key: key,
                Name: $"Material {material.MaterialIndex}",
                Data: (object)material,
                Metadata: context.Metadata,
                Dependencies: deps));
        }

        var meshKeys = new Dictionary<string, AssetKey>(StringComparer.Ordinal);
        var meshWrites = new List<AssetWriteInput<object>>(result.Meshes.Length);
        foreach (GltfMeshAsset mesh in result.Meshes)
        {
            AssetKey key = SubKey($"mesh/{mesh.MeshIndex}/primitive/{mesh.PrimitiveIndex}");
            meshKeys[mesh.Id] = key;

            List<AssetDependencyInput>? deps = null;
            if (mesh.MaterialKey != null && materialKeys.TryGetValue(mesh.MaterialKey, out AssetKey materialKey))
            {
                deps = new List<AssetDependencyInput>(1) { materialKey };
            }

            meshWrites.Add(new AssetWriteInput<object>(
                Kind: "gltf.mesh",
                Id: null,
                Key: key,
                Name: $"Mesh {mesh.MeshIndex} Primitive {mesh.PrimitiveIndex}",
                Data: (object)mesh,
                Metadata: context.Metadata,
                Dependencies: deps));
        }

        var skinKeys = new Dictionary<string, AssetKey>(StringComparer.Ordinal);
        var skinWrites = new List<AssetWriteInput<object>>(result.Skins.Length);
        foreach (GltfSkinAsset skin in result.Skins)
        {
            AssetKey key = SubKey($"skin/{skin.SkinIndex}");
            skinKeys[skin.Id] = key;
            skinWrites.Add(new AssetWriteInput<object>(
                Kind: "gltf.skin",
                Id: null,
                Key: key,
                Name: $"Skin {skin.SkinIndex}",
                Data: (object)skin,
                Metadata: context.Metadata));
        }

        var animationKeys = new Dictionary<string, AssetKey>(StringComparer.Ordinal);
        var animationWrites = new List<AssetWriteInput<object>>(result.Animations.Length);
        foreach (GltfAnimationClipAsset animation in result.Animations)
        {
            AssetKey key = SubKey($"animation/{animation.AnimationIndex}");
            animationKeys[animation.Id] = key;
            animationWrites.Add(new AssetWriteInput<object>(
                Kind: "gltf.animation",
                Id: null,
                Key: key,
                Name: $"Animation {animation.AnimationIndex}",
                Data: (object)animation,
                Metadata: context.Metadata));
        }

        var prefabWrites = new List<AssetWriteInput<object>>(result.Prefabs.Length);
        foreach (GltfPrefabAsset prefab in result.Prefabs)
        {
            AssetKey key = SubKey($"scene/{prefab.SceneIndex}/prefab");

            var deps = new List<AssetDependencyInput>(prefab.MeshKeys.Length + prefab.SkinKeys.Length + prefab.AnimationKeys.Length);
            foreach (string meshId in prefab.MeshKeys)
            {
                if (meshKeys.TryGetValue(meshId, out AssetKey meshKey))
                {
                    deps.Add(meshKey);
                }
            }

            foreach (string skinId in prefab.SkinKeys)
            {
                if (skinKeys.TryGetValue(skinId, out AssetKey skinKey))
                {
                    deps.Add(skinKey);
                }
            }

            foreach (string animationId in prefab.AnimationKeys)
            {
                if (animationKeys.TryGetValue(animationId, out AssetKey animationKey))
                {
                    deps.Add(animationKey);
                }
            }

            string name = $"Scene {prefab.SceneIndex}";
            foreach (GltfDocumentSceneAsset scene in result.Document.Scenes)
            {
                if (scene.SceneIndex == prefab.SceneIndex)
                {
                    name = scene.Name;
                    break;
                }
            }

            prefabWrites.Add(new AssetWriteInput<object>(
                Kind: "gltf.prefab",
                Id: null,
                Key: key,
                Name: name,
                Data: (object)prefab,
                Metadata: context.Metadata,
                Dependencies: deps));
        }

        var docDeps = new List<AssetDependencyInput>(textureWrites.Count + materialWrites.Count + meshWrites.Count + skinWrites.Count + animationWrites.Count + prefabWrites.Count);
        foreach (AssetWriteInput<object> write in textureWrites)
        {
            docDeps.Add(write.Key);
        }

        foreach (AssetWriteInput<object> write in materialWrites)
        {
            docDeps.Add(write.Key);
        }

        foreach (AssetWriteInput<object> write in meshWrites)
        {
            docDeps.Add(write.Key);
        }

        foreach (AssetWriteInput<object> write in skinWrites)
        {
            docDeps.Add(write.Key);
        }

        foreach (AssetWriteInput<object> write in animationWrites)
        {
            docDeps.Add(write.Key);
        }

        foreach (AssetWriteInput<object> write in prefabWrites)
        {
            docDeps.Add(write.Key);
        }

        var primary = new AssetWriteInput<object>(
            Kind: "gltf.document",
            Id: null,
            Key: docKey,
            Name: result.Document.Name,
            Data: (object)result.Document,
            Metadata: context.Metadata,
            Dependencies: docDeps);

        var additional = new List<AssetWriteInput<object>>(textureWrites.Count + materialWrites.Count + meshWrites.Count + skinWrites.Count + animationWrites.Count + prefabWrites.Count);
        additional.AddRange(textureWrites);
        additional.AddRange(materialWrites);
        additional.AddRange(meshWrites);
        additional.AddRange(skinWrites);
        additional.AddRange(animationWrites);
        additional.AddRange(prefabWrites);

        List<AssetImportDiagnostic>? diagnostics = null;
        if (result.Diagnostics.Length > 0)
        {
            diagnostics = new List<AssetImportDiagnostic>(result.Diagnostics.Length);
            foreach (GltfDiagnostic diagnostic in result.Diagnostics)
            {
                string severity = diagnostic.Level switch
                {
                    GltfDiagnosticLevel.Info => "info",
                    GltfDiagnosticLevel.Warning => "warning",
                    _ => "error",
                };
                diagnostics.Add(new AssetImportDiagnostic(severity, diagnostic.Code, diagnostic.Message));
            }
        }

        return new AssetImportResult { Primary = primary, Additional = additional, Diagnostics = diagnostics };
    }

    private static bool TryGetSourceBytes(AssetImportContext context, [NotNullWhen(true)] out byte[]? bytes)
    {
        switch (context.Source.RawData)
        {
            case byte[] raw:
                bytes = raw;
                return true;
            case string text:
                bytes = Encoding.UTF8.GetBytes(text);
                return true;
            default:
                bytes = null;
                return false;
        }
    }

    private static bool HasGlbMagic(byte[] bytes) =>
        bytes.Length >= 4 && BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0, 4)) == 0x46546C67;

    private static bool HasJsonObjectStart(byte[] bytes)
    {
        int i = 0;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            i = 3;
        }

        while (i < bytes.Length)
        {
            byte b = bytes[i];
            if (b == 0x20 || b == 0x09 || b == 0x0D || b == 0x0A)
            {
                i++;
                continue;
            }

            return b == 0x7B;
        }

        return false;
    }
}
