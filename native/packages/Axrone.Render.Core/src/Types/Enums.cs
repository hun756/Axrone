namespace Axrone.Render.Core;

/// <summary>
/// Render pass kinds defining the rendering pipeline stages.
/// Values correspond to priority ordering in frame graph compilation.
/// </summary>
public enum PassKind : byte
{
    /// <summary>Depth pre-pass for early-Z optimization.</summary>
    DepthPrepass = 0,

    /// <summary>Shadow map rendering pass.</summary>
    Shadow = 1,

    /// <summary>Opaque geometry rendering pass.</summary>
    Opaque = 2,

    /// <summary>Transparent geometry rendering pass (alpha blended).</summary>
    Transparent = 3,

    /// <summary>Skybox rendering pass.</summary>
    Skybox = 4,

    /// <summary>Reflection probe rendering pass.</summary>
    ReflectionProbe = 5,

    /// <summary>Global illumination rendering pass.</summary>
    GlobalIllumination = 6,

    /// <summary>Volumetric effects rendering pass.</summary>
    Volumetric = 7,

    /// <summary>Tone mapping pass (HDR to LDR conversion).</summary>
    Tonemap = 8,

    /// <summary>Post-processing effects pass.</summary>
    PostProcess = 9,

    /// <summary>Final presentation pass (blit to backbuffer).</summary>
    Present = 10,

    /// <summary>Light baking pass.</summary>
    LightBake = 11,

    /// <summary>Unknown or custom pass kind.</summary>
    Unknown = 255
}

/// <summary>
/// Color space specifications for texture sampling and rendering.
/// </summary>
public enum ColorSpace : byte
{
    /// <summary>Linear color space (no gamma correction).</summary>
    Linear = 0,

    /// <summary>sRGB color space (gamma-corrected).</summary>
    SRGB = 1,

    /// <summary>HDR10 color space (not yet implemented).</summary>
    HDR10 = 2,

    /// <summary>Rec. 2020 color space (not yet implemented).</summary>
    Rec2020 = 3
}

/// <summary>
/// Vertex attribute semantic locations matching GLSL layout qualifiers.
/// Values are the attribute location indices (0-14).
/// </summary>
public enum VertexSemantic : uint
{
    /// <summary>Vertex position (vec3).</summary>
    Position = 0,

    /// <summary>Vertex normal (vec3).</summary>
    Normal = 1,

    /// <summary>Vertex tangent (vec4, w = handedness).</summary>
    Tangent = 2,

    /// <summary>Texture coordinates (vec2).</summary>
    TexCoord = 3,

    /// <summary>Vertex color (vec4).</summary>
    Color = 4,

    /// <summary>Bone joint indices (uvec4).</summary>
    Joints = 5,

    /// <summary>Bone weights (vec4).</summary>
    Weights = 6,

    /// <summary>Custom attribute slot 0.</summary>
    Custom0 = 11,

    /// <summary>Custom attribute slot 1.</summary>
    Custom1 = 12,

    /// <summary>Custom attribute slot 2.</summary>
    Custom2 = 13,

    /// <summary>Custom attribute slot 3.</summary>
    Custom3 = 14
}

/// <summary>
/// Texture dimension types.
/// </summary>
public enum TextureDimension : byte
{
    /// <summary>1D texture (not widely supported).</summary>
    Texture1D = 0,

    /// <summary>2D texture.</summary>
    Texture2D = 1,

    /// <summary>3D volume texture.</summary>
    Texture3D = 2,

    /// <summary>Cube map texture (6 faces).</summary>
    TextureCube = 3,

    /// <summary>2D texture array.</summary>
    Texture2DArray = 4,

    /// <summary>Cube map array (not yet implemented).</summary>
    TextureCubeArray = 5
}

/// <summary>
/// Texture filtering modes.
/// </summary>
public enum FilterMode : byte
{
    /// <summary>Nearest neighbor filtering (pixelated).</summary>
    Nearest = 0,

    /// <summary>Linear filtering (smooth).</summary>
    Linear = 1,

    /// <summary>Nearest mipmap nearest.</summary>
    NearestMipmapNearest = 2,

    /// <summary>Linear mipmap nearest.</summary>
    LinearMipmapNearest = 3,

    /// <summary>Nearest mipmap linear.</summary>
    NearestMipmapLinear = 4,

    /// <summary>Linear mipmap linear (trilinear).</summary>
    LinearMipmapLinear = 5
}

/// <summary>
/// Texture wrapping modes.
/// </summary>
public enum WrapMode : byte
{
    /// <summary>Clamp to edge.</summary>
    ClampToEdge = 0,

    /// <summary>Clamp to border.</summary>
    ClampToBorder = 1,

    /// <summary>Repeat (tile).</summary>
    Repeat = 2,

