namespace Axrone.AssetGltf;

/// <summary>Mesh vertex attribute semantic.</summary>
public enum GltfMeshSemantic : byte
{
    /// <summary>POSITION attribute.</summary>
    Position = 0,

    /// <summary>NORMAL attribute.</summary>
    Normal = 1,

    /// <summary>TEXCOORD_0 attribute.</summary>
    Uv0 = 2,

    /// <summary>TEXCOORD_1 attribute.</summary>
    Uv1 = 3,

    /// <summary>TANGENT attribute.</summary>
    Tangent = 4,

    /// <summary>COLOR_0 attribute.</summary>
    Color0 = 5,

    /// <summary>JOINTS_0 attribute.</summary>
    Joints0 = 6,

    /// <summary>WEIGHTS_0 attribute.</summary>
    Weights0 = 7,
}

/// <summary>Morph target attribute semantic.</summary>
public enum GltfMorphTargetSemantic : byte
{
    /// <summary>Position delta.</summary>
    Position = 0,

    /// <summary>Normal delta.</summary>
    Normal = 1,

    /// <summary>Tangent delta.</summary>
    Tangent = 2,
}

/// <summary>Simplified mesh topology used by the runtime pipeline.</summary>
public enum GltfMeshTopology : byte
{
    /// <summary>Point list.</summary>
    Points = 0,

    /// <summary>Line list.</summary>
    Lines = 1,

    /// <summary>Triangle list.</summary>
    Triangles = 2,
}

/// <summary>Material alpha mode.</summary>
public enum GltfAlphaMode : byte
{
    /// <summary>Opaque.</summary>
    Opaque = 0,

    /// <summary>Alpha-tested mask.</summary>
    Mask = 1,

    /// <summary>Alpha blending.</summary>
    Blend = 2,
}

/// <summary>Material texture slot.</summary>
public enum GltfTextureUsage : byte
{
    /// <summary>Base color.</summary>
    BaseColor = 0,

    /// <summary>Metallic-roughness.</summary>
    MetallicRoughness = 1,

    /// <summary>Normal map.</summary>
    Normal = 2,

    /// <summary>Occlusion.</summary>
    Occlusion = 3,

    /// <summary>Emissive.</summary>
    Emissive = 4,

    /// <summary>Clearcoat.</summary>
    Clearcoat = 5,

    /// <summary>Clearcoat roughness.</summary>
    ClearcoatRoughness = 6,

    /// <summary>Clearcoat normal.</summary>
    ClearcoatNormal = 7,

    /// <summary>Specular intensity.</summary>
    Specular = 8,

    /// <summary>Specular color.</summary>
    SpecularColor = 9,

    /// <summary>Sheen color.</summary>
    SheenColor = 10,

    /// <summary>Sheen roughness.</summary>
    SheenRoughness = 11,

    /// <summary>Transmission.</summary>
    Transmission = 12,

    /// <summary>Volume thickness.</summary>
    Thickness = 13,

    /// <summary>Iridescence.</summary>
    Iridescence = 14,

    /// <summary>Iridescence thickness.</summary>
    IridescenceThickness = 15,

    /// <summary>Anisotropy.</summary>
    Anisotropy = 16,
}

/// <summary>Animation channel target path.</summary>
public enum GltfAnimationPath : byte
{
    /// <summary>Translation track.</summary>
    Translation = 0,

    /// <summary>Rotation track.</summary>
    Rotation = 1,

    /// <summary>Scale track.</summary>
    Scale = 2,

    /// <summary>Morph weights track.</summary>
    Weights = 3,
}

/// <summary>Animation sampler interpolation.</summary>
public enum GltfInterpolation : byte
{
    /// <summary>Linear.</summary>
    Linear = 0,

    /// <summary>Step.</summary>
    Step = 1,

    /// <summary>Cubic spline.</summary>
    CubicSpline = 2,
}

/// <summary>Runtime texture format (KTX2 / Basis profile).</summary>
public enum GltfTextureFormat : ushort
{
    /// <summary>Unknown format.</summary>
    Unknown = 0,

    /// <summary>8-bit RGBA.</summary>
    R8G8B8A8_UNorm = 1,

    /// <summary>BC1 RGB.</summary>
    BC1_RGB = 131,

    /// <summary>BC3 RGBA.</summary>
    BC3_RGBA = 137,

    /// <summary>BC4 red.</summary>
    BC4_R = 139,

    /// <summary>BC5 red-green.</summary>
    BC5_RG = 141,

    /// <summary>BC7 RGBA.</summary>
    BC7_RGBA = 145,

    /// <summary>ASTC 4x4.</summary>
    ASTC_4x4 = 157,

