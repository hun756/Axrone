namespace Axrone.Utility.Concurrency;

/// <summary>Atomic arithmetic returning the new value (addAndGet semantics).</summary>
/// <typeparam name="T">Numeric value type.</typeparam>
public interface IAtomicNumber<T> : IAtomic<T>
    where T : unmanaged, INumber<T>
{
    /// <summary>Adds and returns the new value.</summary>
    T Add(T value, MemoryOrder order = MemoryOrder.SequentiallyConsistent);

    /// <summary>Subtracts and returns the new value.</summary>
    T Subtract(T value, MemoryOrder order = MemoryOrder.SequentiallyConsistent);

    /// <summary>Adds one and returns the new value.</summary>
    T Increment();

    /// <summary>Subtracts one and returns the new value.</summary>
    T Decrement();
}
