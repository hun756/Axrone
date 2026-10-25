using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Axrone.AssetGltf;

namespace Axrone.AssetGltf.Codecs;

/// <summary>
/// Native Draco mesh decoder policy for <c>KHR_draco_mesh_compression</c>.
/// Backed by the official google/draco C++ sources (Apache-2.0) through a thin
/// C ABI bridge built per RID from <c>native/extern/draco-bridge</c>.
/// Behaves like <see cref="NoDracoCompressionPolicy"/> when the native library
/// for the current RID is absent, so the core importer keeps working.
/// </summary>
public readonly struct NativeDracoMeshPolicy : IDracoMeshPolicy
{
    /// <summary>
    /// Whether the RID-specific native library is present, so a required
    /// <c>KHR_draco_mesh_compression</c> payload can actually be decoded.
    /// </summary>
    public static bool IsSupported => NativeDracoInterop.IsNativeLibraryAvailable;

    /// <summary>Whether the RID-specific native library was found next to the app.</summary>
    public static bool IsNativeLibraryAvailable => NativeDracoInterop.IsNativeLibraryAvailable;

    /// <inheritdoc/>
    public static unsafe GltfDracoDecodeStatus TryDecodeMesh(
        ReadOnlySpan<byte> dracoBytes,
        GltfDracoDecodeLimits limits,
        [NotNullWhen(true)] out GltfDracoDecodedMesh? mesh,
        out long decodedBytes)
    {
        mesh = null;
        decodedBytes = 0;
        if (dracoBytes.IsEmpty)
        {
            return GltfDracoDecodeStatus.InvalidData;
        }

        nint handle = 0;
        try
        {
            fixed (byte* data = dracoBytes)
            {
                if (NativeDracoInterop.Decode(data, (nuint)dracoBytes.Length, out handle) != NativeDracoInterop.Ok ||
                    handle == 0)
                {
                    return GltfDracoDecodeStatus.InvalidData;
                }
            }

            uint points = NativeDracoInterop.PointCount(handle);
            uint faces = NativeDracoInterop.FaceCount(handle);
            if (points == 0 || points > int.MaxValue)
            {
                return GltfDracoDecodeStatus.InvalidData;
            }

            int vertexCount = (int)points;
            int attributeCount = NativeDracoInterop.AttributeCount(handle);
            if (attributeCount <= 0)
            {
                return GltfDracoDecodeStatus.InvalidData;
            }

            long indexLong = (long)faces * 3;
            if (indexLong > int.MaxValue)
            {
                return GltfDracoDecodeStatus.InvalidData;
            }

            // Pass 1: measure the decoded buffers before allocating anything.
            long required = indexLong * sizeof(uint);
            if (required > limits.MaxDecodedBytes)
            {
                decodedBytes = required;
                return GltfDracoDecodeStatus.LimitExceeded;
            }

            var uniqueIds = new int[attributeCount];
            var componentCounts = new int[attributeCount];
            var dataTypes = new GltfDracoAttributeDataType[attributeCount];
            for (int i = 0; i < attributeCount; i++)
            {
                if (NativeDracoInterop.AttributeInfo(
                        handle, i, out uint uniqueId, out int components, out _, out int dataType) != NativeDracoInterop.Ok ||
                    components <= 0 ||
                    components > 4 ||
                    uniqueId > int.MaxValue)
                {
                    return GltfDracoDecodeStatus.Unsupported;
                }

                long attributeBytes = (long)vertexCount * components * sizeof(float);
                if (attributeBytes > limits.MaxDecodedBytes - required)
                {
                    decodedBytes = required + attributeBytes;
                    return GltfDracoDecodeStatus.LimitExceeded;
                }

                required += attributeBytes;
                uniqueIds[i] = (int)uniqueId;
                componentCounts[i] = components;
                dataTypes[i] = dataType is >= 0 and <= (int)GltfDracoAttributeDataType.Other
                    ? (GltfDracoAttributeDataType)dataType
                    : GltfDracoAttributeDataType.Other;
            }

            // Pass 2: allocate and pull the values.
            var attributes = ImmutableDictionary.CreateBuilder<int, float[]>();
            var attributeDataTypes = ImmutableDictionary.CreateBuilder<int, GltfDracoAttributeDataType>();
            for (int i = 0; i < attributeCount; i++)
            {
                int components = componentCounts[i];
                int floatCount = vertexCount * components;
                float[] values = new float[floatCount];
                fixed (float* destination = values)
                {
                    if (NativeDracoInterop.AttributeFloats(handle, i, destination, floatCount) != NativeDracoInterop.Ok)
                    {
                        return GltfDracoDecodeStatus.Unsupported;
                    }
                }

                attributes[uniqueIds[i]] = values;
                attributeDataTypes[uniqueIds[i]] = dataTypes[i];
            }

            int indexCount = (int)indexLong;
            uint[] indices = new uint[indexCount];
            if (indexCount > 0)
            {
                fixed (uint* destination = indices)
                {
                    if (NativeDracoInterop.FaceIndices(handle, destination, indexCount) != NativeDracoInterop.Ok)
                    {
                        return GltfDracoDecodeStatus.InvalidData;
                    }
                }
            }

            mesh = new GltfDracoDecodedMesh
            {
                VertexCount = vertexCount,
                Attributes = attributes.ToImmutable(),
                AttributeDataTypes = attributeDataTypes.ToImmutable(),
                Indices = indices.ToImmutableArray(),
            };

            decodedBytes = required;
            return GltfDracoDecodeStatus.Success;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            mesh = null;
            return GltfDracoDecodeStatus.InvalidData;
        }
        finally
        {
            if (handle != 0)
            {
                try
                {
                    NativeDracoInterop.FreeMesh(handle);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    // Best effort: the decode result is already decided.
                }
            }
        }
    }
}
