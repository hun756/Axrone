namespace Axrone.Scene;

/// <summary>
/// Dense identifier of a registered managed behaviour type. Kept in a separate
/// namespace from data component ids so a behaviour can never be resolved as
/// an unmanaged column.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 2)]
public readonly record struct SceneBehaviourTypeId(ushort Value) :
    IComparable<SceneBehaviourTypeId>,
    IComparisonOperators<SceneBehaviourTypeId, SceneBehaviourTypeId, bool>
{
    /// <summary>Sentinel for an unregistered behaviour type.</summary>
    public static SceneBehaviourTypeId Invalid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(ushort.MaxValue);
    }

    /// <summary>Whether the id names a registered type.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value != ushort.MaxValue;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(SceneBehaviourTypeId other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left id sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(SceneBehaviourTypeId left, SceneBehaviourTypeId right) => left.Value < right.Value;

    /// <summary>Determines whether the left id sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(SceneBehaviourTypeId left, SceneBehaviourTypeId right) => left.Value <= right.Value;

    /// <summary>Determines whether the left id sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(SceneBehaviourTypeId left, SceneBehaviourTypeId right) => left.Value > right.Value;

    /// <summary>Determines whether the left id sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(SceneBehaviourTypeId left, SceneBehaviourTypeId right) => left.Value >= right.Value;

    /// <summary>Unwraps the raw id.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator ushort(SceneBehaviourTypeId id) => id.Value;

    /// <summary>Wraps a raw id.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator SceneBehaviourTypeId(ushort value) => new(value);

    /// <inheritdoc/>
    public override string ToString() => Value == ushort.MaxValue
        ? "SceneBehaviourTypeId[Invalid]"
        : string.Create(CultureInfo.InvariantCulture, $"SceneBehaviourTypeId[{Value}]");
}
