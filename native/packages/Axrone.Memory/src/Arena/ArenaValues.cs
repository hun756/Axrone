namespace Axrone.Memory.Arena;

/// <summary>
/// Strongly-typed chunk identity within a unified arena. Zero is never issued.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct ChunkId(uint Value) :
    IComparable<ChunkId>,
    IComparisonOperators<ChunkId, ChunkId, bool>,
    IIncrementOperators<ChunkId>
{
    /// <summary>No chunk; never a live identity.</summary>
    public static ChunkId None => new(0);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(ChunkId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <(ChunkId left, ChunkId right) => left.Value < right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <=(ChunkId left, ChunkId right) => left.Value <= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >(ChunkId left, ChunkId right) => left.Value > right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >=(ChunkId left, ChunkId right) => left.Value >= right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ChunkId operator ++(ChunkId value) => new(unchecked(value.Value + 1));

    /// <inheritdoc/>
    public override string ToString() => $"ChunkId({Value})";
}

/// <summary>
/// Rewind target: the chunk plus the cursor offset a reset should restore.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct ArenaMarker(ChunkId ChunkId, ByteSize Offset)
{
    /// <summary>Rewind-to-start marker.</summary>
    public static ArenaMarker Zero => new(ChunkId.None, ByteSize.Zero);

    /// <summary>Whether this is the rewind-to-start marker.</summary>
    public bool IsZero => ChunkId == ChunkId.None;
}

/// <summary>
/// A single allocation: the pointer plus the granted length. The length is the
/// requested size, not the padded span, so callers never read padding.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly unsafe struct ArenaAllocationHandle : IEquatable<ArenaAllocationHandle>
{
    /// <summary>Aligned start of the grant.</summary>
    public readonly void* Pointer;

    /// <summary>Granted length in bytes.</summary>
    public readonly ByteSize Length;

    /// <summary>Creates a handle over <paramref name="pointer"/> and <paramref name="length"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ArenaAllocationHandle(void* pointer, ByteSize length)
    {
        Pointer = pointer;
        Length = length;
    }

    /// <summary>Exposes the grant as bytes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Span<byte> AsSpan()
    {
        if (Length.Value > (nuint)int.MaxValue)
        {
            ThrowHelper.ThrowCountOutOfRange();
        }
        return new(Pointer, (int)Length.Value);
    }

    /// <summary>Exposes the grant as typed elements.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Span<T> AsSpan<T>() where T : unmanaged
    {
        nuint elementSize = (nuint)sizeof(T);
        nuint count = Length.Value / elementSize;
        if (count > (nuint)int.MaxValue)
        {
            ThrowHelper.ThrowCountOutOfRange();
        }
        return new(Pointer, (int)count);
    }

    /// <summary>Exposes the grant as a single reference.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ref T AsRef<T>() where T : unmanaged
    {
        if (Length.Value < (nuint)sizeof(T))
        {
            ThrowHelper.ThrowBufferTooSmall();
        }
        return ref Unsafe.AsRef<T>(Pointer);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Equals(ArenaAllocationHandle other) => Pointer == other.Pointer && Length == other.Length;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ArenaAllocationHandle other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine((IntPtr)Pointer, Length.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator ==(ArenaAllocationHandle left, ArenaAllocationHandle right) => left.Equals(right);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator !=(ArenaAllocationHandle left, ArenaAllocationHandle right) => !left.Equals(right);
}

/// <summary>Machine-readable outcome of a fallible allocation.</summary>
public enum AllocationStatus : byte
{
    /// <summary>The grant succeeded.</summary>
    Success = 0,

    /// <summary>No chunk could satisfy the request.</summary>
    OutOfMemory = 1,

    /// <summary>The arena is not accepting allocations.</summary>
    ArenaInactive = 2,

    /// <summary>The alignment is not a non-zero power of two.</summary>
    InvalidAlignment = 3
}

/// <summary>Handle plus status: the throwing API unwraps it, the trying API returns it.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly struct AllocationResult : IEquatable<AllocationResult>
{
    /// <summary>The grant; default on failure.</summary>
    public readonly ArenaAllocationHandle Handle;

    /// <summary>What happened.</summary>
    public readonly AllocationStatus Status;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private AllocationResult(ArenaAllocationHandle handle, AllocationStatus status)
    {
        Handle = handle;
        Status = status;
    }

    /// <summary>Wraps a successful grant.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static AllocationResult Succeeded(ArenaAllocationHandle handle) => new(handle, AllocationStatus.Success);

    /// <summary>Wraps a failure; the handle is default.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static AllocationResult Failed(AllocationStatus status) => new(default, status);

    /// <summary>Whether the grant succeeded.</summary>
    public bool IsSuccess => Status == AllocationStatus.Success;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(AllocationResult other) => Handle.Equals(other.Handle) && Status == other.Status;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is AllocationResult other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Handle, Status);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator ==(AllocationResult left, AllocationResult right) => left.Equals(right);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator !=(AllocationResult left, AllocationResult right) => !left.Equals(right);
}

/// <summary>Snapshot of one chunk for inspectors and telemetry.</summary>
public readonly record struct ChunkInfo(
    ChunkId Id,
    ByteSize Capacity,
    ByteSize Allocated,
    nuint BufferAddress,
    nuint CommittedSize);
