namespace Axrone.Memory;

[StructLayout(LayoutKind.Explicit, Size = 128)]
internal struct PoolCounterCell
{
    [FieldOffset(0)] public long Tier1Hits;
    [FieldOffset(8)] public long Tier2Hits;
    [FieldOffset(16)] public long AllocatorMisses;
    [FieldOffset(24)] public long ActiveAllocations;
    [FieldOffset(32)] public long TotalAllocatedBytes;
    [FieldOffset(40)] public long TotalRentedBytes;
}

internal sealed class PoolCounterStore
{
    [ThreadStatic]
    private static PoolCounterStore? t_current;

    private static readonly List<WeakReference<PoolCounterStore>> s_all = new();
    private static readonly object s_gate = new();

    private PoolCounterCell _cell;

    public static PoolCounterStore Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var current = t_current;
            if (current is not null) return current;

            current = new PoolCounterStore();
            t_current = current;
            lock (s_gate) s_all.Add(new WeakReference<PoolCounterStore>(current));
            return current;
        }
    }

    public ref PoolCounterCell Cell
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _cell;
    }

    public static PoolDiagnosticsSnapshot Aggregate()
    {
        long t1 = 0, t2 = 0, miss = 0, active = 0, alloc = 0, rented = 0;

        lock (s_gate)
        {
            for (int i = s_all.Count - 1; i >= 0; i--)
            {
                if (s_all[i].TryGetTarget(out var store))
                {
                    t1 += store._cell.Tier1Hits;
                    t2 += store._cell.Tier2Hits;
                    miss += store._cell.AllocatorMisses;
                    active += store._cell.ActiveAllocations;
                    alloc += store._cell.TotalAllocatedBytes;
                    rented += store._cell.TotalRentedBytes;
                }
                else
                {
                    s_all.RemoveAt(i);
                }
            }
        }

        return new PoolDiagnosticsSnapshot(alloc, rented, active, t1, t2, miss);
    }
}
