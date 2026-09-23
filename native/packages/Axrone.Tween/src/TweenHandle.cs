namespace Axrone.Tween;

/// <summary>
/// Generational tween handle routing control operations to the owning engine. Stale handles
/// (recycled slots) fail safely instead of touching the new occupant.
/// </summary>
public readonly struct TweenHandle : IEquatable<TweenHandle>
{
    private readonly TweenEngine? _engine;

    /// <summary>Tween identity.</summary>
    public TweenId Id { get; }

    internal TweenHandle(TweenEngine engine, TweenId id)
    {
        _engine = engine;
        Id = id;
    }

    /// <summary>Whether the handle names a live engine allocation.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _engine != null && Id.IsValid;
    }

    /// <summary>Current state; stale identities report inactive.</summary>
    public TweenState State
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _engine?.QueryState(Id) ?? TweenState.Inactive;
    }

    /// <summary>Cancels the tween; false for stale identities.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Cancel() => _engine != null && _engine.Cancel(Id);

    /// <summary>Pauses a playing tween.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Pause() => _engine != null && _engine.Pause(Id);

    /// <summary>Resumes a paused tween.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Resume() => _engine != null && _engine.Resume(Id);

    /// <summary>
    /// Awaits termination through the owning engine (no downcast, no polling): true for natural
    /// completion, false for cancel, fault, or unknown outcome.
    /// </summary>
    public TaskAwaiter<bool> GetAwaiter() =>
        (_engine != null ? _engine.AwaitAsync(Id) : Task.FromResult(false)).GetAwaiter();

    /// <inheritdoc/>
    public bool Equals(TweenHandle other) => Id.Equals(other.Id) && ReferenceEquals(_engine, other._engine);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TweenHandle other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_engine, Id);

    public static bool operator ==(TweenHandle left, TweenHandle right) => left.Equals(right);

    public static bool operator !=(TweenHandle left, TweenHandle right) => !left.Equals(right);
}
