using System.Buffers;
using System.Buffers.Text;
using Base64Status = System.Buffers.Text.Base64;

namespace Axrone.Utility.Base64;

/// <summary>
/// High-performance Base64 encoding/decoding with URL-safe alphabet support.
/// </summary>
/// <remarks>
/// <para>Wraps <see cref="System.Buffers.Text.Base64"/> for standard encoding
/// and provides a custom URL-safe implementation.</para>
/// <para>All methods operate on <see cref="ReadOnlySpan{T}"/> / <see cref="Span{T}"/> — zero allocation on hot paths.</para>
/// </remarks>
public static class Base64Helper
{
    /// <summary>Standard Base64 alphabet: A-Z, a-z, 0-9, +, /.</summary>
    public const string StandardAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

    /// <summary>URL-safe Base64 alphabet: A-Z, a-z, 0-9, -, _.</summary>
    public const string UrlSafeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    /// <summary>Calculate the encoded length for a given input length (with padding).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetEncodedLength(int byteLength) =>
        byteLength == 0 ? 0 : ((byteLength + 2) / 3) * 4;

    /// <summary>Calculate the maximum decoded length for a given encoded length.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetMaxDecodedLength(int encodedLength) =>
        encodedLength == 0 ? 0 : (encodedLength / 4) * 3;

    /// <summary>Encode bytes to standard Base64 string.</summary>
    public static string Encode(ReadOnlySpan<byte> data) => Convert.ToBase64String(data);

    /// <summary>Encode bytes to standard Base64, writing into a destination span.</summary>
    public static OperationStatus Encode(ReadOnlySpan<byte> source, Span<byte> destination, out int bytesWritten) =>
        Base64Status.EncodeToUtf8(source, destination, out _, out bytesWritten);

    /// <summary>Decode standard Base64 string to bytes.</summary>
    public static byte[] Decode(string base64) => Convert.FromBase64String(base64);

    /// <summary>Decode standard Base64 UTF-8 bytes to binary.</summary>
    public static OperationStatus Decode(ReadOnlySpan<byte> source, Span<byte> destination, out int bytesWritten) =>
        Base64Status.DecodeFromUtf8(source, destination, out _, out bytesWritten);

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

            for (var i = 0; i < span.Length; i++)
            {
                if (span[i] == (byte)'+') span[i] = (byte)'-';
                else if (span[i] == (byte)'/') span[i] = (byte)'_';
                else if (span[i] == (byte)'=') { span = span[..i]; break; }
            }

            return System.Text.Encoding.ASCII.GetString(span);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

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

            for (var i = 0; i < span.Length; i++)
            {
                if (span[i] == '-') span[i] = '+';
                else if (span[i] == '_') span[i] = '/';
            }

            return Convert.FromBase64CharArray(buffer, 0, chars.Length);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

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
}
