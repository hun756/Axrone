namespace Axrone.Input;

/// <summary>
/// Engine registry management: context upsert, removal, activation and
/// priority changes. Part 6 of the <see cref="InputEngine{TSchema}"/> partial.
/// </summary>
public sealed partial class InputEngine<TSchema> where TSchema : unmanaged, Enum
{
    /// <summary>Whether a context is registered.</summary>
    /// <param name="id">Context identity.</param>
    public bool HasContext(ContextId id)
    {
        ThrowIfDisposed();
        return FindContext(id) is not null;
    }

    /// <summary>Registers or replaces an action map.</summary>
    /// <param name="definition">Context registration.</param>
    public void UpsertContext(InputContextDefinition<TSchema> definition)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(definition);
        ContextEntry? existing = FindContext(definition.Id);
        if (existing is null)
        {
            RegisterContext(definition);
            return;
        }

        for (int i = 0; i < _actionCount; i++)
        {
            if (i < definition.BindingsByAction.Length)
            {
                ValidateAll(_kinds[i], definition.BindingsByAction[i]);
            }
        }

        var replacement = new ContextEntry(
            definition.Id, definition.Priority, definition.Enabled,
            definition.UserId, definition.Capture, _actionCount)
        {
            Sequence = existing.Sequence,
        };
        for (int i = 0; i < _actionCount; i++)
        {
            replacement.EnsureSlot(i, _kinds[i], _buttonDefs[i], _axisDefs[i], _vectorDefs[i]);
            if (i < definition.BindingsByAction.Length)
            {
                replacement.SetBindings(i, definition.BindingsByAction[i]);
            }
        }

