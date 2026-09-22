namespace Axrone.Reactive;

/// <summary>
/// Derivable cold-observable root: game classes inherit stream behavior instead of wiring it.
/// </summary>
/// <remarks>
/// Zero-cost contract: subscribing allocates (node + array copy, cold path); publishing never
/// allocates and never locks — readers take a snapshot reference. Concurrent publishes may
/// interleave per observer; terminal delivery (error/completed) replays to late subscribers.
/// An observer that throws propagates to the publisher; faulty observers must be isolated by
/// the subscriber side, not the source. No schedulers, no timers — the engine drives threads.
/// </remarks>
/// <typeparam name="T">Notification type.</typeparam>
public abstract class ObservableBase<T> : IObservable<T>, IDisposable
{
    private const int StateActive = 0;
    private const int StateCompleted = 1;
    private const int StateFaulted = 2;

    private IObserver<T>[] _observers = [];
    private int _terminal;
    private Exception? _error;
    private int _disposed;

    /// <summary>Guards fault publication only; hot paths never take it.</summary>
    private readonly Lock _faultGate = new();

    /// <summary>Live subscriptions.</summary>
    public int SubscriberCount => Volatile.Read(ref _observers).Length;

    /// <summary>Whether error/completed was delivered.</summary>
    public bool IsTerminated => Volatile.Read(ref _terminal) != StateActive;

    /// <inheritdoc/>
    public virtual IDisposable Subscribe(IObserver<T> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        int terminal = Volatile.Read(ref _terminal);
        if (terminal == StateCompleted)
        {
            observer.OnCompleted();
            return NoopDisposable.Instance;
        }

        if (terminal == StateFaulted)
        {
            return SubscribeTerminal(observer);
        }

        while (true)
        {
            IObserver<T>[] current = Volatile.Read(ref _observers);
            if (Volatile.Read(ref _terminal) != StateActive)
            {
                return SubscribeTerminal(observer);
            }

            var next = new IObserver<T>[current.Length + 1];
            Array.Copy(current, next, current.Length);
            next[current.Length] = observer;
            if (Interlocked.CompareExchange(ref _observers, next, current) == current)
            {
                ReactiveTelemetry.Subscribed();
                return new Subscription(this, observer);
            }
        }
    }

    /// <summary>Publishes to current subscribers; ignored once terminated.</summary>
    protected void Publish(T value)
    {
        if (Volatile.Read(ref _terminal) != StateActive)
        {
            return;
        }

        IObserver<T>[] snapshot = Volatile.Read(ref _observers);
        for (int i = 0; i < snapshot.Length; i++)
        {
            snapshot[i].OnNext(value);
        }
    }

    /// <summary>Delivers completion; later publishes are ignored, late subscribers replay it.</summary>
    protected void Complete()
    {
        if (Interlocked.CompareExchange(ref _terminal, StateCompleted, StateActive) != StateActive)
        {
            return;
        }

        ReactiveTelemetry.SourceCompleted();
        ReactiveEventSource.Log.SourceCompleted();

        IObserver<T>[] snapshot = Interlocked.Exchange(ref _observers, []);
        for (int i = 0; i < snapshot.Length; i++)
        {
            snapshot[i].OnCompleted();
        }
    }

    /// <summary>Delivers a terminal fault; later publishes are ignored, late subscribers replay it.</summary>
    protected void Fault(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);

        IObserver<T>[] snapshot;
        lock (_faultGate)
        {
            if (_terminal != StateActive)
            {
                return;
            }

            _error = error;
            Volatile.Write(ref _terminal, StateFaulted);
            snapshot = Interlocked.Exchange(ref _observers, []);
        }

        ReactiveTelemetry.SourceFaulted();
        ReactiveEventSource.Log.FaultDelivered(error.GetType().Name, error.Message);

        for (int i = 0; i < snapshot.Length; i++)
        {
            snapshot[i].OnError(error);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases subscriptions; derived types release managed state when disposing.</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (disposing)
        {
            Complete();
        }
    }

    private NoopDisposable SubscribeTerminal(IObserver<T> observer)
    {
        if (Volatile.Read(ref _terminal) == StateFaulted)
        {
            Exception error;
            lock (_faultGate)
            {
                error = _error!;
            }

            observer.OnError(error);
        }
        else
        {
            observer.OnCompleted();
        }

        return NoopDisposable.Instance;
    }

    private void Unsubscribe(Subscription subscription)
    {
        while (true)
        {
            IObserver<T>[] current = Volatile.Read(ref _observers);
            int index = -1;
            for (int i = 0; i < current.Length; i++)
            {
                if (ReferenceEquals(current[i], subscription.Observer))
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                return;
            }

            var next = new IObserver<T>[current.Length - 1];
            Array.Copy(current, 0, next, 0, index);
            Array.Copy(current, index + 1, next, index, current.Length - index - 1);
            if (Interlocked.CompareExchange(ref _observers, next, current) == current)
            {
                return;
            }
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly ObservableBase<T> _source;
        private int _disposed;

        public IObserver<T> Observer { get; }

        public Subscription(ObservableBase<T> source, IObserver<T> observer)
        {
            _source = source;
            Observer = observer;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _source.Unsubscribe(this);
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
