using System.Runtime.ExceptionServices;

namespace Axrone.Memory.Arena;

internal sealed class AtomicLifecycleCoordinator
{
    private long _stateAndLeases;
    private ExceptionDispatchInfo? _terminalFault;
    private readonly AsyncAutoResetSignal _drainSignal = new();

    private const int StateActive = 0;
    private const int StateCompleting = 1;
    private const int StateFaulted = 2;
    private const int StateDrained = 3;
    private const int StateDisposed = 4;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AcquireLease()
    {
        while (true)
        {
            long current = Volatile.Read(ref _stateAndLeases);
            int state = (int)(current >> 32);
            uint leases = (uint)(current & 0xFFFFFFFF);

            if (state == StateFaulted)
            {
                Volatile.Read(ref _terminalFault)?.Throw();
                ThrowHelper.ThrowLifecycleTerminated();
            }

            if (state >= StateCompleting)
            {
                ThrowHelper.ThrowLifecycleTerminated();
            }

            long updated = ((long)state << 32) | (leases + 1);
            if (Interlocked.CompareExchange(ref _stateAndLeases, updated, current) == current)
            {
                return;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReleaseLease()
    {
        while (true)
        {
            long current = Volatile.Read(ref _stateAndLeases);
            int state = (int)(current >> 32);
            uint leases = (uint)(current & 0xFFFFFFFF);

            if (leases == 0)
            {
                ThrowHelper.ThrowInvalidOperationException("Lease underflow detected.");
            }

            uint nextLeases = leases - 1;
            long updated = ((long)state << 32) | nextLeases;

            if (Interlocked.CompareExchange(ref _stateAndLeases, updated, current) == current)
            {
                if (nextLeases == 0 && (state is StateCompleting or StateFaulted))
                {
                    TransitionDrained();
                }
                return;
            }
        }
    }

    public void Complete(Exception? error)
    {
        while (true)
        {
            long current = Volatile.Read(ref _stateAndLeases);
            int state = (int)(current >> 32);
            uint leases = (uint)(current & 0xFFFFFFFF);

            if (state >= StateCompleting)
            {
                return;
            }

            int nextState;
            if (error != null)
            {
                Volatile.Write(ref _terminalFault, ExceptionDispatchInfo.Capture(error));
                nextState = StateFaulted;
            }
            else
            {
                nextState = StateCompleting;
            }

            long updated = ((long)nextState << 32) | leases;
            if (Interlocked.CompareExchange(ref _stateAndLeases, updated, current) == current)
            {
                if (leases == 0)
                {
                    TransitionDrained();
                }
                return;
            }
        }
    }

    private void TransitionDrained()
    {
        while (true)
        {
            long current = Volatile.Read(ref _stateAndLeases);
            int state = (int)(current >> 32);
            uint leases = (uint)(current & 0xFFFFFFFF);

            if (state is StateDrained or StateDisposed)
            {
                return;
            }

            long updated = ((long)StateDrained << 32) | leases;
            if (Interlocked.CompareExchange(ref _stateAndLeases, updated, current) == current)
            {
                _drainSignal.Signal();
                return;
            }
        }
    }

    public void TransitionDisposed()
    {
        while (true)
        {
            long current = Volatile.Read(ref _stateAndLeases);
            uint leases = (uint)(current & 0xFFFFFFFF);
            long updated = ((long)StateDisposed << 32) | leases;

            if (Interlocked.CompareExchange(ref _stateAndLeases, updated, current) == current)
            {
                return;
            }
        }
    }

    public async ValueTask WaitForDrainAsync(CancellationToken cancellationToken)
    {
        long current = Volatile.Read(ref _stateAndLeases);
        int state = (int)(current >> 32);
        if (state >= StateDrained) return;

        await _drainSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public bool IsActive => ((int)(Volatile.Read(ref _stateAndLeases) >> 32)) == StateActive;
    public bool IsCompleted => ((int)(Volatile.Read(ref _stateAndLeases) >> 32)) >= StateCompleting;
    public bool IsFaulted => ((int)(Volatile.Read(ref _stateAndLeases) >> 32)) == StateFaulted;
    public uint ActiveLeaseCount => (uint)(Volatile.Read(ref _stateAndLeases) & 0xFFFFFFFF);
    public Exception? TerminalException => Volatile.Read(ref _terminalFault)?.SourceException;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ThrowIfTerminated()
    {
        int state = (int)(Volatile.Read(ref _stateAndLeases) >> 32);
        if (state == StateFaulted)
        {
            Volatile.Read(ref _terminalFault)?.Throw();
        }
        if (state == StateDisposed)
        {
            ThrowHelper.ThrowObjectDisposed("AtomicLifecycleCoordinator");
        }
    }
}
