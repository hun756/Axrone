using System.Buffers;
using System.Buffers.Text;

namespace Axrone.Utility.Base64;

/// <summary>
/// High-performance Base64 encoding/decoding with URL-safe alphabet support.
/// </summary>
/// <remarks>
/// <para>Thin wrapper over <see cref="System.Buffers.Text.Base64"/> (SIMD-accelerated, AVX-512 on modern hardware)
/// that adds what the BCL lacks: URL-safe alphabet, <see cref="IBufferWriter{T}"/> streaming, and a unified API surface.</para>
/// <para>All span-based methods are zero-allocation on hot paths.</para>
/// </remarks>
public static class Base64Helper
{
    /// <summary>Standard Base64 alphabet: A-Z, a-z, 0-9, +, /.</summary>
    public const string StandardAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

    /// <summary>URL-safe Base64 alphabet: A-Z, a-z, 0-9, -, _.</summary>
    public const string UrlSafeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    // ─── Length Calculation ──────────────────────────────────────────

    /// <summary>Calculate the encoded length for a given input length (with padding).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetEncodedLength(int byteLength) =>
        byteLength == 0 ? 0 : ((byteLength + 2) / 3) * 4;

    /// <summary>Calculate the maximum decoded length for a given encoded length.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetMaxDecodedLength(int encodedLength) =>
        encodedLength == 0 ? 0 : (encodedLength / 4) * 3;

    // ─── Standard Encode ────────────────────────────────────────────

    /// <summary>Encode bytes to standard Base64 string.</summary>
    public static string Encode(ReadOnlySpan<byte> data) =>
        Convert.ToBase64String(data);

    /// <summary>Encode bytes to standard Base64 string (explicit alias for <see cref="Encode(ReadOnlySpan{byte})"/>).</summary>
    public static string EncodeToString(ReadOnlySpan<byte> data) =>
        Convert.ToBase64String(data);

    /// <summary>Encode bytes to standard Base64 UTF-8, writing into a destination span.</summary>
    public static OperationStatus Encode(ReadOnlySpan<byte> source, Span<byte> destination, out int bytesWritten) =>
        System.Buffers.Text.Base64.EncodeToUtf8(source, destination, out _, out bytesWritten);

    /// <summary>Encode bytes to standard Base64, writing to an <see cref="IBufferWriter{T}"/>.</summary>
    public static void EncodeToWriter(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (source.IsEmpty) return;

        var encodedLength = GetEncodedLength(source.Length);
        var span = writer.GetSpan(encodedLength);
        System.Buffers.Text.Base64.EncodeToUtf8(source, span, out _, out int written);
        writer.Advance(written);
    }

    // ─── Standard Decode ────────────────────────────────────────────

    /// <summary>Decode standard Base64 string to bytes.</summary>
    public static byte[] Decode(string base64) =>
        Convert.FromBase64String(base64);

    /// <summary>Decode standard Base64 UTF-8 bytes to binary.</summary>
    public static OperationStatus Decode(ReadOnlySpan<byte> source, Span<byte> destination, out int bytesWritten) =>
        System.Buffers.Text.Base64.DecodeFromUtf8(source, destination, out _, out bytesWritten);

    // ─── URL-Safe Encode ────────────────────────────────────────────

