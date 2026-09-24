namespace Axrone.Reactive;

/// <summary>
/// Subject replaying the latest value to late subscribers: capture, register, then deliver the
/// capture. A concurrent publish landing between capture and delivery yields stale-then-fresh,
/// which preserves order; fresh-then-stale can never happen.
/// </summary>
/// <typeparam name="T">Notification type.</typeparam>
public sealed class BehaviorSubject<T> : ObservableBase<T>, IObserver<T>
{
    private T _current;

    /// <summary>Creates a subject with the given current value.</summary>
    public BehaviorSubject(T initial) => _current = initial;

    /// <summary>Latest published value.</summary>
    /// <remarks>
    /// Barrier-disciplined rather than volatile: generic <c>Volatile</c> covers reference types
    /// only. Values wider than the native word may tear under a racing read; cross-thread
    /// current reads should use reference types or small value types.
    /// </remarks>
    public T Value
    {
        get
        {
            Thread.MemoryBarrier();
            return _current;
        }
    }

    /// <inheritdoc/>
    public override IDisposable Subscribe(IObserver<T> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);

        Thread.MemoryBarrier();
        T snapshot = _current;
        IDisposable subscription = base.Subscribe(observer);
        if (!IsTerminated)
        {
            observer.OnNext(snapshot);
        }

        return subscription;
    }

    /// <summary>Publishes and records as current.</summary>
    public void OnNext(T value)
    {
        _current = value;
        Thread.MemoryBarrier();
        Publish(value);
    }

    /// <summary>Delivers a terminal fault.</summary>
    public void OnError(Exception error) => Fault(error);

    /// <summary>Delivers completion.</summary>
    public void OnCompleted() => Complete();
}
