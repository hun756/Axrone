namespace Axrone.Animation;

/// <summary>
/// Build-time resolved parameter address. Strings name parameters at authoring
/// time; handles address them at runtime — no hashing on the frame path.
/// Obtained from <see cref="ParameterStore.ResolveHandle"/>; invalid until resolved.
/// </summary>
public readonly record struct ParameterHandle(int Index)
{
    /// <summary>Unresolved sentinel.</summary>
    public static readonly ParameterHandle Invalid = new(-1);

    /// <summary>Whether the handle names a real slot.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Index >= 0;
    }
}

/// <summary>
/// Build-time resolved curve slot. Curve ids name channels at authoring time;
/// handles address them at sampling time — no dictionary lookup per channel per frame.
/// Obtained from <see cref="CurveStore.ResolveHandle"/>; invalid until resolved.
/// </summary>
public readonly record struct CurveHandle(int Slot)
{
    /// <summary>Unresolved sentinel.</summary>
    public static readonly CurveHandle Invalid = new(-1);

    /// <summary>Whether the handle names a real slot.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Slot >= 0;
    }
}
