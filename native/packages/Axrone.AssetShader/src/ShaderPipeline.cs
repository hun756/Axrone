namespace Axrone.AssetShader;

/// <summary>Builder stage: nothing configured yet.</summary>
public readonly struct StageUninitialized;

/// <summary>Builder stage: vertex stage configured.</summary>
public readonly struct StageVertexConfigured;

/// <summary>Builder stage: fragment stage configured.</summary>
public readonly struct StageFragmentConfigured;

/// <summary>Builder stage: sealed, ready to build.</summary>
public readonly struct StageSealed;

/// <summary>Typestate shader pipeline builder.</summary>
/// <typeparam name="TStage">Build stage.</typeparam>
public readonly struct ShaderPipelineBuilder<TStage>
    where TStage : struct
{
    private readonly ShaderSymbol _id;
    private readonly uint _version;
    private readonly ShaderStageDefinition _vertex;
    private readonly ShaderStageDefinition _fragment;
    private readonly ImmutableArray<ShaderAttributeDefinition> _attributes;
    private readonly ImmutableArray<ShaderInterfaceDefinition> _varyings;
    private readonly ImmutableArray<ShaderPropertyDefinition> _properties;
    private readonly ImmutableArray<ShaderKeywordDefinition> _keywords;
    private readonly ImmutableArray<ShaderLibraryDefinition> _libraries;
    private readonly ShaderSymbol? _defaultTechnique;
    private readonly ImmutableArray<ShaderTechniqueDefinition> _techniques;
    private readonly ShaderRenderState? _renderState;

    internal ShaderPipelineBuilder(
        ShaderSymbol id,
        uint version,
        in ShaderStageDefinition vertex,
        in ShaderStageDefinition fragment,
        ImmutableArray<ShaderAttributeDefinition> attributes,
        ImmutableArray<ShaderInterfaceDefinition> varyings,
        ImmutableArray<ShaderPropertyDefinition> properties,
        ImmutableArray<ShaderKeywordDefinition> keywords,
        ImmutableArray<ShaderLibraryDefinition> libraries,
        ShaderSymbol? defaultTechnique,
        ImmutableArray<ShaderTechniqueDefinition> techniques,
        ShaderRenderState? renderState)
    {
        _id = id;
        _version = version;
        _vertex = vertex;
        _fragment = fragment;
        _attributes = attributes;
        _varyings = varyings;
        _properties = properties;
        _keywords = keywords;
        _libraries = libraries;
        _defaultTechnique = defaultTechnique;
        _techniques = techniques;
        _renderState = renderState;
    }

    /// <summary>Begins a pipeline.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ShaderPipelineBuilder<StageUninitialized> Begin(ShaderSymbol id, uint version = 1) =>
        new(id, version, default, default,
            ImmutableArray<ShaderAttributeDefinition>.Empty,
            ImmutableArray<ShaderInterfaceDefinition>.Empty,
            ImmutableArray<ShaderPropertyDefinition>.Empty,
            ImmutableArray<ShaderKeywordDefinition>.Empty,
            ImmutableArray<ShaderLibraryDefinition>.Empty,
            null,
            ImmutableArray<ShaderTechniqueDefinition>.Empty,
            null);
}

