namespace Axrone.AssetGltf;

/// <summary>Canonical imported glTF document.</summary>
public sealed class GltfDocumentAsset
{
    /// <summary>Document identity.</summary>
    public required string Id { get; init; }

    /// <summary>Source URI.</summary>
    public string? Uri { get; init; }

    /// <summary>Document name.</summary>
    public required string Name { get; init; }

    /// <summary>Container format (glb/gltf).</summary>
    public required string Format { get; init; }

    /// <summary>glTF version.</summary>
    public required string Version { get; init; }

    /// <summary>Generator string.</summary>
    public string? Generator { get; init; }

    /// <summary>Copyright string.</summary>
    public string? Copyright { get; init; }

    /// <summary>Default scene index.</summary>
    public int DefaultScene { get; init; }

    /// <summary>Scene assets.</summary>
    public required ImmutableArray<GltfDocumentSceneAsset> Scenes { get; init; }

    /// <summary>Mesh asset keys.</summary>
    public required ImmutableArray<string> MeshKeys { get; init; }

    /// <summary>Skin asset keys.</summary>
    public required ImmutableArray<string> SkinKeys { get; init; }

    /// <summary>Animation asset keys.</summary>
    public required ImmutableArray<string> AnimationKeys { get; init; }

    /// <summary>Material asset keys.</summary>
    public required ImmutableArray<string> MaterialKeys { get; init; }

    /// <summary>Texture asset keys.</summary>
    public required ImmutableArray<string> TextureKeys { get; init; }

    /// <summary>Used extensions.</summary>
    public required ImmutableArray<string> ExtensionsUsed { get; init; }

    /// <summary>Required extensions.</summary>
    public required ImmutableArray<string> ExtensionsRequired { get; init; }

    /// <summary>Import statistics.</summary>
    public required GltfDocumentStats Stats { get; init; }
}

/// <summary>Import statistics.</summary>
public readonly record struct GltfDocumentStats(
    int SceneCount,
    int NodeCount,
    int CameraCount,
    int LightCount,
    int MeshCount,
    int PrimitiveCount,
    int MaterialCount,
    int TextureCount,
    int SkinCount,
    int AnimationCount);

/// <summary>Canonical scene asset.</summary>
public sealed class GltfDocumentSceneAsset
{
    /// <summary>Scene index.</summary>
    public required int SceneIndex { get; init; }

    /// <summary>Scene name.</summary>
    public required string Name { get; init; }

    /// <summary>Prefab key.</summary>
    public required string PrefabKey { get; init; }

    /// <summary>Root node ids.</summary>
    public required ImmutableArray<string> RootNodeIds { get; init; }

    /// <summary>Animation controller metadata.</summary>
    public GltfAnimationControllerMetadata? AnimationController { get; init; }
}

/// <summary>Canonical prefab asset.</summary>
public sealed class GltfPrefabAsset
{
    /// <summary>Prefab identity.</summary>
    public required string Id { get; init; }

    /// <summary>Scene index.</summary>
    public required int SceneIndex { get; init; }

    /// <summary>Prefab definition.</summary>
    public required GltfPrefabDefinition Definition { get; init; }

    /// <summary>Root node ids.</summary>
    public required ImmutableArray<string> RootNodeIds { get; init; }

    /// <summary>All node ids.</summary>
    public required ImmutableArray<string> NodeIds { get; init; }

    /// <summary>Mesh keys.</summary>
    public required ImmutableArray<string> MeshKeys { get; init; }

    /// <summary>Skin keys.</summary>
    public required ImmutableArray<string> SkinKeys { get; init; }

    /// <summary>Animation keys.</summary>
    public required ImmutableArray<string> AnimationKeys { get; init; }

    /// <summary>Material keys.</summary>
    public required ImmutableArray<string> MaterialKeys { get; init; }

    /// <summary>Animation controller metadata.</summary>
    public GltfAnimationControllerMetadata? AnimationController { get; init; }
}

/// <summary>Canonical mesh asset (one primitive).</summary>
public sealed class GltfMeshAsset
{
    /// <summary>Mesh identity.</summary>
    public required string Id { get; init; }

