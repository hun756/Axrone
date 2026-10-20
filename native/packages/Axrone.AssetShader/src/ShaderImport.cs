namespace Axrone.AssetShader;

/// <summary>Shader importer contract: match by extension, then import.</summary>
public interface IShaderImporter
{
    /// <summary>Importer identity.</summary>
    ShaderSymbol Id { get; }

    /// <summary>Priority; higher wins.</summary>
    int Priority { get; }

    /// <summary>Handled extensions.</summary>
    IReadOnlyList<string> Extensions { get; }

    /// <summary>Imports source text.</summary>
    ShaderEffectDefinition Import(string source, string sourcePath);
}

/// <summary>JSON effect importer.</summary>
public sealed class ShaderEffectJsonImporter : IShaderImporter
{
    /// <inheritdoc/>
    public ShaderSymbol Id => "asset-shader.effect.json";

    /// <inheritdoc/>
    public int Priority => 20;

    /// <inheritdoc/>
    public IReadOnlyList<string> Extensions => [".effect.json", ".shader.json"];

    /// <inheritdoc/>
    public ShaderEffectDefinition Import(string source, string sourcePath) =>
        ShaderSerialization.DeserializeFromJson(source);
}

/// <summary>Raw GLSL module importer: wraps the source as both stages.</summary>
public sealed class ShaderEffectModuleImporter : IShaderImporter
{
    /// <inheritdoc/>
    public ShaderSymbol Id => "asset-shader.effect.module";

    /// <inheritdoc/>
    public int Priority => 10;

    /// <inheritdoc/>
    public IReadOnlyList<string> Extensions => [".glsl", ".shader"];

    /// <inheritdoc/>
    public ShaderEffectDefinition Import(string source, string sourcePath)
    {
        string baseName = Path.GetFileNameWithoutExtension(sourcePath);
        return new ShaderEffectDefinition(
            Id: new ShaderSymbol(baseName),
            Vertex: new ShaderStageDefinition(source),
            Fragment: new ShaderStageDefinition(source));
    }
}

/// <summary>Extension-routed shader import pipeline.</summary>
public sealed class AssetImportPipeline
{
    private readonly List<IShaderImporter> _importers = new();

    /// <summary>Creates a pipeline.</summary>
    public AssetImportPipeline(params ReadOnlySpan<IShaderImporter> importers)
    {
        foreach (IShaderImporter importer in importers)
        {
            _importers.Add(importer);
        }

        _importers.Sort(static (a, b) => a.Priority.CompareTo(b.Priority));
    }

    /// <summary>Imports content through the first matching importer.</summary>
    public ShaderEffectDefinition Import(string content, string path)
    {
        ReadOnlySpan<char> pathSpan = path.AsSpan();
        foreach (IShaderImporter importer in _importers)
        {
            foreach (string ext in importer.Extensions)
            {
                if (pathSpan.EndsWith(ext.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    return importer.Import(content, path);
                }
            }
        }

        ThrowHelper.ThrowNoMatchingImporter(path);
        return default;
    }
}
