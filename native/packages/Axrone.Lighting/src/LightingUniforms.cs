namespace Axrone.Lighting;

/// <summary>Shader uniform descriptor.</summary>
public readonly record struct UniformDescriptor(string Name, UniformSemantic Semantic, byte VectorDimension);

/// <summary>Uniform layout metadata and shader defines.</summary>
public sealed class LightingUniformMetadata
{
    /// <summary>Max directional define.</summary>
    public const string DefineMaxDirectional = "AXRONE_LIGHTING_MAX_DIRECTIONAL_LIGHTS";

    /// <summary>Max point define.</summary>
    public const string DefineMaxPoint = "AXRONE_LIGHTING_MAX_POINT_LIGHTS";

    /// <summary>Max spot define.</summary>
    public const string DefineMaxSpot = "AXRONE_LIGHTING_MAX_SPOT_LIGHTS";

    /// <summary>Max local define.</summary>
    public const string DefineMaxLocal = "AXRONE_LIGHTING_MAX_LOCAL_LIGHTS";

    /// <summary>All uniform descriptors.</summary>
    public static readonly UniformDescriptor[] Descriptors =
    [
        new("u_AmbientLight", UniformSemantic.AmbientLight, 3),
        new("u_SkyLight", UniformSemantic.SkyLight, 3),
        new("u_GroundLight", UniformSemantic.GroundLight, 3),
        new("u_Exposure", UniformSemantic.Exposure, 1),
        new("u_Gamma", UniformSemantic.Gamma, 1),

        new("u_DirectionalLightCount", UniformSemantic.DirectionalLightCount, 1),
        new("u_DirectionalLightDirection", UniformSemantic.DirectionalLightDirection, 3),
        new("u_DirectionalLightColor", UniformSemantic.DirectionalLightColor, 3),
        new("u_DirectionalLightAmbientColor", UniformSemantic.DirectionalLightAmbientColor, 3),
        new("u_DirectionalLightIntensity", UniformSemantic.DirectionalLightIntensity, 1),

        new("u_PointLightCount", UniformSemantic.PointLightCount, 1),
        new("u_PointLightPosition", UniformSemantic.PointLightPosition, 3),
        new("u_PointLightColor", UniformSemantic.PointLightColor, 3),
        new("u_PointLightIntensity", UniformSemantic.PointLightIntensity, 1),
        new("u_PointLightRange", UniformSemantic.PointLightRange, 1),
        new("u_PointLightAttenuation", UniformSemantic.PointLightAttenuation, 1),

        new("u_SpotLightCount", UniformSemantic.SpotLightCount, 1),
        new("u_SpotLightPosition", UniformSemantic.SpotLightPosition, 3),
        new("u_SpotLightDirection", UniformSemantic.SpotLightDirection, 3),
        new("u_SpotLightColor", UniformSemantic.SpotLightColor, 3),
        new("u_SpotLightIntensity", UniformSemantic.SpotLightIntensity, 1),
        new("u_SpotLightRange", UniformSemantic.SpotLightRange, 1),
        new("u_SpotLightInnerConeCosine", UniformSemantic.SpotLightInnerConeCosine, 1),
        new("u_SpotLightOuterConeCosine", UniformSemantic.SpotLightOuterConeCosine, 1),

        new("u_LocalLightCount", UniformSemantic.LocalLightCount, 1),
        new("u_LocalLightKind", UniformSemantic.LocalLightKind, 1),
        new("u_LocalLightPosition", UniformSemantic.LocalLightPosition, 3),
        new("u_LocalLightDirection", UniformSemantic.LocalLightDirection, 3),
        new("u_LocalLightColor", UniformSemantic.LocalLightColor, 3),
        new("u_LocalLightIntensity", UniformSemantic.LocalLightIntensity, 1),
        new("u_LocalLightRange", UniformSemantic.LocalLightRange, 1),
        new("u_LocalLightAttenuation", UniformSemantic.LocalLightAttenuation, 1),
        new("u_LocalLightInnerConeCosine", UniformSemantic.LocalLightInnerConeCosine, 1),
        new("u_LocalLightOuterConeCosine", UniformSemantic.LocalLightOuterConeCosine, 1),
    ];

    /// <summary>Shader defines.</summary>
    public IReadOnlyDictionary<string, string> ShaderDefines { get; }

