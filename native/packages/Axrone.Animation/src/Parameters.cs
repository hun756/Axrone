namespace Axrone.Animation;

/// <summary>State machine parameter kind.</summary>
public enum ParameterType
{
    /// <summary>Continuous value.</summary>
    Float = 0,

    /// <summary>Discrete value.</summary>
    Int = 1,

    /// <summary>Flag.</summary>
    Bool = 2,

    /// <summary>One-shot consumed on transition.</summary>
    Trigger = 3,
}

/// <summary>Transition condition operator.</summary>
public enum ConditionOperator
{
    /// <summary>Equality.</summary>
    Equal = 0,

    /// <summary>Inequality.</summary>
    NotEqual = 1,

    /// <summary>Strictly greater.</summary>
    GreaterThan = 2,

    /// <summary>Greater or equal.</summary>
    GreaterThanOrEqual = 3,

    /// <summary>Strictly less.</summary>
    LessThan = 4,

    /// <summary>Less or equal.</summary>
    LessThanOrEqual = 5,
}

/// <summary>Single transition condition against a named parameter.</summary>
public readonly record struct ParameterCondition(string ParameterName, ConditionOperator Operator, float Threshold);

/// <summary>
/// Typed parameter bank over parallel arrays. Ints ride their own lane (no float
/// precision loss); triggers consume atomically from the caller's perspective.
/// </summary>
public sealed class ParameterStore
{
    private readonly Dictionary<string, int> _nameToIndex;
    private readonly ParameterType[] _types;
    private readonly float[] _floats;
    private readonly int[] _ints;
    private readonly byte[] _bools;
    private readonly byte[] _triggers;

    /// <summary>Creates a store; duplicate names are rejected.</summary>
    public ParameterStore(Dictionary<string, ParameterType> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        int count = definitions.Count;
        _nameToIndex = new Dictionary<string, int>(count, StringComparer.Ordinal);
        _types = new ParameterType[count];
        _floats = new float[count];
        _ints = new int[count];
        _bools = new byte[count];
        _triggers = new byte[count];

        int index = 0;
        foreach (KeyValuePair<string, ParameterType> definition in definitions)
        {
            if (!_nameToIndex.TryAdd(definition.Key, index))
            {
                AnimationThrowHelper.ThrowValidation(AnimationErrorCode.StateMachineParameterNotFound, $"Duplicate parameter '{definition.Key}'.");
            }

            _types[index] = definition.Value;
            index++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ResolveIndex(string name)
    {
        if (!_nameToIndex.TryGetValue(name, out int index))
        {
            AnimationThrowHelper.ThrowStateMachine(AnimationErrorCode.StateMachineParameterNotFound, $"Parameter '{name}' not found.");
        }

        return index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RequireType(int index, ParameterType type, string name)
    {
        if (_types[index] != type)
        {
            AnimationThrowHelper.ThrowStateMachine(AnimationErrorCode.StateMachineTypeMismatch, $"Parameter '{name}' is not {type}.");
        }
    }

    /// <summary>Sets a float; non-finite values sanitize to zero.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetFloat(string name, float value)
    {
        int index = ResolveIndex(name);
        RequireType(index, ParameterType.Float, name);
        _floats[index] = float.IsFinite(value) ? value : 0.0f;
    }

    /// <summary>Reads a float.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetFloat(string name) => _floats[ResolveIndex(name)];

    /// <summary>Sets an int.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetInt(string name, int value)
    {
        int index = ResolveIndex(name);
        RequireType(index, ParameterType.Int, name);
        _ints[index] = value;
    }

    /// <summary>Reads an int.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetInt(string name) => _ints[ResolveIndex(name)];

    /// <summary>Sets a bool.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetBool(string name, bool value)
    {
        int index = ResolveIndex(name);
        RequireType(index, ParameterType.Bool, name);
        _bools[index] = value ? (byte)1 : (byte)0;
    }

    /// <summary>Reads a bool.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetBool(string name) => _bools[ResolveIndex(name)] != 0;

    /// <summary>Fires a trigger.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetTrigger(string name)
    {
        int index = ResolveIndex(name);
        RequireType(index, ParameterType.Trigger, name);
        _triggers[index] = 1;
    }

    /// <summary>Clears a trigger without reporting.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetTrigger(string name)
    {
        int index = ResolveIndex(name);
        RequireType(index, ParameterType.Trigger, name);
        _triggers[index] = 0;
    }

    /// <summary>Reads and clears a trigger.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ConsumeTrigger(string name)
    {
        int index = ResolveIndex(name);
        RequireType(index, ParameterType.Trigger, name);
        bool wasSet = _triggers[index] != 0;
        _triggers[index] = 0;
        return wasSet;
    }

    /// <summary>Clears all triggers (frame boundary).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearTriggers() => Array.Clear(_triggers, 0, _triggers.Length);

    /// <summary>Evaluates a transition condition against current values.</summary>
    public bool EvaluateCondition(in ParameterCondition condition)
    {
        int index = ResolveIndex(condition.ParameterName);
        ParameterType type = _types[index];

        switch (type)
        {
            case ParameterType.Float:
                float fValue = _floats[index];
                return condition.Operator switch
                {
                    ConditionOperator.Equal => MathF.Abs(fValue - condition.Threshold) <= AnimationConstants.SoaEpsilon,
                    ConditionOperator.NotEqual => MathF.Abs(fValue - condition.Threshold) > AnimationConstants.SoaEpsilon,
                    ConditionOperator.GreaterThan => fValue > condition.Threshold,
                    ConditionOperator.GreaterThanOrEqual => fValue >= condition.Threshold,
                    ConditionOperator.LessThan => fValue < condition.Threshold,
                    ConditionOperator.LessThanOrEqual => fValue <= condition.Threshold,
                    _ => false,
                };

            case ParameterType.Int:
                int iValue = _ints[index];
                int iThreshold = (int)condition.Threshold;
                return condition.Operator switch
                {
                    ConditionOperator.Equal => iValue == iThreshold,
                    ConditionOperator.NotEqual => iValue != iThreshold,
                    ConditionOperator.GreaterThan => iValue > iThreshold,
                    ConditionOperator.GreaterThanOrEqual => iValue >= iThreshold,
                    ConditionOperator.LessThan => iValue < iThreshold,
                    ConditionOperator.LessThanOrEqual => iValue <= iThreshold,
                    _ => false,
                };

            case ParameterType.Bool:
                bool bValue = _bools[index] != 0;
                bool expected = condition.Threshold > 0.5f;
                return condition.Operator switch
                {
                    ConditionOperator.Equal => bValue == expected,
                    ConditionOperator.NotEqual => bValue != expected,
                    _ => false,
                };

            case ParameterType.Trigger:
                return _triggers[index] != 0;

            default:
                return false;
        }
    }
}
