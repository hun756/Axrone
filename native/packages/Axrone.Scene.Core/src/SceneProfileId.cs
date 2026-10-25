namespace Axrone.Scene;

/// <summary>
/// Stable identity of a scene profile. Profile ids are wire-facing names and
/// never depend on the registration order of component descriptors.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct SceneProfileId(SceneStringId Value) :
    IComparable<SceneProfileId>,
    IComparisonOperators<SceneProfileId, SceneProfileId, bool>
{
    /// <summary>Whether the id names a profile.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value.IsValid;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(SceneProfileId other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left id sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(SceneProfileId left, SceneProfileId right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether the left id sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(SceneProfileId left, SceneProfileId right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether the left id sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(SceneProfileId left, SceneProfileId right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether the left id sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(SceneProfileId left, SceneProfileId right) => left.CompareTo(right) >= 0;

    /// <summary>Unwraps the profile symbol.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator SceneStringId(SceneProfileId id) => id.Value;

    /// <summary>Wraps a profile symbol.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator SceneProfileId(SceneStringId value) => new(value);

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}

/// <summary>Built-in profile identities implemented by the scene core.</summary>
public static class SceneProfileIds
{
    /// <summary>Core 3D scene: intrinsic hierarchy plus 3D transforms.</summary>
    public static readonly SceneProfileId Core3D = new(SceneStringId.FromName("scene/core-3d"));

    /// <summary>Core 2D scene: intrinsic hierarchy plus 2D transforms.</summary>
    public static readonly SceneProfileId Core2D = new(SceneStringId.FromName("scene/core-2d"));
}
