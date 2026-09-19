namespace Axrone.Batching;

/// <summary>
/// Overflow policy for bounded batch structures.
/// </summary>
/// <remarks>
/// Static-abstract so the choice monomorphizes at the call site with no vtable — the house
/// pattern from <c>Axrone.Utility.Backoff.ISpinBackoff</c>. Unlike a decorative policy, this one
/// is read on the full path: the deque either rejects or drops-oldest, never ignores it.
/// </remarks>
public interface IBatchEvictionPolicy
{
    /// <summary>Whether a full structure drops the oldest item to make room.</summary>
    static abstract bool DropOldest { get; }

    /// <summary>Accounts dropped items.</summary>
    /// <param name="droppedCounter">Running dropped counter.</param>
    /// <param name="count">Items dropped by this overflow.</param>
    static abstract void OnDropped(ref long droppedCounter, int count);
}

/// <summary>Overflow rejects the incoming item; nothing is lost silently.</summary>
public readonly struct RejectNewEvictionPolicy : IBatchEvictionPolicy
{
    /// <inheritdoc />
    public static bool DropOldest => false;

    /// <inheritdoc />
    public static void OnDropped(ref long droppedCounter, int count)
    {
    }
}

/// <summary>Overflow drops the oldest item to admit the incoming one.</summary>
public readonly struct DropOldestEvictionPolicy : IBatchEvictionPolicy
{
    /// <inheritdoc />
    public static bool DropOldest => true;

    /// <inheritdoc />
    public static void OnDropped(ref long droppedCounter, int count) =>
        Interlocked.Add(ref droppedCounter, count);
}
