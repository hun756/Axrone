namespace Axrone.Batching;

using System.Runtime.ExceptionServices;

/// <summary>
/// Graceful termination for the time-sliced pipeline.
/// </summary>
/// <remarks>
/// Proportionate to a synchronous SPSC buffer: no async drain engine, no lease table — the full
/// two-phase machinery lives in <c>Axrone.Memory.Arena</c> where leased reservations exist. What
/// this guarantees: after <see cref="Complete"/>, no new input is accepted; fault causes travel
/// with their stack via <see cref="ExceptionDispatchInfo"/>; disposal never tears an in-flight
/// slice because slices borrow managed slots, never native memory.
/// </remarks>
public sealed partial class TimeSlicedPipeline<T>
{
    private const int LifecycleActive = 0;
    private const int LifecycleCompleting = 1;
    private const int LifecycleFaulted = 2;

    private int _lifecycle;
    private ExceptionDispatchInfo? _fault;

    /// <summary>Whether the pipeline accepts work and reports no terminal fault.</summary>
    public bool IsHealthy => Volatile.Read(ref _lifecycle) == LifecycleActive;

    /// <summary>Cause of the terminal fault, if any.</summary>
    public Exception? TerminalFault => Volatile.Read(ref _fault)?.SourceException;

    /// <summary>Signals no more input; acquired batches still drain.</summary>
    /// <param name="error">Terminal fault cause; <see langword="null"/> for a clean completion.</param>
    /// <remarks>A fault sticks: a later clean <see cref="Complete"/> never clears it.</remarks>
    public void Complete(Exception? error = null)
    {
        if (error is not null)
        {
            Volatile.Write(ref _fault, ExceptionDispatchInfo.Capture(error));
            Volatile.Write(ref _lifecycle, LifecycleFaulted);
        }
        else if (Volatile.Read(ref _lifecycle) == LifecycleActive)
        {
            Volatile.Write(ref _lifecycle, LifecycleCompleting);
        }
    }

    /// <summary>Drains remaining batches without a budget.</summary>
    /// <typeparam name="TKernel">Kernel type, held by ref so the call devirtualizes.</typeparam>
    /// <param name="kernel">Batch kernel.</param>
    /// <param name="cancellationToken">Stops the drain.</param>
    public void Drain<TKernel>(ref TKernel kernel, CancellationToken cancellationToken = default)
        where TKernel : struct, IBatchKernel<T>
    {
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfTerminated();
            ExecuteSlice(ref kernel, FrameBudget.Infinite);
        }
        while (HasRemainingWork);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfTerminated()
    {
        Volatile.Read(ref _fault)?.Throw();
        if (Volatile.Read(ref _lifecycle) == LifecycleFaulted)
        {
            ThrowHelper.ThrowInvalidOperationException("Pipeline is faulted.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsActive() => Volatile.Read(ref _lifecycle) == LifecycleActive;
}
