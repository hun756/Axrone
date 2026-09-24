namespace Axrone.Tween;

/// <summary>
/// Generational tween handle identity: slot index fused with an allocation generation, so a
/// recycled slot never aliases a stale handle. The zero value is the invalid sentinel.
/// </summary>
public readonly record struct TweenId : IEquatable<TweenId>
{
    private readonly ulong _value;

    /// <summary>Creates an identity.</summary>
    public TweenId(uint index, uint generation) => _value = ((ulong)generation << 32) | index;

    /// <summary>Slot index (low word).</summary>
    public uint Index
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (uint)(_value & 0xFFFF_FFFF);
    }

    /// <summary>Allocation generation (high word).</summary>
    public uint Generation
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (uint)(_value >> 32);
    }

    /// <summary>Packed word.</summary>
    public ulong RawValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value;
    }

    /// <summary>Whether this identity names a real allocation.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _value != 0;
    }

    /// <summary>Invalid sentinel.</summary>
    public static TweenId Invalid => default;

    /// <inheritdoc/>
    public override string ToString() => $"TweenId({Index}:{Generation})";
}
