namespace Axrone.Utility.Concurrency;

/// <summary>
/// Striped sync roots for operations without a native interlocked form (types over 8 bytes).
/// The same table backs wait/notify rendezvous, so fallback-locked updates and waits on one
/// address always meet on one root.
/// </summary>
internal static class SharedFallbackLock
{
    private const int TableSize = 512;
    private const int TableMask = TableSize - 1;

    private static readonly object[] s_roots = CreateRoots();

    private static object[] CreateRoots()
    {
        var roots = new object[TableSize];
        for (int i = 0; i < roots.Length; i++)
        {
            roots[i] = new object();
        }

        return roots;
    }

    /// <summary>Returns the shared root guarding the given location.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object For<T>(ref T location)
        where T : unmanaged
    {
        unsafe
        {
            return ForAddress((void*)Unsafe.AsPointer(ref location));
        }
    }

    /// <summary>Returns the shared root guarding the given address.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe object ForAddress(void* address)
    {
        nuint mixed = (nuint)address;
        mixed ^= mixed >> 9;
        mixed ^= mixed >> 18;
        return s_roots[(int)(mixed & (nuint)TableMask)];
    }
}
