namespace Axrone.Memory.Arena;

public sealed class RingConsumer<T, TBackoff> : IArenaConsumer<T>
    where T : unmanaged
    where TBackoff : struct, ISpinBackoff
{
    private readonly RingCore<T, TBackoff> _core;

    internal RingConsumer(RingCore<T, TBackoff> core) => _core = core;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryRead(out T item)
    {
        _core.Lifecycle.AcquireLease();
        bool committed = false;
        try
        {
            nuint mask = _core.Capacity.Mask;
            while (true)
            {
                long currentTail = _core.TailReserved.Value;
                long currentHead = _core.HeadCommitted.Value;

                if (currentTail >= currentHead)
                {
                    item = default;
                    return false;
                }

                if (_core.TailReserved.CompareExchange(currentTail + 1, currentTail))
                {
                    nuint index = (nuint)currentTail & mask;
                    unsafe { item = *(_core.Storage.BasePointer + index); }

                    _core.CommitRead((ulong)currentTail, 1);
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
    public bool TryReadSpinning(out T item, CancellationToken cancellationToken = default)
    {
        int spinCount = 0;
        ulong contention = 0;
        while (!TryRead(out item))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_core.Lifecycle.IsCompleted && _core.HeadCommitted.Value == _core.TailCommitted.Value)
            {
                item = default;
                return false;
            }
            _core.Lifecycle.ThrowIfTerminated();

            TBackoff.Advance(ref spinCount);
            contention++;
        }

        if (contention > 0)
        {
            _core.Telemetry.RecordContention(contention);
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public async ValueTask<T> ReadAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _core.Lifecycle.ThrowIfTerminated();

            ValueTask<bool> waitTask = _core.ReadSignal.WaitAsync(cancellationToken);

            if (TryRead(out var item))
            {
                return item;
            }

            if (_core.Lifecycle.IsCompleted && _core.HeadCommitted.Value == _core.TailCommitted.Value)
            {
                ThrowHelper.ThrowInvalidOperationException("Ring buffer drained and marked complete.");
            }

            await waitTask.ConfigureAwait(false);
        }
    }
}
