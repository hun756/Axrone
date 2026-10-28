namespace Axrone.Reactive;

/// <summary>
/// Time-based operators driven by <see cref="TimeProvider"/>. Notifications never allocate;
/// the debounce timer and its callback are created once per subscription.
/// </summary>
public static partial class ObservableOperators
{
    /// <summary>
    /// Emits the latest value once no newer value arrived for <paramref name="dueTime"/>.
    /// Completion and error drop any pending value.
    /// </summary>
    public static IObservable<T> Debounce<T>(
        this IObservable<T> source,
        TimeSpan dueTime,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (dueTime <= TimeSpan.Zero)
        {
            return source;
        }

        return new DebounceObservable<T>(source, dueTime, timeProvider ?? TimeProvider.System);
    }

    /// <summary>
    /// Emits the first value and then at most one value per <paramref name="interval"/>.
    /// Uses a leading edge; values inside the window are dropped.
    /// </summary>
    public static IObservable<T> Throttle<T>(
        this IObservable<T> source,
        TimeSpan interval,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (interval <= TimeSpan.Zero)
        {
            return source;
        }

        return new ThrottleObservable<T>(source, interval, timeProvider ?? TimeProvider.System);
    }

    private sealed class DebounceObservable<T> : IObservable<T>
    {
        private readonly IObservable<T> _source;
        private readonly TimeSpan _dueTime;
        private readonly TimeProvider _timeProvider;

        public DebounceObservable(IObservable<T> source, TimeSpan dueTime, TimeProvider timeProvider)
        {
            _source = source;
            _dueTime = dueTime;
            _timeProvider = timeProvider;
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            var subscription = new DebounceSubscription<T>(_source, observer, _dueTime, _timeProvider);
            subscription.Attach();
            return subscription;
        }
    }

    private sealed class DebounceSubscription<T> : IObserver<T>, IDisposable
    {
        private readonly IObservable<T> _source;
        private readonly IObserver<T> _downstream;
        private readonly TimeSpan _dueTime;
        private readonly ITimer _timer;
        private readonly Lock _gate = new();
        private IDisposable? _upstream;
        private T _latest = default!;
        private bool _hasValue;
        private int _disposed;

        public DebounceSubscription(
            IObservable<T> source,
            IObserver<T> downstream,
            TimeSpan dueTime,
            TimeProvider timeProvider)
        {
            _source = source;
            _downstream = downstream;
            _dueTime = dueTime;
            _timer = timeProvider.CreateTimer(
                static state => ((DebounceSubscription<T>)state!).OnTimer(),
                this,
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);
        }

        public void Attach()
        {
            IDisposable upstream = _source.Subscribe(this);
            _upstream = upstream;
            if (Volatile.Read(ref _disposed) != 0)
            {
                upstream.Dispose();
            }
        }

        public void OnNext(T value)
        {
            lock (_gate)
            {
                if (_disposed != 0)
                {
                    return;
                }

                _latest = value;
                _hasValue = true;
                _timer.Change(_dueTime, Timeout.InfiniteTimeSpan);
            }
        }

        public void OnError(Exception error)
        {
            DisposeCore();
            _downstream.OnError(error);
        }

        public void OnCompleted()
        {
            DisposeCore();
            _downstream.OnCompleted();
        }

        public void Dispose() => DisposeCore();

        private void OnTimer()
        {
            T value;
            lock (_gate)
            {
                if (_disposed != 0 || !_hasValue)
                {
                    return;
                }

                value = _latest;
                _hasValue = false;
            }

            try
            {
                _downstream.OnNext(value);
            }
            catch (Exception error)
            {
                _downstream.OnError(error);
            }
        }

        private void DisposeCore()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _timer.Dispose();
            _upstream?.Dispose();
            lock (_gate)
            {
                _hasValue = false;
                _latest = default!;
            }
        }
    }

    private sealed class ThrottleObservable<T> : IObservable<T>
    {
        private readonly IObservable<T> _source;
        private readonly TimeSpan _interval;
        private readonly TimeProvider _timeProvider;

        public ThrottleObservable(IObservable<T> source, TimeSpan interval, TimeProvider timeProvider)
        {
            _source = source;
            _interval = interval;
            _timeProvider = timeProvider;
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            var subscription = new ThrottleSubscription<T>(_source, observer, _interval, _timeProvider);
            subscription.Attach();
            return subscription;
        }
    }

    private sealed class ThrottleSubscription<T> : IObserver<T>, IDisposable
    {
        private readonly IObservable<T> _source;
        private readonly IObserver<T> _downstream;
        private readonly TimeSpan _interval;
        private readonly TimeProvider _timeProvider;
        private readonly Lock _gate = new();
        private IDisposable? _upstream;
        private long _lastTimestamp;
        private bool _hasEmitted;
        private int _disposed;

        public ThrottleSubscription(
            IObservable<T> source,
            IObserver<T> downstream,
            TimeSpan interval,
            TimeProvider timeProvider)
        {
            _source = source;
            _downstream = downstream;
            _interval = interval;
            _timeProvider = timeProvider;
        }

        public void Attach()
        {
            IDisposable upstream = _source.Subscribe(this);
            _upstream = upstream;
            if (Volatile.Read(ref _disposed) != 0)
            {
                upstream.Dispose();
            }
        }

        public void OnNext(T value)
        {
            bool emit;
            lock (_gate)
            {
                if (_disposed != 0)
                {
                    return;
                }

                long now = _timeProvider.GetTimestamp();
                emit = !_hasEmitted ||
                    _timeProvider.GetElapsedTime(_lastTimestamp, now) >= _interval;
                if (emit)
                {
                    _hasEmitted = true;
                    _lastTimestamp = now;
                }
            }

            if (emit)
            {
                _downstream.OnNext(value);
            }
        }

        public void OnError(Exception error)
        {
            DisposeCore();
            _downstream.OnError(error);
        }

        public void OnCompleted()
        {
            DisposeCore();
            _downstream.OnCompleted();
        }

        public void Dispose() => DisposeCore();

        private void DisposeCore()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _upstream?.Dispose();
        }
    }
}
