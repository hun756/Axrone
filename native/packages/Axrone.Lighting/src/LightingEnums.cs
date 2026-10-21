namespace Axrone.Lighting;

/// <summary>Light type.</summary>
public enum LightType : byte
{
    /// <summary>Directional sun light.</summary>
    Directional = 0,

    /// <summary>Point light.</summary>
    Point = 1,

    /// <summary>Spot light.</summary>
    Spot = 2,
}

/// <summary>Light state flags.</summary>
[Flags]
public enum LightFlags : byte
{
    /// <summary>No flags.</summary>
    None = 0,

    /// <summary>Light is enabled.</summary>
    Enabled = 1 << 0,
}

/// <summary>Frame sort strategy.</summary>
public enum LightSortStrategy : byte
{
    /// <summary>Insertion order.</summary>
    None = 0,

    /// <summary>Priority order.</summary>
    Priority = 1,

    /// <summary>Influence order.</summary>
    Influence = 2,
}

/// <summary>Shader uniform semantic.</summary>
public enum UniformSemantic : byte
{
    /// <summary>Ambient light color.</summary>
    AmbientLight = 0,

    /// <summary>Sky light color.</summary>
    SkyLight,

    /// <summary>Ground light color.</summary>
    GroundLight,

    /// <summary>Exposure.</summary>
    Exposure,

    /// <summary>Gamma.</summary>
    Gamma,

    /// <summary>Directional light count.</summary>
    DirectionalLightCount,

    /// <summary>Directional light direction.</summary>
    DirectionalLightDirection,

    /// <summary>Directional light color.</summary>
    DirectionalLightColor,

    /// <summary>Directional ambient color.</summary>
    DirectionalLightAmbientColor,

    /// <summary>Directional intensity.</summary>
    DirectionalLightIntensity,

    /// <summary>Point light count.</summary>
    PointLightCount,

    /// <summary>Point light position.</summary>
    PointLightPosition,

    /// <summary>Point light color.</summary>
    PointLightColor,

    /// <summary>Point light intensity.</summary>
    PointLightIntensity,

    /// <summary>Point light range.</summary>
    PointLightRange,

    /// <summary>Point light attenuation.</summary>
    PointLightAttenuation,

    /// <summary>Spot light count.</summary>
    SpotLightCount,

    /// <summary>Spot light position.</summary>
    SpotLightPosition,

    /// <summary>Spot light direction.</summary>
    SpotLightDirection,

    /// <summary>Spot light color.</summary>
    SpotLightColor,

    /// <summary>Spot light intensity.</summary>
    SpotLightIntensity,

    /// <summary>Spot light range.</summary>
    SpotLightRange,

    /// <summary>Spot inner cone cosine.</summary>
    SpotLightInnerConeCosine,

    /// <summary>Spot outer cone cosine.</summary>
    SpotLightOuterConeCosine,

    /// <summary>Local light count.</summary>
    LocalLightCount,

    /// <summary>Local light kind.</summary>
    LocalLightKind,

    /// <summary>Local light position.</summary>
    LocalLightPosition,

    /// <summary>Local light direction.</summary>
    LocalLightDirection,

    /// <summary>Local light color.</summary>
    LocalLightColor,

    /// <summary>Local light intensity.</summary>
    LocalLightIntensity,

    /// <summary>Local light range.</summary>
    LocalLightRange,

    /// <summary>Local light attenuation.</summary>
    LocalLightAttenuation,

    /// <summary>Local inner cone cosine.</summary>
    LocalLightInnerConeCosine,

    /// <summary>Local outer cone cosine.</summary>
    LocalLightOuterConeCosine,
}
