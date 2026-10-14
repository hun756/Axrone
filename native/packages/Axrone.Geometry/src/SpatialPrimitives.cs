using Axrone.Utility.Identifiers;

namespace Axrone.Geometry;

using System.Globalization;

/// <summary>Relationship between a query volume and a spatial item.</summary>
public enum ContainmentType : byte
{
    /// <summary>No point of the item lies inside the volume.</summary>
    Disjoint = 0,

    /// <summary>The volume lies wholly inside the item.</summary>
    Contains = 1,

    /// <summary>The shapes share at least one point.</summary>
    Intersects = 2,
}

/// <summary>Coordinate axis selector, ordered to match spatial-tree child slots.</summary>
public enum Axis : byte
{
    /// <summary>The X axis.</summary>
    X = 0,

    /// <summary>The Y axis.</summary>
    Y = 1,

    /// <summary>The Z axis.</summary>
    Z = 2,
}

/// <summary>Outcome of a ray or segment query against a single candidate.</summary>
public enum IntersectionKind : byte
{
    /// <summary>The ray misses the candidate.</summary>
    None = 0,

    /// <summary>The ray hits the candidate.</summary>
    Hit = 1,
}

/// <summary>Outcome of a swept-volume query against a single candidate.</summary>
public enum SweepKind : byte
{
    /// <summary>The swept volume misses the candidate.</summary>
    Miss = 0,

    /// <summary>The swept volume hits the candidate.</summary>
    Hit = 1,
}

/// <summary>Domain metadata tag for spatial items (1-based; zero is invalid).</summary>
public readonly struct ItemTag : IIdentifierTag<ItemTag>
{
    /// <inheritdoc/>
    public static string TagName => "Item";

    /// <inheritdoc/>
    public static string Prefix => "i";

    /// <inheritdoc/>
    public static bool IsOneBased => true;
}

/// <summary>Domain metadata tag for tree node slots (0-based; all-bits-set is null).</summary>
public readonly struct NodeTag : IIdentifierTag<NodeTag>
{
    /// <inheritdoc/>
    public static string TagName => "Node";

    /// <inheritdoc/>
    public static string Prefix => "n";

    /// <inheritdoc/>
    public static bool IsOneBased => false;
}

/// <summary>
/// 4-byte identity of a spatial item registered in a tree. The zero value is the
/// invalid sentinel, so a default-initialized slot can never name a live item.
/// Backed by the centralized identifier engine.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SpatialItemId(StrongId<ItemTag, uint> Handle) : IStrongId<SpatialItemId, uint>
{
    /// <summary>Invalid sentinel; never a registered item.</summary>
    public static SpatialItemId Invalid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(StrongId<ItemTag, uint>.Invalid);
    }

    /// <summary>The backing value.</summary>
    public uint RawValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Handle.RawValue;
    }

    /// <summary>Whether this identity names a registered item.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Handle.IsValid;
    }

    /// <summary>Wraps a raw value without validation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SpatialItemId FromRaw(uint value) => new(StrongId<ItemTag, uint>.FromRaw(value));

    /// <summary>Converts a 0-based slot index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SpatialItemId FromIndex(int index) => new(StrongId<ItemTag, uint>.FromIndex(index));

    /// <summary>Converts back to a 0-based slot index, or -1 while invalid.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToIndex() => Handle.ToIndex();

    /// <summary>Wraps a raw identifier.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator SpatialItemId(uint value) => FromRaw(value);

    /// <summary>Wraps an engine handle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator StrongId<ItemTag, uint>(SpatialItemId id) => id.Handle;

    /// <summary>Unwraps an engine handle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator SpatialItemId(StrongId<ItemTag, uint> handle) => new(handle);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(SpatialItemId other) => Handle.CompareTo(other.Handle);

    /// <summary>Determines whether the left identity sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(SpatialItemId left, SpatialItemId right) => left.Handle < right.Handle;

    /// <summary>Determines whether the left identity sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(SpatialItemId left, SpatialItemId right) => left.Handle > right.Handle;

    /// <summary>Determines whether the left identity sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(SpatialItemId left, SpatialItemId right) => left.Handle <= right.Handle;

    /// <summary>Determines whether the left identity sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(SpatialItemId left, SpatialItemId right) => left.Handle >= right.Handle;

    /// <inheritdoc/>
    public bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null)
    {
        if (!IsValid)
        {
            ReadOnlySpan<char> invalid = "Item(Invalid)";
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

        "Item(".AsSpan().CopyTo(destination);
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

        utf8Destination[0] = (byte)'I';
        utf8Destination[1] = (byte)'t';
        utf8Destination[2] = (byte)'e';
        utf8Destination[3] = (byte)'m';
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
            return "Item(Invalid)";
        }

        Span<char> buffer = stackalloc char[32];
        if (TryFormat(buffer, out int charsWritten, format, formatProvider))
        {
            return buffer.Slice(0, charsWritten).ToString();
        }

        return $"Item({RawValue})";
    }

    /// <inheritdoc/>
    public override string ToString() => ToString(null, null);
}

