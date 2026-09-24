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

/// <summary>Live state machine: time advance, transition arbitration, evaluation.</summary>
public sealed class StateMachineInstance
{
    private readonly AnimationState[] _states;
    private readonly StateTransition[] _anyStateTransitions;

    private StateTransition? _activeTransition;
    private int _transitionSourceStateIndex;
    private float _transitionProgress;
    private float _transitionDurationSec;
    private float _targetNormalizedTime;

    /// <summary>Current state index.</summary>
    public int CurrentStateIndex { get; private set; }

    /// <summary>Current normalized time.</summary>
    public float StateNormalizedTime { get; private set; }

    /// <summary>Previous normalized time (event window).</summary>
    public float PreviousNormalizedTime { get; private set; }

    /// <summary>Creates an instance over states with any-state edges and an entry.</summary>
    public StateMachineInstance(AnimationState[] states, StateTransition[] anyStateTransitions, int entryStateIndex)
    {
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(anyStateTransitions);
        if (states.Length == 0)
        {
            AnimationThrowHelper.ThrowValidation(AnimationErrorCode.ValidationControllerEmpty, "State machine requires at least one state.");
        }

        if ((uint)entryStateIndex >= (uint)states.Length)
        {
            AnimationThrowHelper.ThrowValidation(AnimationErrorCode.StateMachineInvalidTransition, "Entry state index out of range.");
        }

        _states = states;
        _anyStateTransitions = anyStateTransitions;
        CurrentStateIndex = entryStateIndex;
    }

    /// <summary>Snaps to a state without blending.</summary>
    public void ForceState(int stateIndex, float normalizedTime = 0.0f)
    {
        if ((uint)stateIndex >= (uint)_states.Length)
        {
            AnimationThrowHelper.ThrowValidation(AnimationErrorCode.StateMachineInvalidTransition, "Forced state index out of range.");
        }

        CurrentStateIndex = stateIndex;
        StateNormalizedTime = normalizedTime;
        PreviousNormalizedTime = normalizedTime;
        _activeTransition = null;
    }

    /// <summary>Starts a manual cross-fade.</summary>
    public void CrossFade(int targetStateIndex, float duration, float offset = 0.0f)
    {
        if ((uint)targetStateIndex >= (uint)_states.Length)
        {
            AnimationThrowHelper.ThrowValidation(AnimationErrorCode.StateMachineInvalidTransition, "Cross-fade target out of range.");
        }

        if (targetStateIndex == CurrentStateIndex && _activeTransition == null)
        {
            return;
        }

        _activeTransition = new StateTransition(targetStateIndex, duration) { Offset = offset, HasFixedDuration = true, CanInterrupt = true };
        _transitionSourceStateIndex = CurrentStateIndex;
        _transitionProgress = 0.0f;
        _transitionDurationSec = duration;
        _targetNormalizedTime = offset;
    }

    /// <summary>Advances time, starts due transitions, and drives the active blend.</summary>
    public void Update(float deltaTime, ParameterStore parameters, ICollection<ClipEvent> outEvents, float layerWeight)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(outEvents);

        AnimationState current = _states[CurrentStateIndex];
        float motionDuration = current.RootMotion.GetDuration();
        float effectiveSpeed = MathF.Abs(current.Speed) > AnimationConstants.SoaEpsilon ? current.Speed : 1.0f;
        float stateDuration = MathF.Max(motionDuration / effectiveSpeed, 1e-6f);

        PreviousNormalizedTime = StateNormalizedTime;
        StateNormalizedTime += deltaTime / stateDuration;

        if (_activeTransition != null)
        {
            AdvanceTransition(deltaTime, parameters, current, stateDuration);
        }
        else
        {
            StateTransition? next = CheckTransitions(current, parameters);
            if (next != null)
            {
                _activeTransition = next;
                _transitionSourceStateIndex = CurrentStateIndex;
                _transitionProgress = 0.0f;
                _transitionDurationSec = next.HasFixedDuration ? next.Duration : next.Duration * stateDuration;
                _targetNormalizedTime = next.Offset;
                ConsumeTransitionTriggers(next, parameters);
            }
        }

