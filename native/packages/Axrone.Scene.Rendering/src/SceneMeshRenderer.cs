namespace Axrone.Scene.Rendering;

/// <summary>Built-in render-pass identities used by the single-pass scene path.</summary>
public static class SceneRenderPassIds
{
    /// <summary>Default opaque/transparent scene pass.</summary>
    public static readonly SceneAssetId<RenderPassAssetTag> Main =
        SceneAssetId<RenderPassAssetTag>.FromName("main");
}

/// <summary>
/// Mesh renderer data component. The component carries asset references and
/// queue hints; the render item collector resolves bounds and writes the
/// compact planning record.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct SceneMeshRenderer : ISceneDataComponent
{
    /// <summary>Mesh asset id.</summary>
    public SceneAssetId<MeshAssetTag> MeshId;

    /// <summary>Material asset id.</summary>
    public SceneAssetId<MaterialAssetTag> MaterialId;

    /// <summary>Render pass membership.</summary>
    public SceneAssetId<RenderPassAssetTag> PassId;

    /// <summary>Whether the renderer emits items.</summary>
    public bool Visible;

    /// <summary>Whether lighting uniforms are applied.</summary>
    public bool ReceiveLighting;

    /// <summary>Whether the item enters the transparent queue.</summary>
    public bool Transparent;

    /// <summary>Queue order bias.</summary>
    public int RenderOrder;

    /// <summary>Optional local-space bounds override.</summary>
    public BoundingSphere3? Bounds;

    /// <summary>Default renderer: visible, lit, main pass, opaque.</summary>
    public static SceneMeshRenderer Default
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new()
        {
            MeshId = default,
            MaterialId = default,
            PassId = SceneRenderPassIds.Main,
            Visible = true,
            ReceiveLighting = true,
            Transparent = false,
            RenderOrder = 0,
            Bounds = null
        };
    }
}
