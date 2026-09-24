namespace Axrone.Utility.Concurrency;

/// <summary>Computes the next value from the current one plus an argument inside a CAS loop.</summary>
/// <typeparam name="T">Value type.</typeparam>
public interface IAtomicMutator<T>
    where T : allows ref struct
{
    /// <summary>Derives the next value; must be pure (may run multiple times).</summary>
    static abstract T Mutate(in T current, in T argument);
}
