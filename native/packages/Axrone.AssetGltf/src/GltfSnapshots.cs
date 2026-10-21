namespace Axrone.AssetGltf;

/// <summary>Animation controller metadata.</summary>
public sealed class GltfAnimationControllerMetadata
{
    /// <summary>Parameter names.</summary>
    public ImmutableArray<string> Parameters { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>Layer names.</summary>
    public ImmutableArray<string> Layers { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>Clip asset keys.</summary>
    public ImmutableArray<string> Clips { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>Root motion node id.</summary>
    public string? RootMotionNodeId { get; init; }
}

/// <summary>Actor component snapshot base.</summary>
public abstract record GltfComponentSnapshot;

/// <summary>Transform component snapshot.</summary>
public sealed record GltfTransformComponent(
    Vec3 Position,
    Quat Rotation,
    Vec3 Scale) : GltfComponentSnapshot;

/// <summary>Camera component snapshot.</summary>
public sealed record GltfCameraComponent(
    bool Primary,
    float Near,
    float Far,
    float FieldOfView,
    bool Orthographic,
    float OrthographicSize) : GltfComponentSnapshot;

/// <summary>Directional light component snapshot.</summary>
public sealed record GltfDirectionalLightComponent(
    Vec3 Color,
    float Intensity,
    bool Primary) : GltfComponentSnapshot;

/// <summary>Point light component snapshot.</summary>
public sealed record GltfPointLightComponent(
    Vec3 Color,
    float Intensity,
    float Range) : GltfComponentSnapshot;

/// <summary>Spot light component snapshot.</summary>
public sealed record GltfSpotLightComponent(
    Vec3 Color,
    float Intensity,
    float Range,
    float InnerConeAngle,
    float OuterConeAngle) : GltfComponentSnapshot;

/// <summary>Mesh renderer component snapshot.</summary>
public sealed record GltfMeshRendererComponent(
    string MeshId,
    string? MaterialId,
    bool Visible,
    int RenderOrder,
    string PassId,
    bool ReceiveLighting,
    string? SkinId,
    int MorphTargetCount) : GltfComponentSnapshot;

/// <summary>Animator component snapshot.</summary>
public sealed record GltfAnimatorComponent(
    ImmutableArray<string> Clips,
    string? DefaultClipId,
    bool PlayOnStart,
    bool Loop,
    float Speed) : GltfComponentSnapshot;

/// <summary>Actor snapshot.</summary>
public sealed class GltfActorSnapshot
{
    /// <summary>Node id.</summary>
    public required string NodeId { get; init; }

    /// <summary>Parent node id.</summary>
    public string? ParentNodeId { get; init; }

    /// <summary>Actor name.</summary>
    public required string Name { get; init; }

    /// <summary>Layer index.</summary>
    public int Layer { get; init; }

    /// <summary>Tag.</summary>
    public string Tag { get; init; } = "Default";

    /// <summary>Active flag.</summary>
    public bool Active { get; init; } = true;

    /// <summary>Persistent flag.</summary>
    public bool Persistent { get; init; }

    /// <summary>Pooled flag.</summary>
    public bool Pooled { get; init; }

    /// <summary>Components.</summary>
    public required ImmutableArray<GltfComponentSnapshot> Components { get; init; }
}

/// <summary>Prefab definition: actor list.</summary>
public sealed class GltfPrefabDefinition
{
    /// <summary>Actors.</summary>
    public required ImmutableArray<GltfActorSnapshot> Actors { get; init; }
}

/// <summary>Portable animation manifest.</summary>
public sealed class PortableAnimationManifest
{
    /// <summary>Schema version.</summary>
    public string Schema { get; init; } = "1.0";

    /// <summary>Scene entries.</summary>
    public List<PortableAnimationManifestSceneEntry> Scenes { get; } = new();

    /// <summary>Clip entries.</summary>
    public List<PortableAnimationClipManifestEntry> Clips { get; } = new();
}

/// <summary>Portable manifest scene entry.</summary>
public sealed class PortableAnimationManifestSceneEntry
{
    /// <summary>Scene index.</summary>
    public int SceneIndex { get; set; }

    /// <summary>Controller key.</summary>
    public string? ControllerKey { get; set; }

    /// <summary>Default clip keys.</summary>
    public List<string> DefaultClips { get; } = new();
}

/// <summary>Portable manifest clip entry.</summary>
public sealed class PortableAnimationClipManifestEntry
{
    /// <summary>Clip id.</summary>
    public string ClipId { get; set; } = string.Empty;

    /// <summary>Duration seconds.</summary>
    public float Duration { get; set; }

    /// <summary>Tags.</summary>
    public List<string> Tags { get; } = new();

    /// <summary>Foot contact markers.</summary>
    public List<string> FootContacts { get; } = new();
}

/// <summary>Streaming chunk time/keyframe range.</summary>
public readonly record struct PortableAnimationStreamingChunkRange(
    int ChunkIndex,
    float StartTime,
    float EndTime,
    int KeyframeStart,
    int KeyframeEnd);

/// <summary>Streaming clip bundle descriptor.</summary>
public sealed class PortableAnimationStreamingClipBundle
{
    /// <summary>Bundle MIME type.</summary>
    public const string MimeType = "application/vnd.axrone.animation.clip-chunk+json";

    /// <summary>Clip id.</summary>
    public required string ClipId { get; init; }

    /// <summary>Total duration seconds.</summary>
    public required float TotalDuration { get; init; }

    /// <summary>Chunk duration seconds.</summary>
    public required float ChunkDuration { get; init; }

    /// <summary>Chunk ranges.</summary>
    public required ImmutableArray<PortableAnimationStreamingChunkRange> Ranges { get; init; }
}
