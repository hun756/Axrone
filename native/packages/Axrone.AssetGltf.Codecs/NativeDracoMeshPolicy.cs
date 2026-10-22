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
    public static unsafe bool TryDecodeMesh(
        ReadOnlySpan<byte> dracoBytes,
        [NotNullWhen(true)] out GltfDracoDecodedMesh? mesh)
    {
        mesh = null;
        if (dracoBytes.IsEmpty)
        {
            return false;
        }

        nint handle = 0;
        try
        {
            fixed (byte* data = dracoBytes)
            {
                if (NativeDracoInterop.Decode(data, (nuint)dracoBytes.Length, out handle) != NativeDracoInterop.Ok ||
                    handle == 0)
                {
                    return false;
                }
            }

            uint points = NativeDracoInterop.PointCount(handle);
            uint faces = NativeDracoInterop.FaceCount(handle);
            if (points == 0 || points > int.MaxValue)
            {
                return false;
            }

            int vertexCount = (int)points;
            int attributeCount = NativeDracoInterop.AttributeCount(handle);
            if (attributeCount <= 0)
            {
                return false;
            }

            var attributes = ImmutableDictionary.CreateBuilder<int, float[]>();
            for (int i = 0; i < attributeCount; i++)
            {
                if (NativeDracoInterop.AttributeInfo(handle, i, out uint uniqueId, out int components, out _) != NativeDracoInterop.Ok ||
                    components <= 0 ||
                    components > 4 ||
                    uniqueId > int.MaxValue)
                {
                    return false;
                }

                int floatCount = checked(vertexCount * components);
                float[] values = new float[floatCount];
                fixed (float* destination = values)
                {
                    if (NativeDracoInterop.AttributeFloats(handle, i, destination, floatCount) != NativeDracoInterop.Ok)
                    {
                        return false;
                    }
                }

                attributes[(int)uniqueId] = values;
            }

            long indexLong = (long)faces * 3;
            if (indexLong > int.MaxValue)
            {
                return false;
            }

            int indexCount = (int)indexLong;
            uint[] indices = new uint[indexCount];
            if (indexCount > 0)
            {
                fixed (uint* destination = indices)
                {
                    if (NativeDracoInterop.FaceIndices(handle, destination, indexCount) != NativeDracoInterop.Ok)
                    {
                        return false;
                    }
                }
            }

            mesh = new GltfDracoDecodedMesh
            {
                VertexCount = vertexCount,
                Attributes = attributes.ToImmutable(),
                Indices = indices.ToImmutableArray(),
            };

            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            mesh = null;
            return false;
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