        current.RootMotion.CollectEvents(PreviousNormalizedTime, StateNormalizedTime, layerWeight, outEvents);
    }

    private void AdvanceTransition(float deltaTime, ParameterStore parameters, AnimationState current, float stateDuration)
    {
        StateTransition active = _activeTransition!;
        AnimationState target = _states[active.TargetStateIndex];
        float targetMotionDuration = target.RootMotion.GetDuration();
        float targetSpeed = MathF.Abs(target.Speed) > AnimationConstants.SoaEpsilon ? target.Speed : 1.0f;
        float targetDuration = MathF.Max(targetMotionDuration / targetSpeed, 1e-6f);

        _targetNormalizedTime += deltaTime / targetDuration;
        _transitionProgress += _transitionDurationSec > AnimationConstants.SoaEpsilon ? deltaTime / _transitionDurationSec : 1.0f;

        if (active.CanInterrupt)
        {
            StateTransition? interrupt = CheckTransitions(current, parameters);
            if (interrupt != null && interrupt.TargetStateIndex != active.TargetStateIndex)
            {
                if (_transitionProgress >= 0.5f)
                {
                    _transitionSourceStateIndex = active.TargetStateIndex;
                }

                _activeTransition = interrupt;
                _transitionProgress = 0.0f;
                _transitionDurationSec = interrupt.HasFixedDuration ? interrupt.Duration : interrupt.Duration * stateDuration;
                _targetNormalizedTime = interrupt.Offset;
                ConsumeTransitionTriggers(interrupt, parameters);
                return;
            }
        }

        if (_transitionProgress >= 1.0f)
        {
            CurrentStateIndex = active.TargetStateIndex;
            StateNormalizedTime = _targetNormalizedTime;
            PreviousNormalizedTime = _targetNormalizedTime;
            _activeTransition = null;
        }
    }

    private static void ConsumeTransitionTriggers(StateTransition transition, ParameterStore parameters)
    {
        foreach (ParameterCondition condition in transition.Conditions)
        {
            if (parameters.IsTrigger(condition.ParameterName))
            {
                parameters.ConsumeTrigger(condition.ParameterName);
            }
        }
    }

    private StateTransition? CheckTransitions(AnimationState current, ParameterStore parameters)
    {
        StateTransition? best = null;

        for (int i = 0; i < _anyStateTransitions.Length; i++)
        {
            StateTransition candidate = _anyStateTransitions[i];
            if (candidate.TargetStateIndex == CurrentStateIndex)
            {
                continue;
            }

            if (EvaluateTransition(candidate, parameters, StateNormalizedTime, PreviousNormalizedTime)
                && (best == null || CompareTransitions(candidate, best) > 0))
            {
                best = candidate;
            }
        }

        foreach (StateTransition candidate in current.Transitions)
        {
            if (EvaluateTransition(candidate, parameters, StateNormalizedTime, PreviousNormalizedTime)
                && (best == null || CompareTransitions(candidate, best) > 0))
            {
                best = candidate;
            }
        }

        return best;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CompareTransitions(StateTransition a, StateTransition b)
    {
        int priority = a.Priority.CompareTo(b.Priority);
        if (priority != 0)
        {
            return priority;
        }

        return b.TieBreakerIndex.CompareTo(a.TieBreakerIndex);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool EvaluateTransition(StateTransition transition, ParameterStore parameters, float curNormTime, float prevNormTime)
    {
        if (transition.ExitTime.HasValue)
        {
            float exit = transition.ExitTime.Value;
            float prev = prevNormTime % 1.0f;
            float cur = curNormTime % 1.0f;
            bool crossed = (prev < exit && exit <= cur) || (prev > cur && (exit >= prev || exit <= cur));
            if (!crossed)
            {
                return false;
            }
        }

        foreach (ParameterCondition condition in transition.Conditions)
        {
            if (!parameters.EvaluateCondition(condition))
            {
                return false;
            }
        }

        return true;
    }
}
