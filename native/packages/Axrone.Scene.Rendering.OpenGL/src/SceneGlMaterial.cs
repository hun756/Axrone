namespace Axrone.Scene.Rendering.OpenGL;

/// <summary>Resolved texture/sampler pair for one material slot.</summary>
public readonly record struct SceneGlTextureSlot(
    string UniformName,
    int Unit,
    GLTexture Texture,
    GLSampler Sampler);

/// <summary>
/// GPU-ready material: compiled shader, resolved texture slots and the canonical
/// definition used for uniform and state binding.
/// </summary>
public sealed class SceneGlMaterial
{
    /// <summary>Creates a GPU material.</summary>
    public SceneGlMaterial(
        SceneMaterialDefinition definition,
        SceneGlShader shader,
        SceneGlTextureSlot[] textureSlots)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(shader);
        ArgumentNullException.ThrowIfNull(textureSlots);

        Definition = definition;
        Shader = shader;
        TextureSlots = textureSlots;
        PrimaryPass = ResolvePrimaryPass(definition.Passes);
    }

    /// <summary>Canonical material definition.</summary>
    public SceneMaterialDefinition Definition { get; }

    /// <summary>Compiled shader.</summary>
    public SceneGlShader Shader { get; }

    /// <summary>Resolved texture slots.</summary>
    public IReadOnlyList<SceneGlTextureSlot> TextureSlots { get; }

    /// <summary>Pass used for state overrides, if any.</summary>
    public SceneMaterialPass? PrimaryPass { get; }

    /// <summary>Surface parameters.</summary>
    public SceneMaterialSurface Surface => Definition.Surface;

    /// <summary>Custom uniform values.</summary>
    public IReadOnlyDictionary<string, SceneUniformValue> Uniforms => Definition.Uniforms;

    private static SceneMaterialPass? ResolvePrimaryPass(IReadOnlyList<SceneMaterialPass> passes)
    {
        if (passes.Count == 0)
        {
            return null;
        }

        for (int i = 0; i < passes.Count; i++)
        {
            if (string.Equals(passes[i].Id, "main", StringComparison.Ordinal))
            {
                return passes[i];
            }
        }

        return passes[0];
    }
}
