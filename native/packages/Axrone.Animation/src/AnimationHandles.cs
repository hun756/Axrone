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

/// <summary>
/// Typed bone reference: an index that cannot be confused with counts, offsets,
/// or versions. Raw <c>-1</c> sentinels are banned from public APIs — absence is
/// <see cref="Invalid"/>. Implicitly convertible from <see cref="int"/> so existing
/// index call sites keep compiling; the explicit conversion goes back.
/// </summary>
public readonly record struct BoneHandle(int Index)
{
    /// <summary>Absent-bone sentinel.</summary>
    public static readonly BoneHandle Invalid = new(-1);

    /// <summary>Whether the handle names a real bone.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Index >= 0;
    }

    /// <summary>Widens a raw index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BoneHandle FromInt32(int index) => new(index);

    /// <summary>Narrows back to a raw index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToInt32() => Index;

    /// <summary>Widens a raw index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator BoneHandle(int index) => FromInt32(index);

    /// <summary>Narrows back to a raw index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator int(BoneHandle handle) => handle.ToInt32();
}
