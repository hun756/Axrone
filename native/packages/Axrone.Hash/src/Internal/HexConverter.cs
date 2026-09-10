namespace Axrone.Hash;

internal static class HexConverter
{
    private static ReadOnlySpan<byte> LowercaseLookup => "0123456789abcdef"u8;
    private static ReadOnlySpan<byte> UppercaseLookup => "0123456789ABCDEF"u8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryFormat(ReadOnlySpan<byte> bytes, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format)
    {
        int required = bytes.Length * 2;
        if (destination.Length < required) { charsWritten = 0; return false; }

        bool upper = format.Length > 0 && format[0] == 'X';
        ReadOnlySpan<byte> lookup = upper ? UppercaseLookup : LowercaseLookup;

        ref char dstRef = ref MemoryMarshal.GetReference(destination);
        ref byte srcRef = ref MemoryMarshal.GetReference(bytes);
        ref byte lkpRef = ref MemoryMarshal.GetReference(lookup);

        for (int i = 0; i < bytes.Length; i++)
        {
            byte val = Unsafe.Add(ref srcRef, i);
            Unsafe.Add(ref dstRef, i * 2) = (char)Unsafe.Add(ref lkpRef, val >> 4);
            Unsafe.Add(ref dstRef, (i * 2) + 1) = (char)Unsafe.Add(ref lkpRef, val & 0x0F);
        }

        charsWritten = required;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryFormatUtf8(ReadOnlySpan<byte> bytes, Span<byte> destination, out int bytesWritten, ReadOnlySpan<char> format)
    {
        int required = bytes.Length * 2;
        if (destination.Length < required) { bytesWritten = 0; return false; }

        bool upper = format.Length > 0 && format[0] == 'X';
        ReadOnlySpan<byte> lookup = upper ? UppercaseLookup : LowercaseLookup;

        ref byte dstRef = ref MemoryMarshal.GetReference(destination);
        ref byte srcRef = ref MemoryMarshal.GetReference(bytes);
        ref byte lkpRef = ref MemoryMarshal.GetReference(lookup);

        for (int i = 0; i < bytes.Length; i++)
        {
            byte val = Unsafe.Add(ref srcRef, i);
            Unsafe.Add(ref dstRef, i * 2) = Unsafe.Add(ref lkpRef, val >> 4);
            Unsafe.Add(ref dstRef, (i * 2) + 1) = Unsafe.Add(ref lkpRef, val & 0x0F);
        }

        bytesWritten = required;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<char> hex, Span<byte> destination, out int bytesWritten)
    {
        if ((hex.Length & 1) != 0 || destination.Length < hex.Length / 2) { bytesWritten = 0; return false; }

        int targetLen = hex.Length / 2;
        for (int i = 0; i < targetLen; i++)
        {
            int hi = FromHexChar(hex[i * 2]);
            int lo = FromHexChar(hex[(i * 2) + 1]);
            if ((hi | lo) < 0) { bytesWritten = 0; return false; }
            destination[i] = (byte)((hi << 4) | lo);
        }

        bytesWritten = targetLen;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseUtf8(ReadOnlySpan<byte> utf8Hex, Span<byte> destination, out int bytesWritten)
    {
        if ((utf8Hex.Length & 1) != 0 || destination.Length < utf8Hex.Length / 2) { bytesWritten = 0; return false; }

        int targetLen = utf8Hex.Length / 2;
        for (int i = 0; i < targetLen; i++)
        {
            int hi = FromHexByte(utf8Hex[i * 2]);
            int lo = FromHexByte(utf8Hex[(i * 2) + 1]);
            if ((hi | lo) < 0) { bytesWritten = 0; return false; }
            destination[i] = (byte)((hi << 4) | lo);
        }

        bytesWritten = targetLen;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FromHexChar(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FromHexByte(byte b) => b switch
    {
        >= (byte)'0' and <= (byte)'9' => b - (byte)'0',
        >= (byte)'a' and <= (byte)'f' => b - (byte)'a' + 10,
        >= (byte)'A' and <= (byte)'F' => b - (byte)'A' + 10,
        _ => -1
    };
}

internal static class Utf8EncodingHelper
{
    public static void AppendUtf8<TAccumulator, TDigest>(
        ref TAccumulator accumulator,
        ReadOnlySpan<char> source)
        where TAccumulator : struct, IHashAccumulator<TAccumulator, TDigest>
        where TDigest : unmanaged, IHashDigest<TDigest>
    {
        if (source.IsEmpty) return;

        int maxBytes = Encoding.UTF8.GetMaxByteCount(source.Length);
        if (maxBytes <= 512)
        {
            Span<byte> buffer = stackalloc byte[maxBytes];
            int written = Encoding.UTF8.GetBytes(source, buffer);
            accumulator.Append(buffer[..written]);
            return;
        }

        byte[] poolBuffer = ArrayPool<byte>.Shared.Rent(maxBytes);
        try
        {
            int written = Encoding.UTF8.GetBytes(source, poolBuffer);
            accumulator.Append(poolBuffer.AsSpan(0, written));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(poolBuffer);
        }
    }

    public static void AppendUtf8(IncrementalHash hash, ReadOnlySpan<char> source)
    {
        if (source.IsEmpty) return;

        int maxBytes = Encoding.UTF8.GetMaxByteCount(source.Length);
        if (maxBytes <= 512)
        {
            Span<byte> buffer = stackalloc byte[maxBytes];
            int written = Encoding.UTF8.GetBytes(source, buffer);
            hash.AppendData(buffer[..written]);
            return;
        }

        byte[] poolBuffer = ArrayPool<byte>.Shared.Rent(maxBytes);
        try
        {
            int written = Encoding.UTF8.GetBytes(source, poolBuffer);
            hash.AppendData(poolBuffer.AsSpan(0, written));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(poolBuffer);
        }
    }
}