    /// <summary>Creates metadata.</summary>
    public LightingUniformMetadata(in LightingCapacity? capacity = null)
    {
        LightingCapacity cap = capacity ?? LightingCapacity.Default;
        ShaderDefines = new Dictionary<string, string>(4, StringComparer.Ordinal)
        {
            [DefineMaxDirectional] = cap.MaxDirectionalLights.ToString(CultureInfo.InvariantCulture),
            [DefineMaxPoint] = cap.MaxPointLights.ToString(CultureInfo.InvariantCulture),
            [DefineMaxSpot] = cap.MaxSpotLights.ToString(CultureInfo.InvariantCulture),
            [DefineMaxLocal] = cap.MaxLocalLights.ToString(CultureInfo.InvariantCulture),
        };
    }
}

/// <summary>Type-safe non-boxing uniform value accessor.</summary>
public readonly ref struct LightingUniformAccessor
{
    private readonly LightingSelectionState _state;

    /// <summary>Creates an accessor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LightingUniformAccessor(LightingSelectionState state)
    {
        _state = state;
    }

    /// <summary>Gets a float span uniform.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<float> GetFloatSpan(UniformSemantic semantic) => semantic switch
    {
        UniformSemantic.DirectionalLightDirection => _state.DirectionalDirections.Span,
        UniformSemantic.DirectionalLightColor => _state.DirectionalColors.Span,
        UniformSemantic.DirectionalLightAmbientColor => _state.DirectionalAmbientColors.Span,
        UniformSemantic.DirectionalLightIntensity => _state.DirectionalIntensities.Span,

        UniformSemantic.PointLightPosition => _state.PointPositions.Span,
        UniformSemantic.PointLightColor => _state.PointColors.Span,
        UniformSemantic.PointLightIntensity => _state.PointIntensities.Span,
        UniformSemantic.PointLightRange => _state.PointRanges.Span,
        UniformSemantic.PointLightAttenuation => _state.PointAttenuations.Span,

        UniformSemantic.SpotLightPosition => _state.SpotPositions.Span,
        UniformSemantic.SpotLightDirection => _state.SpotDirections.Span,
        UniformSemantic.SpotLightColor => _state.SpotColors.Span,
        UniformSemantic.SpotLightIntensity => _state.SpotIntensities.Span,
        UniformSemantic.SpotLightRange => _state.SpotRanges.Span,
        UniformSemantic.SpotLightInnerConeCosine => _state.SpotInnerConeCosines.Span,
        UniformSemantic.SpotLightOuterConeCosine => _state.SpotOuterConeCosines.Span,

        UniformSemantic.LocalLightPosition => _state.LocalPositions.Span,
        UniformSemantic.LocalLightDirection => _state.LocalDirections.Span,
        UniformSemantic.LocalLightColor => _state.LocalColors.Span,
        UniformSemantic.LocalLightIntensity => _state.LocalIntensities.Span,
        UniformSemantic.LocalLightRange => _state.LocalRanges.Span,
        UniformSemantic.LocalLightAttenuation => _state.LocalAttenuations.Span,
        UniformSemantic.LocalLightInnerConeCosine => _state.LocalInnerConeCosines.Span,
        UniformSemantic.LocalLightOuterConeCosine => _state.LocalOuterConeCosines.Span,

        _ => ReadOnlySpan<float>.Empty,
    };

    /// <summary>Gets an int span uniform.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<int> GetIntSpan(UniformSemantic semantic) => semantic switch
    {
        UniformSemantic.LocalLightKind => _state.LocalKinds.Span,
        _ => ReadOnlySpan<int>.Empty,
    };

    /// <summary>Gets a vector uniform.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vec3 GetVector3(UniformSemantic semantic) => semantic switch
    {
        UniformSemantic.AmbientLight => _state.Environment.Ambient,
        UniformSemantic.SkyLight => _state.Environment.Sky,
        UniformSemantic.GroundLight => _state.Environment.Ground,
        _ => Vec3.Zero,
    };

    /// <summary>Gets a float uniform.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetFloat(UniformSemantic semantic) => semantic switch
    {
        UniformSemantic.Exposure => _state.Environment.Exposure,
        UniformSemantic.Gamma => _state.Environment.Gamma,
        _ => 0.0f,
    };

    /// <summary>Gets an int uniform.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetInt(UniformSemantic semantic) => semantic switch
    {
        UniformSemantic.DirectionalLightCount => _state.Stats.SelectedDirectionalCount,
        UniformSemantic.PointLightCount => _state.Stats.SelectedPointCount,
        UniformSemantic.SpotLightCount => _state.Stats.SelectedSpotCount,
        UniformSemantic.LocalLightCount => _state.Stats.SelectedLocalLightCount,
        _ => 0,
    };
}
