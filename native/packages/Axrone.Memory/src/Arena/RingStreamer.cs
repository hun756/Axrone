using System.Runtime.CompilerServices;

namespace Axrone.Memory.Arena;

public sealed class RingStreamer<T, TBackoff> : IAsyncStreamable<T>
    where T : unmanaged
    where TBackoff : struct, ISpinBackoff
{
    private readonly RingCore<T, TBackoff> _core;

    internal RingStreamer(RingCore<T, TBackoff> core) => _core = core;

    public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
    {
        _core.Lifecycle.ThrowIfTerminated();

        if (_core.HeadCommitted.Value > _core.TailCommitted.Value)
        {
            return new ValueTask<bool>(true);
        }

        if (_core.Lifecycle.IsCompleted && _core.HeadCommitted.Value == _core.TailCommitted.Value)
        {
            return new ValueTask<bool>(false);
        }

        return WaitToReadCoreAsync(cancellationToken);
    }

    private async ValueTask<bool> WaitToReadCoreAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _core.Lifecycle.ThrowIfTerminated();

            if (_core.HeadCommitted.Value > _core.TailCommitted.Value)
            {
                return true;
            }

            if (_core.Lifecycle.IsCompleted && _core.HeadCommitted.Value == _core.TailCommitted.Value)
            {
                return false;
            }

            await _core.ReadSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = default)
    {
        _core.Lifecycle.ThrowIfTerminated();

        if (_core.Lifecycle.IsCompleted)
        {
            return new ValueTask<bool>(false);
        }

        if ((ulong)(_core.HeadReserved.Value - _core.TailReserved.Value) < _core.Capacity.Value)
        {
            return new ValueTask<bool>(true);
        }

        return WaitToWriteCoreAsync(cancellationToken);
    }

    private async ValueTask<bool> WaitToWriteCoreAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _core.Lifecycle.ThrowIfTerminated();

            if (_core.Lifecycle.IsCompleted)
            {
                return false;
            }

            if ((ulong)(_core.HeadReserved.Value - _core.TailReserved.Value) < _core.Capacity.Value)
            {
                return true;
            }

            await _core.WriteSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async IAsyncEnumerable<T> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _core.Lifecycle.ThrowIfTerminated();

            if (_core.HeadCommitted.Value > _core.TailCommitted.Value)
            {
                long tail = _core.TailReserved.Value;
                if (tail < _core.HeadCommitted.Value)
                {
                    if (_core.TailReserved.CompareExchange(tail + 1, tail))
                    {
                        nuint mask = _core.Capacity.Mask;
                        nuint index = (nuint)tail & mask;
                        T item;
                        unsafe { item = *(_core.Storage.BasePointer + index); }
                        _core.DrainCommitRead((ulong)tail, 1);
                        yield return item;
                        continue;
                    }
                }
            }

            if (_core.Lifecycle.IsCompleted && _core.HeadCommitted.Value == _core.TailCommitted.Value)
            {
                yield break;
            }

            await _core.ReadSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