/// <summary>
/// 4-byte slot index into a spatial-tree node pool. Negative values are null slots and
/// never address a live node. Backed by the centralized identifier engine.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct NodeIndex(StrongId<NodeTag, int> Handle) : IStrongId<NodeIndex, int>
{
    /// <summary>Null sentinel; never a live node slot.</summary>
    public static NodeIndex Null
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(StrongId<NodeTag, int>.Invalid);
    }

    /// <summary>Invalid sentinel; identical to <see cref="Null"/>.</summary>
    public static NodeIndex Invalid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Null;
    }

    /// <summary>Whether this index addresses no node.</summary>
    public bool IsNull
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RawValue < 0;
    }

    /// <summary>Whether this index addresses a live node slot.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => RawValue >= 0;
    }

    /// <summary>The backing value.</summary>
    public int RawValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Handle.RawValue;
    }

    /// <summary>Wraps a raw value without validation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NodeIndex FromRaw(int value) => new(StrongId<NodeTag, int>.FromRaw(value));

    /// <summary>Wraps a 0-based slot.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NodeIndex FromIndex(int index) => new(StrongId<NodeTag, int>.FromIndex(index));

    /// <summary>Unwraps to a 0-based slot, or -1 while null.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToIndex() => Handle.ToIndex();

    /// <summary>Wraps a raw slot index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator NodeIndex(int value) => FromRaw(value);

    /// <summary>Wraps an engine handle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator StrongId<NodeTag, int>(NodeIndex id) => id.Handle;

    /// <summary>Unwraps an engine handle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator NodeIndex(StrongId<NodeTag, int> handle) => new(handle);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(NodeIndex other) => Handle.CompareTo(other.Handle);

    /// <summary>Determines whether the left index sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(NodeIndex left, NodeIndex right) => left.Handle < right.Handle;

    /// <summary>Determines whether the left index sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(NodeIndex left, NodeIndex right) => left.Handle > right.Handle;

    /// <summary>Determines whether the left index sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(NodeIndex left, NodeIndex right) => left.Handle <= right.Handle;

    /// <summary>Determines whether the left index sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(NodeIndex left, NodeIndex right) => left.Handle >= right.Handle;

    /// <inheritdoc/>
    public bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null)
    {
        if (!IsValid)
        {
            ReadOnlySpan<char> invalid = "Node(Null)";
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

        "Node(".AsSpan().CopyTo(destination);
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

        utf8Destination[0] = (byte)'N';
        utf8Destination[1] = (byte)'o';
        utf8Destination[2] = (byte)'d';
        utf8Destination[3] = (byte)'e';
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
            return "Node(Null)";
        }

        Span<char> buffer = stackalloc char[32];
        if (TryFormat(buffer, out int charsWritten, format, formatProvider))
        {
            return buffer.Slice(0, charsWritten).ToString();
        }

        return $"Node({RawValue})";
    }

    /// <inheritdoc/>
    public override string ToString() => ToString(null, null);
}

/// <summary>
/// Node capacity of a spatial tree, floored at <see cref="MinimumCapacity"/> so the
/// root fan-out and split thresholds always have room to work. The guard lives in the
/// constructor, so an out-of-range capacity cannot enter the engine.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct TreeCapacity : IComparable<TreeCapacity>
{
    /// <summary>Smallest capacity a spatial tree accepts.</summary>
    public const int MinimumCapacity = 16;

    /// <summary>Creates a capacity, rejecting anything below <see cref="MinimumCapacity"/>.</summary>
    public TreeCapacity(int value)
    {
        if (value < MinimumCapacity)
        {
            ThrowHelper.ThrowInvalidInitialCapacity();
        }

        Value = value;
    }

    /// <summary>Node capacity.</summary>
    public int Value { get; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(TreeCapacity other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"TreeCapacity({Value})");

    /// <summary>Unwraps the underlying capacity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int(TreeCapacity capacity) => capacity.Value;

    /// <summary>Wraps a raw capacity, applying the minimum-capacity guard.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator TreeCapacity(int value) => new(value);

    /// <summary>Determines whether the left capacity is below the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(TreeCapacity left, TreeCapacity right) => left.Value < right.Value;

    /// <summary>Determines whether the left capacity exceeds the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(TreeCapacity left, TreeCapacity right) => left.Value > right.Value;

    /// <summary>Determines whether the left capacity is at or below the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(TreeCapacity left, TreeCapacity right) => left.Value <= right.Value;

    /// <summary>Determines whether the left capacity is at or above the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(TreeCapacity left, TreeCapacity right) => left.Value >= right.Value;
}

/// <summary>
/// Squared distance between two points. Keeping the squared form avoids a square root
/// on every reject, which is the dominant cost of a traversal.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct DistanceSquared(float Value) : IComparable<DistanceSquared>
{
    /// <summary>An unreachable distance; used as a "no candidate" result.</summary>
    public static DistanceSquared PositiveInfinity => new(float.PositiveInfinity);

    /// <summary>Coincident points.</summary>
    public static DistanceSquared Zero => new(0F);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(DistanceSquared other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"DistanceSquared({Value})");

    /// <summary>Unwraps the underlying squared distance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator float(DistanceSquared distance) => distance.Value;

    /// <summary>Wraps a raw squared distance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator DistanceSquared(float value) => new(value);

    /// <summary>Determines whether the left distance is below the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(DistanceSquared left, DistanceSquared right) => left.Value < right.Value;

    /// <summary>Determines whether the left distance exceeds the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(DistanceSquared left, DistanceSquared right) => left.Value > right.Value;

    /// <summary>Determines whether the left distance is at or below the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(DistanceSquared left, DistanceSquared right) => left.Value <= right.Value;

    /// <summary>Determines whether the left distance is at or above the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(DistanceSquared left, DistanceSquared right) => left.Value >= right.Value;
}