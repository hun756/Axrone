namespace Axrone.Batching;

/// <summary>
/// Transforms a contiguous batch of elements in place.
/// </summary>
/// <typeparam name="T">Element type. Unmanaged so batches can back onto native memory.</typeparam>
/// <remarks>
/// Implementations are expected to be <c>struct</c> types: the batching pipeline holds kernels by
/// <c>ref</c> under this constraint so the dispatch devirtualizes and the batch loop stays
/// allocation-free. A class implementation compiles but forfeits that guarantee.
/// </remarks>
public interface IBatchKernel<T> where T : unmanaged
{
    /// <summary>Processes <paramref name="batch"/>, writing results back into the same span.</summary>
    /// <param name="batch">The elements to transform. The callee may not retain this span.</param>
    void Execute(scoped Span<T> batch);
}

/// <summary>
/// Transforms a single element in place; the pipeline applies it across a batch.
/// </summary>
/// <typeparam name="T">Element type. Unmanaged so batches can back onto native memory.</typeparam>
/// <remarks>
/// Use this over <see cref="IBatchKernel{T}"/> when the per-element rule is the natural unit and
/// the caller wants the pipeline to own chunking and stride selection. When the operation can
/// exploit cross-element structure, prefer <see cref="IBatchKernel{T}"/>.
/// </remarks>
public interface IElementKernel<T> where T : unmanaged
{
    /// <summary>Transforms one element.</summary>
    /// <param name="item">The element to mutate. The callee may not retain this reference.</param>
    void Execute(scoped ref T item);
}

/// <summary>
/// Decides whether a single element is retained by a compaction or filter pass.
/// </summary>
/// <typeparam name="T">Element type. Unmanaged so batches can back onto native memory.</typeparam>
/// <remarks>
/// Implementations must be pure and side-effect free: compaction may evaluate a predicate more than
/// once per element as it widens or narrows its processing stride.
/// </remarks>
public interface IBatchPredicate<T> where T : unmanaged
{
    /// <summary>Returns <see langword="true"/> when <paramref name="item"/> should be retained.</summary>
    /// <param name="item">The element to test.</param>
    bool Evaluate(in T item);
}

/// <summary>
/// Asserts an invariant over a whole batch, enabling bisection isolation of the failing range.
/// </summary>
/// <typeparam name="T">Element type. Unmanaged so batches can back onto native memory.</typeparam>
/// <remarks>
/// The contract is a batch-level check, not a per-element one: the isolator halves the range until
/// it pinpoints the offending element, so <see cref="Validate"/> is called on many overlapping
/// sub-ranges of the same input. Implementations must therefore be cheap relative to their range
/// length, or the bisection degrades to worse than a linear scan.
/// </remarks>
public interface IBatchValidator<T> where T : unmanaged
{
    /// <summary>Returns <see langword="true"/> when every element of <paramref name="batch"/> satisfies the invariant.</summary>
    /// <param name="batch">The range to validate. The callee may not retain this span.</param>
    bool Validate(scoped ReadOnlySpan<T> batch);
}

/// <summary>
/// Establishes a total order over elements for the batch sort kernels.
/// </summary>
/// <typeparam name="T">Element type. Unmanaged so batches can back onto native memory.</typeparam>
/// <remarks>
/// <para>
/// The sorts in this package assume a strict weak ordering. An inconsistent comparer — one where
/// <c>Compare(a, b)</c> and <c>Compare(b, a)</c> disagree, or that is not transitive — does not
/// merely produce a wrong order: the introsort partition and heapify steps index off the end of the
/// batch while searching for a pivot that never resolves.
/// </para>
/// <para>
/// Unlike <see cref="IComparer{T}"/>, this is not required to be consistent with
/// <see cref="object.Equals(object)"/>; it only has to be internally consistent.
/// </para>
/// </remarks>
public interface IBatchComparer<T> where T : unmanaged
{
    /// <summary>Compares two elements.</summary>
    /// <param name="left">The first element.</param>
    /// <param name="right">The second element.</param>
    /// <returns>A value less than, equal to, or greater than zero when <paramref name="left"/> sorts before, alongside, or after <paramref name="right"/>.</returns>
    int Compare(T left, T right);
}
