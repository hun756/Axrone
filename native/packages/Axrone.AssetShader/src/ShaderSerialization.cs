namespace Axrone.AssetShader;

/// <summary>DTO mapping between effect definitions and trim-safe JSON.</summary>
public static class ShaderSerialization
{
    /// <summary>Serializes an effect to JSON.</summary>
    public static string SerializeToJson(in ShaderEffectDefinition effect)
    {
        ShaderEffectDto dto = ToDto(effect);
        return JsonSerializer.Serialize(dto, ShaderAotJsonContext.Default.ShaderEffectDto);
    }

    /// <summary>Deserializes an effect from JSON.</summary>
    public static ShaderEffectDefinition DeserializeFromJson(string json)
    {
        ShaderEffectDto dto = JsonSerializer.Deserialize(json, ShaderAotJsonContext.Default.ShaderEffectDto)
            ?? throw new InvalidOperationException("Failed to deserialize shader definition.");
        return FromDto(dto);
    }

    private static ShaderEffectDto ToDto(in ShaderEffectDefinition effect)
    {
        var dto = new ShaderEffectDto
        {
            Id = effect.Id.Name,
            Version = effect.Version,
            Vertex = StageToDto(effect.Vertex),
            Fragment = StageToDto(effect.Fragment),
            DefaultTechnique = effect.DefaultTechnique?.Name,
        };

        if (!effect.Attributes.IsDefaultOrEmpty)
        {
            dto.Attributes = new List<ShaderAttributeDto>(effect.Attributes.Length);
            foreach (ShaderAttributeDefinition a in effect.Attributes)
            {
                dto.Attributes.Add(new ShaderAttributeDto { Name = a.Name.Name, Type = a.Type, Location = a.Location });
            }
        }

        if (!effect.Varyings.IsDefaultOrEmpty)
        {
            dto.Varyings = new List<ShaderInterfaceDto>(effect.Varyings.Length);
            foreach (ShaderInterfaceDefinition v in effect.Varyings)
            {
                dto.Varyings.Add(new ShaderInterfaceDto { Name = v.Name.Name, Type = v.Type, Interpolation = v.Interpolation.ToString().ToLowerInvariant() });
            }
        }

        if (!effect.Properties.IsDefaultOrEmpty)
        {
            dto.Properties = new List<ShaderPropertyDto>(effect.Properties.Length);
            foreach (ShaderPropertyDefinition p in effect.Properties)
            {
                dto.Properties.Add(PropToDto(p));
            }
        }

        if (!effect.Keywords.IsDefaultOrEmpty)
        {
            dto.Keywords = new List<ShaderKeywordDto>(effect.Keywords.Length);
            foreach (ShaderKeywordDefinition k in effect.Keywords)
            {
                dto.Keywords.Add(KeywordToDto(k));
            }
        }

        if (!effect.Libraries.IsDefaultOrEmpty)
        {
            dto.Libraries = new List<ShaderLibraryDto>(effect.Libraries.Length);
            foreach (ShaderLibraryDefinition l in effect.Libraries)
            {
                dto.Libraries.Add(new ShaderLibraryDto { Id = l.Id.Name, Code = l.Code });
            }
        }

        if (!effect.Techniques.IsDefaultOrEmpty)
        {
            dto.Techniques = new List<ShaderTechniqueDto>(effect.Techniques.Length);
            foreach (ShaderTechniqueDefinition t in effect.Techniques)
            {
                dto.Techniques.Add(TechToDto(t));
            }
        }

        if (effect.RenderState is not null)
        {
            dto.RenderState = RenderStateToDto(effect.RenderState.Value);
        }

        return dto;
    }

