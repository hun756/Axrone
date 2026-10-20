using Axrone.Asset;

namespace Axrone.AssetShader;

/// <summary>
/// Axrone.Asset pipeline importer for shader effects: matches effect sources
/// by extension, then upserts the deserialized definition. Libraries traveling
/// with the definition are registered into the shared
/// <see cref="ShaderLibraryRegistry"/> so later bakes resolve includes.
/// </summary>
public sealed class ShaderEffectAssetImporter : IAssetImporter
{
    /// <inheritdoc/>
    public AssetImporterId Id => new("axrone.importer.shader-effect");

    /// <inheritdoc/>
    public int Priority => 20;

    /// <inheritdoc/>
    public bool CanImport(AssetImportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!TryGetSource(context, out _, out string path))
        {
            return false;
        }

        ReadOnlySpan<char> pathSpan = path.AsSpan();
        return pathSpan.EndsWith(".effect.json".AsSpan(), StringComparison.OrdinalIgnoreCase)
            || pathSpan.EndsWith(".shader.json".AsSpan(), StringComparison.OrdinalIgnoreCase)
            || pathSpan.EndsWith(".glsl".AsSpan(), StringComparison.OrdinalIgnoreCase)
            || pathSpan.EndsWith(".shader".AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public ValueTask<AssetImportResult> ImportAsync(AssetImportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!TryGetSource(context, out string? source, out string path))
        {
            throw new ShaderPreprocessException(ImmutableArray.Create(
                new ShaderDiagnostic("asset-shader.import", ShaderDiagnosticSeverity.Error, default, default, "Shader importer received an unsupported source payload.")));
        }

        ShaderEffectDefinition definition;
        try
        {
            definition = new AssetImportPipeline(new ShaderEffectModuleImporter(), new ShaderEffectJsonImporter())
                .Import(source, path);
        }
        catch (Exception ex) when (ex is not ShaderPreprocessException)
        {
            throw new ShaderPreprocessException(ImmutableArray.Create(
                new ShaderDiagnostic("asset-shader.import", ShaderDiagnosticSeverity.Error, default, default, ex.Message)));
        }

        if (!definition.Libraries.IsDefaultOrEmpty)
        {
            foreach (ShaderLibraryDefinition library in definition.Libraries)
            {
                ShaderLibraryRegistry.Instance.Register(library.Id, library.Code);
            }
        }

        definition = NormalizeDefinition(in definition);

        AssetWriteInput<object> primary = new(
            Kind: "shader-effect",
            Id: null,
            Key: context.BaseKey,
            Name: definition.Id.Name,
            Data: (object)definition,
            Metadata: context.Metadata);

        return ValueTask.FromResult(new AssetImportResult { Primary = primary });
    }

    private static ShaderEffectDefinition NormalizeDefinition(in ShaderEffectDefinition definition)
    {
        ShaderStageDefinition vertex = definition.Vertex;
        ShaderStageDefinition fragment = definition.Fragment;
        return definition with
        {
            Vertex = NormalizeStage(in vertex),
            Fragment = NormalizeStage(in fragment),
            Attributes = definition.Attributes.IsDefault ? ImmutableArray<ShaderAttributeDefinition>.Empty : definition.Attributes,
            Varyings = definition.Varyings.IsDefault ? ImmutableArray<ShaderInterfaceDefinition>.Empty : definition.Varyings,
            Properties = definition.Properties.IsDefault ? ImmutableArray<ShaderPropertyDefinition>.Empty : definition.Properties,
            Keywords = definition.Keywords.IsDefault ? ImmutableArray<ShaderKeywordDefinition>.Empty : definition.Keywords,
            Libraries = definition.Libraries.IsDefault ? ImmutableArray<ShaderLibraryDefinition>.Empty : definition.Libraries,
            Techniques = definition.Techniques.IsDefault
                ? ImmutableArray<ShaderTechniqueDefinition>.Empty
                : ImmutableArray.CreateRange(definition.Techniques, static t => t with
                {
                    Passes = t.Passes.IsDefault
                        ? ImmutableArray<ShaderPassDefinition>.Empty
                        : ImmutableArray.CreateRange(t.Passes, static p => p with
                        {
                            Keywords = p.Keywords.IsDefault ? ImmutableArray<ShaderSymbol>.Empty : p.Keywords,
                        }),
                }),
        };
    }

    private static ShaderStageDefinition NormalizeStage(in ShaderStageDefinition stage)
    {
        return stage with
        {
            Declarations = stage.Declarations.IsDefault ? ImmutableArray<ShaderGlslBlock>.Empty : stage.Declarations,
            Directives = stage.Directives.IsDefault ? ImmutableArray<string>.Empty : stage.Directives,
            Inputs = stage.Inputs.IsDefault ? ImmutableArray<ShaderInterfaceDefinition>.Empty : stage.Inputs,
            Outputs = stage.Outputs.IsDefault ? ImmutableArray<ShaderInterfaceDefinition>.Empty : stage.Outputs,
            Includes = stage.Includes.IsDefault ? ImmutableArray<ShaderSymbol>.Empty : stage.Includes,
        };
    }

    private static bool TryGetSource(AssetImportContext context, [NotNullWhen(true)] out string? source, out string path)
    {
        path = context.SourceUri?.Value ?? context.BaseKey.Value ?? string.Empty;
        switch (context.Source.RawData)
        {
            case string text:
                source = text;
                return true;
            case byte[] raw:
                source = Encoding.UTF8.GetString(raw);
                return true;
            default:
                source = null;
                return false;
        }
    }
}
