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
}
