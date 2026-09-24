namespace Axrone.Animation;

/// <summary>Layer composition mode.</summary>
public enum LayerMode
{
    /// <summary>Masked override blend.</summary>
    Override = 0,

    /// <summary>Rest-relative additive blend.</summary>
    Additive = 1,
}

/// <summary>One state machine lane with weight, mode, and optional bone mask.</summary>
public sealed class AnimationLayer
{
    /// <summary>Layer identity.</summary>
    public LayerId Id { get; }

    /// <summary>Driven state machine.</summary>
    public StateMachineInstance Machine { get; }

    /// <summary>Composition mode.</summary>
    public LayerMode Mode { get; init; } = LayerMode.Override;

    /// <summary>Layer weight.</summary>
    public float Weight { get; set; } = 1.0f;

    /// <summary>Optional bone mask (null includes everything).</summary>
    public AnimationMask? BoneMask { get; init; }

    /// <summary>Creates a layer.</summary>
    public AnimationLayer(LayerId id, StateMachineInstance machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        Id = id;
        Machine = machine;
    }
}
