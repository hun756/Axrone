namespace Axrone.Scene.Assets;

/// <summary>Texture filtering mode.</summary>
public enum SceneFilterMode : byte
{
    /// <summary>Nearest-neighbor sampling.</summary>
    Nearest = 0,

    /// <summary>Linear sampling.</summary>
    Linear = 1,

    /// <summary>Nearest within a mip level, nearest between levels.</summary>
    NearestMipmapNearest = 2,

    /// <summary>Linear within a mip level, nearest between levels.</summary>
    LinearMipmapNearest = 3,

    /// <summary>Nearest within a mip level, linear between levels.</summary>
    NearestMipmapLinear = 4,

    /// <summary>Trilinear sampling.</summary>
    LinearMipmapLinear = 5
}

/// <summary>Texture coordinate wrapping mode.</summary>
public enum SceneWrapMode : byte
{
    /// <summary>Repeat the texture.</summary>
    Repeat = 0,

    /// <summary>Clamp to the edge texel.</summary>
    ClampToEdge = 1,

    /// <summary>Mirror and repeat.</summary>
    MirroredRepeat = 2
}

/// <summary>Immutable sampler state definition.</summary>
public sealed class SceneSamplerDefinition : ISceneAssetDefinition<SamplerAssetTag>
{
    /// <summary>Creates a sampler definition.</summary>
    public SceneSamplerDefinition(
        SceneAssetId<SamplerAssetTag> id,
        SceneFilterMode minFilter = SceneFilterMode.Linear,
        SceneFilterMode magFilter = SceneFilterMode.Linear,
        SceneWrapMode wrapS = SceneWrapMode.Repeat,
        SceneWrapMode wrapT = SceneWrapMode.Repeat,
        SceneWrapMode wrapR = SceneWrapMode.Repeat,
        int maxAnisotropy = 1)
    {
        if (!id.IsValid)
        {
            throw new SceneAssetException(
                SceneAssetErrorCodes.Invalid,
                "Sampler definitions require a non-empty id.");
        }

        if (maxAnisotropy < 1)
        {
            throw new SceneAssetException(
                SceneAssetErrorCodes.Invalid,
                $"Sampler '{id}' max anisotropy must be at least one.");
        }

        Id = id;
        MinFilter = minFilter;
        MagFilter = magFilter;
        WrapS = wrapS;
        WrapT = wrapT;
        WrapR = wrapR;
        MaxAnisotropy = maxAnisotropy;
    }

    /// <inheritdoc/>
    public SceneAssetId<SamplerAssetTag> Id { get; }

    /// <summary>Minification filter.</summary>
    public SceneFilterMode MinFilter { get; }

    /// <summary>Magnification filter.</summary>
    public SceneFilterMode MagFilter { get; }

    /// <summary>Horizontal wrap mode.</summary>
    public SceneWrapMode WrapS { get; }

    /// <summary>Vertical wrap mode.</summary>
    public SceneWrapMode WrapT { get; }

    /// <summary>Depth wrap mode.</summary>
    public SceneWrapMode WrapR { get; }

    /// <summary>Maximum anisotropy; one disables anisotropic filtering.</summary>
    public int MaxAnisotropy { get; }
}
