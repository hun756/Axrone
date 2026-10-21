namespace Axrone.AssetGltf;

/// <summary>Raw glTF schema model (parsed JSON, unresolved).</summary>
public sealed class GltfRawModel
{
    /// <summary>Raw buffer descriptor.</summary>
    public sealed class Buffer
    {
        /// <summary>Declared byte length.</summary>
        public int ByteLength { get; set; }

        /// <summary>External or data URI, null for GLB chunk.</summary>
        public string? Uri { get; set; }
    }

    /// <summary>EXT_meshopt_compression bufferView extension.</summary>
    public sealed class BufferViewMeshoptExtension
    {
        /// <summary>Source buffer index.</summary>
        public int Buffer { get; set; }

        /// <summary>Byte offset into the source buffer.</summary>
        public int ByteOffset { get; set; }

        /// <summary>Compressed byte length.</summary>
        public int ByteLength { get; set; }

        /// <summary>Decompressed byte stride.</summary>
        public int ByteStride { get; set; }

        /// <summary>Element count.</summary>
        public int Count { get; set; }

        /// <summary>Compression mode.</summary>
        public string Mode { get; set; } = "ATTRIBUTES";
    }

    /// <summary>Raw bufferView descriptor.</summary>
    public sealed class BufferView
    {
        /// <summary>Buffer index.</summary>
        public int Buffer { get; set; }

        /// <summary>Byte offset into the buffer.</summary>
        public int ByteOffset { get; set; }

        /// <summary>Byte length.</summary>
        public int ByteLength { get; set; }

        /// <summary>Byte stride.</summary>
        public int ByteStride { get; set; }

        /// <summary>GL target hint.</summary>
        public int Target { get; set; }

        /// <summary>Meshopt extension payload.</summary>
        public BufferViewMeshoptExtension? Meshopt { get; set; }
    }

    /// <summary>Sparse accessor indices.</summary>
    public sealed class SparseIndices
    {
        /// <summary>BufferView index.</summary>
        public int BufferView { get; set; }

        /// <summary>Byte offset.</summary>
        public int ByteOffset { get; set; }

        /// <summary>Component type.</summary>
        public int ComponentType { get; set; }
    }

    /// <summary>Sparse accessor values.</summary>
    public sealed class SparseValues
    {
        /// <summary>BufferView index.</summary>
        public int BufferView { get; set; }

        /// <summary>Byte offset.</summary>
        public int ByteOffset { get; set; }
    }

    /// <summary>Sparse accessor patch.</summary>
    public sealed class AccessorSparse
    {
        /// <summary>Patched element count.</summary>
        public int Count { get; set; }

        /// <summary>Index stream.</summary>
        public SparseIndices Indices { get; set; } = new();

        /// <summary>Value stream.</summary>
        public SparseValues Values { get; set; } = new();
    }

    /// <summary>Raw accessor descriptor.</summary>
    public sealed class Accessor
    {
        /// <summary>BufferView index (-1 when empty).</summary>
        public int BufferView { get; set; } = -1;

        /// <summary>Byte offset into the bufferView.</summary>
        public int ByteOffset { get; set; }

        /// <summary>Component type.</summary>
        public int ComponentType { get; set; }

        /// <summary>Whether integer data is normalized.</summary>
        public bool Normalized { get; set; }

        /// <summary>Element count.</summary>
        public int Count { get; set; }

        /// <summary>Accessor type string.</summary>
        public string Type { get; set; } = "SCALAR";

        /// <summary>Declared minimums.</summary>
        public float[]? Min { get; set; }

        /// <summary>Declared maximums.</summary>
        public float[]? Max { get; set; }

        /// <summary>Sparse patch.</summary>
        public AccessorSparse? Sparse { get; set; }
    }

    /// <summary>Accessors.</summary>
    public List<Accessor> Accessors { get; } = new();

    /// <summary>BufferViews.</summary>
    public List<BufferView> BufferViews { get; } = new();

    /// <summary>Buffers.</summary>
    public List<Buffer> Buffers { get; } = new();

    /// <summary>KHR_draco_mesh_compression primitive extension.</summary>
    public sealed class DracoPrimitiveExtension
    {
        /// <summary>BufferView index.</summary>
        public int BufferView { get; set; }

