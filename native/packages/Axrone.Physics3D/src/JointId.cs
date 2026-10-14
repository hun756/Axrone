namespace Axrone.Physics3D;

using System.Numerics;

/// <summary>
/// Type-safe nominal identifier for constraints. Upper 4 bits encode the joint kind,
/// lower 28 bits encode the dense slot index. Register-passable (4 bytes).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct JointId(uint Value) :
    IComparable<JointId>,
    IEquatable<JointId>,
    IComparisonOperators<JointId, JointId, bool>,
    IEqualityOperators<JointId, JointId, bool>
{
    private const int KindShift = 28;
    private const uint IndexMask = 0x0FFF_FFFF;

    /// <summary>Invalid sentinel; never a registered joint.</summary>
    public static JointId Invalid => default;

    /// <summary>Wraps a kind and a dense slot index.</summary>
    /// <param name="kind">The constraint archetype.</param>
    /// <param name="index">The dense slot index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JointId(JointKind kind, uint index)
        : this((((uint)kind + 1u) << KindShift) | (index & IndexMask))
    {
    }

    /// <summary>Whether this identity names a registered joint.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Value >> KindShift) != 0;
    }

    /// <summary>The constraint archetype encoded in the identity.</summary>
    public JointKind Kind
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (JointKind)((Value >> KindShift) - 1u);
    }

    /// <summary>The dense slot index encoded in the identity.</summary>
    public uint SlotIndex
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value & IndexMask;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(JointId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => IsValid ? $"Joint({Kind}:{SlotIndex})" : "Joint(Invalid)";

    /// <summary>Determines whether the left identity sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(JointId left, JointId right) => left.Value < right.Value;

    /// <summary>Determines whether the left identity sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(JointId left, JointId right) => left.Value > right.Value;

    /// <summary>Determines whether the left identity sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(JointId left, JointId right) => left.Value <= right.Value;

    /// <summary>Determines whether the left identity sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(JointId left, JointId right) => left.Value >= right.Value;

    /// <summary>Unwraps the underlying identifier.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint(JointId id) => id.Value;
}
