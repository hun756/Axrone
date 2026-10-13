namespace Axrone.Physics2D;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
/// Physical memory or dense array slot index wrapper. Prevents accidental
/// indexing by raw integer primitives.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SlotIndex(int Value) : IComparable<SlotIndex>
{
    /// <summary>Unbound slot; never a live index.</summary>
    public static readonly SlotIndex Invalid = new(-1);

    /// <summary>Whether the index can name a live slot.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value >= 0;
    }

    /// <summary>Unwraps the underlying index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int(SlotIndex index) => index.Value;

    /// <summary>Wraps a raw index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator SlotIndex(int index) => new(index);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(SlotIndex other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left index sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(SlotIndex left, SlotIndex right) => left.Value < right.Value;

    /// <summary>Determines whether the left index sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(SlotIndex left, SlotIndex right) => left.Value > right.Value;

    /// <summary>Determines whether the left index sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(SlotIndex left, SlotIndex right) => left.Value <= right.Value;

    /// <summary>Determines whether the left index sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(SlotIndex left, SlotIndex right) => left.Value >= right.Value;

    /// <inheritdoc/>
    public override string ToString() => $"Slot({Value})";
}

/// <summary>
/// Strongly-typed undirected graph edge representing a manifold contact
/// between two rigid bodies.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ContactPair(BodyId BodyA, BodyId BodyB);

/// <summary>
/// Strongly-typed structural constraint edge joining two rigid bodies.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct JointPair(BodyId BodyA, BodyId BodyB);

/// <summary>
/// Immutable, register-passable aggregate of physics step temporal and
/// kinematic sleep criteria.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SimulationParameters(
    float DeltaTime,
    float TimeToSleep,
    float LinearTolerance,
    float AngularTolerance)
{
    /// <summary>Squared linear quiet threshold.</summary>
    public float LinearToleranceSquared
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => LinearTolerance * LinearTolerance;
    }

    /// <summary>Squared angular quiet threshold.</summary>
    public float AngularToleranceSquared
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => AngularTolerance * AngularTolerance;
    }
}

/// <summary>
/// Role contract segregating body store read/write capabilities for island
/// evaluation. Supports ref-struct implementations.
/// </summary>
internal interface IBodyStore
{
    /// <summary>Addressable slots.</summary>
    int SlotCapacity { get; }

    /// <summary>Whether a slot holds a live body.</summary>
    /// <param name="slot">The slot.</param>
    bool IsSlotLive(SlotIndex slot);

    /// <summary>Live body state by slot.</summary>
    /// <param name="slot">The slot.</param>
    ref BodyState GetSlot(SlotIndex slot);

    /// <summary>Puts a body to sleep or wakes it.</summary>
    /// <param name="id">The body.</param>
    /// <param name="awake">The target state.</param>
    void SetAwake(BodyId id, bool awake);
}
