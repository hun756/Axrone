namespace Axrone.AssetGltf;

/// <summary>Zero-allocation duck-typed span enumerator.</summary>
/// <typeparam name="T">Element type.</typeparam>
public ref struct DuckSpanEnumerator<T>
{
    private readonly ReadOnlySpan<T> _span;
    private int _index;

    /// <summary>Creates an enumerator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DuckSpanEnumerator(ReadOnlySpan<T> span)
    {
        _span = span;
        _index = -1;
    }

    /// <summary>Advances to the next element.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        _index++;
        return (uint)_index < (uint)_span.Length;
    }

    /// <summary>Current element.</summary>
    public ref readonly T Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _span[_index];
    }

    /// <summary>Returns this enumerator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DuckSpanEnumerator<T> GetEnumerator() => this;
}

/// <summary>glTF accessor decoding (component types, sparse merge).</summary>
internal static class GltfAccessorRuntime
{
    /// <summary>Byte size of a glTF component type.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetComponentByteSize(int componentType) =>
        componentType switch
        {
            5120 or 5121 => 1,
            5122 or 5123 => 2,
            5125 or 5126 => 4,
            _ => 1,
        };

    /// <summary>Element count of a glTF accessor type string.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetTypeElementCount(string type) =>
        type switch
        {
            "SCALAR" => 1,
            "VEC2" => 2,
            "VEC3" => 3,
            "VEC4" => 4,
            "MAT2" => 4,
            "MAT3" => 9,
            "MAT4" => 16,
            _ => 1,
        };

    /// <summary>Validates that an accessor fits inside its bufferView.</summary>
    public static void ValidateAccessorRange(
        GltfRawModel model,
        int accessorIndex,
        int componentCount)
    {
        GltfRawModel.Accessor accessor = model.Accessors[accessorIndex];
        if (accessor.BufferView < 0)
        {
            return;
        }

        if (accessor.BufferView >= model.BufferViews.Count)
        {
            ThrowHelper.ThrowAccessor(accessorIndex, "references a missing bufferView.");
        }

        GltfRawModel.BufferView bufferView = model.BufferViews[accessor.BufferView];
        int elementSize = componentCount * GetComponentByteSize(accessor.ComponentType);
        if (elementSize <= 0 || accessor.Count < 0 || accessor.ByteOffset < 0)
        {
            ThrowHelper.ThrowAccessor(accessorIndex, "has invalid element metadata.");
        }

        long stride = bufferView.ByteStride > 0 ? bufferView.ByteStride : elementSize;
        if (stride < elementSize)
        {
            ThrowHelper.ThrowAccessor(accessorIndex, "has a byteStride smaller than one element.");
        }

        long requiredEnd = accessor.Count == 0
            ? accessor.ByteOffset
            : accessor.ByteOffset + ((long)(accessor.Count - 1) * stride) + elementSize;

        if (requiredEnd > bufferView.ByteLength)
        {
            ThrowHelper.ThrowAccessor(
                accessorIndex,
                $"exceeds its bufferView bounds ({requiredEnd} > {bufferView.ByteLength}).");
        }
    }