        /// <summary>Semantic to attribute-index mapping.</summary>
        public Dictionary<string, int> Attributes { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>Raw mesh primitive.</summary>
    public sealed class Primitive
    {
        /// <summary>Semantic to accessor-index mapping.</summary>
        public Dictionary<string, int> Attributes { get; } = new(StringComparer.Ordinal);

        /// <summary>Indices accessor index.</summary>
        public int? Indices { get; set; }

        /// <summary>Material index.</summary>
        public int? Material { get; set; }

        /// <summary>Topology mode (default triangles).</summary>
        public int Mode { get; set; } = 4;

        /// <summary>Morph target attribute maps.</summary>
        public List<Dictionary<string, int>> Targets { get; } = new();

        /// <summary>Draco extension payload.</summary>
        public DracoPrimitiveExtension? Draco { get; set; }
    }

    /// <summary>Raw mesh.</summary>
    public sealed class Mesh
    {
        /// <summary>Mesh name.</summary>
        public string? Name { get; set; }

        /// <summary>Primitives.</summary>
        public List<Primitive> Primitives { get; } = new();
    }

    /// <summary>Meshes.</summary>
    public List<Mesh> Meshes { get; } = new();

    /// <summary>Texture sampler binding with KHR_texture_transform.</summary>
    public sealed class TextureInfo
    {
        /// <summary>Texture index (-1 when unset).</summary>
        public int Index { get; set; } = -1;

        /// <summary>UV set.</summary>
        public int TexCoord { get; set; }

        /// <summary>Texture scale (clearcoat/occlusion strength).</summary>
        public float Scale { get; set; } = 1.0f;

        /// <summary>Texture strength.</summary>
        public float Strength { get; set; } = 1.0f;

        /// <summary>UV transform.</summary>
        public GltfTextureTransform Transform { get; set; } = GltfTextureTransform.Default;
    }

    /// <summary>PBR metallic-roughness block.</summary>
    public sealed class PbrMetallicRoughness
    {
        /// <summary>Base color factor.</summary>
        public float[]? BaseColorFactor { get; set; }

        /// <summary>Base color texture.</summary>
        public TextureInfo? BaseColorTexture { get; set; }

        /// <summary>Metallic factor.</summary>
        public float MetallicFactor { get; set; } = 1.0f;

        /// <summary>Roughness factor.</summary>
        public float RoughnessFactor { get; set; } = 1.0f;

        /// <summary>Metallic-roughness texture.</summary>
        public TextureInfo? MetallicRoughnessTexture { get; set; }
    }

    /// <summary>Raw material.</summary>
    public sealed class Material
    {
        /// <summary>Material name.</summary>
        public string? Name { get; set; }

        /// <summary>PBR block.</summary>
        public PbrMetallicRoughness Pbr { get; set; } = new();

        /// <summary>Normal texture.</summary>
        public TextureInfo? NormalTexture { get; set; }

        /// <summary>Occlusion texture.</summary>
        public TextureInfo? OcclusionTexture { get; set; }

        /// <summary>Emissive texture.</summary>
        public TextureInfo? EmissiveTexture { get; set; }

        /// <summary>Emissive factor.</summary>
        public float[]? EmissiveFactor { get; set; }

        /// <summary>Alpha mode string.</summary>
        public string AlphaMode { get; set; } = "OPAQUE";

        /// <summary>Alpha cutoff, or null when the document omits it.</summary>
        public float? AlphaCutoff { get; set; }

        /// <summary>Double-sided flag.</summary>
        public bool DoubleSided { get; set; }

        /// <summary>KHR_materials_unlit flag.</summary>
        public bool Unlit { get; set; }

        /// <summary>Clearcoat factor.</summary>
        public float ClearcoatFactor { get; set; }

        /// <summary>Clearcoat roughness factor.</summary>
        public float ClearcoatRoughnessFactor { get; set; }

        /// <summary>Clearcoat texture.</summary>
        public TextureInfo? ClearcoatTexture { get; set; }

        /// <summary>Clearcoat roughness texture.</summary>
        public TextureInfo? ClearcoatRoughnessTexture { get; set; }

        /// <summary>Clearcoat normal texture.</summary>
        public TextureInfo? ClearcoatNormalTexture { get; set; }

        /// <summary>KHR_materials_emissive_strength value.</summary>
        public float EmissiveStrength { get; set; } = 1.0f;

        /// <summary>KHR_materials_specular factor.</summary>
        public float SpecularFactor { get; set; } = 1.0f;

        /// <summary>KHR_materials_specular color factor.</summary>
        public float[]? SpecularColorFactor { get; set; }

        /// <summary>KHR_materials_specular specular texture (alpha channel).</summary>
        public TextureInfo? SpecularTexture { get; set; }

        /// <summary>KHR_materials_specular color texture (RGB channels).</summary>
        public TextureInfo? SpecularColorTexture { get; set; }

        /// <summary>KHR_materials_sheen color factor.</summary>
        public float[]? SheenColorFactor { get; set; }

        /// <summary>KHR_materials_sheen color texture.</summary>
        public TextureInfo? SheenColorTexture { get; set; }

        /// <summary>KHR_materials_sheen roughness factor.</summary>
        public float SheenRoughnessFactor { get; set; }

        /// <summary>KHR_materials_sheen roughness texture (alpha channel).</summary>
        public TextureInfo? SheenRoughnessTexture { get; set; }

        /// <summary>KHR_materials_transmission factor.</summary>
        public float TransmissionFactor { get; set; }

        /// <summary>KHR_materials_transmission texture (red channel).</summary>
        public TextureInfo? TransmissionTexture { get; set; }

        /// <summary>KHR_materials_volume thickness factor.</summary>
        public float ThicknessFactor { get; set; }

        /// <summary>KHR_materials_volume thickness texture (green channel).</summary>
        public TextureInfo? ThicknessTexture { get; set; }

        /// <summary>KHR_materials_volume attenuation distance.</summary>
        public float AttenuationDistance { get; set; } = float.PositiveInfinity;

        /// <summary>KHR_materials_volume attenuation color.</summary>
        public float[]? AttenuationColor { get; set; }

        /// <summary>KHR_materials_ior index of refraction.</summary>
        public float Ior { get; set; } = 1.5f;

        /// <summary>KHR_materials_iridescence factor.</summary>
        public float IridescenceFactor { get; set; }

        /// <summary>KHR_materials_iridescence texture.</summary>
        public TextureInfo? IridescenceTexture { get; set; }

        /// <summary>KHR_materials_iridescence index of refraction.</summary>
        public float IridescenceIor { get; set; } = 1.3f;

        /// <summary>KHR_materials_iridescence minimum thickness in nanometers.</summary>
        public float IridescenceThicknessMinimum { get; set; } = 100.0f;

        /// <summary>KHR_materials_iridescence maximum thickness in nanometers.</summary>
        public float IridescenceThicknessMaximum { get; set; } = 400.0f;

        /// <summary>KHR_materials_iridescence thickness texture (green channel).</summary>
        public TextureInfo? IridescenceThicknessTexture { get; set; }

        /// <summary>KHR_materials_anisotropy strength.</summary>
        public float AnisotropyStrength { get; set; }

        /// <summary>KHR_materials_anisotropy rotation in radians.</summary>
        public float AnisotropyRotation { get; set; }

        /// <summary>KHR_materials_anisotropy texture.</summary>
        public TextureInfo? AnisotropyTexture { get; set; }

        /// <summary>KHR_materials_dispersion strength.</summary>
        public float Dispersion { get; set; }
    }

    /// <summary>Raw texture.</summary>
    public sealed class Texture
    {
        /// <summary>Sampler index.</summary>
        public int? Sampler { get; set; }

        /// <summary>Image index.</summary>
        public int? Source { get; set; }
    }

    /// <summary>Raw image.</summary>
    public sealed class Image
    {
        /// <summary>External or data URI.</summary>
        public string? Uri { get; set; }

        /// <summary>MIME type.</summary>
        public string? MimeType { get; set; }

        /// <summary>BufferView index.</summary>
        public int? BufferView { get; set; }
    }

    /// <summary>Raw sampler.</summary>
    public sealed class Sampler
    {
        /// <summary>Magnification filter.</summary>
        public int MagFilter { get; set; } = 9729;

        /// <summary>Minification filter.</summary>
        public int MinFilter { get; set; } = 9729;

        /// <summary>S wrap mode.</summary>
        public int WrapS { get; set; } = 10497;

        /// <summary>T wrap mode.</summary>
        public int WrapT { get; set; } = 10497;
    }

    /// <summary>Materials.</summary>
    public List<Material> Materials { get; } = new();

    /// <summary>Textures.</summary>
    public List<Texture> Textures { get; } = new();

    /// <summary>Images.</summary>
    public List<Image> Images { get; } = new();

    /// <summary>Samplers.</summary>
    public List<Sampler> Samplers { get; } = new();

    /// <summary>Raw skin.</summary>
    public sealed class Skin
    {
        /// <summary>Skin name.</summary>
        public string? Name { get; set; }

        /// <summary>Inverse-bind-matrices accessor.</summary>
        public int? InverseBindMatrices { get; set; }

        /// <summary>Skeleton node.</summary>
        public int? Skeleton { get; set; }

        /// <summary>Joint node indices.</summary>
        public List<int> Joints { get; } = new();
    }

    /// <summary>Raw animation sampler.</summary>
    public sealed class AnimationSampler
    {
        /// <summary>Input (time) accessor.</summary>
        public int Input { get; set; }

        /// <summary>Interpolation string.</summary>
        public string Interpolation { get; set; } = "LINEAR";

        /// <summary>Output accessor.</summary>
        public int Output { get; set; }
    }

    /// <summary>Raw animation channel.</summary>
    public sealed class AnimationChannel
    {
        /// <summary>Sampler index.</summary>
        public int Sampler { get; set; }

        /// <summary>Target node index (-1 when unset).</summary>
        public int TargetNode { get; set; } = -1;

        /// <summary>Target path string.</summary>
        public string TargetPath { get; set; } = "translation";
    }

    /// <summary>Raw animation.</summary>
    public sealed class Animation
    {
        /// <summary>Animation name.</summary>
        public string? Name { get; set; }

        /// <summary>Samplers.</summary>
        public List<AnimationSampler> Samplers { get; } = new();

        /// <summary>Channels.</summary>
        public List<AnimationChannel> Channels { get; } = new();
    }

    /// <summary>KHR_lights_punctual light.</summary>
    public sealed class Light
    {
        /// <summary>Light name.</summary>
        public string? Name { get; set; }

        /// <summary>RGB color.</summary>
        public float[] Color { get; set; } = [1f, 1f, 1f];

        /// <summary>Intensity.</summary>
        public float Intensity { get; set; } = 1.0f;

        /// <summary>Light type string.</summary>
        public string Type { get; set; } = "directional";

        /// <summary>Range (point/spot).</summary>
        public float? Range { get; set; }

        /// <summary>Spot inner cone angle.</summary>
        public float InnerConeAngle { get; set; }

        /// <summary>Spot outer cone angle.</summary>
        public float OuterConeAngle { get; set; } = 0.785398f;
    }

    /// <summary>Raw camera.</summary>
    public sealed class Camera
    {
        /// <summary>Camera name.</summary>
        public string? Name { get; set; }

        /// <summary>Camera type string.</summary>
        public string Type { get; set; } = "perspective";

        /// <summary>Vertical field of view.</summary>
        public float Yfov { get; set; } = 0.785398f;

        /// <summary>Far plane.</summary>
        public float Zfar { get; set; } = 1000f;

        /// <summary>Near plane.</summary>
        public float Znear { get; set; } = 0.1f;

        /// <summary>Orthographic horizontal size.</summary>
        public float Xmag { get; set; } = 1.0f;

        /// <summary>Orthographic vertical size.</summary>
        public float Ymag { get; set; } = 1.0f;
    }

    /// <summary>Raw node.</summary>
    public sealed class Node
    {
        /// <summary>Node name.</summary>
        public string? Name { get; set; }

        /// <summary>Camera index.</summary>
        public int? Camera { get; set; }

        /// <summary>Child node indices.</summary>
        public List<int> Children { get; } = new();

        /// <summary>Skin index.</summary>
        public int? Skin { get; set; }

        /// <summary>Column-major transform matrix.</summary>
        public float[]? Matrix { get; set; }

        /// <summary>Mesh index.</summary>
        public int? Mesh { get; set; }

        /// <summary>Rotation quaternion.</summary>
        public float[]? Rotation { get; set; }

        /// <summary>Scale vector.</summary>
        public float[]? Scale { get; set; }

        /// <summary>Translation vector.</summary>
        public float[]? Translation { get; set; }

        /// <summary>Morph weights.</summary>
        public float[]? Weights { get; set; }

        /// <summary>Attached light index.</summary>
        public int? Light { get; set; }
    }

    /// <summary>Raw scene.</summary>
    public sealed class Scene
    {
        /// <summary>Scene name.</summary>
        public string? Name { get; set; }

        /// <summary>Root node indices.</summary>
        public List<int> Nodes { get; } = new();
    }

    /// <summary>Asset version string.</summary>
    public string Version { get; set; } = "2.0";

    /// <summary>Generator string.</summary>
    public string? Generator { get; set; }

    /// <summary>Copyright string.</summary>
    public string? Copyright { get; set; }

    /// <summary>Default scene index.</summary>
    public int DefaultScene { get; set; } = -1;

    /// <summary>Scenes.</summary>
    public List<Scene> Scenes { get; } = new();

    /// <summary>Nodes.</summary>
    public List<Node> Nodes { get; } = new();

    /// <summary>Skins.</summary>
    public List<Skin> Skins { get; } = new();

    /// <summary>Animations.</summary>
    public List<Animation> Animations { get; } = new();

    /// <summary>Lights.</summary>
    public List<Light> Lights { get; } = new();

    /// <summary>Cameras.</summary>
    public List<Camera> Cameras { get; } = new();

    /// <summary>Used extensions.</summary>
    public HashSet<string> ExtensionsUsed { get; } = new(StringComparer.Ordinal);

    /// <summary>Required extensions.</summary>
    public HashSet<string> ExtensionsRequired { get; } = new(StringComparer.Ordinal);
}
