namespace Axrone.Asset;

/// <summary>False-sharing-proof lifecycle counters.</summary>
[StructLayout(LayoutKind.Explicit, Size = 128)]
internal struct CachePaddedCounters
{
    /// <summary>Completed upserts.</summary>
    [FieldOffset(0)]
    public long Upserts;

    /// <summary>Completed deletes.</summary>
    [FieldOffset(64)]
    public long Deletes;
}

/// <summary>Database construction parameters.</summary>
public sealed class AssetDatabaseOptions
{
    /// <summary>Default locale.</summary>
    public string Locale
    {
        get;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : "invariant";
    } = "invariant";

    /// <summary>Revision history budget per asset.</summary>
    public int MaxRevisions
    {
        get;
        init => field = value > 0 ? value : 32;
    } = 32;

    /// <summary>Inline threshold in bytes.</summary>
    public int InlineThresholdBytes
    {
        get;
        init => field = value >= 0 ? value : 65536;
    } = 65536;

    /// <summary>Binary persistence mode.</summary>
    public AssetBinaryPersistenceMode BinaryMode { get; init; } = AssetBinaryPersistenceMode.Auto;

    /// <summary>External binary store.</summary>
    public IBinaryStore? BinaryStore { get; init; }

    /// <summary>Millisecond clock.</summary>
    public Func<long>? Clock { get; init; }

    /// <summary>Message resolver hook.</summary>
    public Func<string, string>? MessageResolver { get; init; }
}
