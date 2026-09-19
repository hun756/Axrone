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
    /// Spins until the active batch drains or the retry policy gives up.
    /// </summary>
    /// <typeparam name="TKernel">Kernel type, held by ref so the call devirtualizes.</typeparam>
    /// <typeparam name="TBackoff">Backoff policy for pacing between overruns.</typeparam>
    /// <param name="kernel">Batch kernel.</param>
    /// <param name="retry">Bounds overrun attempts and stand-downs.</param>
    /// <param name="cancellationToken">Stops the spin.</param>
    /// <returns>Overrun slices tolerated before stopping with work remaining.</returns>
    public int ExecuteSpinning<TKernel, TBackoff>(
        ref TKernel kernel, SliceRetryPolicy retry, CancellationToken cancellationToken = default)
        where TKernel : struct, IBatchKernel<T>
        where TBackoff : struct, ISpinBackoff
    {
        TBackoff.Initialize(out var backoff);
        var overruns = 0;
        var rngState = (ulong)Stopwatch.GetTimestamp() | 0x9E3779B97F4A7C15UL;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = ExecuteSlice(ref kernel, new FrameBudget(SpinSliceMilliseconds));
            if (result.Status != FrameBudgetStatus.BudgetExceeded)
            {
                TBackoff.Reset(ref backoff);
            }
            else if (retry.ShouldRetry(++overruns))
            {
                var standDownMs = retry.DelayTicks(overruns, ref rngState) * 1000d / Stopwatch.Frequency;
                if (standDownMs >= 1d)
                {
                    Thread.Sleep((int)Math.Min(standDownMs, 50d));
                }
                else
                {
                    TBackoff.Advance(ref backoff);
                }
            }
            else
            {
                break;
            }
        }
        while (HasRemainingWork);
        return overruns;
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
