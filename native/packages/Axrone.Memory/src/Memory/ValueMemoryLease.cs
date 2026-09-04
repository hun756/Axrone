namespace Axrone.Memory;

public readonly struct ValueMemoryLease<T> : IDisposable, IEquatable<ValueMemoryLease<T>>
{
    private readonly IPooledBufferToken<T>? _token;
    private readonly uint _leaseId;
    private readonly int _offset;
    private readonly int _length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueMemoryLease(IPooledBufferToken<T> token, uint leaseId, int offset, int length)
    {
        _token = token;
        _leaseId = leaseId;
        _offset = offset;
        _length = length;
    }

    public Memory<T> Memory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ValidateState();
            return _token!.Memory.Slice(_offset, _length);
        }
    }

    public ReadOnlyMemory<T> ReadOnlyMemory
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ValidateState();
            return _token!.Memory.Slice(_offset, _length);
        }
    }

    public Span<T> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ValidateState();
            return _token!.Span.Slice(_offset, _length);
        }
    }

    public ReadOnlySpan<T> ReadOnlySpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ValidateState();
            return _token!.Span.Slice(_offset, _length);
        }
    }

    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _length;
    }

    public bool IsActive
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _token != null && _token.IsLeaseValid(_leaseId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueMemoryLease<T> Slice(int start, int length)
    {
        ValidateState();
        if ((uint)start > (uint)_length || (uint)length > (uint)(_length - start))
        {
            ThrowArgumentOutOfRange();
        }
        return new ValueMemoryLease<T>(_token!, _leaseId, _offset + start, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_token != null && _token.IsLeaseValid(_leaseId))
        {
            _token.Return(_leaseId);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateState()
    {
        if (_token == null || !_token.IsLeaseValid(_leaseId))
        {
            ThrowInvalidLeaseState();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ValueMemoryLease<T> other) =>
        ReferenceEquals(_token, other._token) && _leaseId == other._leaseId && _offset == other._offset && _length == other._length;

    public override bool Equals(object? obj) =>
        obj is ValueMemoryLease<T> other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(_token, _leaseId, _offset, _length);

    public static bool operator ==(ValueMemoryLease<T> left, ValueMemoryLease<T> right) =>
        left.Equals(right);

    public static bool operator !=(ValueMemoryLease<T> left, ValueMemoryLease<T> right) =>
        !left.Equals(right);

    [DoesNotReturn]
    private static void ThrowInvalidLeaseState() =>
        throw new ObjectDisposedException(nameof(ValueMemoryLease<T>), "Lease is invalid, already returned, or expired.");

    [DoesNotReturn]
    private static void ThrowArgumentOutOfRange() =>
        throw new ArgumentOutOfRangeException("start");
}
