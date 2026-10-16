namespace Axrone.Input;

/// <summary>
/// Engine context-free reads: highest-priority enabled context wins, mirroring
/// consumption order. Part 4 of the <see cref="InputEngine{TSchema}"/> partial.
/// </summary>
public sealed partial class InputEngine<TSchema> where TSchema : unmanaged, Enum
{
    /// <summary>Reads the winning button state for an action.</summary>
    /// <param name="action">Action value.</param>
    /// <param name="user">Reading user, or any user.</param>
    /// <param name="state">Winning state.</param>
    /// <returns><see langword="true"/> when a slot applies.</returns>
    public bool TryReadAnyButton(TSchema action, UserId user, out InputButtonState state)
    {
        ThrowIfDisposed();
        int index = ToIndex(action);
        for (int i = 0; i < _contexts.Count; i++)
        {
            ContextEntry context = _contexts[i];
            if (!context.Enabled || !UserMatches(context.UserId, user))
            {
                continue;
            }

            InputButtonSlot? slot = context.ButtonSlot(index);
            if (slot is null)
            {
                continue;
            }

            state = slot.State;
            return true;
        }

        state = default;
        return false;
    }

    /// <summary>Reads the winning axis state for an action.</summary>
    /// <param name="action">Action value.</param>
    /// <param name="user">Reading user, or any user.</param>
    /// <param name="state">Winning state.</param>
    /// <returns><see langword="true"/> when a slot applies.</returns>
    public bool TryReadAnyAxis(TSchema action, UserId user, out InputAxisState state)
    {
        ThrowIfDisposed();
        int index = ToIndex(action);
        for (int i = 0; i < _contexts.Count; i++)
        {
            ContextEntry context = _contexts[i];
            if (!context.Enabled || !UserMatches(context.UserId, user))
            {
                continue;
            }

            InputAxisSlot? slot = context.AxisSlot(index);
            if (slot is null)
            {
                continue;
            }

            state = slot.State;
            return true;
        }

        state = default;
        return false;
    }

    /// <summary>Reads the winning vector state for an action.</summary>
    /// <param name="action">Action value.</param>
    /// <param name="user">Reading user, or any user.</param>
    /// <param name="state">Winning state.</param>
    /// <returns><see langword="true"/> when a slot applies.</returns>
    public bool TryReadAnyVector2(TSchema action, UserId user, out InputVector2State state)
    {
        ThrowIfDisposed();
        int index = ToIndex(action);
        for (int i = 0; i < _contexts.Count; i++)
        {
            ContextEntry context = _contexts[i];
            if (!context.Enabled || !UserMatches(context.UserId, user))
            {
                continue;
            }

            InputVector2Slot? slot = context.VectorSlot(index);
            if (slot is null)
            {
                continue;
            }

            state = slot.State;
            return true;
        }

        state = default;
        return false;
    }

    /// <summary>Reads the winning value for an action.</summary>
    /// <param name="action">Action value.</param>
    /// <param name="user">Reading user, or any user.</param>
    public InputActionValue ReadValue(TSchema action, UserId user)
    {
        ThrowIfDisposed();
        int index = ToIndex(action);
        return _kinds[index] switch
        {
            ActionKind.Axis when TryReadAnyAxis(action, user, out InputAxisState axis) => new InputActionValue(axis.Value),
            ActionKind.Vector2 when TryReadAnyVector2(action, user, out InputVector2State vector) => new InputActionValue(vector.Value),
            _ when TryReadAnyButton(action, user, out InputButtonState button) => new InputActionValue(button.Value),
            _ => default,
        };
    }

    /// <summary>Whether an action is actuated.</summary>
    /// <param name="action">Action value.</param>
    /// <param name="user">Reading user, or any user.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsActive(TSchema action, UserId user)
    {
        InputActionValue value = ReadValue(action, user);
        return value.Kind switch
        {
            ActionKind.Button => value.BoolValue,
            ActionKind.Axis => value.FloatValue != 0.0f,
            ActionKind.Vector2 => value.Vector2Value != Vector2.Zero,
            _ => false,
        };
    }

    /// <summary>Whether a button transitioned down this frame.</summary>
    /// <param name="action">Action value.</param>
    /// <param name="user">Reading user, or any user.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsPressed(TSchema action, UserId user) =>
        TryReadAnyButton(action, user, out InputButtonState state) && state.Pressed;

    /// <summary>Whether a button transitioned up this frame.</summary>
    /// <param name="action">Action value.</param>
    /// <param name="user">Reading user, or any user.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsReleased(TSchema action, UserId user) =>
        TryReadAnyButton(action, user, out InputButtonState state) && state.Released;

    /// <summary>Whether a context owner matches a reader.</summary>
    /// <param name="owner">Context owner.</param>
    /// <param name="reader">Reading user, or any user.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool UserMatches(UserId owner, UserId reader) =>
        owner.Equals(UserId.Any) || reader.Equals(UserId.Any) || owner.Equals(reader);
}
