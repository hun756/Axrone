namespace Axrone.AssetGltf;

/// <summary>Cache-line padded atomic import lifecycle state.</summary>
[StructLayout(LayoutKind.Explicit, Size = 128)]
public struct PaddedAtomicLifecycleState
{
    [FieldOffset(0)]
    private int _state;

    [FieldOffset(64)]
    private int _inFlightOperations;

    /// <summary>Compares and exchanges the state.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryTransition(int expected, int target) =>
        Interlocked.CompareExchange(ref _state, target, expected) == expected;

    /// <summary>Increments in-flight operations.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IncrementOps() => Interlocked.Increment(ref _inFlightOperations);

    /// <summary>Decrements in-flight operations.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int DecrementOps() => Interlocked.Decrement(ref _inFlightOperations);

    /// <summary>Current state.</summary>
    public int State
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _state);
    }

    /// <summary>In-flight operation count.</summary>
    public int InFlightOps
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _inFlightOperations);
    }
}