/// <summary>Typestate transitions for the shader pipeline builder.</summary>
public static class ShaderPipelineBuilderExtensions
{
    /// <summary>Sets the vertex stage.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ShaderPipelineBuilder<StageVertexConfigured> WithVertex(
        this ShaderPipelineBuilder<StageUninitialized> builder,
        in ShaderStageDefinition vertex)
    {
        return new ShaderPipelineBuilder<StageVertexConfigured>(
            GetId(builder), GetVersion(builder), vertex, default,
            ImmutableArray<ShaderAttributeDefinition>.Empty,
            ImmutableArray<ShaderInterfaceDefinition>.Empty,
            ImmutableArray<ShaderPropertyDefinition>.Empty,
            ImmutableArray<ShaderKeywordDefinition>.Empty,
            ImmutableArray<ShaderLibraryDefinition>.Empty,
            null,
            ImmutableArray<ShaderTechniqueDefinition>.Empty,
            null);
    }

    /// <summary>Sets the fragment stage.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ShaderPipelineBuilder<StageFragmentConfigured> WithFragment(
        this ShaderPipelineBuilder<StageVertexConfigured> builder,
        in ShaderStageDefinition fragment)
    {
        return new ShaderPipelineBuilder<StageFragmentConfigured>(
            GetId(builder), GetVersion(builder), GetVertex(builder), fragment,
            ImmutableArray<ShaderAttributeDefinition>.Empty,
            ImmutableArray<ShaderInterfaceDefinition>.Empty,
            ImmutableArray<ShaderPropertyDefinition>.Empty,
            ImmutableArray<ShaderKeywordDefinition>.Empty,
            ImmutableArray<ShaderLibraryDefinition>.Empty,
            null,
            ImmutableArray<ShaderTechniqueDefinition>.Empty,
            null);
    }

    /// <summary>Sets the attribute list.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ShaderPipelineBuilder<StageFragmentConfigured> WithAttributes(
        this ShaderPipelineBuilder<StageFragmentConfigured> builder,
        ImmutableArray<ShaderAttributeDefinition> attributes)
    {
        return new ShaderPipelineBuilder<StageFragmentConfigured>(
            GetId(builder), GetVersion(builder), GetVertex(builder), GetFragment(builder),
            attributes, GetVaryings(builder), GetProperties(builder),
            GetKeywords(builder), GetLibraries(builder), GetDefaultTechnique(builder),
            GetTechniques(builder), GetRenderState(builder));
    }

    /// <summary>Sets the varying list.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ShaderPipelineBuilder<StageFragmentConfigured> WithVaryings(
        this ShaderPipelineBuilder<StageFragmentConfigured> builder,
        ImmutableArray<ShaderInterfaceDefinition> varyings)
    {
        return new ShaderPipelineBuilder<StageFragmentConfigured>(
            GetId(builder), GetVersion(builder), GetVertex(builder), GetFragment(builder),
            GetAttributes(builder), varyings, GetProperties(builder),
            GetKeywords(builder), GetLibraries(builder), GetDefaultTechnique(builder),
            GetTechniques(builder), GetRenderState(builder));
    }

    /// <summary>Sets the property list.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ShaderPipelineBuilder<StageFragmentConfigured> WithProperties(
        this ShaderPipelineBuilder<StageFragmentConfigured> builder,
        ImmutableArray<ShaderPropertyDefinition> properties)
    {
        return new ShaderPipelineBuilder<StageFragmentConfigured>(
            GetId(builder), GetVersion(builder), GetVertex(builder), GetFragment(builder),
            GetAttributes(builder), GetVaryings(builder), properties,
            GetKeywords(builder), GetLibraries(builder), GetDefaultTechnique(builder),
            GetTechniques(builder), GetRenderState(builder));
    }

    /// <summary>Sets the keyword list.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ShaderPipelineBuilder<StageFragmentConfigured> WithKeywords(
        this ShaderPipelineBuilder<StageFragmentConfigured> builder,
        ImmutableArray<ShaderKeywordDefinition> keywords)
    {
        return new ShaderPipelineBuilder<StageFragmentConfigured>(
            GetId(builder), GetVersion(builder), GetVertex(builder), GetFragment(builder),
            GetAttributes(builder), GetVaryings(builder), GetProperties(builder),
            keywords, GetLibraries(builder), GetDefaultTechnique(builder),
            GetTechniques(builder), GetRenderState(builder));
    }

    /// <summary>Sets the technique list.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ShaderPipelineBuilder<StageFragmentConfigured> WithTechniques(
        this ShaderPipelineBuilder<StageFragmentConfigured> builder,
        ImmutableArray<ShaderTechniqueDefinition> techniques,
        ShaderSymbol? defaultTechnique = null)
    {
        return new ShaderPipelineBuilder<StageFragmentConfigured>(
            GetId(builder), GetVersion(builder), GetVertex(builder), GetFragment(builder),
            GetAttributes(builder), GetVaryings(builder), GetProperties(builder),
            GetKeywords(builder), GetLibraries(builder), defaultTechnique,
            techniques, GetRenderState(builder));
    }

    /// <summary>Seals the pipeline.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ShaderPipelineBuilder<StageSealed> Seal(this ShaderPipelineBuilder<StageFragmentConfigured> builder)
    {
        return new ShaderPipelineBuilder<StageSealed>(
            GetId(builder), GetVersion(builder), GetVertex(builder), GetFragment(builder),
            GetAttributes(builder), GetVaryings(builder), GetProperties(builder),
            GetKeywords(builder), GetLibraries(builder), GetDefaultTechnique(builder),
            GetTechniques(builder), GetRenderState(builder));
    }

    /// <summary>Builds the effect.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ShaderEffect BuildEffect(this ShaderPipelineBuilder<StageSealed> builder)
    {
        return new ShaderEffect(new ShaderEffectDefinition(
            Id: GetId(builder),
            Vertex: GetVertex(builder),
            Fragment: GetFragment(builder),
            Version: GetVersion(builder),
            Attributes: GetAttributes(builder),
            Varyings: GetVaryings(builder),
            Properties: GetProperties(builder),
            Keywords: GetKeywords(builder),
            Libraries: GetLibraries(builder),
            DefaultTechnique: GetDefaultTechnique(builder),
            Techniques: GetTechniques(builder),
            RenderState: GetRenderState(builder)));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_id")]
    private static extern ref readonly ShaderSymbol GetId<TStage>(in ShaderPipelineBuilder<TStage> builder)
        where TStage : struct;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_version")]
    private static extern ref readonly uint GetVersion<TStage>(in ShaderPipelineBuilder<TStage> builder)
        where TStage : struct;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_vertex")]
    private static extern ref readonly ShaderStageDefinition GetVertex<TStage>(in ShaderPipelineBuilder<TStage> builder)
        where TStage : struct;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_fragment")]
    private static extern ref readonly ShaderStageDefinition GetFragment<TStage>(in ShaderPipelineBuilder<TStage> builder)
        where TStage : struct;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_attributes")]
    private static extern ref readonly ImmutableArray<ShaderAttributeDefinition> GetAttributes<TStage>(in ShaderPipelineBuilder<TStage> builder)
        where TStage : struct;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_varyings")]
    private static extern ref readonly ImmutableArray<ShaderInterfaceDefinition> GetVaryings<TStage>(in ShaderPipelineBuilder<TStage> builder)
        where TStage : struct;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_properties")]
    private static extern ref readonly ImmutableArray<ShaderPropertyDefinition> GetProperties<TStage>(in ShaderPipelineBuilder<TStage> builder)
        where TStage : struct;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_keywords")]
    private static extern ref readonly ImmutableArray<ShaderKeywordDefinition> GetKeywords<TStage>(in ShaderPipelineBuilder<TStage> builder)
        where TStage : struct;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_libraries")]
    private static extern ref readonly ImmutableArray<ShaderLibraryDefinition> GetLibraries<TStage>(in ShaderPipelineBuilder<TStage> builder)
        where TStage : struct;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_defaultTechnique")]
    private static extern ref readonly ShaderSymbol? GetDefaultTechnique<TStage>(in ShaderPipelineBuilder<TStage> builder)
        where TStage : struct;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_techniques")]
    private static extern ref readonly ImmutableArray<ShaderTechniqueDefinition> GetTechniques<TStage>(in ShaderPipelineBuilder<TStage> builder)
        where TStage : struct;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_renderState")]
    private static extern ref readonly ShaderRenderState? GetRenderState<TStage>(in ShaderPipelineBuilder<TStage> builder)
        where TStage : struct;
}
