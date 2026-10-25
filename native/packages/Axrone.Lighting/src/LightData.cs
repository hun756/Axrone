namespace Axrone.Lighting;

/// <summary>Flat discriminated light record (zero vtable, cache aligned).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct LightData : IEquatable<LightData>
{
    /// <summary>Light identity.</summary>
    public readonly LightId Id;

    /// <summary>Rig-local sequence.</summary>
    public readonly ulong Sequence;

    /// <summary>Light type.</summary>
    public readonly LightType Type;

    /// <summary>State flags.</summary>
    public readonly LightFlags Flags;

    private readonly ushort _alignmentPadding;

    /// <summary>Priority.</summary>
    public readonly LightPriority Priority;

    /// <summary>Intensity.</summary>
    public readonly LightIntensity Intensity;

    /// <summary>Color (linear).</summary>
    public readonly Vec3 Color;

    /// <summary>World position.</summary>
    public readonly Vec3 Position;

    /// <summary>Normalized direction.</summary>
    public readonly Vec3 Direction;

    /// <summary>Ambient term (directional).</summary>
    public readonly Vec3 Ambient;

    /// <summary>Range.</summary>
    public readonly LightRange Range;

    /// <summary>Attenuation.</summary>
    public readonly AttenuationFactor Attenuation;

    /// <summary>Inner cone cosine.</summary>
    public readonly ConeCosine InnerConeCosine;

    /// <summary>Outer cone cosine.</summary>
    public readonly ConeCosine OuterConeCosine;

    /// <summary>Whether the light is enabled.</summary>
    public bool IsEnabled
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Flags & LightFlags.Enabled) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private LightData(
        LightId id,
        ulong sequence,
        LightType type,
        LightFlags flags,
        LightPriority priority,
        LightIntensity intensity,
        Vec3 color,
        Vec3 position,
        Vec3 direction,
        Vec3 ambient,
        LightRange range,
        AttenuationFactor attenuation,
        ConeCosine innerConeCosine,
        ConeCosine outerConeCosine)
    {
        Id = id;
        Sequence = sequence;
        Type = type;
        Flags = flags;
        _alignmentPadding = 0;
        Priority = priority;
        Intensity = intensity;
        Color = color;
        Position = position;
        Direction = direction;
        Ambient = ambient;
        Range = range;
        Attenuation = attenuation;
        InnerConeCosine = innerConeCosine;
        OuterConeCosine = outerConeCosine;
    }

    /// <summary>Creates a directional light.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LightData CreateDirectional(
        LightId id,
        ulong sequence,
        in Vec3 direction,
        in Vec3 color,
        in Vec3 ambient,
        LightIntensity intensity,
        LightPriority priority,
        bool enabled = true)
    {
        LightingGuards.ValidateVectorFinite(direction, nameof(direction));
        LightingGuards.ValidateVectorFinite(color, nameof(color));
        LightingGuards.ValidateVectorFinite(ambient, nameof(ambient));
        Vec3 normDir = LightingGuards.ValidateAndNormalize(direction);

        return new LightData(
            id,
            sequence,
            LightType.Directional,
            enabled ? LightFlags.Enabled : LightFlags.None,
            priority,
            intensity,
            color,
            Vec3.Zero,
            normDir,
            ambient,
            LightRange.Default,
            AttenuationFactor.Default,
            ConeCosine.DefaultInner,
            ConeCosine.DefaultOuter);
    }

    /// <summary>Creates a point light.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LightData CreatePoint(
        LightId id,
        ulong sequence,
        in Vec3 position,
        in Vec3 color,
        LightIntensity intensity,
        LightPriority priority,
        LightRange range,
        AttenuationFactor attenuation,
        bool enabled = true)
    {
        LightingGuards.ValidateVectorFinite(position, nameof(position));
        LightingGuards.ValidateVectorFinite(color, nameof(color));

        return new LightData(
            id,
            sequence,
            LightType.Point,
            enabled ? LightFlags.Enabled : LightFlags.None,
            priority,
            intensity,
            color,
            position,
            Vec3.Zero,
            Vec3.Zero,
            range,
            attenuation,
            ConeCosine.DefaultInner,
            ConeCosine.DefaultOuter);
    }

    /// <summary>Creates a spot light.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LightData CreateSpot(
        LightId id,
        ulong sequence,
        in Vec3 position,
        in Vec3 direction,
        in Vec3 color,
        LightIntensity intensity,
        LightPriority priority,
        LightRange range,
        AttenuationFactor attenuation,
        ConeCosine innerConeCosine,
        ConeCosine outerConeCosine,
        bool enabled = true)
    {
        LightingGuards.ValidateVectorFinite(position, nameof(position));
        LightingGuards.ValidateVectorFinite(color, nameof(color));
        Vec3 normDir = LightingGuards.ValidateAndNormalize(direction);
        LightingGuards.ValidateConeCosines(innerConeCosine.Value, outerConeCosine.Value);

        return new LightData(
            id,
            sequence,
            LightType.Spot,
            enabled ? LightFlags.Enabled : LightFlags.None,
            priority,
            intensity,
            color,
            position,
            normDir,
            Vec3.Zero,
            range,
            attenuation,
            innerConeCosine,
            outerConeCosine);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(LightData other) => Id == other.Id && Sequence == other.Sequence;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is LightData other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Id, Sequence);

    /// <inheritdoc/>
    public static bool operator ==(LightData left, LightData right) => left.Equals(right);

    /// <inheritdoc/>
    public static bool operator !=(LightData left, LightData right) => !left.Equals(right);
}
