namespace Axrone.Batching;

/// <summary>
/// Polled execution path for the time-sliced pipeline.
/// </summary>
/// <remarks>
/// Between the non-blocking <see cref="IBatchSlicer{T}.ExecuteSlice"/> fast path and a blocking
/// wait sits this middle ground: bounded slices with a policy backoff between overruns. The backoff
/// is a static-abstract policy (<see cref="ISpinBackoff"/>) so RyuJIT specializes per policy with
/// no vtable, following the house pattern in <c>Axrone.Collections</c> and <c>Axrone.Memory</c>.
/// </remarks>
public sealed partial class TimeSlicedPipeline<T>
{
    /// <summary>Per-attempt slice of the spinning path, in milliseconds.</summary>
    public const double SpinSliceMilliseconds = 1d;

    /// <summary>
    /// Spins until the active batch drains or cancellation fires.
    /// </summary>
    /// <typeparam name="TKernel">Kernel type, held by ref so the call devirtualizes.</typeparam>
    /// <typeparam name="TBackoff">Backoff policy.</typeparam>
    /// <param name="kernel">Batch kernel.</param>
    /// <param name="cancellationToken">Stops the spin.</param>
    public void ExecuteSpinning<TKernel, TBackoff>(
        ref TKernel kernel, CancellationToken cancellationToken = default)
        where TKernel : struct, IBatchKernel<T>
        where TBackoff : struct, ISpinBackoff
    {
        TBackoff.Initialize(out var backoff);
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = ExecuteSlice(ref kernel, new FrameBudget(SpinSliceMilliseconds));
            if (result.Status == FrameBudgetStatus.BudgetExceeded)
            {
                TBackoff.Advance(ref backoff);
            }
            else
            {
                TBackoff.Reset(ref backoff);
            }
        }
        while (HasRemainingWork);
    }

    /// <summary>
    /// Spins until the active batch drains through an element kernel or cancellation fires.
    /// </summary>
    /// <typeparam name="TKernel">Kernel type, held by ref so the call devirtualizes.</typeparam>
    /// <typeparam name="TBackoff">Backoff policy.</typeparam>
    /// <param name="kernel">Element kernel.</param>
    /// <param name="cancellationToken">Stops the spin.</param>
    public void ExecuteElementsSpinning<TKernel, TBackoff>(
        ref TKernel kernel, CancellationToken cancellationToken = default)
        where TKernel : struct, IElementKernel<T>
        where TBackoff : struct, ISpinBackoff
    {
        TBackoff.Initialize(out var backoff);
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = ExecuteElements(ref kernel, new FrameBudget(SpinSliceMilliseconds));
            if (result.Status == FrameBudgetStatus.BudgetExceeded)
            {
                TBackoff.Advance(ref backoff);
            }
            else
            {
                TBackoff.Reset(ref backoff);
            }
        }
        while (HasRemainingWork);
    }
}
