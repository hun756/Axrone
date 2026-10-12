namespace Axrone.Utility.NativeBuffer;

/// <summary>Element count with checked arithmetic.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct ElementCount(nuint Value) :
    IComparable<ElementCount>,
    IComparisonOperators<ElementCount, ElementCount, bool>,
    IAdditionOperators<ElementCount, ElementCount, ElementCount>,
    ISubtractionOperators<ElementCount, ElementCount, ElementCount>
{
    /// <summary>Creates a count.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ElementCount From(nuint value) => new(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator nuint(ElementCount count) => count.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator ElementCount(nuint value) => new(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(ElementCount other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(ElementCount left, ElementCount right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(ElementCount left, ElementCount right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(ElementCount left, ElementCount right) => left.Value <= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(ElementCount left, ElementCount right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ElementCount operator +(ElementCount left, ElementCount right) => new(left.Value + right.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ElementCount operator -(ElementCount left, ElementCount right) => new(left.Value - right.Value);

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Element index into a buffer.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct BufferIndex(nuint Value) :
    IComparable<BufferIndex>,
    IComparisonOperators<BufferIndex, BufferIndex, bool>
{
    /// <summary>Creates an index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BufferIndex From(nuint value) => new(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator nuint(BufferIndex index) => index.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator BufferIndex(nuint value) => new(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(BufferIndex other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(BufferIndex left, BufferIndex right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(BufferIndex left, BufferIndex right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(BufferIndex left, BufferIndex right) => left.Value <= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(BufferIndex left, BufferIndex right) => left.Value >= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Byte offset into a buffer.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct ByteOffset(nuint Value) :
    IComparable<ByteOffset>,
    IComparisonOperators<ByteOffset, ByteOffset, bool>
{
    /// <summary>Creates an offset.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ByteOffset From(nuint value) => new(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator nuint(ByteOffset offset) => offset.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator ByteOffset(nuint value) => new(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(ByteOffset other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(ByteOffset left, ByteOffset right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(ByteOffset left, ByteOffset right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(ByteOffset left, ByteOffset right) => left.Value <= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(ByteOffset left, ByteOffset right) => left.Value >= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Buffer alignment in bytes. Unlike <see cref="Axrone.Utility.Alignment.Alignment"/>,
/// construction floors at the pointer word: sub-word alignment is meaningless
/// for native buffers.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct MemoryAlignment :
    IComparable<MemoryAlignment>,
    IComparisonOperators<MemoryAlignment, MemoryAlignment, bool>
{
    /// <summary>Alignment in bytes.</summary>
    public nuint Value { get; }

    private MemoryAlignment(nuint value) => Value = value;

    /// <summary>Creates an alignment; must be a power of two at least pointer-sized.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MemoryAlignment From(nuint value)
    {
        if (value < (nuint)UIntPtr.Size || !BitOperations.IsPow2(value))
        {
            return ThrowHelper.ThrowInvalidAlignment<MemoryAlignment>(value);
        }
        return new MemoryAlignment(value);
    }

    /// <summary>64-byte cache line alignment.</summary>
    public static MemoryAlignment CacheLine => new(64);

    /// <summary>4096-byte page alignment.</summary>
    public static MemoryAlignment Page => new(4096);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator nuint(MemoryAlignment alignment) => alignment.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(MemoryAlignment other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(MemoryAlignment left, MemoryAlignment right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(MemoryAlignment left, MemoryAlignment right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(MemoryAlignment left, MemoryAlignment right) => left.Value <= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(MemoryAlignment left, MemoryAlignment right) => left.Value >= right.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
