namespace Axrone.Scene.Rendering;

/// <summary>
/// Deterministically ordered enabled render passes. Rebuild after asset
/// registration changes; the plan itself is read-only during a frame.
/// </summary>
public sealed class SceneRenderPassPlan
{
    private readonly SceneAssetRegistry _assets;
    private SceneRenderPassDefinition[] _passes = [];

    /// <summary>Creates a plan over a scene asset registry.</summary>
    public SceneRenderPassPlan(SceneAssetRegistry assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        _assets = assets;
        Rebuild();
    }

    /// <summary>Enabled passes in execution order.</summary>
    public ReadOnlySpan<SceneRenderPassDefinition> Passes => _passes;

    /// <summary>Number of enabled passes.</summary>
    public int Count => _passes.Length;

    /// <summary>Whether the plan has no enabled passes.</summary>
    public bool IsEmpty => _passes.Length == 0;

    /// <summary>Rebuilds the ordered pass list from the asset registry.</summary>
    public void Rebuild()
    {
        SceneRenderPassDefinition[] definitions = _assets.RenderPasses.Definitions();
        int count = 0;
        for (int i = 0; i < definitions.Length; i++)
        {
            if (definitions[i].Enabled)
            {
                definitions[count++] = definitions[i];
            }
        }

        if (count != definitions.Length)
        {
            Array.Resize(ref definitions, count);
        }

        Array.Sort(definitions, static (left, right) =>
        {
            int order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : left.Id.CompareTo(right.Id);
        });

        _passes = definitions;
    }
}
