namespace Axrone.Input;

using System.Globalization;

/// <summary>
/// Action-map context identity: FNV-1a hash of the context name.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct ContextId(ulong Value) :
    IComparable<ContextId>,
    IComparisonOperators<ContextId, ContextId, bool>
{
    /// <summary>No context.</summary>
    public static readonly ContextId None = new(0UL);

    /// <summary>Hashes a context name.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ContextId FromString(ReadOnlySpan<char> name)
    {
        if (name.IsEmpty)
        {
            return None;
        }

        ulong hash = 14695981039346656037UL;
        for (int i = 0; i < name.Length; i++)
        {
            hash ^= (byte)name[i];
            hash *= 1099511628211UL;
            hash ^= (byte)(name[i] >> 8);
            hash *= 1099511628211UL;
        }
        return new ContextId(hash != 0UL ? hash : 1UL);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(ContextId other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left identity sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(ContextId left, ContextId right) => left.Value > right.Value;

    /// <summary>Determines whether the left identity sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(ContextId left, ContextId right) => left.Value >= right.Value;

    /// <summary>Determines whether the left identity sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(ContextId left, ContextId right) => left.Value < right.Value;

    /// <summary>Determines whether the left identity sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(ContextId left, ContextId right) => left.Value <= right.Value;

    /// <summary>Hashes a context name.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ContextId(string name) => FromString(name.AsSpan());

    /// <inheritdoc/>
    public override string ToString() => $"ContextId(0x{Value:X16})";
}

/// <summary>
/// Input user slot. Slot 255 addresses any user.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct UserId(byte Value) :
    IComparable<UserId>,
    IComparisonOperators<UserId, UserId, bool>
{
    /// <summary>Any user.</summary>
    public static readonly UserId Any = new(255);

    /// <summary>System user.</summary>
    public static readonly UserId System = new(0);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(UserId other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left identity sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(UserId left, UserId right) => left.Value > right.Value;

    /// <summary>Determines whether the left identity sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(UserId left, UserId right) => left.Value >= right.Value;

    /// <summary>Determines whether the left identity sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(UserId left, UserId right) => left.Value < right.Value;

    /// <summary>Determines whether the left identity sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(UserId left, UserId right) => left.Value <= right.Value;

    /// <summary>Wraps a slot.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator UserId(byte value) => new(value);

    /// <summary>Unwraps the slot.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator byte(UserId user) => user.Value;

    /// <inheritdoc/>
    public override string ToString() => $"User({Value})";
}

/// <summary>
/// Context priority: higher wins on conflicts.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct PriorityLevel(int Value) :
    IComparable<PriorityLevel>,
    IComparisonOperators<PriorityLevel, PriorityLevel, bool>
{
    /// <summary>Default priority.</summary>
    public static readonly PriorityLevel Default = new(0);

    /// <summary>Lowest priority.</summary>
    public static readonly PriorityLevel Lowest = new(int.MinValue);

    /// <summary>Highest priority.</summary>
    public static readonly PriorityLevel Highest = new(int.MaxValue);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(PriorityLevel other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left level is higher.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(PriorityLevel left, PriorityLevel right) => left.Value > right.Value;

    /// <summary>Determines whether the left level is higher or equal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(PriorityLevel left, PriorityLevel right) => left.Value >= right.Value;

    /// <summary>Determines whether the left level is lower.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(PriorityLevel left, PriorityLevel right) => left.Value < right.Value;

    /// <summary>Determines whether the left level is lower or equal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(PriorityLevel left, PriorityLevel right) => left.Value <= right.Value;

    /// <summary>Wraps a level.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator PriorityLevel(int value) => new(value);

    /// <summary>Unwraps the level.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int(PriorityLevel level) => level.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Gamepad slot for user assignment.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct GamepadIndex(byte Value) :
    IComparable<GamepadIndex>,
    IComparisonOperators<GamepadIndex, GamepadIndex, bool>
{
    /// <summary>First gamepad.</summary>
    public static readonly GamepadIndex Primary = new(0);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(GamepadIndex other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left index sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(GamepadIndex left, GamepadIndex right) => left.Value > right.Value;

    /// <summary>Determines whether the left index sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(GamepadIndex left, GamepadIndex right) => left.Value >= right.Value;

    /// <summary>Determines whether the left index sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(GamepadIndex left, GamepadIndex right) => left.Value < right.Value;

    /// <summary>Determines whether the left index sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(GamepadIndex left, GamepadIndex right) => left.Value <= right.Value;

    /// <summary>Wraps a slot.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator GamepadIndex(byte value) => new(value);

    /// <summary>Unwraps the slot.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator byte(GamepadIndex index) => index.Value;
}

/// <summary>
/// Binding slot within an action.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SlotIndex(int Value) :
    IComparable<SlotIndex>,
    IComparisonOperators<SlotIndex, SlotIndex, bool>
{
    /// <summary>First slot.</summary>
    public static readonly SlotIndex Primary = new(0);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(SlotIndex other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left index sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(SlotIndex left, SlotIndex right) => left.Value > right.Value;

    /// <summary>Determines whether the left index sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(SlotIndex left, SlotIndex right) => left.Value >= right.Value;

    /// <summary>Determines whether the left index sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(SlotIndex left, SlotIndex right) => left.Value < right.Value;

    /// <summary>Determines whether the left index sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(SlotIndex left, SlotIndex right) => left.Value <= right.Value;

    /// <summary>Wraps a slot.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator SlotIndex(int value) => new(value);

    /// <summary>Unwraps the slot.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int(SlotIndex slot) => slot.Value;
}
