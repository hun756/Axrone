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
}
