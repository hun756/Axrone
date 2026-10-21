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

        /// <summary>Alpha cutoff.</summary>
        public float AlphaCutoff { get; set; } = 0.5f;

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
}
