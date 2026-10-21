namespace Axrone.AssetShader;

/// <summary>JSON DTO for an effect definition.</summary>
public sealed class ShaderEffectDto
{
    /// <summary>Effect id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Version.</summary>
    public uint Version { get; set; } = 1;

    /// <summary>Vertex stage.</summary>
    public ShaderStageDto Vertex { get; set; } = new();

    /// <summary>Fragment stage.</summary>
    public ShaderStageDto Fragment { get; set; } = new();

    /// <summary>Attributes.</summary>
    public List<ShaderAttributeDto>? Attributes { get; set; }

    /// <summary>Varyings.</summary>
    public List<ShaderInterfaceDto>? Varyings { get; set; }

    /// <summary>Properties.</summary>
    public List<ShaderPropertyDto>? Properties { get; set; }

    /// <summary>Keywords.</summary>
    public List<ShaderKeywordDto>? Keywords { get; set; }

    /// <summary>Libraries.</summary>
    public List<ShaderLibraryDto>? Libraries { get; set; }

    /// <summary>Default technique name.</summary>
    public string? DefaultTechnique { get; set; }

    /// <summary>Techniques.</summary>
    public List<ShaderTechniqueDto>? Techniques { get; set; }

    /// <summary>Render state.</summary>
    public ShaderRenderStateDto? RenderState { get; set; }

    /// <summary>Migrations.</summary>
    public ShaderEffectMigrationsDto? Migrations { get; set; }
}

/// <summary>JSON DTO for a stage.</summary>
public sealed class ShaderStageDto
{
    /// <summary>Main body.</summary>
    public string Main { get; set; } = string.Empty;

    /// <summary>Declaration blocks.</summary>
    public List<string>? Declarations { get; set; }

    /// <summary>Precision name.</summary>
    public string? Precision { get; set; }

    /// <summary>Directives.</summary>
    public List<string>? Directives { get; set; }

    /// <summary>Inputs.</summary>
    public List<ShaderInterfaceDto>? Inputs { get; set; }

    /// <summary>Outputs.</summary>
    public List<ShaderInterfaceDto>? Outputs { get; set; }

    /// <summary>Includes.</summary>
    public List<string>? Includes { get; set; }
}

/// <summary>JSON DTO for an attribute.</summary>
public sealed class ShaderAttributeDto
{
    /// <summary>Name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Type.</summary>
    public string Type { get; set; } = "float";

    /// <summary>Location.</summary>
    public int? Location { get; set; }
}

/// <summary>JSON DTO for an interface variable.</summary>
public sealed class ShaderInterfaceDto
{
    /// <summary>Name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Type.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Interpolation name.</summary>
    public string? Interpolation { get; set; }
}

/// <summary>JSON DTO for a property.</summary>
public sealed class ShaderPropertyDto
{
    /// <summary>Name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Type.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Scope name.</summary>
    public string? Scope { get; set; }

    /// <summary>Default value.</summary>
    public string? DefaultValue { get; set; }

    /// <summary>Kind name.</summary>
    public string? Kind { get; set; }

    /// <summary>Range minimum.</summary>
    public double Min { get; set; }

    /// <summary>Range maximum.</summary>
    public double Max { get; set; }

    /// <summary>Range step.</summary>
    public double Step { get; set; }

    /// <summary>Enum options.</summary>
    public List<string>? EnumOptions { get; set; }

    /// <summary>Stage names.</summary>
    public List<string>? Stages { get; set; }

    /// <summary>Array length.</summary>
    public int ArrayLength { get; set; }

    /// <summary>Binding.</summary>
    public string? Binding { get; set; }

    /// <summary>Inspector.</summary>
    public ShaderInspectorDto? Inspector { get; set; }
}

/// <summary>JSON DTO for a keyword.</summary>
public sealed class ShaderKeywordDto
{
    /// <summary>Name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Stage names.</summary>
    public List<string>? Stages { get; set; }

    /// <summary>Options.</summary>
    public List<string>? Options { get; set; }

    /// <summary>Default value.</summary>
    public string? DefaultValue { get; set; }

    /// <summary>Inspector.</summary>
    public ShaderInspectorDto? Inspector { get; set; }
}

/// <summary>JSON DTO for inspector metadata.</summary>
public sealed class ShaderInspectorDto
{
    /// <summary>Widget.</summary>
    public string? Widget { get; set; }

    /// <summary>Group.</summary>
    public string? Group { get; set; }

    /// <summary>Tooltip.</summary>
    public string? Tooltip { get; set; }
}

/// <summary>JSON DTO for a property migration rule.</summary>
public sealed class ShaderPropertyMigrationDto
{
    /// <summary>Formerly serialized name.</summary>
    public string? FormerlySerializedAs { get; set; }

    /// <summary>Target channels.</summary>
    public List<string>? TargetChannels { get; set; }

    /// <summary>Whether removed.</summary>
    public bool Removed { get; set; }

    /// <summary>Reason.</summary>
    public string? Reason { get; set; }
}

/// <summary>JSON DTO for effect migrations.</summary>
public sealed class ShaderEffectMigrationsDto
{
    /// <summary>Property migrations by name.</summary>
    public Dictionary<string, ShaderPropertyMigrationDto>? Properties { get; set; }

    /// <summary>Keyword migrations by name.</summary>
    public Dictionary<string, ShaderPropertyMigrationDto>? Keywords { get; set; }
}

/// <summary>JSON DTO for a library.</summary>
public sealed class ShaderLibraryDto
{
    /// <summary>Id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Code.</summary>
    public string Code { get; set; } = string.Empty;
}

/// <summary>JSON DTO for a technique.</summary>
public sealed class ShaderTechniqueDto
{
    /// <summary>Id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Passes.</summary>
    public List<ShaderPassDto>? Passes { get; set; }

    /// <summary>Label.</summary>
    public string? Label { get; set; }
}

/// <summary>JSON DTO for a pass.</summary>
public sealed class ShaderPassDto
{
    /// <summary>Id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Vertex override.</summary>
    public ShaderStageDto? VertexOverride { get; set; }

    /// <summary>Fragment override.</summary>
    public ShaderStageDto? FragmentOverride { get; set; }

    /// <summary>Render state.</summary>
    public ShaderRenderStateDto? RenderState { get; set; }

    /// <summary>Keywords.</summary>
    public List<string>? Keywords { get; set; }
}

/// <summary>JSON DTO for render state.</summary>
public sealed class ShaderRenderStateDto
{
    /// <summary>Blend mode.</summary>
    public string? BlendMode { get; set; }

    /// <summary>Depth write.</summary>
    public bool? DepthWrite { get; set; }

    /// <summary>Depth test.</summary>
    public string? DepthTest { get; set; }

    /// <summary>Cull mode.</summary>
    public string? CullMode { get; set; }
}

/// <summary>Trim-safe source-generated JSON context.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ShaderEffectDto))]
internal sealed partial class ShaderAotJsonContext : JsonSerializerContext
{
}
