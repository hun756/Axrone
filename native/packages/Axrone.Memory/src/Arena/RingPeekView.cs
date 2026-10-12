namespace Axrone.Memory.Arena;

/// <summary>
/// Non-destructive read view: the committed-but-unconsumed runs as two spans.
/// A snapshot — concurrent producers may extend past it, but it never shrinks
/// under the reader and never claims anything.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public readonly ref struct RingPeekView<T> where T : unmanaged
{
    /// <summary>First contiguous run.</summary>
    public ReadOnlySpan<T> First { get; }

    /// <summary>Wrapped remainder, possibly empty.</summary>
    public ReadOnlySpan<T> Second { get; }

    /// <summary>Creates a view.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RingPeekView(ReadOnlySpan<T> first, ReadOnlySpan<T> second)
    {
        First = first;
        Second = second;
    }

    /// <summary>Whether both runs are empty.</summary>
    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => First.IsEmpty && Second.IsEmpty;
    }

    /// <summary>Total readable elements.</summary>
    public int TotalCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => First.Length + Second.Length;
    }

    /// <summary>Enumerates the non-empty runs.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RingPeekEnumerator<T> GetEnumerator() => new(First, Second);
}

/// <summary>Enumerator over the non-empty runs of a peek view.</summary>
/// <typeparam name="T">The element type.</typeparam>
public ref struct RingPeekEnumerator<T> where T : unmanaged
{
    private readonly ReadOnlySpan<T> _first;
    private readonly ReadOnlySpan<T> _second;
    private int _state;

    /// <summary>Creates an enumerator; prefer the view.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RingPeekEnumerator(ReadOnlySpan<T> first, ReadOnlySpan<T> second)
    {
        _first = first;
        _second = second;
        _state = 0;
    }

    /// <summary>The run at the current position.</summary>
    public ReadOnlySpan<T> Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _state switch
        {
            1 => _first,
            2 => _second,
            _ => ReadOnlySpan<T>.Empty
        };
    }

    /// <summary>Advances to the next non-empty run, if any.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (_state == 0)
        {
            if (!_first.IsEmpty)
            {
                _state = 1;
                return true;
            }
            if (!_second.IsEmpty)
            {
                _state = 2;
                return true;
            }
            _state = 3;
            return false;
        }

        if (_state == 1)
        {
            if (!_second.IsEmpty)
            {
                _state = 2;
                return true;
            }
            _state = 3;
            return false;
        }

        _state = 3;
        return false;
    }
}
