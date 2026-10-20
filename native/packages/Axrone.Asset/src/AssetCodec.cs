namespace Axrone.Asset;

/// <summary>Codec contract: format tag, size bound, span-based round trip.</summary>
public interface IAssetCodecPolicy<TData>
{
    /// <summary>Format tag, e.g. "binary".</summary>
    static abstract string Format { get; }

    /// <summary>Upper bound for one serialized payload.</summary>
    static abstract int MaxSerializedBytes(in TData data);

    /// <summary>Serializes into the destination.</summary>
    static abstract bool TrySerialize(in TData data, Span<byte> destination, out int bytesWritten);

    /// <summary>Deserializes from a buffer.</summary>
    static abstract bool TryDeserialize(ReadOnlySpan<byte> source, out TData data);
}

/// <summary>Byte array codec: identity copy.</summary>
public readonly struct ByteArrayCodecPolicy : IAssetCodecPolicy<byte[]>
{
    /// <inheritdoc/>
    public static string Format => "binary";

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MaxSerializedBytes(in byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return data.Length;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TrySerialize(in byte[] data, Span<byte> destination, out int bytesWritten)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (destination.Length < data.Length)
        {
            bytesWritten = 0;
            return false;
        }

        data.CopyTo(destination);
        bytesWritten = data.Length;
        return true;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryDeserialize(ReadOnlySpan<byte> source, out byte[] data)
    {
        data = source.ToArray();
        return true;
    }
}

/// <summary>UTF-8 string codec.</summary>
public readonly struct Utf8StringCodecPolicy : IAssetCodecPolicy<string>
{
    /// <inheritdoc/>
    public static string Format => "text";

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MaxSerializedBytes(in string data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Encoding.UTF8.GetMaxByteCount(data.Length);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TrySerialize(in string data, Span<byte> destination, out int bytesWritten)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Encoding.UTF8.TryGetBytes(data, destination, out bytesWritten);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryDeserialize(ReadOnlySpan<byte> source, out string data)
    {
        data = Encoding.UTF8.GetString(source);
        return true;
    }
}

/// <summary>Blittable binary codec.</summary>
public readonly struct UnmanagedBinaryCodecPolicy<T> : IAssetCodecPolicy<T> where T : unmanaged
{
    /// <inheritdoc/>
    public static string Format => "unmanaged";

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int MaxSerializedBytes(in T data) => Unsafe.SizeOf<T>();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TrySerialize(in T data, Span<byte> destination, out int bytesWritten)
    {
        if (destination.Length < Unsafe.SizeOf<T>())
        {
            bytesWritten = 0;
            return false;
        }

        MemoryMarshal.Write(destination, in data);
        bytesWritten = Unsafe.SizeOf<T>();
        return true;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryDeserialize(ReadOnlySpan<byte> source, out T data)
    {
        if (source.Length < Unsafe.SizeOf<T>())
        {
            data = default;
            return false;
        }

        data = MemoryMarshal.Read<T>(source);
        return true;
    }
}
