namespace Axrone.Utility.NativeBuffer;

/// <summary>Why a lease could not be acquired.</summary>
public enum LeaseOutcome : byte
{
    /// <summary>The lease was acquired.</summary>
    Acquired = 0,

    /// <summary>The buffer is draining and refuses new leases.</summary>
    BufferDraining = 1,

    /// <summary>The buffer already drained.</summary>
    BufferDrained = 2,

    /// <summary>The buffer is disposed.</summary>
    BufferDisposed = 3,

    /// <summary>The buffer faulted.</summary>
    BufferFaulted = 4
}

/// <summary>
/// Lease result: either a lease or the reason there is none. Following the
/// house Result convention, <see cref="Value"/> throws on failure and
/// <see cref="TryGetLease"/> reports it without throwing.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly struct LeaseResult<T, TAllocator> : IEquatable<LeaseResult<T, TAllocator>>
    where T : unmanaged
    where TAllocator : struct, INativeAllocator
{
    private readonly NativeBufferLease<T, TAllocator> _lease;
    private readonly LeaseOutcome _outcome;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal LeaseResult(NativeBufferLease<T, TAllocator> lease, LeaseOutcome outcome)
    {
        _lease = lease;
        _outcome = outcome;
    }

    /// <summary>Wraps an acquired lease.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LeaseResult<T, TAllocator> Acquired(NativeBufferLease<T, TAllocator> lease) =>
        new(lease, LeaseOutcome.Acquired);

    /// <summary>Wraps a failure; the lease is default.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LeaseResult<T, TAllocator> Failed(LeaseOutcome outcome) =>
        new(default, outcome);

    /// <summary>Why the lease could not be acquired.</summary>
    public LeaseOutcome Outcome => _outcome;

    /// <summary>Whether the lease was acquired.</summary>
    public bool IsSuccess => _outcome == LeaseOutcome.Acquired;

    /// <summary>The lease; throws when acquisition failed.</summary>
    public NativeBufferLease<T, TAllocator> Value => _outcome switch
    {
        LeaseOutcome.Acquired => _lease,
        LeaseOutcome.BufferDraining => ThrowHelper.ThrowDrainingLeaseAccess<NativeBufferLease<T, TAllocator>>(),
        LeaseOutcome.BufferDrained => ThrowHelper.ThrowDrainedLeaseAccess<NativeBufferLease<T, TAllocator>>(),
        LeaseOutcome.BufferDisposed => ThrowHelper.ThrowDisposedLeaseAccess<NativeBufferLease<T, TAllocator>>(),
        LeaseOutcome.BufferFaulted => ThrowHelper.ThrowFaultedLeaseAccess<NativeBufferLease<T, TAllocator>>(),
        _ => ThrowHelper.ThrowInvalidState<NativeBufferLease<T, TAllocator>>((long)_outcome)
    };

    /// <summary>Tries to read the lease without throwing.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetLease(out NativeBufferLease<T, TAllocator> lease)
    {
        if (_outcome == LeaseOutcome.Acquired)
        {
            lease = _lease;
            return true;
        }
        lease = default;
        return false;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(LeaseResult<T, TAllocator> other) =>
        _outcome == other._outcome && _lease.Equals(other._lease);

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is LeaseResult<T, TAllocator> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine((byte)_outcome, _lease);

    /// <inheritdoc/>
    public static bool operator ==(LeaseResult<T, TAllocator> left, LeaseResult<T, TAllocator> right) =>
        left.Equals(right);

    /// <inheritdoc/>
    public static bool operator !=(LeaseResult<T, TAllocator> left, LeaseResult<T, TAllocator> right) =>
        !left.Equals(right);
}

/// <summary>
/// Reference-counted buffer handle. Its dispose releases the lease, so a
/// drain always waits for the handle to go out of scope.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly unsafe struct NativeBufferLease<T, TAllocator> : IDisposable, IEquatable<NativeBufferLease<T, TAllocator>>
    where T : unmanaged
    where TAllocator : struct, INativeAllocator
{
    private readonly NativeBuffer<T, TAllocator>? _buffer;
    private readonly uint _length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal NativeBufferLease(NativeBuffer<T, TAllocator> buffer, uint length)
    {
        _buffer = buffer;
        _length = length;
    }

    /// <summary>Whether the lease holds a live buffer.</summary>
    public bool IsActive => _buffer is not null;

    /// <summary>Leased elements, clamped to the buffer length.</summary>
    public uint Length => _length;

    /// <summary>Leased elements as a count.</summary>
    public ElementCount ElementLength => ElementCount.From(_length);

    /// <summary>Mutable view of the lease.</summary>
    public Span<T> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_buffer is null)
            {
                return Span<T>.Empty;
            }
            return new Span<T>(_buffer.DangerousGetPointerUnchecked(), (int)_length);
        }
    }

    /// <summary>Read-only view of the lease.</summary>
    public ReadOnlySpan<T> ReadOnlySpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_buffer is null)
            {
                return ReadOnlySpan<T>.Empty;
            }
            return new ReadOnlySpan<T>(_buffer.DangerousGetPointerUnchecked(), (int)_length);
        }
    }

    /// <summary>Base pointer of the lease, or null when inactive.</summary>
    public T* Pointer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _buffer is not null ? _buffer.DangerousGetPointerUnchecked() : null;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _buffer?.ReleaseLease();

    /// <summary>Enumerates the leased elements by value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NativeBufferEnumerator<T> GetEnumerator() => new(Pointer, _length);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(NativeBufferLease<T, TAllocator> other) =>
        ReferenceEquals(_buffer, other._buffer) && _length == other._length;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is NativeBufferLease<T, TAllocator> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(_buffer), _length);

    /// <inheritdoc/>
    public static bool operator ==(NativeBufferLease<T, TAllocator> left, NativeBufferLease<T, TAllocator> right) => left.Equals(right);

    /// <inheritdoc/>
    public static bool operator !=(NativeBufferLease<T, TAllocator> left, NativeBufferLease<T, TAllocator> right) => !left.Equals(right);
}

