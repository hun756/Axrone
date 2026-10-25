namespace Axrone.Scene.Assets;

using System.Collections.Frozen;

/// <summary>
/// Immutable material definition: shader binding, surface parameters, pass
/// overrides and custom uniforms. Passes and uniforms are copied on
/// construction, so later caller mutation cannot change a registered material.
/// </summary>
public sealed class SceneMaterialDefinition : ISceneAssetDefinition<MaterialAssetTag>
{
    /// <summary>Creates a material definition.</summary>
    public SceneMaterialDefinition(
        SceneAssetId<MaterialAssetTag> id,
        SceneAssetId<ShaderAssetTag> shaderId,
        SceneMaterialSurface surface,
        IReadOnlyList<SceneMaterialPass>? passes = null,
        IReadOnlyDictionary<string, SceneUniformValue>? uniforms = null)
    {
        if (!id.IsValid)
        {
            throw new SceneAssetException(
                SceneAssetErrorCodes.Invalid,
                "Material definitions require a non-empty id.");
        }

        if (!shaderId.IsValid)
        {
            throw new SceneAssetException(
                SceneAssetErrorCodes.Invalid,
                $"Material '{id}' requires a valid shader id.");
        }

        ArgumentNullException.ThrowIfNull(surface);
        surface.Validate();

        SceneMaterialPass[] passArray = passes is null ? [] : [.. passes];
        HashSet<string> passIds = new(StringComparer.Ordinal);
        for (int i = 0; i < passArray.Length; i++)
        {
            SceneMaterialPass pass = passArray[i] ??
                throw new SceneAssetException(
                    SceneAssetErrorCodes.Invalid,
                    $"Material '{id}' contains a null pass.");
            if (!passIds.Add(pass.Id))
            {
                throw new SceneAssetException(
                    SceneAssetErrorCodes.Invalid,
                    $"Material '{id}' declares duplicate pass '{pass.Id}'.");
            }
        }

        Id = id;
        ShaderId = shaderId;
        Surface = surface;
        Passes = passArray;
        Uniforms = CreateUniforms(id, uniforms);
    }

    /// <inheritdoc/>
    public SceneAssetId<MaterialAssetTag> Id { get; }

    /// <summary>Shader used by the material.</summary>
    public SceneAssetId<ShaderAssetTag> ShaderId { get; }

    /// <summary>Surface parameters.</summary>
    public SceneMaterialSurface Surface { get; }

    /// <summary>Pass overrides in declaration order.</summary>
    public IReadOnlyList<SceneMaterialPass> Passes { get; }

    /// <summary>Custom uniform values.</summary>
    public IReadOnlyDictionary<string, SceneUniformValue> Uniforms { get; }

    private static FrozenDictionary<string, SceneUniformValue> CreateUniforms(
        SceneAssetId<MaterialAssetTag> id,
        IReadOnlyDictionary<string, SceneUniformValue>? uniforms)
    {
        if (uniforms is null || uniforms.Count == 0)
        {
            return FrozenDictionary<string, SceneUniformValue>.Empty;
        }

        foreach (string name in uniforms.Keys)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new SceneAssetException(
                    SceneAssetErrorCodes.Invalid,
                    $"Material '{id}' contains an empty uniform name.");
            }
        }

        return uniforms.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
