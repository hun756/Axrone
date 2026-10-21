namespace Axrone.Numeric;

/// <summary>Allocation-free duck-typed span wrapper.</summary>
/// <typeparam name="T">Element type.</typeparam>
public readonly ref struct ColorSpan<T>
    where T : struct
{
    private readonly Span<T> _span;

    /// <summary>Wraps a span.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ColorSpan(Span<T> span) => _span = span;

    /// <summary>Element count.</summary>
    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => _span.Length;
    }

    /// <summary>Element accessor.</summary>
    public ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => ref _span[index];
    }

    /// <summary>Enumerates elements.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Enumerator GetEnumerator() => new(_span);

    /// <summary>Zero-allocation enumerator.</summary>
    public ref struct Enumerator
    {
        private readonly Span<T> _span;
        private int _index;

        /// <summary>Creates an enumerator.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal Enumerator(Span<T> span)
        {
            _span = span;
            _index = -1;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool MoveNext()
        {
            int next = _index + 1;
            if ((uint)next < (uint)_span.Length)
            {
                _index = next;
                return true;
            }

            return false;
        }

        /// <summary>Current element.</summary>
        public ref T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => ref _span[_index];
        }
    }
}

/// <summary>Read-only allocation-free duck-typed span wrapper.</summary>
/// <typeparam name="T">Element type.</typeparam>
public readonly ref struct ReadOnlyColorSpan<T>
    where T : struct
{
    private readonly ReadOnlySpan<T> _span;

    /// <summary>Wraps a span.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ReadOnlyColorSpan(ReadOnlySpan<T> span) => _span = span;

    /// <summary>Element count.</summary>
    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => _span.Length;
    }

    /// <summary>Element accessor.</summary>
    public ref readonly T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => ref _span[index];
    }

    /// <summary>Enumerates elements.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Enumerator GetEnumerator() => new(_span);

    /// <summary>Zero-allocation enumerator.</summary>
    public ref struct Enumerator
    {
        private readonly ReadOnlySpan<T> _span;
        private int _index;

        /// <summary>Creates an enumerator.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal Enumerator(ReadOnlySpan<T> span)
        {
            _span = span;
            _index = -1;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool MoveNext()
        {
            int next = _index + 1;
            if ((uint)next < (uint)_span.Length)
            {
                _index = next;
                return true;
            }

            return false;
        }

        /// <summary>Current element.</summary>
        public ref readonly T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => ref _span[_index];
        }
    }
}

