namespace Axrone.Memory.Arena;

public sealed class RingProducer<T, TBackoff> : IArenaProducer<T>
    where T : unmanaged
    where TBackoff : struct, IBackoffPolicy
{
    private readonly RingCore<T, TBackoff> _core;

    internal RingProducer(RingCore<T, TBackoff> core) => _core = core;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryWrite(in T item)
    {
        _core.Lifecycle.AcquireLease();
        bool committed = false;
        try
        {
            nuint mask = _core.Capacity.Mask;
            while (true)
            {
                long currentTail = _core.TailCommitted.Value;
                long currentHead = _core.HeadReserved.Value;

                if ((ulong)(currentHead - currentTail) >= _core.Capacity.Value)
                {
                    return false;
                }

                if (_core.HeadReserved.CompareExchange(currentHead + 1, currentHead))
                {
                    nuint index = (nuint)currentHead & mask;
                    unsafe { *(_core.Storage.BasePointer + index) = item; }

                    _core.CommitWrite((ulong)currentHead, 1);
                    committed = true;
                    return true;
                }
            }
        }
        finally
        {
            if (!committed)
            {
                _core.Lifecycle.ReleaseLease();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void WriteSpinning(in T item, CancellationToken cancellationToken = default)
    {
        int spinCount = 0;
        ulong contention = 0;
        while (!TryWrite(in item))
        {
            cancellationToken.ThrowIfCancellationRequested();
            _core.Lifecycle.ThrowIfTerminated();

            TBackoff.Step(ref spinCount);
            contention++;
        }

        if (contention > 0)
        {
            _core.Telemetry.RecordContention(contention);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public async ValueTask WriteAsync(T item, CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _core.Lifecycle.ThrowIfTerminated();

            ValueTask<bool> waitTask = _core.WriteSignal.WaitAsync(cancellationToken);

            if (TryWrite(in item))
            {
                return;
            }

            await waitTask.ConfigureAwait(false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool TryWriteMany(params ReadOnlySpan<T> items)
    {
        if (items.IsEmpty) return true;

        var batch = new RingBatchCoordinator<T, TBackoff>(_core);
        if (!batch.TryReserveWrite(items.Length, out var reservation))
        {
            return false;
        }

        try
        {
            items[..reservation.FirstSegment.Length].CopyTo(reservation.FirstSegment);
            if (!reservation.SecondSegment.IsEmpty)
            {
                items[reservation.FirstSegment.Length..].CopyTo(reservation.SecondSegment);
            }
            reservation.Commit();
            return true;
        }
        finally
        {
            reservation.Dispose();
        }
    }
}