    /// <summary>Decodes accessor bytes into floats (normalized when requested).</summary>
    public static void DecodeFloats(
        ReadOnlySpan<byte> source,
        int count,
        int componentType,
        int componentCount,
        int byteStride,
        bool normalized,
        Span<float> destination)
    {
        int compSize = GetComponentByteSize(componentType);
        int elemStride = byteStride > 0 ? byteStride : compSize * componentCount;

        nuint uCount = (nuint)count;
        nuint uCompCount = (nuint)componentCount;
        nuint uElemStride = (nuint)elemStride;
        nuint uCompSize = (nuint)compSize;

        for (nuint i = 0; i < uCount; i++)
        {
            nuint elemOffset = i * uElemStride;
            for (nuint c = 0; c < uCompCount; c++)
            {
                int offset = (int)(elemOffset + (c * uCompSize));

                float val = componentType switch
                {
                    5126 => BinaryPrimitives.ReadSingleLittleEndian(source.Slice(offset, 4)),
                    5120 => normalized ? Math.Max(-1.0f, (sbyte)source[offset] / 127.0f) : (sbyte)source[offset],
                    5121 => normalized ? source[offset] / 255.0f : source[offset],
                    5122 => normalized
                        ? Math.Max(-1.0f, BinaryPrimitives.ReadInt16LittleEndian(source.Slice(offset, 2)) / 32767.0f)
                        : BinaryPrimitives.ReadInt16LittleEndian(source.Slice(offset, 2)),
                    5123 => normalized
                        ? BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(offset, 2)) / 65535.0f
                        : BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(offset, 2)),
                    5125 => normalized
                        ? BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, 4)) / 4294967295.0f
                        : BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, 4)),
                    _ => 0.0f,
                };

                destination[(int)((i * uCompCount) + c)] = val;
            }
        }
    }

    /// <summary>Validates that every index references an existing vertex.</summary>
    public static void ValidateIndices(ReadOnlySpan<uint> indices, int vertexCount, int accessorIndex)
    {
        uint count = (uint)vertexCount;
        nuint length = (nuint)indices.Length;

        for (nuint i = 0; i < length; i++)
        {
            uint index = indices[(int)i];
            if (index >= count)
            {
                ThrowHelper.ThrowAccessor(
                    accessorIndex,
                    $"index {index} exceeds the vertex count {vertexCount}.");
            }
        }
    }

    /// <summary>Decodes index bytes into uint32 indices.</summary>
    public static void DecodeIndices(
        ReadOnlySpan<byte> source,
        int count,
        int componentType,
        int byteStride,
        Span<uint> destination)
    {
        int compSize = GetComponentByteSize(componentType);
        int stride = byteStride > 0 ? byteStride : compSize;

        nuint uCount = (nuint)count;
        nuint uStride = (nuint)stride;

        for (nuint i = 0; i < uCount; i++)
        {
            int offset = (int)(i * uStride);
            destination[(int)i] = componentType switch
            {
                5121 => source[offset],
                5123 => BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(offset, 2)),
                5125 => BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, 4)),
                _ => 0u,
            };
        }
    }

    /// <summary>Merges sparse accessor deltas into a dense float buffer.</summary>
    public static void MergeSparseFloats(
        int accessorIndex,
        ReadOnlySpan<byte> indicesBytes,
        int indicesComponentType,
        ReadOnlySpan<byte> valuesBytes,
        int sparseCount,
        int destinationElementCount,
        int componentType,
        int componentCount,
        bool normalized,
        Span<float> destination)
    {
        if (indicesComponentType is not (5121 or 5123 or 5125))
        {
            ThrowHelper.ThrowAccessor(
                accessorIndex,
                "sparse indices must use UNSIGNED_BYTE, UNSIGNED_SHORT, or UNSIGNED_INT components.");
        }

        if (sparseCount < 0 || sparseCount > destinationElementCount)
        {
            ThrowHelper.ThrowAccessor(
                accessorIndex,
                $"sparse count {sparseCount} exceeds the accessor element count {destinationElementCount}.");
        }

        int indexCompSize = GetComponentByteSize(indicesComponentType);
        int valueCompSize = GetComponentByteSize(componentType);
        int valueElemSize = valueCompSize * componentCount;

        nuint uSparseCount = (nuint)sparseCount;
        nuint uIndexCompSize = (nuint)indexCompSize;
        nuint uValueElemSize = (nuint)valueElemSize;
        nuint uValueCompSize = (nuint)valueCompSize;
        nuint uCompCount = (nuint)componentCount;

        for (nuint i = 0; i < uSparseCount; i++)
        {
            int indexOffset = (int)(i * uIndexCompSize);
            uint targetIndex = indicesComponentType switch
            {
                5121 => indicesBytes[indexOffset],
                5123 => BinaryPrimitives.ReadUInt16LittleEndian(indicesBytes.Slice(indexOffset, 2)),
                5125 => BinaryPrimitives.ReadUInt32LittleEndian(indicesBytes.Slice(indexOffset, 4)),
                _ => 0u,
            };

            if (targetIndex >= (uint)destinationElementCount)
            {
                ThrowHelper.ThrowAccessor(
                    accessorIndex,
                    $"sparse index {targetIndex} exceeds the accessor element count {destinationElementCount}.");
            }

            int valueOffset = (int)(i * uValueElemSize);
            ReadOnlySpan<byte> elemValueBytes = valuesBytes.Slice(valueOffset, valueElemSize);

            for (nuint c = 0; c < uCompCount; c++)
            {
                int compOffset = (int)(c * uValueCompSize);
                float val = componentType switch
                {
                    5126 => BinaryPrimitives.ReadSingleLittleEndian(elemValueBytes.Slice(compOffset, 4)),
                    5120 => normalized ? Math.Max(-1.0f, (sbyte)elemValueBytes[compOffset] / 127.0f) : (sbyte)elemValueBytes[compOffset],
                    5121 => normalized ? elemValueBytes[compOffset] / 255.0f : elemValueBytes[compOffset],
                    5122 => normalized
                        ? Math.Max(-1.0f, BinaryPrimitives.ReadInt16LittleEndian(elemValueBytes.Slice(compOffset, 2)) / 32767.0f)
                        : BinaryPrimitives.ReadInt16LittleEndian(elemValueBytes.Slice(compOffset, 2)),
                    5123 => normalized
                        ? BinaryPrimitives.ReadUInt16LittleEndian(elemValueBytes.Slice(compOffset, 2)) / 65535.0f
                        : BinaryPrimitives.ReadUInt16LittleEndian(elemValueBytes.Slice(compOffset, 2)),
                    5125 => normalized
                        ? BinaryPrimitives.ReadUInt32LittleEndian(elemValueBytes.Slice(compOffset, 4)) / 4294967295.0f
                        : BinaryPrimitives.ReadUInt32LittleEndian(elemValueBytes.Slice(compOffset, 4)),
                    _ => 0.0f,
                };

                destination[(int)((targetIndex * (uint)uCompCount) + (uint)c)] = val;
            }
        }
    }
}

