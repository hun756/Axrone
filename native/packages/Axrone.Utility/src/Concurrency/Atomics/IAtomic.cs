namespace Axrone.Utility.Concurrency;

/// <summary>Core load/store/exchange/compare contract over an atomic location.</summary>
/// <typeparam name="T">Value type.</typeparam>
public interface IAtomic<T>
{
    /// <summary>Current value (sequentially consistent).</summary>
    T Value { get; set; }

    /// <summary>Whether the location updates without locks.</summary>
    bool IsLockFree { get; }

    /// <summary>Reads the value.</summary>
    T Load(MemoryOrder order = MemoryOrder.SequentiallyConsistent);

    /// <summary>Writes the value.</summary>
    void Store(T desired, MemoryOrder order = MemoryOrder.SequentiallyConsistent);

    /// <summary>Writes and returns the previous value.</summary>
    T Exchange(T desired, MemoryOrder order = MemoryOrder.SequentiallyConsistent);

    /// <summary>Conditional write; refreshes <paramref name="expected"/> on mismatch.</summary>
    bool CompareExchange(ref T expected, T desired, MemoryOrder order = MemoryOrder.SequentiallyConsistent);

    /// <summary>Conditional write with split success/failure orders.</summary>
    bool CompareExchange(ref T expected, T desired, MemoryOrder success, MemoryOrder failure);
}
