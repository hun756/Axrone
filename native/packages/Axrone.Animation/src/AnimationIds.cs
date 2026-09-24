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
