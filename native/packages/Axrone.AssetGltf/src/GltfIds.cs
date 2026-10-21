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
