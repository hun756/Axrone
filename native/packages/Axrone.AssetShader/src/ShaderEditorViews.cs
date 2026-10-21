namespace Axrone.AssetShader;

/// <summary>Capability identity for the shader package.</summary>
public static class ShaderCapabilities
{
    /// <summary>Stable cross-runtime capability id.</summary>
    public const string Id = "asset/shader";

    /// <summary>Owning native package.</summary>
    public const string Package = "Axrone.AssetShader";

    /// <summary>Core package this capability extends.</summary>
    public const string OwnerPackage = "Axrone.Asset";
}

/// <summary>Editor-facing uniform summary.</summary>
public readonly record struct ShaderUniformView(string Name, string Type, int ArrayLength);

/// <summary>Editor-facing keyword summary.</summary>
public readonly record struct ShaderKeywordView(
    string Name,
    string Kind,
    ImmutableArray<string> Options,
    string? DefaultValue);

/// <summary>Editor-facing technique summary.</summary>
public readonly record struct ShaderTechniqueView(string Id, string? Label, int PassCount);

/// <summary>Editor-facing effect summary.</summary>
public readonly record struct ShaderEditorSummary(
    ImmutableArray<ShaderUniformView> Uniforms,
    ImmutableArray<ShaderKeywordView> Keywords,
    ImmutableArray<ShaderTechniqueView> Techniques);

/// <summary>Editor-facing summaries derived from definitions and scans.</summary>
public static class ShaderEditorViews
{
    /// <summary>Summarizes an effect for editors.</summary>
    public static ShaderEditorSummary Summarize(
        in ShaderEffectDefinition effect,
        ImmutableArray<ShaderUniformScanResult> uniforms = default)
    {
        ImmutableArray<ShaderUniformView>.Builder uniformViews =
            ImmutableArray.CreateBuilder<ShaderUniformView>();
        foreach (ShaderUniformScanResult uniform in uniforms)
        {
            uniformViews.Add(new ShaderUniformView(uniform.Name.Name, uniform.Type.Name, uniform.ArrayLength));
        }

        ImmutableArray<ShaderKeywordView>.Builder keywordViews =
            ImmutableArray.CreateBuilder<ShaderKeywordView>();
        if (!effect.Keywords.IsDefault)
        {
            foreach (ShaderKeywordDefinition keyword in effect.Keywords)
            {
                keywordViews.Add(new ShaderKeywordView(
                    keyword.Name.Name,
                    keyword.IsToggle ? "toggle" : "enum",
                    keyword.Options.IsDefault ? [] : keyword.Options,
                    keyword.DefaultValue));
            }
        }

        ImmutableArray<ShaderTechniqueView>.Builder techniqueViews =
            ImmutableArray.CreateBuilder<ShaderTechniqueView>();
        if (!effect.Techniques.IsDefault)
        {
            foreach (ShaderTechniqueDefinition technique in effect.Techniques)
            {
                techniqueViews.Add(new ShaderTechniqueView(
                    technique.Id.Name,
                    technique.Label,
                    technique.Passes.IsDefault ? 0 : technique.Passes.Length));
            }
        }

        return new ShaderEditorSummary(
            uniformViews.ToImmutable(),
            keywordViews.ToImmutable(),
            techniqueViews.ToImmutable());
    }
}
