namespace Axrone.AssetShader;

/// <summary>Inspector metadata for a property or keyword.</summary>
public readonly record struct ShaderInspectorControl(
    string? Widget = null,
    string? Group = null,
    string? Tooltip = null);

/// <summary>Vertex attribute declaration.</summary>
public readonly record struct ShaderAttributeDefinition(
    ShaderSymbol Name,
    string Type = "float",
    int? Location = null);

/// <summary>Stage input/output interface variable.</summary>
public readonly record struct ShaderInterfaceDefinition(
    ShaderSymbol Name,
    string Type,
    ShaderInterpolation Interpolation = ShaderInterpolation.Smooth);

/// <summary>Material property declaration.</summary>
public readonly record struct ShaderPropertyDefinition(
    ShaderSymbol Name,
    string Type,
    ShaderPropertyScope Scope = ShaderPropertyScope.Material,
    string? DefaultValue = null,
    ShaderPropertyKind Kind = ShaderPropertyKind.Regular,
    double Min = 0.0,
    double Max = 1.0,
    double Step = 0.01,
    ImmutableArray<string> EnumOptions = default,
    ImmutableArray<ShaderStageTag> Stages = default,
    int ArrayLength = 0,
    ShaderInspectorControl Inspector = default,
    string? Binding = null);

/// <summary>Keyword (toggle or multi-option) declaration.</summary>
public readonly record struct ShaderKeywordDefinition(
    ShaderSymbol Name,
    ImmutableArray<ShaderStageTag> Stages = default,
    ImmutableArray<string> Options = default,
    string? DefaultValue = null,
    ShaderInspectorControl Inspector = default)
{
    /// <summary>Whether this keyword is a plain on/off toggle.</summary>
    public bool IsToggle => Options.IsDefaultOrEmpty;
}

/// <summary>Raw GLSL declaration block.</summary>
public readonly record struct ShaderGlslBlock(string Code);

/// <summary>One programmable stage.</summary>
public readonly record struct ShaderStageDefinition(
    string Main,
    ImmutableArray<ShaderGlslBlock> Declarations = default,
    ShaderPrecision Precision = ShaderPrecision.None,
    ImmutableArray<string> Directives = default,
    ImmutableArray<ShaderInterfaceDefinition> Inputs = default,
    ImmutableArray<ShaderInterfaceDefinition> Outputs = default,
    ImmutableArray<ShaderSymbol> Includes = default);

/// <summary>Fixed-function render state.</summary>
public readonly record struct ShaderRenderState(
    string? BlendMode = null,
    bool? DepthWrite = null,
    string? DepthTest = null,
    string? CullMode = null);

/// <summary>One render pass with optional stage overrides.</summary>
public readonly record struct ShaderPassDefinition(
    ShaderSymbol Id,
    ShaderStageDefinition? VertexOverride = null,
    ShaderStageDefinition? FragmentOverride = null,
    ShaderRenderState? RenderState = null,
    ImmutableArray<ShaderSymbol> Keywords = default);

/// <summary>One technique: an ordered pass list.</summary>
public readonly record struct ShaderTechniqueDefinition(
    ShaderSymbol Id,
    ImmutableArray<ShaderPassDefinition> Passes,
    string? Label = null);

/// <summary>Named reusable GLSL library chunk.</summary>
public readonly record struct ShaderLibraryDefinition(
    ShaderSymbol Id,
    string Code);

/// <summary>Resolved shader effect.</summary>
public readonly record struct ShaderEffectDefinition(
    ShaderSymbol Id,
    ShaderStageDefinition Vertex,
    ShaderStageDefinition Fragment,
    uint Version = 1,
    ImmutableArray<ShaderAttributeDefinition> Attributes = default,
    ImmutableArray<ShaderInterfaceDefinition> Varyings = default,
    ImmutableArray<ShaderPropertyDefinition> Properties = default,
    ImmutableArray<ShaderKeywordDefinition> Keywords = default,
    ImmutableArray<ShaderLibraryDefinition> Libraries = default,
    ShaderSymbol? DefaultTechnique = null,
    ImmutableArray<ShaderTechniqueDefinition> Techniques = default,
    ShaderRenderState? RenderState = null,
    ShaderEffectMigrations? Migrations = null);

/// <summary>Property migration rule.</summary>
public readonly record struct ShaderPropertyMigration(
    string? FormerlySerializedAs = null,
    ImmutableArray<string>? TargetChannels = null,
    bool Removed = false,
    string? Reason = null);

/// <summary>Effect migration map.</summary>
public readonly record struct ShaderEffectMigrations(
    ImmutableDictionary<string, ShaderPropertyMigration> Properties,
    ImmutableDictionary<string, ShaderPropertyMigration> Keywords);

/// <summary>Reference wrapper over an effect definition.</summary>
public sealed class ShaderEffect
{
    /// <summary>Effect definition.</summary>
    public ShaderEffectDefinition Definition { get; }

    /// <summary>Effect identity.</summary>
    public ShaderSymbol Id => Definition.Id;

    /// <summary>Creates an effect.</summary>
    public ShaderEffect(in ShaderEffectDefinition definition)
    {
        Definition = definition;
    }

    /// <summary>Wraps a definition.</summary>
    public static implicit operator ShaderEffect(in ShaderEffectDefinition def) => new(def);
}
