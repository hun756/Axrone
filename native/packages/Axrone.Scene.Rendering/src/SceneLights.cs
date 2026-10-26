namespace Axrone.Scene.Rendering;

/// <summary>Directional light data component.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct SceneDirectionalLight : ISceneDataComponent
{
    /// <summary>Linear color.</summary>
    public Vec3 Color;

    /// <summary>Ambient contribution.</summary>
    public Vec3 Ambient;

    /// <summary>Intensity.</summary>
    public float Intensity;

    /// <summary>Whether this light wins directional selection.</summary>
    public bool Primary;

    /// <summary>Whether the light participates in collection.</summary>
    public bool Enabled;

    /// <summary>Default white directional light.</summary>
    public static SceneDirectionalLight Default
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new()
        {
            Color = Vec3.One,
            Ambient = Vec3.Zero,
            Intensity = 1.0f,
            Primary = false,
            Enabled = true
        };
    }
}

/// <summary>Point light data component.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct ScenePointLight : ISceneDataComponent
{
    /// <summary>Linear color.</summary>
    public Vec3 Color;

    /// <summary>Intensity.</summary>
    public float Intensity;

    /// <summary>Influence radius.</summary>
    public float Range;

    /// <summary>Falloff exponent.</summary>
    public float Attenuation;

    /// <summary>Whether the light participates in collection.</summary>
    public bool Enabled;

    /// <summary>Default white point light.</summary>
    public static ScenePointLight Default
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new()
        {
            Color = Vec3.One,
            Intensity = 1.0f,
            Range = 8.0f,
            Attenuation = 2.0f,
            Enabled = true
        };
    }
}

/// <summary>Spot light data component.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct SceneSpotLight : ISceneDataComponent
{
    /// <summary>Linear color.</summary>
    public Vec3 Color;

    /// <summary>Intensity.</summary>
    public float Intensity;

    /// <summary>Influence radius.</summary>
    public float Range;

    /// <summary>Falloff exponent.</summary>
    public float Attenuation;

    /// <summary>Inner cone half-angle in radians.</summary>
    public float InnerConeAngleRadians;

    /// <summary>Outer cone half-angle in radians.</summary>
    public float OuterConeAngleRadians;

    /// <summary>Whether the light participates in collection.</summary>
    public bool Enabled;

    /// <summary>Default white spot light.</summary>
    public static SceneSpotLight Default
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new()
        {
            Color = Vec3.One,
            Intensity = 1.0f,
            Range = 8.0f,
            Attenuation = 2.0f,
            InnerConeAngleRadians = 0.5f,
            OuterConeAngleRadians = 1.0f,
            Enabled = true
        };
    }
}