/// <summary>Cheap subview over a buffer.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly unsafe struct NativeBufferSlice<T, TAllocator> : IEquatable<NativeBufferSlice<T, TAllocator>>
    where T : unmanaged
    where TAllocator : struct, INativeAllocator
{
    private readonly NativeBuffer<T, TAllocator> _buffer;
    private readonly ElementCount _offset;
    private readonly ElementCount _length;

    /// <summary>Element offset from the buffer start.</summary>
    public ElementCount Offset => _offset;

    /// <summary>Elements in the slice.</summary>
    public ElementCount Length => _length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal NativeBufferSlice(NativeBuffer<T, TAllocator> buffer, ElementCount offset, ElementCount length)
    {
        _buffer = buffer;
        _offset = offset;
        _length = length;
    }

    /// <summary>Indexes into the slice.</summary>
    public ref T this[BufferIndex index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            if (index.Value >= _length.Value)
            {
                ThrowHelper.ThrowIndexOutOfRange(index.Value, _length.Value);
            }
            return ref _buffer[BufferIndex.From(_offset.Value + index.Value)];
        }
    }

    /// <summary>Mutable view of the slice.</summary>
    public Span<T> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_length.Value > (nuint)int.MaxValue)
            {
                ThrowHelper.ThrowSpanLengthExceeded(_length.Value);
            }
            return new Span<T>(_buffer.DangerousGetPointerUnchecked() + _offset.Value, (int)_length.Value);
        }
    }

    /// <summary>Read-only view of the slice.</summary>
    public ReadOnlySpan<T> ReadOnlySpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_length.Value > (nuint)int.MaxValue)
            {
                ThrowHelper.ThrowSpanLengthExceeded(_length.Value);
            }
            return new ReadOnlySpan<T>(_buffer.DangerousGetPointerUnchecked() + _offset.Value, (int)_length.Value);
        }
    }

    /// <summary>Sub-slices again.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NativeBufferSlice<T, TAllocator> Slice(ElementCount subOffset, ElementCount subLength)
    {
        if (subOffset.Value + subLength.Value > _length.Value)
        {
            ThrowHelper.ThrowRangeInvalid(subOffset.Value, subLength.Value, _length.Value);
        }
        return new NativeBufferSlice<T, TAllocator>(_buffer, ElementCount.From(_offset.Value + subOffset.Value), subLength);
    }

    /// <summary>Enumerates the slice by value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NativeBufferEnumerator<T> GetEnumerator() =>
        new(_buffer.DangerousGetPointerUnchecked() + _offset.Value, _length.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(NativeBufferSlice<T, TAllocator> other) =>
        ReferenceEquals(_buffer, other._buffer) && _offset == other._offset && _length == other.Length;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is NativeBufferSlice<T, TAllocator> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(_buffer), _offset, _length);

    /// <inheritdoc/>
    public static bool operator ==(NativeBufferSlice<T, TAllocator> left, NativeBufferSlice<T, TAllocator> right) => left.Equals(right);

    /// <inheritdoc/>
    public static bool operator !=(NativeBufferSlice<T, TAllocator> left, NativeBufferSlice<T, TAllocator> right) => !left.Equals(right);
}
