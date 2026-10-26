namespace Axrone.Scene;

/// <summary>
/// Dense identifier of a registered data component type. Ids are assigned by
/// the owning <c>SceneComponentRegistry</c>; serialized scenes use stable names,
/// never these process-local values.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 2)]
public readonly record struct SceneComponentTypeId(ushort Value) :
    IComparable<SceneComponentTypeId>,
    IComparisonOperators<SceneComponentTypeId, SceneComponentTypeId, bool>
{
    /// <summary>Sentinel for an unregistered component type.</summary>
    public static SceneComponentTypeId Invalid
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
    public int CompareTo(SceneComponentTypeId other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left id sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(SceneComponentTypeId left, SceneComponentTypeId right) => left.Value < right.Value;

    /// <summary>Determines whether the left id sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(SceneComponentTypeId left, SceneComponentTypeId right) => left.Value <= right.Value;

    /// <summary>Determines whether the left id sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(SceneComponentTypeId left, SceneComponentTypeId right) => left.Value > right.Value;

    /// <summary>Determines whether the left id sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(SceneComponentTypeId left, SceneComponentTypeId right) => left.Value >= right.Value;

    /// <summary>Unwraps the raw id.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator ushort(SceneComponentTypeId id) => id.Value;

    /// <summary>Wraps a raw id.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator SceneComponentTypeId(ushort value) => new(value);

    /// <inheritdoc/>
    public override string ToString() => Value == ushort.MaxValue
        ? "SceneComponentTypeId[Invalid]"
        : string.Create(CultureInfo.InvariantCulture, $"SceneComponentTypeId[{Value}]");
}
