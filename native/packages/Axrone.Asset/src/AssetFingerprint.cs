namespace Axrone.Asset;

using Axrone.Hash;

/// <summary>
/// Content fingerprint: <c>fp:</c> plus the lowercase hex FNV-1a 32-bit hash
/// of <c>kind|key|data|metadata</c>. Data and metadata arrive pre-serialized
/// in stable form; the codec layer owns that canonicalization.
/// </summary>
public static class AssetFingerprintComputer
{
    private const int StackLimit = 256;

    /// <summary>Computes a fingerprint from stable parts.</summary>
    public static AssetFingerprint Compute(
        in AssetKind kind,
        in AssetKey key,
        string stableDataJson,
        string stableMetadataJson)
    {
        ArgumentNullException.ThrowIfNull(stableDataJson);
        ArgumentNullException.ThrowIfNull(stableMetadataJson);

        string joined = string.Create(
            CultureInfo.InvariantCulture,
            $"{kind.Value}|{key.Value}|{stableDataJson}|{stableMetadataJson}");

        int byteCount = System.Text.Encoding.UTF8.GetByteCount(joined);
        if (byteCount <= StackLimit)
        {
            Span<byte> small = stackalloc byte[StackLimit];
            Span<byte> target = small[..byteCount];
            System.Text.Encoding.UTF8.GetBytes(joined, target);
            return new AssetFingerprint("fp:" + Fnv1a32Algorithm.Hash(target).ToString());
        }

        byte[] rented = System.Buffers.ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            Span<byte> target = rented.AsSpan(0, byteCount);
            System.Text.Encoding.UTF8.GetBytes(joined, target);
            return new AssetFingerprint("fp:" + Fnv1a32Algorithm.Hash(target).ToString());
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