    private static ShaderStageDto StageToDto(in ShaderStageDefinition stage)
    {
        var dto = new ShaderStageDto
        {
            Main = stage.Main,
            Precision = stage.Precision != ShaderPrecision.None ? stage.Precision.ToString().ToLowerInvariant() : null,
        };

        if (!stage.Declarations.IsDefaultOrEmpty)
        {
            dto.Declarations = new List<string>(stage.Declarations.Length);
            foreach (ShaderGlslBlock b in stage.Declarations)
            {
                dto.Declarations.Add(b.Code);
            }
        }

        if (!stage.Directives.IsDefaultOrEmpty)
        {
            dto.Directives = new List<string>(stage.Directives);
        }

        if (!stage.Inputs.IsDefaultOrEmpty)
        {
            dto.Inputs = new List<ShaderInterfaceDto>(stage.Inputs.Length);
            foreach (ShaderInterfaceDefinition i in stage.Inputs)
            {
                dto.Inputs.Add(new ShaderInterfaceDto { Name = i.Name.Name, Type = i.Type, Interpolation = i.Interpolation.ToString().ToLowerInvariant() });
            }
        }

        if (!stage.Outputs.IsDefaultOrEmpty)
        {
            dto.Outputs = new List<ShaderInterfaceDto>(stage.Outputs.Length);
            foreach (ShaderInterfaceDefinition o in stage.Outputs)
            {
                dto.Outputs.Add(new ShaderInterfaceDto { Name = o.Name.Name, Type = o.Type, Interpolation = o.Interpolation.ToString().ToLowerInvariant() });
            }
        }

        if (!stage.Includes.IsDefaultOrEmpty)
        {
            dto.Includes = new List<string>(stage.Includes.Length);
            foreach (ShaderSymbol inc in stage.Includes)
            {
                dto.Includes.Add(inc.Name);
            }
        }

        return dto;
    }

    private static ShaderPropertyDto PropToDto(in ShaderPropertyDefinition p)
    {
        var dto = new ShaderPropertyDto
        {
            Name = p.Name.Name,
            Type = p.Type,
            Scope = p.Scope.ToString().ToLowerInvariant(),
            DefaultValue = p.DefaultValue,
            Kind = p.Kind.ToString().ToLowerInvariant(),
            Min = p.Min,
            Max = p.Max,
            Step = p.Step,
            ArrayLength = p.ArrayLength,
            Binding = p.Binding,
            Inspector = new ShaderInspectorDto
            {
                Widget = p.Inspector.Widget,
                Group = p.Inspector.Group,
                Tooltip = p.Inspector.Tooltip,
            },
        };

        if (!p.EnumOptions.IsDefaultOrEmpty)
        {
            dto.EnumOptions = new List<string>(p.EnumOptions);
        }

        if (!p.Stages.IsDefaultOrEmpty)
        {
            dto.Stages = new List<string>(p.Stages.Length);
            foreach (ShaderStageTag st in p.Stages)
            {
                dto.Stages.Add(st.ToString().ToLowerInvariant());
            }
        }

        return dto;
    }

    private static ShaderKeywordDto KeywordToDto(in ShaderKeywordDefinition k)
    {
        var dto = new ShaderKeywordDto
        {
            Name = k.Name.Name,
            DefaultValue = k.DefaultValue,
            Inspector = new ShaderInspectorDto
            {
                Widget = k.Inspector.Widget,
                Group = k.Inspector.Group,
                Tooltip = k.Inspector.Tooltip,
            },
        };

        if (!k.Options.IsDefaultOrEmpty)
        {
            dto.Options = new List<string>(k.Options);
        }

        if (!k.Stages.IsDefaultOrEmpty)
        {
            dto.Stages = new List<string>(k.Stages.Length);
            foreach (ShaderStageTag st in k.Stages)
            {
                dto.Stages.Add(st.ToString().ToLowerInvariant());
            }
        }

        return dto;
    }

    private static ShaderTechniqueDto TechToDto(in ShaderTechniqueDefinition t)
    {
        var dto = new ShaderTechniqueDto
        {
            Id = t.Id.Name,
            Label = t.Label,
        };

        if (!t.Passes.IsDefaultOrEmpty)
        {
            dto.Passes = new List<ShaderPassDto>(t.Passes.Length);
            foreach (ShaderPassDefinition p in t.Passes)
            {
                dto.Passes.Add(PassToDto(p));
            }
        }

        return dto;
    }

