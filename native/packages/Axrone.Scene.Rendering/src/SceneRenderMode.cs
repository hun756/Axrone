namespace Axrone.Scene.Rendering;

/// <summary>Render paths active for the planned frame.</summary>
public enum SceneRenderMode
{
    /// <summary>No camera is available.</summary>
    None = 0,

    /// <summary>3D meshes only.</summary>
    ThreeD = 1,

    /// <summary>2D sprites only.</summary>
    TwoD = 2,

    /// <summary>Both 3D meshes and 2D sprites.</summary>
    Mixed = 3
}

/// <summary>
/// Item source consumed by backend render orchestrators. Implementations
/// collect, cull and sort per pass; backends only submit what the source
/// returns.
/// </summary>
public interface ISceneFrameItemSource
{
    /// <summary>Resolved lighting for the current frame, or null for 2D frames.</summary>
    LightingSelectionState? Lighting { get; }

    /// <summary>Collects and returns mesh items for one pass.</summary>
    ReadOnlySpan<SceneRenderItem> CollectMeshes(SceneAssetId<RenderPassAssetTag> passId);

    /// <summary>Collects and returns sprite items for one pass.</summary>
    ReadOnlySpan<SceneSpriteRenderItem> CollectSprites(SceneAssetId<RenderPassAssetTag> passId);
}
