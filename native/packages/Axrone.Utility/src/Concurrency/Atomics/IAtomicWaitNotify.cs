namespace Axrone.Utility.Concurrency;

/// <summary>Blocking wait/notify rendezvous: sleep until the value differs, wake on notify.</summary>
/// <remarks>
/// A value change without a matching notify never wakes — pair every observed store with a
/// notify. Async waiting arrives separately and is not part of this contract.
/// </remarks>
/// <typeparam name="T">Value type.</typeparam>
public interface IAtomicWaitNotify<T>
{
    /// <summary>Blocks while the value equals <paramref name="comparand"/>.</summary>
    void Wait(T comparand, MemoryOrder order = MemoryOrder.SequentiallyConsistent);

    /// <summary>Waits asynchronously while the value equals <paramref name="comparand"/>.</summary>
    ValueTask WaitAsync(T comparand, CancellationToken cancellationToken = default);

    /// <summary>Wakes one waiter.</summary>
    void NotifyOne();

    /// <summary>Wakes all waiters.</summary>
    void NotifyAll();
}