    /// <summary>Mirrored repeat.</summary>
    MirroredRepeat = 3
}

/// <summary>
/// Texture usage hints for GPU optimization.
/// </summary>
public enum TextureUsage : byte
{
    /// <summary>Static texture (uploaded once, read many times).</summary>
    Static = 0,

    /// <summary>Dynamic texture (updated frequently).</summary>
    Dynamic = 1,

    /// <summary>Stream texture (updated every frame).</summary>
    Stream = 2,

    /// <summary>Render target texture.</summary>
    RenderTarget = 3,

    /// <summary>Depth buffer texture.</summary>
    DepthBuffer = 4,

    /// <summary>Compute shader storage texture.</summary>
    Compute = 5
}

/// <summary>
/// Buffer usage hints for GPU optimization.
/// </summary>
public enum BufferUsage : byte
{
    /// <summary>Static draw (uploaded once, drawn many times).</summary>
    StaticDraw = 0,

    /// <summary>Dynamic draw (updated frequently, drawn many times).</summary>
    DynamicDraw = 1,

    /// <summary>Stream draw (updated every frame, drawn many times).</summary>
    StreamDraw = 2,

    /// <summary>Static read (uploaded once, read back frequently).</summary>
    StaticRead = 3,

    /// <summary>Dynamic read (updated frequently, read back frequently).</summary>
    DynamicRead = 4,

    /// <summary>Stream read (updated every frame, read back frequently).</summary>
    StreamRead = 5,

    /// <summary>Static copy (uploaded once, copied frequently).</summary>
    StaticCopy = 6,

    /// <summary>Dynamic copy (updated frequently, copied frequently).</summary>
    DynamicCopy = 7,

    /// <summary>Stream copy (updated every frame, copied frequently).</summary>
    StreamCopy = 8
}

/// <summary>
/// Primitive topology for drawing.
/// </summary>
public enum PrimitiveTopology : byte
{
    /// <summary>Point list.</summary>
    Points = 0,

    /// <summary>Line list.</summary>
    Lines = 1,

    /// <summary>Line strip.</summary>
    LineStrip = 2,

    /// <summary>Triangle list.</summary>
    Triangles = 3,

    /// <summary>Triangle strip.</summary>
    TriangleStrip = 4,

    /// <summary>Triangle fan.</summary>
    TriangleFan = 5,

    /// <summary>Line list with adjacency.</summary>
    LinesAdjacency = 6,

    /// <summary>Line strip with adjacency.</summary>
    LineStripAdjacency = 7,

    /// <summary>Triangle list with adjacency.</summary>
    TrianglesAdjacency = 8,

    /// <summary>Triangle strip with adjacency.</summary>
    TriangleStripAdjacency = 9,

    /// <summary>Patches (for tessellation).</summary>
    Patches = 10
}

/// <summary>
/// Index buffer data types.
/// </summary>
public enum IndexType : byte
{
    /// <summary>Unsigned 8-bit integer (not widely supported).</summary>
    UInt8 = 0,

    /// <summary>Unsigned 16-bit integer.</summary>
    UInt16 = 1,

    /// <summary>Unsigned 32-bit integer.</summary>
    UInt32 = 2
}

/// <summary>
/// Blend modes for transparency.
/// </summary>
public enum BlendMode : byte
{
    /// <summary>No blending (opaque).</summary>
    Opaque = 0,

    /// <summary>Standard alpha blending.</summary>
    AlphaBlend = 1,

    /// <summary>Additive blending.</summary>
    Additive = 2,

    /// <summary>Multiplicative blending.</summary>
    Multiply = 3,

    /// <summary>Screen blending.</summary>
    Screen = 4,

    /// <summary>Overlay blending.</summary>
    Overlay = 5
}

/// <summary>
/// Face culling modes.
/// </summary>
public enum CullMode : byte
{
    /// <summary>No culling.</summary>
    None = 0,

    /// <summary>Cull front faces.</summary>
    Front = 1,

    /// <summary>Cull back faces.</summary>
    Back = 2,

    /// <summary>Cull front and back faces.</summary>
    FrontAndBack = 3
}

/// <summary>
/// Depth comparison functions.
/// </summary>
public enum DepthFunc : byte
{
    /// <summary>Never pass.</summary>
    Never = 0,

    /// <summary>Pass if less than.</summary>
    Less = 1,

    /// <summary>Pass if equal.</summary>
    Equal = 2,

    /// <summary>Pass if less than or equal.</summary>
    LessEqual = 3,

    /// <summary>Pass if greater than.</summary>
    Greater = 4,

    /// <summary>Pass if not equal.</summary>
    NotEqual = 5,

    /// <summary>Pass if greater than or equal.</summary>
    GreaterEqual = 6,

    /// <summary>Always pass.</summary>
    Always = 7
}
