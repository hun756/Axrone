namespace Axrone.Lighting;

/// <summary>Global lighting environment.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct LightingEnvironment
{
    /// <summary>Default environment.</summary>
    public static readonly LightingEnvironment Default = new(
        ambient: new Vec3(0.2f, 0.2f, 0.25f),
        sky: new Vec3(0.4f, 0.45f, 0.55f),
        ground: new Vec3(0.15f, 0.13f, 0.1f),
        exposure: 1.0f,
        gamma: 2.2f);

    /// <summary>Ambient radiance.</summary>
    public readonly Vec3 Ambient;

    /// <summary>Sky radiance.</summary>
    public readonly Vec3 Sky;

    /// <summary>Ground radiance.</summary>
    public readonly Vec3 Ground;

    /// <summary>Exposure.</summary>
    public readonly float Exposure;

    /// <summary>Gamma.</summary>
    public readonly float Gamma;

    /// <summary>Creates an environment.</summary>
    public LightingEnvironment(
        Vec3? ambient = null,
        Vec3? sky = null,
        Vec3? ground = null,
        float? exposure = null,
        float? gamma = null)
    {
        Ambient = ambient ?? new Vec3(0.2f, 0.2f, 0.25f);
        Sky = sky ?? new Vec3(0.4f, 0.45f, 0.55f);
        Ground = ground ?? new Vec3(0.15f, 0.13f, 0.1f);
        Exposure = exposure ?? 1.0f;
        Gamma = gamma ?? 2.2f;

        LightingGuards.ValidateEnvironment(this);
    }
}

/// <summary>Frame capacity limits.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct LightingCapacity
{
    /// <summary>Default capacities.</summary>
    public static readonly LightingCapacity Default = new(
        maxDirectionalLights: 1,
        maxPointLights: 8,
        maxSpotLights: 8,
        maxLocalLights: 12);

    /// <summary>Max directional lights.</summary>
    public readonly int MaxDirectionalLights;

    /// <summary>Max point lights.</summary>
    public readonly int MaxPointLights;

    /// <summary>Max spot lights.</summary>
    public readonly int MaxSpotLights;

    /// <summary>Max local lights.</summary>
    public readonly int MaxLocalLights;

    /// <summary>Creates capacity limits.</summary>
    public LightingCapacity(int maxDirectionalLights = 1, int maxPointLights = 8, int maxSpotLights = 8, int maxLocalLights = 12)
    {
        if (maxDirectionalLights < 0 || maxPointLights < 0 || maxSpotLights < 0 || maxLocalLights < 0)
        {
            ThrowHelper.ThrowValidationError("lighting.rig.invalid-capacity", "Capacities must be non-negative.");
        }

        MaxDirectionalLights = maxDirectionalLights;
        MaxPointLights = maxPointLights;
        MaxSpotLights = maxSpotLights;
        MaxLocalLights = Math.Min(maxLocalLights, maxPointLights + maxSpotLights);
    }
}

/// <summary>Frame resolve options.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ResolveOptions(Vec3? CameraPosition = null, LightSortStrategy SortStrategy = LightSortStrategy.Influence);