    private static ShaderPassDto PassToDto(in ShaderPassDefinition p)
    {
        var dto = new ShaderPassDto
        {
            Id = p.Id.Name,
            VertexOverride = p.VertexOverride is not null ? StageToDto(p.VertexOverride.Value) : null,
            FragmentOverride = p.FragmentOverride is not null ? StageToDto(p.FragmentOverride.Value) : null,
            RenderState = p.RenderState is not null ? RenderStateToDto(p.RenderState.Value) : null,
        };

        if (!p.Keywords.IsDefaultOrEmpty)
        {
            dto.Keywords = new List<string>(p.Keywords.Length);
            foreach (ShaderSymbol kw in p.Keywords)
            {
                dto.Keywords.Add(kw.Name);
            }
        }

        return dto;
    }

    private static ShaderRenderStateDto RenderStateToDto(in ShaderRenderState r) => new()
    {
        BlendMode = r.BlendMode,
        DepthWrite = r.DepthWrite,
        DepthTest = r.DepthTest,
        CullMode = r.CullMode,
    };

    private static ShaderEffectDefinition FromDto(ShaderEffectDto dto)
    {
        return new ShaderEffectDefinition(
            Id: new ShaderSymbol(dto.Id),
            Version: dto.Version,
            Vertex: StageFromDto(dto.Vertex),
            Fragment: StageFromDto(dto.Fragment),
            Attributes: ConvertAttributes(dto.Attributes),
            Varyings: ConvertInterfaces(dto.Varyings),
            Properties: ConvertProperties(dto.Properties),
            Keywords: ConvertKeywords(dto.Keywords),
            Libraries: ConvertLibraries(dto.Libraries),
            DefaultTechnique: dto.DefaultTechnique is not null ? (ShaderSymbol?)new ShaderSymbol(dto.DefaultTechnique) : null,
            Techniques: ConvertTechniques(dto.Techniques),
            RenderState: dto.RenderState is not null ? (ShaderRenderState?)RenderStateFromDto(dto.RenderState) : null);
    }

    private static ShaderStageDefinition StageFromDto(ShaderStageDto dto)
    {
        ImmutableArray<ShaderGlslBlock>.Builder declBuilder = ImmutableArray.CreateBuilder<ShaderGlslBlock>();
        if (dto.Declarations is not null)
        {
            foreach (string d in dto.Declarations)
            {
                declBuilder.Add(new ShaderGlslBlock(d));
            }
        }

        ImmutableArray<ShaderSymbol>.Builder incBuilder = ImmutableArray.CreateBuilder<ShaderSymbol>();
        if (dto.Includes is not null)
        {
            foreach (string inc in dto.Includes)
            {
                incBuilder.Add(new ShaderSymbol(inc));
            }
        }

        return new ShaderStageDefinition(
            Main: GLSL.Dedent(dto.Main),
            Declarations: declBuilder.ToImmutable(),
            Precision: ParsePrecision(dto.Precision),
            Directives: dto.Directives is not null ? dto.Directives.ToImmutableArray() : ImmutableArray<string>.Empty,
            Inputs: ConvertInterfaces(dto.Inputs),
            Outputs: ConvertInterfaces(dto.Outputs),
            Includes: incBuilder.ToImmutable());
    }

    private static ImmutableArray<ShaderAttributeDefinition> ConvertAttributes(List<ShaderAttributeDto>? list)
    {
        if (list is null || list.Count == 0)
        {
            return ImmutableArray<ShaderAttributeDefinition>.Empty;
        }

        ImmutableArray<ShaderAttributeDefinition>.Builder builder = ImmutableArray.CreateBuilder<ShaderAttributeDefinition>(list.Count);
        foreach (ShaderAttributeDto a in list)
        {
            builder.Add(new ShaderAttributeDefinition(new ShaderSymbol(a.Name), a.Type, a.Location));
        }

        return builder.MoveToImmutable();
    }

