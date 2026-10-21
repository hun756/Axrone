namespace Axrone.AssetShader;

/// <summary>Partial stage override: only set fields replace the base.</summary>
public readonly record struct PartialShaderStageOverride(
    string? Main = null,
    ImmutableArray<ShaderGlslBlock> Declarations = default,
    ShaderPrecision? Precision = null,
    ImmutableArray<string> Directives = default,
    ImmutableArray<ShaderInterfaceDefinition> Inputs = default,
    ImmutableArray<ShaderInterfaceDefinition> Outputs = default,
    ImmutableArray<ShaderSymbol> Includes = default);

/// <summary>Partial effect override: only set fields replace the base.</summary>
public readonly record struct PartialShaderEffectOverride(
    ShaderSymbol? Id = null,
    uint? Version = null,
    PartialShaderStageOverride? Vertex = null,
    PartialShaderStageOverride? Fragment = null,
    ImmutableArray<ShaderAttributeDefinition> Attributes = default,
    ImmutableArray<ShaderInterfaceDefinition> Varyings = default,
    ImmutableArray<ShaderPropertyDefinition> Properties = default,
    ImmutableArray<ShaderKeywordDefinition> Keywords = default,
    ImmutableArray<ShaderLibraryDefinition> Libraries = default,
    ShaderSymbol? DefaultTechnique = null,
    ImmutableArray<ShaderTechniqueDefinition> Techniques = default,
    ShaderRenderState? RenderState = null,
    ShaderEffectMigrations? Migrations = null);

/// <summary>Effect extension engine: merges partial overrides onto a base effect.</summary>
public static class ShaderComposer
{
    /// <summary>Extends a base effect with an override.</summary>
    public static ShaderEffectDefinition ExtendShaderEffect(
        in ShaderEffectDefinition baseEffect,
        in PartialShaderEffectOverride over)
    {
        ShaderSymbol id = over.Id ?? baseEffect.Id;
        uint version = over.Version ?? baseEffect.Version;

        ShaderStageDefinition vtx = MergeStage(baseEffect.Vertex, over.Vertex);
        ShaderStageDefinition frag = MergeStage(baseEffect.Fragment, over.Fragment);

        ImmutableArray<ShaderAttributeDefinition> attributes = MergeNamed(
            baseEffect.Attributes, over.Attributes, static a => a.Name);

        ImmutableArray<ShaderInterfaceDefinition> varyings = MergeNamed(
            baseEffect.Varyings, over.Varyings, static v => v.Name);

        ImmutableArray<ShaderPropertyDefinition> properties = MergeNamed(
            baseEffect.Properties, over.Properties, static p => p.Name);

        ImmutableArray<ShaderKeywordDefinition> keywords = MergeNamed(
            baseEffect.Keywords, over.Keywords, static k => k.Name);

        ImmutableArray<ShaderLibraryDefinition> libraries = MergeNamed(
            baseEffect.Libraries, over.Libraries, static l => l.Id);

        ImmutableArray<ShaderTechniqueDefinition> techniques = MergeNamed(
            baseEffect.Techniques, over.Techniques, static t => t.Id);

        ShaderSymbol? defaultTech = over.DefaultTechnique ?? baseEffect.DefaultTechnique;
        ShaderRenderState? renderState = over.RenderState ?? baseEffect.RenderState;
        ShaderEffectMigrations? migrations = over.Migrations ?? baseEffect.Migrations;

        return new ShaderEffectDefinition(
            Id: id,
            Vertex: vtx,
            Fragment: frag,
            Version: version,
            Attributes: attributes,
            Varyings: varyings,
            Properties: properties,
            Keywords: keywords,
            Libraries: libraries,
            DefaultTechnique: defaultTech,
            Techniques: techniques,
            RenderState: renderState,
            Migrations: migrations);
    }

    private static ShaderStageDefinition MergeStage(
        in ShaderStageDefinition baseStage,
        in PartialShaderStageOverride? stageOverride)
    {
        if (stageOverride is null)
        {
            return baseStage;
        }

        PartialShaderStageOverride ov = stageOverride.Value;
        string main = !string.IsNullOrWhiteSpace(ov.Main) ? GLSL.Dedent(ov.Main) : baseStage.Main;
        ShaderPrecision prec = ov.Precision ?? baseStage.Precision;

        ImmutableArray<ShaderGlslBlock>.Builder decls = ImmutableArray.CreateBuilder<ShaderGlslBlock>();
        if (!baseStage.Declarations.IsDefaultOrEmpty)
        {
            decls.AddRange(baseStage.Declarations);
        }

        if (!ov.Declarations.IsDefaultOrEmpty)
        {
            decls.AddRange(ov.Declarations);
        }

        ImmutableArray<string>.Builder directives = ImmutableArray.CreateBuilder<string>();
        if (!baseStage.Directives.IsDefaultOrEmpty)
        {
            directives.AddRange(baseStage.Directives);
        }

        if (!ov.Directives.IsDefaultOrEmpty)
        {
            directives.AddRange(ov.Directives);
        }

        var includes = new HashSet<ShaderSymbol>();
        if (!baseStage.Includes.IsDefaultOrEmpty)
        {
            foreach (ShaderSymbol inc in baseStage.Includes)
            {
                includes.Add(inc);
            }
        }

        if (!ov.Includes.IsDefaultOrEmpty)
        {
            foreach (ShaderSymbol inc in ov.Includes)
            {
                includes.Add(inc);
            }
        }

        ImmutableArray<ShaderInterfaceDefinition> inputs = MergeNamed(
            baseStage.Inputs, ov.Inputs, static i => i.Name);
        ImmutableArray<ShaderInterfaceDefinition> outputs = MergeNamed(
            baseStage.Outputs, ov.Outputs, static o => o.Name);

        return new ShaderStageDefinition(
            Main: main,
            Declarations: decls.ToImmutable(),
            Precision: prec,
            Directives: directives.ToImmutable(),
            Inputs: inputs,
            Outputs: outputs,
            Includes: includes.ToImmutableArray());
    }

    private static ImmutableArray<T> MergeNamed<T>(
        ImmutableArray<T> baseItems,
        ImmutableArray<T> overrideItems,
        Func<T, ShaderSymbol> keySelector)
    {
        if (overrideItems.IsDefaultOrEmpty)
        {
            return baseItems.IsDefault ? ImmutableArray<T>.Empty : baseItems;
        }

        if (baseItems.IsDefaultOrEmpty)
        {
            return overrideItems;
        }

        var map = new Dictionary<ShaderSymbol, T>();
        foreach (T item in baseItems)
        {
            map[keySelector(item)] = item;
        }

        foreach (T item in overrideItems)
        {
            map[keySelector(item)] = item;
        }

        ImmutableArray<T>.Builder builder = ImmutableArray.CreateBuilder<T>(map.Count);
        foreach (T val in map.Values)
        {
            builder.Add(val);
        }

        return builder.MoveToImmutable();
    }
}
