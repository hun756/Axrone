namespace Axrone.Input;

/// <summary>
/// Registered action map: per-action bindings, tuning and one committed slot
/// per action. Slots rebuild when bindings or definitions change.
/// </summary>
internal sealed class ContextEntry
{
    private readonly InputBinding[][] _bindings;
    private readonly InputButtonSlot?[] _buttonSlots;
    private readonly InputAxisSlot?[] _axisSlots;
    private readonly InputVector2Slot?[] _vectorSlots;
    private readonly InputButtonActionDefinition?[] _buttonDefs;
    private readonly InputAxisActionDefinition?[] _axisDefs;
    private readonly InputVector2ActionDefinition?[] _vectorDefs;
    private readonly ActionKind[] _kinds;

    /// <summary>Creates an entry from raw fields.</summary>
    /// <param name="id">Context identity.</param>
    /// <param name="priority">Conflict priority.</param>
    /// <param name="enabled">Whether the context evaluates.</param>
    /// <param name="userId">Owning user.</param>
    /// <param name="capture">Capture mode.</param>
    /// <param name="actionCount">Schema action count.</param>
    public ContextEntry(ContextId id, PriorityLevel priority, bool enabled, UserId userId, ContextCaptureMode capture, int actionCount)
    {
        Id = id;
        Priority = priority;
        Enabled = enabled;
        UserId = userId;
        Capture = capture;
        _bindings = new InputBinding[actionCount][];
        _buttonSlots = new InputButtonSlot[actionCount];
        _axisSlots = new InputAxisSlot[actionCount];
        _vectorSlots = new InputVector2Slot[actionCount];
        _buttonDefs = new InputButtonActionDefinition[actionCount];
        _axisDefs = new InputAxisActionDefinition[actionCount];
        _vectorDefs = new InputVector2ActionDefinition[actionCount];
        _kinds = new ActionKind[actionCount];
        for (int i = 0; i < actionCount; i++)
        {
            _bindings[i] = [];
        }
    }

    /// <summary>Context identity.</summary>
    public ContextId Id { get; }

    /// <summary>Conflict priority.</summary>
    public PriorityLevel Priority { get; }

    /// <summary>Whether the context evaluates.</summary>
    public bool Enabled { get; set; }

    /// <summary>Owning user.</summary>
    public UserId UserId { get; }

    /// <summary>Capture mode.</summary>
    public ContextCaptureMode Capture { get; }

    /// <summary>Registration order; breaks priority ties.</summary>
    public ulong Sequence { get; set; }

    /// <summary>Bindings for an action.</summary>
    /// <param name="index">Action index.</param>
    public InputBinding[] GetBindings(int index) => _bindings[index];

    /// <summary>Replaces bindings for an action.</summary>
    /// <param name="index">Action index.</param>
    /// <param name="bindings">New bindings.</param>
    public void SetBindings(int index, InputBinding[] bindings)
    {
        _bindings[index] = (InputBinding[])bindings.Clone();
        RebuildSlot(index);
    }

    /// <summary>Ensures a slot exists for an action kind.</summary>
    /// <param name="index">Action index.</param>
    /// <param name="kind">Value shape.</param>
    /// <param name="button">Button tuning.</param>
    /// <param name="axis">Axis tuning.</param>
    /// <param name="vector">Vector tuning.</param>
    public void EnsureSlot(
        int index,
        ActionKind kind,
        InputButtonActionDefinition? button,
        InputAxisActionDefinition? axis,
        InputVector2ActionDefinition? vector)
    {
        _kinds[index] = kind;
        _buttonDefs[index] = button;
        _axisDefs[index] = axis;
        _vectorDefs[index] = vector;
        RebuildSlot(index);
    }

    /// <summary>Button slot for an action.</summary>
    /// <param name="index">Action index.</param>
    public InputButtonSlot? ButtonSlot(int index) => _buttonSlots[index];

    /// <summary>Axis slot for an action.</summary>
    /// <param name="index">Action index.</param>
    public InputAxisSlot? AxisSlot(int index) => _axisSlots[index];

    /// <summary>Vector slot for an action.</summary>
    /// <param name="index">Action index.</param>
    public InputVector2Slot? VectorSlot(int index) => _vectorSlots[index];

    /// <summary>Value shape for an action.</summary>
    /// <param name="index">Action index.</param>
    public ActionKind Kind(int index) => _kinds[index];

    /// <summary>Clears every gesture.</summary>
    public void ResetAll()
    {
        for (int i = 0; i < _buttonSlots.Length; i++)
        {
            _buttonSlots[i]?.Reset();
            _axisSlots[i]?.Reset();
            _vectorSlots[i]?.Reset();
        }
    }

    /// <summary>Rebuilds one slot from stored tuning and bindings.</summary>
    /// <param name="index">Action index.</param>
    private void RebuildSlot(int index)
    {
        switch (_kinds[index])
        {
            case ActionKind.Axis:
                _axisSlots[index] = new InputAxisSlot(_axisDefs[index] ?? new InputAxisActionDefinition(), _bindings[index]);
                _buttonSlots[index] = null;
                _vectorSlots[index] = null;
                break;
            case ActionKind.Vector2:
                _vectorSlots[index] = new InputVector2Slot(_vectorDefs[index] ?? new InputVector2ActionDefinition(), _bindings[index]);
                _buttonSlots[index] = null;
                _axisSlots[index] = null;
                break;
            default:
                _buttonSlots[index] = new InputButtonSlot(_buttonDefs[index] ?? new InputButtonActionDefinition(), _bindings[index]);
                _axisSlots[index] = null;
                _vectorSlots[index] = null;
                break;
        }
    }
}