    private static ImmutableArray<ShaderInterfaceDefinition> ConvertInterfaces(List<ShaderInterfaceDto>? list)
    {
        if (list is null || list.Count == 0)
        {
            return ImmutableArray<ShaderInterfaceDefinition>.Empty;
        }

        ImmutableArray<ShaderInterfaceDefinition>.Builder builder = ImmutableArray.CreateBuilder<ShaderInterfaceDefinition>(list.Count);
        foreach (ShaderInterfaceDto v in list)
        {
            builder.Add(new ShaderInterfaceDefinition(new ShaderSymbol(v.Name), v.Type, ParseInterpolation(v.Interpolation)));
        }

        return builder.MoveToImmutable();
    }

    private static ImmutableArray<ShaderPropertyDefinition> ConvertProperties(List<ShaderPropertyDto>? list)
    {
        if (list is null || list.Count == 0)
        {
            return ImmutableArray<ShaderPropertyDefinition>.Empty;
        }

        ImmutableArray<ShaderPropertyDefinition>.Builder builder = ImmutableArray.CreateBuilder<ShaderPropertyDefinition>(list.Count);
        foreach (ShaderPropertyDto p in list)
        {
            ImmutableArray<ShaderStageTag>.Builder stagesBuilder = ImmutableArray.CreateBuilder<ShaderStageTag>();
            if (p.Stages is not null)
            {
                foreach (string st in p.Stages)
                {
                    stagesBuilder.Add(ParseStageTag(st));
                }
            }

            builder.Add(new ShaderPropertyDefinition(
                Name: new ShaderSymbol(p.Name),
                Type: p.Type,
                Scope: string.Equals(p.Scope, "renderer", StringComparison.OrdinalIgnoreCase) ? ShaderPropertyScope.Renderer : ShaderPropertyScope.Material,
                DefaultValue: p.DefaultValue,
                Kind: ParsePropertyKind(p.Kind),
                Min: p.Min,
                Max: p.Max,
                Step: p.Step,
                EnumOptions: p.EnumOptions is not null ? p.EnumOptions.ToImmutableArray() : ImmutableArray<string>.Empty,
                Stages: stagesBuilder.ToImmutable(),
                ArrayLength: p.ArrayLength,
                Inspector: new ShaderInspectorControl(p.Inspector?.Widget, p.Inspector?.Group, p.Inspector?.Tooltip),
                Binding: p.Binding));
        }

        return builder.MoveToImmutable();
    }

    private static ImmutableArray<ShaderKeywordDefinition> ConvertKeywords(List<ShaderKeywordDto>? list)
    {
        if (list is null || list.Count == 0)
        {
            return ImmutableArray<ShaderKeywordDefinition>.Empty;
        }

        ImmutableArray<ShaderKeywordDefinition>.Builder builder = ImmutableArray.CreateBuilder<ShaderKeywordDefinition>(list.Count);
        foreach (ShaderKeywordDto k in list)
        {
            ImmutableArray<ShaderStageTag>.Builder stagesBuilder = ImmutableArray.CreateBuilder<ShaderStageTag>();
            if (k.Stages is not null)
            {
                foreach (string st in k.Stages)
                {
                    stagesBuilder.Add(ParseStageTag(st));
                }
            }

            builder.Add(new ShaderKeywordDefinition(
                Name: new ShaderSymbol(k.Name),
                Stages: stagesBuilder.ToImmutable(),
                Options: k.Options is not null ? k.Options.ToImmutableArray() : ImmutableArray<string>.Empty,
                DefaultValue: k.DefaultValue,
                Inspector: new ShaderInspectorControl(k.Inspector?.Widget, k.Inspector?.Group, k.Inspector?.Tooltip)));
        }

        return builder.MoveToImmutable();
    }

