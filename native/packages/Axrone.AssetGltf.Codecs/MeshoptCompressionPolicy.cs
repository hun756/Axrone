using System.Runtime.InteropServices;
using Axrone.AssetGltf;
using MeshOptimizer;

namespace Axrone.AssetGltf.Codecs;

/// <summary>Native meshoptimizer decoder policy for EXT_meshopt_compression.</summary>
public readonly struct MeshoptCompressionPolicy : IMeshoptCompressionPolicy
{
    /// <inheritdoc/>
    public static bool IsSupported =>
        OperatingSystem.IsWindows() ||
        OperatingSystem.IsLinux() ||
        OperatingSystem.IsMacOS() ||
        OperatingSystem.IsAndroid();

    /// <inheritdoc/>
    public static unsafe bool TryDecompressBufferView(
        ReadOnlySpan<byte> source,
        int count,
        int byteStride,
        ReadOnlySpan<char> mode,
        ReadOnlySpan<char> filter,
        Span<byte> destination)
    {
        if (count <= 0 || byteStride <= 0 || source.IsEmpty || destination.IsEmpty)
        {
            return false;
        }

        long required = (long)count * byteStride;
        if (required > destination.Length || required > int.MaxValue)
        {
            return false;
        }

        bool attributes = mode.Equals("ATTRIBUTES", StringComparison.OrdinalIgnoreCase);
        bool triangles = mode.Equals("TRIANGLES", StringComparison.OrdinalIgnoreCase);
        bool indices = mode.Equals("INDICES", StringComparison.OrdinalIgnoreCase);
        if (!attributes && !triangles && !indices)
        {
            return false;
        }

        if (!attributes && !filter.IsEmpty &&
            !filter.Equals("NONE", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            fixed (byte* sourcePtr = source)
            fixed (byte* destinationPtr = destination)
            {
                int result = attributes
                    ? Meshopt.DecodeVertexBuffer(destinationPtr, (nuint)count, (nuint)byteStride, sourcePtr, (nuint)source.Length)
                    : triangles
                        ? Meshopt.DecodeIndexBuffer(destinationPtr, (nuint)count, (nuint)byteStride, sourcePtr, (nuint)source.Length)
                        : Meshopt.DecodeIndexSequence(destinationPtr, (nuint)count, (nuint)byteStride, sourcePtr, (nuint)source.Length);

                if (result != 0)
                {
                    return false;
                }

                if (attributes && !filter.IsEmpty)
                {
                    if (filter.Equals("OCTAHEDRAL", StringComparison.OrdinalIgnoreCase))
                    {
                        Meshopt.DecodeFilterOct(destinationPtr, (nuint)count, (nuint)byteStride);
                    }
                    else if (filter.Equals("QUATERNION", StringComparison.OrdinalIgnoreCase))
                    {
                        Meshopt.DecodeFilterQuat(destinationPtr, (nuint)count, (nuint)byteStride);
                    }
                    else if (filter.Equals("EXPONENTIAL", StringComparison.OrdinalIgnoreCase))
                    {
                        Meshopt.DecodeFilterExp(destinationPtr, (nuint)count, (nuint)byteStride);
                    }
                    else if (!filter.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        catch (Exception exception) when (
            exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return false;
        }
    }
}
