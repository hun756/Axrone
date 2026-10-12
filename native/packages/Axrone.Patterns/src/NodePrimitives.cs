namespace Axrone.Patterns;

/// <summary>
/// Strongly-typed node-kind discriminator for variant dispatch tables.
/// Negative values are never a registered kind.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct NodeTypeId(int Value) :
    IComparable<NodeTypeId>,
    IEquatable<NodeTypeId>,
    IComparisonOperators<NodeTypeId, NodeTypeId, bool>,
    IEqualityOperators<NodeTypeId, NodeTypeId, bool>
{
    /// <summary>Invalid sentinel; never a registered kind.</summary>
    public static NodeTypeId Invalid => new(-1);

    /// <summary>Whether this value names a registered kind.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value >= 0;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(NodeTypeId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(NodeTypeId other) => Value == other.Value;

    /// <inheritdoc/>
    public override int GetHashCode() => Value;

    /// <inheritdoc/>
    public override string ToString() => IsValid ? $"Type({Value})" : "Type(Invalid)";

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(NodeTypeId left, NodeTypeId right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(NodeTypeId left, NodeTypeId right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(NodeTypeId left, NodeTypeId right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(NodeTypeId left, NodeTypeId right) => left.Value <= right.Value;
}

/// <summary>
/// Strongly-typed slot handle into a node arena. Negative values address nothing.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct NodeHandle(int Value) :
    IComparable<NodeHandle>,
    IEquatable<NodeHandle>,
    IComparisonOperators<NodeHandle, NodeHandle, bool>,
    IEqualityOperators<NodeHandle, NodeHandle, bool>
{
    /// <summary>Invalid sentinel; never a live slot.</summary>
    public static NodeHandle Invalid => new(-1);

    /// <summary>Whether this value addresses a slot.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value >= 0;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(NodeHandle other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(NodeHandle other) => Value == other.Value;

    /// <inheritdoc/>
    public override int GetHashCode() => Value;

    /// <inheritdoc/>
    public override string ToString() => IsValid ? $"Handle({Value})" : "Handle(Invalid)";

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(NodeHandle left, NodeHandle right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(NodeHandle left, NodeHandle right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(NodeHandle left, NodeHandle right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(NodeHandle left, NodeHandle right) => left.Value <= right.Value;
}

/// <summary>
/// Contiguous slice of arena slots, expressed as an offset plus a length.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct NodeRange(int Offset, int Length) : IEquatable<NodeRange>
{
    /// <summary>An empty range at the origin.</summary>
    public static NodeRange Empty => new(0, 0);

    /// <summary>Whether the range covers no slots.</summary>
    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Length == 0;
    }

    /// <summary>Whether <paramref name="index"/> falls inside the range; overflow-safe.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(int index) => (uint)(index - Offset) < (uint)Length;
}

/// <summary>
/// The zero-sized result for policies that only observe: cheaper than a bool
/// to ignore and unambiguous at the call site.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 1)]
public readonly record struct Unit : IEquatable<Unit>, IComparable<Unit>
{
    /// <summary>The single unit value.</summary>
    public static Unit Value => default;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(Unit other) => 0;

    /// <inheritdoc/>
    public override string ToString() => "()";
}

/// <summary>
/// Arena counters isolated on two cache lines: writers bump the count while
/// seal signals travel on their own line.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 128)]
public struct CachePaddedAtomicCounters
{
    /// <summary>Number of live slots.</summary>
    [FieldOffset(0)]
    public int Value;

    /// <summary>Seal/dispose signal line.</summary>
    [FieldOffset(64)]
    public int LockSignal;
}

/// <summary>128-slot inline traversal stack; lives on the stack, never rents.</summary>
[InlineArray(128)]
public struct InlineBuffer128<T>
{
    private T _element0;
}

/// <summary>256-slot inline traversal stack; lives on the stack, never rents.</summary>
[InlineArray(256)]
public struct InlineBuffer256<T>
{
    private T _element0;
}
