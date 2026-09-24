namespace Axrone.Utility.Concurrency;

/// <summary>Computes the next value from the current one inside a CAS loop.</summary>
/// <typeparam name="T">Value type.</typeparam>
/// <typeparam name="TResult">Result type.</typeparam>
public interface IAtomicTransformer<T, TResult>
    where T : allows ref struct
    where TResult : allows ref struct
{
    /// <summary>Derives the next value; must be pure (may run multiple times).</summary>
    static abstract TResult Transform(in T current);
}
