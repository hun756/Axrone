namespace Axrone.Animation;

/// <summary>Conditional edge between states with duration and priority.</summary>
public sealed class StateTransition
{
    private readonly ParameterCondition[] _conditions;

    /// <summary>Destination state index.</summary>
    public int TargetStateIndex { get; init; }

    /// <summary>Blend duration (seconds when fixed, normalized otherwise).</summary>
    public float Duration { get; init; }

    /// <summary>Destination start offset in normalized time.</summary>
    public float Offset { get; init; }

    /// <summary>Normalized exit time gate, when set.</summary>
    public float? ExitTime { get; init; }

    /// <summary>Whether Duration is seconds (true) or normalized (false).</summary>
    public bool HasFixedDuration { get; init; }

    /// <summary>Whether a newer transition may steal this one past halfway.</summary>
    public bool CanInterrupt { get; init; }

    /// <summary>Arbitration priority.</summary>
    public int Priority { get; init; }

    /// <summary>Deterministic tie-breaker.</summary>
    public int TieBreakerIndex { get; init; }

    /// <summary>Guard conditions.</summary>
    public ReadOnlySpan<ParameterCondition> Conditions => _conditions;

    /// <summary>Creates a transition.</summary>
    public StateTransition(int targetStateIndex, float duration, ParameterCondition[]? conditions = null)
    {
        TargetStateIndex = targetStateIndex;
        Duration = duration;
        _conditions = conditions ?? Array.Empty<ParameterCondition>();
    }
}

/// <summary>State: motion plus speed plus outgoing transitions.</summary>
public sealed class AnimationState
{
    private readonly StateTransition[] _transitions;

    /// <summary>State identity.</summary>
    public StateId Id { get; }

    /// <summary>Root motion.</summary>
    public MotionNode RootMotion { get; }

    /// <summary>Playback speed.</summary>
    public float Speed { get; init; } = 1.0f;

    /// <summary>Outgoing transitions.</summary>
    public ReadOnlySpan<StateTransition> Transitions => _transitions;

    /// <summary>Creates a state.</summary>
    public AnimationState(StateId id, MotionNode motion, StateTransition[]? transitions = null)
    {
        ArgumentNullException.ThrowIfNull(motion);
        Id = id;
        RootMotion = motion;
        _transitions = transitions ?? Array.Empty<StateTransition>();
    }
}
