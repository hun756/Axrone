namespace Axrone.Scene.Assets;

/// <summary>Framebuffer clear operations requested by a render pass.</summary>
[Flags]
public enum SceneClearMask : byte
{
    /// <summary>Do not clear.</summary>
    None = 0,

    /// <summary>Clear color attachments.</summary>
    Color = 1 << 0,

    /// <summary>Clear depth.</summary>
    Depth = 1 << 1
}

/// <summary>
/// Immutable render-pass definition: ordering, clear state and optional state
/// overrides. Backend mapping happens in the rendering assembly.
/// </summary>
public sealed class SceneRenderPassDefinition : ISceneAssetDefinition<RenderPassAssetTag>
{
    /// <summary>Creates a render-pass definition.</summary>
    public SceneRenderPassDefinition(
        SceneAssetId<RenderPassAssetTag> id,
        int order = 0,
        bool enabled = true,
        SceneClearMask clearMask = SceneClearMask.None,
        Vec4? clearColor = null,
        float? clearDepth = null,
        bool? depthTest = null,
        bool? cull = null,
        bool? blend = null)
    {
        if (!id.IsValid)
        {
            throw new SceneAssetException(
                SceneAssetErrorCodes.Invalid,
                "Render-pass definitions require a non-empty id.");
        }

        Id = id;
        Order = order;
        Enabled = enabled;
        ClearMask = clearMask;
        ClearColor = clearColor;
        ClearDepth = clearDepth;
        DepthTest = depthTest;
        Cull = cull;
        Blend = blend;
    }

    /// <inheritdoc/>
    public SceneAssetId<RenderPassAssetTag> Id { get; }

    /// <summary>Sort order; lower values execute first.</summary>
    public int Order { get; }

    /// <summary>Whether the pass participates in the frame.</summary>
    public bool Enabled { get; }

    /// <summary>Requested clear operations.</summary>
    public SceneClearMask ClearMask { get; }

    /// <summary>Optional clear color.</summary>
    public Vec4? ClearColor { get; }

    /// <summary>Optional clear depth.</summary>
    public float? ClearDepth { get; }

    /// <summary>Optional depth-test override.</summary>
    public bool? DepthTest { get; }

    /// <summary>Optional face-culling override.</summary>
    public bool? Cull { get; }

    /// <summary>Optional blending override.</summary>
    public bool? Blend { get; }
}
