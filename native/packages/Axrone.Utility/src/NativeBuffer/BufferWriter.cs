namespace Axrone.Utility.NativeBuffer;

using System.Buffers;

/// <summary>
/// Sequential writer over a fixed buffer: position-tracked <see cref="IBufferWriter{T}"/>
/// without growth. Advancing past the end throws.
/// </summary>
public sealed unsafe class NativeBufferWriter<T, TAllocator> : IBufferWriter<T>
    where T : unmanaged
    where TAllocator : struct, INativeAllocator
{
    private readonly NativeBuffer<T, TAllocator> _buffer;
    private nuint _position;

    /// <summary>Committed elements.</summary>
    public ElementCount Capacity => _buffer.Length;

    /// <summary>Elements written so far.</summary>
    public ElementCount WrittenCount => ElementCount.From(_position);

    /// <summary>Elements still writable.</summary>
    public ElementCount FreeCapacity => ElementCount.From(_buffer.Length.Value - _position);

    /// <summary>Creates a writer at position zero.</summary>
    public NativeBufferWriter(NativeBuffer<T, TAllocator> buffer)
    {
        _buffer = buffer;
        _position = 0;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        if (count < 0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(count), "Count cannot be negative.");
        }

        nuint advanceCount = (nuint)count;
        if (_position + advanceCount > _buffer.Length.Value)
        {
            ThrowHelper.ThrowRangeInvalid(ElementCount.From(_position), ElementCount.From(advanceCount), _buffer.Length);
        }

        _position += advanceCount;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<T> GetMemory(int sizeHint = 0)
    {
        CheckCapacity(sizeHint);
        return _buffer.Memory.Slice((int)_position);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetSpan(int sizeHint = 0)
    {
        CheckCapacity(sizeHint);
        nuint available = _buffer.Length.Value - _position;
        int clamped = (int)Math.Min(available, (nuint)int.MaxValue);
        return new Span<T>(_buffer.DangerousGetPointerUnchecked() + _position, clamped);
    }

    /// <summary>Resets the position to zero.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _position = 0;
    }

    /// <summary>Read-only view of the written prefix.</summary>
    public ReadOnlySpan<T> WrittenSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            int count = (int)Math.Min(_position, (nuint)int.MaxValue);
            return new ReadOnlySpan<T>(_buffer.DangerousGetPointerUnchecked(), count);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CheckCapacity(int sizeHint)
    {
        if (sizeHint < 0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(sizeHint), "Size hint cannot be negative.");
        }

        nuint required = sizeHint == 0 ? 1 : (nuint)sizeHint;
        if (_position + required > _buffer.Length.Value)
        {
            ThrowHelper.ThrowInvalidOperationException($"Requested capacity {ElementCount.From(_position + required).Value} exceeds buffer ceiling {_buffer.Length.Value}.");
        }
    }
}
