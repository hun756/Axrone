namespace Axrone.Asset;

/// <summary>Binary persistence mode.</summary>
public enum AssetBinaryPersistenceMode : byte
{
    /// <summary>Inline small payloads, externalize large ones.</summary>
    Auto = 0,

    /// <summary>Always inline as base64.</summary>
    Inline = 1,

    /// <summary>Always externalize through the binary store.</summary>
    External = 2,
}

/// <summary>External binary blob store.</summary>
public interface IBinaryStore
{
    /// <summary>Writes bytes, returns a storage key.</summary>
    ValueTask<string> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);

    /// <summary>Reads bytes by storage key.</summary>
    ValueTask<byte[]> ReadAsync(string storageKey, CancellationToken ct = default);
}

/// <summary>In-memory binary store.</summary>
public sealed class InMemoryBinaryStore : IBinaryStore
{
    private readonly ConcurrentDictionary<string, byte[]> _store = new(StringComparer.Ordinal);
    private long _counter;

    /// <inheritdoc/>
    public ValueTask<string> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        string key = string.Create(
            CultureInfo.InvariantCulture,
            $"bin_{Interlocked.Increment(ref _counter):x16}_{Guid.NewGuid():N}");
        _store[key] = data.ToArray();
        return ValueTask.FromResult(key);
    }

    /// <inheritdoc/>
    public ValueTask<byte[]> ReadAsync(string storageKey, CancellationToken ct = default)
    {
        if (_store.TryGetValue(storageKey, out byte[]? value))
        {
            return ValueTask.FromResult(value);
        }

        throw new FileNotFoundException($"Binary key not found: {storageKey}.", storageKey);
    }
}

/// <summary>Binary persistence tuning.</summary>
public sealed class AssetBinaryPersistenceOptions
{
    /// <summary>Inline versus external strategy.</summary>
    public AssetBinaryPersistenceMode Mode { get; init; } = AssetBinaryPersistenceMode.Auto;

    /// <summary>Inline threshold in bytes.</summary>
    public int InlineThresholdBytes { get; init; } = 65536;

    /// <summary>External store; defaults to an in-memory store.</summary>
    public IBinaryStore? Store { get; init; }
}

/// <summary>Binary envelope for snapshots.</summary>
public sealed record AssetBinaryValue
{
    /// <summary>Discriminator.</summary>
    [JsonPropertyName("__asset")]
    public string AssetMarker { get; init; } = "axrone.binary";

    /// <summary>Storage kind.</summary>
    [JsonPropertyName("storage")]
    public required string Storage { get; init; }

    /// <summary>Encoding for inline payloads.</summary>
    [JsonPropertyName("encoding")]
    public string? Encoding { get; init; }

    /// <summary>Base64 payload for inline storage.</summary>
    [JsonPropertyName("data")]
    public string? Data { get; init; }

    /// <summary>Storage key for external blobs.</summary>
    [JsonPropertyName("storageKey")]
    public string? StorageKey { get; init; }

    /// <summary>Byte length.</summary>
    [JsonPropertyName("byteLength")]
    public required int ByteLength { get; init; }
}
