namespace Axrone.Input;

using System.Collections.ObjectModel;

/// <summary>
/// Versioned engine snapshot: users plus ordered contexts with bindings.
/// Definitions (button/axis/vector tuning) stay engine-side and are not captured.
/// </summary>
public sealed class InputSystemSnapshot
{
    /// <summary>Snapshot version; currently 1.</summary>
    public int Version { get; init; } = 1;

    /// <summary>Captured users.</summary>
    public Collection<InputUserSnapshot> Users { get; } = new();

    /// <summary>Captured contexts in evaluation order.</summary>
    public Collection<InputContextSnapshot> Contexts { get; } = new();
}

/// <summary>
/// Captured user registration.
/// </summary>
public sealed class InputUserSnapshot
{
    /// <summary>User slot.</summary>
    public required byte User { get; init; }

    /// <summary>Whether the user evaluates.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Keyboard ownership.</summary>
    public bool OwnsKeyboard { get; init; } = true;

    /// <summary>Mouse ownership.</summary>
    public bool OwnsMouse { get; init; } = true;

    /// <summary>Touch ownership.</summary>
    public bool OwnsTouch { get; init; } = true;

    /// <summary>Assigned gamepad bitmask.</summary>
    public uint AssignedGamepadMask { get; init; }
}

/// <summary>
/// Captured action map with bindings per action index.
/// </summary>
public sealed class InputContextSnapshot
{
    /// <summary>Context identity.</summary>
    public required ulong Context { get; init; }

    /// <summary>Conflict priority.</summary>
    public int Priority { get; init; }

    /// <summary>Whether the context evaluates.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Capture mode.</summary>
    public ContextCaptureMode Capture { get; init; } = ContextCaptureMode.None;

    /// <summary>Owning user slot.</summary>
    public byte User { get; init; }

    /// <summary>Bindings per action index.</summary>
    public Collection<InputBinding[]> BindingsByAction { get; } = new();
}

/// <summary>
/// Engine snapshot and restore. Part 5 of the <see cref="InputEngine{TSchema}"/> partial.
/// </summary>
public sealed partial class InputEngine<TSchema> where TSchema : unmanaged, Enum
{
    /// <summary>Captures users and contexts.</summary>
    public InputSystemSnapshot Snapshot()
    {
        ThrowIfDisposed();
        var snapshot = new InputSystemSnapshot();
        foreach (InputUserDefinition user in _users.Values)
        {
            snapshot.Users.Add(new InputUserSnapshot
            {
                User = user.Id.Value,
                Enabled = user.Enabled,
                OwnsKeyboard = user.OwnsKeyboard,
                OwnsMouse = user.OwnsMouse,
                OwnsTouch = user.OwnsTouch,
                AssignedGamepadMask = user.AssignedGamepadMask,
            });
        }

        foreach (ContextEntry context in _contexts)
        {
            var captured = new InputContextSnapshot
            {
                Context = context.Id.Value,
                Priority = context.Priority.Value,
                Enabled = context.Enabled,
                Capture = context.Capture,
                User = context.UserId.Value,
            };
            for (int a = 0; a < _actionCount; a++)
            {
                captured.BindingsByAction.Add((InputBinding[])context.GetBindings(a).Clone());
            }

            snapshot.Contexts.Add(captured);
        }

        return snapshot;
    }

    /// <summary>Restores users and contexts.</summary>
    /// <param name="snapshot">Snapshot to apply.</param>
    /// <param name="merge">Whether to keep existing entries.</param>
    public void Restore(InputSystemSnapshot snapshot, bool merge = false)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Version != 1)
        {
            ThrowHelper.ThrowSnapshotError("input.invalid-snapshot", "Unsupported snapshot version.");
        }

        foreach (InputContextSnapshot context in snapshot.Contexts)
        {
            if (context.BindingsByAction.Count > _actionCount)
            {
                ThrowHelper.ThrowSnapshotError("input.invalid-snapshot", "Snapshot has more actions than the schema.");
            }
        }

        if (!merge)
        {
            _users.Clear();
            _contexts.Clear();
        }

        foreach (InputUserSnapshot user in snapshot.Users)
        {
            _users[new UserId(user.User)] = new InputUserDefinition
            {
                Id = new UserId(user.User),
                Enabled = user.Enabled,
                OwnsKeyboard = user.OwnsKeyboard,
                OwnsMouse = user.OwnsMouse,
                OwnsTouch = user.OwnsTouch,
                AssignedGamepadMask = user.AssignedGamepadMask,
            };
        }

        foreach (InputContextSnapshot captured in snapshot.Contexts)
        {
            ContextEntry? existing = FindContext(new ContextId(captured.Context));
            if (existing is not null && merge)
            {
                existing.Enabled = captured.Enabled;
                for (int a = 0; a < captured.BindingsByAction.Count; a++)
                {
                    existing.SetBindings(a, captured.BindingsByAction[a]);
                }

                continue;
            }

            if (existing is not null)
            {
                _contexts.Remove(existing);
            }

            var entry = new ContextEntry(
                new ContextId(captured.Context),
                new PriorityLevel(captured.Priority),
                captured.Enabled,
                new UserId(captured.User),
                captured.Capture,
                _actionCount);
            entry.Sequence = _sequence++;
            for (int a = 0; a < _actionCount; a++)
            {
                entry.EnsureSlot(a, _kinds[a], _buttonDefs[a], _axisDefs[a], _vectorDefs[a]);
                if (a < captured.BindingsByAction.Count)
                {
                    entry.SetBindings(a, captured.BindingsByAction[a]);
                }
            }

            entry.CaptureDefaults();
            _contexts.Add(entry);
        }

        _contexts.Sort(static (a, b) =>
        {
            int order = b.Priority.CompareTo(a.Priority);
            return order != 0 ? order : a.Sequence.CompareTo(b.Sequence);
        });
    }
}
