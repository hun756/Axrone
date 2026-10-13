namespace Axrone.Physics2D;

using Axrone.Utility.Identifiers;

/// <summary>Domain metadata tag for memory slots (0-based; all-bits-set is invalid).</summary>
public readonly struct SlotTag : IIdentifierTag<SlotTag>
{
    /// <inheritdoc/>
    public static string TagName => "Slot";

    /// <inheritdoc/>
    public static string Prefix => "s";

    /// <inheritdoc/>
    public static bool IsOneBased => false;
}

/// <summary>
/// Physical memory or dense array slot index wrapper. Prevents accidental
/// indexing by raw integer primitives. Backed by the centralized identifier
/// engine; the all-bits-set value is the invalid sentinel.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SlotIndex(StrongId<SlotTag, int> Handle) : IStrongId<SlotIndex, int>
{
    /// <summary>Unbound slot; never a live index.</summary>
    public static SlotIndex Invalid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(StrongId<SlotTag, int>.Invalid);
    }

    /// <summary>The backing value.</summary>
    public int RawValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Handle.RawValue;
    }

    /// <summary>Whether the index can name a live slot.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Handle.IsValid;
    }

    /// <summary>Wraps a raw value without validation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SlotIndex FromRaw(int value) => new(StrongId<SlotTag, int>.FromRaw(value));

    /// <summary>Wraps a 0-based index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SlotIndex FromIndex(int index) => new(StrongId<SlotTag, int>.FromIndex(index));

    /// <summary>Unwraps to a 0-based index, or -1 while invalid.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToIndex() => Handle.ToIndex();

    /// <summary>Unwraps the underlying index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int(SlotIndex index) => index.RawValue;

    /// <summary>Wraps a raw index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator SlotIndex(int index) => FromIndex(index);

    /// <summary>Wraps an engine handle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator StrongId<SlotTag, int>(SlotIndex id) => id.Handle;

    /// <summary>Unwraps an engine handle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator SlotIndex(StrongId<SlotTag, int> handle) => new(handle);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(SlotIndex other) => Handle.CompareTo(other.Handle);

    /// <summary>Determines whether the left index sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(SlotIndex left, SlotIndex right) => left.Handle < right.Handle;

    /// <summary>Determines whether the left index sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(SlotIndex left, SlotIndex right) => left.Handle > right.Handle;

    /// <summary>Determines whether the left index sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(SlotIndex left, SlotIndex right) => left.Handle <= right.Handle;

    /// <summary>Determines whether the left index sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(SlotIndex left, SlotIndex right) => left.Handle >= right.Handle;

    /// <inheritdoc/>
    public bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null)
    {
        if (!IsValid)
        {
            ReadOnlySpan<char> invalid = "Slot(Invalid)";
            if (destination.Length < invalid.Length)
            {
                charsWritten = 0;
                return false;
            }

            invalid.CopyTo(destination);
            charsWritten = invalid.Length;
            return true;
        }

        if (destination.Length < 7)
        {
            charsWritten = 0;
            return false;
        }

        "Slot(".AsSpan().CopyTo(destination);
        if (RawValue.TryFormat(destination.Slice(5), out int valueChars, format, provider) &&
            5 + valueChars < destination.Length)
        {
            destination[5 + valueChars] = ')';
            charsWritten = 6 + valueChars;
            return true;
        }

        charsWritten = 0;
        return false;
    }

    /// <inheritdoc/>
    public bool TryFormat(
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null)
    {
        if (utf8Destination.Length < 7)
        {
            bytesWritten = 0;
            return false;
        }

        utf8Destination[0] = (byte)'S';
        utf8Destination[1] = (byte)'l';
        utf8Destination[2] = (byte)'o';
        utf8Destination[3] = (byte)'t';
        utf8Destination[4] = (byte)'(';
        if (RawValue.TryFormat(utf8Destination.Slice(5), out int valueBytes, format, provider) &&
            5 + valueBytes < utf8Destination.Length)
        {
            utf8Destination[5 + valueBytes] = (byte)')';
            bytesWritten = 6 + valueBytes;
            return true;
        }

        bytesWritten = 0;
        return false;
    }

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        if (!IsValid)
        {
            return "Slot(Invalid)";
        }

        Span<char> buffer = stackalloc char[32];
        if (TryFormat(buffer, out int charsWritten, format, formatProvider))
        {
            return buffer.Slice(0, charsWritten).ToString();
        }

        return $"Slot({RawValue})";
    }

    /// <inheritdoc/>
    public override string ToString() => ToString(null, null);
}

/// <summary>
/// Strongly-typed undirected graph edge representing a manifold contact
/// between two rigid bodies.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ContactPair(BodyId BodyA, BodyId BodyB);

/// <summary>
/// Strongly-typed structural constraint edge joining two rigid bodies.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct JointPair(BodyId BodyA, BodyId BodyB);

/// <summary>
/// Immutable, register-passable aggregate of physics step temporal and
/// kinematic sleep criteria.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SimulationParameters(
    float DeltaTime,
    float TimeToSleep,
    float LinearTolerance,
    float AngularTolerance)
{
    /// <summary>Squared linear quiet threshold.</summary>
    public float LinearToleranceSquared
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => LinearTolerance * LinearTolerance;
    }

    /// <summary>Squared angular quiet threshold.</summary>
    public float AngularToleranceSquared
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => AngularTolerance * AngularTolerance;
    }
}

/// <summary>
/// Role contract segregating body store read/write capabilities for island
/// evaluation. Supports ref-struct implementations.
/// </summary>
internal interface IBodyStore
{
    /// <summary>Addressable slots.</summary>
    int SlotCapacity { get; }

    /// <summary>Whether a slot holds a live body.</summary>
    /// <param name="slot">The slot.</param>
    bool IsSlotLive(SlotIndex slot);

    /// <summary>Live body state by slot.</summary>
    /// <param name="slot">The slot.</param>
    ref BodyState GetSlot(SlotIndex slot);

    /// <summary>Puts a body to sleep or wakes it.</summary>
    /// <param name="id">The body.</param>
    /// <param name="awake">The target state.</param>
    void SetAwake(BodyId id, bool awake);
}
