namespace Axrone.Scene.Assets;

/// <summary>
/// Scene asset catalog. The registry owns the shared name table used to create
/// stable typed ids and to resolve symbols back to authoring names.
/// </summary>
public sealed class SceneAssetRegistry
{
    /// <summary>Creates a registry, optionally sharing a name table.</summary>
    public SceneAssetRegistry(SceneNameTable? names = null)
    {
        Names = names ?? new SceneNameTable();
    }

    /// <summary>Name table backing all asset symbols.</summary>
    public SceneNameTable Names { get; }

    /// <summary>Shader definitions.</summary>
    public SceneDefinitionRegistry<ShaderAssetTag, SceneShaderDefinition> Shaders { get; } = new();

    /// <summary>Mesh definitions.</summary>
    public SceneDefinitionRegistry<MeshAssetTag, SceneMeshDefinition> Meshes { get; } = new();

    /// <summary>Total number of registered definitions.</summary>
    public int Count => Shaders.Count + Meshes.Count;

    /// <summary>Interns a name and wraps it as a typed asset id.</summary>
    public SceneAssetId<TTag> CreateId<TTag>(string name)
        where TTag : struct
    {
        ArgumentNullException.ThrowIfNull(name);
        return SceneAssetId<TTag>.FromSymbol(Names.Intern(name));
    }

    /// <summary>Resolves a typed id back to its authoring name.</summary>
    public string ResolveName<TTag>(SceneAssetId<TTag> id)
        where TTag : struct =>
        Names.Resolve(id.Value);

    /// <summary>Clears every definition, keeping the name table.</summary>
    public void Clear()
    {
        Shaders.Clear();
        Meshes.Clear();
    }
}