    /// <summary>Encode bytes to URL-safe Base64 string (no padding).</summary>
    public static string EncodeUrlSafe(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return string.Empty;

        var encodedLength = GetEncodedLength(data.Length);
        var buffer = ArrayPool<byte>.Shared.Rent(encodedLength);
        try
        {
            System.Buffers.Text.Base64.EncodeToUtf8(data, buffer, out _, out var written);
            var span = buffer.AsSpan(0, written);

            ReplaceStandardToUrl(span, out var trimmedLength);
            return System.Text.Encoding.ASCII.GetString(span[..trimmedLength]);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Encode bytes to URL-safe Base64 UTF-8, writing into a destination span (no padding).</summary>
    /// <returns>The number of bytes written to <paramref name="destination"/>.</returns>
    public static int EncodeUrlSafe(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (source.IsEmpty) return 0;

        System.Buffers.Text.Base64.EncodeToUtf8(source, destination, out _, out int written);
        ReplaceStandardToUrl(destination[..written], out var trimmedLength);
        return trimmedLength;
    }

    /// <summary>Encode bytes to URL-safe Base64, writing to an <see cref="IBufferWriter{T}"/> (no padding).</summary>
    public static void EncodeUrlSafeToWriter(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (source.IsEmpty) return;

        var encodedLength = GetEncodedLength(source.Length);
        var span = writer.GetSpan(encodedLength);
        System.Buffers.Text.Base64.EncodeToUtf8(source, span, out _, out int written);
        ReplaceStandardToUrl(span[..written], out var trimmedLength);
        writer.Advance(trimmedLength);
    }

    // ─── URL-Safe Decode ────────────────────────────────────────────

    /// <summary>Decode URL-safe Base64 string to bytes.</summary>
    public static byte[] DecodeUrlSafe(string input)
    {
        if (string.IsNullOrEmpty(input)) return [];

        var padded = (input.Length % 4) switch
        {
            2 => input + "==",
            3 => input + "=",
            _ => input
        };

        var chars = padded.AsSpan();
        var buffer = ArrayPool<char>.Shared.Rent(chars.Length);
        try
        {
            chars.CopyTo(buffer);
            var span = buffer.AsSpan(0, chars.Length);
            ReplaceUrlToStandard(span);
            return Convert.FromBase64CharArray(buffer, 0, chars.Length);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    /// <summary>Decode URL-safe Base64 UTF-8 bytes to binary.</summary>
    /// <returns>The number of bytes written to <paramref name="destination"/>, or 0 on invalid input.</returns>
    public static int DecodeUrlSafe(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (source.IsEmpty) return 0;

        // Calculate padding needed
        var remainder = source.Length % 4;
        var paddedLength = remainder == 0 ? source.Length : source.Length + (4 - remainder);

        var buffer = ArrayPool<byte>.Shared.Rent(paddedLength);
        try
        {
            source.CopyTo(buffer);
            var span = buffer.AsSpan(0, paddedLength);

            // Replace URL-safe chars back to standard
            for (int i = 0; i < source.Length; i++)
            {
                if (span[i] == (byte)'-') span[i] = (byte)'+';
                else if (span[i] == (byte)'_') span[i] = (byte)'/';
            }

            // Add padding
            for (int i = source.Length; i < paddedLength; i++)
            {
                span[i] = (byte)'=';
            }

            var status = System.Buffers.Text.Base64.DecodeFromUtf8(span, destination, out _, out int written);
            return status == OperationStatus.Done ? written : 0;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    // ─── Validation ─────────────────────────────────────────────────

    /// <summary>Validate whether a string is valid standard Base64.</summary>
    public static bool IsValid(string input)
    {
        if (string.IsNullOrEmpty(input)) return true;
        if (input.Length % 4 != 0) return false;

        var maxDecoded = (input.Length / 4) * 3;
        var buffer = ArrayPool<byte>.Shared.Rent(maxDecoded);
        try
        {
            return Convert.TryFromBase64String(input, buffer, out _);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    // ─── Internal Helpers ───────────────────────────────────────────

    /// <summary>Replace +/ with -_ in-place and trim padding.</summary>
    private static void ReplaceStandardToUrl(Span<byte> span, out int trimmedLength)
    {
        trimmedLength = span.Length;
        for (int i = 0; i < span.Length; i++)
        {
            switch (span[i])
            {
                case (byte)'+': span[i] = (byte)'-'; break;
                case (byte)'/': span[i] = (byte)'_'; break;
                case (byte)'=': trimmedLength = i; return;
            }
        }
    }

    /// <summary>Replace -_ with +/ in-place (char span for string decode path).</summary>
    private static void ReplaceUrlToStandard(Span<char> span)
    {
        for (int i = 0; i < span.Length; i++)
        {
            switch (span[i])
            {
                case '-': span[i] = '+'; break;
                case '_': span[i] = '/'; break;
            }
        }
    }
}