    /// <summary>ASTC 5x4.</summary>
    ASTC_5x4 = 158,

    /// <summary>ASTC 5x5.</summary>
    ASTC_5x5 = 159,

    /// <summary>ASTC 6x5.</summary>
    ASTC_6x5 = 160,

    /// <summary>ASTC 6x6.</summary>
    ASTC_6x6 = 161,

    /// <summary>ASTC 8x5.</summary>
    ASTC_8x5 = 162,

    /// <summary>ASTC 8x6.</summary>
    ASTC_8x6 = 163,

    /// <summary>ASTC 8x8.</summary>
    ASTC_8x8 = 164,

    /// <summary>ASTC 10x5.</summary>
    ASTC_10x5 = 165,

    /// <summary>ASTC 10x6.</summary>
    ASTC_10x6 = 166,

    /// <summary>ASTC 10x8.</summary>
    ASTC_10x8 = 167,

    /// <summary>ASTC 10x10.</summary>
    ASTC_10x10 = 168,

    /// <summary>ASTC 12x10.</summary>
    ASTC_12x10 = 169,

    /// <summary>ASTC 12x12.</summary>
    ASTC_12x12 = 170,
}

/// <summary>Import diagnostic severity.</summary>
public enum GltfDiagnosticLevel : byte
{
    /// <summary>Informational.</summary>
    Info = 0,

    /// <summary>Recoverable warning.</summary>
    Warning = 1,

    /// <summary>Non-recoverable error.</summary>
    Error = 2,
}

/// <summary>One import diagnostic.</summary>
public readonly record struct GltfDiagnostic(GltfDiagnosticLevel Level, string Code, string Message);

