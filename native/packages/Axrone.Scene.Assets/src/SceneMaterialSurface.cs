namespace Axrone.Scene.Assets;

/// <summary>
/// Immutable material surface description. Standard PBR inputs live here;
/// arbitrary shader uniforms live on the material definition.
/// </summary>
public sealed class SceneMaterialSurface
{
    /// <summary>Shading model.</summary>
    public SceneMaterialShadingModel ShadingModel { get; init; } = SceneMaterialShadingModel.Pbr;

    /// <summary>Alpha handling mode.</summary>
    public SceneMaterialAlphaMode AlphaMode { get; init; } = SceneMaterialAlphaMode.Opaque;

    /// <summary>Alpha-test threshold in the zero-to-one range.</summary>
    public float AlphaCutoff { get; init; } = 0.5f;

    /// <summary>Enabled surface features.</summary>
    public SceneMaterialFeatures Features { get; init; } = SceneMaterialFeatures.None;

    /// <summary>Base albedo color.</summary>
    public Vec4 Albedo { get; init; } = Vec4.One;

    /// <summary>Emissive color.</summary>
    public Vec3 Emissive { get; init; } = Vec3.Zero;

    /// <summary>Metallic response in the zero-to-one range.</summary>
    public float Metallic { get; init; }

    /// <summary>Roughness in the zero-to-one range.</summary>
    public float Roughness { get; init; } = 1.0f;

    /// <summary>Normal-map strength.</summary>
    public float NormalScale { get; init; } = 1.0f;

    /// <summary>Occlusion strength in the zero-to-one range.</summary>
    public float Occlusion { get; init; } = 1.0f;

    /// <summary>UV tiling.</summary>
    public Vec2 Tiling { get; init; } = Vec2.One;

    /// <summary>UV offset.</summary>
    public Vec2 Offset { get; init; } = Vec2.Zero;

    /// <summary>Optional albedo texture.</summary>
    public SceneMaterialTextureBinding? AlbedoMap { get; init; }

    /// <summary>Optional normal texture.</summary>
    public SceneMaterialTextureBinding? NormalMap { get; init; }

    /// <summary>Optional PBR parameter texture.</summary>
    public SceneMaterialTextureBinding? PbrMap { get; init; }

    /// <summary>Optional occlusion texture.</summary>
    public SceneMaterialTextureBinding? OcclusionMap { get; init; }

    /// <summary>Optional emissive texture.</summary>
    public SceneMaterialTextureBinding? EmissiveMap { get; init; }

    internal void Validate()
    {
        ValidateUnitRange(AlphaCutoff, "Alpha cutoff");
        ValidateUnitRange(Metallic, "Metallic");
        ValidateUnitRange(Roughness, "Roughness");
        ValidateUnitRange(Occlusion, "Occlusion");

        if (!float.IsFinite(NormalScale))
        {
            throw new SceneAssetException(
                SceneAssetErrorCodes.Invalid,
                "Normal scale must be finite.");
        }

        ValidateFinite(Albedo.X, Albedo.Y, Albedo.Z, Albedo.W, "Albedo");
        ValidateFinite(Emissive.X, Emissive.Y, Emissive.Z, "Emissive");
        ValidateFinite(Tiling.X, Tiling.Y, "Tiling");
        ValidateFinite(Offset.X, Offset.Y, "Offset");

        ValidateBinding(AlbedoMap, "albedo");
        ValidateBinding(NormalMap, "normal");
        ValidateBinding(PbrMap, "PBR");
        ValidateBinding(OcclusionMap, "occlusion");
        ValidateBinding(EmissiveMap, "emissive");
    }

    private static void ValidateBinding(SceneMaterialTextureBinding? binding, string name)
    {
        if (binding is { } value && !value.TextureId.IsValid)
        {
            throw new SceneAssetException(
                SceneAssetErrorCodes.Invalid,
                $"Material {name} texture binding must reference a valid texture.");
        }
    }

    private static void ValidateUnitRange(float value, string name)
    {
        if (!float.IsFinite(value) || value is < 0.0f or > 1.0f)
        {
            throw new SceneAssetException(
                SceneAssetErrorCodes.Invalid,
                $"{name} must be between zero and one.");
        }
    }

    private static void ValidateFinite(float x, float y, string name)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y))
        {
            throw new SceneAssetException(
                SceneAssetErrorCodes.Invalid,
                $"{name} components must be finite.");
        }
    }

    private static void ValidateFinite(float x, float y, float z, string name)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z))
        {
            throw new SceneAssetException(
                SceneAssetErrorCodes.Invalid,
                $"{name} components must be finite.");
        }
    }

    private static void ValidateFinite(float x, float y, float z, float w, string name)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z) || !float.IsFinite(w))
        {
            throw new SceneAssetException(
                SceneAssetErrorCodes.Invalid,
                $"{name} components must be finite.");
        }
    }
}