    private static ImmutableArray<ShaderLibraryDefinition> ConvertLibraries(List<ShaderLibraryDto>? list)
    {
        if (list is null || list.Count == 0)
        {
            return ImmutableArray<ShaderLibraryDefinition>.Empty;
        }

        ImmutableArray<ShaderLibraryDefinition>.Builder builder = ImmutableArray.CreateBuilder<ShaderLibraryDefinition>(list.Count);
        foreach (ShaderLibraryDto l in list)
        {
            builder.Add(new ShaderLibraryDefinition(new ShaderSymbol(l.Id), l.Code));
        }

        return builder.MoveToImmutable();
    }

    private static ImmutableArray<ShaderTechniqueDefinition> ConvertTechniques(List<ShaderTechniqueDto>? list)
    {
        if (list is null || list.Count == 0)
        {
            return ImmutableArray<ShaderTechniqueDefinition>.Empty;
        }

        ImmutableArray<ShaderTechniqueDefinition>.Builder builder = ImmutableArray.CreateBuilder<ShaderTechniqueDefinition>(list.Count);
        foreach (ShaderTechniqueDto t in list)
        {
            ImmutableArray<ShaderPassDefinition>.Builder passesBuilder = ImmutableArray.CreateBuilder<ShaderPassDefinition>();
            if (t.Passes is not null)
            {
                foreach (ShaderPassDto p in t.Passes)
                {
                    ImmutableArray<ShaderSymbol>.Builder kwBuilder = ImmutableArray.CreateBuilder<ShaderSymbol>();
                    if (p.Keywords is not null)
                    {
                        foreach (string kw in p.Keywords)
                        {
                            kwBuilder.Add(new ShaderSymbol(kw));
                        }
                    }

                    passesBuilder.Add(new ShaderPassDefinition(
                        Id: new ShaderSymbol(p.Id),
                        VertexOverride: p.VertexOverride is not null ? StageFromDto(p.VertexOverride) : null,
                        FragmentOverride: p.FragmentOverride is not null ? StageFromDto(p.FragmentOverride) : null,
                        RenderState: p.RenderState is not null ? RenderStateFromDto(p.RenderState) : null,
                        Keywords: kwBuilder.ToImmutable()));
                }
            }

            builder.Add(new ShaderTechniqueDefinition(
                Id: new ShaderSymbol(t.Id),
                Passes: passesBuilder.ToImmutable(),
                Label: t.Label));
        }

        return builder.MoveToImmutable();
    }

    private static ShaderRenderState RenderStateFromDto(ShaderRenderStateDto dto) => new(
        BlendMode: dto.BlendMode,
        DepthWrite: dto.DepthWrite,
        DepthTest: dto.DepthTest,
        CullMode: dto.CullMode);

    private static ShaderInterpolation ParseInterpolation(string? s) =>
        string.Equals(s, "flat", StringComparison.OrdinalIgnoreCase)
            ? ShaderInterpolation.Flat
            : ShaderInterpolation.Smooth;

    private static ShaderPrecision ParsePrecision(string? s) => s?.ToLowerInvariant() switch
    {
        "lowp" => ShaderPrecision.Lowp,
        "mediump" => ShaderPrecision.Mediump,
        "highp" => ShaderPrecision.Highp,
        _ => ShaderPrecision.None,
    };

    private static ShaderPropertyKind ParsePropertyKind(string? s) => s?.ToLowerInvariant() switch
    {
        "range" => ShaderPropertyKind.Range,
        "toggle" => ShaderPropertyKind.Toggle,
        "enum" => ShaderPropertyKind.Enum,
        _ => ShaderPropertyKind.Regular,
    };

    private static ShaderStageTag ParseStageTag(string s) =>
        string.Equals(s, "vertex", StringComparison.OrdinalIgnoreCase)
            ? ShaderStageTag.Vertex
            : ShaderStageTag.Fragment;
}