/// <summary>Nominal accessor index.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct AccessorId(int Value) :
    IEquatable<AccessorId>,
    IComparable<AccessorId>,
    IComparisonOperators<AccessorId, AccessorId, bool>
{
    /// <summary>Invalid sentinel.</summary>
    public static AccessorId Invalid => new(-1);

    /// <summary>Whether the index is valid.</summary>
    public bool IsValid => Value >= 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(AccessorId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(AccessorId left, AccessorId right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(AccessorId left, AccessorId right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(AccessorId left, AccessorId right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(AccessorId left, AccessorId right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Nominal buffer index.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct BufferId(int Value) :
    IEquatable<BufferId>,
    IComparable<BufferId>,
    IComparisonOperators<BufferId, BufferId, bool>
{
    /// <summary>Invalid sentinel.</summary>
    public static BufferId Invalid => new(-1);

    /// <summary>Whether the index is valid.</summary>
    public bool IsValid => Value >= 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(BufferId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(BufferId left, BufferId right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(BufferId left, BufferId right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(BufferId left, BufferId right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(BufferId left, BufferId right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Nominal bufferView index.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct BufferViewId(int Value) :
    IEquatable<BufferViewId>,
    IComparable<BufferViewId>,
    IComparisonOperators<BufferViewId, BufferViewId, bool>
{
    /// <summary>Invalid sentinel.</summary>
    public static BufferViewId Invalid => new(-1);

    /// <summary>Whether the index is valid.</summary>
    public bool IsValid => Value >= 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(BufferViewId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(BufferViewId left, BufferViewId right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(BufferViewId left, BufferViewId right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(BufferViewId left, BufferViewId right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(BufferViewId left, BufferViewId right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Nominal mesh index.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct MeshId(int Value) :
    IEquatable<MeshId>,
    IComparable<MeshId>,
    IComparisonOperators<MeshId, MeshId, bool>
{
    /// <summary>Invalid sentinel.</summary>
    public static MeshId Invalid => new(-1);

    /// <summary>Whether the index is valid.</summary>
    public bool IsValid => Value >= 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(MeshId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(MeshId left, MeshId right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(MeshId left, MeshId right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(MeshId left, MeshId right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(MeshId left, MeshId right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Nominal material index.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct MaterialId(int Value) :
    IEquatable<MaterialId>,
    IComparable<MaterialId>,
    IComparisonOperators<MaterialId, MaterialId, bool>
{
    /// <summary>Invalid sentinel.</summary>
    public static MaterialId Invalid => new(-1);

    /// <summary>Whether the index is valid.</summary>
    public bool IsValid => Value >= 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(MaterialId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(MaterialId left, MaterialId right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(MaterialId left, MaterialId right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(MaterialId left, MaterialId right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(MaterialId left, MaterialId right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Nominal texture index.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct TextureId(int Value) :
    IEquatable<TextureId>,
    IComparable<TextureId>,
    IComparisonOperators<TextureId, TextureId, bool>
{
    /// <summary>Invalid sentinel.</summary>
    public static TextureId Invalid => new(-1);

    /// <summary>Whether the index is valid.</summary>
    public bool IsValid => Value >= 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(TextureId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(TextureId left, TextureId right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(TextureId left, TextureId right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(TextureId left, TextureId right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(TextureId left, TextureId right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Nominal sampler index.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SamplerId(int Value) :
    IEquatable<SamplerId>,
    IComparable<SamplerId>,
    IComparisonOperators<SamplerId, SamplerId, bool>
{
    /// <summary>Invalid sentinel.</summary>
    public static SamplerId Invalid => new(-1);

    /// <summary>Whether the index is valid.</summary>
    public bool IsValid => Value >= 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(SamplerId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(SamplerId left, SamplerId right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(SamplerId left, SamplerId right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(SamplerId left, SamplerId right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(SamplerId left, SamplerId right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Nominal image index.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ImageId(int Value) :
    IEquatable<ImageId>,
    IComparable<ImageId>,
    IComparisonOperators<ImageId, ImageId, bool>
{
    /// <summary>Invalid sentinel.</summary>
    public static ImageId Invalid => new(-1);

    /// <summary>Whether the index is valid.</summary>
    public bool IsValid => Value >= 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(ImageId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(ImageId left, ImageId right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(ImageId left, ImageId right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(ImageId left, ImageId right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(ImageId left, ImageId right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Nominal skin index.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SkinId(int Value) :
    IEquatable<SkinId>,
    IComparable<SkinId>,
    IComparisonOperators<SkinId, SkinId, bool>
{
    /// <summary>Invalid sentinel.</summary>
    public static SkinId Invalid => new(-1);

    /// <summary>Whether the index is valid.</summary>
    public bool IsValid => Value >= 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(SkinId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(SkinId left, SkinId right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(SkinId left, SkinId right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(SkinId left, SkinId right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(SkinId left, SkinId right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Nominal animation index.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct AnimationId(int Value) :
    IEquatable<AnimationId>,
    IComparable<AnimationId>,
    IComparisonOperators<AnimationId, AnimationId, bool>
{
    /// <summary>Invalid sentinel.</summary>
    public static AnimationId Invalid => new(-1);

    /// <summary>Whether the index is valid.</summary>
    public bool IsValid => Value >= 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(AnimationId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(AnimationId left, AnimationId right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(AnimationId left, AnimationId right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(AnimationId left, AnimationId right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(AnimationId left, AnimationId right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Nominal node index.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct NodeId(int Value) :
    IEquatable<NodeId>,
    IComparable<NodeId>,
    IComparisonOperators<NodeId, NodeId, bool>
{
    /// <summary>Invalid sentinel.</summary>
    public static NodeId Invalid => new(-1);

    /// <summary>Whether the index is valid.</summary>
    public bool IsValid => Value >= 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(NodeId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(NodeId left, NodeId right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(NodeId left, NodeId right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(NodeId left, NodeId right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(NodeId left, NodeId right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Nominal scene index.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SceneId(int Value) :
    IEquatable<SceneId>,
    IComparable<SceneId>,
    IComparisonOperators<SceneId, SceneId, bool>
{
    /// <summary>Invalid sentinel.</summary>
    public static SceneId Invalid => new(-1);

    /// <summary>Whether the index is valid.</summary>
    public bool IsValid => Value >= 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(SceneId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(SceneId left, SceneId right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(SceneId left, SceneId right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(SceneId left, SceneId right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(SceneId left, SceneId right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Axis-aligned mesh bounds in model space.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct GltfMeshBounds(Vec3 Min, Vec3 Max)
{
    /// <summary>Empty (inverted) bounds.</summary>
    public static GltfMeshBounds Empty => new(
        new Vec3(float.PositiveInfinity),
        new Vec3(float.NegativeInfinity));

    /// <summary>Returns bounds expanded to include a point.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public GltfMeshBounds Encapsulate(in Vec3 point) =>
        new(Vec3.Min(Min, point), Vec3.Max(Max, point));
}

/// <summary>KHR_texture_transform payload.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct GltfTextureTransform(Vec2 Offset, Vec2 Scale, float Rotation, int TexCoord)
{
    /// <summary>Identity transform.</summary>
    public static GltfTextureTransform Default => new(Vec2.Zero, Vec2.One, 0.0f, 0);
}

/// <summary>Runtime sampler parameters (raw glTF filter/wrap values).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct GltfTextureSampler(int MagFilter, int MinFilter, int WrapS, int WrapT);

/// <summary>Interleaved vertex attribute layout.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct GltfVertexAttribute(
    int Offset,
    int Stride,
    int ComponentType,
    GltfMeshSemantic Semantic,
    byte ComponentCount,
    bool Normalized,
    bool Integer);
