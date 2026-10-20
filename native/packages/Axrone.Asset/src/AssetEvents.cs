namespace Axrone.Asset;

/// <summary>Asset event type.</summary>
public enum AssetEventType : byte
{
    /// <summary>No event.</summary>
    None = 0,

    /// <summary>Asset inserted or updated.</summary>
    Upsert = 1,

    /// <summary>Asset deleted.</summary>
    Delete = 2,

    /// <summary>Asset imported.</summary>
    Import = 3,
}

/// <summary>One asset change notice.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct AssetEvent(AssetEventType Type, AssetId Id, AssetRevision Revision, ulong KindHash);
