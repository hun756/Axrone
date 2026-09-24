namespace Axrone.Animation;

/// <summary>Strongly-typed rig identity.</summary>
public readonly record struct RigId(string Value)
{
    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Unwraps the value.</summary>
    public static implicit operator string(RigId id) => id.Value;
}

/// <summary>Strongly-typed clip identity.</summary>
public readonly record struct ClipId(string Value)
{
    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Unwraps the value.</summary>
    public static implicit operator string(ClipId id) => id.Value;
}

/// <summary>Strongly-typed layer identity.</summary>
public readonly record struct LayerId(string Value)
{
    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Unwraps the value.</summary>
    public static implicit operator string(LayerId id) => id.Value;
}

/// <summary>Strongly-typed state identity.</summary>
public readonly record struct StateId(string Value)
{
    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Unwraps the value.</summary>
    public static implicit operator string(StateId id) => id.Value;
}

/// <summary>Strongly-typed parameter identity.</summary>
public readonly record struct ParameterId(string Value)
{
    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Unwraps the value.</summary>
    public static implicit operator string(ParameterId id) => id.Value;
}

/// <summary>Strongly-typed curve identity.</summary>
public readonly record struct CurveId(string Value)
{
    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Unwraps the value.</summary>
    public static implicit operator string(CurveId id) => id.Value;
}

/// <summary>Strongly-typed IK job identity.</summary>
public readonly record struct IkJobId(string Value)
{
    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Unwraps the value.</summary>
    public static implicit operator string(IkJobId id) => id.Value;
}

/// <summary>Strongly-typed retarget profile identity.</summary>
public readonly record struct RetargetProfileId(string Value)
{
    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Unwraps the value.</summary>
    public static implicit operator string(RetargetProfileId id) => id.Value;
}
