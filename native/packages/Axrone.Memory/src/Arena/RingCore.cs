using Axrone.Utility.Alignment;

namespace Axrone.Memory.Arena;

internal sealed unsafe class RingCore<T, TBackoff> : IArenaCommitCoordinator<T>, IDisposable
    where T : unmanaged
    where TBackoff : struct, IBackoffPolicy
{
    public readonly BufferCapacity Capacity;
    public readonly bool ZeroOnRecycle;
    public readonly IArenaStorageBlock<T> Storage;
    public readonly ArenaTelemetryEngine Telemetry;
    public readonly AtomicLifecycleCoordinator Lifecycle;

    public AlignedAtomicCounter128 HeadReserved;
    public AlignedAtomicCounter128 HeadCommitted;
    public AlignedAtomicCounter128 TailReserved;
    public AlignedAtomicCounter128 TailCommitted;

    public readonly AsyncAutoResetSignal ReadSignal = new();
    public readonly AsyncAutoResetSignal WriteSignal = new();

    private int _disposed;

    public RingCore(
        BufferCapacity capacity,
        MemoryTopology topology,
        Alignment alignment,
        bool zeroOnRecycle,
        string meterName,
        string instanceName)
    {
        Capacity = capacity;
        ZeroOnRecycle = zeroOnRecycle;
        Lifecycle = new AtomicLifecycleCoordinator();
        Telemetry = new ArenaTelemetryEngine(meterName, instanceName);

        Storage = topology switch
        {
            MemoryTopology.NativeAligned => new NativeMemoryStorageBlock<T>(capacity.Value, alignment),
            MemoryTopology.PinnedObjectHeap => new PinnedObjectHeapStorageBlock<T>(capacity.Value),
            _ => throw new NotSupportedException()
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CommitWrite(ulong sequence, int count)
    {
        SpinWaitCommitHead((long)sequence, count);
        Telemetry.RecordAllocation((ulong)count, (nuint)(count * sizeof(T)));
        Lifecycle.ReleaseLease();
        ReadSignal.Signal();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void AbandonWrite(ulong sequence, int count)
    {
        ZeroRingRange((nuint)sequence, (nuint)count);
        SpinWaitCommitHead((long)sequence, count);
        Lifecycle.ReleaseLease();
        ReadSignal.Signal();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CommitRead(ulong sequence, int count)
    {
        if (ZeroOnRecycle)
        {
            ZeroRingRange((nuint)sequence, (nuint)count);
        }

        SpinWaitCommitTail((long)sequence, count);
        Lifecycle.ReleaseLease();
        WriteSignal.Signal();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void AbandonRead(ulong sequence, int count)
    {
        SpinWaitCommitTail((long)sequence, count);
        Lifecycle.ReleaseLease();
        WriteSignal.Signal();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal void DrainCommitRead(ulong sequence, int count)
    {
        if (ZeroOnRecycle)
        {
            ZeroRingRange((nuint)sequence, (nuint)count);
        }

        SpinWaitCommitTail((long)sequence, count);
        WriteSignal.Signal();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void SpinWaitCommitHead(long sequence, int count)
    {
        int spinCount = 0;
        long startTimestamp = Stopwatch.GetTimestamp();

        while (HeadCommitted.CompareExchange(sequence + count, sequence) == false)
        {
            TBackoff.Step(ref spinCount);

            if ((spinCount & 0x3FF) == 0 && spinCount > 0)
            {
                double elapsedNs = (double)(Stopwatch.GetTimestamp() - startTimestamp) / Stopwatch.Frequency * 1_000_000_000.0;
                Telemetry.RecordCommitLatency(elapsedNs);

                if (spinCount >= 10_000)
                {
                    Telemetry.RecordCommitStall();
                    spinCount = 0;
                }
            }
        }

        double totalNs = (double)(Stopwatch.GetTimestamp() - startTimestamp) / Stopwatch.Frequency * 1_000_000_000.0;
        Telemetry.RecordCommitLatency(totalNs);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void SpinWaitCommitTail(long sequence, int count)
    {
        int spinCount = 0;
        long startTimestamp = Stopwatch.GetTimestamp();

        while (TailCommitted.CompareExchange(sequence + count, sequence) == false)
        {
            TBackoff.Step(ref spinCount);

            if ((spinCount & 0x3FF) == 0 && spinCount > 0)
            {
                double elapsedNs = (double)(Stopwatch.GetTimestamp() - startTimestamp) / Stopwatch.Frequency * 1_000_000_000.0;
                Telemetry.RecordCommitLatency(elapsedNs);

                if (spinCount >= 10_000)
                {
                    Telemetry.RecordCommitStall();
                    spinCount = 0;
                }
            }
        }

        double totalNs = (double)(Stopwatch.GetTimestamp() - startTimestamp) / Stopwatch.Frequency * 1_000_000_000.0;
        Telemetry.RecordCommitLatency(totalNs);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void ZeroRingRange(nuint sequence, nuint count)
    {
        nuint mask = Capacity.Mask;
        nuint start = sequence & mask;
        nuint firstLen = Math.Min(count, Capacity.Value - start);
        nuint elementSize = (nuint)sizeof(T);

        NativeMemory.Clear(Storage.BasePointer + start, firstLen * elementSize);

        nuint secondLen = count - firstLen;
        if (secondLen > 0)
        {
            NativeMemory.Clear(Storage.BasePointer, secondLen * elementSize);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            Lifecycle.TransitionDisposed();
            Storage.Dispose();
            Telemetry.Dispose();
        }
    }
}
