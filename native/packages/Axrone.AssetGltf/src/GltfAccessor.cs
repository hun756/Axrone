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
        ReadOnlySpan<byte> indicesBytes,
        int indicesComponentType,
        ReadOnlySpan<byte> valuesBytes,
        int sparseCount,
        int componentType,
        int componentCount,
        bool normalized,
        Span<float> destination)
    {
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
