namespace Axrone.Scene;

/// <summary>Dirty bits carried by transform payloads.</summary>
[Flags]
public enum SceneTransformDirty : byte
{
    /// <summary>No cached matrix is stale.</summary>
    None = 0,

    /// <summary>The local matrix must be rebuilt from TRS.</summary>
    Local = 1 << 0,

    /// <summary>The world matrix must be rebuilt from the parent chain.</summary>
    World = 1 << 1
}
