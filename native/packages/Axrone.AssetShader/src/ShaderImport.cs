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
    public ShaderEffectDefinition Import(string source, string sourcePath)
    {
        using JsonDocument document = JsonDocument.Parse(source);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Shader effect payload must be an object.");
        }

        JsonElement candidate = document.RootElement;
        if (candidate.TryGetProperty("effect", out JsonElement wrapped) && wrapped.ValueKind == JsonValueKind.Object)
        {
            candidate = wrapped;
        }

        if (candidate.TryGetProperty("format", out JsonElement format) &&
            format.ValueKind == JsonValueKind.String &&
            format.GetString() != "axrone.shader/effect")
        {
            throw new InvalidOperationException("Shader effect payload.format must be axrone.shader/effect");
        }

        bool hasVersion = candidate.TryGetProperty("version", out JsonElement versionElement) &&
            versionElement.ValueKind == JsonValueKind.Number;
        uint version = hasVersion ? versionElement.GetUInt32() : (HasSchemaV2Fields(candidate) ? 2u : 1u);
        if (version != 1 && version != 2)
        {
            throw new InvalidOperationException("Shader effect payload.version must be 1 or 2");
        }

        if (version == 1 && HasSchemaV2Fields(candidate))
        {
            throw new InvalidOperationException(
                "Shader effect payload.version must be 2 when keywords, property bindings, or techniques are used");
        }

        ShaderEffectDefinition definition = ShaderSerialization.DeserializeFromJson(candidate.GetRawText());
        string id = !string.IsNullOrWhiteSpace(definition.Id.Name)
            ? definition.Id.Name
            : DeriveShaderEffectId(sourcePath);
        return definition with { Id = new ShaderSymbol(id), Version = version };
    }

    private static bool HasSchemaV2Fields(JsonElement candidate)
    {
        if (candidate.TryGetProperty("keywords", out _) ||
            candidate.TryGetProperty("defaultTechnique", out _) ||
            candidate.TryGetProperty("techniques", out _))
        {
            return true;
        }

        if (candidate.TryGetProperty("properties", out JsonElement properties) &&
            properties.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement entry in properties.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("binding", out _))
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal static string DeriveShaderEffectId(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return "shader-effect";
        }

        ReadOnlySpan<char> path = sourcePath.AsSpan();
        int query = path.IndexOfAny('?', '#');
        if (query >= 0)
        {
            path = path[..query];
        }

        int slash = path.LastIndexOfAny('/', '\\');
        ReadOnlySpan<char> leaf = (slash >= 0 ? path[(slash + 1)..] : path).Trim();
        foreach (string ext in new[] { ".effect.json", ".shader.json", ".json" })
        {
            if (leaf.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                leaf = leaf[..^ext.Length];
                break;
            }
        }

        int dot = leaf.LastIndexOf('.');
        if (dot >= 0)
        {
            leaf = leaf[..dot];
        }

        return leaf.IsEmpty ? "shader-effect" : new string(leaf);
    }
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