/// <summary>Mesh topology expansion, index downcast and bounds.</summary>
internal static class GltfMeshPipeline
{
    /// <summary>Expands glTF primitive modes into a triangle/line/point index list.</summary>
    public static uint[] ExpandTopology(int mode, ReadOnlySpan<uint> sourceIndices, int vertexFallbackCount)
    {
        uint[] source = sourceIndices.ToArray();
        if (source.Length == 0)
        {
            if (vertexFallbackCount < 0)
            {
                ThrowHelper.ThrowTopology(mode, "Negative vertex fallback count.");
            }

            source = new uint[vertexFallbackCount];
            for (uint i = 0; i < (uint)vertexFallbackCount; i++)
            {
                source[i] = i;
            }
        }

        switch (mode)
        {
            case 0:
            case 1:
            case 4:
                return source;

            case 2:
                {
                    if (source.Length < 2)
                    {
                        return Array.Empty<uint>();
                    }

                    var output = GC.AllocateUninitializedArray<uint>(source.Length * 2);
                    int dst = 0;
                    for (int i = 0; i < source.Length; i++)
                    {
                        output[dst++] = source[i];
                        output[dst++] = source[(i + 1) % source.Length];
                    }

                    return output;
                }

            case 3:
                {
                    if (source.Length < 2)
                    {
                        return Array.Empty<uint>();
                    }

                    var output = GC.AllocateUninitializedArray<uint>((source.Length - 1) * 2);
                    int dst = 0;
                    for (int i = 0; i < source.Length - 1; i++)
                    {
                        output[dst++] = source[i];
                        output[dst++] = source[i + 1];
                    }

                    return output;
                }

            case 5:
                {
                    if (source.Length < 3)
                    {
                        return Array.Empty<uint>();
                    }

                    var output = GC.AllocateUninitializedArray<uint>((source.Length - 2) * 3);
                    int dst = 0;
                    for (int i = 0; i < source.Length - 2; i++)
                    {
                        if ((i & 1) == 0)
                        {
                            output[dst++] = source[i];
                            output[dst++] = source[i + 1];
                            output[dst++] = source[i + 2];
                        }
                        else
                        {
                            output[dst++] = source[i + 1];
                            output[dst++] = source[i];
                            output[dst++] = source[i + 2];
                        }
                    }

                    return output;
                }

            case 6:
                {
                    if (source.Length < 3)
                    {
                        return Array.Empty<uint>();
                    }

                    var output = GC.AllocateUninitializedArray<uint>((source.Length - 2) * 3);
                    int dst = 0;
                    uint root = source[0];
                    for (int i = 1; i < source.Length - 1; i++)
                    {
                        output[dst++] = root;
                        output[dst++] = source[i];
                        output[dst++] = source[i + 1];
                    }

                    return output;
                }

            default:
                ThrowHelper.ThrowTopology(mode, "Unrecognized glTF topology primitive mode.");
                return Array.Empty<uint>();
        }
    }

    /// <summary>Downcasts uint32 indices to the smallest wire width.</summary>
    public static (ReadOnlyMemory<byte> IndexBytes, bool Is16Bit) DowncastIndices(ReadOnlySpan<uint> indices)
    {
        uint maxVal = 0;
        nuint len = (nuint)indices.Length;
        for (nuint i = 0; i < len; i++)
        {
            if (indices[(int)i] > maxVal)
            {
                maxVal = indices[(int)i];
            }
        }

        if (maxVal <= 65535)
        {
            byte[] buffer = GC.AllocateUninitializedArray<byte>(indices.Length * 2, pinned: true);
            Span<ushort> target = MemoryMarshal.Cast<byte, ushort>(buffer.AsSpan());
            for (nuint i = 0; i < len; i++)
            {
                target[(int)i] = (ushort)indices[(int)i];
            }

            return (buffer, true);
        }
        else
        {
            byte[] buffer = GC.AllocateUninitializedArray<byte>(indices.Length * 4, pinned: true);
            Span<uint> target = MemoryMarshal.Cast<byte, uint>(buffer.AsSpan());
            indices.CopyTo(target);
            return (buffer, false);
        }
    }

    /// <summary>Computes axis-aligned bounds over a packed position buffer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static GltfMeshBounds ComputeBounds(ReadOnlySpan<float> positions)
    {
        GltfMeshBounds bounds = GltfMeshBounds.Empty;
        nuint vertexSpan = (nuint)positions.Length;
        for (nuint i = 0; i < vertexSpan; i += 3)
        {
            bounds = bounds.Encapsulate(new Vec3(positions[(int)i], positions[(int)(i + 1)], positions[(int)(i + 2)]));
        }

        return bounds;
    }
}
