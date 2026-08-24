using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Enterprise.Patterns.Result;

/// <summary>Allocation-optimized, immutable, read-only collection of <see cref="Error"/> values. Uses optimized storage: a single field for 0 or 1 error, an array for 2+.</summary>
[DebuggerDisplay("Count = {Count}")]
[DebuggerTypeProxy(typeof(ErrorCollectionDebugView))]
[StructLayout(LayoutKind.Auto)]
public readonly struct ErrorCollection : IReadOnlyList<Error>, IEquatable<ErrorCollection>
{
    private static readonly Error[] EmptyArray = [];

    /// <summary>Empty <see cref="ErrorCollection"/> with zero errors.</summary>
    public static readonly ErrorCollection Empty = new(EmptyArray);

    private readonly Error _single;
    private readonly Error[]? _multiple;
    private readonly int _count;

    /// <summary>Creates a collection containing a single error.</summary>
    /// <param name="error">The error to store.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ErrorCollection(Error error)
    {
        _single = error;
        _multiple = null;
        _count = 1;
    }

    /// <summary>Creates a collection from an error array.</summary>
    /// <param name="errors">Source array, or <see langword="null"/> for an empty collection.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ErrorCollection(Error[]? errors)
    {
        if (errors is null || errors.Length == 0)
        {
            _single = default;
            _multiple = EmptyArray;
            _count = 0;
            return;
        }

        if (errors.Length == 1)
        {
            _single = errors[0];
            _multiple = null;
            _count = 1;
            return;
        }

        _single = default;
        _multiple = errors;
        _count = errors.Length;
    }

    /// <summary>Creates a collection from an error list.</summary>
    /// <param name="errors">Source list, or <see langword="null"/> for an empty collection.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ErrorCollection(List<Error>? errors)
    {
        if (errors is null || errors.Count == 0)
        {
            _single = default;
            _multiple = EmptyArray;
            _count = 0;
            return;
        }

        if (errors.Count == 1)
        {
            _single = errors[0];
            _multiple = null;
            _count = 1;
            return;
        }

        _single = default;
        _multiple = errors.ToArray();
        _count = _multiple.Length;
    }

    /// <summary>Gets the number of errors in the collection.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => _count;
    }

    /// <summary>Gets the first error, or <see cref="Error.None"/> if the collection is empty.</summary>
    public Error TopError
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => _count switch
        {
            0 => Error.None,
            1 => _single,
            _ => _multiple![0]
        };
    }

    /// <summary>Gets the error at the specified index.</summary>
    /// <param name="index">Zero-based index.</param>
    /// <returns>The <see cref="Error"/> at <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public Error this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            if ((uint)index >= (uint)_count)
            {
                ThrowIndexOutOfRangeException();
            }

            return _count == 1 ? _single : _multiple![index];
        }
    }

    /// <summary>Returns a struct enumerator for allocation-free iteration.</summary>
    /// <returns>An <see cref="Enumerator"/> over the collection.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Enumerator GetEnumerator() => new(in this);

    IEnumerator<Error> IEnumerable<Error>.GetEnumerator()
    {
        return _count switch
        {
            0 => ((IEnumerable<Error>)EmptyArray).GetEnumerator(),
            1 => ((IEnumerable<Error>)new[] { _single }).GetEnumerator(),
            _ => ((IEnumerable<Error>)_multiple!).GetEnumerator()
        };
    }

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<Error>)this).GetEnumerator();

    /// <summary>Copies the errors into a new array, or returns a shared empty array.</summary>
    /// <returns>An array containing all errors in order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Error[] ToArray()
    {
        return _count switch
        {
            0 => EmptyArray,
            1 => [_single],
            _ => (Error[])_multiple!.Clone()
        };
    }

    /// <summary>Returns a <see cref="ReadOnlySpan{T}"/> view over the errors.</summary>
    /// <returns>A read-only span of all errors in order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ReadOnlySpan<Error> AsSpan()
    {
        return _count switch
        {
            0 => ReadOnlySpan<Error>.Empty,
            1 => ToArray(),
            _ => _multiple.AsSpan(0, _count)
        };
    }

    /// <summary>Implicitly wraps a single <see cref="Error"/> into an <see cref="ErrorCollection"/>.</summary>
    /// <param name="error">The error to wrap.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator ErrorCollection(Error error) => new(error);

    /// <summary>Implicitly converts an <see cref="Error"/> array into an <see cref="ErrorCollection"/>.</summary>
    /// <param name="errors">The source array.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator ErrorCollection(Error[]? errors) => new(errors);

    /// <summary>Implicitly converts a <see cref="List{T}"/> of errors into an <see cref="ErrorCollection"/>.</summary>
    /// <param name="errors">The source list.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator ErrorCollection(List<Error>? errors) => new(errors);

    /// <summary>Determines whether this collection contains the same errors in the same order as <paramref name="other"/>.</summary>
    /// <param name="other">The other collection to compare.</param>
    /// <returns><see langword="true"/> if the collections are element-wise equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Equals(ErrorCollection other)
    {
        if (_count != other._count) return false;
        if (_count == 0) return true;
        if (_count == 1) return _single.Equals(other._single);

        ReadOnlySpan<Error> left = AsSpan();
        ReadOnlySpan<Error> right = other.AsSpan();
        for (int i = 0; i < left.Length; i++)
        {
            if (!left[i].Equals(right[i])) return false;
        }

        return true;
    }

    /// <summary>Determines whether this collection equals the specified object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="obj"/> is an <see cref="ErrorCollection"/> with the same value.</returns>
    public override bool Equals(object? obj) => obj is ErrorCollection other && Equals(other);

    /// <summary>Returns a hash code derived from all errors in the collection.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        if (_count == 0) return 0;
        if (_count == 1) return _single.GetHashCode();

        HashCode hash = default;
        ReadOnlySpan<Error> span = AsSpan();
        for (int i = 0; i < span.Length; i++)
        {
            hash.Add(span[i]);
        }
        return hash.ToHashCode();
    }

    /// <summary>Equality operator.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> if both collections are element-wise equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator ==(ErrorCollection left, ErrorCollection right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> if the collections differ.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator !=(ErrorCollection left, ErrorCollection right) => !left.Equals(right);

    [DoesNotReturn]
    private static void ThrowIndexOutOfRangeException() =>
        throw new ArgumentOutOfRangeException("index", "Index was out of range.");

    /// <summary>Struct enumerator for <see cref="ErrorCollection"/>, avoiding box allocation.</summary>
    public struct Enumerator : IEnumerator<Error>
    {
        private readonly ErrorCollection _collection;
        private int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal Enumerator(in ErrorCollection collection)
        {
            _collection = collection;
            _index = -1;
        }

        /// <summary>Gets the error at the current position.</summary>
        public readonly Error Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => _collection[_index];
        }

        readonly object? IEnumerator.Current => Current;

        /// <summary>Advances the enumerator to the next error.</summary>
        /// <returns><see langword="true"/> if the enumerator advanced; <see langword="false"/> if past the end.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool MoveNext()
        {
            int next = _index + 1;
            if (next < _collection.Count)
            {
                _index = next;
                return true;
            }
            return false;
        }

        /// <summary>Resets the enumerator to its initial position (before the first element).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void Reset() => _index = -1;

        /// <summary>No-op; there are no unmanaged resources to release.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public readonly void Dispose() { }
    }

    private sealed class ErrorCollectionDebugView
    {
        private readonly ErrorCollection _collection;

        public ErrorCollectionDebugView(ErrorCollection collection) => _collection = collection;

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public Error[] Items => _collection.ToArray();
    }
}
