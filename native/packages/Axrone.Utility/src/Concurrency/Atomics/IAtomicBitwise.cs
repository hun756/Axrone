namespace Axrone.Utility.Concurrency;

/// <summary>Atomic bitwise operations returning the new value.</summary>
/// <typeparam name="T">Integer value type.</typeparam>
public interface IAtomicBitwise<T>
    where T : unmanaged, IBitwiseOperators<T, T, T>
{
    /// <summary>Applies the mask and returns the new value.</summary>
    T And(T mask, MemoryOrder order = MemoryOrder.SequentiallyConsistent);

    /// <summary>Applies the mask and returns the new value.</summary>
    T Or(T mask, MemoryOrder order = MemoryOrder.SequentiallyConsistent);

    /// <summary>Applies the mask and returns the new value.</summary>
    T Xor(T mask, MemoryOrder order = MemoryOrder.SequentiallyConsistent);
}
