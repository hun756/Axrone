namespace Axrone.Reactive;

using Axrone.Reactive.Internal;

/// <summary>
/// Hot broadcast hub: both observer and observable. Late subscribers see only future values.
/// </summary>
/// <remarks>
/// <para>Backed by the generation-tagged <see cref="ObserverEngine{T}"/>: subscription is an O(1)
/// slot claim, publish walks the active slots in priority and registration order, and disposing a
/// subscription from inside a callback is safe because reclamation is deferred to the last pin
/// release.</para>
/// <para>Fault policy follows <see cref="SubjectOptions.ErrorPropagation"/>: the default rethrows
/// observer faults to the publisher; disabling it isolates each fault and continues the broadcast.
/// </para>
/// </remarks>
public sealed class Subject<T> : IObservable<T>, IObserver<T>, IPriorityObservable<T>, IDisposable
{
    private readonly ObserverEngine<T> _engine;
    private readonly SubjectOptions _options;
    private int _disposed;

    public Subject()
        : this(SubjectOptions.Default)
    {
    }

    public Subject(in SubjectOptions options)
    {
        _options = options.Normalize();
        int requested = Math.Max(2, _options.MaxObservers);
        int slots = NextPowerOfTwo(requested > 16_384 ? 65_536 : requested * 4);
        _engine = new ObserverEngine<T>(new ObserverEngineOptions
        {
            MaxObservers = slots,
            MaxSubscribers = _options.MaxObservers,
            FaultBehavior = _options.ErrorPropagation
                ? ObserverFaultBehavior.Propagate
                : ObserverFaultBehavior.IsolateObserver,
        });
    }

    /// <summary>Live subscriptions.</summary>
    public int SubscriberCount => _engine.ActiveSubscriberCount;

    /// <summary>Observer callbacks that faulted since construction.</summary>
    public int ObserverFaultCount => _engine.ObserverFaultCount;

    /// <summary>Whether completion or a fault was signalled.</summary>
    public bool IsTerminated => _engine.Status is
        ObserverEngineStatus.Completed or
        ObserverEngineStatus.Faulted or
        ObserverEngineStatus.Disposed;

    /// <summary>Whether the terminal signal was a fault.</summary>
    public bool IsFaulted => _engine.Status == ObserverEngineStatus.Faulted;

    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<T> observer) =>
        Subscribe(observer, ObservationPriority.Normal);

    /// <summary>
    /// Subscribes at an explicit priority. High runs first, low last; equal priorities keep
    /// registration order.
    /// </summary>
    public IDisposable Subscribe(IObserver<T> observer, ObservationPriority priority)
    {
        ArgumentNullException.ThrowIfNull(observer);

        ObserverEngineStatus status = _engine.Status;
        if (status is ObserverEngineStatus.Completed or ObserverEngineStatus.Disposed)
        {
            observer.OnCompleted();
            return NoopDisposable.Instance;
        }

        if (status == ObserverEngineStatus.Faulted)
        {
            observer.OnError(_engine.TerminalError!);
            return NoopDisposable.Instance;
        }

        try
        {
            ObserverSubscription subscription = _engine.Subscribe(new ObserverAdapter(observer), (byte)priority);
            ReactiveTelemetry.Subscribed();
            return new Subscription(subscription);
        }
        catch (InvalidOperationException) when (_engine.Status == ObserverEngineStatus.Active)
        {
            ReactiveThrowHelper.ThrowMaxObserversExceeded(_options.MaxObservers, SubscriberCount);
            throw;
        }
        catch (InvalidOperationException)
        {
            if (_engine.Status == ObserverEngineStatus.Faulted)
            {
                observer.OnError(_engine.TerminalError!);
            }
            else
            {
                observer.OnCompleted();
            }

            return NoopDisposable.Instance;
        }
    }

    /// <summary>Publishes to current subscribers; ignored once terminated.</summary>
    public void OnNext(T value)
    {
        if (_engine.Status != ObserverEngineStatus.Active)
        {
            return;
        }

        _ = _engine.TryPublish(value, out _);
    }

    /// <summary>Delivers a terminal fault.</summary>
    public void OnError(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (!_engine.Complete(error))
        {
            return;
        }

        ReactiveTelemetry.SourceFaulted();
        ReactiveEventSource.Log.FaultDelivered(error.GetType().Name, error.Message);
        if (_options.AutoComplete)
        {
            Dispose();
        }
    }

    /// <summary>Delivers completion.</summary>
    public void OnCompleted()
    {
        if (!_engine.Complete())
        {
            return;
        }

        ReactiveTelemetry.SourceCompleted();
        ReactiveEventSource.Log.SourceCompleted();
        if (_options.AutoComplete)
        {
            Dispose();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        bool wasActive = _engine.Status == ObserverEngineStatus.Active;
        _engine.Dispose();
        if (wasActive)
        {
            ReactiveTelemetry.SourceCompleted();
            ReactiveEventSource.Log.SourceCompleted();
        }
    }

    private static int NextPowerOfTwo(int value)
    {
        int result = 2;
        while (result < value)
        {
            result <<= 1;
        }

        return result;
    }

    private sealed class ObserverAdapter(IObserver<T> observer) : IValueObserver<T>
    {
        public void OnNext(in T value, nint context) => observer.OnNext(value);

        public void OnError(Exception error, nint context) => observer.OnError(error);

        public void OnCompleted(nint context) => observer.OnCompleted();
    }

    private sealed class Subscription : IDisposable
    {
        private readonly ObserverSubscription _inner;
        private int _disposed;

        public Subscription(ObserverSubscription inner) => _inner = inner;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _inner.Dispose();
            }
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