    /// <summary>Mesh index.</summary>
    public required int MeshIndex { get; init; }

    /// <summary>Primitive index.</summary>
    public required int PrimitiveIndex { get; init; }

    /// <summary>Mesh definition.</summary>
    public required GltfMeshDefinition Definition { get; init; }

    /// <summary>Computed bounds.</summary>
    public GltfMeshBounds? Bounds { get; init; }

    /// <summary>Material key.</summary>
    public string? MaterialKey { get; init; }

    /// <summary>Extras payload.</summary>
    public ImmutableDictionary<string, object>? Extras { get; init; }
}

/// <summary>Morph target delta buffer.</summary>
public readonly record struct GltfMorphTargetDefinition(
    GltfMorphTargetSemantic Semantic,
    ReadOnlyMemory<byte> DeltaBuffer,
    int Stride,
    int ComponentCount);

/// <summary>Interleaved mesh definition.</summary>
public readonly record struct GltfMeshDefinition(
    string Id,
    ReadOnlyMemory<byte> Vertices,
    ImmutableArray<GltfVertexAttribute> Attributes,
    ImmutableArray<GltfMorphTargetDefinition> MorphTargets,
    ReadOnlyMemory<byte> Indices,
    int VertexCount,
    int IndexCount,
    bool Index16Bit,
    GltfMeshTopology Topology);

/// <summary>Canonical skin asset.</summary>
public sealed class GltfSkinAsset
{
    /// <summary>Skin identity.</summary>
    public required string Id { get; init; }

    /// <summary>Skin index.</summary>
    public required int SkinIndex { get; init; }

    /// <summary>Joint node ids.</summary>
    public required ImmutableArray<string> JointNodeIds { get; init; }

    /// <summary>Joint node indices.</summary>
    public required ImmutableArray<int> JointNodeIndices { get; init; }

    /// <summary>Skeleton node id.</summary>
    public string? SkeletonNodeId { get; init; }

    /// <summary>Skeleton node index.</summary>
    public int? SkeletonNodeIndex { get; init; }

    /// <summary>Row-major inverse bind matrices.</summary>
    public ReadOnlyMemory<float> InverseBindMatrices { get; init; }
}

/// <summary>Canonical animation clip asset.</summary>
public sealed class GltfAnimationClipAsset
{
    /// <summary>Clip identity.</summary>
    public required string Id { get; init; }

    /// <summary>Animation index.</summary>
    public required int AnimationIndex { get; init; }

    /// <summary>Duration seconds.</summary>
    public required float Duration { get; init; }

    /// <summary>Tracks.</summary>
    public required ImmutableArray<GltfAnimationTrackAsset> Tracks { get; init; }

    /// <summary>Tags.</summary>
    public ImmutableArray<string> Tags { get; init; } = ImmutableArray<string>.Empty;
}

/// <summary>Canonical animation track asset.</summary>
public sealed class GltfAnimationTrackAsset
{
    /// <summary>Channel index.</summary>
    public required int ChannelIndex { get; init; }

    /// <summary>Sampler index.</summary>
    public required int SamplerIndex { get; init; }

    /// <summary>Input accessor.</summary>
    public required int InputAccessor { get; init; }

    /// <summary>Output accessor.</summary>
    public required int OutputAccessor { get; init; }

    /// <summary>Target node index.</summary>
    public required int TargetNodeIndex { get; init; }

    /// <summary>Target node id.</summary>
    public required string TargetNodeId { get; init; }

    /// <summary>Target path.</summary>
    public required GltfAnimationPath Path { get; init; }

    /// <summary>Interpolation.</summary>
    public required GltfInterpolation Interpolation { get; init; }

    /// <summary>Keyframe count.</summary>
    public required int KeyframeCount { get; init; }

    /// <summary>Value component count.</summary>
    public required int ValueComponentCount { get; init; }

    /// <summary>Sample stride.</summary>
    public required int SampleStride { get; init; }

    /// <summary>Keyframe times.</summary>
    public required ReadOnlyMemory<float> Times { get; init; }

    /// <summary>Keyframe values.</summary>
    public required ReadOnlyMemory<float> Values { get; init; }
}
