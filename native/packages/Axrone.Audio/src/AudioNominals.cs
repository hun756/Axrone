namespace Axrone.Audio;

using System.Globalization;

/// <summary>
/// Mixer bus identity: well-known buses occupy slots 1-8, zero is invalid.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct AudioBusId(uint Value) :
    IComparable<AudioBusId>,
    IComparisonOperators<AudioBusId, AudioBusId, bool>
{
    /// <summary>No bus.</summary>
    public static readonly AudioBusId Invalid = new(0U);

    /// <summary>Root bus.</summary>
    public static readonly AudioBusId Master = new(1U);

    /// <summary>Music bus.</summary>
    public static readonly AudioBusId Music = new(2U);

    /// <summary>Sound-effects bus.</summary>
    public static readonly AudioBusId Sfx = new(3U);

    /// <summary>Voice bus.</summary>
    public static readonly AudioBusId Voice = new(4U);

    /// <summary>Ambient bus.</summary>
    public static readonly AudioBusId Ambient = new(5U);

    /// <summary>Soundtrack bus.</summary>
    public static readonly AudioBusId Soundtrack = new(6U);

    /// <summary>UI bus.</summary>
    public static readonly AudioBusId Ui = new(7U);

    /// <summary>World bus.</summary>
    public static readonly AudioBusId World = new(8U);

    /// <summary>Whether the value names a live bus.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value != 0U;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(AudioBusId other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left identity sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(AudioBusId left, AudioBusId right) => left.Value > right.Value;

    /// <summary>Determines whether the left identity sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(AudioBusId left, AudioBusId right) => left.Value >= right.Value;

    /// <summary>Determines whether the left identity sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(AudioBusId left, AudioBusId right) => left.Value < right.Value;

    /// <summary>Determines whether the left identity sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(AudioBusId left, AudioBusId right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Decoded clip identity: zero is invalid.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct AudioClipId(uint Value) :
    IComparable<AudioClipId>,
    IComparisonOperators<AudioClipId, AudioClipId, bool>
{
    /// <summary>No clip.</summary>
    public static readonly AudioClipId Invalid = new(0U);

    /// <summary>Whether the value names a live clip.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value != 0U;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(AudioClipId other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left identity sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(AudioClipId left, AudioClipId right) => left.Value > right.Value;

    /// <summary>Determines whether the left identity sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(AudioClipId left, AudioClipId right) => left.Value >= right.Value;

    /// <summary>Determines whether the left identity sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(AudioClipId left, AudioClipId right) => left.Value < right.Value;

    /// <summary>Determines whether the left identity sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(AudioClipId left, AudioClipId right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Listener identity: slot 1 is the primary listener, zero is invalid.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct AudioListenerId(uint Value) :
    IComparable<AudioListenerId>,
    IComparisonOperators<AudioListenerId, AudioListenerId, bool>
{
    /// <summary>No listener.</summary>
    public static readonly AudioListenerId Invalid = new(0U);

    /// <summary>Primary listener.</summary>
    public static readonly AudioListenerId Primary = new(1U);

    /// <summary>Whether the value names a live listener.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value != 0U;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(AudioListenerId other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left identity sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(AudioListenerId left, AudioListenerId right) => left.Value > right.Value;

    /// <summary>Determines whether the left identity sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(AudioListenerId left, AudioListenerId right) => left.Value >= right.Value;

    /// <summary>Determines whether the left identity sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(AudioListenerId left, AudioListenerId right) => left.Value < right.Value;

    /// <summary>Determines whether the left identity sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(AudioListenerId left, AudioListenerId right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Registered source identity: zero is invalid.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct AudioSourceId(uint Value) :
    IComparable<AudioSourceId>,
    IComparisonOperators<AudioSourceId, AudioSourceId, bool>
{
    /// <summary>No source.</summary>
    public static readonly AudioSourceId Invalid = new(0U);

    /// <summary>Whether the value names a live source.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value != 0U;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(AudioSourceId other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left identity sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(AudioSourceId left, AudioSourceId right) => left.Value > right.Value;

    /// <summary>Determines whether the left identity sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(AudioSourceId left, AudioSourceId right) => left.Value >= right.Value;

    /// <summary>Determines whether the left identity sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(AudioSourceId left, AudioSourceId right) => left.Value < right.Value;

    /// <summary>Determines whether the left identity sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(AudioSourceId left, AudioSourceId right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Mixer snapshot identity: zero is invalid.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct AudioSnapshotId(uint Value) :
    IComparable<AudioSnapshotId>,
    IComparisonOperators<AudioSnapshotId, AudioSnapshotId, bool>
{
    /// <summary>No snapshot.</summary>
    public static readonly AudioSnapshotId Invalid = new(0U);

    /// <summary>Whether the value names a live snapshot.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value != 0U;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(AudioSnapshotId other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left identity sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(AudioSnapshotId left, AudioSnapshotId right) => left.Value > right.Value;

    /// <summary>Determines whether the left identity sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(AudioSnapshotId left, AudioSnapshotId right) => left.Value >= right.Value;

    /// <summary>Determines whether the left identity sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(AudioSnapshotId left, AudioSnapshotId right) => left.Value < right.Value;

    /// <summary>Determines whether the left identity sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(AudioSnapshotId left, AudioSnapshotId right) => left.Value <= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
