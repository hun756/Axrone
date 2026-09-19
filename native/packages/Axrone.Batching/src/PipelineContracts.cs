namespace Axrone.Batching;

/// <summary>Producer side of a batch pipeline.</summary>
/// <typeparam name="T">Element type.</typeparam>
public interface IBatchProducer<T> where T : unmanaged
{
    /// <summary>Elements per buffer slot.</summary>
    int Capacity { get; }

    /// <summary>Appends one item to the producer slot.</summary>
    /// <param name="item">Item to append.</param>
    /// <returns><see langword="false"/> when the producer slot is full.</returns>
    bool TryWrite(in T item);

    /// <summary>Appends items to the producer slot.</summary>
    /// <param name="items">Items to append.</param>
    /// <returns>Elements accepted; may be fewer than requested when full.</returns>
    int WriteRange(ReadOnlySpan<T> items);

    /// <summary>Publishes the producer slot.</summary>
    void SwapProducer();
}

/// <summary>Budgeted execution side of a batch pipeline.</summary>
/// <typeparam name="T">Element type.</typeparam>
public interface IBatchSlicer<T> where T : unmanaged
{
    /// <summary>Whether an acquired batch still has unprocessed elements.</summary>
    bool HasRemainingWork { get; }

    /// <summary>Runs a batch kernel within a frame budget.</summary>
    /// <typeparam name="TKernel">Kernel type, held by ref so the call devirtualizes.</typeparam>
    /// <param name="kernel">Batch kernel.</param>
    /// <param name="budget">Slice deadline.</param>
    /// <returns>How the pass ended and how much work remains.</returns>
    SliceResult ExecuteSlice<TKernel>(ref TKernel kernel, in FrameBudget budget)
        where TKernel : struct, IBatchKernel<T>;

    /// <summary>Runs an element kernel within a frame budget.</summary>
    /// <typeparam name="TKernel">Kernel type, held by ref so the call devirtualizes.</typeparam>
    /// <param name="kernel">Element kernel.</param>
    /// <param name="budget">Slice deadline.</param>
    /// <returns>How the pass ended and how much work remains.</returns>
    SliceResult ExecuteElements<TKernel>(ref TKernel kernel, in FrameBudget budget)
        where TKernel : struct, IElementKernel<T>;
}

/// <summary>Zero-allocation visitor over a drained batch.</summary>
/// <typeparam name="T">Element type.</typeparam>
/// <typeparam name="TState">Visitor state; may be a stack-only type.</typeparam>
public interface IBatchVisitor<T, TState>
    where T : unmanaged
    where TState : allows ref struct
{
    /// <summary>Consumes one batch.</summary>
    /// <param name="batch">Items to consume; the callee may not retain the span.</param>
    /// <param name="state">Visitor state.</param>
    void Visit(ReadOnlySpan<T> batch, ref TState state);
}
