namespace Axrone.Scene.Assets;

using System.Collections.Frozen;

/// <summary>
/// Immutable shader definition: source pair, reflected uniform names and the
/// semantic-to-attribute binding used by the draw path.
/// </summary>
public sealed class SceneShaderDefinition : ISceneAssetDefinition<ShaderAssetTag>
{
    /// <summary>Creates a shader definition.</summary>
    public SceneShaderDefinition(
        SceneAssetId<ShaderAssetTag> id,
        string vertexSource,
        string fragmentSource,
        IReadOnlyList<string>? uniforms = null,
        IReadOnlyDictionary<SceneMeshSemantic, string>? attributes = null,
        bool depthTest = true,
        bool cull = true,
        bool blend = false)
    {
        if (!id.IsValid)
        {
            throw new SceneAssetException(
                SceneAssetErrorCodes.Invalid,
                "Shader definitions require a non-empty id.");
        }

        if (string.IsNullOrWhiteSpace(vertexSource))
        {
            throw new SceneAssetException(
                SceneAssetErrorCodes.Invalid,
                $"Shader '{id}' requires vertex source.");
        }

        if (string.IsNullOrWhiteSpace(fragmentSource))
        {
            throw new SceneAssetException(
                SceneAssetErrorCodes.Invalid,
                $"Shader '{id}' requires fragment source.");
        }

        Id = id;
        VertexSource = vertexSource;
        FragmentSource = fragmentSource;
        Uniforms = uniforms is null ? [] : [.. uniforms];
        Attributes = attributes is null
            ? FrozenDictionary<SceneMeshSemantic, string>.Empty
            : attributes.ToFrozenDictionary();
        DepthTest = depthTest;
        Cull = cull;
        Blend = blend;
    }

    /// <inheritdoc/>
    public SceneAssetId<ShaderAssetTag> Id { get; }

    /// <summary>Vertex shader source.</summary>
    public string VertexSource { get; }

    /// <summary>Fragment shader source.</summary>
    public string FragmentSource { get; }

    /// <summary>Uniform names declared by the shader.</summary>
    public IReadOnlyList<string> Uniforms { get; }

    /// <summary>Semantic-to-attribute-name bindings.</summary>
    public IReadOnlyDictionary<SceneMeshSemantic, string> Attributes { get; }

    /// <summary>Default depth-test state.</summary>
    public bool DepthTest { get; }

    /// <summary>Default face-culling state.</summary>
    public bool Cull { get; }

    /// <summary>Default blending state.</summary>
    public bool Blend { get; }
}
