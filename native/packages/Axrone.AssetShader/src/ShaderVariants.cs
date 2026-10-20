namespace Axrone.AssetShader;

/// <summary>Keyword selection: keyword to chosen value.</summary>
public sealed class ShaderKeywordSelection : IEquatable<ShaderKeywordSelection>
{
    /// <summary>Selections.</summary>
    public ImmutableDictionary<ShaderSymbol, string> Selections { get; }

    /// <summary>Creates a selection.</summary>
    public ShaderKeywordSelection(ImmutableDictionary<ShaderSymbol, string> selections)
    {
        Selections = selections;
    }

    /// <summary>Empty selection.</summary>
    public static readonly ShaderKeywordSelection Empty =
        new(ImmutableDictionary<ShaderSymbol, string>.Empty);

    /// <inheritdoc/>
    public bool Equals(ShaderKeywordSelection? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (Selections.Count != other.Selections.Count)
        {
            return false;
        }

        foreach (KeyValuePair<ShaderSymbol, string> kvp in Selections)
        {
            if (!other.Selections.TryGetValue(kvp.Key, out string? val) || val != kvp.Value)
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as ShaderKeywordSelection);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (KeyValuePair<ShaderSymbol, string> kvp in Selections)
        {
            hash.Add(kvp.Key);
            hash.Add(kvp.Value, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}

/// <summary>Baked shader variant.</summary>
public sealed class ShaderVariant
{
    /// <summary>Variant identity.</summary>
    public ShaderSymbol Id { get; }

    /// <summary>Variant key.</summary>
    public VariantKey Key { get; }

    /// <summary>Keyword selection.</summary>
    public ShaderKeywordSelection Selection { get; }

    /// <summary>Macro defines.</summary>
    public ImmutableDictionary<ShaderSymbol, string> Defines { get; }

    /// <summary>Preprocessed vertex source.</summary>
    public string VertexSource { get; }

    /// <summary>Preprocessed fragment source.</summary>
    public string FragmentSource { get; }

    /// <summary>Scanned uniforms.</summary>
    public ImmutableArray<ShaderUniformScanResult> Uniforms { get; }

    /// <summary>Bake diagnostics.</summary>
    public ImmutableArray<ShaderDiagnostic> Diagnostics { get; }

    /// <summary>Creates a variant.</summary>
    public ShaderVariant(
        ShaderSymbol id,
        VariantKey key,
        ShaderKeywordSelection selection,
        ImmutableDictionary<ShaderSymbol, string> defines,
        string vertexSource,
        string fragmentSource,
        ImmutableArray<ShaderUniformScanResult> uniforms,
        ImmutableArray<ShaderDiagnostic> diagnostics)
    {
        Id = id;
        Key = key;
        Selection = selection;
        Defines = defines;
        VertexSource = vertexSource;
        FragmentSource = fragmentSource;
        Uniforms = uniforms;
        Diagnostics = diagnostics;
    }
}

/// <summary>Variant permutation and baking engine.</summary>
public static class ShaderVariantSystem
{
    private static readonly ConditionalWeakTable<ShaderEffect, ConcurrentDictionary<VariantKey, ShaderVariant>> s_variantCache = new();

    /// <summary>Counts the variant space.</summary>
    public static int ComputeVariantCount(ImmutableArray<ShaderKeywordDefinition> keywords)
    {
        if (keywords.IsDefaultOrEmpty)
        {
            return 1;
        }

        int total = 1;
        foreach (ShaderKeywordDefinition kw in keywords)
        {
            int optionsCount = kw.IsToggle ? 2 : Math.Max(1, kw.Options.Length);
            total = checked(total * optionsCount);
        }

        return total;
    }

    /// <summary>Folds a selection into a variant key via <c>Axrone.Hash</c> FNV-1a streams.</summary>
    public static VariantKey ComputeVariantKey(in ShaderKeywordSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.Selections.Count == 0)
        {
            return VariantKey.Default;
        }

        Fnv1a64Accumulator low = new(Fnv1a64Accumulator.OffsetBasis);
        Fnv1a64Accumulator high = new(5214872134981UL);

        Span<byte> keyBytes = stackalloc byte[8];
        foreach (KeyValuePair<ShaderSymbol, string> kvp in selection.Selections)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(keyBytes, kvp.Key.Hash);
            low.Append(keyBytes);
            high.Append(MemoryMarshal.AsBytes(kvp.Value.AsSpan()));
        }

        return new VariantKey(low.GetDigest().Value, high.GetDigest().Value);
    }

    /// <summary>Enumerates every keyword combination.</summary>
    public static ShaderKeywordSelection[] EnumerateCombinations(ImmutableArray<ShaderKeywordDefinition> keywords)
    {
        if (keywords.IsDefaultOrEmpty)
        {
            return [ShaderKeywordSelection.Empty];
        }

        int totalCount = ComputeVariantCount(keywords);
        var result = new ShaderKeywordSelection[totalCount];
        var indices = new int[keywords.Length];

        for (int i = 0; i < totalCount; i++)
        {
            ImmutableDictionary<ShaderSymbol, string>.Builder dict = ImmutableDictionary.CreateBuilder<ShaderSymbol, string>();
            for (int k = 0; k < keywords.Length; k++)
            {
                ShaderKeywordDefinition kw = keywords[k];
                if (kw.IsToggle)
                {
                    dict[kw.Name] = indices[k] == 1 ? "true" : "false";
                }
                else
                {
                    dict[kw.Name] = kw.Options[indices[k]];
                }
            }

            result[i] = new ShaderKeywordSelection(dict.ToImmutable());

            for (int k = keywords.Length - 1; k >= 0; k--)
            {
                indices[k]++;
                int max = keywords[k].IsToggle ? 2 : keywords[k].Options.Length;
                if (indices[k] < max)
                {
                    break;
                }

                indices[k] = 0;
            }
        }

        return result;
    }

    /// <summary>Lowers a selection to macro defines.</summary>
    public static ImmutableDictionary<ShaderSymbol, string> SelectionToDefines(
        ImmutableArray<ShaderKeywordDefinition> keywords,
        ShaderKeywordSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ImmutableDictionary<ShaderSymbol, string>.Builder builder = ImmutableDictionary.CreateBuilder<ShaderSymbol, string>();

        foreach (ShaderKeywordDefinition kw in keywords)
        {
            if (!selection.Selections.TryGetValue(kw.Name, out string? val))
            {
                continue;
            }

            if (kw.IsToggle)
            {
                if (string.Equals(val, "true", StringComparison.OrdinalIgnoreCase) || val == "1")
                {
                    builder[kw.Name] = "1";
                }
            }
            else
            {
                builder[kw.Name] = val;
                builder[$"{kw.Name.Name}_{val.ToUpperInvariant()}"] = "1";
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>Bakes (or reuses a cached) variant for a selection.</summary>
    public static ShaderVariant BakeVariant(
        ShaderEffect effect,
        ShaderKeywordSelection? selection = null,
        ShaderDiagnosticSink? sink = null)
    {
        ArgumentNullException.ThrowIfNull(effect);
        selection ??= ShaderKeywordSelection.Empty;
        VariantKey key = ComputeVariantKey(selection);

        ConcurrentDictionary<VariantKey, ShaderVariant> cache = s_variantCache.GetOrCreateValue(effect);
        if (cache.TryGetValue(key, out ShaderVariant? cached))
        {
            return cached;
        }

        sink ??= new ShaderDiagnosticSink();
        ImmutableDictionary<ShaderSymbol, string> defines = SelectionToDefines(effect.Definition.Keywords, selection);
        var macroPolicy = new ImmutableMacroEnvironment(defines);

        string rawVtx = ComposeStage(effect.Definition.Vertex);
        string rawFrag = ComposeStage(effect.Definition.Fragment);

        string vtxInc = IncludeExpander.ExpandIncludes(rawVtx, ShaderLibraryRegistry.Instance, sink, $"{effect.Id.Name}:vtx");
        string fragInc = IncludeExpander.ExpandIncludes(rawFrag, ShaderLibraryRegistry.Instance, sink, $"{effect.Id.Name}:frag");

        string vtxPre = GLSLPreprocessor.Preprocess(vtxInc, macroPolicy, sink, $"{effect.Id.Name}:vtx");
        string fragPre = GLSLPreprocessor.Preprocess(fragInc, macroPolicy, sink, $"{effect.Id.Name}:frag");

        ImmutableArray<ShaderUniformScanResult> uniforms = UniformExtractor.ScanUniforms(vtxPre.AsSpan(), fragPre.AsSpan());

        var variantId = new ShaderSymbol($"{effect.Id.Name}_{key}");
        var variant = new ShaderVariant(
            id: variantId,
            key: key,
            selection: selection,
            defines: defines,
            vertexSource: vtxPre,
            fragmentSource: fragPre,
            uniforms: uniforms,
            diagnostics: sink.Diagnostics);

        cache.TryAdd(key, variant);
        return variant;
    }

    private static string ComposeStage(in ShaderStageDefinition stage)
    {
        var sb = new StringBuilder();

        if (stage.Precision != ShaderPrecision.None)
        {
            sb.Append("precision ").Append(stage.Precision.ToString().ToLowerInvariant()).Append(" float;\n");
        }

        if (!stage.Directives.IsDefaultOrEmpty)
        {
            foreach (string dir in stage.Directives)
            {
                sb.Append(dir).Append('\n');
            }
        }

        if (!stage.Includes.IsDefaultOrEmpty)
        {
            foreach (ShaderSymbol inc in stage.Includes)
            {
                sb.Append("#include <").Append(inc.Name).Append(">\n");
            }
        }

        if (!stage.Declarations.IsDefaultOrEmpty)
        {
            foreach (ShaderGlslBlock block in stage.Declarations)
            {
                sb.Append(block.Code).Append('\n');
            }
        }

        sb.Append("void main() {\n").Append(stage.Main).Append("\n}\n");
        return sb.ToString();
    }
}

/// <summary>Per-stage reflection summary.</summary>
public readonly record struct ShaderStageReflection(
    string? Precision,
    int InputCount,
    int OutputCount,
    int DeclarationCount,
    int MainLineCount);

/// <summary>Effect reflection summary.</summary>
public readonly record struct ShaderEffectReflection(
    ShaderSymbol Id,
    uint Version,
    int VariantCount,
    ImmutableArray<ShaderAttributeDefinition> Attributes,
    ImmutableArray<ShaderInterfaceDefinition> Varyings,
    ImmutableArray<ShaderPropertyDefinition> Properties,
    ImmutableArray<ShaderKeywordDefinition> Keywords,
    ImmutableArray<ShaderTechniqueDefinition> Techniques,
    ShaderStageReflection Vertex,
    ShaderStageReflection Fragment);

/// <summary>Structural reflection over an effect definition.</summary>
public static class ShaderReflector
{
    /// <summary>Reflects an effect.</summary>
    public static ShaderEffectReflection Reflect(in ShaderEffectDefinition effect)
    {
        int vtxLines = 0;
        var vtxEnum = new SpanLineEnumerator(effect.Vertex.Main.AsSpan());
        while (vtxEnum.MoveNext())
        {
            vtxLines++;
        }

        int fragLines = 0;
        var fragEnum = new SpanLineEnumerator(effect.Fragment.Main.AsSpan());
        while (fragEnum.MoveNext())
        {
            fragLines++;
        }

        var vtxReflect = new ShaderStageReflection(
            Precision: effect.Vertex.Precision != ShaderPrecision.None ? effect.Vertex.Precision.ToString().ToLowerInvariant() : null,
            InputCount: effect.Vertex.Inputs.IsDefault ? 0 : effect.Vertex.Inputs.Length,
            OutputCount: effect.Vertex.Outputs.IsDefault ? 0 : effect.Vertex.Outputs.Length,
            DeclarationCount: effect.Vertex.Declarations.IsDefault ? 0 : effect.Vertex.Declarations.Length,
            MainLineCount: vtxLines);

        var fragReflect = new ShaderStageReflection(
            Precision: effect.Fragment.Precision != ShaderPrecision.None ? effect.Fragment.Precision.ToString().ToLowerInvariant() : null,
            InputCount: effect.Fragment.Inputs.IsDefault ? 0 : effect.Fragment.Inputs.Length,
            OutputCount: effect.Fragment.Outputs.IsDefault ? 0 : effect.Fragment.Outputs.Length,
            DeclarationCount: effect.Fragment.Declarations.IsDefault ? 0 : effect.Fragment.Declarations.Length,
            MainLineCount: fragLines);

        return new ShaderEffectReflection(
            Id: effect.Id,
            Version: effect.Version,
            VariantCount: ShaderVariantSystem.ComputeVariantCount(effect.Keywords),
            Attributes: effect.Attributes.IsDefault ? ImmutableArray<ShaderAttributeDefinition>.Empty : effect.Attributes,
            Varyings: effect.Varyings.IsDefault ? ImmutableArray<ShaderInterfaceDefinition>.Empty : effect.Varyings,
            Properties: effect.Properties.IsDefault ? ImmutableArray<ShaderPropertyDefinition>.Empty : effect.Properties,
            Keywords: effect.Keywords.IsDefault ? ImmutableArray<ShaderKeywordDefinition>.Empty : effect.Keywords,
            Techniques: effect.Techniques.IsDefault ? ImmutableArray<ShaderTechniqueDefinition>.Empty : effect.Techniques,
            Vertex: vtxReflect,
            Fragment: fragReflect);
    }
}
