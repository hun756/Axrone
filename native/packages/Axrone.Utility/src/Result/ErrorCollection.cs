using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Enterprise.Patterns.Result;

[DebuggerDisplay("Count = {Count}")]
[DebuggerTypeProxy(typeof(ErrorCollectionDebugView))]
[StructLayout(LayoutKind.Auto)]
public readonly struct ErrorCollection : IReadOnlyList<Error>, IEquatable<ErrorCollection>
{
    private static readonly Error[] EmptyArray = [];
    public static readonly ErrorCollection Empty = new(EmptyArray);

    private readonly Error _single;
    private readonly Error[]? _multiple;
    private readonly int _count;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ErrorCollection(Error error)
    {
        _single = error;
        _multiple = null;
        _count = 1;
    }

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

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => _count;
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Enumerator GetEnumerator() => new(in this);

    IEnumerator<Error> IEnumerable<Error>.GetEnumerator()
    {
        return _count switch
        {
            0 => ((IEnumerable<Error>)EmptyArray).GetEnumerator(),
            1 => GetSingleEnumerator(_single),
            _ => ((IEnumerable<Error>)_multiple!).GetEnumerator()
        };
    }

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<Error>)this).GetEnumerator();

    private static IEnumerator<Error> GetSingleEnumerator(Error error)
    {
        yield return error;
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ReadOnlySpan<Error> AsSpan()
    {
        return _count switch
        {
            0 => ReadOnlySpan<Error>.Empty,
            1 => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _single), 1),
            _ => _multiple.AsSpan(0, _count)
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator ErrorCollection(Error error) => new(error);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator ErrorCollection(Error[]? errors) => new(errors);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator ErrorCollection(List<Error>? errors) => new(errors);

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

    public override bool Equals(object? obj) => obj is ErrorCollection other && Equals(other);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator ==(ErrorCollection left, ErrorCollection right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator !=(ErrorCollection left, ErrorCollection right) => !left.Equals(right);

    [DoesNotReturn]
    private static void ThrowIndexOutOfRangeException() =>
        throw new ArgumentOutOfRangeException("index", "Index was out of range.");

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

        public readonly Error Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => _collection[_index];
        }

        readonly object? IEnumerator.Current => Current;

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

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void Reset() => _index = -1;

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
