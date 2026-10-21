namespace Axrone.AssetGltf;

/// <summary>Resource limits applied before decoding untrusted glTF input.</summary>
public sealed class GltfImportLimits
{
    /// <summary>Default limits for typical editor and runtime imports.</summary>
    public static GltfImportLimits Default { get; } = new();

    /// <summary>Maximum source payload size in bytes.</summary>
    public int MaxSourceBytes { get; init; } = 256 * 1024 * 1024;

    /// <summary>Maximum sum of declared buffer byte lengths.</summary>
    public long MaxBufferBytes { get; init; } = 2L * 1024 * 1024 * 1024;

    /// <summary>Maximum number of accessors.</summary>
    public int MaxAccessorCount { get; init; } = 16_777_216;

    /// <summary>Maximum number of nodes.</summary>
    public int MaxNodeCount { get; init; } = 1_000_000;

    /// <summary>Maximum number of meshes.</summary>
    public int MaxMeshCount { get; init; } = 1_000_000;

    /// <summary>Maximum number of textures.</summary>
    public int MaxTextureCount { get; init; } = 65_536;

    /// <summary>Maximum estimated decoded bytes across accessors and interleaved mesh output.</summary>
    public long MaxDecodedBytes { get; init; } = 2L * 1024 * 1024 * 1024;
}

/// <summary>Preflight limit validation for untrusted glTF documents.</summary>
internal static class GltfLimitsValidator
{
    /// <summary>Validates document counts and estimated decode budgets.</summary>
    public static void Validate(int sourceLength, GltfRawModel model, GltfImportLimits limits)
    {
        if (sourceLength > limits.MaxSourceBytes)
        {
            ThrowHelper.ThrowLimit("MaxSourceBytes", sourceLength, limits.MaxSourceBytes);
        }

        if (model.Accessors.Count > limits.MaxAccessorCount)
        {
            ThrowHelper.ThrowLimit("MaxAccessorCount", model.Accessors.Count, limits.MaxAccessorCount);
        }

        if (model.Nodes.Count > limits.MaxNodeCount)
        {
            ThrowHelper.ThrowLimit("MaxNodeCount", model.Nodes.Count, limits.MaxNodeCount);
        }

        if (model.Meshes.Count > limits.MaxMeshCount)
        {
            ThrowHelper.ThrowLimit("MaxMeshCount", model.Meshes.Count, limits.MaxMeshCount);
        }

        if (model.Textures.Count > limits.MaxTextureCount)
        {
            ThrowHelper.ThrowLimit("MaxTextureCount", model.Textures.Count, limits.MaxTextureCount);
        }

        long bufferBytes = 0;
        for (int i = 0; i < model.Buffers.Count; i++)
        {
            int byteLength = model.Buffers[i].ByteLength;
            if (byteLength > 0)
            {
                bufferBytes = AddChecked("MaxBufferBytes", bufferBytes, byteLength, limits.MaxBufferBytes);
            }
        }

        long decodedBytes = 0;
        for (int i = 0; i < model.Accessors.Count; i++)
        {
            GltfRawModel.Accessor accessor = model.Accessors[i];
            long bytes = (long)accessor.Count * GltfAccessorRuntime.GetTypeElementCount(accessor.Type) * 4;
            if (accessor.Sparse != null)
            {
                bytes += (long)accessor.Sparse.Count * GltfAccessorRuntime.GetTypeElementCount(accessor.Type) * 4;
            }

            decodedBytes = AddChecked("MaxDecodedBytes", decodedBytes, bytes, limits.MaxDecodedBytes);
        }

        for (int i = 0; i < model.Meshes.Count; i++)
        {
            GltfRawModel.Mesh mesh = model.Meshes[i];
            for (int p = 0; p < mesh.Primitives.Count; p++)
            {
                GltfRawModel.Primitive primitive = mesh.Primitives[p];
                int vertexCount = 0;
                long stride = 0;
                foreach (KeyValuePair<string, int> attribute in primitive.Attributes)
                {
                    if (attribute.Value < 0 || attribute.Value >= model.Accessors.Count)
                    {
                        continue;
                    }

                    GltfRawModel.Accessor accessor = model.Accessors[attribute.Value];
                    vertexCount = Math.Max(vertexCount, accessor.Count);
                    stride += GltfAccessorRuntime.GetTypeElementCount(accessor.Type) * 4;
                }

                decodedBytes = AddChecked(
                    "MaxDecodedBytes",
                    decodedBytes,
                    (long)vertexCount * stride,
                    limits.MaxDecodedBytes);

                if (primitive.Indices.HasValue &&
                    primitive.Indices.Value >= 0 &&
                    primitive.Indices.Value < model.Accessors.Count)
                {
                    decodedBytes = AddChecked(
                        "MaxDecodedBytes",
                        decodedBytes,
                        (long)model.Accessors[primitive.Indices.Value].Count * 4,
                        limits.MaxDecodedBytes);
                }

                for (int t = 0; t < primitive.Targets.Count; t++)
                {
                    foreach (KeyValuePair<string, int> target in primitive.Targets[t])
                    {
                        if (target.Value < 0 || target.Value >= model.Accessors.Count)
                        {
                            continue;
                        }

                        GltfRawModel.Accessor accessor = model.Accessors[target.Value];
                        decodedBytes = AddChecked(
                            "MaxDecodedBytes",
                            decodedBytes,
                            (long)accessor.Count * GltfAccessorRuntime.GetTypeElementCount(accessor.Type) * 4,
                            limits.MaxDecodedBytes);
                    }
                }
            }
        }
    }

    private static long AddChecked(string limit, long current, long addition, long maximum)
    {
        if (addition > 0 && current > long.MaxValue - addition)
        {
            ThrowHelper.ThrowLimit(limit, long.MaxValue, maximum);
        }

        long next = current + addition;
        if (next > maximum)
        {
            ThrowHelper.ThrowLimit(limit, next, maximum);
        }

        return next;
    }
}
