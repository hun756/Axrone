namespace Axrone.Reactive;

/// <summary>
/// Lazy stream operators. Composition allocates one node; subscribing allocates one subscription;
/// notifications never allocate. No schedulers, no timers — the engine drives threads.
/// </summary>
public static class ObservableOperators
{
    /// <summary>Forwards only values satisfying the predicate.</summary>
    public static IObservable<T> Where<T>(this IObservable<T> source, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return new WhereObservable<T>(source, predicate);
    }

    /// <summary>Projects every value.</summary>
    public static IObservable<TResult> Select<T, TResult>(this IObservable<T> source, Func<T, TResult> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return new SelectObservable<T, TResult>(source, selector);
    }

    /// <summary>Merges two streams; completes when both complete, faults on the first error.</summary>
    public static IObservable<T> Merge<T>(this IObservable<T> first, IObservable<T> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return new MergeObservable<T>(first, second);
    }

    /// <summary>Suppresses consecutive duplicates; state is per subscription.</summary>
    public static IObservable<T> DistinctUntilChanged<T>(this IObservable<T> source, IEqualityComparer<T>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new DistinctObservable<T>(source, comparer ?? EqualityComparer<T>.Default);
    }

    private sealed class WhereObservable<T> : IObservable<T>
    {
        private readonly IObservable<T> _source;
        private readonly Func<T, bool> _predicate;

        public WhereObservable(IObservable<T> source, Func<T, bool> predicate)
        {
            _source = source;
            _predicate = predicate;
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            var subscription = new WhereSubscription(this, observer);
            subscription.Attach();
            return subscription;
        }

        private sealed class WhereSubscription : IObserver<T>, IDisposable
        {
            private readonly WhereObservable<T> _parent;
            private readonly IObserver<T> _downstream;
            private IDisposable? _upstream;
            private int _disposed;

            public WhereSubscription(WhereObservable<T> parent, IObserver<T> downstream)
            {
                _parent = parent;
                _downstream = downstream;
            }

            public void Attach() => _upstream = _parent._source.Subscribe(this);

            public void OnNext(T value)
            {
                if (_parent._predicate(value))
                {
                    _downstream.OnNext(value);
                }
            }

            public void OnError(Exception error) => _downstream.OnError(error);

            public void OnCompleted() => _downstream.OnCompleted();

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    _upstream?.Dispose();
                }
            }
        }
    }

    private sealed class SelectObservable<T, TResult> : IObservable<TResult>
    {
        private readonly IObservable<T> _source;
        private readonly Func<T, TResult> _selector;

        public SelectObservable(IObservable<T> source, Func<T, TResult> selector)
        {
            _source = source;
            _selector = selector;
        }

        public IDisposable Subscribe(IObserver<TResult> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            var subscription = new SelectSubscription(this, observer);
            subscription.Attach();
            return subscription;
        }

        private sealed class SelectSubscription : IObserver<T>, IDisposable
        {
            private readonly SelectObservable<T, TResult> _parent;
            private readonly IObserver<TResult> _downstream;
            private IDisposable? _upstream;
            private int _disposed;

            public SelectSubscription(SelectObservable<T, TResult> parent, IObserver<TResult> downstream)
            {
                _parent = parent;
                _downstream = downstream;
            }

            public void Attach() => _upstream = _parent._source.Subscribe(this);

            public void OnNext(T value) => _downstream.OnNext(_parent._selector(value));

            public void OnError(Exception error) => _downstream.OnError(error);

            public void OnCompleted() => _downstream.OnCompleted();

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    _upstream?.Dispose();
                }
            }
        }
    }

    private sealed class DistinctObservable<T> : IObservable<T>
    {
        private readonly IObservable<T> _source;
        private readonly IEqualityComparer<T> _comparer;

        public DistinctObservable(IObservable<T> source, IEqualityComparer<T> comparer)
        {
            _source = source;
            _comparer = comparer;
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            var subscription = new DistinctSubscription(this, observer);
            subscription.Attach();
            return subscription;
        }

        private sealed class DistinctSubscription : IObserver<T>, IDisposable
        {
            private readonly DistinctObservable<T> _parent;
            private readonly IObserver<T> _downstream;
            private IDisposable? _upstream;
            private T _last = default!;
            private bool _hasValue;
            private int _disposed;

            public DistinctSubscription(DistinctObservable<T> parent, IObserver<T> downstream)
            {
                _parent = parent;
                _downstream = downstream;
            }

            public void Attach() => _upstream = _parent._source.Subscribe(this);

            public void OnNext(T value)
            {
                if (!_hasValue || !_parent._comparer.Equals(_last, value))
                {
                    _hasValue = true;
                    _last = value;
                    _downstream.OnNext(value);
                }
            }

            public void OnError(Exception error) => _downstream.OnError(error);

            public void OnCompleted() => _downstream.OnCompleted();

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    _upstream?.Dispose();
                }
            }
        }
    }

    private sealed class MergeObservable<T> : IObservable<T>
    {
        private readonly IObservable<T> _first;
        private readonly IObservable<T> _second;

        public MergeObservable(IObservable<T> first, IObservable<T> second)
        {
            _first = first;
            _second = second;
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            var subscription = new MergeSubscription(this, observer);
            subscription.Attach();
            return subscription;
        }

        private sealed class MergeSubscription : IObserver<T>, IDisposable
        {
            private readonly MergeObservable<T> _parent;
            private readonly IObserver<T> _downstream;
            private IDisposable? _first;
            private IDisposable? _second;
            private int _remaining = 2;
            private int _stopped;
            private int _disposed;

            public MergeSubscription(MergeObservable<T> parent, IObserver<T> downstream)
            {
                _parent = parent;
                _downstream = downstream;
            }

            public void Attach()
            {
                _first = _parent._first.Subscribe(this);
                _second = _parent._second.Subscribe(this);
            }

            public void OnNext(T value) => _downstream.OnNext(value);

            public void OnError(Exception error)
            {
                if (Interlocked.Exchange(ref _stopped, 1) == 0)
                {
                    _first?.Dispose();
                    _second?.Dispose();
                    _downstream.OnError(error);
                }
            }

            public void OnCompleted()
            {
                if (Interlocked.Decrement(ref _remaining) == 0 && Interlocked.CompareExchange(ref _stopped, 1, 0) == 0)
                {
                    _downstream.OnCompleted();
                }
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    _first?.Dispose();
                    _second?.Dispose();
                }
            }
        }
    }
}