        replacement.CaptureDefaults();
        _contexts.Remove(existing);
        _contexts.Add(replacement);
        SortContexts();
    }

    /// <summary>Removes an action map.</summary>
    /// <param name="id">Context identity.</param>
    /// <returns><see langword="true"/> when removed.</returns>
    public bool RemoveContext(ContextId id)
    {
        ThrowIfDisposed();
        ContextEntry? existing = FindContext(id);
        if (existing is null)
        {
            return false;
        }

        if (_rebind is not null && _rebind.IsActive && _rebind.Request.ContextId.Equals(id))
        {
            CancelRebind();
        }

        _contexts.Remove(existing);
        return true;
    }

    /// <summary>Enables or disables an action map.</summary>
    /// <param name="id">Context identity.</param>
    /// <param name="enabled">Whether the context evaluates.</param>
    /// <returns><see langword="true"/> when the context exists.</returns>
    public bool ActivateContext(ContextId id, bool enabled)
    {
        ThrowIfDisposed();
        ContextEntry? existing = FindContext(id);
        if (existing is null)
        {
            return false;
        }

        existing.Enabled = enabled;
        return true;
    }

    /// <summary>Changes an action map's priority and re-sorts.</summary>
    /// <param name="id">Context identity.</param>
    /// <param name="priority">New priority.</param>
    /// <returns><see langword="true"/> when the context exists.</returns>
    public bool SetContextPriority(ContextId id, PriorityLevel priority)
    {
        ThrowIfDisposed();
        ContextEntry? existing = FindContext(id);
        if (existing is null)
        {
            return false;
        }

        existing.Priority = priority;
        SortContexts();
        return true;
    }

    /// <summary>Sorts contexts by priority then registration order.</summary>
    private void SortContexts() =>
        _contexts.Sort(static (a, b) =>
        {
            int order = b.Priority.CompareTo(a.Priority);
            return order != 0 ? order : a.Sequence.CompareTo(b.Sequence);
        });

    /// <summary>Reads a user registration.</summary>
    /// <param name="id">User identity.</param>
    /// <param name="user">User registration.</param>
    /// <returns><see langword="true"/> when the user exists.</returns>
    public bool TryGetUser(UserId id, out InputUserDefinition? user)
    {
        ThrowIfDisposed();
        return _users.TryGetValue(id, out user);
    }

    /// <summary>Registers or replaces a user.</summary>
    /// <param name="definition">User registration.</param>
    public void UpsertUser(InputUserDefinition definition)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(definition);
        _users[definition.Id] = definition;
    }

    /// <summary>Removes a user.</summary>
    /// <param name="id">User identity.</param>
    /// <returns><see langword="true"/> when removed.</returns>
    public bool RemoveUser(UserId id)
    {
        ThrowIfDisposed();
        return _users.Remove(id);
    }

    /// <summary>Enables or disables a user.</summary>
    /// <param name="id">User identity.</param>
    /// <param name="enabled">Whether the user evaluates.</param>
    /// <returns><see langword="true"/> when the user exists.</returns>
    public bool SetUserEnabled(UserId id, bool enabled)
    {
        ThrowIfDisposed();
        if (!_users.TryGetValue(id, out InputUserDefinition? user))
        {
            return false;
        }

        _users[id] = new InputUserDefinition
        {
            Id = user.Id,
            Enabled = enabled,
            OwnsKeyboard = user.OwnsKeyboard,
            OwnsMouse = user.OwnsMouse,
            OwnsTouch = user.OwnsTouch,
            AssignedGamepadMask = user.AssignedGamepadMask,
        };
        return true;
    }

    /// <summary>Assigns a gamepad slot, stealing it from any other owner.</summary>
    /// <param name="id">User identity.</param>
    /// <param name="slot">Gamepad slot.</param>
    /// <returns><see langword="true"/> when the user exists and the slot is valid.</returns>
    public bool AssignGamepad(UserId id, byte slot)
    {
        ThrowIfDisposed();
        if (slot >= 4 || !_users.TryGetValue(id, out InputUserDefinition? user))
        {
            return false;
        }

        uint bit = 1u << slot;
        foreach (UserId other in new List<UserId>(_users.Keys))
        {
            if (!other.Equals(id) && (_users[other].AssignedGamepadMask & bit) != 0)
            {
                InputUserDefinition current = _users[other];
                _users[other] = new InputUserDefinition
                {
                    Id = current.Id,
                    Enabled = current.Enabled,
                    OwnsKeyboard = current.OwnsKeyboard,
                    OwnsMouse = current.OwnsMouse,
                    OwnsTouch = current.OwnsTouch,
                    AssignedGamepadMask = current.AssignedGamepadMask & ~bit,
                };
            }
        }

        _users[id] = new InputUserDefinition
        {
            Id = user.Id,
            Enabled = user.Enabled,
            OwnsKeyboard = user.OwnsKeyboard,
            OwnsMouse = user.OwnsMouse,
            OwnsTouch = user.OwnsTouch,
            AssignedGamepadMask = user.AssignedGamepadMask | bit,
        };
        return true;
    }

    /// <summary>Releases a gamepad slot from its owner.</summary>
    /// <param name="slot">Gamepad slot.</param>
    /// <returns><see langword="true"/> when a slot was released.</returns>
    public bool ReleaseGamepad(byte slot)
    {
        ThrowIfDisposed();
        if (slot >= 4)
        {
            return false;
        }

        uint bit = 1u << slot;
        bool released = false;
        foreach (UserId other in new List<UserId>(_users.Keys))
        {
            if ((_users[other].AssignedGamepadMask & bit) != 0)
            {
                InputUserDefinition current = _users[other];
                _users[other] = new InputUserDefinition
                {
                    Id = current.Id,
                    Enabled = current.Enabled,
                    OwnsKeyboard = current.OwnsKeyboard,
                    OwnsMouse = current.OwnsMouse,
                    OwnsTouch = current.OwnsTouch,
                    AssignedGamepadMask = current.AssignedGamepadMask & ~bit,
                };
                released = true;
            }
        }

        return released;
    }
}
